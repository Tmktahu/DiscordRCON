namespace DiscordRcon.Models;

public class DiscoveredCommand
{
  public string Name { get; set; } = "";
  public string Description { get; set; } = "";
  List<string> _overloads = new();

  public DiscoveredCommand() { }

  public DiscoveredCommand(string name, string detail)
  {
    Name = name;
    _overloads.Add(detail);
    Description = BuildDescription();
  }

  public void AddOverload(string detail)
  {
    _overloads.Add(detail);
    Description = BuildDescription();
  }

  string BuildDescription()
  {
    if (_overloads.Count == 0)
      return $"RCON command: {Name}";

    if (_overloads.Count == 1)
    {
      var d = _overloads[0];
      return string.IsNullOrEmpty(d) ? $"RCON command: {Name}" : d;
    }

    var parts = _overloads.Where(o => !string.IsNullOrEmpty(o)).ToList();
    return parts.Count == 0 ? $"RCON command: {Name}" : string.Join(" | ", parts);
  }
}
