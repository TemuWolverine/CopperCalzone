using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CopperCalzone.Models;

namespace CopperCalzone.Services;

public sealed class GitHubFileCacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _cacheRootDir;
    private readonly string _filesDir;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _indexFilePath;
    private readonly long _maxCacheBytes;
    private Dictionary<string, GitHubCacheEntry>? _index;

    public GitHubFileCacheService(string? customCacheDir = null, long maxCacheBytes = 250L * 1024 * 1024)
    {
        _maxCacheBytes = maxCacheBytes;

        if (!string.IsNullOrWhiteSpace(customCacheDir))
        {
            _cacheRootDir = customCacheDir;
        }
        else
        {
            string baseFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(baseFolder))
            {
                baseFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            }
            if (string.IsNullOrWhiteSpace(baseFolder))
            {
                baseFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }
            if (string.IsNullOrWhiteSpace(baseFolder))
            {
                baseFolder = AppContext.BaseDirectory;
            }

            _cacheRootDir = Path.Combine(baseFolder, "CopperCalzone", "github-cache");
        }

        _filesDir = Path.Combine(_cacheRootDir, "files");
        _indexFilePath = Path.Combine(_cacheRootDir, "index.json");
    }

    public async Task ClearAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            _index = new Dictionary<string, GitHubCacheEntry>(StringComparer.Ordinal);
            if (Directory.Exists(_filesDir))
            {
                Directory.Delete(_filesDir, recursive: true);
            }
            if (File.Exists(_indexFilePath))
            {
                File.Delete(_indexFilePath);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StoreAsync(long repositoryId, string path, string sha, string content)
    {
        if (string.IsNullOrWhiteSpace(sha))
        {
            return;
        }

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_filesDir);
            var index = await LoadIndexCoreAsync().ConfigureAwait(false);
            string key = MakeKey(repositoryId, path);

            if (index.TryGetValue(key, out var existing))
            {
                DeleteFileQuietly(Path.Combine(_filesDir, existing.CacheFileName));
                index.Remove(key);
            }

            string cacheFileName = MakeCacheFileName(repositoryId, path, sha);
            string targetPath = Path.Combine(_filesDir, cacheFileName);
            string tempPath = targetPath + ".tmp";

            byte[] bytes = Encoding.UTF8.GetBytes(content);
            await File.WriteAllBytesAsync(tempPath, bytes).ConfigureAwait(false);
            File.Move(tempPath, targetPath, overwrite: true);

            index[key] = new GitHubCacheEntry
            {
                RepositoryId = repositoryId,
                Path = path,
                Sha = sha,
                CacheFileName = cacheFileName,
                SizeBytes = bytes.LongLength,
                LastAccessedUtc = DateTime.UtcNow
            };

            EnforceSizeLimitCore(index);
            await SaveIndexCoreAsync(index).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string?> TryGetAsync(long repositoryId, string path, string? expectedSha)
    {
        if (string.IsNullOrWhiteSpace(expectedSha))
        {
            return null;
        }

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var index = await LoadIndexCoreAsync().ConfigureAwait(false);
            string key = MakeKey(repositoryId, path);
            if (!index.TryGetValue(key, out var entry))
            {
                return null;
            }

            string filePath = Path.Combine(_filesDir, entry.CacheFileName);
            if (!string.Equals(entry.Sha, expectedSha, StringComparison.Ordinal) || !File.Exists(filePath))
            {
                DeleteFileQuietly(filePath);
                index.Remove(key);
                await SaveIndexCoreAsync(index).ConfigureAwait(false);
                return null;
            }

            string content = await File.ReadAllTextAsync(filePath, Encoding.UTF8).ConfigureAwait(false);
            entry.LastAccessedUtc = DateTime.UtcNow;
            await SaveIndexCoreAsync(index).ConfigureAwait(false);
            return content;
        }
        catch
        {
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<HashSet<string>> ValidateAndPruneBatchAsync(
                    long repositoryId,
        IReadOnlyList<(string Path, string? Sha)> items)
    {
        var validPaths = new HashSet<string>(StringComparer.Ordinal);

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var index = await LoadIndexCoreAsync().ConfigureAwait(false);
            bool indexDirty = false;

            foreach (var (path, latestSha) in items)
            {
                string key = MakeKey(repositoryId, path);
                if (!index.TryGetValue(key, out var entry))
                {
                    continue;
                }

                string filePath = Path.Combine(_filesDir, entry.CacheFileName);
                bool hashMatches = !string.IsNullOrWhiteSpace(latestSha) &&
                                   string.Equals(entry.Sha, latestSha, StringComparison.Ordinal);

                if (!hashMatches || !File.Exists(filePath))
                {
                    DeleteFileQuietly(filePath);
                    index.Remove(key);
                    indexDirty = true;
                }
                else
                {
                    validPaths.Add(path);
                }
            }

            if (indexDirty)
            {
                await SaveIndexCoreAsync(index).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }

        return validPaths;
    }

    private static void DeleteFileQuietly(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
            // Ignore deletion failures
        }
    }

    private static string MakeCacheFileName(long repositoryId, string path, string sha)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(path));
        string pathHash = Convert.ToHexString(hash)[..16].ToLowerInvariant();
        string safeSha = new string(sha.Where(char.IsLetterOrDigit).ToArray());
        return $"{repositoryId}_{pathHash}_{safeSha}.cache";
    }

    private static string MakeKey(long repositoryId, string path) => $"{repositoryId}:{path}";

    private void EnforceSizeLimitCore(Dictionary<string, GitHubCacheEntry> index)
    {
        long totalBytes = index.Values.Sum(e => e.SizeBytes);
        if (totalBytes <= _maxCacheBytes)
        {
            return;
        }

        foreach (var kvp in index.OrderBy(k => k.Value.LastAccessedUtc).ToList())
        {
            if (totalBytes <= _maxCacheBytes)
            {
                break;
            }

            DeleteFileQuietly(Path.Combine(_filesDir, kvp.Value.CacheFileName));
            index.Remove(kvp.Key);
            totalBytes -= kvp.Value.SizeBytes;
        }
    }

    private async Task<Dictionary<string, GitHubCacheEntry>> LoadIndexCoreAsync()
    {
        if (_index is not null)
        {
            return _index;
        }

        try
        {
            if (File.Exists(_indexFilePath))
            {
                string json = await File.ReadAllTextAsync(_indexFilePath, Encoding.UTF8).ConfigureAwait(false);
                var list = JsonSerializer.Deserialize<List<GitHubCacheEntry>>(json, JsonOptions);
                if (list is not null)
                {
                    _index = new Dictionary<string, GitHubCacheEntry>(StringComparer.Ordinal);
                    foreach (var entry in list)
                    {
                        _index[MakeKey(entry.RepositoryId, entry.Path)] = entry;
                    }
                    return _index;
                }
            }
        }
        catch
        {
            // Start with an empty index if corrupt
        }

        _index = new Dictionary<string, GitHubCacheEntry>(StringComparer.Ordinal);
        return _index;
    }

    private async Task SaveIndexCoreAsync(Dictionary<string, GitHubCacheEntry> index)
    {
        Directory.CreateDirectory(_cacheRootDir);
        string tempPath = _indexFilePath + ".tmp";
        string json = JsonSerializer.Serialize(index.Values.ToList(), JsonOptions);
        await File.WriteAllTextAsync(tempPath, json, Encoding.UTF8).ConfigureAwait(false);
        File.Move(tempPath, _indexFilePath, overwrite: true);
    }
}
