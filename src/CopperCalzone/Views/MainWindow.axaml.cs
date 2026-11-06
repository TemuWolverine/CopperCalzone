using Avalonia.Controls;

namespace CopperCalzone.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        MainView.ImmersiveModeHandler = isImmersive =>
        {
            WindowState = isImmersive ? WindowState.FullScreen : WindowState.Normal;
        };
    }
}
