namespace CopperCalzone.Models.Schematic;

public readonly record struct SchPoint(double X, double Y);

public enum SchPinType
{
    Input,
    Output,
    Bidirectional,
    TriState,
    Passive,
    Free,
    Unspecified,
    PowerIn,
    PowerOut,
    OpenCollector,
    OpenEmitter,
    NoConnect
}

public sealed class SchPin
{
    public string Number { get; init; } = "";
    public string Name { get; init; } = "";
    public SchPinType Type { get; init; } = SchPinType.Unspecified;
    public double X { get; init; }
    public double Y { get; init; }
    public double TargetX { get; init; }
    public double TargetY { get; init; }
    public double DirX { get; init; }
    public double DirY { get; init; }
    public double Rotation { get; init; }
    public double Length { get; init; } = 2.54;
    public bool HideNumber { get; init; }
    public bool HideName { get; init; }
}

public abstract class SchPrimitive
{
    public double StrokeWidth { get; init; } = 0.15;
    public string? FillType { get; init; } // "none", "outline", "background"
}

public sealed class SchPolyline : SchPrimitive
{
    public List<SchPoint> Points { get; init; } = [];
}

public sealed class SchRectangle : SchPrimitive
{
    public double StartX { get; init; }
    public double StartY { get; init; }
    public double EndX { get; init; }
    public double EndY { get; init; }
}

public sealed class SchCircle : SchPrimitive
{
    public double CenterX { get; init; }
    public double CenterY { get; init; }
    public double Radius { get; init; }
}

public sealed class SchArc : SchPrimitive
{
    public double StartX { get; init; }
    public double StartY { get; init; }
    public double MidX { get; init; }
    public double MidY { get; init; }
    public double EndX { get; init; }
    public double EndY { get; init; }
}

public sealed class SchTextPrimitive : SchPrimitive
{
    public string Text { get; init; } = "";
    public double X { get; init; }
    public double Y { get; init; }
    public double Rotation { get; init; }
    public double Size { get; init; } = 1.27;
}

