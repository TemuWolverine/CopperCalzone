using System.Text.Json.Serialization;

namespace CopperCalzone.Models;

public sealed class PcbLayer
{
    [JsonPropertyName("id")]
    public object? Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("userName")]
    public string UserName { get; set; } = "";
}