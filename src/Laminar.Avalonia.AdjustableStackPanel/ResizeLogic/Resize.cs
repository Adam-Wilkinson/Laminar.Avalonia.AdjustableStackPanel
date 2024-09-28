namespace Laminar.Avalonia.AdjustableStackPanel.ResizeLogic;

public readonly record struct Resize(int IndexOffset, ResizeAmountTransformation ResizeAmountTransformation, ResizerMode ResizerMode)
{
    public readonly bool HasSpaceForResize<T>(IList<T> resizeElements, IResizingHarness<T> resizeHarness, double resizeAmount, int indexOfCurrentResizer, int activeResizerIndex, Span<double> spaceBeforeResizers, ResizeFlags flags)
    {
        double spaceBeforeResizer = indexOfCurrentResizer < 0 ? 0 : spaceBeforeResizers[indexOfCurrentResizer];
        double spaceAfterResizer = spaceBeforeResizers[^1] - spaceBeforeResizer;

        double desiredResizerPositionChange = ResizeAmountTransformation(resizeAmount, activeResizerIndex < 0 ? 0 : spaceBeforeResizers[activeResizerIndex], spaceBeforeResizer, spaceBeforeResizers[^1]);

        if (desiredResizerPositionChange > 0)
        {
            return !ResizeAfterIndex(indexOfCurrentResizer, resizeElements, flags) || spaceAfterResizer > desiredResizerPositionChange;
        }
        else
        {
            return !ResizeBeforeIndex(indexOfCurrentResizer, resizeElements, flags) || spaceBeforeResizer > -desiredResizerPositionChange;
        }
    }

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
            if (ResizeAfterIndex(indexOfCurrentResizer, resizeElements, flags))
            {
                resizerPositionChange = -methodAfterResizer.RunMethod(elementsAfterResizer, resizeHarness, -resizerPositionChange, spaceAfterResizer);
            }

            if (ResizeBeforeIndex(indexOfCurrentResizer, resizeElements, flags))
            {
                methodBeforeResizer.RunMethod(elementsBeforeResizer, resizeHarness, resizerPositionChange, spaceBeforeResizer);
            }
        }
        else if (resizerPositionChange < 0)
        {
            if (ResizeBeforeIndex(indexOfCurrentResizer, resizeElements, flags))
            {
                resizerPositionChange = methodBeforeResizer.RunMethod(elementsBeforeResizer, resizeHarness, resizerPositionChange, spaceBeforeResizer);
            }

            if (ResizeAfterIndex(indexOfCurrentResizer, resizeElements, flags))
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

    private static bool ResizeAfterIndex<T>(int indexOfCurrentResizer, IList<T> resizeElements, ResizeFlags flags)
        => indexOfCurrentResizer < resizeElements.Count - 1 && !flags.HasFlag(ResizeFlags.DisableResizeAfter);

    private static bool ResizeBeforeIndex<T>(int indexOfCurrentResizer, IList<T> resizeElements, ResizeFlags flags)
        => indexOfCurrentResizer > -1 && !flags.HasFlag(ResizeFlags.DisableResizeBefore);
}