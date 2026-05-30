namespace DiscordRcon.Models;

public class DiscoveredCommand
{
  public string CommandId { get; set; } = "";
  public string Usage { get; set; } = "";
  public string Description { get; set; } = "";
  public string Category { get; set; } = "";

  public DiscoveredCommand(string commandId, string usage, string description, string category = "")
  {
    CommandId = commandId;
    Usage = usage;
    Description = description;
    Category = category;
  }
}
