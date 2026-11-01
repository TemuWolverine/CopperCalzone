using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CopperCalzone.Rendering;

namespace CopperCalzone.ViewModels;

public partial class LayerItemViewModel : ObservableObject
{
    private readonly Action<string, bool> _onVisibilityChanged;

    public string LayerName { get; }
    public string DisplayName { get; }
    public IBrush ColorBrush { get; }

    private bool _suppressNotification;

    [ObservableProperty]
    private bool _isVisible;

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

    partial void OnIsVisibleChanged(bool value)
    {
        if (!_suppressNotification)
        {
            _onVisibilityChanged(LayerName, value);
        }
    }

    public void SetVisibleSilent(bool visible)
    {
        _suppressNotification = true;
        IsVisible = visible;
        _suppressNotification = false;
    }
}
