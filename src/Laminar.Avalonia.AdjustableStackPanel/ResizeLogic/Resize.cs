using System.Diagnostics;

namespace Laminar.Avalonia.AdjustableStackPanel.ResizeLogic;

public readonly record struct Resize(int IndexOffset, ResizeAmountTransformation ResizeAmountTransformation, ResizerMode ResizerMode)
{
    public readonly double Execute<T>(IList<T> resizeElements, IResizingHarness<T> resizeHarness, double resizeAmount, int indexOfCurrentResizer, int activeResizerIndex, Span<double> spaceBeforeResizers, ResizeFlags flags)
    {
        double spaceBeforeResizer = indexOfCurrentResizer < 0 ? 0 : spaceBeforeResizers[indexOfCurrentResizer];
        double spaceAfterResizer = spaceBeforeResizers[^1] - spaceBeforeResizer;

        double resizerPositionChange = ResizeAmountTransformation(resizeAmount, activeResizerIndex < 0 ? 0 : spaceBeforeResizers[activeResizerIndex], spaceBeforeResizer, spaceBeforeResizers[^1]);

        if (ResizerMode.GetResizeMethods() is not (ResizeMethod methodBeforeResizer, ResizeMethod methodAfterResizer))
        {
            return 0;
        }

        ListSlice<T> elementsBeforeResizer = resizeElements.CreateBackwardsSlice(indexOfCurrentResizer);
        ListSlice<T> elementsAfterResizer = resizeElements.CreateForwardsSlice(indexOfCurrentResizer + 1);

        if (resizerPositionChange > 0)
        {
            if (indexOfCurrentResizer < resizeElements.Count - 1 && !flags.HasFlag(ResizeFlags.DisableResizeAfter))
            {
                resizerPositionChange = -methodAfterResizer.RunMethod(elementsAfterResizer, resizeHarness, -resizerPositionChange, spaceAfterResizer);
            }

            if (indexOfCurrentResizer > -1 && !flags.HasFlag(ResizeFlags.DisableResizeBefore))
            {
                methodBeforeResizer.RunMethod(elementsBeforeResizer, resizeHarness, resizerPositionChange, spaceBeforeResizer);
            }
        }
        else if (resizerPositionChange < 0)
        {
            if (indexOfCurrentResizer > -1 && !flags.HasFlag(ResizeFlags.DisableResizeBefore))
            {
                resizerPositionChange = methodBeforeResizer.RunMethod(elementsBeforeResizer, resizeHarness, resizerPositionChange, spaceBeforeResizer);
            }

            if (indexOfCurrentResizer < resizeElements.Count - 1 && !flags.HasFlag(ResizeFlags.DisableResizeAfter))
            {
                methodAfterResizer.RunMethod(elementsAfterResizer, resizeHarness, -resizerPositionChange, spaceAfterResizer);
            }
        }

        if (flags.HasFlag(ResizeFlags.DisableResizeAfter))
        {
            return resizerPositionChange;
        }
        else if (flags.HasFlag(ResizeFlags.DisableResizeBefore))
        { 
            return -resizerPositionChange;
        }
        else
        {
            return 0.0;
        }
    }
}