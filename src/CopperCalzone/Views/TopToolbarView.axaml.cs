using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Material.Icons;

namespace CopperCalzone.Views;

public partial class TopToolbarView : UserControl
{
    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<TopToolbarView, bool>(
            nameof(IsCompact),
            defaultValue: OperatingSystem.IsAndroid());

    public bool IsCompact
    {
        get => GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    public TopToolbarView()
    {
        InitializeComponent();
        if (RootBorder != null)
        {
            RootBorder.Padding = Padding;
        }
        if (WindowControlsPanel != null)
        {
            WindowControlsPanel.IsVisible = !OperatingSystem.IsAndroid();
        }
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed &&
            TopLevel.GetTopLevel(this) is Window window)
        {
            if (e.ClickCount == 2)
            {
                ToggleMaximize(window);
            }
            else
            {
                window.BeginMoveDrag(e);
            }
        }
    }

    private void OnMinimizeClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is Window window)
        {
            window.WindowState = WindowState.Minimized;
        }
    }

    private void OnMaximizeRestoreClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is Window window)
        {
            ToggleMaximize(window);
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is Window window)
        {
            window.Close();
        }
    }

    private void ToggleMaximize(Window window)
    {
        window.WindowState = window.WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

        if (MaximizeIcon != null)
        {
            MaximizeIcon.Kind = window.WindowState == WindowState.Maximized
                ? MaterialIconKind.WindowRestore
                : MaterialIconKind.WindowMaximize;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PaddingProperty && RootBorder != null)
        {
            RootBorder.Padding = change.GetNewValue<Thickness>();
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (e.NewSize.Width > 0)
        {
            IsCompact = e.NewSize.Width < 760;
        }
    }
}

