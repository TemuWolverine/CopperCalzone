namespace CopperCalzone.Models.Schematic;

public sealed class SchBus
{
    public List<SchPoint> Points { get; init; } = [];
    public double StrokeWidth { get; init; } = 0.4;
    public string Uuid { get; init; } = "";
}
