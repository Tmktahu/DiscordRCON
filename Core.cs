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

    try
    {
      ConfigService.Initialize();
    }
    catch (Exception e)
    {
      Log.LogError($"Config failed to load: {e.Message}. DiscordRcon will not start.");
      return;
    }

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
    Log.LogWarning("   - Add your Discord role IDs to adminRoles");
    Log.LogWarning("   - Or add per-command grants in commandRoles");
    Log.LogWarning("");
    Log.LogWarning("3. Optionally edit the custom commands file:");
    Log.LogWarning($"   {ConfigService.CustomCommandsPath}");
    Log.LogWarning("   - Add or remove slash command shortcuts for RCON commands");
    Log.LogWarning("");
    Log.LogWarning("4. Restart the server");
    Log.LogWarning("========================================");
  }

  static void LogStartupStatus()
  {
    var cfg = ConfigService;
    Log.LogInfo("--- DiscordRcon Startup Status ---");
    Log.LogInfo($"  Discord: {(string.IsNullOrEmpty(cfg.DiscordBotToken) ? "NOT CONFIGURED" : "Token set")}");
    Log.LogInfo($"  Discord Guild: {(cfg.DiscordGuildId == 0 ? "NOT SET" : cfg.DiscordGuildId.ToString())}");
    Log.LogInfo($"  RCON: {cfg.RconHost}:{cfg.RconPort} (password {(string.IsNullOrEmpty(cfg.RconPassword) ? "NOT SET" : "set")})");
    Log.LogInfo($"  Role Config: {cfg.RoleConfig.AdminRoles.Count} admin roles, {cfg.RoleConfig.CommandRoles.Count} command roles");
    Log.LogInfo($"  Custom Commands: {cfg.CustomCommands.Count} commands");
    Log.LogInfo("-----------------------------------");
  }

  public static void Shutdown()
  {
    DiscordBotService?.Shutdown();
    RconService?.Shutdown();
  }
}
