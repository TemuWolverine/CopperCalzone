using System.Text.Json.Serialization;

namespace CopperCalzone.Models;

public sealed class PcbGraphic
{
    [JsonPropertyName("center")]
    public double[]? Center { get; set; }

    [JsonPropertyName("end")]
    public double[]? End { get; set; }

    [JsonPropertyName("fill")]
    public string? Fill { get; set; }

    [JsonPropertyName("layer")]
    public string Layer { get; set; } = "";

    [JsonPropertyName("mid")]
    public double[]? Mid { get; set; }

    [JsonPropertyName("pts")]
    public List<double[]>? Pts { get; set; }

    [JsonPropertyName("rot")]
    public double Rot { get; set; }

    [JsonPropertyName("size")]
    public double Size { get; set; } = 1.0;

    [JsonPropertyName("start")]
    public double[]? Start { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("width")]
    public double Width { get; set; } = 0.12;

    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }
}