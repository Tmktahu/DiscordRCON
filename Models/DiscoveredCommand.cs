namespace DiscordRcon.Models;

public class DiscoveredCommand
{
  public string CommandId { get; set; } = "";
  public string Usage { get; set; } = "";
  public string Description { get; set; } = "";

  public DiscoveredCommand(string commandId, string usage, string description)
  {
    CommandId = commandId;
    Usage = usage;
    Description = description;
  }
}
