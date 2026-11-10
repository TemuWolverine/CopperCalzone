using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopperCalzone.Models;
using CopperCalzone.Services;
using Octokit;
using System.Collections.ObjectModel;

namespace CopperCalzone.ViewModels;

public partial class GitHubBrowserViewModel : ObservableObject
{
    private readonly GitHubAuthService _authService;
    private readonly GitHubPcbService _pcbService;
    private List<Repository> _allRepositories = [];

    [ObservableProperty]
    private string _currentPath = "";

    [ObservableProperty]
    private GitHubBrowserStep _currentStep = GitHubBrowserStep.NotAuthenticated;

    [ObservableProperty]
    private string _currentUserAvatarUrl = "";

    [ObservableProperty]
    private string _currentUserLogin = "";

    [ObservableProperty]
    private string _currentUserName = "";

    [ObservableProperty]
    private string _errorMessage = "";

    [ObservableProperty]
    private bool _isLoading;

    private CancellationTokenSource? _loginCts;

    [ObservableProperty]
    private string _repoSearchQuery = "";

    [ObservableProperty]
    private Repository? _selectedRepository;

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private string _userCode = "";

    [ObservableProperty]
    private string _verificationUri = "https://github.com/login/device";

    public event Func<string, string, Task>? OpenBrowserAndCopyRequested;

    public event Action<string, string, List<(string fileName, string content)>>? PcbContentReady;

    public event Action? RequestClose;

    public ObservableCollection<BreadcrumbItem> Breadcrumbs { get; } = [];
    public ObservableCollection<GitHubItemModel> CurrentFolderItems { get; } = [];
    public ObservableCollection<Repository> FilteredRepositories { get; } = [];
    public bool IsDeviceCodePending => CurrentStep == GitHubBrowserStep.DeviceCodePending;
    public bool IsFileNavigation => CurrentStep == GitHubBrowserStep.FileNavigation;
    public bool IsNotAuthenticated => CurrentStep == GitHubBrowserStep.NotAuthenticated;
    public bool IsRepoList => CurrentStep == GitHubBrowserStep.RepoList;

    public GitHubBrowserViewModel(GitHubAuthService authService, GitHubPcbService pcbService)
    {
        _authService = authService;
        _pcbService = pcbService;
        _authService.AuthStateChanged += OnAuthStateChanged;
    }

    [RelayCommand]
    public void BackToRepos()
    {
        SelectedRepository = null;
        CurrentPath = "";
        CurrentFolderItems.Clear();
        Breadcrumbs.Clear();
        CurrentStep = GitHubBrowserStep.RepoList;
    }

    [RelayCommand]
    public void CancelLogin()
    {
        _loginCts?.Cancel();
        CurrentStep = GitHubBrowserStep.NotAuthenticated;
        StatusMessage = "";
        ErrorMessage = "";
    }

    [RelayCommand]
    public void Close()
    {
        _loginCts?.Cancel();
        RequestClose?.Invoke();
    }

    [RelayCommand]
    public async Task CopyCodeAndOpenBrowserAsync()
    {
        if (OpenBrowserAndCopyRequested != null)
        {
            await OpenBrowserAndCopyRequested.Invoke(UserCode, VerificationUri);
        }
    }

    public async Task InitializeAsync()
    {
        ErrorMessage = "";
        if (_authService.IsAuthenticated)
        {
            if (_authService.CurrentAuthState?.User is { } user)
            {
                CurrentUserName = user.Name;
                CurrentUserLogin = user.Login;
                CurrentUserAvatarUrl = user.AvatarUrl;
            }
            CurrentStep = SelectedRepository != null ? GitHubBrowserStep.FileNavigation : GitHubBrowserStep.RepoList;
            if (_allRepositories.Count == 0)
            {
                await LoadRepositoriesAsync();
            }
            return;
        }

        IsLoading = true;
        StatusMessage = "Checking saved session...";
        bool restored = await _authService.TryRestoreSessionAsync();
        IsLoading = false;

        if (restored)
        {
            CurrentStep = SelectedRepository != null ? GitHubBrowserStep.FileNavigation : GitHubBrowserStep.RepoList;
            if (_allRepositories.Count == 0)
            {
                await LoadRepositoriesAsync();
            }
        }
        else
        {
            CurrentStep = GitHubBrowserStep.NotAuthenticated;
        }
    }

    [RelayCommand]
    public async Task LoadPcbFileAsync(GitHubItemModel item)
    {
        if (SelectedRepository == null || item.IsDirectory) return;

        ErrorMessage = "";
        IsLoading = true;
        StatusMessage = $"Downloading '{item.Name}'...";

        try
        {
            string content = await _pcbService.DownloadPcbFileAsync(SelectedRepository, item);

            StatusMessage = "Checking for schematic files...";
            var schematics = await _pcbService.DownloadSchematicFilesForPcbAsync(SelectedRepository, item.Path);

            PcbContentReady?.Invoke(content, item.Name, schematics);
            RequestClose?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to download file: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task LogoutAsync()
    {
        _loginCts?.Cancel();
        await _authService.LogoutAsync();
        _allRepositories.Clear();
        FilteredRepositories.Clear();
        CurrentFolderItems.Clear();
        Breadcrumbs.Clear();
        SelectedRepository = null;
        CurrentPath = "";
        CurrentStep = GitHubBrowserStep.NotAuthenticated;
    }

    [RelayCommand]
    public async Task NavigateBreadcrumbAsync(string path)
    {
        await LoadFolderContentsAsync(path);
    }

    [RelayCommand]
    public async Task NavigateFolderAsync(GitHubItemModel item)
    {
        if (item.IsDirectory)
        {
            await LoadFolderContentsAsync(item.Path);
        }
        else
        {
            await LoadPcbFileAsync(item);
        }
    }

    [RelayCommand]
    public async Task NavigatePreviousAsync()
    {
        if (string.IsNullOrEmpty(CurrentPath))
        {
            BackToRepos();
            return;
        }

        int lastSlash = CurrentPath.LastIndexOf('/');
        string parentPath = lastSlash >= 0 ? CurrentPath[..lastSlash] : "";
        await LoadFolderContentsAsync(parentPath);
    }

    [RelayCommand]
    public async Task NavigateUpAsync()
    {
        await NavigatePreviousAsync();
    }

    [RelayCommand]
    public async Task RefreshRepositoriesAsync()
    {
        await LoadRepositoriesAsync();
    }

    [RelayCommand]
    public async Task SelectRepositoryAsync(Repository repo)
    {
        SelectedRepository = repo;
        CurrentPath = "";
        CurrentStep = GitHubBrowserStep.FileNavigation;
        await LoadFolderContentsAsync("");
    }

    [RelayCommand]
    public async Task StartLoginAsync()
    {
        ErrorMessage = "";
        IsLoading = true;
        StatusMessage = "Requesting device code from GitHub...";

        _loginCts?.Cancel();
        _loginCts = new CancellationTokenSource();
        var ct = _loginCts.Token;

        try
        {
            var deviceFlow = await _authService.StartDeviceFlowAsync(ct);
            UserCode = deviceFlow.UserCode;
            VerificationUri = string.IsNullOrWhiteSpace(deviceFlow.VerificationUri)
                ? "https://github.com/login/device"
                : deviceFlow.VerificationUri;

            CurrentStep = GitHubBrowserStep.DeviceCodePending;
            IsLoading = false;
            StatusMessage = "Waiting for authorization in browser...";

            // Poll for access token in background
            var state = await _authService.WaitForTokenAsync(deviceFlow, ct);
            CurrentUserName = state.User?.Name ?? "";
            CurrentUserLogin = state.User?.Login ?? "";
            CurrentUserAvatarUrl = state.User?.AvatarUrl ?? "";

            StatusMessage = "Logged in successfully!";
            CurrentStep = GitHubBrowserStep.RepoList;
            await LoadRepositoriesAsync();
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Login cancelled.";
            CurrentStep = GitHubBrowserStep.NotAuthenticated;
        }
        catch (Octokit.ApiException apiEx)
        {
            ErrorMessage = $"GitHub API Error: {apiEx.Message}";
            CurrentStep = GitHubBrowserStep.NotAuthenticated;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Login failed: {ex.Message}";
            CurrentStep = GitHubBrowserStep.NotAuthenticated;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyRepoFilter()
    {
        FilteredRepositories.Clear();
        string q = RepoSearchQuery.Trim();
        var matches = _allRepositories.Where(r =>
            string.IsNullOrEmpty(q) ||
            r.FullName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            (r.Description != null && r.Description.Contains(q, StringComparison.OrdinalIgnoreCase))
        );

        foreach (var r in matches)
        {
            FilteredRepositories.Add(r);
        }
    }

    private async Task LoadFolderContentsAsync(string path)
    {
        if (SelectedRepository == null) return;

        ErrorMessage = "";
        IsLoading = true;
        StatusMessage = $"Loading {(string.IsNullOrEmpty(path) ? "root" : path)}...";
        CurrentPath = path;

        UpdateBreadcrumbs(path);

        try
        {
            var items = await _pcbService.GetFolderContentsAsync(SelectedRepository.Id, path);
            CurrentFolderItems.Clear();
            foreach (var item in items)
            {
                CurrentFolderItems.Add(item);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to read folder contents: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadRepositoriesAsync()
    {
        ErrorMessage = "";
        IsLoading = true;
        StatusMessage = "Loading repositories...";

        try
        {
            var repos = await _pcbService.GetAllAccessibleRepositoriesAsync();
            _allRepositories = repos.ToList();
            ApplyRepoFilter();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load repositories: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void OnAuthStateChanged(bool isAuthenticated)
    {
        if (isAuthenticated && _authService.CurrentAuthState?.User is { } user)
        {
            CurrentUserName = user.Name;
            CurrentUserLogin = user.Login;
            CurrentUserAvatarUrl = user.AvatarUrl;
            CurrentStep = SelectedRepository != null ? GitHubBrowserStep.FileNavigation : GitHubBrowserStep.RepoList;
        }
        else
        {
            CurrentUserName = "";
            CurrentUserLogin = "";
            CurrentUserAvatarUrl = "";
            SelectedRepository = null;
            CurrentPath = "";
            CurrentFolderItems.Clear();
            Breadcrumbs.Clear();
            CurrentStep = GitHubBrowserStep.NotAuthenticated;
        }
    }

    partial void OnCurrentStepChanged(GitHubBrowserStep value)
    {
        OnPropertyChanged(nameof(IsNotAuthenticated));
        OnPropertyChanged(nameof(IsDeviceCodePending));
        OnPropertyChanged(nameof(IsRepoList));
        OnPropertyChanged(nameof(IsFileNavigation));
    }

    partial void OnRepoSearchQueryChanged(string value)
    {
        ApplyRepoFilter();
    }

    private void UpdateBreadcrumbs(string path)
    {
        Breadcrumbs.Clear();
        Breadcrumbs.Add(new BreadcrumbItem(SelectedRepository?.Name ?? "root", ""));

        if (!string.IsNullOrWhiteSpace(path))
        {
            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            string acc = "";
            foreach (var part in parts)
            {
                acc = string.IsNullOrEmpty(acc) ? part : $"{acc}/{part}";
                Breadcrumbs.Add(new BreadcrumbItem(part, acc));
            }
        }
    }
}
