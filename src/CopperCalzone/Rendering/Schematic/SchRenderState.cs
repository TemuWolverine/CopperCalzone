using CopperCalzone.Models.Schematic;

namespace CopperCalzone.Rendering.Schematic;

public sealed class SchRenderState
{
    public KiCadSchematic? Schematic { get; init; }
    public double Scale { get; init; } = 1.0;
    public double PanX { get; init; }
    public double PanY { get; init; }
    public bool IsLightTheme { get; init; }

    public HashSet<string> HighlightedSymbolRefs { get; init; } = [];
    public HashSet<string> PlacedSymbolRefs { get; init; } = [];
    public string? ActiveSymbolRef { get; init; }
    public SchSymbol? HoveredSymbol { get; init; }
}

