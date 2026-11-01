using System.Text.Json.Serialization;

namespace CopperCalzone.Models;

public sealed class PcbPad
{
    [JsonIgnore]
    public string DisplayTitle => string.IsNullOrEmpty(Fn)
        ? $"{FpRef}.{Num}"
        : $"{FpRef}.{Num} ({Fn})";

    [JsonPropertyName("drill")]
    public double Drill { get; set; }

    [JsonPropertyName("drillH")]
    public double DrillH { get; set; }

    [JsonPropertyName("drillW")]
    public double DrillW { get; set; }

    [JsonPropertyName("fn")]
    public string Fn { get; set; } = "";

    [JsonPropertyName("fpLayer")]
    public string FpLayer { get; set; } = "F.Cu";

    [JsonPropertyName("fpRef")]
    public string FpRef { get; set; } = "";

    [JsonPropertyName("fpVal")]
    public string FpVal { get; set; } = "";

    [JsonPropertyName("h")]
    public double H { get; set; } = 1.0;

    [JsonPropertyName("layers")]
    public List<string> Layers { get; set; } = [];

    [JsonPropertyName("net")]
    public int Net { get; set; }

    [JsonPropertyName("netName")]
    public string NetName { get; set; } = "";

    [JsonPropertyName("num")]
    public string Num { get; set; } = "";

    [JsonPropertyName("pinType")]
    public string PinType { get; set; } = "";

    [JsonPropertyName("rot")]
    public double Rot { get; set; }

    [JsonPropertyName("rratio")]
    public double Rratio { get; set; } = 0.25;

    [JsonPropertyName("shape")]
    public string Shape { get; set; } = "rect";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("w")]
    public double W { get; set; } = 1.0;

    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }
}