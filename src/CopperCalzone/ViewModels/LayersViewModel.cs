using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopperCalzone.Models;
using CopperCalzone.Rendering;

namespace CopperCalzone.ViewModels;

public sealed partial class LayersViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isZoneHatch;

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
        if (IsZoneHatch) return ZoneDisplayMode.Hatch;
        if (IsZoneOutline) return ZoneDisplayMode.Outline;
        if (IsZoneHide) return ZoneDisplayMode.Hide;
        return ZoneDisplayMode.Solid;
    }

    public void SetupLayers(List<PcbLayer> boardLayers)
    {
        CopperLayers.Clear();
        TechLayers.Clear();

        foreach (var l in boardLayers.Where(x => x.Name.EndsWith(".Cu", StringComparison.Ordinal)))
        {
            string displayName = string.IsNullOrWhiteSpace(l.UserName) ? l.Name : l.UserName;
            CopperLayers.Add(new LayerItemViewModel(l.Name, displayName, true, OnLayerVisibilityChangedInternal));
        }

        if (CopperLayers.Count == 0)
        {
            CopperLayers.Add(new LayerItemViewModel("F.Cu", "F.Cu", true, OnLayerVisibilityChangedInternal));
            CopperLayers.Add(new LayerItemViewModel("B.Cu", "B.Cu", true, OnLayerVisibilityChangedInternal));
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
            TechLayers.Add(new LayerItemViewModel(td.Name, td.Display, td.DefaultVis, OnLayerVisibilityChangedInternal));
        }
    }

    [RelayCommand]
    public void TogglePadLabels()
    {
        ShowPadNetNames = !ShowPadNetNames;
    }

    partial void OnIsZoneHatchChanged(bool value)
    {
        if (value) ZoneModeChanged?.Invoke(ZoneDisplayMode.Hatch);
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
