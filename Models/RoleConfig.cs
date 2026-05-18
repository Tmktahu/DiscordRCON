using System.Text.Json.Serialization;

namespace DiscordRcon.Models;

public class RoleConfig
{
  [JsonPropertyName("defaultRoles")]
  public List<string> DefaultRoles { get; set; } = new();

  [JsonPropertyName("commandOverrides")]
  public Dictionary<string, List<string>> CommandOverrides { get; set; } = new();
}
