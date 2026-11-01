using CopperCalzone.Models;
using Octokit;
using System.Text;

namespace CopperCalzone.Services;

public class GitHubPcbService
{
    private readonly GitHubAuthService _authService;

    public GitHubPcbService(GitHubAuthService authService)
    {
        _authService = authService;
    }

    public async Task<IReadOnlyList<Repository>> GetAllAccessibleRepositoriesAsync()
    {
        var client = _authService.Client;

        var request = new RepositoryRequest
        {
            Sort = RepositorySort.Pushed,
            Direction = SortDirection.Descending,
            Affiliation = RepositoryAffiliation.Owner | RepositoryAffiliation.OrganizationMember | RepositoryAffiliation.Collaborator
        };

        var options = new ApiOptions
        {
            PageSize = 100,
            PageCount = 1
        };

        var repos = await client.Repository.GetAllForCurrent(request, options);
        return repos;
    }

    public async Task<IReadOnlyList<GitHubItemModel>> GetFolderContentsAsync(long repositoryId, string path)
    {
        var client = _authService.Client;

        IReadOnlyList<RepositoryContent> contents;
        if (string.IsNullOrWhiteSpace(path))
        {
            contents = await client.Repository.Content.GetAllContents(repositoryId);
        }
        else
        {
            contents = await client.Repository.Content.GetAllContents(repositoryId, path);
        }

        var results = new List<GitHubItemModel>();

        foreach (var item in contents)
        {
            if (item.Type == ContentType.Dir)
            {
                results.Add(new GitHubItemModel
                {
                    Name = item.Name,
                    Path = item.Path,
                    IsDirectory = true,
                    Sha = item.Sha
                });
            }
            else if (item.Type == ContentType.File && item.Name.EndsWith(".kicad_pcb", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(new GitHubItemModel
                {
                    Name = item.Name,
                    Path = item.Path,
                    IsDirectory = false,
                    Size = item.Size,
                    Sha = item.Sha
                });
            }
        }

        return results
            .OrderByDescending(r => r.IsDirectory)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<string> DownloadPcbFileAsync(Repository repository, GitHubItemModel item)
    {
        var client = _authService.Client;

        // If file is reasonably sized (e.g., <= 1MB), use GetRawContent
        if (!item.Size.HasValue || item.Size.Value <= 1024 * 1024)
        {
            try
            {
                byte[] raw = await client.Repository.Content.GetRawContent(repository.Owner.Login, repository.Name, item.Path);
                return Encoding.UTF8.GetString(raw);
            }
            catch
            {
                // Fall back to Blob API if GetRawContent fails
            }
        }

        // For larger files or fallback, use the Git Blob API
        if (!string.IsNullOrEmpty(item.Sha))
        {
            var blob = await client.Git.Blob.Get(repository.Id, item.Sha);
            if (blob.Encoding == EncodingType.Base64)
            {
                byte[] bytes = Convert.FromBase64String(blob.Content);
                return Encoding.UTF8.GetString(bytes);
            }
            return blob.Content;
        }

        throw new InvalidOperationException($"Unable to download PCB file '{item.Path}'.");
    }
}
