namespace CopperCalzone.Models.Schematic;

public sealed class SchArc : SchPrimitive
{
    public double EndX { get; init; }
    public double EndY { get; init; }
    public double MidX { get; init; }
    public double MidY { get; init; }
    public double StartX { get; init; }
    public double StartY { get; init; }
}
