using Avalonia.Controls;
using Avalonia.Layout;
using Laminar.Avalonia.AdjustableStackPanel.ResizeLogic;

namespace Laminar.Avalonia.AdjustableStackPanel;

internal class ControlResizingHarness : IResizingHarness<Control>
{
    private static readonly ControlResizingHarness HorizontalHarness = new() { Orientation = Orientation.Horizontal };
    private static readonly ControlResizingHarness VerticalHarness = new() { Orientation = Orientation .Vertical };

    public static ControlResizingHarness GetHarness(Orientation orientation) => orientation switch
    {
        Orientation.Vertical => VerticalHarness,
        Orientation.Horizontal => HorizontalHarness,
        _ => throw new ArgumentException($"Invalid Orientation {orientation}", nameof(orientation)),
    };

    public required Orientation Orientation { get; init; }

    public double GetMinimumSize(Control resizable) 
        => Orientation == Orientation.Horizontal ? resizable.DesiredSize.Width : resizable.DesiredSize.Height;

    public double GetSize(Control resizable)
        => ResizeWidget.GetOrCreateResizer(resizable).Size;

    public void SetSize(Control resizable, double size)
         => ResizeWidget.GetOrCreateResizer(resizable).Size = size;
}
