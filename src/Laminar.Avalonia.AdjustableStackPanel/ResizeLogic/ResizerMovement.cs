namespace Laminar.Avalonia.AdjustableStackPanel.ResizeLogic;

public readonly record struct ResizerMovement(int IndexOffset, ResizeAmountTransformation ResizeAmountTransformation, ResizerMode ResizerMode)
{
    public readonly bool IsValid<T>(IList<T> resizeElements, int indexOfCurrentResizer, ResizeInfo<T> resizeInfo)
    {
        if (resizeInfo.GetElementsBefore(indexOfCurrentResizer, resizeElements).ElementCount <= 0 && !resizeInfo.Flags.HasFlag(ResizeFlags.IgnoreResizeBefore)) { return false; }

        if (resizeInfo.GetElementsAfter(indexOfCurrentResizer, resizeElements).ElementCount <= 0 && !resizeInfo.Flags.HasFlag(ResizeFlags.IgnoreResizeAfter)) { return false; }

        double desiredResizerPositionChange = TransformResize(resizeInfo, indexOfCurrentResizer);

        if (desiredResizerPositionChange > 0)
        {
            return resizeInfo.SpaceAfterResizer(indexOfCurrentResizer) > desiredResizerPositionChange || resizeInfo.Flags.HasFlag(ResizeFlags.IgnoreResizeAfter);
        }
        else
        {
            return resizeInfo.SpaceBeforeResizer(indexOfCurrentResizer) > -desiredResizerPositionChange || resizeInfo.Flags.HasFlag(ResizeFlags.IgnoreResizeBefore);
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
            if (!resizeInfo.Flags.HasFlag(ResizeFlags.IgnoreResizeAfter))
            {
                resizerPositionChange = -methodAfterResizer.RunMethod(elementsAfterResizer, resizeInfo.Harness, -resizerPositionChange, resizeInfo.SpaceAfterResizer(indexOfCurrentResizer));
            }

            if (!resizeInfo.Flags.HasFlag(ResizeFlags.IgnoreResizeBefore))
            {
                methodBeforeResizer.RunMethod(elementsBeforeResizer, resizeInfo.Harness, resizerPositionChange, resizeInfo.SpaceBeforeResizer(indexOfCurrentResizer));
            }
        }
        else if (resizerPositionChange < 0)
        {
            if (!resizeInfo.Flags.HasFlag(ResizeFlags.IgnoreResizeBefore))
            {
                resizerPositionChange = methodBeforeResizer.RunMethod(elementsBeforeResizer, resizeInfo.Harness, resizerPositionChange, resizeInfo.SpaceBeforeResizer(indexOfCurrentResizer));
            }
             
            if (!resizeInfo.Flags.HasFlag(ResizeFlags.IgnoreResizeAfter))
            {
                methodAfterResizer.RunMethod(elementsAfterResizer, resizeInfo.Harness, -resizerPositionChange, resizeInfo.SpaceAfterResizer(indexOfCurrentResizer));
            }
        }

        if (resizeInfo.Flags.HasFlag(ResizeFlags.IgnoreResizeAfter))
        {
            return resizerPositionChange;
        }
        else if (resizeInfo.Flags.HasFlag(ResizeFlags.IgnoreResizeBefore))
        { 
            return -resizerPositionChange;
        }
        else
        {
            return 0.0;
        }
    }

    private double TransformResize<T>(ResizeInfo<T> resizeInfo, int indexOfCurrentResizer)
        => ResizeAmountTransformation(resizeInfo.ActiveResizerRequestedChange, resizeInfo.SpaceBeforeResizer(resizeInfo.ActiveResizerIndex), resizeInfo.SpaceBeforeResizer(indexOfCurrentResizer), resizeInfo.TotalResizeSpace());
}