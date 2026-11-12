namespace CopperCalzone.Models.Schematic;

public sealed class SchPolyline : SchPrimitive
{
    public List<SchPoint> Points { get; init; } = [];
}
