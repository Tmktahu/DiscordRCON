using System.Text.RegularExpressions;
using DSharpPlus;
using DSharpPlus.Entities;
using DiscordRcon.Models;

namespace DiscordRcon.Services;

public class CommandDiscoveryService
{
  DiscordClient _client;
  ulong _guildId;
  Timer _pollTimer;
  bool _discoveryRunning;

  static readonly Regex _ansiRegex = new(@"\x1b\[\d+(?:;\d+)*m", RegexOptions.Compiled);

  public void SetSlashExtension(DiscordClient client, ulong guildId)
  {
    _client = client;
    _guildId = guildId;
  }

  public void RunDiscovery()
  {
    if (_discoveryRunning) return;
    if (!Core.ConfigService.DiscoveryEnabled) return;

    _ = DiscoverAndRetryAsync();
  }

  async Task DiscoverAndRetryAsync()
  {
    var success = await DiscoverAndRegisterAsync();

    if (!success)
    {
      Core.Log.LogInfo("Discovery failed, retrying in 30s...");
      await Task.Delay(30_000);

      if (Core.ConfigService.DiscoveryEnabled)
      {
        _ = DiscoverAndRetryAsync();
      }
      return;
    }

    StartPolling();
  }

  public void StartPolling()
  {
    if (!Core.ConfigService.DiscoveryEnabled) return;
    if (_pollTimer != null) return;

    var interval = Core.ConfigService.DiscoveryPollIntervalSeconds * 1000;
    _pollTimer = new Timer(_ => RunPeriodicDiscovery(), null, interval, interval);
  }

  void RunPeriodicDiscovery()
  {
    if (_discoveryRunning) return;
    _ = DiscoverAndRegisterAsync();
  }

  async Task<bool> DiscoverAndRegisterAsync()
  {
    _discoveryRunning = true;

    try
    {
      var result = await Core.RconService.SendCommandAsync("help", 30000);
      if (!result.Success)
      {
        Core.Log.LogWarning($"Discovery failed: {result.Message}");
        return false;
      }

      var plainText = StripAnsi(result.Message);
      var commands = ParseScarletHelpOutput(plainText);
      await BulkRegisterSlashCommandsAsync(commands);
      return true;
    }
    catch (Exception e)
    {
      Core.Log.LogError($"Discovery error: {e.Message}");
      return false;
    }
    finally
    {
      _discoveryRunning = false;
    }
  }

  static string StripAnsi(string input)
  {
    return _ansiRegex.Replace(input, "");
  }

  List<DiscoveredCommand> ParseScarletHelpOutput(string helpText)
  {
    var commands = new Dictionary<string, DiscoveredCommand>(StringComparer.OrdinalIgnoreCase);

    foreach (var line in helpText.Split('\n'))
    {
      var trimmed = line.Trim();
      if (string.IsNullOrEmpty(trimmed)) continue;

      if (trimmed.StartsWith("Total commands:", StringComparison.OrdinalIgnoreCase)) continue;
      if (!trimmed.StartsWith("-")) continue;

      var body = trimmed[1..].Trim();

      var colonIdx = body.IndexOf(':');
      if (colonIdx < 0) continue;

      var name = body[..colonIdx].Trim();
      if (string.IsNullOrEmpty(name)) continue;
      if (!IsValidCommandName(name)) continue;

      var detail = body[(colonIdx + 1)..].Trim();

      if (commands.TryGetValue(name, out var existing))
      {
        existing.AddOverload(detail);
      }
      else
      {
        commands[name] = new DiscoveredCommand(name, detail);
      }
    }

    return commands.Values.ToList();
  }

  static bool IsValidCommandName(string name)
  {
    if (name.Length < 1 || name.Length > 32) return false;
    return name.All(c => char.IsLetterOrDigit(c) || c == '_') && name.All(c => c <= 127);
  }

  static string SanitizeSlashName(string name)
  {
    var sanitized = new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
    if (sanitized.Length > 32) sanitized = sanitized[..32];
    return sanitized.ToLower();
  }

  async Task BulkRegisterSlashCommandsAsync(List<DiscoveredCommand> commands)
  {
    if (_client == null || _guildId == 0) return;

    try
    {
      var slashCommands = new List<DiscordApplicationCommand>();

      foreach (var cmd in commands)
      {
        var slashName = SanitizeSlashName(cmd.Name);
        if (string.IsNullOrEmpty(slashName)) continue;

        var desc = cmd.Description;
        if (desc.Length > 100) desc = desc[..97] + "...";

        var option = new DiscordApplicationCommandOption(
          "arguments", "Command arguments", ApplicationCommandOptionType.String,
          false);

        slashCommands.Add(new DiscordApplicationCommand(slashName, desc, new[] { option }));
      }

      await _client.BulkOverwriteGuildApplicationCommandsAsync(_guildId, slashCommands);

      Core.Log.LogInfo($"Discovery sync: registered {slashCommands.Count} slash commands (bulk replace)");
    }
    catch (Exception e)
    {
      Core.Log.LogError($"Failed to bulk register slash commands: {e.Message}");
    }
  }

  public void Shutdown()
  {
    _pollTimer?.Dispose();
  }
}
