using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopperCalzone.Models;
using System.Collections.ObjectModel;

namespace CopperCalzone.ViewModels;

public sealed partial class NetsViewModel : ObservableObject
{
    private List<NetItemViewModel> _allNetItems = [];

    [ObservableProperty]
    private string _searchQuery = "";

    [ObservableProperty]
    private NetItemViewModel? _selectedNetItem;

    [ObservableProperty]
    private string _showingNetsLabel = "Showing 0 nets";

    private bool _suppressSelectionCallback;

    [ObservableProperty]
    private int _totalNetCount;

    public event Action<NetItemViewModel?>? NetSelected;

    public ObservableCollection<NetItemViewModel> FilteredNets { get; } = [];

    public void ApplyFilters()
    {
        FilteredNets.Clear();
        var q = SearchQuery.Trim();
        var query = string.IsNullOrEmpty(q)
            ? _allNetItems
            : _allNetItems.Where(n => n.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                                      n.Id.ToString() == q);

        foreach (var net in query)
        {
            FilteredNets.Add(net);
        }

        ShowingNetsLabel = $"Showing {FilteredNets.Count} of {_allNetItems.Count} nets";
    }

    [RelayCommand]
    public void ClearNetSelection()
    {
        ClearSelection();
        NetSelected?.Invoke(null);
    }

    [RelayCommand]
    public void ClearSearch()
    {
        SearchQuery = "";
    }

    public void ClearSelection()
    {
        _suppressSelectionCallback = true;
        SelectedNetItem = null;
        _suppressSelectionCallback = false;
    }

    public void LoadNets(IReadOnlyList<PcbNet> nets)
    {
        _allNetItems = nets
            .OrderByDescending(n => n.PadCount)
            .ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
            .Select(n => new NetItemViewModel(n))
            .ToList();

        TotalNetCount = _allNetItems.Count;
        ClearSelection();
        ApplyFilters();
    }

    public void SelectNetById(int netId)
    {
        var item = _allNetItems.FirstOrDefault(n => n.Id == netId);
        SelectedNetItem = item;
    }

    partial void OnSearchQueryChanged(string value) => ApplyFilters();

    partial void OnSelectedNetItemChanged(NetItemViewModel? value)
    {
        if (_suppressSelectionCallback) return;
        NetSelected?.Invoke(value);
    }
}
