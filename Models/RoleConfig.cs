using System.Text.Json.Serialization;

namespace DiscordRcon.Models;

public class RoleConfig
{
  [JsonPropertyName("adminRoles")]
  public List<string> AdminRoles { get; set; } = new();

  [JsonPropertyName("commandRoles")]
  public Dictionary<string, List<string>> CommandRoles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
