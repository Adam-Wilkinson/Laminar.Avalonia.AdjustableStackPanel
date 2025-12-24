using System.Diagnostics;

namespace Laminar.Avalonia.AdjustableStackPanel.ResizeLogic;

public readonly record struct ResizerMovement(int IndexOffset, ResizeAmountTransformation ResizeAmountTransformation, ResizerMode ResizerMode)
{
    public bool IsValid<T>(IList<T> resizeElements, int indexOfCurrentResizer, ResizeInfo<T> resizeInfo)
    {
        if (!resizeInfo.Flags.HasFlag(ResizeFlags.CanMoveStackStart) && resizeInfo.GetElementsBefore(indexOfCurrentResizer, resizeElements).Length <= 0) { return false; }

        if (!resizeInfo.Flags.HasFlag(ResizeFlags.CanMoveStackEnd) && resizeInfo.GetElementsAfter(indexOfCurrentResizer, resizeElements).Length <= 0) { return false; }

        double desiredResizerPositionChange = TransformResize(resizeInfo, indexOfCurrentResizer);

        if (desiredResizerPositionChange > 0)
        {
            return resizeInfo.Flags.HasFlag(ResizeFlags.CanMoveStackEnd) || resizeInfo.SpaceAfterResizer(indexOfCurrentResizer) > desiredResizerPositionChange;
        }
        else
        {
            return resizeInfo.Flags.HasFlag(ResizeFlags.CanMoveStackStart) || resizeInfo.SpaceBeforeResizer(indexOfCurrentResizer) > -desiredResizerPositionChange;
        }
    }

    public double Execute<T>(IList<T> resizeElements, int indexOfCurrentResizer, ResizeInfo<T> resizeInfo)
    {
        double requestedResizerPositionChange = TransformResize(resizeInfo, indexOfCurrentResizer);
        double actualResizerPositionChange = 0;
        double changeInStackSize = 0;

        ResizableElementSlice<T> elementsBeforeResizer = resizeInfo.GetElementsBefore(indexOfCurrentResizer, resizeElements);
        ResizableElementSlice<T> elementsAfterResizer = resizeInfo.GetElementsAfter(indexOfCurrentResizer, resizeElements);

        if (requestedResizerPositionChange > 0)
        {
            if (resizeInfo.Flags.HasFlag(ResizeFlags.PreferResizeAfter) || !resizeInfo.Flags.HasFlag(ResizeFlags.CanMoveStackEnd))
            {
                actualResizerPositionChange = -ResizerMode.MethodAfter.Run(elementsAfterResizer, resizeInfo.Harness, -requestedResizerPositionChange, resizeInfo.SpaceAfterResizer(indexOfCurrentResizer));
            }

            if (resizeInfo.Flags.HasFlag(ResizeFlags.CanMoveStackEnd) && actualResizerPositionChange < requestedResizerPositionChange)
            {
                changeInStackSize = requestedResizerPositionChange - actualResizerPositionChange;
                actualResizerPositionChange = requestedResizerPositionChange;
            }

            if (resizeInfo.Flags.HasFlag(ResizeFlags.PreferResizeBefore) || !resizeInfo.Flags.HasFlag(ResizeFlags.CanMoveStackStart))
            {
                ResizerMode.MethodBefore.Run(elementsBeforeResizer, resizeInfo.Harness, actualResizerPositionChange, resizeInfo.SpaceBeforeResizer(indexOfCurrentResizer));
            }
        }
        else if (requestedResizerPositionChange < 0)
        {
            if (resizeInfo.Flags.HasFlag(ResizeFlags.PreferResizeBefore) || !resizeInfo.Flags.HasFlag(ResizeFlags.CanMoveStackStart))
            {
                actualResizerPositionChange = ResizerMode.MethodBefore.Run(elementsBeforeResizer, resizeInfo.Harness, requestedResizerPositionChange, resizeInfo.SpaceBeforeResizer(indexOfCurrentResizer));
            }
             
            if (resizeInfo.Flags.HasFlag(ResizeFlags.CanMoveStackStart) && actualResizerPositionChange > requestedResizerPositionChange)
            {
                changeInStackSize = actualResizerPositionChange - requestedResizerPositionChange;
                actualResizerPositionChange = requestedResizerPositionChange;
            }

            if (resizeInfo.Flags.HasFlag(ResizeFlags.PreferResizeAfter) || !resizeInfo.Flags.HasFlag(ResizeFlags.CanMoveStackEnd))
            {
                ResizerMode.MethodAfter.Run(elementsAfterResizer, resizeInfo.Harness, -actualResizerPositionChange, resizeInfo.SpaceAfterResizer(indexOfCurrentResizer));
            }
        }

        return changeInStackSize;
    }

    private double TransformResize<T>(ResizeInfo<T> resizeInfo, int indexOfCurrentResizer)
        => ResizeAmountTransformation(resizeInfo.ActiveResizerRequestedChange, resizeInfo.SpaceBeforeResizer(resizeInfo.ActiveResizerIndex), resizeInfo.SpaceBeforeResizer(indexOfCurrentResizer), resizeInfo.TotalResizeSpace());
}