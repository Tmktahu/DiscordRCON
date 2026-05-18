using BepInEx.Logging;
using DiscordRcon.Config;
using DiscordRcon.Services;

namespace DiscordRcon;

internal static class Core
{
  public static ManualLogSource Log => Plugin.LogInstance;

  public static ConfigService ConfigService { get; private set; }
  public static RconService RconService { get; private set; }
  public static DiscordBotService DiscordBotService { get; private set; }
  public static CommandDiscoveryService CommandDiscoveryService { get; private set; }

  public static void Initialize()
  {
    ConfigService = new ConfigService();
    ConfigService.Initialize();

    RconService = new RconService();
    DiscordBotService = new DiscordBotService();
    CommandDiscoveryService = new CommandDiscoveryService();

    if (ConfigService.IsFirstRun)
    {
      LogFirstRunSetup();
      return;
    }

    DiscordBotService.Initialize();

    LogStartupStatus();
  }

  static void LogFirstRunSetup()
  {
    Log.LogWarning("========================================");
    Log.LogWarning("DiscordRcon - First Run Setup Required");
    Log.LogWarning("========================================");
    Log.LogWarning("This mod needs configuration before it can start.");
    Log.LogWarning("");
    Log.LogWarning("1. Edit the BepInEx config file:");
    Log.LogWarning($"   {BepInEx.Paths.ConfigPath}\\io.vrising.DiscordRcon.cfg");
    Log.LogWarning("   - Set Discord.BotToken to your bot token");
    Log.LogWarning("   - Set Discord.GuildId to your Discord server ID");
    Log.LogWarning("   - Set RCON.Password to your RCON password");
    Log.LogWarning("");
    Log.LogWarning("2. Edit the role config file:");
    Log.LogWarning($"   {ConfigService.RoleConfigPath}");
    Log.LogWarning("   - Add your Discord role IDs to defaultRoles");
    Log.LogWarning("   - Or add per-command overrides in commandOverrides");
    Log.LogWarning("");
    Log.LogWarning("3. Restart the server");
    Log.LogWarning("========================================");
  }

  static void LogStartupStatus()
  {
    var cfg = ConfigService;
    Log.LogInfo("--- DiscordRcon Startup Status ---");
    Log.LogInfo($"  Discord: {(string.IsNullOrEmpty(cfg.DiscordBotToken) ? "NOT CONFIGURED" : "Token set")}");
    Log.LogInfo($"  Discord Guild: {(cfg.DiscordGuildId == 0 ? "NOT SET" : cfg.DiscordGuildId.ToString())}");
    Log.LogInfo($"  RCON: {cfg.RconHost}:{cfg.RconPort} (password {(string.IsNullOrEmpty(cfg.RconPassword) ? "NOT SET" : "set")})");
    Log.LogInfo($"  Command Prefix: {cfg.CommandPrefix}");
    Log.LogInfo($"  Discovery: {(cfg.DiscoveryEnabled ? "enabled" : "disabled")}");
    Log.LogInfo($"  Role Config: {cfg.RoleConfig.DefaultRoles.Count} default roles, {cfg.RoleConfig.CommandOverrides.Count} command overrides");
    Log.LogInfo("-----------------------------------");
  }

  public static void Shutdown()
  {
    CommandDiscoveryService?.Shutdown();
    DiscordBotService?.Shutdown();
    RconService?.Shutdown();
  }
}
