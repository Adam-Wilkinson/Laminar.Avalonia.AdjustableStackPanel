namespace Laminar.Avalonia.AdjustableStackPanel.ResizeLogic;

public readonly record struct ResizerMovement(int IndexOffset, ResizeAmountTransformation ResizeAmountTransformation, ResizerMode ResizerMode)
{
    public readonly bool HasSpaceForResize<T>(IList<T> resizeElements, int indexOfCurrentResizer, ResizeInfo<T> resizeInfo)
    {
        double desiredResizerPositionChange = TransformResize(resizeInfo, indexOfCurrentResizer);

        if (desiredResizerPositionChange > 0)
        {
            return !ShouldResizeAfterIndex(indexOfCurrentResizer, resizeElements, resizeInfo.ResizeFlags) || resizeInfo.SpaceAfterResizer(indexOfCurrentResizer) > desiredResizerPositionChange;
        }
        else
        {
            return !ShouldResizeBeforeIndex(indexOfCurrentResizer, resizeElements, resizeInfo.ResizeFlags) || resizeInfo.SpaceBeforeResizer(indexOfCurrentResizer) > -desiredResizerPositionChange;
        }
    }

    public readonly double Execute<T>(IList<T> resizeElements, int indexOfCurrentResizer, ResizeInfo<T> resizeInfo)
    {
        double resizerPositionChange = TransformResize(resizeInfo, indexOfCurrentResizer);

        if (ResizerMode.GetResizeMethods() is not (ResizeMethod methodBeforeResizer, ResizeMethod methodAfterResizer))
        {
            return 0;
        }

        ListSlice<T> elementsBeforeResizer = resizeInfo.GetElementsBefore(indexOfCurrentResizer, resizeElements);
        ListSlice<T> elementsAfterResizer = resizeInfo.GetElementsAfter(indexOfCurrentResizer, resizeElements);

        if (resizerPositionChange > 0)
        {
            if (ShouldResizeAfterIndex(indexOfCurrentResizer, resizeElements, resizeInfo.ResizeFlags))
            {
                resizerPositionChange = -methodAfterResizer.RunMethod(elementsAfterResizer, resizeInfo.Harness, -resizerPositionChange, resizeInfo.SpaceAfterResizer(indexOfCurrentResizer));
            }

            if (ShouldResizeBeforeIndex(indexOfCurrentResizer, resizeElements, resizeInfo.ResizeFlags))
            {
                methodBeforeResizer.RunMethod(elementsBeforeResizer, resizeInfo.Harness, resizerPositionChange, resizeInfo.SpaceBeforeResizer(indexOfCurrentResizer));
            }
        }
        else if (resizerPositionChange < 0)
        {
            if (ShouldResizeBeforeIndex(indexOfCurrentResizer, resizeElements, resizeInfo.ResizeFlags))
            {
                resizerPositionChange = methodBeforeResizer.RunMethod(elementsBeforeResizer, resizeInfo.Harness, resizerPositionChange, resizeInfo.SpaceBeforeResizer(indexOfCurrentResizer));
            }

            if (ShouldResizeAfterIndex(indexOfCurrentResizer, resizeElements, resizeInfo.ResizeFlags))
            {
                methodAfterResizer.RunMethod(elementsAfterResizer, resizeInfo.Harness, -resizerPositionChange, resizeInfo.SpaceAfterResizer(indexOfCurrentResizer));
            }
        }

        if (resizeInfo.ResizeFlags.HasFlag(ResizeFlags.DisableResizeAfter))
        {
            return resizerPositionChange;
        }
        else if (resizeInfo.ResizeFlags.HasFlag(ResizeFlags.DisableResizeBefore))
        { 
            return -resizerPositionChange;
        }
        else
        {
            return 0.0;
        }
    }

    private static bool ShouldResizeAfterIndex<T>(int indexOfCurrentResizer, IList<T> resizeElements, ResizeFlags flags)
        => indexOfCurrentResizer < resizeElements.Count - 1 && !flags.HasFlag(ResizeFlags.DisableResizeAfter);

    private static bool ShouldResizeBeforeIndex<T>(int indexOfCurrentResizer, IList<T> resizeElements, ResizeFlags flags)
        => indexOfCurrentResizer > -1 && !flags.HasFlag(ResizeFlags.DisableResizeBefore);

    private double TransformResize<T>(ResizeInfo<T> resizeInfo, int indexOfCurrentResizer)
        => ResizeAmountTransformation(resizeInfo.ActiveResizerRequestedChange, resizeInfo.SpaceBeforeResizer(resizeInfo.ActiveResizerIndex), resizeInfo.SpaceBeforeResizer(indexOfCurrentResizer), resizeInfo.TotalResizeSpace());
}