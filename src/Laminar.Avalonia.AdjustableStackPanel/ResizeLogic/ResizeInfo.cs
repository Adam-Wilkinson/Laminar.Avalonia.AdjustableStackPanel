namespace Laminar.Avalonia.AdjustableStackPanel.ResizeLogic;

public readonly record struct ResizeElementInfo(double ResizableSpaceBefore, int DisabledElementsBefore);

public ref struct ResizeInfo<T>(Span<ResizeElementInfo> resizeElementInfos)
{
    private readonly Span<ResizeElementInfo> _resizeElementInfos = resizeElementInfos;
    private readonly int _resizeElementCapacity = resizeElementInfos.Length;
    private int _resizeElementCount;
    private int _disabledElementCount = 0;

    public double ActiveResizerRequestedChange { get; set; }

    public int ActiveResizerIndex { get; set; }

    public ResizeFlags ResizeFlags { get; set; }

    public readonly (ListSlice<T>? elementsBefore, ListSlice<T>? elementsAfter) SplitElements(IList<T> allElements, IResizingHarness<T> harness, int index)
    {
        return (
            index < 0 ? null : allElements.CreateBackwardsSlice(index, harness, _resizeElementInfos[index].DisabledElementsBefore),
            index + 1 >= _resizeElementCount ? null : allElements.CreateForwardsSlice(index + 1, harness, _disabledElementCount - _resizeElementInfos[index + 1].DisabledElementsBefore)
        );
    }

    public readonly double SpaceBeforeResizer(int resizerIndex) => resizerIndex < 0 ? 0 : _resizeElementInfos[resizerIndex].ResizableSpaceBefore;

    public readonly double SpaceAfterResizer(int resizerIndex) => TotalResizeSpace() - SpaceBeforeResizer(resizerIndex);

    public readonly double TotalResizeSpace() => _resizeElementInfos[^1].ResizableSpaceBefore;

    public readonly bool IsValid() => ActiveResizerRequestedChange != 0 && _resizeElementCapacity > 0;

    public static ResizeInfo<T> Build(IEnumerable<T> elements, IResizingHarness<T> harness, Span<ResizeElementInfo> resizeElementInfos)
    {
        ResizeInfo<T> retVal = new(resizeElementInfos);
        foreach (T element in elements)
        {
            retVal.AddElement(harness, element);
        }

        return retVal;
    }

    public void AddElement(IResizingHarness<T> harness, T resizable)
    {
        if (_resizeElementCount >= _resizeElementCapacity)
        {
            throw new IndexOutOfRangeException("ResizeInfo is at capacity");
        }

        _resizeElementInfos[_resizeElementCount] = new()
        {
            ResizableSpaceBefore = (_resizeElementCount == 0 ? 0 : _resizeElementInfos[_resizeElementCount - 1].ResizableSpaceBefore) + harness.GetResizableSpace(resizable),
            DisabledElementsBefore = _disabledElementCount,
        };

        _disabledElementCount += harness.IsEnabled(resizable) ? 0 : 1;
        _resizeElementCount++;
    }
}
