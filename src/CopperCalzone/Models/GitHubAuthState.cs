namespace CopperCalzone.Models;

public record GitHubAuthState(
    string AccessToken,
    GitHubUserModel? User,
    DateTime CreatedAtUtc
);