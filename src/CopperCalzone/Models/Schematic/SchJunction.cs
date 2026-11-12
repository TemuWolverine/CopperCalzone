namespace CopperCalzone.Models.Schematic;

public sealed class SchJunction
{
    public double Diameter { get; init; } = 1.0;
    public string Uuid { get; init; } = "";
    public double X { get; init; }
    public double Y { get; init; }
}
