using System.Text.Json.Serialization;

namespace CopperCalzone.Models;

public sealed class PcbNet
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonIgnore]
    public bool IsPower
    {
        get
        {
            if (string.IsNullOrEmpty(Name)) return false;
            var upper = Name.ToUpperInvariant();
            return upper is "GND" or "VCC" or "VDD" or "VSS"
                   || upper.StartsWith('+')
                   || upper.StartsWith("VBUS", StringComparison.Ordinal)
                   || upper.Contains("3V3", StringComparison.Ordinal)
                   || upper.Contains("5V", StringComparison.Ordinal)
                   || upper.Contains("1V8", StringComparison.Ordinal)
                   || upper.Contains("12V", StringComparison.Ordinal)
                   || upper.Contains("PWR", StringComparison.Ordinal);
        }
    }

    [JsonPropertyName("length")]
    public double Length { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("padCount")]
    public int PadCount { get; set; }

    [JsonPropertyName("trackCount")]
    public int TrackCount { get; set; }

    [JsonPropertyName("viaCount")]
    public int ViaCount { get; set; }
}