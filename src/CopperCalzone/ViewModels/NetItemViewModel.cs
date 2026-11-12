using Avalonia.Media;
using CopperCalzone.Models;

namespace CopperCalzone.ViewModels;

public sealed class NetItemViewModel(PcbNet net)
{
    private static readonly IBrush PowerBrush = new SolidColorBrush(Color.Parse("#EFB8C8"));
    private static readonly IBrush SignalBrush = new SolidColorBrush(Color.Parse("#8FD8A5"));

    public string DisplayName => string.IsNullOrEmpty(Net.Name) ? "(unnamed)" : Net.Name;
    public IBrush DotBrush => Net.IsPower ? PowerBrush : SignalBrush;
    public bool HasVias => Net.ViaCount > 0;
    public int Id => Net.Id;
    public bool IsPower => Net.IsPower;
    public string Name => Net.Name;
    public PcbNet Net { get; } = net;
    public int PadCount => Net.PadCount;
    public string PadCountBadge => $"{Net.PadCount} pins";
    public int ViaCount => Net.ViaCount;
    public string ViaCountBadge => $"{Net.ViaCount} vias";
}
