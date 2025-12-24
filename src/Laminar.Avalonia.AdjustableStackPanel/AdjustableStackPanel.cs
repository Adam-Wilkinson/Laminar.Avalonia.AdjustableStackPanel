using System.Collections.Specialized;
using System.Diagnostics;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Reactive;
using Avalonia.VisualTree;
using Laminar.Avalonia.AdjustableStackPanel.ResizeLogic;
using Size = Avalonia.Size;

namespace Laminar.Avalonia.AdjustableStackPanel;

public class AdjustableStackPanel : StackPanel
{
    public static readonly StyledProperty<TimeSpan> TransitionDurationProperty = AvaloniaProperty.Register<AdjustableStackPanel, TimeSpan>("TransitionDuration");
    public TimeSpan TransitionDuration
    {
        get => GetValue(TransitionDurationProperty);
        set => SetValue(TransitionDurationProperty, value);
    }

    public static readonly StyledProperty<Easing> TransitionEasingProperty = AvaloniaProperty.Register<AdjustableStackPanel, Easing>("TransitionEasing", new LinearEasing());
    public Easing TransitionEasing
    {
        get => GetValue(TransitionEasingProperty);
        set => SetValue(TransitionEasingProperty, value);
    }

    public Dictionary<KeyModifiers, ResizerModifier> ResizerModifierKeys { get; } = new()
    {
        { KeyModifiers.Control, ResizerModifier.Move },
        { KeyModifiers.Shift, ResizerModifier.ShrinkGrow },
    };

    private double _totalStackSize;
    private int? _lastChangedResizerIndex;
    private double? _currentResizeAmount;
    private ResizeWidget? _lastChangedResizer;
    private ResizerModifier _resizerModifier = ResizerModifier.None;
    private bool _modeChanging;
    private bool _sizeChangeInvalidatesMeasure = true;

    private readonly ResizeWidget _originalResizer = ResizeWidget.GetOrCreateResizer(new Control());

    static AdjustableStackPanel()
    {
        AffectsParentMeasure<AdjustableStackPanel>(ResizeWidget.AnimatedSizeProperty);
        ResizeWidget.ModeProperty.Changed.Subscribe(new AnonymousObserver<AvaloniaPropertyChangedEventArgs<ResizerMode?>>(ResizeWidgetModeChanged));
    }

    private void OnResize(object? sender, ResizeEventArgs e)
    {
        if (!_sizeChangeInvalidatesMeasure) return;

        _sizeChangeInvalidatesMeasure = false;
        e.Handled = true;
        _currentResizeAmount = e.ResizeAmount;
        InvalidateMeasure();
    }

    private static void ResizeWidgetModeChanged(AvaloniaPropertyChangedEventArgs<ResizerMode?> e)
    {
        if (e.Sender is not ResizeWidget resizer || resizer.GetVisualParent() is not AdjustableStackPanel adjustableStackPanel || adjustableStackPanel._modeChanging)
        {
            return;
        }

        adjustableStackPanel._lastChangedResizer = resizer;
        adjustableStackPanel.UpdateGesture();
    }

    public AdjustableStackPanel()
    {
        AddHandler(ResizeWidget.ResizeEvent, OnResize, RoutingStrategies.Tunnel);

        LogicalChildren.Add(_originalResizer);
        VisualChildren.Add(_originalResizer);
        _originalResizer.BindProperties(TransitionDurationProperty, TransitionEasingProperty, OrientationProperty, this);
        Children.CollectionChanged += Children_CollectionChanged;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        TopLevel.GetTopLevel(this)!.KeyDown += GlobalKeyPressed;
        TopLevel.GetTopLevel(this)!.KeyUp += GlobalKeyPressed;
    }

    private void GlobalKeyPressed(object? sender, KeyEventArgs e)
    {
        var newResizerModifier = ResizerModifierKeys.GetValueOrDefault(e.KeyModifiers, ResizerModifier.None);

        if (newResizerModifier != _resizerModifier || _modeChanging)
        {
            _resizerModifier = newResizerModifier;
            UpdateGesture();
        }
    }

    private void UpdateGesture()
    {
        _modeChanging = true;
        _lastChangedResizerIndex = _originalResizer == _lastChangedResizer ? -1 : null;
        for (var i = 0; i < Children.Count; i++)
        {
            var resizer = ResizeWidget.GetOrCreateResizer(Children[i]);
            if (resizer == _lastChangedResizer)
            {
                _lastChangedResizerIndex = i;
                continue;
            }

            resizer.HideAccessibleModes();
            resizer.Mode = null;
        }

        if (_lastChangedResizerIndex is null)
        {
            _modeChanging = false;
            return;
        }
        
        foreach (var (currentResizerIndex, resize) in ResizeGesture
                     .GetGesture(_lastChangedResizer?.Mode, _resizerModifier)
                     .AccessibleResizes(Children, _lastChangedResizerIndex!.Value, CurrentStackResizeFlags()))
        {
            var currentResizer = ResizerAtIndex(currentResizerIndex);
            currentResizer.ShowAccessibleModes();
            currentResizer.Mode = resize.ResizerMode;
        }

        _modeChanging = false;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var flags = CurrentStackResizeFlags();
        var isHorizontal = Orientation == Orientation.Horizontal;
        _originalResizer.IsVisible = flags.HasFlag(ResizeFlags.CanMoveStackStart);

        
        if (IsInStretchMode() && Children.Count > 0)
        {
            var resizeInfo = ResizeInfo<Control>.Build(Children, ControlResizingHarness.GetHarness(Orientation), stackalloc ResizeElementInfo[Children.Count]);
            var freeSpace = (isHorizontal ? finalSize.Width : finalSize.Height) - _totalStackSize;
            _totalStackSize += new Scale().Run(resizeInfo.GetElementsAfter(-1, Children), resizeInfo.Harness, freeSpace, resizeInfo.TotalResizeSpace());
        }

        double currentDepth = 0;

        if (flags.HasFlag(ResizeFlags.CanMoveStackStart))
        {
            var originalResizerDepth = isHorizontal ? _originalResizer.DesiredSize.Width : _originalResizer.DesiredSize.Height;
            _originalResizer.Arrange(OrientedArrangeRect(currentDepth, originalResizerDepth, finalSize));
            currentDepth += originalResizerDepth + _originalResizer.PositionOffset;
        }

        for (int i = 0, count = Children.Count; i < count; i++)
        {
            var child = Children[i];
            var currentResizer = ResizeWidget.GetOrCreateResizer(child);

            if (!child.IsVisible)
            {
                currentResizer.IsVisible = false;
                continue;
            }

            var resizerDepth = Orientation == Orientation.Horizontal ? currentResizer.DesiredSize.Width : currentResizer.DesiredSize.Height;
            var spaceAfterThisResizer = flags.HasFlag(ResizeFlags.CanMoveStackEnd) ?
                double.PositiveInfinity :
                _totalStackSize - currentDepth - currentResizer.AnimatedSize - currentResizer.PositionOffset;

            var trueResizerControlDepth = Math.Min(resizerDepth, spaceAfterThisResizer);
            var relativeStartOfResizer = currentResizer.AnimatedSize - trueResizerControlDepth;
            child.Arrange(OrientedArrangeRect(currentDepth, Math.Max(relativeStartOfResizer, 0), finalSize));
            currentDepth += relativeStartOfResizer;
            currentResizer.Arrange(OrientedArrangeRect(currentDepth, Math.Max(trueResizerControlDepth, 0), finalSize));
            currentDepth += trueResizerControlDepth + currentResizer.PositionOffset;
        }
        
        _sizeChangeInvalidatesMeasure = true;
        _totalStackSize = currentDepth;
        return finalSize;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _sizeChangeInvalidatesMeasure = false;
        var harness = ControlResizingHarness.GetHarness(Orientation);
        var currentHoverResizer = _lastChangedResizer;
        var isHorizontal = Orientation == Orientation.Horizontal;
        double measuredStackHeight = 0;
        double maximumStackDesiredWidth = 0;
        ResizeInfo<Control> resizeInfo = new(stackalloc ResizeElementInfo[Children.Count], harness)
        {
            ActiveResizerRequestedChange = _currentResizeAmount.GetValueOrDefault(),
            ActiveResizerIndex = _lastChangedResizerIndex.GetValueOrDefault(),
            Flags = CurrentStackResizeFlags(),
        };
        
        if (CurrentStackResizeFlags().HasFlag(ResizeFlags.CanMoveStackStart))
        {
            _originalResizer.Measure(availableSize);
            measuredStackHeight += isHorizontal ? _originalResizer.DesiredSize.Width : _originalResizer.DesiredSize.Height;
        }

        measuredStackHeight += _originalResizer.PositionOffset;
        var totalStackSize = measuredStackHeight;

        for (int i = 0, count = Children.Count; i < count; ++i)
        {
            var child = Children[i];
            
            if (!child.IsVisible)
            {
                continue;
            }

            var resizer = ResizeWidget.GetOrCreateResizer(child);
            child.Measure(availableSize);
            resizer.Measure(availableSize);

            if (double.IsNaN(resizer.AnimatedSize))
            {
                resizer.SetSizeTo(
                    IsInStretchMode() 
                        ? _totalStackSize / Math.Max(1, Children.Count - 1) 
                        : harness.GetMinimumSize(child), 
                    IsLoaded);
            }

            measuredStackHeight += (IsInStretchMode() ? harness.GetMinimumSize(child) : resizer.AnimatedSize) + resizer.PositionOffset;
            maximumStackDesiredWidth = Math.Max(maximumStackDesiredWidth, isHorizontal ? child.DesiredSize.Height : child.DesiredSize.Width);

            totalStackSize += resizer.AnimatedSize + resizer.PositionOffset;
            resizeInfo.AddElement(child);
        }

        if (resizeInfo.IsValid() 
            && currentHoverResizer?.Mode is not null 
            && ResizeGesture.TryGetGesture(currentHoverResizer.Mode.Value, _resizerModifier, out var gesture))
        {
            var stackHeightChange = gesture.Execute(Children, resizeInfo); 
            measuredStackHeight += stackHeightChange;
            totalStackSize += stackHeightChange;
        }

        
        _currentResizeAmount = null;
        _totalStackSize = totalStackSize;
        return isHorizontal ? new Size(measuredStackHeight, maximumStackDesiredWidth) : new Size(maximumStackDesiredWidth, measuredStackHeight);
    }

    private void Children_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var spaceRemoved = e.OldItems is null ? 0 : ProcessItemsRemoved(e);
        var spaceAdded = e.NewItems is null ? 0 : ProcessItemsAdded(e);

        double lastOffset = 0;
        for (var i = -1; i < Children.Count; i++)
        {
            var index = i;
            var resizer = ResizerAtIndex(i);
            resizer.ModeAccessibleCheck = (mode) => ModeAccessibleCheck(mode, index);
            var currentOffset = (i + 2 > e.OldStartingIndex ? spaceRemoved : 0) - (i + 1 > e.NewStartingIndex ? spaceAdded : 0);
            resizer.PositionOffset += currentOffset - lastOffset;
            lastOffset = currentOffset;
        }
    }

    private double ProcessItemsAdded(NotifyCollectionChangedEventArgs e)
    {
        _sizeChangeInvalidatesMeasure = false;

        double addedSize = 0;
        foreach (var item in e.NewItems!)
        {
            if (item is not Control addedControl)
            {
                continue;
            }

            var resizer = ResizeWidget.GetOrCreateResizer(addedControl, this);
            resizer.BindProperties(TransitionDurationProperty, TransitionEasingProperty, OrientationProperty, this);
            LogicalChildren.Add(resizer);
            VisualChildren.Add(resizer);
            addedSize += double.IsNaN(resizer.AnimatedSize) ? 0 : resizer.AnimatedSize;
        }

        return addedSize;
    }

    private double ProcessItemsRemoved(NotifyCollectionChangedEventArgs e)
    {
        _sizeChangeInvalidatesMeasure = false;

        double removedSize = 0;
        foreach (var item in e.OldItems!)
        {
            if (item is not Control removedControl)
            {
                continue;
            }

            var resizer = ResizeWidget.GetOrCreateResizer(removedControl);
            removedSize += resizer.AnimatedSize;
            resizer.ModeAccessibleCheck = null;
            LogicalChildren.Remove(resizer);
            VisualChildren.Remove(resizer);
        }

        return removedSize;
    }

    private bool ModeAccessibleCheck(ResizerMode mode, int resizerIndex) => mode.IsAccessible(resizerIndex, Children.Count, CurrentStackResizeFlags());

    private ResizeFlags CurrentStackResizeFlags()
    {
        if (Orientation == Orientation.Horizontal)
        {
            return HorizontalAlignment switch
            {
                HorizontalAlignment.Left => ResizeFlags.CanMoveStackEnd,
                HorizontalAlignment.Right => ResizeFlags.CanMoveStackStart,
                HorizontalAlignment.Center => ResizeFlags.CanMoveStackEnd | ResizeFlags.CanMoveStackStart | ResizeFlags.PreferResize,
                _ => ResizeFlags.None,
            };
        }

        return VerticalAlignment switch
        {
            VerticalAlignment.Top => ResizeFlags.CanMoveStackEnd,
            VerticalAlignment.Bottom => ResizeFlags.CanMoveStackStart,
            VerticalAlignment.Center => ResizeFlags.CanMoveStackEnd | ResizeFlags.CanMoveStackStart | ResizeFlags.PreferResize,
            _ => ResizeFlags.None,
        };
    }

    private bool IsInStretchMode()
        => Orientation == Orientation.Horizontal ? HorizontalAlignment == HorizontalAlignment.Stretch : VerticalAlignment == VerticalAlignment.Stretch;

    private ResizeWidget ResizerAtIndex(int index) 
        => index == -1 ? _originalResizer : ResizeWidget.GetOrCreateResizer(Children[index]);

    private Rect OrientedArrangeRect(double depthStart, double depthSize, Size finalSize) 
        => Orientation == Orientation.Horizontal 
            ? new Rect(depthStart, 0, depthSize, finalSize.Height) 
            : new Rect(0, depthStart, finalSize.Width, depthSize);
}
