using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopperCalzone.Models.Schematic;
using CopperCalzone.Rendering.Schematic;

namespace CopperCalzone.ViewModels;

public sealed partial class SchematicViewModel : ObservableObject
{
    private SchCanvasControl? _canvas;
    private HashSet<string> _currentPlacedRefs = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _currentHighlightedRefs = new(StringComparer.OrdinalIgnoreCase);
    private string? _currentActiveRef;

    [ObservableProperty]
    private bool _hasSchematic;

    [ObservableProperty]
    private string _hudCoordinates = "X: 0.00 mm  Y: 0.00 mm";

    [ObservableProperty]
    private string _hudZoom = "100%";

    [ObservableProperty]
    private string _schematicTitle = "No Schematic Loaded";

    [ObservableProperty]
    private string _schematicSubtitle = "Open a KiCad board with .kicad_sch";

    [ObservableProperty]
    private SchematicSheetItem? _selectedSheet;

    public ObservableCollection<SchematicSheetItem> Sheets { get; } = [];

    public event Action<string>? SymbolSelectedForCrossProbing;

    public void AttachCanvas(SchCanvasControl canvas)
    {
        _canvas = canvas;
        _canvas.CursorCoordinatesChanged += coords => HudCoordinates = coords;
        _canvas.ZoomPercentageChanged += pct => HudZoom = $"{pct}%";
        _canvas.SymbolClicked += OnCanvasSymbolClicked;

        if (SelectedSheet?.Schematic is not null)
        {
            _canvas.SetSchematic(SelectedSheet.Schematic);
            _canvas.SetPlacedSymbols(_currentPlacedRefs);
            _canvas.SetHighlightedSymbols(_currentHighlightedRefs, _currentActiveRef, flyTo: false);
        }
    }

    public void LoadSheets(List<SchematicSheetItem> sheets)
    {
        Sheets.Clear();
        foreach (var s in sheets)
        {
            Sheets.Add(s);
        }

        HasSchematic = Sheets.Count > 0;
        if (HasSchematic)
        {
            SelectedSheet = Sheets[0];
            UpdateTitles();
        }
        else
        {
            SelectedSheet = null;
            SchematicTitle = "No Schematic Found";
            SchematicSubtitle = "No matching .kicad_sch found in board directory";
            _canvas?.SetSchematic(null);
        }
    }

    private void UpdateTitles()
    {
        if (SelectedSheet?.Schematic is { } sch)
        {
            SchematicTitle = string.IsNullOrWhiteSpace(sch.Title) ? SelectedSheet.Name : sch.Title;
            SchematicSubtitle = $"{sch.Symbols.Count} symbols • {sch.Wires.Count} wires • {Sheets.Count} sheet(s)";
        }
    }

    partial void OnSelectedSheetChanged(SchematicSheetItem? value)
    {
        if (value?.Schematic is not null)
        {
            _canvas?.SetSchematic(value.Schematic);
            _canvas?.SetPlacedSymbols(_currentPlacedRefs);
            _canvas?.SetHighlightedSymbols(_currentHighlightedRefs, _currentActiveRef, flyTo: false);
            UpdateTitles();
        }
    }

    public void SetTheme(bool isLightTheme)
    {
        _canvas?.SetTheme(isLightTheme);
    }

    public void HighlightSymbols(IEnumerable<string>? refs, string? activeRef = null, bool flyTo = true)
    {
        _currentHighlightedRefs = refs is not null
            ? new HashSet<string>(refs, StringComparer.OrdinalIgnoreCase)
            : [];
        _currentActiveRef = activeRef;

        if (!string.IsNullOrWhiteSpace(activeRef))
        {
            // Check if active symbol is on current sheet
            bool onCurrentSheet = SelectedSheet?.Schematic?.Symbols.Any(s => s.Reference.Equals(activeRef, StringComparison.OrdinalIgnoreCase)) == true;
            if (!onCurrentSheet)
            {
                // Find which sheet has this symbol!
                var targetSheet = Sheets.FirstOrDefault(sh => sh.Schematic?.Symbols.Any(s => s.Reference.Equals(activeRef, StringComparison.OrdinalIgnoreCase)) == true);
                if (targetSheet is not null && targetSheet != SelectedSheet)
                {
                    SelectedSheet = targetSheet;
                }
            }
        }

        _canvas?.SetHighlightedSymbols(_currentHighlightedRefs, _currentActiveRef, flyTo);
    }

    public void SetPlacedSymbols(IEnumerable<string>? refs)
    {
        _currentPlacedRefs = refs is not null
            ? new HashSet<string>(refs, StringComparer.OrdinalIgnoreCase)
            : [];
        _canvas?.SetPlacedSymbols(_currentPlacedRefs);
    }

    private void OnCanvasSymbolClicked(SchSymbol? symbol)
    {
        if (symbol is null || string.IsNullOrWhiteSpace(symbol.Reference)) return;

        // Highlight locally
        HighlightSymbols([symbol.Reference], symbol.Reference, flyTo: false);

        // Notify for PCB cross-probing
        SymbolSelectedForCrossProbing?.Invoke(symbol.Reference);
    }

    [RelayCommand]
    public void ZoomIn() => _canvas?.ZoomBy(1.25);

    [RelayCommand]
    public void ZoomOut() => _canvas?.ZoomBy(1.0 / 1.25);

    [RelayCommand]
    public void ZoomFit() => _canvas?.FitSchematicToView();
}

