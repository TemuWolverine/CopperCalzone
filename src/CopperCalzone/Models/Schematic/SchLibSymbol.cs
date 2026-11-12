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

