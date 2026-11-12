namespace CopperCalzone.Models.Schematic;

public sealed class SchPin
{
    public double DirX { get; init; }
    public double DirY { get; init; }
    public bool HideName { get; init; }
    public bool HideNumber { get; init; }
    public double Length { get; init; } = 2.54;
    public string Name { get; init; } = "";
    public string Number { get; init; } = "";
    public double Rotation { get; init; }
    public double TargetX { get; init; }
    public double TargetY { get; init; }
    public SchPinType Type { get; init; } = SchPinType.Unspecified;
    public double X { get; init; }
    public double Y { get; init; }
}
