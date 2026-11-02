using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopperCalzone.Models;

namespace CopperCalzone.ViewModels;

public sealed partial class PinInspectorViewModel : ObservableObject
{
    private List<PcbPad> _activeNetPads = [];
    private int _currentPinIndex = -1;

    [ObservableProperty]
    private bool _hasActiveInspector;

    [ObservableProperty]
    private Thickness _hoverTooltipMargin = new(0);

    [ObservableProperty]
    private string _hoverTooltipNet = "";

    [ObservableProperty]
    private string _hoverTooltipTitle = "";

    // HUD Coordinates & Zoom
    [ObservableProperty]
    private string _hudCoordinates = "X: 0.00 mm, Y: 0.00 mm";

    [ObservableProperty]
    private string _hudZoom = "100%";

    [ObservableProperty]
    private string _inspectorConnectedPins = "";

    [ObservableProperty]
    private string _inspectorNetName = "";

    [ObservableProperty]
    private string _inspectorNetSummary = "";

    [ObservableProperty]
    private string _inspectorPinBadge = "Pin 0 / 0";

    [ObservableProperty]
    private string _inspectorPinMeta = "";

    [ObservableProperty]
    private string _inspectorPinTitle = "";

    // Hover Tooltip
    [ObservableProperty]
    private bool _isHoverTooltipVisible;

    public event Action? ClearNetSelectionRequested;

    public event Action<PcbPad?, bool>? PadFocusRequested;

    [RelayCommand]
    public void ClearSelection()
    {
        CloseInspector();
        ClearNetSelectionRequested?.Invoke();
    }

    public void CloseInspector()
    {
        HasActiveInspector = false;
        _activeNetPads.Clear();
        _currentPinIndex = -1;
        PadFocusRequested?.Invoke(null, false);
    }

    [RelayCommand]
    public void FocusCurrentPin()
    {
        if (_currentPinIndex >= 0 && _currentPinIndex < _activeNetPads.Count)
        {
            var pad = _activeNetPads[_currentPinIndex];
            PadFocusRequested?.Invoke(pad, true);
        }
    }

    public void InspectNet(PcbNet? net, List<PcbPad> netPads, PcbPad? specificPad = null, bool flyToFirstPin = false)
    {
        if (net is null)
        {
            CloseInspector();
            return;
        }

        _activeNetPads = netPads;
        if (specificPad is not null)
        {
            _currentPinIndex = _activeNetPads.IndexOf(specificPad);
            if (_currentPinIndex < 0 && _activeNetPads.Count > 0)
                _currentPinIndex = 0;
        }
        else
        {
            _currentPinIndex = _activeNetPads.Count > 0 ? 0 : -1;
        }

        HasActiveInspector = true;
        InspectorNetName = string.IsNullOrEmpty(net.Name) ? "(unnamed)" : net.Name;
        InspectorNetSummary = $"{net.PadCount} pins • {net.ViaCount} vias • {net.TrackCount} segs • {net.Length:0.1f} mm";

        var allPinRefs = _activeNetPads.Select(p => $"{p.FpRef}.{p.Num}").ToList();
        InspectorConnectedPins = allPinRefs.Count > 0
            ? string.Join(", ", allPinRefs.Take(16)) + (allPinRefs.Count > 16 ? $" (+{allPinRefs.Count - 16} more)" : "")
            : "No component pins";

        UpdatePinDetails(flyToPin: flyToFirstPin);
    }

    [RelayCommand]
    public void NextPin()
    {
        if (_activeNetPads.Count == 0) return;
        _currentPinIndex = (_currentPinIndex + 1) % _activeNetPads.Count;
        UpdatePinDetails(flyToPin: true);
    }

    [RelayCommand]
    public void PrevPin()
    {
        if (_activeNetPads.Count == 0) return;
        _currentPinIndex = (_currentPinIndex - 1 + _activeNetPads.Count) % _activeNetPads.Count;
        UpdatePinDetails(flyToPin: true);
    }

    public void UpdateHoverTooltip(PcbPad? pad, Point screenPt)
    {
        if (pad is null)
        {
            IsHoverTooltipVisible = false;
            return;
        }

        HoverTooltipTitle = string.IsNullOrEmpty(pad.Fn)
            ? $"{pad.FpRef}.{pad.Num}"
            : $"{pad.FpRef}.{pad.Num} ({pad.Fn})";
        HoverTooltipNet = $"Net: {(string.IsNullOrEmpty(pad.NetName) ? "NC" : pad.NetName)}";
        HoverTooltipMargin = new Thickness(screenPt.X + 14, Math.Max(8, screenPt.Y - 46), 0, 0);
        IsHoverTooltipVisible = true;
    }

    private void UpdatePinDetails(bool flyToPin)
    {
        if (!HasActiveInspector) return;

        if (_currentPinIndex >= 0 && _currentPinIndex < _activeNetPads.Count)
        {
            var pad = _activeNetPads[_currentPinIndex];
            InspectorPinBadge = $"Pin {_currentPinIndex + 1} of {_activeNetPads.Count}";
            InspectorPinTitle = string.IsNullOrEmpty(pad.Fn)
                ? $"{pad.FpRef} — Pin {pad.Num}"
                : $"{pad.FpRef} — Pin {pad.Num} ({pad.Fn})";

            string valPart = string.IsNullOrEmpty(pad.FpVal) ? "" : $" ({pad.FpVal})";
            string pinTypePart = string.IsNullOrEmpty(pad.PinType) ? "" : $" • {pad.PinType}";
            InspectorPinMeta = $"{pad.FpRef}{valPart} • {pad.FpLayer} • {pad.Shape} {pad.W:0.##}×{pad.H:0.##} mm{pinTypePart}";

            PadFocusRequested?.Invoke(pad, flyToPin);
        }
        else
        {
            InspectorPinBadge = "0 Pins";
            InspectorPinTitle = InspectorNetName;
            InspectorPinMeta = "Copper net without component pads";
            PadFocusRequested?.Invoke(null, false);
        }
    }
}
