using Avalonia.Controls;
using Avalonia.Layout;
using Laminar.Avalonia.AdjustableStackPanel.ResizeLogic;

namespace Laminar.Avalonia.AdjustableStackPanel;

internal class ControlResizingHarness : IResizingHarness<Control>
{
    private static readonly ControlResizingHarness HorizontalHarness = new() { Orientation = Orientation.Horizontal, Animated = false };
    private static readonly ControlResizingHarness HorizontalHarnessAnimated = new() { Orientation = Orientation.Horizontal, Animated = true };
    private static readonly ControlResizingHarness VerticalHarness = new() { Orientation = Orientation.Vertical, Animated = false };
    private static readonly ControlResizingHarness VerticalHarnessAnimated = new() { Orientation = Orientation.Vertical, Animated = true };

    public static ControlResizingHarness GetHarness(Orientation orientation, bool animated = false) => (orientation, animated) switch
    {
        (Orientation.Vertical, false) => VerticalHarness,
        (Orientation.Vertical, true) => VerticalHarnessAnimated,
        (Orientation.Horizontal, false) => HorizontalHarness,
        (Orientation.Horizontal, true) => HorizontalHarnessAnimated,
        _ => throw new ArgumentException($"Invalid Orientation {orientation}", nameof(orientation)),
    };

    public required Orientation Orientation { get; init; }
    public required bool Animated { get; init; }

    public double GetMinimumSize(Control resizable) 
        => Orientation == Orientation.Horizontal ? resizable.DesiredSize.Width : resizable.DesiredSize.Height;

    public double GetSize(Control resizable)
        => ResizeWidget.GetOrCreateResizer(resizable).TargetSize;

    public void SetSize(Control resizable, double size)
        => ResizeWidget.GetOrCreateResizer(resizable).SetSizeTo(size, Animated);
}
