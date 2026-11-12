namespace CopperCalzone.Models.Schematic;

public sealed class SchRectangle : SchPrimitive
{
    public double EndX { get; init; }
    public double EndY { get; init; }
    public double StartX { get; init; }
    public double StartY { get; init; }
}
