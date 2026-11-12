namespace CopperCalzone.Models.Schematic;

public sealed class SchLabel
{
    public string Kind { get; init; } = "local";
    public double Rotation { get; init; }
    public string Shape { get; init; } = "passive";
    public double Size { get; init; } = 1.27;
    public string Text { get; init; } = "";
    public string Uuid { get; init; } = "";

    // input, output, bidirectional, tri_state, passive
    // local, global, hierarchical
    public double X { get; init; }

    public double Y { get; init; }
}
