using CopperCalzone.Models;
using System.Text.Json;

namespace CopperCalzone.Parsing;

public static class BoardLoader
{
    public static PcbBoard LoadFromContent(string content, string fileName = "Board")
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidDataException("File content is empty.");

        string fallbackTitle = Path.GetFileNameWithoutExtension(fileName);
        if (fallbackTitle.EndsWith("_net_viewer", StringComparison.OrdinalIgnoreCase))
            fallbackTitle = fallbackTitle[..^"_net_viewer".Length];

        ReadOnlySpan<char> trimmed = content.AsSpan().TrimStart();

        // 1. Check if it's a native KiCad S-expression PCB file (.kicad_pcb)
        if (trimmed.StartsWith("(kicad_pcb", StringComparison.Ordinal))
        {
            var parser = new KiCadPcbParser(content);
            return parser.ParseAll(fallbackTitle);
        }

        // 2. Check if it's a bundled HTML Net Viewer file containing window.PCB_DATA
        int markerIdx = content.IndexOf("window.PCB_DATA =", StringComparison.Ordinal);
        if (markerIdx >= 0)
        {
            int jsonStart = content.IndexOf('{', markerIdx);
            if (jsonStart >= 0)
            {
                string candidate = ExtractBalancedJsonObject(content.AsSpan(), jsonStart);
                var htmlBoard = JsonSerializer.Deserialize<PcbBoard>(candidate);
                if (htmlBoard is not null)
                {
                    if (string.IsNullOrWhiteSpace(htmlBoard.Metadata.Title))
                        htmlBoard.Metadata.Title = fallbackTitle;
                    return htmlBoard;
                }
            }
        }

        // 3. Otherwise treat as JSON board payload
        if (trimmed.StartsWith("{", StringComparison.Ordinal))
        {
            var jsonBoard = JsonSerializer.Deserialize<PcbBoard>(content);
            if (jsonBoard is not null)
            {
                if (string.IsNullOrWhiteSpace(jsonBoard.Metadata.Title))
                    jsonBoard.Metadata.Title = fallbackTitle;
                return jsonBoard;
            }
        }

        throw new InvalidDataException($"Unrecognized board format in '{fileName}'. Expected .kicad_pcb, .json, or CopperCalzone .html.");
    }

    public static async Task<PcbBoard> LoadFromStreamAsync(Stream stream, string fileName = "Board")
    {
        using var reader = new StreamReader(stream);
        string content = await reader.ReadToEndAsync();
        return LoadFromContent(content, fileName);
    }

    private static string ExtractBalancedJsonObject(ReadOnlySpan<char> span, int startIdx)
    {
        int depth = 0;
        bool inQuotes = false;
        for (int i = startIdx; i < span.Length; i++)
        {
            char c = span[i];
            if (inQuotes)
            {
                if (c == '\\' && i + 1 < span.Length)
                {
                    i++; // skip escaped character
                }
                else if (c == '"')
                {
                    inQuotes = false;
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == '{')
                {
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return span.Slice(startIdx, i - startIdx + 1).ToString();
                    }
                }
            }
        }
        return span[startIdx..].ToString();
    }
}
