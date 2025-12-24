namespace Laminar.Avalonia.AdjustableStackPanel.ResizeLogic;

public interface IResizingHarness<in T>
{
    public bool IsEnabled(T resizable);

    public double GetMinimumSize(T resizable);

    public double GetTargetSize(T resizable);

    public void SetSize(T resizable, double size);

    public void ChangeSize(T resizable, double changeInSize);
}

public static class ResizingHarnessExtensions
{
    extension<T>(IResizingHarness<T> resizingHarness)
    {
        public double GetResizableSpace(T resizable)
        {
            return resizingHarness.GetTargetSize(resizable) - resizingHarness.GetMinimumSize(resizable);
        }

        public double TryResize(T resizable, double changeInSize)
        {
            var originalSize = resizingHarness.GetTargetSize(resizable);
            var minimumSize = resizingHarness.GetMinimumSize(resizable);
            var newSize = Math.Max(minimumSize, originalSize + changeInSize);
            resizingHarness.SetSize(resizable, newSize);
            return newSize - originalSize;
        }
    }
}