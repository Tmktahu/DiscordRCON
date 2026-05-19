using System.Text.Json.Serialization;

namespace DiscordRcon.Models;

public class CustomCommand
{
  [JsonPropertyName("name")]
  public string Name { get; set; } = "";

  [JsonPropertyName("description")]
  public string Description { get; set; } = "";

  [JsonPropertyName("rconCommand")]
  public string RconCommand { get; set; } = "";
}
