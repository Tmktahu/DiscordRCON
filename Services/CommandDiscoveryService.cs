using System.Text.RegularExpressions;
using DiscordRcon.Models;

namespace DiscordRcon.Services;

public class CommandDiscoveryService
{
  volatile int _discoveryRunning;

  public List<DiscoveredCommand> DiscoveredCommands { get; private set; } = new();
  public bool IsReady { get; private set; }

  static readonly Regex _ansiRegex = new(@"\x1b\[\d+(?:;\d+)*m", RegexOptions.Compiled);

  public void RunDiscovery()
  {
    if (Interlocked.CompareExchange(ref _discoveryRunning, 1, 0) != 0) return;

    _ = DiscoverAndRetryAsync();
  }

  async Task DiscoverAndRetryAsync()
  {
    var attempt = 0;
    try
    {
      while (true)
      {
        var success = await DiscoverAndBuildIndexAsync();

        if (success)
        {
          IsReady = true;
          break;
        }

        attempt++;
        if (attempt >= 20)
        {
          Core.Log.LogError("Discovery failed after 20 attempts. Restart the server to retry.");
          break;
        }

        Core.Log.LogInfo($"Discovery failed (attempt {attempt}), retrying in 30s...");
        await Task.Delay(30_000);
      }
    }
    finally
    {
      Interlocked.Exchange(ref _discoveryRunning, 0);
    }
  }

  async Task<bool> DiscoverAndBuildIndexAsync()
  {
    try
    {
      var result = await Core.RconService.SendCommandAsync("help", 30000);
      if (!result.Success)
      {
        Core.Log.LogWarning($"Discovery failed: {result.Message}");
        return false;
      }

      var plainText = _ansiRegex.Replace(result.Message, "");
      var commands = ParseHelpOutput(plainText);

      DiscoveredCommands = commands;
      IsReady = true;

      Core.Log.LogInfo($"Discovery complete: indexed {commands.Count} commands");
      return true;
    }
    catch (Exception e)
    {
      Core.Log.LogError($"Discovery error: {e.Message}");
      return false;
    }
  }

  List<DiscoveredCommand> ParseHelpOutput(string helpText)
  {
    var commands = new List<DiscoveredCommand>();

    foreach (var line in helpText.Split('\n'))
    {
      var trimmed = line.Trim();
      if (string.IsNullOrEmpty(trimmed)) continue;
      if (trimmed.StartsWith("Total commands:", StringComparison.OrdinalIgnoreCase)) continue;
      if (!trimmed.StartsWith("-")) continue;

      var body = trimmed[1..].Trim();

      var colonIdx = body.IndexOf(':');
      if (colonIdx < 0) continue;

      var commandId = body[..colonIdx].Trim();
      if (string.IsNullOrEmpty(commandId)) continue;

      var detail = body[(colonIdx + 1)..].Trim();

      var usage = "";
      var description = detail;

      var dashIdx = detail.IndexOf(" - ");
      if (dashIdx >= 0)
      {
        usage = detail[..dashIdx].Trim();
        description = detail[(dashIdx + 3)..].Trim();
      }

      commands.Add(new DiscoveredCommand(commandId, usage, description));
    }

    return commands;
  }

  public List<DiscoveredCommand> SearchCommands(string query)
  {
    if (string.IsNullOrEmpty(query) || DiscoveredCommands.Count == 0)
      return new List<DiscoveredCommand>();

    var lowerQuery = query.ToLowerInvariant();

    var prefixMatches = PrefixMatches(lowerQuery);
    if (prefixMatches.Count > 0) return prefixMatches;

    var parentMatches = ParentPrefixMatches(lowerQuery);
    if (parentMatches.Count > 0) return parentMatches;

    var substringMatches = SubstringMatches(lowerQuery);
    if (substringMatches.Count > 0) return substringMatches;

    return new List<DiscoveredCommand>();
  }

  List<DiscoveredCommand> PrefixMatches(string query)
  {
    return DiscoveredCommands
      .Where(c => c.CommandId.ToLowerInvariant() == query
               || c.CommandId.ToLowerInvariant().StartsWith(query + "."))
      .ToList();
  }

  List<DiscoveredCommand> ParentPrefixMatches(string query)
  {
    var segmentIdx = query.LastIndexOf('.');
    while (segmentIdx > 0)
    {
      var parent = query[..segmentIdx];
      var matches = PrefixMatches(parent);
      if (matches.Count > 0) return matches;
      segmentIdx = parent.LastIndexOf('.');
    }

    return new List<DiscoveredCommand>();
  }

  List<DiscoveredCommand> SubstringMatches(string query)
  {
    return DiscoveredCommands
      .Where(c => c.CommandId.ToLowerInvariant().Contains(query))
      .Take(10)
      .ToList();
  }

  public DiscoveredCommand FindExact(string commandId)
  {
    return DiscoveredCommands.FirstOrDefault(c =>
      string.Equals(c.CommandId, commandId, StringComparison.OrdinalIgnoreCase));
  }
}
