using System.Text.Json;
using BepInEx.Configuration;
using DiscordRcon.Models;

namespace DiscordRcon.Config;

public class ConfigService
{
  public string DiscordBotToken { get; private set; } = "";
  public ulong DiscordGuildId { get; private set; }
  public string CommandPrefix { get; private set; } = "!rcon";
  public string RconHost { get; private set; } = "127.0.0.1";
  public int RconPort { get; private set; } = 25575;
  public string RconPassword { get; private set; } = "";
  public int RconCommandTimeoutMs { get; private set; } = 10000;
  public bool DiscoveryEnabled { get; private set; } = true;
  public int DiscoveryPollIntervalSeconds { get; private set; } = 300;
  public bool LogDiscordEvents { get; private set; } = false;
  public bool LogRconEvents { get; private set; } = false;

  public RoleConfig RoleConfig { get; private set; } = new();

  public bool IsFirstRun { get; private set; }

  public static readonly string ConfigDir = Path.Combine(
    BepInEx.Paths.ConfigPath, "DiscordRcon");
  public static readonly string RoleConfigPath = Path.Combine(ConfigDir, "discordrcon_roles.json");

  public void Initialize()
  {
    Directory.CreateDirectory(ConfigDir);

    BindConfig();
    DetectFirstRun();
    LoadRoleConfig();
  }

  void DetectFirstRun()
  {
    IsFirstRun = string.IsNullOrEmpty(DiscordBotToken)
      && DiscordGuildId == 0
      && string.IsNullOrEmpty(RconPassword);
  }

  void BindConfig()
  {
    var cfg = Plugin.Instance.Config;

    DiscordBotToken = cfg.Bind("Discord", "BotToken", "",
      "Discord bot token. Get this from https://discord.com/developers/applications").Value;

    DiscordGuildId = cfg.Bind("Discord", "GuildId", 0ul,
      "Discord server (guild) ID for registering slash commands").Value;

    CommandPrefix = cfg.Bind("Discord", "CommandPrefix", "!rcon",
      "Prefix for text commands in Discord").Value;

    RconHost = cfg.Bind("RCON", "Host", "127.0.0.1",
      "RCON server hostname").Value;

    RconPort = cfg.Bind("RCON", "Port", 25575,
      "RCON server port").Value;

    RconPassword = cfg.Bind("RCON", "Password", "",
      "RCON server password").Value;

    RconCommandTimeoutMs = cfg.Bind("RCON", "CommandTimeoutMs", 10000,
      "Timeout in milliseconds for RCON command responses").Value;

    DiscoveryEnabled = cfg.Bind("Discovery", "Enabled", true,
      "Enable dynamic slash command discovery from RCON help output").Value;

    DiscoveryPollIntervalSeconds = cfg.Bind("Discovery", "PollIntervalSeconds", 300,
      "How often to re-poll RCON for new commands (seconds)").Value;

    LogDiscordEvents = cfg.Bind("Debug", "LogDiscordEvents", false,
      "Verbose logging of Discord gateway events").Value;

    LogRconEvents = cfg.Bind("Debug", "LogRconEvents", false,
      "Verbose logging of RCON events").Value;
  }

  void LoadRoleConfig()
  {
    if (!File.Exists(RoleConfigPath))
    {
      var defaultConfig = new RoleConfig
      {
        DefaultRoles = new List<string>(),
        CommandOverrides = new Dictionary<string, List<string>>()
      };

      SaveRoleConfig(defaultConfig);
      Core.Log.LogInfo($"Created default role config at {RoleConfigPath}");
    }

    try
    {
      var json = File.ReadAllText(RoleConfigPath);
      RoleConfig = JsonSerializer.Deserialize<RoleConfig>(json) ?? new RoleConfig();
      Core.Log.LogInfo($"Loaded role config: {RoleConfig.DefaultRoles.Count} default roles, {RoleConfig.CommandOverrides.Count} command overrides");
    }
    catch (Exception e)
    {
      Core.Log.LogError($"Failed to load role config: {e.Message}");
      RoleConfig = new RoleConfig();
    }
  }

  void SaveRoleConfig(RoleConfig config)
  {
    try
    {
      var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
      File.WriteAllText(RoleConfigPath, json);
    }
    catch (Exception e)
    {
      Core.Log.LogError($"Failed to save role config: {e.Message}");
    }
  }

  public void ReloadRoleConfig()
  {
    LoadRoleConfig();
  }
}
