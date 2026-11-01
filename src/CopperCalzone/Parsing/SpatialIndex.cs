using CopperCalzone.Models;

namespace CopperCalzone.Parsing;

public sealed class SpatialIndex
{
    private const double CellSize = 5.0;
    private readonly Dictionary<(int Gx, int Gy), List<PcbPad>> _grid = [];
    // 5mm cells

    public void Build(IEnumerable<PcbPad>? pads)
    {
        _grid.Clear();
        if (pads is null) return;

        foreach (var pad in pads)
        {
            int gx = (int)Math.Floor(pad.X / CellSize);
            int gy = (int)Math.Floor(pad.Y / CellSize);
            var key = (gx, gy);
            if (!_grid.TryGetValue(key, out var list))
            {
                list = [];
                _grid[key] = list;
            }
            list.Add(pad);
        }
    }

    public PcbPad? FindPadAt(double worldX, double worldY, Func<PcbPad, bool> isPadVisible, double extraHitMarginMm = 0.15)
    {
        int gx = (int)Math.Floor(worldX / CellSize);
        int gy = (int)Math.Floor(worldY / CellSize);

        PcbPad? bestPad = null;
        double bestDistSq = double.MaxValue;

        for (int ox = -1; ox <= 1; ox++)
        {
            for (int oy = -1; oy <= 1; oy++)
            {
                if (!_grid.TryGetValue((gx + ox, gy + oy), out var pads))
                    continue;

                foreach (var pad in pads)
                {
                    if (!isPadVisible(pad))
                        continue;

                    double maxDim = Math.Max(pad.W, pad.H) / 2.0 + extraHitMarginMm;
                    double dx = Math.Abs(worldX - pad.X);
                    double dy = Math.Abs(worldY - pad.Y);
                    if (dx <= maxDim && dy <= maxDim)
                    {
                        double distSq = dx * dx + dy * dy;
                        if (distSq < bestDistSq)
                        {
                            bestDistSq = distSq;
                            bestPad = pad;
                        }
                    }
                }
            }
        }

        return bestPad;
    }
}