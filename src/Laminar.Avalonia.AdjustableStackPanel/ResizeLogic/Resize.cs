namespace Laminar.Avalonia.AdjustableStackPanel.ResizeLogic;

public readonly record struct Resize(int IndexOffset, ResizeAmountTransformation ResizeAmountTransformation, ResizerMode ResizerMode)
{
    public readonly double Execute<T>(IList<T> resizeElements, IResizingHarness<T> resizeHarness, double resizeAmount, double spaceToExpandInto, int indexOfCurrentResizer, int activeResizerIndex, Span<double> spaceBeforeResizers, ResizeFlags flags)
    {
        double spaceBeforeResizer = indexOfCurrentResizer < 0 ? 0 : spaceBeforeResizers[indexOfCurrentResizer];
        double spaceAfterResizer = spaceBeforeResizers[^1] - spaceBeforeResizer;

        double requestedResizerPositionChange = ResizeAmountTransformation(resizeAmount, activeResizerIndex < 0 ? 0 : spaceBeforeResizers[activeResizerIndex], spaceBeforeResizer, spaceBeforeResizers[^1]);

        if (ResizerMode.GetResizeMethods() is not (ResizeMethod methodBeforeResizer, ResizeMethod methodAfterResizer))
        {
            return 0;
        }

        ListSlice<T> elementsBeforeResizer = resizeElements.CreateBackwardsSlice(indexOfCurrentResizer);
        ListSlice<T> elementsAfterResizer = resizeElements.CreateForwardsSlice(indexOfCurrentResizer + 1);

        if (requestedResizerPositionChange > 0)
        {
            double changeInStackSize = 0;
            double remainingResizerPositionChange = requestedResizerPositionChange;
            if (indexOfCurrentResizer == resizeElements.Count - 1 || flags.HasFlag(ResizeFlags.CanConsumeSpaceAfterStack))
            {
                double spaceToTakeAfterStack = Math.Min(spaceToExpandInto, requestedResizerPositionChange);
                remainingResizerPositionChange -= spaceToTakeAfterStack;
                spaceToExpandInto -= spaceToTakeAfterStack;
            }
            if (remainingResizerPositionChange > 0 && indexOfCurrentResizer < resizeElements.Count - 1)
            {
                double elementsAfterResizerSizeReduction = -methodAfterResizer.RunMethod(elementsAfterResizer, resizeHarness, -remainingResizerPositionChange, spaceAfterResizer, spaceToExpandInto);
                changeInStackSize -= elementsAfterResizerSizeReduction;
                remainingResizerPositionChange -= elementsAfterResizerSizeReduction;
            }
            if (remainingResizerPositionChange > 0 && spaceToExpandInto > 0)
            {
                double spaceToTakeAfterResizer = Math.Min(spaceToExpandInto, requestedResizerPositionChange);
                remainingResizerPositionChange -= spaceToTakeAfterResizer;
                spaceToExpandInto -= spaceToTakeAfterResizer;
            }

            double successfulResizerPositionChange = requestedResizerPositionChange - remainingResizerPositionChange;

            if (indexOfCurrentResizer > -1 && !flags.HasFlag(ResizeFlags.CanConsumeSpaceBeforeStack))
            {
                changeInStackSize += methodBeforeResizer.RunMethod(elementsBeforeResizer, resizeHarness, successfulResizerPositionChange, spaceBeforeResizer, spaceToExpandInto);
            }

            return changeInStackSize;
        }
        else if (requestedResizerPositionChange < 0)
        {
            double changeInStackSize = 0;
            double remainingResizerPositionReduction = -requestedResizerPositionChange;
            if (indexOfCurrentResizer == -1 || flags.HasFlag(ResizeFlags.CanConsumeSpaceBeforeStack))
            {
                double spaceToTakeBeforeStack = Math.Min(spaceToExpandInto, remainingResizerPositionReduction);
                remainingResizerPositionReduction -= spaceToTakeBeforeStack;
                spaceToExpandInto -= spaceToTakeBeforeStack;
            }
            if (remainingResizerPositionReduction > 0 && indexOfCurrentResizer > -1)
            {
                double elementsBeforeResizerSizeReduction = -methodBeforeResizer.RunMethod(elementsBeforeResizer, resizeHarness, -remainingResizerPositionReduction, spaceBeforeResizer, spaceToExpandInto);
                changeInStackSize -= elementsBeforeResizerSizeReduction;
                remainingResizerPositionReduction -= elementsBeforeResizerSizeReduction;
            }
            if (remainingResizerPositionReduction > 0 && spaceToExpandInto > 0)
            {
                double spaceToTakeBeforeStack = Math.Min(spaceToExpandInto, remainingResizerPositionReduction);
                remainingResizerPositionReduction -= spaceToTakeBeforeStack;
                spaceToExpandInto -= spaceToTakeBeforeStack;
            }

            double successfulResizerPositionChange = requestedResizerPositionChange + remainingResizerPositionReduction;

            if (indexOfCurrentResizer < resizeElements.Count - 1 && !flags.HasFlag(ResizeFlags.CanConsumeSpaceAfterStack))
            {
                changeInStackSize += methodAfterResizer.RunMethod(elementsAfterResizer, resizeHarness, -successfulResizerPositionChange, spaceAfterResizer, spaceToExpandInto);
            }

            return changeInStackSize;
        }

        return 0;
    }
}