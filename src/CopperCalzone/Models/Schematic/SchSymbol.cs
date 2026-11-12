namespace CopperCalzone.Models.Schematic;

public sealed class SchSymbol
{
    public string Footprint { get; set; } = "";
    public string LibId { get; init; } = "";
    public double MaxX { get; set; }
    public double MaxY { get; set; }

    // World bounding box
    public double MinX { get; set; }

    public double MinY { get; set; }
    public bool MirrorX { get; init; }
    public bool MirrorY { get; init; }
    public string Reference { get; set; } = "";
    public bool RefHide { get; set; }
    public double RefRotation { get; set; }

    // Position of text properties
    public double RefX { get; set; }

    public double RefY { get; set; }
    public double Rotation { get; init; }
    public int Unit { get; init; } = 1;
    public string Uuid { get; init; } = "";
    public bool ValHide { get; set; }
    public double ValRotation { get; set; }
    public string Value { get; set; } = "";
    public double ValX { get; set; }
    public double ValY { get; set; }

    // Resolved world-space elements
    public List<SchPin> WorldPins { get; init; } = [];

    public List<SchPrimitive> WorldPrimitives { get; init; } = [];
    public double X { get; init; }
    public double Y { get; init; }
}
