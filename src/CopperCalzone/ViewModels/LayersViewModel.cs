using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopperCalzone.Models;
using CopperCalzone.Rendering;
using System.Collections.ObjectModel;

namespace CopperCalzone.ViewModels;

public sealed partial class LayersViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isZoneHide;

    [ObservableProperty]
    private bool _isZoneOutline;

    [ObservableProperty]
    private bool _isZoneSolid = true;

    [ObservableProperty]
    private bool _showPadNetNames = true;

    public event Action<bool>? FlipRequested;

    public event Action<string, bool>? LayerVisibilityChanged;

    public event Action<bool>? ShowPadNetNamesChanged;

    public event Action<ZoneDisplayMode>? ZoneModeChanged;

    public ObservableCollection<LayerItemViewModel> CopperLayers { get; } = [];
    public ObservableCollection<LayerItemViewModel> TechLayers { get; } = [];

    [RelayCommand]
    public void ApplyLayerPreset(string preset)
    {
        foreach (var l in CopperLayers.Concat(TechLayers))
        {
            bool show = preset switch
            {
                "all" => true,
                "top" => l.LayerName is "Edge.Cuts" or "F.Cu" or "F.SilkS" or "F.Mask",
                "bottom" => l.LayerName is "Edge.Cuts" or "B.Cu" or "B.SilkS" or "B.Mask",
                "copper" => l.LayerName.EndsWith(".Cu", StringComparison.Ordinal) || l.LayerName == "Edge.Cuts",
                _ => l.IsVisible
            };

            l.SetVisibleSilent(show);
            LayerVisibilityChanged?.Invoke(l.LayerName, show);
        }

        if (preset == "top")
        {
            FlipRequested?.Invoke(false);
        }
        else if (preset == "bottom")
        {
            FlipRequested?.Invoke(true);
        }
    }

    public ZoneDisplayMode GetSelectedZoneMode()
    {
        if (IsZoneOutline) return ZoneDisplayMode.Outline;
        if (IsZoneHide) return ZoneDisplayMode.Hide;
        return ZoneDisplayMode.Solid;
    }

    public void SetupLayers(List<PcbLayer> boardLayers, IEnumerable<string>? defaultVisibleLayers = null)
    {
        HashSet<string>? visibleSet = defaultVisibleLayers is not null
            ? new HashSet<string>(defaultVisibleLayers, StringComparer.OrdinalIgnoreCase)
            : null;

        CopperLayers.Clear();
        TechLayers.Clear();

        foreach (var l in boardLayers.Where(x => x.Name.EndsWith(".Cu", StringComparison.Ordinal)))
        {
            string displayName = string.IsNullOrWhiteSpace(l.UserName) ? l.Name : l.UserName;
            bool isVis = visibleSet?.Contains(l.Name) ?? true;
            CopperLayers.Add(new LayerItemViewModel(l.Name, displayName, isVis, OnLayerVisibilityChangedInternal));
        }

        if (CopperLayers.Count == 0)
        {
            CopperLayers.Add(new LayerItemViewModel("F.Cu", "F.Cu", visibleSet?.Contains("F.Cu") ?? true, OnLayerVisibilityChangedInternal));
            CopperLayers.Add(new LayerItemViewModel("B.Cu", "B.Cu", visibleSet?.Contains("B.Cu") ?? true, OnLayerVisibilityChangedInternal));
        }

        (string Name, string Display, bool DefaultVis)[] techDefs =
        [
            ("Edge.Cuts", "Board Outline", true),
            ("F.SilkS", "Front Silkscreen", true),
            ("B.SilkS", "Back Silkscreen", false),
            ("F.Mask", "Front Solder Mask", false),
            ("B.Mask", "Back Solder Mask", false),
            ("F.Fab", "Front Fabrication", false),
            ("B.Fab", "Back Fabrication", false)
        ];

        foreach (var td in techDefs)
        {
            bool isVis = visibleSet?.Contains(td.Name) ?? td.DefaultVis;
            TechLayers.Add(new LayerItemViewModel(td.Name, td.Display, isVis, OnLayerVisibilityChangedInternal));
        }
    }

    [RelayCommand]
    public void TogglePadLabels()
    {
        ShowPadNetNames = !ShowPadNetNames;
    }

    partial void OnIsZoneHideChanged(bool value)
    {
        if (value) ZoneModeChanged?.Invoke(ZoneDisplayMode.Hide);
    }

    partial void OnIsZoneOutlineChanged(bool value)
    {
        if (value) ZoneModeChanged?.Invoke(ZoneDisplayMode.Outline);
    }

    partial void OnIsZoneSolidChanged(bool value)
    {
        if (value) ZoneModeChanged?.Invoke(ZoneDisplayMode.Solid);
    }

    private void OnLayerVisibilityChangedInternal(string layerName, bool isVisible)
    {
        LayerVisibilityChanged?.Invoke(layerName, isVisible);
    }

    partial void OnShowPadNetNamesChanged(bool value)
    {
        ShowPadNetNamesChanged?.Invoke(value);
    }
}
