using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using CopperCalzone.Parsing;
using CopperCalzone.ViewModels;

namespace CopperCalzone.Views;

public partial class MainView : UserControl
{
    private static Thickness _osSafeInsets = OperatingSystem.IsAndroid() ? new Thickness(0, 24, 0, 0) : default;
    private static MainView? _currentInstance;

    public static void SetOsSafeInsets(double left, double top, double right, double bottom)
    {
        _osSafeInsets = new Thickness(left, top, right, bottom);
        _currentInstance?.ApplySafeArea(_osSafeInsets);
    }

    public MainView()
    {
        InitializeComponent();
        _currentInstance = this;
        AddHandler(DragDrop.DropEvent, OnDropAsync);
        ApplySafeArea(_osSafeInsets);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is MainViewModel vm)
        {
            vm.AttachCanvas(PcbCanvas);
            vm.OpenFileRequested -= OnOpenFileRequestedAsync;
            vm.OpenFileRequested += OnOpenFileRequestedAsync;
            vm.PropertyChanged += (s, ev) =>
            {
                if (ev?.PropertyName is nameof(MainViewModel.SidebarWidth) or nameof(MainViewModel.IsBomTabActive))
                {
                    UpdateLayoutMode(Bounds.Width);
                }
            };
        }
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (DataContext is MainViewModel vm)
        {
            vm.AttachCanvas(PcbCanvas);
        }

        if (!OperatingSystem.IsAndroid())
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.InsetsManager != null)
            {
                ApplySafeArea(topLevel.InsetsManager.SafeAreaPadding);
                topLevel.InsetsManager.SafeAreaChanged += (s, ev) => ApplySafeArea(ev.SafeAreaPadding);
            }
        }
        else
        {
            ApplySafeArea(_osSafeInsets);
        }

        UpdateLayoutMode(Bounds.Width);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateLayoutMode(e.NewSize.Width);
    }

    public void ApplySafeArea(Thickness safeArea)
    {
        TopToolbar.Padding = new Thickness(
            Math.Max(12, safeArea.Left + 12),
            safeArea.Top,
            Math.Max(12, safeArea.Right + 12),
            0);

        ViewportOverlay.Margin = new Thickness(0, 0, safeArea.Right, safeArea.Bottom);
    }

    private void UpdateLayoutMode(double width)
    {
        if (width <= 0) return;
        bool isCompact = width < 760;
        double targetWidth = DataContext is MainViewModel vm ? vm.SidebarWidth : 340;
        if (isCompact)
        {
            Grid.SetColumnSpan(Sidebar, 2);
            Sidebar.ZIndex = 10;
            Sidebar.MaxWidth = Math.Max(260, width - 20);
        }
        else
        {
            Grid.SetColumnSpan(Sidebar, 1);
            Sidebar.ZIndex = 0;
            Sidebar.MaxWidth = Math.Max(340, targetWidth);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        // Ignore single-key shortcuts when typing inside a TextBox
        if (e.Source is TextBox && e.Key != Key.Escape)
            return;

        if (DataContext is not MainViewModel vm)
            return;

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.O)
        {
            _ = OnOpenFileRequestedAsync();
            e.Handled = true;
            return;
        }

        switch (e.Key)
        {
            case Key.OemPlus:
            case Key.Add:
                vm.ZoomIn();
                e.Handled = true;
                break;

            case Key.OemMinus:
            case Key.Subtract:
                vm.ZoomOut();
                e.Handled = true;
                break;

            case Key.Home:
                vm.ZoomFit();
                e.Handled = true;
                break;

            case Key.R:
                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                    vm.RotateCounterClockwise();
                else
                    vm.RotateClockwise();
                e.Handled = true;
                break;

            case Key.F:
                vm.ToggleFlipBoard();
                e.Handled = true;
                break;

            case Key.N:
                vm.TogglePadLabels();
                e.Handled = true;
                break;

            case Key.OemOpenBrackets:
            case Key.Left:
                vm.PrevPin();
                e.Handled = true;
                break;

            case Key.OemCloseBrackets:
            case Key.Right:
                vm.NextPin();
                e.Handled = true;
                break;

            case Key.Escape:
                vm.ClearSelection();
                e.Handled = true;
                break;
        }
    }

    private async Task OnOpenFileRequestedAsync()
    {
        if (DataContext is not MainViewModel vm) return;
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open KiCad PCB or CopperCalzone File",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("KiCad PCB & CopperCalzone Files")
                {
                    Patterns = ["*.kicad_pcb", "*.json", "*.html", "*.htm"],
                    MimeTypes = ["application/octet-stream", "application/json", "text/html", "text/plain", "*/*"]
                },
                FilePickerFileTypes.All
            ]
        });

        if (files is { Count: > 0 })
        {
            var file = files[0];
            try
            {
                await using var stream = await file.OpenReadAsync();
                var board = await BoardLoader.LoadFromStreamAsync(stream, file.Name);
                vm.LoadBoard(board);
            }
            catch (Exception ex)
            {
                vm.BoardSubtitle = $"Error loading '{file.Name}': {ex.Message}";
            }
        }
    }

    private async void OnDropAsync(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        var files = e.Data.GetFiles()?.ToList();
        if (files is { Count: > 0 } && files[0] is IStorageFile storageFile)
        {
            try
            {
                await using var stream = await storageFile.OpenReadAsync();
                var board = await BoardLoader.LoadFromStreamAsync(stream, storageFile.Name);
                vm.LoadBoard(board);
            }
            catch (Exception ex)
            {
                vm.BoardSubtitle = $"Error loading '{storageFile.Name}': {ex.Message}";
            }
        }
    }
}
