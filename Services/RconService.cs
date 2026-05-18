namespace DiscordRcon.Services;

public class RconService
{
  public async Task<RconResult> SendCommandAsync(string command, int? timeoutMs = null)
  {
    var cfg = Core.ConfigService;

    if (string.IsNullOrEmpty(cfg.RconPassword))
    {
      return RconResult.Fail("RCON password not configured");
    }

    var timeout = timeoutMs ?? cfg.RconCommandTimeoutMs;

    using var client = new RconClient();

    try
    {
      await client.ConnectAsync(cfg.RconHost, cfg.RconPort, cfg.RconPassword);
      var response = await client.SendCommandAsync(command, timeout);

      if (Core.ConfigService.LogRconEvents)
      {
        Core.Log.LogInfo($"RCON [{command}] -> {response}");
      }

      return RconResult.Ok(string.IsNullOrEmpty(response) ? "Command executed (no output)" : response);
    }
    catch (OperationCanceledException)
    {
      return RconResult.Fail("RCON command timed out");
    }
    catch (Exception e)
    {
      Core.Log.LogWarning($"RCON failed: {e.Message}");
      return RconResult.Fail($"RCON not ready: {e.Message}");
    }
  }

  public void Shutdown()
  {
  }
}

public readonly struct RconResult
{
  public bool Success { get; }
  public string Message { get; }

  RconResult(bool success, string message)
  {
    Success = success;
    Message = message;
  }

  public static RconResult Ok(string message) => new(true, message);
  public static RconResult Fail(string message) => new(false, message);
}
