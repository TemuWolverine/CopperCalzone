using CopperCalzone.Models;

namespace CopperCalzone.Rendering;

public sealed class PcbRenderState
{
    public string? ActiveFootprintRef { get; init; }
    public int? ActiveNetId { get; init; }
    public PcbBoard? Board { get; init; }
    public HashSet<string> HighlightedFootprintRefs { get; init; } = [];
    public HashSet<string> PlacedFootprintRefs { get; init; } = [];
    public PcbPad? HoveredPad { get; init; }
    public bool IsFlipped { get; init; }
    public double PanX { get; init; }
    public double PanY { get; init; }
    public double Rotation { get; init; }
    public double Scale { get; init; } = 1.0;
    public PcbPad? SelectedPad { get; init; }
    public bool ShowPadNetNames { get; init; } = true;
    public HashSet<string> VisibleLayers { get; init; } = [];
    public ZoneDisplayMode ZoneMode { get; init; } = ZoneDisplayMode.Solid;

    public bool IsPadVisible(PcbPad? pad)
    {
        if (pad is null || pad.Layers.Count == 0) return true;
        foreach (string l in pad.Layers)
        {
            if (VisibleLayers.Contains(l)) return true;
            if (l is "*.Cu" or "*")
            {
                foreach (string vl in VisibleLayers)
                {
                    if (vl.EndsWith(".Cu", StringComparison.Ordinal))
                        return true;
                }
            }
            if (l == "*.Mask")
            {
                if (VisibleLayers.Contains("F.Mask") || VisibleLayers.Contains("B.Mask"))
                    return true;
            }
        }
        return false;
    }
}