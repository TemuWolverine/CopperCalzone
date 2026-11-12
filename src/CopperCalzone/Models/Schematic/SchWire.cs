namespace CopperCalzone.Models.Schematic;

public sealed class SchWire
{
    public List<SchPoint> Points { get; init; } = [];
    public double StrokeWidth { get; init; } = 0.15;
    public string Uuid { get; init; } = "";
}
