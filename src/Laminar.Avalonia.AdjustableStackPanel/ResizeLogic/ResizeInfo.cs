namespace Laminar.Avalonia.AdjustableStackPanel.ResizeLogic;

public ref struct ResizeInfo<T>
{
    public readonly Span<double> SpaceBeforeResizers;

    public readonly int TotalResizeElements;

    public double ActiveResizerRequestedChange;

    public int ActiveResizerIndex;

    public ResizeFlags ResizeFlags;

    private int _currentBuildElement = 0;

    public ResizeInfo(Span<double> spaceBeforeResizers)
    {
        SpaceBeforeResizers = spaceBeforeResizers;
        TotalResizeElements = spaceBeforeResizers.Length;
    }

    public readonly double SpaceBeforeResizer(int resizerIndex) => resizerIndex < 0 ? 0 : SpaceBeforeResizers[resizerIndex];

    public readonly double SpaceAfterResizer(int resizerIndex) => TotalResizeSpace() - SpaceBeforeResizer(resizerIndex);

    public readonly double TotalResizeSpace() => SpaceBeforeResizers[^1];

    public readonly bool IsValid() => ActiveResizerRequestedChange != 0 && TotalResizeElements > 0;

    public static ResizeInfo<T> Build(IEnumerable<T> elements, IResizingHarness<T> harness, Span<double> spaceBeforeResizers)
    {
        ResizeInfo<T> retVal = new(spaceBeforeResizers);
        foreach (T element in elements)
        {
            retVal.AddElement(harness, element);
        }

        return retVal;
    }

    public void AddElement(IResizingHarness<T> harness, T resizable)
    {
        if (_currentBuildElement >= TotalResizeElements)
        {
            throw new IndexOutOfRangeException("Cannot add element to already full ResieInfo");
        }

        SpaceBeforeResizers[_currentBuildElement] = (_currentBuildElement == 0 ? 0 : SpaceBeforeResizers[_currentBuildElement - 1]) + harness.GetResizableSpace(resizable);

        _currentBuildElement++;
    }
}
