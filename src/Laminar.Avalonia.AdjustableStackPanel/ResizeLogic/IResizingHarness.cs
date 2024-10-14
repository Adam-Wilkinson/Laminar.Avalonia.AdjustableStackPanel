namespace Laminar.Avalonia.AdjustableStackPanel.ResizeLogic;

public interface IResizingHarness<T>
{
    public bool IsEnabled(T resizable);

    public double GetMinimumSize(T resizable);

    public double GetTargetSize(T resizable);

    public void SetSize(T resizable, double size);
}

public static class ResizingHarnessExtensions
{
    public static double GetResizableSpace<T>(this IResizingHarness<T> resizingHarness, T resizable)
    {
        return resizingHarness.GetTargetSize(resizable) - resizingHarness.GetMinimumSize(resizable);
    }

    public static void ChangeSize<T>(this IResizingHarness<T> resizingHarness, T resizable, double changeInSize)
    {
        resizingHarness.SetSize(resizable, resizingHarness.GetTargetSize(resizable) + changeInSize);
    }

    public static double TryResize<T>(this IResizingHarness<T> resizingHarness, T resizable, double changeInSize)
    {
        double originalSize = resizingHarness.GetTargetSize(resizable);
        double minimumSize = resizingHarness.GetMinimumSize(resizable);
        double newSize = Math.Max(minimumSize, originalSize + changeInSize);
        resizingHarness.SetSize(resizable, newSize);
        return newSize - originalSize;
    }
}