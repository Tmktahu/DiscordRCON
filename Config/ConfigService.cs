using System.Text.Json;
using DiscordRcon.Models;

namespace DiscordRcon.Config;

public class ConfigService
{
  public string DiscordBotToken { get; private set; } = "";
  public ulong DiscordGuildId { get; private set; }
  public string RconHost { get; private set; } = "127.0.0.1";
  public int RconPort { get; private set; } = 25575;
  public string RconPassword { get; private set; } = "";
  public int RconCommandTimeoutMs { get; private set; } = 10000;
  public bool LogDiscordEvents { get; private set; } = false;
  public bool LogRconEvents { get; private set; } = false;

  public RoleConfig RoleConfig { get; private set; } = new();
  public List<CustomCommand> CustomCommands { get; private set; } = new();

  public bool IsFirstRun { get; private set; }

  public static readonly string ConfigDir = Path.Combine(
    BepInEx.Paths.ConfigPath, "DiscordRcon");
  public static readonly string RoleConfigPath = Path.Combine(ConfigDir, "discordrcon_roles.json");
  public static readonly string CustomCommandsPath = Path.Combine(ConfigDir, "discordrcon_commands.json");

  public void Initialize()
  {
    Directory.CreateDirectory(ConfigDir);

    BindConfig();
    DetectFirstRun();
    LoadRoleConfig();
    LoadCustomCommands();
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

    RconHost = cfg.Bind("RCON", "Host", "127.0.0.1",
      "RCON server hostname").Value;

    RconPort = cfg.Bind("RCON", "Port", 25575,
      "RCON server port").Value;

    RconPassword = cfg.Bind("RCON", "Password", "",
      "RCON server password").Value;

    RconCommandTimeoutMs = cfg.Bind("RCON", "CommandTimeoutMs", 10000,
      "Timeout in milliseconds for RCON command responses").Value;

    LogDiscordEvents = cfg.Bind("Debug", "LogDiscordEvents", false,
      "Verbose logging of Discord gateway events").Value;

    LogRconEvents = cfg.Bind("Debug", "LogRconEvents", false,
      "Verbose logging of RCON events").Value;
  }

  void LoadRoleConfig()
  {
    if (!File.Exists(RoleConfigPath))
    {
      var defaultConfig = new RoleConfig();
      SaveRoleConfig(defaultConfig);
      Core.Log.LogInfo($"Created role config at {RoleConfigPath}");
    }

    try
    {
      var json = File.ReadAllText(RoleConfigPath);
      RoleConfig = JsonSerializer.Deserialize<RoleConfig>(json) ?? new RoleConfig();
      Core.Log.LogInfo($"Loaded role config: {RoleConfig.AdminRoles.Count} admin roles, {RoleConfig.CommandRoles.Count} command roles");
    }
    catch (Exception e)
    {
      Core.Log.LogError($"Failed to load role config: {e.Message}. Fix or delete the file and restart: {RoleConfigPath}");
      throw;
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

  void LoadCustomCommands()
  {
    if (!File.Exists(CustomCommandsPath))
    {
      var defaults = new List<CustomCommand>
      {
        new() { Name = "kick", Description = "Kick a player from the server", RconCommand = "kick" },
        new() { Name = "ban", Description = "Ban a player from the server", RconCommand = "ban" },
        new() { Name = "listplayers", Description = "List all online players", RconCommand = "listplayers" }
      };
      SaveCustomCommands(defaults);
      Core.Log.LogInfo($"Created custom commands config at {CustomCommandsPath}");
    }

    try
    {
      var json = File.ReadAllText(CustomCommandsPath);
      CustomCommands = JsonSerializer.Deserialize<List<CustomCommand>>(json) ?? new List<CustomCommand>();
      Core.Log.LogInfo($"Loaded custom commands: {CustomCommands.Count} commands");
    }
    catch (Exception e)
    {
      Core.Log.LogError($"Failed to load custom commands: {e.Message}. Fix or delete the file and restart: {CustomCommandsPath}");
      throw;
    }
  }

  void SaveCustomCommands(List<CustomCommand> commands)
  {
    try
    {
      var json = JsonSerializer.Serialize(commands, new JsonSerializerOptions { WriteIndented = true });
      File.WriteAllText(CustomCommandsPath, json);
    }
    catch (Exception e)
    {
      Core.Log.LogError($"Failed to save custom commands: {e.Message}");
    }
  }
}
