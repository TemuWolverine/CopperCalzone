using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using CopperCalzone.ViewModels;

namespace CopperCalzone.Views;

public partial class SidebarView : UserControl
{
    public SidebarView()
    {
        InitializeComponent();
    }

    private void OnColResizerDragDelta(object? sender, VectorEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        var tag = (sender as Thumb)?.Tag as string;
        switch (tag)
        {
            case "placed":
                vm.BomColPlacedWidth = Math.Clamp(vm.BomColPlacedWidth + e.Vector.X, 36, 140);
                break;
            case "references":
                vm.BomColRefWidth = Math.Clamp(vm.BomColRefWidth + e.Vector.X, 45, 350);
                break;
            case "value":
                vm.BomColValWidth = Math.Clamp(vm.BomColValWidth + e.Vector.X, 45, 350);
                break;
            case "footprint":
                vm.BomColFpWidth = Math.Clamp(vm.BomColFpWidth + e.Vector.X, 50, 500);
                break;
            case "quantity":
                vm.BomColQtyWidth = Math.Clamp(vm.BomColQtyWidth + e.Vector.X, 35, 140);
                break;
        }
    }

    private void OnColResizerDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        var tag = (sender as Thumb)?.Tag as string;
        switch (tag)
        {
            case "placed":
                vm.BomColPlacedWidth = 50;
                break;
            case "references":
                vm.BomColRefWidth = 110;
                break;
            case "value":
                vm.BomColValWidth = 100;
                break;
            case "footprint":
                vm.BomColFpWidth = 140;
                break;
            case "quantity":
                vm.BomColQtyWidth = 50;
                break;
        }
    }
}
