namespace Laminar.Avalonia.AdjustableStackPanel.ResizeLogic;

public readonly ref struct ResizableElementSlice<T>(IResizingHarness<T> harness)
{
    private readonly IResizingHarness<T> _harness = harness;

    public required IList<T> OriginalList { get; init; }

    public required int StartingIndex { get; init; }

    public required int Length { get; init; }

    public required bool Reverse { get; init; }

    public Enumerator GetEnumerator()
        => new(this);

    public ref struct Enumerator
    {
        private readonly IList<T> _originalList;
        private readonly IResizingHarness<T> _harness;
        private readonly int _elementCount;
        private readonly int _increment;

        private int _currentIndex;
        private int _returnedElementCount = 0;

        public Enumerator(ResizableElementSlice<T> list)
        {
            _originalList = list.OriginalList;
            _harness = list._harness;
            _elementCount = list.Length;
            _increment = list.Reverse ? -1 : 1;

            _currentIndex = list.StartingIndex - _increment;
        }

        public bool MoveNext()
        {
            _currentIndex += _increment;
            if (_returnedElementCount < _elementCount)
            {
                if (_harness.IsEnabled(_originalList[_currentIndex]))
                {
                    _returnedElementCount++;
                    return true;
                }
                else
                {
                    return MoveNext();
                }
            }

            return false;
        }

        public readonly T Current => _originalList[_currentIndex];
    }
}