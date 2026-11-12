namespace CopperCalzone.Models.Schematic;

public sealed class KiCadSchematic
{
    public List<SchBus> Buses { get; init; } = [];
    public double CenterX => (MinX + MaxX) / 2.0;
    public double CenterY => (MinY + MaxY) / 2.0;
    public string Date { get; init; } = "";
    public double Height => Math.Max(0, MaxY - MinY);
    public List<SchJunction> Junctions { get; init; } = [];
    public List<SchLabel> Labels { get; init; } = [];
    public Dictionary<string, SchLibSymbol> LibSymbols { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public double MaxX { get; set; }
    public double MaxY { get; set; }

    // Overall bounding box in mm
    public double MinX { get; set; }

    public double MinY { get; set; }
    public List<SchPoint> NoConnects { get; init; } = [];
    public string Paper { get; init; } = "A4";
    public string Revision { get; init; } = "";
    public List<SchSheetRef> Sheets { get; init; } = [];
    public string SourceFilePath { get; set; } = "";
    public List<SchSymbol> Symbols { get; init; } = [];
    public string Title { get; init; } = "";
    public double Width => Math.Max(0, MaxX - MinX);
    public List<SchWire> Wires { get; init; } = [];
}
