using Avalonia.Media;
using CopperCalzone.Models;

namespace CopperCalzone.ViewModels;

public sealed class NetItemViewModel
{
    private static readonly IBrush PowerBrush = new SolidColorBrush(Color.Parse("#EFB8C8"));
    private static readonly IBrush SignalBrush = new SolidColorBrush(Color.Parse("#D0BCFF"));

    public PcbNet Net { get; }

    public int Id => Net.Id;
    public string Name => Net.Name;
    public string DisplayName => string.IsNullOrEmpty(Net.Name) ? "(unnamed)" : Net.Name;
    public int PadCount => Net.PadCount;
    public int ViaCount => Net.ViaCount;
    public bool IsPower => Net.IsPower;
    public bool HasVias => Net.ViaCount > 0;
    public string PadCountBadge => $"{Net.PadCount} pins";
    public string ViaCountBadge => $"{Net.ViaCount} vias";
    public IBrush DotBrush => Net.IsPower ? PowerBrush : SignalBrush;

    public NetItemViewModel(PcbNet net)
    {
        Net = net;
    }
}
