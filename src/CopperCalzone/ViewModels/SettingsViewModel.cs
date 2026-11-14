using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopperCalzone.Models;
using CopperCalzone.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace CopperCalzone.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly Action<AppSettings>? _onSettingsApplied;
    private readonly SettingsService _settingsService;

    [ObservableProperty]
    private string _defaultBomFilter = "all";

    [ObservableProperty]
    private bool _defaultBomSortAscending = true;

    [ObservableProperty]
    private string _defaultBomSortColumn = "references";

    [ObservableProperty]
    private string _defaultLayerPreset = "all";

    [ObservableProperty]
    private string _defaultSidebarTab = "bom";

    [ObservableProperty]
    private bool _isImmersiveMode;

    [ObservableProperty]
    private double _fontScalePercent = 100.0;

    private bool _isLoading;

    [ObservableProperty]
    private bool _isSidebarOpenByDefault = true;

    [ObservableProperty]
    private bool _rememberSidebarWidths = true;

    [ObservableProperty]
    private bool _showPadNetNames = true;

    [ObservableProperty]
    private string _statusMessage = "All preferences are saved automatically";

    [ObservableProperty]
    private string _zoneMode = "solid";

    public event Action? RequestClose;

    public ObservableCollection<LayerDefaultItemViewModel> DefaultCopperLayers { get; } = [];
    public ObservableCollection<LayerDefaultItemViewModel> DefaultTechnicalLayers { get; } = [];
    public bool IsDefaultTabBom => DefaultSidebarTab == "bom";
    public bool IsDefaultTabLayers => DefaultSidebarTab == "layers";

    // Pill Tab Active Getters
    public bool IsDefaultTabNets => DefaultSidebarTab == "nets";

    // BOM Filter Getters
    public bool IsFilterAll => DefaultBomFilter == "all";

    public bool IsFilterBottom => DefaultBomFilter == "bottom";
    public bool IsFilterTop => DefaultBomFilter == "top";
    public bool IsFilterUnplaced => DefaultBomFilter == "unplaced";

    // Preset Active Getters
    public bool IsPresetAll => DefaultLayerPreset == "all";

    public bool IsPresetBottom => DefaultLayerPreset == "bottom";
    public bool IsPresetCopper => DefaultLayerPreset == "copper";
    public bool IsPresetTop => DefaultLayerPreset == "top";
    public bool IsZoneHide => ZoneMode == "hide";
    public bool IsZoneOutline => ZoneMode == "outline";

    // Zone Mode Getters
    public bool IsZoneSolid => ZoneMode == "solid";

    public bool ShowConfigFileLocation => !OperatingSystem.IsAndroid();

    public string SettingsFilePath => _settingsService.SettingsFilePath;

    public SettingsViewModel(SettingsService? settingsService = null, Action<AppSettings>? onSettingsApplied = null)
    {
        _settingsService = settingsService ?? SettingsService.Instance;
        _onSettingsApplied = onSettingsApplied;

        LoadFromModel(_settingsService.Current);
    }

    public void AutoSave()
    {
        if (_isLoading) return;
        var model = ToModel();
        _settingsService.Save(model);
        _onSettingsApplied?.Invoke(model);
        StatusMessage = $"Saved automatically to {Path.GetFileName(SettingsFilePath)} ({DateTime.Now:HH:mm:ss})";
    }

    [RelayCommand]
    public void CloseSettings()
    {
        RequestClose?.Invoke();
    }

    public void LoadFromModel(AppSettings s)
    {
        _isLoading = true;
        try
        {
            DefaultSidebarTab = s.DefaultSidebarTab;
            IsSidebarOpenByDefault = s.IsSidebarOpenByDefault;
            RememberSidebarWidths = s.RememberSidebarWidths;

            DefaultLayerPreset = s.DefaultLayerPreset;
            ShowPadNetNames = s.ShowPadNetNames;
            ZoneMode = s.ZoneMode is "outline" or "hide" ? s.ZoneMode : "solid";

            DefaultBomSortColumn = s.DefaultBomSortColumn;
            DefaultBomSortAscending = s.DefaultBomSortAscending;
            DefaultBomFilter = s.DefaultBomFilter;

            IsImmersiveMode = s.IsImmersiveMode;
            FontScalePercent = Math.Clamp(s.FontScalePercent, 80.0, 150.0);

            // Initialize layer collections
            var activeSet = new HashSet<string>(s.VisibleLayers, StringComparer.OrdinalIgnoreCase);

            DefaultCopperLayers.Clear();
            DefaultCopperLayers.Add(new LayerDefaultItemViewModel("F.Cu", "Front Copper (F.Cu)", "#C83434", activeSet.Contains("F.Cu"), AutoSave));
            DefaultCopperLayers.Add(new LayerDefaultItemViewModel("B.Cu", "Back Copper (B.Cu)", "#3A6BC8", activeSet.Contains("B.Cu"), AutoSave));
            DefaultCopperLayers.Add(new LayerDefaultItemViewModel("In1.Cu", "Inner 1 (In1.Cu)", "#D9822B", activeSet.Contains("In1.Cu"), AutoSave));
            DefaultCopperLayers.Add(new LayerDefaultItemViewModel("In2.Cu", "Inner 2 (In2.Cu)", "#9B51E0", activeSet.Contains("In2.Cu"), AutoSave));
            DefaultCopperLayers.Add(new LayerDefaultItemViewModel("In3.Cu", "Inner 3 (In3.Cu)", "#219653", activeSet.Contains("In3.Cu"), AutoSave));
            DefaultCopperLayers.Add(new LayerDefaultItemViewModel("In4.Cu", "Inner 4 (In4.Cu)", "#F2994A", activeSet.Contains("In4.Cu"), AutoSave));

            DefaultTechnicalLayers.Clear();
            DefaultTechnicalLayers.Add(new LayerDefaultItemViewModel("Edge.Cuts", "Board Outline (Edge.Cuts)", "#FFE600", activeSet.Contains("Edge.Cuts"), AutoSave));
            DefaultTechnicalLayers.Add(new LayerDefaultItemViewModel("F.SilkS", "Front Silkscreen (F.SilkS)", "#00E5FF", activeSet.Contains("F.SilkS"), AutoSave));
            DefaultTechnicalLayers.Add(new LayerDefaultItemViewModel("B.SilkS", "Back Silkscreen (B.SilkS)", "#9C27B0", activeSet.Contains("B.SilkS"), AutoSave));
            DefaultTechnicalLayers.Add(new LayerDefaultItemViewModel("F.Mask", "Front Solder Mask (F.Mask)", "#886600", activeSet.Contains("F.Mask"), AutoSave));
            DefaultTechnicalLayers.Add(new LayerDefaultItemViewModel("B.Mask", "Back Solder Mask (B.Mask)", "#553388", activeSet.Contains("B.Mask"), AutoSave));
            DefaultTechnicalLayers.Add(new LayerDefaultItemViewModel("F.Fab", "Front Fabrication (F.Fab)", "#808080", activeSet.Contains("F.Fab"), AutoSave));
            DefaultTechnicalLayers.Add(new LayerDefaultItemViewModel("B.Fab", "Back Fabrication (B.Fab)", "#606060", activeSet.Contains("B.Fab"), AutoSave));
            DefaultTechnicalLayers.Add(new LayerDefaultItemViewModel("F.CrtYd", "Front Courtyard (F.CrtYd)", "#408080", activeSet.Contains("F.CrtYd"), AutoSave));
            DefaultTechnicalLayers.Add(new LayerDefaultItemViewModel("B.CrtYd", "Back Courtyard (B.CrtYd)", "#804040", activeSet.Contains("B.CrtYd"), AutoSave));
        }
        finally
        {
            _isLoading = false;
        }

        NotifyAllGetters();
    }

    [RelayCommand]
    public void OpenSettingsFolder()
    {
        try
        {
            string folder = _settingsService.SettingsDirectoryPath;
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not open folder: {ex.Message}";
        }
    }

    [RelayCommand]
    public void ResetAllDefaults()
    {
        var s = _settingsService.ResetToDefaults();
        LoadFromModel(s);
        _onSettingsApplied?.Invoke(s);
        StatusMessage = "All preferences reset to factory defaults";
    }

    [RelayCommand]
    public void ResetBomColumnWidths()
    {
        var s = _settingsService.Current;
        s.BomColPlacedWidth = 50;
        s.BomColRefWidth = 110;
        s.BomColValWidth = 100;
        s.BomColFpWidth = 140;
        s.BomColQtyWidth = 50;
        _settingsService.Save(s);
        _onSettingsApplied?.Invoke(s);
        StatusMessage = "BOM column widths reset to defaults (50, 110, 100, 140, 50)";
    }

    [RelayCommand]
    public void SetDefaultBomFilter(string filter)
    {
        DefaultBomFilter = filter;
        NotifyAllGetters();
        AutoSave();
    }

    [RelayCommand]
    public void SetDefaultBomSort(string column)
    {
        if (DefaultBomSortColumn.Equals(column, StringComparison.OrdinalIgnoreCase))
        {
            DefaultBomSortAscending = !DefaultBomSortAscending;
        }
        else
        {
            DefaultBomSortColumn = column.ToLowerInvariant();
            DefaultBomSortAscending = true;
        }
        AutoSave();
    }

    [RelayCommand]
    public void SetDefaultLayerPreset(string preset)
    {
        DefaultLayerPreset = preset;
        NotifyAllGetters();
        AutoSave();
    }

    [RelayCommand]
    public void SetDefaultSidebarTab(string tab)
    {
        DefaultSidebarTab = tab;
        NotifyAllGetters();
        AutoSave();
    }

    [RelayCommand]
    public void SetZoneMode(string mode)
    {
        ZoneMode = mode;
        NotifyAllGetters();
        AutoSave();
    }

    public AppSettings ToModel()
    {
        var s = _settingsService.Current.Clone();
        s.DefaultSidebarTab = DefaultSidebarTab;
        s.IsSidebarOpenByDefault = IsSidebarOpenByDefault;
        s.RememberSidebarWidths = RememberSidebarWidths;

        s.DefaultLayerPreset = DefaultLayerPreset;
        s.ShowPadNetNames = ShowPadNetNames;
        s.ZoneMode = ZoneMode;

        s.DefaultBomSortColumn = DefaultBomSortColumn;
        s.DefaultBomSortAscending = DefaultBomSortAscending;
        s.DefaultBomFilter = DefaultBomFilter;

        s.IsImmersiveMode = IsImmersiveMode;
        s.FontScalePercent = Math.Clamp(FontScalePercent, 80.0, 150.0);

        var visible = new List<string>();
        foreach (var l in DefaultCopperLayers)
        {
            if (l.IsVisible) visible.Add(l.Name);
        }
        foreach (var l in DefaultTechnicalLayers)
        {
            if (l.IsVisible) visible.Add(l.Name);
        }
        s.VisibleLayers = visible;

        return s;
    }

    private void NotifyAllGetters()
    {
        OnPropertyChanged(nameof(IsDefaultTabNets));
        OnPropertyChanged(nameof(IsDefaultTabBom));
        OnPropertyChanged(nameof(IsDefaultTabLayers));

        OnPropertyChanged(nameof(IsPresetAll));
        OnPropertyChanged(nameof(IsPresetTop));
        OnPropertyChanged(nameof(IsPresetBottom));
        OnPropertyChanged(nameof(IsPresetCopper));

        OnPropertyChanged(nameof(IsZoneSolid));
        OnPropertyChanged(nameof(IsZoneOutline));
        OnPropertyChanged(nameof(IsZoneHide));

        OnPropertyChanged(nameof(IsFilterAll));
        OnPropertyChanged(nameof(IsFilterTop));
        OnPropertyChanged(nameof(IsFilterBottom));
        OnPropertyChanged(nameof(IsFilterUnplaced));
    }

    partial void OnFontScalePercentChanged(double value) => AutoSave();

    partial void OnIsImmersiveModeChanged(bool value) => AutoSave();

    partial void OnIsSidebarOpenByDefaultChanged(bool value) => AutoSave();

    partial void OnRememberSidebarWidthsChanged(bool value) => AutoSave();

    partial void OnShowPadNetNamesChanged(bool value) => AutoSave();
}
