namespace Laminar.Avalonia.AdjustableStackPanel.ResizeLogic;

public enum ResizeMethod
{
    None,
    Cascade,
    SqueezeExpand,
}

public static class ResizeMethodExtensions
{
    public static double RunMethods<T>(this Span<ResizeMethod> methods, ResizableElementSlice<T> resizeElements, IResizingHarness<T> resizingHarness, double resizeAmount, double totalResizeSpace)
    {
        double successfulResizeAmount = 0;
        foreach (ResizeMethod method in methods)
        {
            successfulResizeAmount += method.Run(resizeElements, resizingHarness, resizeAmount - successfulResizeAmount, totalResizeSpace);

            if (Math.Abs(successfulResizeAmount) >= Math.Abs(resizeAmount))
            {
                break;
            }
        }
        return successfulResizeAmount;
    }

    public static double Run<T>(this ResizeMethod method, ResizableElementSlice<T> resizeElements, IResizingHarness<T> resizingHarness, double resizeAmount, double totalResizeSpace) => method switch
    {
        ResizeMethod.SqueezeExpand => RunSqueezeExpand(resizeElements, resizingHarness, resizeAmount, totalResizeSpace),
        ResizeMethod.Cascade => RunCascade(resizeElements, resizingHarness, resizeAmount),
        _ => 0.0,
    };

    private static double RunSqueezeExpand<T>(ResizableElementSlice<T> resizeElements, IResizingHarness<T> resizingHarness, double resizeAmount, double totalResizeSpace)
    {
        // Growing is simple, do that first
        if (resizeAmount > 0)
        {
            int resizedControlCount = resizeElements.ElementCount;
            foreach (T resizable in resizeElements)
            {
                resizingHarness.ChangeSize(resizable, resizeAmount / resizedControlCount);
            }

            return resizeAmount;
        }

        // Don't shrink by more than the space we have available
        double sizeReductionAmount = Math.Min(-resizeAmount, totalResizeSpace);

        if (sizeReductionAmount == 0) { return 0; }

        foreach (T resizable in resizeElements)
        {
            double controlResizableSpace = resizingHarness.GetTargetSize(resizable) - resizingHarness.GetMinimumSize(resizable);
            resizingHarness.ChangeSize(resizable, -sizeReductionAmount * controlResizableSpace / totalResizeSpace);
        }

        return -sizeReductionAmount;
    }

    private static double RunCascade<T>(ResizableElementSlice<T> resizeElements, IResizingHarness<T> resizingHarness, double resizeAmount)
    {
        if (resizeAmount == 0 || resizeElements.ElementCount <= 0) { return 0; }

        if (resizeAmount > 0)
        {
            // If the size has increased, there's no need to enumerate the full list, just grow the first child
            ResizableElementSlice<T>.Enumerator elementsEnumerator = resizeElements.GetEnumerator();
            elementsEnumerator.MoveNext();
            resizingHarness.ChangeSize(elementsEnumerator.Current, resizeAmount);
            return resizeAmount;
        }

        double remainingReductionAmount = -resizeAmount;
        foreach (T resizable in resizeElements)
        {
            double sizeDecrease = -resizingHarness.TryResize(resizable, -remainingReductionAmount);
            remainingReductionAmount -= sizeDecrease;

            if (remainingReductionAmount <= 0)
            {
                break;
            }
        }

        return resizeAmount + remainingReductionAmount;
    }
}