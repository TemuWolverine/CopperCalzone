using Avalonia.Controls;
using CopperCalzone.Rendering.Schematic;
using CopperCalzone.ViewModels;

namespace CopperCalzone.Views;

public partial class SchematicView : UserControl
{
    public SchematicView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is SchematicViewModel vm)
        {
            var canvas = this.FindControl<SchCanvasControl>("Canvas");
            if (canvas is not null)
            {
                vm.AttachCanvas(canvas);
            }
        }
    }
}

