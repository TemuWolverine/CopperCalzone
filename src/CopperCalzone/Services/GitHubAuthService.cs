using CopperCalzone.Models;
using Octokit;
using System.Text.Json;

namespace CopperCalzone.Services;

public class GitHubAuthService
{
    public const string DefaultClientId = "Iv23lioVSqQ8oPxlU1dR";
    public const string ProductHeader = "CopperCalzone-PcbBrowser";

    private static readonly string AuthStorageDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CopperCalzone"
    );
    private static readonly string AuthStorageFile = Path.Combine(AuthStorageDir, "github_auth.json");

    private GitHubClient? _client;
    private GitHubAuthState? _currentAuthState;

    public GitHubAuthState? CurrentAuthState => _currentAuthState;
    public bool IsAuthenticated => _currentAuthState != null && !string.IsNullOrWhiteSpace(_currentAuthState.AccessToken);
    public GitHubClient Client => _client ??= CreateClient();

    public event Action<bool>? AuthStateChanged;

    public GitHubAuthService()
    {
        _client = CreateClient();
    }

    private GitHubClient CreateClient()
    {
        var client = new GitHubClient(new ProductHeaderValue(ProductHeader));
        if (_currentAuthState != null && !string.IsNullOrWhiteSpace(_currentAuthState.AccessToken))
        {
            client.Credentials = new Credentials(_currentAuthState.AccessToken);
        }
        return client;
    }

    public async Task<bool> TryRestoreSessionAsync()
    {
        try
        {
            if (!File.Exists(AuthStorageFile))
                return false;

            string json = await File.ReadAllTextAsync(AuthStorageFile);
            var state = JsonSerializer.Deserialize<GitHubAuthState>(json);
            if (state == null || string.IsNullOrWhiteSpace(state.AccessToken))
                return false;

            _currentAuthState = state;
            _client = CreateClient();

            // Validate token by fetching current user
            var user = await _client.User.Current();
            _currentAuthState = new GitHubAuthState(
                state.AccessToken,
                new GitHubUserModel(user.Login, user.Name ?? user.Login, user.AvatarUrl, user.HtmlUrl),
                state.CreatedAtUtc
            );
            await SaveAuthStateAsync(_currentAuthState);
            AuthStateChanged?.Invoke(true);
            return true;
        }
        catch
        {
            await LogoutAsync();
            return false;
        }
    }

    public async Task<OauthDeviceFlowResponse> StartDeviceFlowAsync(CancellationToken cancellationToken = default)
    {
        var request = new OauthDeviceFlowRequest(DefaultClientId)
        {
            Scopes = { "repo", "read:org", "read:user" }
        };

        return await Client.Oauth.InitiateDeviceFlow(request, cancellationToken);
    }

    public async Task<GitHubAuthState> WaitForTokenAsync(OauthDeviceFlowResponse flow, CancellationToken cancellationToken = default)
    {
        // Octokit's CreateAccessTokenForDeviceFlow polls until user completes authorization or expires
        var tokenResponse = await Client.Oauth.CreateAccessTokenForDeviceFlow(DefaultClientId, flow, cancellationToken);

        if (string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
        {
            throw new InvalidOperationException("Failed to acquire access token from GitHub.");
        }

        var authClient = new GitHubClient(new ProductHeaderValue(ProductHeader))
        {
            Credentials = new Credentials(tokenResponse.AccessToken)
        };

        var user = await authClient.User.Current();
        var userModel = new GitHubUserModel(user.Login, user.Name ?? user.Login, user.AvatarUrl, user.HtmlUrl);

        var state = new GitHubAuthState(tokenResponse.AccessToken, userModel, DateTime.UtcNow);
        _currentAuthState = state;
        _client = authClient;

        await SaveAuthStateAsync(state);
        AuthStateChanged?.Invoke(true);
        return state;
    }

    private static async Task SaveAuthStateAsync(GitHubAuthState state)
    {
        try
        {
            Directory.CreateDirectory(AuthStorageDir);
            string json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(AuthStorageFile, json);
        }
        catch
        {
            // Ignore persistence errors if storage is inaccessible
        }
    }

    public async Task LogoutAsync()
    {
        _currentAuthState = null;
        _client = new GitHubClient(new ProductHeaderValue(ProductHeader));
        try
        {
            if (File.Exists(AuthStorageFile))
            {
                File.Delete(AuthStorageFile);
            }
        }
        catch
        {
            // Ignore deletion failures
        }

        AuthStateChanged?.Invoke(false);
        await Task.CompletedTask;
    }
}
