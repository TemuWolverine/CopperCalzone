using Avalonia;
using Avalonia.Controls;

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

