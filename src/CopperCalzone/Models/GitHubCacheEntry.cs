namespace CopperCalzone.Models;

public sealed class GitHubCacheEntry
{
    public required long RepositoryId { get; init; }
    public required string Path { get; init; }
    public required string Sha { get; init; }
    public required string CacheFileName { get; init; }
    public long SizeBytes { get; init; }
    public DateTime LastAccessedUtc { get; set; }
}

