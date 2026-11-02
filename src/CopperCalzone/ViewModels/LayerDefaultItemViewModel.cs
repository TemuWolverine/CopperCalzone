using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CopperCalzone.ViewModels;

public sealed partial class LayerDefaultItemViewModel(
    string name,
    string displayName,
    string hexColor,
    bool isVisible,
    Action? onChanged = null)
    : ObservableObject
{
    public string Name { get; } = name;
    public string DisplayName { get; } = displayName;
    public IBrush ColorBrush { get; } = new SolidColorBrush(Color.Parse(hexColor));

    [ObservableProperty]
    private bool _isVisible = isVisible;

    partial void OnIsVisibleChanged(bool value)
    {
        onChanged?.Invoke();
    }
}
