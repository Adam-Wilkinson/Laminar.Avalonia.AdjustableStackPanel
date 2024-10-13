namespace Laminar.Avalonia.AdjustableStackPanel.ResizeLogic;

public readonly struct ListSlice<T>(IResizingHarness<T> harness)
{
    private readonly IResizingHarness<T> _harness = harness;

    public required IList<T> OriginalList { get; init; }

    public required int StartingIndex { get; init; }

    public required int ElementCount { get; init; }

    public required bool Reverse { get; init; }

    public IEnumerable<T> Items 
    {
        get
        {
            if (ElementCount <= 0)
            {
                return [];
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
        int returnedItemCount = 0;
        int currentIndex = StartingIndex;
        while (returnedItemCount < ElementCount)
        {
            if (_harness.IsEnabled(OriginalList[currentIndex]))
            {
                yield return OriginalList[currentIndex];
                returnedItemCount++;
            }

            currentIndex++;
        }
    }

    private IEnumerable<T> ItemsBackwards()
    {
        int returnedItemCount = 0;
        int currentIndex = StartingIndex;
        while (returnedItemCount < ElementCount)
        {
            if (_harness.IsEnabled(OriginalList[currentIndex]))
            {
                yield return OriginalList[currentIndex];
                returnedItemCount++;
            }

            currentIndex--;
        }
    }
}