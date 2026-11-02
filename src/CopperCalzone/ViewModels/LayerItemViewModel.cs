using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CopperCalzone.Rendering;

namespace CopperCalzone.ViewModels;

public partial class LayerItemViewModel : ObservableObject
{
    private readonly Action<string, bool> _onVisibilityChanged;

    [ObservableProperty]
    private bool _isVisible;

    private bool _suppressNotification;
    public IBrush ColorBrush { get; }
    public string DisplayName { get; }
    public string LayerName { get; }

    public LayerItemViewModel(string layerName, string displayName, bool isVisible, Action<string, bool> onVisibilityChanged)
    {
        LayerName = layerName;
        DisplayName = displayName;
        _isVisible = isVisible;
        _onVisibilityChanged = onVisibilityChanged;

        if (PcbSkiaDrawOperation.LayerColors.TryGetValue(layerName, out var skColor))
        {
            ColorBrush = new SolidColorBrush(Color.FromArgb(skColor.Alpha, skColor.Red, skColor.Green, skColor.Blue));
        }
        else
        {
            ColorBrush = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
        }
    }

    public void SetVisibleSilent(bool visible)
    {
        _suppressNotification = true;
        IsVisible = visible;
        _suppressNotification = false;
    }

    partial void OnIsVisibleChanged(bool value)
    {
        if (!_suppressNotification)
        {
            _onVisibilityChanged(LayerName, value);
        }
    }
}
