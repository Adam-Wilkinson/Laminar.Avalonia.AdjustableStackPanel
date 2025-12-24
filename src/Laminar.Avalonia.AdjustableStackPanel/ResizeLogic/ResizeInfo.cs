namespace Laminar.Avalonia.AdjustableStackPanel.ResizeLogic;

public readonly record struct ResizeElementInfo(double ResizableSpaceBefore, double MinimumSpaceBefore, int DisabledElementsBefore);

public ref struct ResizeInfo<T>(Span<ResizeElementInfo> resizeElementInfos, IResizingHarness<T> harness)
{
    private readonly Span<ResizeElementInfo> _resizeElementInfos = resizeElementInfos;
    private readonly int _resizeElementCapacity = resizeElementInfos.Length;

    private int _resizeElementCount;
    private int _disabledElementCount = 0;

    public double ActiveResizerRequestedChange { get; init; }

    public int ActiveResizerIndex { get; init; }

    public ResizeFlags Flags { get; set; }

    public IResizingHarness<T> Harness { get; } = harness;

    public readonly ResizableElementSlice<T> GetElementsBefore(int index, IList<T> allElements)
        => new(Harness)
        {
            OriginalList = allElements,
            Length = index + 1 - DisabledElementsBefore(index + 1),
            StartingIndex = index,
            Reverse = true,
        };

    public readonly ResizableElementSlice<T> GetElementsAfter(int index, IList<T> allElements)
    {
        int totalElementsAfter = _resizeElementCount - (index + 1);
        int disabledElementsAfter = _disabledElementCount - DisabledElementsBefore(index + 1);
        int enabledElementsAfter = totalElementsAfter - disabledElementsAfter;

        return new ResizableElementSlice<T>(Harness) 
        { 
            OriginalList = allElements, 
            Length = enabledElementsAfter, 
            StartingIndex = index + 1, 
            Reverse = false 
        };
    }

    public readonly int DisabledElementsBefore(int resizerIndex) => resizerIndex switch
    {
        < 0 => 0,
        _ => _resizeElementInfos[Math.Min(resizerIndex, _resizeElementInfos.Length - 1)].DisabledElementsBefore,
    };

    public readonly double SpaceBeforeResizer(int resizerIndex) => resizerIndex < 0 ? 0 : _resizeElementInfos[resizerIndex].ResizableSpaceBefore;

    public readonly double SpaceAfterResizer(int resizerIndex) => TotalResizeSpace() - SpaceBeforeResizer(resizerIndex);

    public readonly double TotalResizeSpace() => _resizeElementInfos[^1].ResizableSpaceBefore;

    public readonly double TotalSpace() => _resizeElementInfos[^1].MinimumSpaceBefore + TotalResizeSpace();
    
    public readonly bool IsValid() => ActiveResizerRequestedChange != 0 && _resizeElementCapacity > 0;

    public static ResizeInfo<T> Build(IEnumerable<T> elements, IResizingHarness<T> harness, Span<ResizeElementInfo> resizeElementInfos)
    {
        ResizeInfo<T> retVal = new(resizeElementInfos, harness);
        foreach (T element in elements)
        {
            retVal.AddElement(element);
        }

        return retVal;
    }

    public void AddElement(T resizable)
    {
        if (_resizeElementCount >= _resizeElementCapacity)
        {
            throw new IndexOutOfRangeException("ResizeInfo is at capacity");
        }

        _resizeElementInfos[_resizeElementCount] = new()
        {
            ResizableSpaceBefore = (_resizeElementCount == 0 ? 0 : _resizeElementInfos[_resizeElementCount - 1].ResizableSpaceBefore) + (Harness.IsEnabled(resizable) ? Harness.GetResizableSpace(resizable) : 0),
            MinimumSpaceBefore = (_resizeElementCount == 0 ? 0 : _resizeElementInfos[_resizeElementCount - 1].MinimumSpaceBefore) + (Harness.IsEnabled(resizable) ? Harness.GetMinimumSize(resizable) : 0),
            DisabledElementsBefore = _disabledElementCount,
        };

        _disabledElementCount += Harness.IsEnabled(resizable) ? 0 : 1;
        _resizeElementCount++;
    }
}
