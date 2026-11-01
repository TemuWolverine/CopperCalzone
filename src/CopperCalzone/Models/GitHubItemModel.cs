namespace CopperCalzone.Models;

public class GitHubItemModel
{
    public string FormattedSize => IsDirectory ? "Folder" : Size.HasValue ? $"{Size.Value / 1024.0:0.#} KB" : "PCB File";
    public bool IsDirectory { get; init; }
    public required string Name { get; init; }
    public required string Path { get; init; }
    public string? Sha { get; init; }
    public long? Size { get; init; }
}