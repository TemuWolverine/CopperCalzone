using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopperCalzone.Models;
using CopperCalzone.Parsing;
using CopperCalzone.Rendering;
using CopperCalzone.Services;
using System.Collections.ObjectModel;

namespace CopperCalzone.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private PcbCanvasControl? _canvas;
    private PcbBoard? _currentBoard;
    private List<NetItemViewModel> _allNetItems = [];
    private List<BomItemViewModel> _allBomItems = [];
    private List<PcbPad> _activeNetPads = [];
    private int _currentPinIndex = -1;
    private bool _suppressSelectionCallback;

    public MainViewModel()
    {
        var authService = new GitHubAuthService();
        var pcbService = new GitHubPcbService(authService);
        GitHubBrowser = new GitHubBrowserViewModel(authService, pcbService);

        GitHubBrowser.PcbContentReady += (content, fileName) =>
        {
            try
            {
                var board = BoardLoader.LoadFromContent(content, fileName);
                LoadBoard(board);
                IsGitHubTabActive = false;
            }
            catch (Exception ex)
            {
                GitHubBrowser.ErrorMessage = $"Failed to parse board '{fileName}': {ex.Message}";
            }
        };

        GitHubBrowser.RequestClose += () =>
        {
            IsGitHubTabActive = false;
        };

    }


    [ObservableProperty]
    private string _boardTitle = "CopperCalzone";

    [ObservableProperty]
    private string _boardSubtitle = "Loading board...";

    [ObservableProperty]
    private bool _isGitHubTabActive;

    public bool IsViewerTabActive => !IsGitHubTabActive;

    partial void OnIsGitHubTabActiveChanged(bool value)
    {
        OnPropertyChanged(nameof(IsViewerTabActive));
    }


    [ObservableProperty]
    private bool _isLeftSidebarOpen = !OperatingSystem.IsAndroid();

    [ObservableProperty]
    private bool _isNetsTabActive = true;

    [ObservableProperty]
    private bool _isBomTabActive;

    [ObservableProperty]
    private bool _isLayersTabActive;

    [ObservableProperty]
    private bool _isDarkTheme = true;

    [ObservableProperty]
    private bool _isFlipped;

    [ObservableProperty]
    private bool _showPadNetNames = true;

    [ObservableProperty]
    private string _searchQuery = "";

    [ObservableProperty]
    private string _activeFilter = "all";

    [ObservableProperty]
    private int _totalNetCount;

    [ObservableProperty]
    private string _showingNetsLabel = "Showing 0 nets";

    [ObservableProperty]
    private NetItemViewModel? _selectedNetItem;

    [ObservableProperty]
    private BomItemViewModel? _selectedBomItem;

    [ObservableProperty]
    private int _selectedBomComponentIndex = -1;

    [ObservableProperty]
    private string _selectedComponentRef = "";

    [ObservableProperty]
    private int _totalBomCount;

    [ObservableProperty]
    private int _totalComponentCount;

    [ObservableProperty]
    private int _placedComponentCount;

    [ObservableProperty]
    private string _assemblyProgressText = "0 / 0 parts placed (0%)";

    [ObservableProperty]
    private double _assemblyProgressRatio;

    [ObservableProperty]
    private string _bomSearchQuery = "";

    [ObservableProperty]
    private string _bomActiveFilter = "all";

    [ObservableProperty]
    private string _showingBomLabel = "Showing 0 items";

    [ObservableProperty]
    private string _bomSortColumn = "references";

    [ObservableProperty]
    private bool _bomSortAscending = true;

    public double SidebarWidth => IsBomTabActive ? 520 : 340;

    // Zone mode radio properties
    [ObservableProperty]
    private bool _isZoneSolid = true;

    [ObservableProperty]
    private bool _isZoneHatch;

    [ObservableProperty]
    private bool _isZoneOutline;

    [ObservableProperty]
    private bool _isZoneHide;

    // HUD & Tooltip
    [ObservableProperty]
    private string _hudCoordinates = "X: 0.00 mm, Y: 0.00 mm";

    [ObservableProperty]
    private string _hudZoom = "100%";

    [ObservableProperty]
    private bool _isHoverTooltipVisible;

    [ObservableProperty]
    private string _hoverTooltipTitle = "";

    [ObservableProperty]
    private string _hoverTooltipNet = "";

    [ObservableProperty]
    private Thickness _hoverTooltipMargin = new(0);

    // Pin Inspector & Pin-to-Pin Navigation Bar
    [ObservableProperty]
    private bool _hasActiveInspector;

    [ObservableProperty]
    private string _inspectorNetName = "";

    [ObservableProperty]
    private string _inspectorNetSummary = "";

    [ObservableProperty]
    private string _inspectorPinBadge = "Pin 0 / 0";

    [ObservableProperty]
    private string _inspectorPinTitle = "";

    [ObservableProperty]
    private string _inspectorPinMeta = "";

    [ObservableProperty]
    private string _inspectorConnectedPins = "";

    [ObservableProperty]
    private string _selectedSampleBoard = BoardLoader.BuiltInSampleNames[0];

    [ObservableProperty]
    private bool _isGitHubBrowserOpen;

    public GitHubBrowserViewModel GitHubBrowser { get; }


    public ObservableCollection<string> SampleBoards { get; } = new(BoardLoader.BuiltInSampleNames);
    public ObservableCollection<NetItemViewModel> FilteredNets { get; } = [];
    public ObservableCollection<BomItemViewModel> FilteredBomItems { get; } = [];
    public ObservableCollection<LayerItemViewModel> CopperLayers { get; } = [];
    public ObservableCollection<LayerItemViewModel> TechLayers { get; } = [];

    public bool IsFilterAll => ActiveFilter == "all";
    public bool IsFilterPower => ActiveFilter == "power";
    public bool IsFilterSignals => ActiveFilter == "signals";
    public bool IsFilterTopPins => ActiveFilter == "top-pins";

    public bool IsBomFilterAll => BomActiveFilter == "all";
    public bool IsBomFilterTop => BomActiveFilter == "top";
    public bool IsBomFilterBottom => BomActiveFilter == "bottom";
    public bool IsBomFilterUnplaced => BomActiveFilter == "unplaced";

    public event Action<bool>? ThemeChanged;
    public event Func<Task>? OpenFileRequested;

    public void AttachCanvas(PcbCanvasControl canvas)
    {
        _canvas = canvas;
        _canvas.CursorCoordinatesChanged += coords => HudCoordinates = coords;
        _canvas.ZoomPercentageChanged += pct => HudZoom = $"{pct}%";
        _canvas.HoveredPadChanged += OnHoveredPadChanged;
        _canvas.PadClicked += OnPadClicked;

        if (_currentBoard is not null)
        {
            _canvas.SetBoard(_currentBoard);
        }
    }

    public void LoadBoard(PcbBoard board)
    {
        _currentBoard = board;
        BoardTitle = string.IsNullOrWhiteSpace(board.Metadata.Title) ? "KiCad Board" : board.Metadata.Title;
        BoardSubtitle = $"{board.Bbox.Width:0.#} × {board.Bbox.Height:0.#} mm • {board.Nets.Count} nets • {board.Pads.Count} pads";

        // Setup sorted nets
        _allNetItems = board.Nets
            .OrderByDescending(n => n.PadCount)
            .ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
            .Select(n => new NetItemViewModel(n))
            .ToList();

        TotalNetCount = _allNetItems.Count;

        // Setup BOM items:
        // 1. Exclude anything on either front or back silkscreen layers (F.SilkS, B.SilkS)
        // 2. Require items to be connected to an electrical net to filter out residual labels,
        //    logos, graphics, test points with no net, fiducials, and unrouted mechanical holes.
        var padsByRef = board.Pads
            .Where(p => !string.IsNullOrWhiteSpace(p.FpRef))
            .GroupBy(p => p.FpRef, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var validFootprints = board.Footprints
            .Where(f => !string.IsNullOrWhiteSpace(f.Ref))
            .Where(f => !f.Layer.Equals("F.SilkS", StringComparison.OrdinalIgnoreCase) &&
                        !f.Layer.Equals("B.SilkS", StringComparison.OrdinalIgnoreCase) &&
                        !f.Layer.EndsWith(".SilkS", StringComparison.OrdinalIgnoreCase))
            .Where(f => padsByRef.TryGetValue(f.Ref, out var fpPads) &&
                        fpPads.Any(p => p.Net > 0 &&
                                       !string.IsNullOrWhiteSpace(p.NetName) &&
                                       !p.NetName.StartsWith("unconnected-(", StringComparison.OrdinalIgnoreCase)))
            .ToList();

        _allBomItems = validFootprints
            .GroupBy(f => (Val: f.Val.Trim(), Pkg: f.Package.Trim()))
            .OrderBy(g => g.First().Ref, NaturalStringComparer.Instance)
            .Select(g => new BomItemViewModel(
                g.Key.Val,
                g.Key.Pkg,
                g.First().Layer,
                g.ToList(),
                OnBomItemPlacedChanged
            ))
            .ToList();

        TotalBomCount = _allBomItems.Count;
        TotalComponentCount = validFootprints.Count;
        UpdateAssemblyProgress();

        // Setup layers
        SetupLayers(board.Layers);

        // Reset selection
        _suppressSelectionCallback = true;
        SelectedNetItem = null;
        SelectedBomItem = null;
        SelectedBomComponentIndex = -1;
        SelectedComponentRef = "";
        _suppressSelectionCallback = false;
        HasActiveInspector = false;
        _activeNetPads.Clear();
        _currentPinIndex = -1;

        ApplyFilters();
        ApplyBomFilters();

        if (_canvas is not null)
        {
            _canvas.SetBoard(board);
            SyncLayersToCanvas();
        }
    }

    private void SetupLayers(List<PcbLayer> boardLayers)
    {
        CopperLayers.Clear();
        TechLayers.Clear();

        foreach (var l in boardLayers.Where(x => x.Name.EndsWith(".Cu", StringComparison.Ordinal)))
        {
            string displayName = string.IsNullOrWhiteSpace(l.UserName) ? l.Name : l.UserName;
            CopperLayers.Add(new LayerItemViewModel(l.Name, displayName, true, OnLayerVisibilityChanged));
        }

        if (CopperLayers.Count == 0)
        {
            CopperLayers.Add(new LayerItemViewModel("F.Cu", "F.Cu", true, OnLayerVisibilityChanged));
            CopperLayers.Add(new LayerItemViewModel("B.Cu", "B.Cu", true, OnLayerVisibilityChanged));
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
            TechLayers.Add(new LayerItemViewModel(td.Name, td.Display, td.DefaultVis, OnLayerVisibilityChanged));
        }
    }

    private void OnLayerVisibilityChanged(string layerName, bool isVisible)
    {
        _canvas?.SetLayerVisibility(layerName, isVisible);
    }

    private void SyncLayersToCanvas()
    {
        if (_canvas is null) return;
        foreach (var l in CopperLayers)
            _canvas.SetLayerVisibility(l.LayerName, l.IsVisible);
        foreach (var l in TechLayers)
            _canvas.SetLayerVisibility(l.LayerName, l.IsVisible);
    }

    partial void OnSelectedSampleBoardChanged(string value)
    {
        if (string.IsNullOrEmpty(value)) return;
        try
        {
            var board = BoardLoader.LoadBuiltInSample(value);
            LoadBoard(board);
        }
        catch
        {
            // Ignore if asset not ready yet
        }
    }

    partial void OnSearchQueryChanged(string value) => ApplyFilters();

    partial void OnActiveFilterChanged(string value)
    {
        OnPropertyChanged(nameof(IsFilterAll));
        OnPropertyChanged(nameof(IsFilterPower));
        OnPropertyChanged(nameof(IsFilterSignals));
        OnPropertyChanged(nameof(IsFilterTopPins));
        ApplyFilters();
    }

    partial void OnBomSearchQueryChanged(string value) => ApplyBomFilters();

    partial void OnBomActiveFilterChanged(string value)
    {
        OnPropertyChanged(nameof(IsBomFilterAll));
        OnPropertyChanged(nameof(IsBomFilterTop));
        OnPropertyChanged(nameof(IsBomFilterBottom));
        OnPropertyChanged(nameof(IsBomFilterUnplaced));
        ApplyBomFilters();
    }

    partial void OnSelectedNetItemChanged(NetItemViewModel? value)
    {
        if (_suppressSelectionCallback) return;
        SelectNetInternal(value?.Id, null, flyToFirstPin: false);
    }

    partial void OnSelectedBomItemChanged(BomItemViewModel? value)
    {
        if (_suppressSelectionCallback) return;
        SelectBomItemInternal(value, flyToComponent: false);
    }

    partial void OnShowPadNetNamesChanged(bool value)
    {
        _canvas?.SetShowPadNetNames(value);
    }

    partial void OnIsZoneSolidChanged(bool value)
    {
        if (value) _canvas?.SetZoneMode(ZoneDisplayMode.Solid);
    }

    partial void OnIsZoneHatchChanged(bool value)
    {
        if (value) _canvas?.SetZoneMode(ZoneDisplayMode.Hatch);
    }

    partial void OnIsZoneOutlineChanged(bool value)
    {
        if (value) _canvas?.SetZoneMode(ZoneDisplayMode.Outline);
    }

    partial void OnIsZoneHideChanged(bool value)
    {
        if (value) _canvas?.SetZoneMode(ZoneDisplayMode.Hide);
    }

    private void ApplyFilters()
    {
        string q = SearchQuery.Trim();
        var matches = _allNetItems.Where(net =>
        {
            if (ActiveFilter == "power" && !net.IsPower) return false;
            if (ActiveFilter == "signals" && net.IsPower) return false;
            if (ActiveFilter == "top-pins" && net.PadCount < 4) return false;

            if (!string.IsNullOrEmpty(q))
            {
                return net.Name.Contains(q, StringComparison.OrdinalIgnoreCase);
            }
            return true;
        }).ToList();

        FilteredNets.Clear();
        foreach (var item in matches)
            FilteredNets.Add(item);

        ShowingNetsLabel = $"Showing {FilteredNets.Count} of {_allNetItems.Count} nets";
    }

    private void ApplyBomFilters()
    {
        string q = BomSearchQuery.Trim();
        var matches = _allBomItems.Where(item =>
        {
            if (BomActiveFilter == "top" && item.IsBottom) return false;
            if (BomActiveFilter == "bottom" && !item.IsBottom) return false;
            if (BomActiveFilter == "unplaced" && item.IsPlaced) return false;

            if (!string.IsNullOrEmpty(q))
            {
                if (item.Value.Contains(q, StringComparison.OrdinalIgnoreCase)) return true;
                if (item.Package.Contains(q, StringComparison.OrdinalIgnoreCase)) return true;
                if (item.References.Any(r => r.Contains(q, StringComparison.OrdinalIgnoreCase))) return true;
                return false;
            }
            return true;
        }).ToList();

        IEnumerable<BomItemViewModel> sorted = BomSortColumn switch
        {
            "placed" => BomSortAscending ? matches.OrderBy(b => b.IsPlaced) : matches.OrderByDescending(b => b.IsPlaced),
            "value" => BomSortAscending ? matches.OrderBy(b => b.Value, NaturalStringComparer.Instance) : matches.OrderByDescending(b => b.Value, NaturalStringComparer.Instance),
            "footprint" => BomSortAscending ? matches.OrderBy(b => b.Package) : matches.OrderByDescending(b => b.Package),
            "quantity" => BomSortAscending ? matches.OrderBy(b => b.Quantity) : matches.OrderByDescending(b => b.Quantity),
            _ => BomSortAscending
                ? matches.OrderBy(b => b.References.FirstOrDefault() ?? "", NaturalStringComparer.Instance)
                : matches.OrderByDescending(b => b.References.FirstOrDefault() ?? "", NaturalStringComparer.Instance)
        };

        FilteredBomItems.Clear();
        foreach (var item in sorted)
            FilteredBomItems.Add(item);

        int totalParts = matches.Sum(m => m.Quantity);
        ShowingBomLabel = $"Showing {matches.Count} items ({totalParts} parts)";
    }

    private void UpdateAssemblyProgress()
    {
        PlacedComponentCount = _allBomItems.Where(b => b.IsPlaced).Sum(b => b.Quantity);
        double pct = TotalComponentCount > 0 ? (double)PlacedComponentCount / TotalComponentCount * 100.0 : 0.0;
        AssemblyProgressRatio = pct / 100.0;
        AssemblyProgressText = $"{PlacedComponentCount} / {TotalComponentCount} parts placed ({pct:0}%)";
    }

    private void OnBomItemPlacedChanged(BomItemViewModel item)
    {
        UpdateAssemblyProgress();
        if (BomActiveFilter == "unplaced")
        {
            ApplyBomFilters();
        }
    }

    public void SelectBomItemInternal(BomItemViewModel? item, bool flyToComponent, string? specificRef = null)
    {
        if (item is null)
        {
            foreach (var b in _allBomItems) b.IsSelected = false;
            _suppressSelectionCallback = true;
            SelectedBomItem = null;
            _suppressSelectionCallback = false;
            SelectedBomComponentIndex = -1;
            SelectedComponentRef = "";
            _canvas?.SetHighlightedFootprints(null, flyTo: false);
            return;
        }

        foreach (var b in _allBomItems) b.IsSelected = b == item;
        _suppressSelectionCallback = true;
        SelectedBomItem = item;
        _suppressSelectionCallback = false;

        if (!string.IsNullOrEmpty(specificRef) && item.References.Contains(specificRef, StringComparer.OrdinalIgnoreCase))
        {
            SelectedComponentRef = specificRef;
            SelectedBomComponentIndex = item.References.FindIndex(r => r.Equals(specificRef, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            SelectedBomComponentIndex = item.References.Count > 0 ? 0 : -1;
            SelectedComponentRef = SelectedBomComponentIndex >= 0 ? item.References[SelectedBomComponentIndex] : "";
        }

        // View must not adjust (neither zoom nor pan)
        _canvas?.SetHighlightedFootprints(item.References, SelectedComponentRef, flyTo: false);
    }

    private void OnHoveredPadChanged(PcbPad? pad, Point screenPt)
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

    private void OnPadClicked(PcbPad? pad)
    {
        if (IsBomTabActive)
        {
            if (pad is null || string.IsNullOrEmpty(pad.FpRef))
            {
                ClearBomSelection();
                return;
            }

            var matchingBom = _allBomItems.FirstOrDefault(b => b.References.Contains(pad.FpRef, StringComparer.OrdinalIgnoreCase));
            if (matchingBom is not null)
            {
                _suppressSelectionCallback = true;
                SelectedBomItem = matchingBom;
                _suppressSelectionCallback = false;
                SelectBomItemInternal(matchingBom, flyToComponent: false, specificRef: pad.FpRef);
            }
            return;
        }

        if (pad is null || pad.Net <= 0)
        {
            ClearSelection();
            return;
        }

        SelectNetInternal(pad.Net, pad, flyToFirstPin: false);
    }

    private void SelectNetInternal(int? netId, PcbPad? specificPad, bool flyToFirstPin)
    {
        if (netId is null || _currentBoard is null)
        {
            _suppressSelectionCallback = true;
            SelectedNetItem = null;
            _suppressSelectionCallback = false;
            HasActiveInspector = false;
            _activeNetPads.Clear();
            _currentPinIndex = -1;
            _canvas?.SetActiveNet(null);
            _canvas?.SetSelectedPad(null, false);
            return;
        }

        var netVm = _allNetItems.FirstOrDefault(n => n.Id == netId.Value);
        _suppressSelectionCallback = true;
        SelectedNetItem = netVm;
        _suppressSelectionCallback = false;

        _canvas?.SetActiveNet(netId.Value);

        // Gather pads belonging to this net sorted by component reference + pin number
        _activeNetPads = _currentBoard.Pads
            .Where(p => p.Net == netId.Value)
            .OrderBy(p => p.FpRef, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => int.TryParse(p.Num, out int n) ? n : int.MaxValue)
            .ThenBy(p => p.Num, StringComparer.OrdinalIgnoreCase)
            .ToList();

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

        UpdateInspectorDetails(flyToPin: flyToFirstPin);
    }

    private void UpdateInspectorDetails(bool flyToPin)
    {
        if (_currentBoard is null || SelectedNetItem is null)
        {
            HasActiveInspector = false;
            return;
        }

        var net = SelectedNetItem.Net;
        HasActiveInspector = true;
        InspectorNetName = string.IsNullOrEmpty(net.Name) ? "(unnamed)" : net.Name;
        InspectorNetSummary = $"{net.PadCount} pins • {net.ViaCount} vias • {net.TrackCount} segs • {net.Length:0.1f} mm";

        var allPinRefs = _activeNetPads.Select(p => $"{p.FpRef}.{p.Num}").ToList();
        InspectorConnectedPins = allPinRefs.Count > 0
            ? string.Join(", ", allPinRefs.Take(16)) + (allPinRefs.Count > 16 ? $" (+{allPinRefs.Count - 16} more)" : "")
            : "No component pins";

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

            _canvas?.SetSelectedPad(pad, flyToPad: flyToPin);
        }
        else
        {
            InspectorPinBadge = "0 Pins";
            InspectorPinTitle = InspectorNetName;
            InspectorPinMeta = "Copper net without component pads";
            _canvas?.SetSelectedPad(null, flyToPad: false);
        }
    }

    [RelayCommand]
    public void PrevPin()
    {
        if (_activeNetPads.Count == 0) return;
        _currentPinIndex = (_currentPinIndex - 1 + _activeNetPads.Count) % _activeNetPads.Count;
        UpdateInspectorDetails(flyToPin: true);
    }

    [RelayCommand]
    public void NextPin()
    {
        if (_activeNetPads.Count == 0) return;
        _currentPinIndex = (_currentPinIndex + 1) % _activeNetPads.Count;
        UpdateInspectorDetails(flyToPin: true);
    }

    [RelayCommand]
    public void FocusCurrentPin()
    {
        if (_currentPinIndex >= 0 && _currentPinIndex < _activeNetPads.Count)
        {
            UpdateInspectorDetails(flyToPin: true);
        }
    }

    [RelayCommand]
    public void ClearSelection()
    {
        SelectNetInternal(null, null, false);
    }

    [RelayCommand]
    public void ClearSearch()
    {
        SearchQuery = "";
    }

    [RelayCommand]
    public void SetFilter(string filter)
    {
        ActiveFilter = filter;
    }

    [RelayCommand]
    public void SwitchTab(string tab)
    {
        IsNetsTabActive = tab == "nets";
        IsBomTabActive = tab == "bom";
        IsLayersTabActive = tab == "layers";
        OnPropertyChanged(nameof(SidebarWidth));
    }

    [RelayCommand]
    public void SortBomBy(string column)
    {
        if (BomSortColumn.Equals(column, StringComparison.OrdinalIgnoreCase))
        {
            BomSortAscending = !BomSortAscending;
        }
        else
        {
            BomSortColumn = column.ToLowerInvariant();
            BomSortAscending = true;
        }
        ApplyBomFilters();
    }

    [RelayCommand]
    public void SetBomFilter(string filter)
    {
        BomActiveFilter = filter;
    }

    [RelayCommand]
    public void ClearBomSearch()
    {
        BomSearchQuery = "";
    }

    [RelayCommand]
    public void ClearBomSelection()
    {
        SelectBomItemInternal(null, flyToComponent: false);
    }

    [RelayCommand]
    public void PrevBomComponent()
    {
        if (SelectedBomItem is null || SelectedBomItem.References.Count <= 1) return;
        SelectedBomComponentIndex = (SelectedBomComponentIndex - 1 + SelectedBomItem.References.Count) % SelectedBomItem.References.Count;
        SelectedComponentRef = SelectedBomItem.References[SelectedBomComponentIndex];
        _canvas?.SetHighlightedFootprints(SelectedBomItem.References, SelectedComponentRef, flyTo: false);
    }

    [RelayCommand]
    public void NextBomComponent()
    {
        if (SelectedBomItem is null || SelectedBomItem.References.Count <= 1) return;
        SelectedBomComponentIndex = (SelectedBomComponentIndex + 1) % SelectedBomItem.References.Count;
        SelectedComponentRef = SelectedBomItem.References[SelectedBomComponentIndex];
        _canvas?.SetHighlightedFootprints(SelectedBomItem.References, SelectedComponentRef, flyTo: false);
    }

    [RelayCommand]
    public void SelectBomComponent(string componentRef)
    {
        if (SelectedBomItem is null || !SelectedBomItem.References.Contains(componentRef, StringComparer.OrdinalIgnoreCase)) return;
        SelectedComponentRef = componentRef;
        SelectedBomComponentIndex = SelectedBomItem.References.FindIndex(r => r.Equals(componentRef, StringComparison.OrdinalIgnoreCase));
        _canvas?.SetHighlightedFootprints(SelectedBomItem.References, SelectedComponentRef, flyTo: false);
    }

    [RelayCommand]
    public void MarkAllBomPlaced()
    {
        foreach (var item in _allBomItems)
            item.IsPlaced = true;
        UpdateAssemblyProgress();
        if (BomActiveFilter == "unplaced") ApplyBomFilters();
    }

    [RelayCommand]
    public void ResetBomPlaced()
    {
        foreach (var item in _allBomItems)
            item.IsPlaced = false;
        UpdateAssemblyProgress();
        if (BomActiveFilter == "unplaced") ApplyBomFilters();
    }

    [RelayCommand]
    public void ToggleLeftSidebar()
    {
        IsLeftSidebarOpen = !IsLeftSidebarOpen;
    }

    [RelayCommand]
    public void ZoomIn() => _canvas?.ZoomBy(1.25);

    [RelayCommand]
    public void ZoomOut() => _canvas?.ZoomBy(1.0 / 1.25);

    [RelayCommand]
    public void ZoomFit() => _canvas?.FitBoardToView();

    [RelayCommand]
    public void RotateClockwise() => _canvas?.RotateClockwise90();

    [RelayCommand]
    public void RotateCounterClockwise() => _canvas?.RotateCounterClockwise90();

    [RelayCommand]
    public void ToggleFlipBoard()
    {
        if (_canvas is null) return;
        _canvas.ToggleFlip();
        IsFlipped = _canvas.IsFlipped;
    }

    [RelayCommand]
    public void TogglePadLabels()
    {
        ShowPadNetNames = !ShowPadNetNames;
    }

    [RelayCommand]
    public void ToggleTheme()
    {
        IsDarkTheme = !IsDarkTheme;
        _canvas?.SetTheme(!IsDarkTheme);
        ThemeChanged?.Invoke(IsDarkTheme);
    }

    [RelayCommand]
    public async Task RequestOpenFileAsync()
    {
        if (OpenFileRequested is not null)
        {
            await OpenFileRequested.Invoke();
        }
    }

    [RelayCommand]
    public async Task OpenGitHubBrowserAsync()
    {
        IsGitHubTabActive = true;
        await GitHubBrowser.InitializeAsync();
    }

    [RelayCommand]
    public void SwitchToViewer()
    {
        IsGitHubTabActive = false;
    }



    [RelayCommand]
    public void ApplyLayerPreset(string preset)
    {
        if (_canvas is null) return;

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
            _canvas.SetLayerVisibility(l.LayerName, show);
        }

        if (preset == "top" && _canvas.IsFlipped)
        {
            _canvas.SetFlip(false);
            IsFlipped = false;
        }
        else if (preset == "bottom" && !_canvas.IsFlipped)
        {
            _canvas.SetFlip(true);
            IsFlipped = true;
        }
    }
}
