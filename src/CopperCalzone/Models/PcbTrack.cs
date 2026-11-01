using System.Text.Json.Serialization;

namespace CopperCalzone.Models;

public sealed class PcbTrack
{
    [JsonPropertyName("end")]
    public double[] End { get; set; } = [0, 0];

    [JsonPropertyName("layer")]
    public string Layer { get; set; } = "F.Cu";

    [JsonPropertyName("mid")]
    public double[]? Mid { get; set; }

    [JsonPropertyName("net")]
    public int Net { get; set; }

    [JsonPropertyName("start")]
    public double[] Start { get; set; } = [0, 0];

    [JsonPropertyName("type")]
    public string Type { get; set; } = "segment";

    [JsonPropertyName("width")]
    public double Width { get; set; } = 0.25;
}