using CopperCalzone.Models.Schematic;

namespace CopperCalzone.ViewModels;

public sealed class SchematicSheetItem
{
    public string Name { get; init; } = "";
    public string FilePath { get; init; } = "";
    public KiCadSchematic? Schematic { get; set; }

    public override string ToString() => Name;
}

