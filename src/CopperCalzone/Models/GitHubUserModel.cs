namespace CopperCalzone.Models;

public record GitHubUserModel(
    string Login,
    string Name,
    string AvatarUrl,
    string HtmlUrl
);