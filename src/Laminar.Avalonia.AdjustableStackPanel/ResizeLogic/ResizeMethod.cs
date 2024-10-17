namespace Laminar.Avalonia.AdjustableStackPanel.ResizeLogic;

public interface IResizeMethod
{
    public double Run<T>(ResizableElementSlice<T> elements, IResizingHarness<T> harness, double resizeAmount, double totalResizeSpace);
}

public struct NoneResizeMethod : IResizeMethod
{
    public readonly double Run<T>(ResizableElementSlice<T> elements, IResizingHarness<T> harness, double resizeAmount, double totalResizeSpace)
        => 0;
}

public struct Scale : IResizeMethod
{
    public readonly double Run<T>(ResizableElementSlice<T> elements, IResizingHarness<T> harness, double resizeAmount, double totalResizeSpace)
    {
        // Growing is simple, do that first
        if (resizeAmount > 0)
        {
            int resizedControlCount = elements.Length;
            foreach (T resizable in elements)
            {
                harness.ChangeSize(resizable, resizeAmount / resizedControlCount);
            }

            return resizeAmount;
        }

        // Don't shrink by more than the space we have available
        double sizeReductionAmount = Math.Min(-resizeAmount, totalResizeSpace);

        if (sizeReductionAmount == 0) { return 0; }

        foreach (T resizable in elements)
        {
            double controlResizableSpace = harness.GetTargetSize(resizable) - harness.GetMinimumSize(resizable);
            harness.ChangeSize(resizable, -sizeReductionAmount * controlResizableSpace / totalResizeSpace);
        }

        return -sizeReductionAmount;
    }
}

public struct Cascade : IResizeMethod
{
    public readonly double Run<T>(ResizableElementSlice<T> elements, IResizingHarness<T> harness, double resizeAmount, double totalResizeSpace)
    {
        if (resizeAmount == 0 || elements.Length <= 0) { return 0; }

        if (resizeAmount > 0)
        {
            // If the size has increased, there's no need to enumerate the full list, just grow the first child
            ResizableElementSlice<T>.Enumerator elementsEnumerator = elements.GetEnumerator();
            elementsEnumerator.MoveNext();
            harness.ChangeSize(elementsEnumerator.Current, resizeAmount);
            return resizeAmount;
        }

        double remainingReductionAmount = -resizeAmount;
        foreach (T resizable in elements)
        {
            double sizeDecrease = -harness.TryResize(resizable, -remainingReductionAmount);
            remainingReductionAmount -= sizeDecrease;

            if (remainingReductionAmount <= 0)
            {
                break;
            }
        }

        return resizeAmount + remainingReductionAmount;
    }
}