using System.Text.Json.Serialization;

namespace CopperCalzone.Models;

public sealed class PcbFootprint
{
    [JsonPropertyName("drawings")]
    public List<PcbGraphic> Drawings { get; set; } = [];

    [JsonPropertyName("layer")]
    public string Layer { get; set; } = "F.Cu";

    [JsonPropertyName("package")]
    public string Package { get; set; } = "";

    [JsonPropertyName("padCount")]
    public int PadCount { get; set; }

    [JsonPropertyName("ref")]
    public string Ref { get; set; } = "";

    [JsonPropertyName("rot")]
    public double Rot { get; set; }

    [JsonPropertyName("texts")]
    public List<PcbText> Texts { get; set; } = [];

    [JsonPropertyName("val")]
    public string Val { get; set; } = "";

    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }
}