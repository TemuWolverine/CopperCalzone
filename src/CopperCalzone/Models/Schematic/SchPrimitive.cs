namespace CopperCalzone.Models.Schematic;

public abstract class SchPrimitive
{
    public string? FillType { get; init; }
    public double StrokeWidth { get; init; } = 0.15;
    // "none", "outline", "background"
}
