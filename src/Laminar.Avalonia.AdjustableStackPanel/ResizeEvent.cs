using Avalonia.Interactivity;
using Laminar.Avalonia.AdjustableStackPanel.ResizeLogic;

namespace Laminar.Avalonia.AdjustableStackPanel;

public class ResizeEventArgs(RoutedEvent routedEvent, double resizeAmount, ResizerMode resizerMode, ResizeWidget changedResizer) 
    : RoutedEventArgs(routedEvent)
{
    public double ResizeAmount { get; } = resizeAmount;

    public ResizerMode ResizerMode { get; } = resizerMode;

    public ResizeWidget ChangedResizer { get; } = changedResizer;
}
