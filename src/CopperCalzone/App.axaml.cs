using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Material.Styles.Themes;
using Material.Styles.Themes.Base;
using CopperCalzone.Parsing;
using CopperCalzone.ViewModels;
using CopperCalzone.Views;

namespace CopperCalzone;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var vm = new MainViewModel();
        vm.ThemeChanged += isDark =>
        {
            RequestedThemeVariant = isDark ? ThemeVariant.Dark : ThemeVariant.Light;
            var materialTheme = this.LocateMaterialTheme<MaterialTheme>();
            if (materialTheme != null)
            {
                materialTheme.BaseTheme = isDark ? BaseThemeMode.Dark : BaseThemeMode.Light;
            }
        };

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
