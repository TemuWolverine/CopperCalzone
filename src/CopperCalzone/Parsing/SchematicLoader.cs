using CopperCalzone.Models.Schematic;
using CopperCalzone.ViewModels;

namespace CopperCalzone.Parsing;

public static class SchematicLoader
{
    public static async Task<List<SchematicSheetItem>> DiscoverAndLoadSheetsAsync(string pcbFilePath)
    {
        var result = new List<SchematicSheetItem>();
        if (string.IsNullOrWhiteSpace(pcbFilePath)) return result;

        string? dir = Path.GetDirectoryName(pcbFilePath);
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) return result;

        string exactMatchPath = pcbFilePath.EndsWith(".kicad_sch", StringComparison.OrdinalIgnoreCase)
            ? pcbFilePath
            : Path.Combine(dir, $"{Path.GetFileNameWithoutExtension(pcbFilePath)}.kicad_sch");

        List<string> allSchFiles;
        try
        {
            allSchFiles = Directory.EnumerateFiles(dir, "*.kicad_sch", SearchOption.AllDirectories)
                .Where(f => !f.Contains(".git", StringComparison.OrdinalIgnoreCase) &&
                            !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                            !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        catch
        {
            allSchFiles = Directory.GetFiles(dir, "*.kicad_sch").ToList();
        }

        if (allSchFiles.Count == 0) return result;

        // Primary / root file: exact match if exists, otherwise first file found
        string rootFilePath = File.Exists(exactMatchPath) ? exactMatchPath : allSchFiles[0];

        var loadedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Load root sheet first
        var rootSheet = await LoadSingleSheetAsync(rootFilePath, isRoot: true);
        if (rootSheet is not null)
        {
            result.Add(rootSheet);
            loadedPaths.Add(rootFilePath);

            // Discover sub-sheets declared via (sheet ...)
            await DiscoverChildSheetsRecursivelyAsync(rootSheet, dir, result, loadedPaths);
        }

        // Also add any other .kicad_sch files in the directory that were not explicitly linked
        foreach (var f in allSchFiles)
        {
            if (loadedPaths.Add(f))
            {
                var extraSheet = await LoadSingleSheetAsync(f, isRoot: false);
                if (extraSheet is not null)
                {
                    result.Add(extraSheet);
                    await DiscoverChildSheetsRecursivelyAsync(extraSheet, dir, result, loadedPaths);
                }
            }
        }

        return result;
    }

    private static async Task DiscoverChildSheetsRecursivelyAsync(
        SchematicSheetItem parentSheet,
        string baseDir,
        List<SchematicSheetItem> result,
        HashSet<string> loadedPaths)
    {
        if (parentSheet.Schematic is null) return;

        foreach (var sRef in parentSheet.Schematic.Sheets)
        {
            if (string.IsNullOrWhiteSpace(sRef.FilePath)) continue;
            string subPath = Path.IsPathRooted(sRef.FilePath)
                ? sRef.FilePath
                : Path.Combine(baseDir, sRef.FilePath);

            if (File.Exists(subPath) && loadedPaths.Add(subPath))
            {
                var subSheet = await LoadSingleSheetAsync(subPath, isRoot: false, displayName: sRef.Name);
                if (subSheet is not null)
                {
                    result.Add(subSheet);
                    await DiscoverChildSheetsRecursivelyAsync(subSheet, baseDir, result, loadedPaths);
                }
            }
        }
    }

    public static async Task<SchematicSheetItem?> LoadSingleSheetAsync(string filePath, bool isRoot = false, string? displayName = null)
    {
        try
        {
            if (!File.Exists(filePath)) return null;

            string content = await File.ReadAllTextAsync(filePath);
            var parser = new KiCadSchParser(content);
            var sch = parser.Parse();
            sch.SourceFilePath = filePath;

            string fileName = Path.GetFileName(filePath);
            string title = !string.IsNullOrWhiteSpace(displayName)
                ? displayName
                : (!string.IsNullOrWhiteSpace(sch.Title) ? sch.Title : Path.GetFileNameWithoutExtension(filePath));

            string name = isRoot ? $"Main: {title} ({fileName})" : $"{title} ({fileName})";

            return new SchematicSheetItem
            {
                Name = name,
                FilePath = filePath,
                Schematic = sch
            };
        }
        catch
        {
            return null;
        }
    }
}

