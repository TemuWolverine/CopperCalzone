using System.Text.Json.Serialization;

namespace CopperCalzone.Models;

public sealed class PcbText
{
    [JsonPropertyName("layer")]
    public string Layer { get; set; } = "";

    [JsonPropertyName("rot")]
    public double Rot { get; set; }

    [JsonPropertyName("size")]
    public double Size { get; set; } = 1.0;

    [JsonPropertyName("text")]
    public string Text { get; set; } = "";

    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }
}