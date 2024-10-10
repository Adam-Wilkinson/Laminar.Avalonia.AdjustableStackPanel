namespace Laminar.Avalonia.AdjustableStackPanel.ResizeLogic;

public readonly struct ListSlice<T>(int excludedElementCount, IResizingHarness<T> harness)
{
    private readonly int _excludedElementCount = excludedElementCount;
    private readonly IResizingHarness<T> _harness = harness;

    public required IList<T> OriginalList { private get; init; }

    public required int StartingIndex { private get; init; }

    public required int EndIndex { private get; init; }

    public required bool Reverse { private get; init; }

    public int Length => 1 + EndIndex - StartingIndex - _excludedElementCount;

    public IEnumerable<T> Items 
    {
        get
        {
            if (StartingIndex > EndIndex)
            {
                throw new IndexOutOfRangeException();
            }

            return Reverse switch
            {
                true => ItemsBackwards(),
                false => ItemsForwards(),
            };
        }
    }

    private IEnumerable<T> ItemsForwards()
    {
        for (int i = StartingIndex; i <= EndIndex; i++)
        {
            if (_harness.IsEnabled(OriginalList[i]))
            {
                yield return OriginalList[i];
            }
        }
    }

    private IEnumerable<T> ItemsBackwards()
    {
        for (int i = EndIndex; i >= StartingIndex; i--)
        {
            if (_harness.IsEnabled(OriginalList[i]))
            {
                yield return OriginalList[i];
            }
        }
    }
}

public static class ListSliceExtensions
{
    public static ListSlice<T> CreateBackwardsSlice<T>(this IList<T> originalList, int index, IResizingHarness<T> harness, int excludedElementCount = 0) => new(excludedElementCount, harness)
    {
        OriginalList = originalList,
        StartingIndex = 0,
        EndIndex = index,
        Reverse = true,
    };

    public static ListSlice<T> CreateForwardsSlice<T>(this IList<T> originalList, int index, IResizingHarness<T> harness, int excludedElementCount = 0) => new(excludedElementCount, harness)
    {
        OriginalList = originalList,
        StartingIndex = index,
        EndIndex = originalList.Count - 1,
        Reverse = false,
    };
}