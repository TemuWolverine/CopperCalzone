using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CopperCalzone.Models;

namespace CopperCalzone.ViewModels;

public sealed partial class BomItemViewModel : ObservableObject
{
    private static readonly IBrush BottomSideBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248));
    private static readonly IBrush TopSideBrush = new SolidColorBrush(Color.FromRgb(249, 115, 22));
    private readonly Action<BomItemViewModel>? _onPlacedChanged;

    [ObservableProperty]
    private bool _isPlaced;

    [ObservableProperty]
    private bool _isSelected;

    public List<PcbFootprint> Footprints { get; }

    public bool IsBottom { get; }

    public string Layer { get; }

    public string Package { get; }

    public int Quantity { get; }

    public List<string> References { get; }

    public string ReferencesText { get; }

    public IBrush SideBrush { get; }

    public string SideText { get; }

    public string Value { get; }

    public BomItemViewModel(string value, string package, string layer, List<PcbFootprint> footprints, Action<BomItemViewModel>? onPlacedChanged = null)
    {
        Value = string.IsNullOrWhiteSpace(value) ? "(No Value)" : value;
        Package = string.IsNullOrWhiteSpace(package) ? "—" : package;
        Layer = layer;
        Footprints = footprints;
        _onPlacedChanged = onPlacedChanged;

        bool hasTop = footprints.Any(f => f.Layer.StartsWith("F", StringComparison.OrdinalIgnoreCase));
        bool hasBot = footprints.Any(f => f.Layer.StartsWith("B", StringComparison.OrdinalIgnoreCase));
        if (hasTop && hasBot)
        {
            SideText = "BOTH";
            SideBrush = TopSideBrush;
            IsBottom = false;
        }
        else if (hasBot)
        {
            SideText = "BOT";
            SideBrush = BottomSideBrush;
            IsBottom = true;
        }
        else
        {
            SideText = "TOP";
            SideBrush = TopSideBrush;
            IsBottom = false;
        }

        References = footprints
            .Select(f => f.Ref)
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .OrderBy(r => r, NaturalStringComparer.Instance)
            .ToList();

        Quantity = References.Count;
        ReferencesText = string.Join(", ", References);
    }

    partial void OnIsPlacedChanged(bool value)
    {
        _onPlacedChanged?.Invoke(this);
    }
}
