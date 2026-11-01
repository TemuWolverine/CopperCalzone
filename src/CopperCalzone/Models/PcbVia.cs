using System.Text.Json.Serialization;

namespace CopperCalzone.Models;

public sealed class PcbVia
{
    [JsonPropertyName("drill")]
    public double Drill { get; set; } = 0.3;

    [JsonPropertyName("layers")]
    public List<string> Layers { get; set; } = [];

    [JsonPropertyName("net")]
    public int Net { get; set; }

    [JsonPropertyName("size")]
    public double Size { get; set; } = 0.6;

    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }
}