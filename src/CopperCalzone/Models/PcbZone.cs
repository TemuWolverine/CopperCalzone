using System.Text.Json.Serialization;

namespace CopperCalzone.Models;

public sealed class PcbZone
{
    [JsonPropertyName("layer")]
    public string Layer { get; set; } = "F.Cu";

    [JsonPropertyName("net")]
    public int Net { get; set; }

    [JsonPropertyName("polygons")]
    public List<List<double[]>> Polygons { get; set; } = [];
}