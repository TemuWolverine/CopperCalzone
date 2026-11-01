using System.Text.Json.Serialization;

namespace CopperCalzone.Models;

public sealed class PcbBoard
{
    [JsonPropertyName("bbox")]
    public BoundingBox Bbox { get; set; } = new();

    [JsonPropertyName("drawings")]
    public List<PcbGraphic> Drawings { get; set; } = [];

    [JsonPropertyName("footprints")]
    public List<PcbFootprint> Footprints { get; set; } = [];

    [JsonPropertyName("layers")]
    public List<PcbLayer> Layers { get; set; } = [];

    [JsonPropertyName("metadata")]
    public BoardMetadata Metadata { get; set; } = new();

    [JsonPropertyName("nets")]
    public List<PcbNet> Nets { get; set; } = [];

    [JsonPropertyName("pads")]
    public List<PcbPad> Pads { get; set; } = [];

    [JsonPropertyName("tracks")]
    public List<PcbTrack> Tracks { get; set; } = [];

    [JsonPropertyName("vias")]
    public List<PcbVia> Vias { get; set; } = [];

    [JsonPropertyName("zones")]
    public List<PcbZone> Zones { get; set; } = [];
}