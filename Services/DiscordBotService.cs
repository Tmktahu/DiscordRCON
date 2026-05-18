using System.Reflection;
using System.Text.RegularExpressions;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

namespace DiscordRcon.Services;

public class DiscordBotService
{
  DiscordClient _client;
  bool _shuttingDown;

  static readonly FieldInfo _roleIdField = typeof(DiscordMember)
    .GetField("_role_ids", BindingFlags.Instance | BindingFlags.NonPublic);

  static readonly Regex _ansiRegex = new(@"\x1b\[(\d+(?:;\d+)*)m", RegexOptions.Compiled);

  static string AnsiToMarkdown(string input)
  {
    var result = "";
    var remaining = input;
    var inBold = false;

    while (remaining.Length > 0)
    {
      var match = _ansiRegex.Match(remaining);
      if (!match.Success)
      {
        result += remaining;
        break;
      }

      result += remaining[..match.Index];

      var codes = match.Groups[1].Value.Split(';');
      var isReset = codes.Contains("0");

      if (isReset)
      {
        if (inBold) { result += "**"; inBold = false; }
      }
      else
      {
        foreach (var code in codes)
        {
          if (code == "97" && !inBold) { result += "**"; inBold = true; }
        }
      }

      remaining = remaining[(match.Index + match.Length)..];
    }

    if (inBold) result += "**";

    return result;
  }

  static List<ulong> GetMemberRoleIds(DiscordMember member)
  {
    try
    {
      return member.Roles.Select(r => r.Id).ToList();
    }
    catch (KeyNotFoundException)
    {
      Core.Log.LogWarning($"[Perm] DiscordMember.Roles threw KeyNotFoundException for {member.Username}({member.Id}), falling back to _role_ids");
    }

    if (_roleIdField == null)
    {
      Core.Log.LogWarning("[Perm] _role_ids field not found via reflection");
      return null;
    }

    try
    {
      var ids = (List<ulong>)_roleIdField.GetValue(member);
      Core.Log.LogInfo($"[Perm] read {ids.Count} role IDs from _role_ids: [{string.Join(", ", ids)}]");
      return ids;
    }
    catch (Exception ex)
    {
      Core.Log.LogWarning($"[Perm] failed to read _role_ids: {ex.Message}");
      return null;
    }
  }

  public void Initialize()
  {
    var cfg = Core.ConfigService;

    if (string.IsNullOrEmpty(cfg.DiscordBotToken))
    {
      Core.Log.LogWarning("Discord bot token is not configured. Set Discord.BotToken in the config and restart.");
      return;
    }

    if (cfg.DiscordGuildId == 0)
    {
      Core.Log.LogWarning("Discord guild ID is not configured. Set Discord.GuildId in the config and restart.");
      return;
    }

    _client = new DiscordClient(new DiscordConfiguration
    {
      Token = cfg.DiscordBotToken,
      TokenType = TokenType.Bot,
      Intents = DiscordIntents.Guilds | DiscordIntents.GuildMessages
        | DiscordIntents.MessageContents | DiscordIntents.GuildMembers
    });

    _client.MessageCreated += OnMessageCreated;
    _client.InteractionCreated += OnInteractionCreated;
    _client.SocketErrored += OnSocketError;
    _client.SocketClosed += OnSocketClosed;
    _client.Ready += OnReady;
    _client.GuildAvailable += OnGuildAvailable;

    _ = ConnectAsync();
  }

  async Task ConnectAsync()
  {
    try
    {
      await _client.ConnectAsync();
      Core.Log.LogInfo("Discord bot connecting...");
    }
    catch (Exception e)
    {
      Core.Log.LogError($"Failed to connect Discord bot: {e.Message}");
    }
  }

  Task OnReady(DiscordClient sender, ReadyEventArgs e)
  {
    Core.Log.LogInfo($"Discord bot is ready, guilds in cache: {sender.Guilds.Count} [{string.Join(", ", sender.Guilds.Keys)}]");

    if (sender.Guilds.Count == 0)
    {
      Core.Log.LogWarning("Discord guild cache is empty. Check that GUILD_MEMBERS privileged intent is enabled in the Discord Developer Portal.");
    }

    Core.CommandDiscoveryService.SetSlashExtension(_client, Core.ConfigService.DiscordGuildId);

    if (Core.ConfigService.DiscoveryEnabled)
    {
      Core.Log.LogInfo("Discovery will run in 60s");
      _ = Task.Run(async () =>
      {
        await Task.Delay(60_000);
        Core.CommandDiscoveryService.RunDiscovery();
      });
    }

    return Task.CompletedTask;
  }

  Task OnGuildAvailable(DiscordClient sender, GuildCreateEventArgs e)
  {
    Core.Log.LogInfo($"Discord guild available: {e.Guild.Name}({e.Guild.Id}) members={e.Guild.MemberCount}");

    return Task.CompletedTask;
  }

  async Task OnMessageCreated(DiscordClient sender, MessageCreateEventArgs e)
  {
    if (e.Author.IsBot) return;

    var prefix = Core.ConfigService.CommandPrefix;
    if (!e.Message.Content.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return;

    var commandText = e.Message.Content[prefix.Length..].Trim();
    if (string.IsNullOrEmpty(commandText)) return;

    Core.Log.LogInfo($"[MsgCmd] user={e.Author.Username}({e.Author.Id}) command=\"{commandText}\"");

    if (!await HasPermission(e.Author, e.Guild?.Id, commandText))
    {
      await e.Message.RespondAsync("You don't have permission to use that command.");
      return;
    }

    var result = await Core.RconService.SendCommandAsync(commandText);
    var response = AnsiToMarkdown(result.Message);
    await RespondAsync(e.Message, response, !result.Success);
  }

  async Task OnInteractionCreated(DiscordClient sender, InteractionCreateEventArgs e)
  {
    if (e.Interaction.Type != InteractionType.ApplicationCommand) return;

    var interaction = e.Interaction;
    var commandName = interaction.Data.Name;
    var argsOption = interaction.Data.Options?.FirstOrDefault(o => o.Name == "arguments");
    var arguments = argsOption?.Value?.ToString() ?? "";
    var commandText = string.IsNullOrEmpty(arguments) ? commandName : $"{commandName} {arguments}";

    Core.Log.LogInfo($"[SlashCmd] user={interaction.User.Username}({interaction.User.Id}) command=\"{commandText}\"");

    if (!await HasPermission(interaction.User, interaction.GuildId, commandText))
    {
      await interaction.CreateResponseAsync(
        InteractionResponseType.ChannelMessageWithSource,
        new DiscordInteractionResponseBuilder().WithContent("You don't have permission to use that command."));
      return;
    }

    var result = await Core.RconService.SendCommandAsync(commandText);
    var response = AnsiToMarkdown(result.Message);

    if (response.Length <= 2000)
    {
      await interaction.CreateResponseAsync(
        InteractionResponseType.ChannelMessageWithSource,
        new DiscordInteractionResponseBuilder().WithContent(result.Success ? response : $":x: {response}"));
    }
    else
    {
      await interaction.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
      await RespondLongAsync(interaction, response);
    }
  }

  async Task RespondLongAsync(DiscordInteraction interaction, string text)
  {
    const int maxLen = 2000;
    var lines = text.Split('\n');
    var current = "";
    var first = true;

    foreach (var line in lines)
    {
      if (current.Length + line.Length + 1 > maxLen)
      {
        if (first)
        {
          await interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder().WithContent(current));
          first = false;
        }
        else
        {
          await interaction.CreateFollowupMessageAsync(new DiscordFollowupMessageBuilder().WithContent(current));
        }
        current = line;
      }
      else
      {
        current = current.Length == 0 ? line : $"{current}\n{line}";
      }
    }

    if (current.Length > 0)
    {
      if (first)
      {
        await interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder().WithContent(current));
      }
      else
      {
        await interaction.CreateFollowupMessageAsync(new DiscordFollowupMessageBuilder().WithContent(current));
      }
    }
  }

  public async Task RespondAsync(DiscordMessage message, string text, bool isError = false)
  {
    const int maxLen = 2000;

    if (isError) text = $":x: {text}";

    if (text.Length <= maxLen)
    {
      await message.RespondAsync(text);
      return;
    }

    var lines = text.Split('\n');
    var current = "";

    foreach (var line in lines)
    {
      if (current.Length + line.Length + 1 > maxLen)
      {
        await message.RespondAsync(current);
        current = line;
      }
      else
      {
        current = current.Length == 0 ? line : $"{current}\n{line}";
      }
    }

    if (current.Length > 0)
    {
      await message.RespondAsync(current);
    }
  }

  async Task<bool> HasPermission(DiscordUser user, ulong? guildId, string commandText)
  {
    if (user == null)
    {
      Core.Log.LogWarning("[Perm] user is null, denying");
      return false;
    }

    var roleConfig = Core.ConfigService.RoleConfig;
    var rootCommand = commandText.Split(' ')[0].ToLower();

    Core.Log.LogInfo($"[Perm] user={user.Username}({user.Id}) rootCmd={rootCommand} guildId={guildId} isMember={user is DiscordMember}");

    List<ulong> roleIds = null;

    if (user is DiscordMember member)
    {
      roleIds = GetMemberRoleIds(member);
    }
    else
    {
      Core.Log.LogInfo($"[Perm] user is DiscordUser (not DiscordMember), _client.Guilds keys: [{string.Join(", ", _client.Guilds.Keys)}]");

      if (guildId.HasValue && _client.Guilds.TryGetValue(guildId.Value, out var guild))
      {
        if (guild.Members.TryGetValue(user.Id, out var cachedMember))
        {
          roleIds = GetMemberRoleIds(cachedMember);
        }
        else
        {
          Core.Log.LogWarning($"[Perm] user {user.Id} not found in guild member cache (have {guild.Members.Count} members)");
        }
      }

      if (roleIds == null && guildId.HasValue)
      {
        Core.Log.LogInfo("[Perm] falling back to REST API to fetch member roles...");
        try
        {
          var restGuild = await _client.GetGuildAsync(guildId.Value);
          var restMember = await restGuild.GetMemberAsync(user.Id);
          roleIds = GetMemberRoleIds(restMember);
          Core.Log.LogInfo($"[Perm] REST API returned {roleIds?.Count ?? 0} roles for user");
        }
        catch (Exception ex)
        {
          Core.Log.LogWarning($"[Perm] REST API fallback failed: {ex.Message}");
        }
      }
    }

    if (roleIds == null || roleIds.Count == 0)
    {
      Core.Log.LogWarning("[Perm] no role IDs resolved, denying");
      return false;
    }

    var roleIdStrings = roleIds.Select(id => id.ToString()).ToList();
    Core.Log.LogInfo($"[Perm] user roles: [{string.Join(", ", roleIdStrings)}]");
    Core.Log.LogInfo($"[Perm] config defaultRoles: [{string.Join(", ", roleConfig.DefaultRoles)}]");
    Core.Log.LogInfo($"[Perm] config commandOverrides for {rootCommand}: {(roleConfig.CommandOverrides.TryGetValue(rootCommand, out var ovr) ? string.Join(", ", ovr) : "none")}");

    if (roleConfig.CommandOverrides.TryGetValue(rootCommand, out var allowedRoles))
    {
      var match = roleIdStrings.Any(id => allowedRoles.Contains(id));
      Core.Log.LogInfo($"[Perm] command override check: match={match}");
      return match;
    }

    if (roleConfig.DefaultRoles.Count > 0)
    {
      var match = roleIdStrings.Any(id => roleConfig.DefaultRoles.Contains(id));
      Core.Log.LogInfo($"[Perm] default roles check: match={match}");
      return match;
    }

    Core.Log.LogWarning("[Perm] no default roles configured and no command override, denying");
    return false;
  }

  Task OnSocketError(DiscordClient sender, SocketErrorEventArgs e)
  {
    Core.Log.LogWarning($"Discord socket error: {e.Exception?.Message}");
    return Task.CompletedTask;
  }

  Task OnSocketClosed(DiscordClient sender, SocketCloseEventArgs e)
  {
    if (_shuttingDown) return Task.CompletedTask;

    Core.Log.LogWarning($"Discord socket closed: {e.CloseCode} - {e.CloseMessage}");
    _ = ReconnectWithBackoffAsync();
    return Task.CompletedTask;
  }

  async Task ReconnectWithBackoffAsync()
  {
    var delays = new[] { 1, 2, 4, 8, 16, 32, 60 };
    int attempt = 0;

    while (!_shuttingDown && attempt < delays.Length)
    {
      await Task.Delay(delays[attempt] * 1000);

      try
      {
        Core.Log.LogInfo($"Discord reconnect attempt {attempt + 1}...");
        await _client.ConnectAsync();
        Core.Log.LogInfo("Discord reconnected successfully");
        return;
      }
      catch (Exception e)
      {
        Core.Log.LogWarning($"Discord reconnect failed: {e.Message}");
        attempt++;
      }
    }

    Core.Log.LogError("Discord reconnect exhausted all attempts. Manual restart required.");
  }

  public void Shutdown()
  {
    _shuttingDown = true;
    _client?.DisconnectAsync().ConfigureAwait(false).GetAwaiter().GetResult();
    _client?.Dispose();
  }
}
