namespace CopperCalzone.Models.Schematic;

public sealed class SchSheetRef
{
    public string FilePath { get; init; } = "";
    public double Height { get; init; }
    public string Name { get; init; } = "";
    public string Uuid { get; init; } = "";
    public double Width { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
}
