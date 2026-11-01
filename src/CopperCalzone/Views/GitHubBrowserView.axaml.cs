using Avalonia.Controls;
using CopperCalzone.ViewModels;

namespace CopperCalzone.Views;

public partial class GitHubBrowserView : UserControl
{
    public GitHubBrowserView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is GitHubBrowserViewModel vm)
        {
            vm.OpenBrowserAndCopyRequested -= OnOpenBrowserAndCopyRequestedAsync;
            vm.OpenBrowserAndCopyRequested += OnOpenBrowserAndCopyRequestedAsync;
        }
    }

    private async Task OnOpenBrowserAndCopyRequestedAsync(string userCode, string verificationUri)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        // 1. Copy code to clipboard using TopLevel Clipboard (works across Desktop & Android)
        if (topLevel.Clipboard is { } clipboard && !string.IsNullOrEmpty(userCode))
        {
            try
            {
                await clipboard.SetTextAsync(userCode);
            }
            catch
            {
                // Continue if clipboard fails
            }
        }

        // 2. Launch browser using Avalonia's cross-platform ILauncher
        if (topLevel.Launcher is { } launcher && !string.IsNullOrEmpty(verificationUri))
        {
            try
            {
                if (Uri.TryCreate(verificationUri, UriKind.Absolute, out var uri))
                {
                    bool launched = await launcher.LaunchUriAsync(uri);
                    if (launched) return;
                }
            }
            catch
            {
                // Fall back to Process.Start on platforms that support it
            }
        }

        // Fallback for desktop if launcher returned false or failed
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = verificationUri,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            if (DataContext is GitHubBrowserViewModel vm)
            {
                vm.ErrorMessage = $"Could not launch browser: {ex.Message}. Please open {verificationUri} manually.";
            }
        }
    }
}
