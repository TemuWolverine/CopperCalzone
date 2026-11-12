using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using CopperCalzone.Parsing;
using CopperCalzone.Services;
using CopperCalzone.ViewModels;
using CopperCalzone.Views;

namespace CopperCalzone;

public partial class App : Application
{
    private static readonly (string Key, double BaseSize)[] FontSizeTokens =
    [
        ("FontSizeMicro", 11.0),
        ("FontSizeXS", 13.0),
        ("FontSizeSM", 14.0),
        ("FontSizeMD", 16.0),
        ("FontSizeLG", 18.0),
        ("FontSizeXL", 24.0),
        ("FontSizeXXL", 32.0),
        ("FontSizeCode", 34.0)
    ];

    public static void ApplyFontScale(double scalePercent)
    {
        if (Current is null) return;

        double clamped = Math.Clamp(scalePercent, 80.0, 150.0);
        double scale = clamped / 100.0;

        foreach (var (key, baseSize) in FontSizeTokens)
        {
            Current.Resources[key] = Math.Round(baseSize * scale, 1);
        }
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        ApplyFontScale(SettingsService.Instance.Current.FontScalePercent);
        var vm = new MainViewModel();

        // Load initial board from CLI argument on Desktop if specified
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopArgs &&
            desktopArgs.Args is { Length: > 0 } &&
            File.Exists(desktopArgs.Args[0]))
        {
            try
            {
                string path = desktopArgs.Args[0];
                string content = File.ReadAllText(path);
                var board = BoardLoader.LoadFromContent(content, Path.GetFileName(path));
                vm.LoadBoard(board);
            }
            catch
            {
                // Gracefully ignore if CLI file fails to load
            }
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = vm
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = vm
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
