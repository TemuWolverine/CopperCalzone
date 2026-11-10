using System.Text.Json.Serialization;

namespace CopperCalzone.Models;

public sealed class AppSettings
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    // Navigation & Startup
    [JsonPropertyName("defaultSidebarTab")]
    public string DefaultSidebarTab { get; set; } = "bom"; // "nets", "bom", "layers"

    [JsonPropertyName("isSidebarOpenByDefault")]
    public bool IsSidebarOpenByDefault { get; set; } = true;

    [JsonPropertyName("rememberSidebarWidths")]
    public bool RememberSidebarWidths { get; set; } = true;

    [JsonPropertyName("lastNetsSidebarWidth")]
    public double LastNetsSidebarWidth { get; set; } = 340;

    [JsonPropertyName("lastBomSidebarWidth")]
    public double LastBomSidebarWidth { get; set; } = 520;

    // Layer Options & Rendering
    [JsonPropertyName("defaultLayerPreset")]
    public string DefaultLayerPreset { get; set; } = "all"; // "all", "top", "bottom", "copper"

    [JsonPropertyName("showPadNetNames")]
    public bool ShowPadNetNames { get; set; } = true;

    [JsonPropertyName("zoneMode")]
    public string ZoneMode { get; set; } = "solid"; // "solid", "hatch", "outline", "hide"

    [JsonPropertyName("visibleLayers")]
    public List<string> VisibleLayers { get; set; } =
    [
        "Edge.Cuts", "F.Cu", "B.Cu", "In1.Cu", "In2.Cu", "In3.Cu", "In4.Cu",
        "F.SilkS", "B.SilkS", "F.Mask", "B.Mask"
    ];

    // BOM & Assembly Tracking
    [JsonPropertyName("defaultBomSortColumn")]
    public string DefaultBomSortColumn { get; set; } = "references";

    [JsonPropertyName("defaultBomSortAscending")]
    public bool DefaultBomSortAscending { get; set; } = true;

    [JsonPropertyName("defaultBomFilter")]
    public string DefaultBomFilter { get; set; } = "all"; // "all", "top", "bottom", "unplaced"

    [JsonPropertyName("bomColPlacedWidth")]
    public double BomColPlacedWidth { get; set; } = 50;

    [JsonPropertyName("bomColRefWidth")]
    public double BomColRefWidth { get; set; } = 110;

    [JsonPropertyName("bomColValWidth")]
    public double BomColValWidth { get; set; } = 100;

    [JsonPropertyName("bomColFpWidth")]
    public double BomColFpWidth { get; set; } = 140;

    [JsonPropertyName("bomColQtyWidth")]
    public double BomColQtyWidth { get; set; } = 50;

    // Appearance
    [JsonPropertyName("isImmersiveMode")]
    public bool IsImmersiveMode { get; set; } = false;

    public AppSettings Clone()
    {
        return new AppSettings
        {
            Version = Version,
            DefaultSidebarTab = DefaultSidebarTab,
            IsSidebarOpenByDefault = IsSidebarOpenByDefault,
            RememberSidebarWidths = RememberSidebarWidths,
            LastNetsSidebarWidth = LastNetsSidebarWidth,
            LastBomSidebarWidth = LastBomSidebarWidth,
            DefaultLayerPreset = DefaultLayerPreset,
            ShowPadNetNames = ShowPadNetNames,
            ZoneMode = ZoneMode,
            VisibleLayers = [.. VisibleLayers],
            DefaultBomSortColumn = DefaultBomSortColumn,
            DefaultBomSortAscending = DefaultBomSortAscending,
            DefaultBomFilter = DefaultBomFilter,
            BomColPlacedWidth = BomColPlacedWidth,
            BomColRefWidth = BomColRefWidth,
            BomColValWidth = BomColValWidth,
            BomColFpWidth = BomColFpWidth,
            BomColQtyWidth = BomColQtyWidth,
            IsImmersiveMode = IsImmersiveMode
        };
    }
}
