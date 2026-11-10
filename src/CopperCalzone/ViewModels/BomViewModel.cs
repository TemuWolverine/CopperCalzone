using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopperCalzone.Models;
using CopperCalzone.Services;

namespace CopperCalzone.ViewModels;

public sealed partial class BomViewModel : ObservableObject
{
    private List<BomItemViewModel> _allBomItems = [];

    [ObservableProperty]
    private double _assemblyProgressRatio;

    [ObservableProperty]
    private string _assemblyProgressText = "0 / 0 parts placed (0%)";

    [ObservableProperty]
    private string _bomActiveFilter = "all";

    [ObservableProperty]
    private double _bomColFpWidth = 140;

    // Adjustable column widths
    [ObservableProperty]
    private double _bomColPlacedWidth = 50;

    [ObservableProperty]
    private double _bomColQtyWidth = 50;

    [ObservableProperty]
    private double _bomColRefWidth = 110;

    [ObservableProperty]
    private double _bomColValWidth = 100;

    [ObservableProperty]
    private string _bomSearchQuery = "";

    [ObservableProperty]
    private bool _bomSortAscending = true;

    [ObservableProperty]
    private string _bomSortColumn = "references";

    [ObservableProperty]
    private int _placedComponentCount;

    [ObservableProperty]
    private int _selectedBomComponentIndex = -1;

    [ObservableProperty]
    private BomItemViewModel? _selectedBomItem;

    [ObservableProperty]
    private string _selectedComponentRef = "";

    [ObservableProperty]
    private string _showingBomLabel = "Showing 0 items";

    private bool _suppressSelectionCallback;

    [ObservableProperty]
    private int _totalBomCount;

    [ObservableProperty]
    private int _totalComponentCount;

    public event Action<IReadOnlyList<string>, string?>? FootprintHighlightRequested;
    public event Action<IReadOnlySet<string>>? PlacedFootprintsChanged;

    private bool _suppressPlacedNotification;

    public ObservableCollection<BomItemViewModel> FilteredBomItems { get; } = [];
    public bool IsBomFilterAll => BomActiveFilter == "all";
    public bool IsBomFilterBottom => BomActiveFilter == "bottom";
    public bool IsBomFilterTop => BomActiveFilter == "top";
    public bool IsBomFilterUnplaced => BomActiveFilter == "unplaced";

    public void ApplyBomFilters()
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

    [RelayCommand]
    public void ClearBomSearch()
    {
        BomSearchQuery = "";
    }

    [RelayCommand]
    public void ClearBomSelection()
    {
        SelectBomItemInternal(null);
    }

    public void ClearSelection()
    {
        foreach (var b in _allBomItems) b.IsSelected = false;
        _suppressSelectionCallback = true;
        SelectedBomItem = null;
        _suppressSelectionCallback = false;
        SelectedBomComponentIndex = -1;
        SelectedComponentRef = "";
        FootprintHighlightRequested?.Invoke([], null);
    }

    public void LoadBom(PcbBoard board)
    {
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

        ClearSelection();
        ApplyBomFilters();
        NotifyPlacedFootprintsChanged();
    }

    [RelayCommand]
    public void MarkAllBomPlaced()
    {
        _suppressPlacedNotification = true;
        try
        {
            foreach (var item in _allBomItems)
                item.IsPlaced = true;
        }
        finally
        {
            _suppressPlacedNotification = false;
        }
        UpdateAssemblyProgress();
        if (BomActiveFilter == "unplaced") ApplyBomFilters();
        NotifyPlacedFootprintsChanged();
    }

    [RelayCommand]
    public void NextBomComponent()
    {
        if (SelectedBomItem is null || SelectedBomItem.References.Count <= 1) return;
        SelectedBomComponentIndex = (SelectedBomComponentIndex + 1) % SelectedBomItem.References.Count;
        SelectedComponentRef = SelectedBomItem.References[SelectedBomComponentIndex];
        FootprintHighlightRequested?.Invoke(SelectedBomItem.References, SelectedComponentRef);
    }

    [RelayCommand]
    public void PrevBomComponent()
    {
        if (SelectedBomItem is null || SelectedBomItem.References.Count <= 1) return;
        SelectedBomComponentIndex = (SelectedBomComponentIndex - 1 + SelectedBomItem.References.Count) % SelectedBomItem.References.Count;
        SelectedComponentRef = SelectedBomItem.References[SelectedBomComponentIndex];
        FootprintHighlightRequested?.Invoke(SelectedBomItem.References, SelectedComponentRef);
    }

    [RelayCommand]
    public void ResetBomPlaced()
    {
        _suppressPlacedNotification = true;
        try
        {
            foreach (var item in _allBomItems)
                item.IsPlaced = false;
        }
        finally
        {
            _suppressPlacedNotification = false;
        }
        UpdateAssemblyProgress();
        if (BomActiveFilter == "unplaced") ApplyBomFilters();
        NotifyPlacedFootprintsChanged();
    }

    [RelayCommand]
    public void SelectBomComponent(string componentRef)
    {
        if (SelectedBomItem is null || !SelectedBomItem.References.Contains(componentRef, StringComparer.OrdinalIgnoreCase)) return;
        SelectedComponentRef = componentRef;
        SelectedBomComponentIndex = SelectedBomItem.References.FindIndex(r => r.Equals(componentRef, StringComparison.OrdinalIgnoreCase));
        FootprintHighlightRequested?.Invoke(SelectedBomItem.References, SelectedComponentRef);
    }

    public void SelectBomItemInternal(BomItemViewModel? item, string? specificRef = null)
    {
        if (item is null)
        {
            ClearSelection();
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

        FootprintHighlightRequested?.Invoke(item.References, SelectedComponentRef);
    }

    public void SelectByFootprintRef(string fpRef)
    {
        var targetGroup = _allBomItems.FirstOrDefault(b => b.References.Contains(fpRef, StringComparer.OrdinalIgnoreCase));
        if (targetGroup is not null)
        {
            SelectBomItemInternal(targetGroup, fpRef);
        }
        else
        {
            ClearSelection();
        }
    }

    [RelayCommand]
    public void SetBomFilter(string filter)
    {
        BomActiveFilter = filter;
    }

    [RelayCommand]
    public void SortBomBy(string column)
    {
        if (BomSortColumn != null && BomSortColumn.Equals(column, StringComparison.OrdinalIgnoreCase))
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

    public void UpdateAssemblyProgress()
    {
        int totalParts = _allBomItems.Sum(b => b.Quantity);
        int placedParts = _allBomItems.Where(b => b.IsPlaced).Sum(b => b.Quantity);
        PlacedComponentCount = placedParts;
        int pct = totalParts > 0 ? (int)Math.Round((double)placedParts / totalParts * 100.0) : 0;
        AssemblyProgressText = $"{placedParts} / {totalParts} parts placed ({pct}%)";
        AssemblyProgressRatio = totalParts > 0 ? (double)placedParts / totalParts : 0.0;
    }

    partial void OnBomActiveFilterChanged(string value)
    {
        OnPropertyChanged(nameof(IsBomFilterAll));
        OnPropertyChanged(nameof(IsBomFilterTop));
        OnPropertyChanged(nameof(IsBomFilterBottom));
        OnPropertyChanged(nameof(IsBomFilterUnplaced));
        ApplyBomFilters();
    }

    partial void OnBomColFpWidthChanged(double value)
    {
        SettingsService.Instance.Current.BomColFpWidth = value; SettingsService.Instance.Save();
    }

    partial void OnBomColPlacedWidthChanged(double value)
    {
        SettingsService.Instance.Current.BomColPlacedWidth = value; SettingsService.Instance.Save();
    }

    partial void OnBomColQtyWidthChanged(double value)
    {
        SettingsService.Instance.Current.BomColQtyWidth = value; SettingsService.Instance.Save();
    }

    partial void OnBomColRefWidthChanged(double value)
    {
        SettingsService.Instance.Current.BomColRefWidth = value; SettingsService.Instance.Save();
    }

    partial void OnBomColValWidthChanged(double value)
    {
        SettingsService.Instance.Current.BomColValWidth = value; SettingsService.Instance.Save();
    }

    private void OnBomItemPlacedChanged(BomItemViewModel item)
    {
        UpdateAssemblyProgress();
        if (BomActiveFilter == "unplaced")
        {
            ApplyBomFilters();
        }
        if (!_suppressPlacedNotification)
        {
            NotifyPlacedFootprintsChanged();
        }
    }

    public HashSet<string> GetPlacedFootprintRefs()
    {
        var placed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in _allBomItems)
        {
            if (item.IsPlaced)
            {
                foreach (var r in item.References)
                {
                    placed.Add(r);
                }
            }
        }
        return placed;
    }

    public void NotifyPlacedFootprintsChanged()
    {
        PlacedFootprintsChanged?.Invoke(GetPlacedFootprintRefs());
    }

    partial void OnBomSearchQueryChanged(string value) => ApplyBomFilters();

    partial void OnSelectedBomItemChanged(BomItemViewModel? value)
    {
        if (_suppressSelectionCallback) return;
        SelectBomItemInternal(value);
    }
}
