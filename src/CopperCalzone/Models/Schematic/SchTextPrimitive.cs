namespace CopperCalzone.Models.Schematic;

public sealed class SchTextPrimitive : SchPrimitive
{
    public double Rotation { get; init; }
    public double Size { get; init; } = 1.27;
    public string Text { get; init; } = "";
    public double X { get; init; }
    public double Y { get; init; }
}
