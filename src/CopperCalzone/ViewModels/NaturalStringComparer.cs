namespace CopperCalzone.ViewModels;

public sealed class NaturalStringComparer : IComparer<string>
{
    public static readonly NaturalStringComparer Instance = new();

    public int Compare(string? x, string? y)
    {
        if (x == y) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        // Extract alpha prefix and numeric suffix
        int ix = 0;
        while (ix < x.Length && !char.IsDigit(x[ix])) ix++;
        int iy = 0;
        while (iy < y.Length && !char.IsDigit(y[iy])) iy++;

        string prefixX = x[..ix];
        string prefixY = y[..iy];

        int prefixComp = string.Compare(prefixX, prefixY, StringComparison.OrdinalIgnoreCase);
        if (prefixComp != 0) return prefixComp;

        if (int.TryParse(x[ix..], out int numX) && int.TryParse(y[iy..], out int numY))
        {
            return numX.CompareTo(numY);
        }

        return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
    }
}