namespace Laminar.Avalonia.AdjustableStackPanel.ResizeLogic;

public interface IResizingHarness<T>
{
    public double GetMinimumSize(T resizable);

    public double GetSize(T resizable);

    public void SetSize(T resizable, double size);
}

public static class ResizingHarnessExtensions
{
    public static void Resize<T>(this IResizingHarness<T> resizingHarness, T resizable, double changeInSize)
    {
        resizingHarness.SetSize(resizable, resizingHarness.GetSize(resizable) + changeInSize);
    }

    public static double TryResize<T>(this IResizingHarness<T> resizingHarness, T resizable, double changeInSize)
    {
        double originalSize = resizingHarness.GetSize(resizable);
        double minimumSize = resizingHarness.GetMinimumSize(resizable);
        double newSize = Math.Max(minimumSize, originalSize + changeInSize);
        resizingHarness.SetSize(resizable, newSize);

        if (newSize < 0)
        {
            throw new Exception();
        }

        return newSize - originalSize;
    }
}