namespace CopperCalzone.Models.Schematic;

public sealed class SchLibSymbol
{
    public string Name { get; init; } = "";
    public List<SchPin> Pins { get; init; } = [];
    public List<SchPrimitive> Primitives { get; init; } = [];
    // Key: Unit number (0 = all units, 1 = unit A, 2 = unit B, etc.)
    public Dictionary<int, List<SchPin>> PinsByUnit { get; init; } = [];
    public Dictionary<int, List<SchPrimitive>> PrimitivesByUnit { get; init; } = [];
}

public sealed class SchSymbol
{
    public string LibId { get; init; } = "";
    public string Reference { get; set; } = "";
    public string Value { get; set; } = "";
    public string Footprint { get; set; } = "";
    public double X { get; init; }
    public double Y { get; init; }
    public double Rotation { get; init; }
    public bool MirrorX { get; init; }
    public bool MirrorY { get; init; }
    public int Unit { get; init; } = 1;
    public string Uuid { get; init; } = "";

    // Position of text properties
    public double RefX { get; set; }
    public double RefY { get; set; }
    public double RefRotation { get; set; }
    public bool RefHide { get; set; }

    public double ValX { get; set; }
    public double ValY { get; set; }
    public double ValRotation { get; set; }
    public bool ValHide { get; set; }

    // Resolved world-space elements
    public List<SchPin> WorldPins { get; init; } = [];
    public List<SchPrimitive> WorldPrimitives { get; init; } = [];

    // World bounding box
    public double MinX { get; set; }
    public double MinY { get; set; }
    public double MaxX { get; set; }
    public double MaxY { get; set; }
}

public sealed class SchWire
{
    public List<SchPoint> Points { get; init; } = [];
    public double StrokeWidth { get; init; } = 0.15;
    public string Uuid { get; init; } = "";
}

public sealed class SchBus
{
    public List<SchPoint> Points { get; init; } = [];
    public double StrokeWidth { get; init; } = 0.4;
    public string Uuid { get; init; } = "";
}

public sealed class SchJunction
{
    public double X { get; init; }
    public double Y { get; init; }
    public double Diameter { get; init; } = 1.0;
    public string Uuid { get; init; } = "";
}

public sealed class SchLabel
{
    public string Text { get; init; } = "";
    public string Shape { get; init; } = "passive"; // input, output, bidirectional, tri_state, passive
    public string Kind { get; init; } = "local"; // local, global, hierarchical
    public double X { get; init; }
    public double Y { get; init; }
    public double Rotation { get; init; }
    public double Size { get; init; } = 1.27;
    public string Uuid { get; init; } = "";
}

public sealed class SchSheetRef
{
    public string Name { get; init; } = "";
    public string FilePath { get; init; } = "";
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
    public string Uuid { get; init; } = "";
}

public sealed class KiCadSchematic
{
    public string Title { get; init; } = "";
    public string Date { get; init; } = "";
    public string Revision { get; init; } = "";
    public string Paper { get; init; } = "A4";
    public string SourceFilePath { get; set; } = "";

    public Dictionary<string, SchLibSymbol> LibSymbols { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public List<SchSymbol> Symbols { get; init; } = [];
    public List<SchWire> Wires { get; init; } = [];
    public List<SchBus> Buses { get; init; } = [];
    public List<SchJunction> Junctions { get; init; } = [];
    public List<SchLabel> Labels { get; init; } = [];
    public List<SchSheetRef> Sheets { get; init; } = [];
    public List<SchPoint> NoConnects { get; init; } = [];

    // Overall bounding box in mm
    public double MinX { get; set; }
    public double MinY { get; set; }
    public double MaxX { get; set; }
    public double MaxY { get; set; }
    public double Width => Math.Max(0, MaxX - MinX);
    public double Height => Math.Max(0, MaxY - MinY);
    public double CenterX => (MinX + MaxX) / 2.0;
    public double CenterY => (MinY + MaxY) / 2.0;
}

