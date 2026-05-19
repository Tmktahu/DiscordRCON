using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using DiscordRcon.Models;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

namespace DiscordRcon.Services;

public class DiscordBotService
{
  DiscordClient _client;
  bool _shuttingDown;
  Dictionary<string, CustomCommand> _customCommandMap = new(StringComparer.OrdinalIgnoreCase);

  static readonly FieldInfo _roleIdField = typeof(DiscordMember)
    .GetField("_role_ids", BindingFlags.Instance | BindingFlags.NonPublic);

  static readonly Regex _ansiRegex = new(@"\x1b\[(\d+(?:;\d+)*)m", RegexOptions.Compiled);

  static string AnsiToMarkdown(string input)
  {
    var sb = new StringBuilder(input.Length);
    var remaining = input;
    var inBold = false;

    while (remaining.Length > 0)
    {
      var match = _ansiRegex.Match(remaining);
      if (!match.Success)
      {
        sb.Append(remaining);
        break;
      }

      sb.Append(remaining[..match.Index]);

      var codes = match.Groups[1].Value.Split(';');
      var isReset = codes.Contains("0");

      if (isReset)
      {
        if (inBold) { sb.Append("**"); inBold = false; }
      }
      else
      {
        foreach (var code in codes)
        {
          if ((code == "1" || code == "97") && !inBold) { sb.Append("**"); inBold = true; }
        }
      }

      remaining = remaining[(match.Index + match.Length)..];
    }

    if (inBold) sb.Append("**");

    return sb.ToString();
  }

  static List<ulong> GetMemberRoleIds(DiscordMember member)
  {
    try
    {
      return member.Roles.Select(r => r.Id).ToList();
    }
    catch (KeyNotFoundException)
    {
      if (Core.ConfigService.LogDiscordEvents)
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
      if (Core.ConfigService.LogDiscordEvents)
        Core.Log.LogInfo($"[Perm] read {ids.Count} role IDs from _role_ids: [{string.Join(", ", ids)}]");
      return ids;
    }
    catch (Exception ex)
    {
      Core.Log.LogWarning($"[Perm] failed to read _role_ids: {ex.Message}");
      return null;
    }
  }

  static readonly Regex _validSlashName = new(@"^[a-z0-9_-]{1,32}$", RegexOptions.Compiled);

  static string FormatSuggestions(List<DiscoveredCommand> matches)
  {
    var sb = new StringBuilder();
    foreach (var cmd in matches)
    {
      sb.Append($"\n  - **{cmd.CommandId}**");
      if (!string.IsNullOrEmpty(cmd.Usage))
        sb.Append($" `{cmd.Usage}`");
      if (!string.IsNullOrEmpty(cmd.Description))
        sb.Append($" - {cmd.Description}");
    }

    return sb.ToString();
  }

  static string FormatFullListing(List<DiscoveredCommand> commands)
  {
    var sb = new StringBuilder();
    foreach (var cmd in commands)
    {
      sb.Append($"- **{cmd.CommandId}**");
      if (!string.IsNullOrEmpty(cmd.Usage))
        sb.Append($" `{cmd.Usage}`");
      if (!string.IsNullOrEmpty(cmd.Description))
        sb.Append($" - {cmd.Description}");
      sb.Append('\n');
    }

    return sb.ToString();
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
      Intents = DiscordIntents.Guilds | DiscordIntents.GuildMembers
    });

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
    }
    catch (Exception e)
    {
      Core.Log.LogError($"Failed to connect Discord bot: {e.Message}. Verify your BotToken and that the bot is not already connected elsewhere.");
    }
  }

  async Task OnReady(DiscordClient sender, ReadyEventArgs e)
  {
    Core.Log.LogInfo($"Discord bot is ready, guilds in cache: {sender.Guilds.Count}");

    if (sender.Guilds.Count == 0)
    {
      Core.Log.LogWarning("Discord guild cache is empty. Check that the bot has been added to your server.");
    }

    await RegisterSlashCommandsAsync();

    Core.Log.LogInfo("Discovery will run in 60s");
    _ = Task.Run(async () =>
    {
      await Task.Delay(60_000);
      try { Core.CommandDiscoveryService.RunDiscovery(); }
      catch (Exception ex) { Core.Log.LogError($"Discovery launch error: {ex.Message}"); }
    });
  }

  async Task RegisterSlashCommandsAsync()
  {
    try
    {
      var guildId = Core.ConfigService.DiscordGuildId;
      var commands = new List<DiscordApplicationCommand>();

      commands.Add(new DiscordApplicationCommand(
        "rcon",
        "Execute an RCON command",
        new List<DiscordApplicationCommandOption>
        {
          new("command", "The RCON command to execute", ApplicationCommandOptionType.String, true)
        }
      ));

      commands.Add(new DiscordApplicationCommand(
        "help",
        "Show available RCON commands or details for a specific command",
        new List<DiscordApplicationCommandOption>
        {
          new("command", "Command name to get help for", ApplicationCommandOptionType.String, false)
        }
      ));

      _customCommandMap.Clear();
      var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      foreach (var custom in Core.ConfigService.CustomCommands)
      {
        if (string.IsNullOrEmpty(custom.Name) || string.IsNullOrEmpty(custom.RconCommand))
        {
          Core.Log.LogWarning($"Skipping custom command with empty name or rconCommand");
          continue;
        }

        if (custom.Name == "rcon" || custom.Name == "help")
        {
          Core.Log.LogWarning($"Custom command name '{custom.Name}' conflicts with built-in commands, skipping");
          continue;
        }

        if (!_validSlashName.IsMatch(custom.Name))
        {
          Core.Log.LogWarning($"Custom command name '{custom.Name}' is invalid (must be lowercase, 1-32 chars, a-z/0-9/hyphen/underscore only), skipping");
          continue;
        }

        if (!seenNames.Add(custom.Name))
        {
          Core.Log.LogWarning($"Duplicate custom command name '{custom.Name}', skipping");
          continue;
        }

        _customCommandMap[custom.Name] = custom;
        commands.Add(new DiscordApplicationCommand(
          custom.Name,
          custom.Description ?? $"Shortcut for {custom.RconCommand}",
          new List<DiscordApplicationCommandOption>
          {
            new("arguments", $"Arguments for {custom.RconCommand}", ApplicationCommandOptionType.String, false)
          }
        ));
      }

      await _client.BulkOverwriteGuildApplicationCommandsAsync(guildId, commands);
      Core.Log.LogInfo($"Registered {commands.Count} slash commands (/rcon, /help, {_customCommandMap.Count} custom)");
    }
    catch (Exception e)
    {
      Core.Log.LogError($"Failed to register slash commands: {e.Message}");
    }
  }

  Task OnGuildAvailable(DiscordClient sender, GuildCreateEventArgs e)
  {
    if (Core.ConfigService.LogDiscordEvents)
      Core.Log.LogInfo($"Discord guild available: {e.Guild.Name}({e.Guild.Id})");
    return Task.CompletedTask;
  }

  async Task OnInteractionCreated(DiscordClient sender, InteractionCreateEventArgs e)
  {
    if (e.Interaction.Type != InteractionType.ApplicationCommand) return;

    var interaction = e.Interaction;
    var commandName = interaction.Data.Name;

    if (commandName == "rcon")
    {
      await HandleRconAsync(interaction);
    }
    else if (commandName == "help")
    {
      await HandleHelpAsync(interaction);
    }
    else if (_customCommandMap.TryGetValue(commandName, out var custom))
    {
      await HandleCustomCommandAsync(interaction, custom);
    }
  }

  async Task HandleRconAsync(DiscordInteraction interaction)
  {
    var commandOption = interaction.Data.Options?.FirstOrDefault(o => o.Name == "command");
    var commandText = commandOption?.Value?.ToString() ?? "";

    if (string.IsNullOrWhiteSpace(commandText))
    {
      await interaction.CreateResponseAsync(
        InteractionResponseType.ChannelMessageWithSource,
        new DiscordInteractionResponseBuilder().WithContent(":x: No command provided."));
      return;
    }

    var commandId = commandText.Split(' ')[0];
    Core.Log.LogInfo($"[/rcon] {interaction.User.Username}: {commandText}");

    if (!await HasPermission(interaction.User, interaction.GuildId, commandId))
    {
      await interaction.CreateResponseAsync(
        InteractionResponseType.ChannelMessageWithSource,
        new DiscordInteractionResponseBuilder().WithContent(":x: You don't have permission to use that command."));
      return;
    }

    await interaction.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

    var result = await Core.RconService.SendCommandAsync(commandText);
    var response = AnsiToMarkdown(result.Message);

    if (!result.Success)
    {
      response = $":x: {response}";
    }
    else if (IsUnknownCommand(response) && Core.CommandDiscoveryService.IsReady)
    {
      var suggestions = Core.CommandDiscoveryService.SearchCommands(commandId);
      if (suggestions.Count > 0)
      {
        response += $"\n\nDid you mean one of these?{FormatSuggestions(suggestions)}";
      }
    }

    await RespondAsync(interaction, response);
  }

  async Task HandleHelpAsync(DiscordInteraction interaction)
  {
    var commandOption = interaction.Data.Options?.FirstOrDefault(o => o.Name == "command");
    var query = commandOption?.Value?.ToString() ?? "";

    Core.Log.LogInfo($"[/help] {interaction.User.Username}: {(string.IsNullOrEmpty(query) ? "(full listing)" : query)}");

    if (string.IsNullOrEmpty(query))
    {
      if (!await HasAnyPermission(interaction.User, interaction.GuildId))
      {
        await interaction.CreateResponseAsync(
          InteractionResponseType.ChannelMessageWithSource,
          new DiscordInteractionResponseBuilder().WithContent(":x: You don't have permission to use that command."));
        return;
      }
    }
    else
    {
      if (!await HasPermission(interaction.User, interaction.GuildId, query))
      {
        await interaction.CreateResponseAsync(
          InteractionResponseType.ChannelMessageWithSource,
          new DiscordInteractionResponseBuilder().WithContent(":x: You don't have permission to use that command."));
        return;
      }
    }

    await interaction.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

    string response;

    if (Core.CommandDiscoveryService.IsReady)
    {
      if (string.IsNullOrEmpty(query))
      {
        response = FormatFullListing(Core.CommandDiscoveryService.DiscoveredCommands);
      }
      else
      {
        var exact = Core.CommandDiscoveryService.FindExact(query);
        if (exact != null)
        {
          var sb = new StringBuilder();
          sb.Append($"**{exact.CommandId}**");
          if (!string.IsNullOrEmpty(exact.Usage))
            sb.Append($"\nUsage: `{exact.Usage}`");
          if (!string.IsNullOrEmpty(exact.Description))
            sb.Append($"\n{exact.Description}");
          response = sb.ToString();
        }
        else
        {
          var matches = Core.CommandDiscoveryService.SearchCommands(query);
          if (matches.Count > 0)
          {
            response = $"No exact match for \"{query}\". Similar commands:{FormatSuggestions(matches)}";
          }
          else
          {
            response = $"No commands found matching \"{query}\".";
          }
        }
      }
    }
    else
    {
      var rconCommand = string.IsNullOrEmpty(query) ? "help" : $"help {query}";
      var result = await Core.RconService.SendCommandAsync(rconCommand);
      response = AnsiToMarkdown(result.Message);

      if (!result.Success)
        response = $":x: {response}";
    }

    await RespondAsync(interaction, response);
  }

  static bool IsUnknownCommand(string response)
  {
    return response.IndexOf("unknown command", StringComparison.OrdinalIgnoreCase) >= 0;
  }

  async Task HandleCustomCommandAsync(DiscordInteraction interaction, CustomCommand custom)
  {
    var argsOption = interaction.Data.Options?.FirstOrDefault(o => o.Name == "arguments");
    var arguments = argsOption?.Value?.ToString() ?? "";
    var commandText = string.IsNullOrEmpty(arguments) ? custom.RconCommand : $"{custom.RconCommand} {arguments}";

    Core.Log.LogInfo($"[/{custom.Name}] {interaction.User.Username}: {commandText}");

    if (!await HasPermission(interaction.User, interaction.GuildId, custom.RconCommand))
    {
      await interaction.CreateResponseAsync(
        InteractionResponseType.ChannelMessageWithSource,
        new DiscordInteractionResponseBuilder().WithContent(":x: You don't have permission to use that command."));
      return;
    }

    await interaction.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

    var result = await Core.RconService.SendCommandAsync(commandText);
    var response = AnsiToMarkdown(result.Message);

    if (!result.Success)
    {
      response = $":x: {response}";
    }
    else if (IsUnknownCommand(response) && Core.CommandDiscoveryService.IsReady)
    {
      var suggestions = Core.CommandDiscoveryService.SearchCommands(custom.RconCommand);
      if (suggestions.Count > 0)
      {
        response += $"\n\nDid you mean one of these?{FormatSuggestions(suggestions)}";
      }
    }

    await RespondAsync(interaction, response);
  }

  async Task RespondAsync(DiscordInteraction interaction, string text)
  {
    if (text.Length <= 2000)
    {
      await interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder().WithContent(text));
    }
    else
    {
      await RespondLongAsync(interaction, text);
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

  async Task<bool> HasAnyPermission(DiscordUser user, ulong? guildId)
  {
    var (resolved, roleIdStrings) = await ResolveUserRoleIds(user, guildId);
    if (!resolved) return false;

    var roleConfig = Core.ConfigService.RoleConfig;

    if (roleConfig.AdminRoles.Count > 0 && roleIdStrings.Any(id => roleConfig.AdminRoles.Contains(id)))
      return true;

    foreach (var entry in roleConfig.CommandRoles.Values)
    {
      if (roleIdStrings.Any(id => entry.Contains(id)))
        return true;
    }

    return false;
  }

  async Task<(bool resolved, List<string> roleIds)> ResolveUserRoleIds(DiscordUser user, ulong? guildId)
  {
    if (_client == null) return (false, null);

    if (user == null)
    {
      Core.Log.LogWarning("[Perm] user is null, denying");
      return (false, null);
    }

    List<ulong> roleIds = null;

    if (user is DiscordMember member)
    {
      roleIds = GetMemberRoleIds(member);
    }
    else if (guildId.HasValue)
    {
      if (_client.Guilds.TryGetValue(guildId.Value, out var guild))
      {
        if (guild.Members.TryGetValue(user.Id, out var cachedMember))
        {
          roleIds = GetMemberRoleIds(cachedMember);
        }
      }

      if (roleIds == null)
      {
        try
        {
          var restGuild = await _client.GetGuildAsync(guildId.Value);
          var restMember = await restGuild.GetMemberAsync(user.Id);
          roleIds = GetMemberRoleIds(restMember);
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
      return (false, null);
    }

    return (true, roleIds.Select(id => id.ToString()).ToList());
  }

  async Task<bool> HasPermission(DiscordUser user, ulong? guildId, string commandId)
  {
    var (resolved, roleIdStrings) = await ResolveUserRoleIds(user, guildId);
    if (!resolved) return false;

    var roleConfig = Core.ConfigService.RoleConfig;

    if (roleConfig.AdminRoles.Count > 0 && roleIdStrings.Any(id => roleConfig.AdminRoles.Contains(id)))
      return true;

    if (roleConfig.CommandRoles.TryGetValue(commandId, out var commandRoleIds))
    {
      if (roleIdStrings.Any(id => commandRoleIds.Contains(id)))
        return true;
    }

    Core.Log.LogWarning("[Perm] no admin or command role match, denying");
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
    if (_client != null)
    {
      _client.DisconnectAsync().ConfigureAwait(false).GetAwaiter().GetResult();
      _client.Dispose();
    }
  }
}
