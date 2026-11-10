using System.Collections.ObjectModel;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopperCalzone.Models;
using CopperCalzone.Models.Schematic;
using CopperCalzone.Parsing;
using CopperCalzone.Rendering;
using CopperCalzone.Services;

namespace CopperCalzone.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private MainTabMode _activeTabMode = MainTabMode.Viewer;

    [ObservableProperty]
    private string _boardSubtitle = "No board loaded";

    [ObservableProperty]
    private string _boardTitle = "CopperCalzone";

    private PcbCanvasControl? _canvas;
    private PcbBoard? _currentBoard;

    [ObservableProperty]
    private bool _hasBoard;

    [ObservableProperty]
    private bool _isBomTabActive;

    [ObservableProperty]
    private bool _isImmersiveMode;

    public event Action<bool>? ImmersiveModeChanged;

    [ObservableProperty]
    private bool _isFlipped;

    [ObservableProperty]
    private bool _isLayersTabActive;

    [ObservableProperty]
    private bool _isLeftSidebarOpen = !OperatingSystem.IsAndroid();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NavRailWidth))]
    private bool _isNavRailExpanded;

    public double NavRailWidth => IsNavRailExpanded ? 220 : 80;

    [ObservableProperty]
    private bool _isNetsTabActive = true;

    private double _lastBomSidebarWidth = 520;

    private double _lastNonBomSidebarWidth = 340;

    [ObservableProperty]
    private double _sidebarWidth = 340;

    public event Func<Task>? OpenFileRequested;

    public double AssemblyProgressRatio => Bom.AssemblyProgressRatio;

    public string AssemblyProgressText => Bom.AssemblyProgressText;

    public BomViewModel Bom { get; } = new();

    public string BomActiveFilter { get => Bom.BomActiveFilter; set => Bom.BomActiveFilter = value; }

    public double BomColFpWidth { get => Bom.BomColFpWidth; set => Bom.BomColFpWidth = value; }

    public double BomColPlacedWidth { get => Bom.BomColPlacedWidth; set => Bom.BomColPlacedWidth = value; }

    public double BomColQtyWidth { get => Bom.BomColQtyWidth; set => Bom.BomColQtyWidth = value; }

    public double BomColRefWidth { get => Bom.BomColRefWidth; set => Bom.BomColRefWidth = value; }

    public double BomColValWidth { get => Bom.BomColValWidth; set => Bom.BomColValWidth = value; }

    public string BomSearchQuery { get => Bom.BomSearchQuery; set => Bom.BomSearchQuery = value; }

    public bool BomSortAscending { get => Bom.BomSortAscending; set => Bom.BomSortAscending = value; }

    public string BomSortColumn { get => Bom.BomSortColumn; set => Bom.BomSortColumn = value; }

    public ObservableCollection<LayerItemViewModel> CopperLayers => Layers.CopperLayers;

    public ObservableCollection<BomItemViewModel> FilteredBomItems => Bom.FilteredBomItems;

    public ObservableCollection<NetItemViewModel> FilteredNets => Nets.FilteredNets;

    public GitHubBrowserViewModel GitHubBrowser { get; }

    public bool HasActiveInspector => Inspector.HasActiveInspector;

    public Thickness HoverTooltipMargin => Inspector.HoverTooltipMargin;

    public string HoverTooltipNet => Inspector.HoverTooltipNet;

    public string HoverTooltipTitle => Inspector.HoverTooltipTitle;

    public string HudCoordinates => Inspector.HudCoordinates;

    public string HudZoom => Inspector.HudZoom;

    public PinInspectorViewModel Inspector { get; } = new();

    public string InspectorConnectedPins => Inspector.InspectorConnectedPins;

    public string InspectorNetName => Inspector.InspectorNetName;

    public string InspectorNetSummary => Inspector.InspectorNetSummary;

    public string InspectorPinBadge => Inspector.InspectorPinBadge;

    public string InspectorPinMeta => Inspector.InspectorPinMeta;

    public string InspectorPinTitle => Inspector.InspectorPinTitle;

    public bool IsBomFilterAll => Bom.IsBomFilterAll;

    public bool IsBomFilterBottom => Bom.IsBomFilterBottom;

    public bool IsBomFilterTop => Bom.IsBomFilterTop;

    public bool IsBomFilterUnplaced => Bom.IsBomFilterUnplaced;

    public bool IsGitHubTabActive => ActiveTabMode == MainTabMode.GitHub;

    public bool IsHoverTooltipVisible => Inspector.IsHoverTooltipVisible;

    public bool IsSchematicTabActive => ActiveTabMode == MainTabMode.Schematic;

    public bool IsSettingsTabActive => ActiveTabMode == MainTabMode.Settings;

    public bool IsViewerTabActive => ActiveTabMode == MainTabMode.Viewer;

    public bool IsZoneHatch { get => Layers.IsZoneHatch; set => Layers.IsZoneHatch = value; }

    public bool IsZoneHide { get => Layers.IsZoneHide; set => Layers.IsZoneHide = value; }

    public bool IsZoneOutline { get => Layers.IsZoneOutline; set => Layers.IsZoneOutline = value; }

    public bool IsZoneSolid { get => Layers.IsZoneSolid; set => Layers.IsZoneSolid = value; }

    public LayersViewModel Layers { get; } = new();
    public NetsViewModel Nets { get; } = new();

    public int PlacedComponentCount => Bom.PlacedComponentCount;
    public string SearchQuery { get => Nets.SearchQuery; set => Nets.SearchQuery = value; }
    public int SelectedBomComponentIndex { get => Bom.SelectedBomComponentIndex; set => Bom.SelectedBomComponentIndex = value; }
    public BomItemViewModel? SelectedBomItem { get => Bom.SelectedBomItem; set => Bom.SelectedBomItem = value; }
    public string SelectedComponentRef { get => Bom.SelectedComponentRef; set => Bom.SelectedComponentRef = value; }
    public NetItemViewModel? SelectedNetItem { get => Nets.SelectedNetItem; set => Nets.SelectedNetItem = value; }
    public SchematicViewModel Schematic { get; } = new();
    public SettingsViewModel Settings { get; }
    public string ShowingBomLabel => Bom.ShowingBomLabel;

    public string ShowingNetsLabel => Nets.ShowingNetsLabel;

    public bool ShowPadNetNames { get => Layers.ShowPadNetNames; set => Layers.ShowPadNetNames = value; }

    public ObservableCollection<LayerItemViewModel> TechLayers => Layers.TechLayers;

    public int TotalBomCount => Bom.TotalBomCount;

    public int TotalComponentCount => Bom.TotalComponentCount;

    public int TotalNetCount => Nets.TotalNetCount;

    public MainViewModel()
    {
        var authService = new GitHubAuthService();
        var pcbService = new GitHubPcbService(authService);
        GitHubBrowser = new GitHubBrowserViewModel(authService, pcbService);

        Settings = new SettingsViewModel(SettingsService.Instance, ApplySettings);
        Settings.RequestClose += SwitchToViewer;

        GitHubBrowser.PcbContentReady += (content, fileName, schematics) =>
        {
            try
            {
                if (fileName.EndsWith(".kicad_pcb", StringComparison.OrdinalIgnoreCase))
                {
                    var board = BoardLoader.LoadFromContent(content, fileName);
                    LoadBoard(board);
                    SwitchToViewer();
                }
                else if (fileName.EndsWith(".kicad_sch", StringComparison.OrdinalIgnoreCase))
                {
                    SwitchToSchematic();
                }

                if (schematics.Count > 0)
                {
                    var sheets = new List<SchematicSheetItem>();
                    string matchBase = Path.GetFileNameWithoutExtension(fileName);

                    foreach (var (schName, schContent) in schematics)
                    {
                        try
                        {
                            var parser = new KiCadSchParser(schContent);
                            var sch = parser.Parse();
                            sch.SourceFilePath = schName;

                            bool isRoot = string.Equals(Path.GetFileNameWithoutExtension(schName), matchBase, StringComparison.OrdinalIgnoreCase)
                                || (sheets.Count == 0 && schematics.Count == 1);

                            string title = !string.IsNullOrWhiteSpace(sch.Title) ? sch.Title : Path.GetFileNameWithoutExtension(schName);
                            string displayName = isRoot ? $"Main: {title} ({schName})" : $"{title} ({schName})";

                            sheets.Add(new SchematicSheetItem
                            {
                                Name = displayName,
                                FilePath = schName,
                                Schematic = sch
                            });
                        }
                        catch
                        {
                            // Ignore individual parse errors
                        }
                    }

                    if (sheets.Count > 0)
                    {
                        var rootSheet = sheets.FirstOrDefault(s => s.Name.StartsWith("Main: ", StringComparison.Ordinal)) ?? sheets[0];
                        sheets.Remove(rootSheet);
                        sheets.Insert(0, rootSheet);

                        Schematic.LoadSheets(sheets);
                    }
                }
            }
            catch (Exception ex)
            {
                GitHubBrowser.ErrorMessage = $"Failed to parse file '{fileName}': {ex.Message}";
            }
        };

        GitHubBrowser.RequestClose += SwitchToViewer;

        // Forward PropertyChanged events from sub-viewmodels to MainViewModel
        Nets.PropertyChanged += (s, e) =>
        {
            if (!string.IsNullOrEmpty(e.PropertyName))
                OnPropertyChanged(e.PropertyName);
        };

        Bom.PropertyChanged += (s, e) =>
        {
            if (!string.IsNullOrEmpty(e.PropertyName))
                OnPropertyChanged(e.PropertyName);
        };

        Layers.PropertyChanged += (s, e) =>
        {
            if (!string.IsNullOrEmpty(e.PropertyName))
                OnPropertyChanged(e.PropertyName);
        };

        Inspector.PropertyChanged += (s, e) =>
        {
            if (!string.IsNullOrEmpty(e.PropertyName))
                OnPropertyChanged(e.PropertyName);
        };

        // Wire Sub-ViewModel Events
        Nets.NetSelected += netItem =>
        {
            if (netItem is null)
            {
                SelectNetInternal(null);
            }
            else
            {
                SelectNetInternal(netItem.Id, flyToFirstPin: true);
            }
        };

        Bom.FootprintHighlightRequested += (refs, activeRef) =>
        {
            if (IsBomTabActive)
            {
                _canvas?.SetHighlightedFootprints(refs, activeRef, flyTo: false);
            }
            Schematic.HighlightSymbols(refs, activeRef, flyTo: false);
        };

        Bom.PlacedFootprintsChanged += placedRefs =>
        {
            if (IsBomTabActive)
            {
                _canvas?.SetPlacedFootprints(placedRefs);
            }
            Schematic.SetPlacedSymbols(placedRefs);
        };

        Schematic.SymbolSelectedForCrossProbing += fpRef =>
        {
            if (Bom.SelectedBomItem is null || !Bom.SelectedBomItem.References.Contains(fpRef, StringComparer.OrdinalIgnoreCase))
            {
                Bom.SelectByFootprintRef(fpRef);
            }
            else
            {
                Bom.SelectBomComponent(fpRef);
            }
            _canvas?.FlyToHighlightedFootprints();
        };

        Layers.LayerVisibilityChanged += (layerName, isVis) =>
        {
            _canvas?.SetLayerVisibility(layerName, isVis);
        };

        Layers.ZoneModeChanged += mode =>
        {
            _canvas?.SetZoneMode(mode);
        };

        Layers.ShowPadNetNamesChanged += show =>
        {
            _canvas?.SetShowPadNetNames(show);
        };

        Layers.FlipRequested += flip =>
        {
            if (_canvas == null || _canvas.IsFlipped == flip)
                return;

            _canvas.SetFlip(flip);
            IsFlipped = flip;
        };

        Inspector.PadFocusRequested += (pad, fly) =>
        {
            _canvas?.SetSelectedPad(pad, flyToPad: fly);
        };

        Inspector.ClearNetSelectionRequested += () =>
        {
            Nets.ClearSelection();
            _canvas?.SetActiveNet(null);
            _canvas?.SetSelectedPad(null, false);
        };

        ApplySettings(SettingsService.Instance.Current);
    }

    [RelayCommand] public void ApplyLayerPreset(string p) => Layers.ApplyLayerPreset(p);

    public void ApplySettings(AppSettings s)
    {
        if (s.RememberSidebarWidths)
        {
            _lastNonBomSidebarWidth = s.LastNetsSidebarWidth;
            _lastBomSidebarWidth = s.LastBomSidebarWidth;
            SidebarWidth = IsBomTabActive ? _lastBomSidebarWidth : _lastNonBomSidebarWidth;
        }

        Bom.BomColPlacedWidth = s.BomColPlacedWidth;
        Bom.BomColRefWidth = s.BomColRefWidth;
        Bom.BomColValWidth = s.BomColValWidth;
        Bom.BomColFpWidth = s.BomColFpWidth;
        Bom.BomColQtyWidth = s.BomColQtyWidth;

        Layers.ShowPadNetNames = s.ShowPadNetNames;
        IsLeftSidebarOpen = s.IsSidebarOpenByDefault;

        Layers.IsZoneSolid = s.ZoneMode == "solid";
        Layers.IsZoneHatch = s.ZoneMode == "hatch";
        Layers.IsZoneOutline = s.ZoneMode == "outline";
        Layers.IsZoneHide = s.ZoneMode == "hide";
        if (_canvas is not null)
        {
            ZoneDisplayMode zm = s.ZoneMode switch
            {
                "hatch" => ZoneDisplayMode.Hatch,
                "outline" => ZoneDisplayMode.Outline,
                "hide" => ZoneDisplayMode.Hide,
                _ => ZoneDisplayMode.Solid
            };
            _canvas.SetZoneMode(zm);
            _canvas.SetShowPadNetNames(Layers.ShowPadNetNames);
        }

        Bom.BomSortColumn = s.DefaultBomSortColumn;
        Bom.BomSortAscending = s.DefaultBomSortAscending;
        Bom.BomActiveFilter = s.DefaultBomFilter;
        Bom.ApplyBomFilters();

        SwitchTab(s.DefaultSidebarTab);

        if (IsImmersiveMode != s.IsImmersiveMode)
        {
            IsImmersiveMode = s.IsImmersiveMode;
            ImmersiveModeChanged?.Invoke(IsImmersiveMode);
        }
    }

    public void AttachCanvas(PcbCanvasControl canvas)
    {
        _canvas = canvas;
        _canvas.CursorCoordinatesChanged += coords => Inspector.HudCoordinates = coords;
        _canvas.ZoomPercentageChanged += pct => Inspector.HudZoom = $"{pct}%";
        _canvas.HoveredPadChanged += (pad, pt) => Inspector.UpdateHoverTooltip(pad, pt);
        _canvas.PadClicked += OnPadClicked;

        if (_currentBoard is not null)
        {
            _canvas.SetBoard(_currentBoard);
            SyncLayersToCanvas();
            if (IsBomTabActive)
            {
                _canvas.SetPlacedFootprints(Bom.GetPlacedFootprintRefs());
            }
        }
    }

    [RelayCommand] public void ClearBomSearch() => Bom.ClearBomSearch();

    [RelayCommand] public void ClearBomSelection() => Bom.ClearBomSelection();

    [RelayCommand] public void ClearNetSelection() => Nets.ClearNetSelection();

    [RelayCommand] public void ClearSearch() => Nets.ClearSearch();

    [RelayCommand] public void ClearSelection() => Inspector.ClearSelection();

    [RelayCommand] public void FocusCurrentPin() => Inspector.FocusCurrentPin();

    public void LoadBoard(PcbBoard board, string? pcbFilePath = null)
    {
        _currentBoard = board;
        HasBoard = true;
        BoardTitle = string.IsNullOrWhiteSpace(board.Metadata.Title) ? "KiCad Board" : board.Metadata.Title;
        BoardSubtitle = $"{board.Bbox.Width:0.#} × {board.Bbox.Height:0.#} mm • {board.Nets.Count} nets • {board.Pads.Count} pads";

        // Load sub-viewmodels
        Nets.LoadNets(board.Nets);
        Bom.LoadBom(board);
        Layers.SetupLayers(board.Layers);
        Inspector.CloseInspector();

        if (_canvas is not null)
        {
            _canvas.SetBoard(board);
            SyncLayersToCanvas();
        }

        if (!string.IsNullOrWhiteSpace(pcbFilePath))
        {
            _ = LoadSchematicsForBoardAsync(pcbFilePath);
        }

        // Apply layer preset and default sidebar tab from settings
        var settings = SettingsService.Instance.Current;
        Layers.ApplyLayerPreset(settings.DefaultLayerPreset);
        SwitchTab(settings.DefaultSidebarTab);
    }

    private async Task LoadSchematicsForBoardAsync(string pcbFilePath)
    {
        try
        {
            var sheets = await SchematicLoader.DiscoverAndLoadSheetsAsync(pcbFilePath);
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                Schematic.LoadSheets(sheets);
            });
        }
        catch
        {
            // Ignore background schematic discovery errors
        }
    }

    [RelayCommand]
    public void SwitchToSchematic()
    {
        ActiveTabMode = MainTabMode.Schematic;
    }

    [RelayCommand] public void MarkAllBomPlaced() => Bom.MarkAllBomPlaced();

    [RelayCommand] public void NextBomComponent() => Bom.NextBomComponent();

    [RelayCommand] public void NextPin() => Inspector.NextPin();

    [RelayCommand]
    public async Task OpenGitHubBrowserAsync()
    {
        ActiveTabMode = MainTabMode.GitHub;
        await GitHubBrowser.InitializeAsync();
    }

    [RelayCommand]
    public void OpenSettings()
    {
        ActiveTabMode = MainTabMode.Settings;
    }

    [RelayCommand] public void PrevBomComponent() => Bom.PrevBomComponent();

    [RelayCommand] public void PrevPin() => Inspector.PrevPin();

    [RelayCommand]
    public async Task RequestOpenFileAsync()
    {
        if (OpenFileRequested is not null)
        {
            await OpenFileRequested.Invoke();
        }
    }

    [RelayCommand] public void ResetBomPlaced() => Bom.ResetBomPlaced();

    [RelayCommand]
    public void RotateClockwise() => _canvas?.RotateClockwise90();

    [RelayCommand]
    public void RotateCounterClockwise() => _canvas?.RotateCounterClockwise90();

    [RelayCommand] public void SelectBomComponent(string r) => Bom.SelectBomComponent(r);

    public void SelectNetInternal(int? netId, PcbPad? specificPad = null, bool flyToFirstPin = false)
    {
        if (netId is null or 0 || _currentBoard is null)
        {
            Nets.ClearSelection();
            Inspector.CloseInspector();
            _canvas?.SetActiveNet(null);
            _canvas?.SetSelectedPad(null, false);
            return;
        }

        Nets.SelectNetById(netId.Value);
        _canvas?.SetActiveNet(netId.Value);

        var net = _currentBoard.Nets.FirstOrDefault(n => n.Id == netId.Value);
        var pads = _currentBoard.Pads
            .Where(p => p.Net == netId.Value)
            .OrderBy(p => p.FpRef, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => int.TryParse(p.Num, out int n) ? n : int.MaxValue)
            .ThenBy(p => p.Num, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Inspector.InspectNet(net, pads, specificPad, flyToFirstPin);
    }

    [RelayCommand] public void SetBomFilter(string filter) => Bom.SetBomFilter(filter);

    [RelayCommand] public void SortBomBy(string col) => Bom.SortBomBy(col);

    [RelayCommand]
    public void SwitchTab(string tab)
    {
        bool wasBom = IsBomTabActive;
        IsNetsTabActive = tab == "nets";
        IsBomTabActive = tab == "bom";
        IsLayersTabActive = tab == "layers";

        if (wasBom && !IsBomTabActive)
        {
            _lastBomSidebarWidth = SidebarWidth;
            SidebarWidth = _lastNonBomSidebarWidth;
            _canvas?.SetHighlightedFootprints(null);
            _canvas?.SetPlacedFootprints(null);
        }
        else if (!wasBom && IsBomTabActive)
        {
            _lastNonBomSidebarWidth = SidebarWidth;
            SidebarWidth = Math.Max(520, _lastBomSidebarWidth);
            _canvas?.SetPlacedFootprints(Bom.GetPlacedFootprintRefs());
            if (Bom.SelectedBomItem is not null)
            {
                _canvas?.SetHighlightedFootprints(Bom.SelectedBomItem.References, Bom.SelectedComponentRef, flyTo: false);
            }
        }
    }

    [RelayCommand]
    public void SwitchToViewer()
    {
        ActiveTabMode = MainTabMode.Viewer;
    }

    public void SyncLayersToCanvas()
    {
        if (_canvas is null) return;
        foreach (var l in Layers.CopperLayers)
            _canvas.SetLayerVisibility(l.LayerName, l.IsVisible);
        foreach (var l in Layers.TechLayers)
            _canvas.SetLayerVisibility(l.LayerName, l.IsVisible);
    }

    [RelayCommand]
    public void ToggleFlipBoard()
    {
        if (_canvas is null) return;
        _canvas.ToggleFlip();
        IsFlipped = _canvas.IsFlipped;
    }

    [RelayCommand]
    public void ToggleLeftSidebar()
    {
        IsLeftSidebarOpen = !IsLeftSidebarOpen;
    }

    [RelayCommand]
    public void ToggleNavRail()
    {
        IsNavRailExpanded = !IsNavRailExpanded;
    }

    [RelayCommand]
    public void TogglePadLabels()
    {
        Layers.TogglePadLabels();
    }

    [RelayCommand]
    public void ToggleImmersiveMode()
    {
        IsImmersiveMode = !IsImmersiveMode;
        ImmersiveModeChanged?.Invoke(IsImmersiveMode);

        var s = SettingsService.Instance.Current;
        if (s.IsImmersiveMode != IsImmersiveMode)
        {
            s.IsImmersiveMode = IsImmersiveMode;
            SettingsService.Instance.Save();
        }
    }

    [RelayCommand]
    public void ZoomFit() => _canvas?.FitBoardToView();

    [RelayCommand]
    public void ZoomIn() => _canvas?.ZoomBy(1.25);

    [RelayCommand]
    public void ZoomOut() => _canvas?.ZoomBy(1.0 / 1.25);

    partial void OnActiveTabModeChanged(MainTabMode value)
    {
        OnPropertyChanged(nameof(IsViewerTabActive));
        OnPropertyChanged(nameof(IsSchematicTabActive));
        OnPropertyChanged(nameof(IsGitHubTabActive));
        OnPropertyChanged(nameof(IsSettingsTabActive));
    }

    private void OnPadClicked(PcbPad? pad)
    {
        if (IsBomTabActive)
        {
            if (pad is null || string.IsNullOrEmpty(pad.FpRef))
            {
                Bom.ClearBomSelection();
                return;
            }

            Bom.SelectByFootprintRef(pad.FpRef);
        }
        else
        {
            if (pad is null || pad.Net == 0)
            {
                Nets.ClearSelection();
                Inspector.CloseInspector();
                _canvas?.SetActiveNet(null);
                _canvas?.SetSelectedPad(null, false);
                return;
            }

            SelectNetInternal(pad.Net, specificPad: pad, flyToFirstPin: false);
        }
    }

    partial void OnSidebarWidthChanged(double value)
    {
        if (IsBomTabActive)
            _lastBomSidebarWidth = value;
        else
            _lastNonBomSidebarWidth = value;

        var s = SettingsService.Instance.Current;
        if (s.RememberSidebarWidths)
        {
            s.LastNetsSidebarWidth = _lastNonBomSidebarWidth;
            s.LastBomSidebarWidth = _lastBomSidebarWidth;
            SettingsService.Instance.Save();
        }
    }
}
