using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using UnityEngine;

namespace DiscordRcon;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("markvaaz.ScarletRCON")]
internal class Plugin : BasePlugin
{
  internal static Plugin Instance { get; private set; }
  public static ManualLogSource LogInstance => Instance.Log;
  public static bool IsServer { get; private set; }

  public override void Load()
  {
    Instance = this;
    IsServer = Application.productName == "VRisingServer";

    if (!IsServer)
    {
      Log.LogWarning("DiscordRcon is a server-only plugin. It will not function on the client.");
      return;
    }

    Core.Initialize();

    Log.LogInfo($"{MyPluginInfo.PLUGIN_NAME} v{MyPluginInfo.PLUGIN_VERSION} loaded successfully!");
  }

  public override bool Unload()
  {
    if (IsServer)
    {
      Core.Shutdown();
    }

    return true;
  }
}
