using System.Text.Json.Serialization;

namespace CopperCalzone.Models;

public sealed class BoardMetadata
{
    [JsonPropertyName("company")]
    public string Company { get; set; } = "";

    [JsonPropertyName("date")]
    public string Date { get; set; } = "";

    [JsonPropertyName("generator")]
    public string Generator { get; set; } = "pcbnew";

    [JsonPropertyName("rev")]
    public string Rev { get; set; } = "";

    [JsonPropertyName("thickness")]
    public double Thickness { get; set; } = 1.6;

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";
}