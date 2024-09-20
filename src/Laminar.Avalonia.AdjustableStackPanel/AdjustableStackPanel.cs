using System.Collections.Specialized;
using System.Diagnostics;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Reactive;
using Avalonia.VisualTree;
using Laminar.Avalonia.AdjustableStackPanel.ResizeLogic;

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

    private double totalStackSize = 0;
    private int? _lastChangedResizerIndex = null;
    private double? _currentResizeAmount = null;
    private ResizeWidget? _lastChangedResizer = null;
    private ResizerModifier _resizerModifier = ResizerModifier.None;
    private bool _modeChanging = false;
    private bool _sizeChangeInvalidatesMeasure = true;

    private readonly ResizeWidget _originalResizer = new();

    static AdjustableStackPanel()
    {
        ResizeWidget.SizeProperty.Changed.Subscribe(new AnonymousObserver<AvaloniaPropertyChangedEventArgs<double>>(ResizeWidgetSizeChanged));
        ResizeWidget.ModeProperty.Changed.Subscribe(new AnonymousObserver<AvaloniaPropertyChangedEventArgs<ResizerMode>>(ResizeWidgetModeChanged));
    }

    private static void ResizeWidgetSizeChanged(AvaloniaPropertyChangedEventArgs<double> e)
    {
        if (e.Sender is not ResizeWidget resizer || resizer.GetVisualParent() is not AdjustableStackPanel adjustableStackPanel || !adjustableStackPanel._sizeChangeInvalidatesMeasure)
        {
            return;
        }

        adjustableStackPanel._sizeChangeInvalidatesMeasure = false;
        adjustableStackPanel._currentResizeAmount = e.NewValue.GetValueOrDefault() - e.OldValue.GetValueOrDefault();
        resizer.Size = e.OldValue.GetValueOrDefault();
        adjustableStackPanel.InvalidateMeasure();
    }

    private static void ResizeWidgetModeChanged(AvaloniaPropertyChangedEventArgs<ResizerMode> e)
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
        _originalResizer[!ResizeWidget.OrientationProperty] = this[!OrientationProperty];
        LogicalChildren.Add(_originalResizer);
        VisualChildren.Add(_originalResizer);
        _originalResizer.OffsetAnimator.BindTransitionProperties(TransitionDurationProperty, TransitionEasingProperty, this);
        Children.CollectionChanged += Children_CollectionChanged;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        TopLevel.GetTopLevel(this)!.KeyDown += GlobalKeyPressed;
        TopLevel.GetTopLevel(this)!.KeyUp += GlobalKeyPressed;
    }

    private void GlobalKeyPressed(object? sender, KeyEventArgs e)
    {
        ResizerModifier newResizerModifier = e.KeyModifiers switch
        {
            KeyModifiers.Control => ResizerModifier.Move,
            KeyModifiers.Shift => ResizerModifier.ShrinkGrow,
            _ => ResizerModifier.None,
        };

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
        for (int i = 0; i < Children.Count; i++)
        {
            ResizeWidget resizer = ResizeWidget.GetOrCreateResizer(Children[i]);
            if (resizer == _lastChangedResizer)
            {
                _lastChangedResizerIndex = i;
                continue;
            }

            resizer.HideAccessibleModes();
            resizer.Mode = ResizerMode.None;
        }

        foreach ((int currentResizerIndex, Resize resize) in ResizeGesture.GetGesture(_lastChangedResizer?.Mode, _resizerModifier).AccessibleResizes(Children, _lastChangedResizerIndex!.Value))
        {
            ResizeWidget currentResizer = ResizerAtIndex(currentResizerIndex);
            currentResizer.ShowAccessibleModes();
            currentResizer.Mode = resize.ResizerMode;
        }

        _modeChanging = false;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Controls children = Children;
        double currentDepth = ArrangeResizer(_originalResizer, 0, finalSize, CurrentStackResizeFlags().HasFlag(ResizeFlags.DisableResizeBefore));
        Span<double> resizableSpaceBeforeControls = stackalloc double[children.Count];

        for (int i = 0, count = children.Count; i < count; i++)
        {
            Control child = children[i];

            if (child is null || !child.IsVisible)
            {
                continue;
            }

            ResizeWidget currentResizer = ResizeWidget.GetOrCreateResizer(child);

            if (currentResizer.Size == 0 && IsInStretchMode())
            {
                currentResizer.Size = (Orientation == Orientation.Horizontal ? finalSize.Width : finalSize.Height) / Math.Max(Children.Count - 1, 1);
                currentResizer.OffsetAnimator.ChangeSizeOffset(-currentResizer.Size);
            }

            double controlSize = Math.Max(0, currentResizer.Size + currentResizer.OffsetAnimator.SizeOffset);
            child.Arrange(OrientedArrangeRect(currentDepth, controlSize, finalSize));
            currentDepth += controlSize;

            currentDepth += ArrangeResizer(currentResizer, currentDepth, finalSize, i < count - 1 || CurrentStackResizeFlags().HasFlag(ResizeFlags.DisableResizeAfter));
            double childDesiredDepth = Orientation == Orientation.Horizontal ? child.DesiredSize.Width : child.DesiredSize.Height;
            resizableSpaceBeforeControls[i] = (i == 0 ? 0 : resizableSpaceBeforeControls[i - 1]) + controlSize - childDesiredDepth;
        }

        double freeSpace = (Orientation == Orientation.Horizontal ? finalSize.Width : finalSize.Height) - currentDepth;
        if (IsInStretchMode())
        {
            ResizeMethod.SqueezeExpand.RunMethod(Children.CreateForwardsSlice(0), ControlResizingHarness.GetHarness(Orientation), freeSpace, resizableSpaceBeforeControls[^1]);
        }
        else if (freeSpace < 0 && _lastChangedResizer is not null && _lastChangedResizerIndex.HasValue)
        {
            ResizeGesture.GetGesture(_lastChangedResizer.Mode, _resizerModifier).Execute(Children, _lastChangedResizerIndex.Value, -freeSpace, resizableSpaceBeforeControls, ControlResizingHarness.GetHarness(Orientation), CurrentStackResizeFlags());
        }

        _currentResizeAmount = null;
        _sizeChangeInvalidatesMeasure = true;
        totalStackSize = currentDepth;
        return finalSize;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _sizeChangeInvalidatesMeasure = false;
        Controls children = Children;
        ResizeWidget? currentHoverResizer = _lastChangedResizer;
        bool isHorizontal = Orientation == Orientation.Horizontal;
        double totalStackHeight = 0;
        double maximumStackDesiredWidth = 0.0;
        Span<double> resizableSpaceBeforeControls = stackalloc double[children.Count];

        if (CurrentStackResizeFlags().HasFlag(ResizeFlags.DisableResizeBefore))
        {
            _originalResizer.Measure(availableSize);
            totalStackHeight += isHorizontal ? _originalResizer.DesiredSize.Width : _originalResizer.DesiredSize.Height;
        }

        totalStackHeight += _originalResizer.OffsetAnimator.PositionOffsetAfter;

        for (int i = 0, count = children.Count; i < count; ++i)
        {
            Control child = children[i];
            ResizeWidget resizer = ResizeWidget.GetOrCreateResizer(child);
            child.Measure(availableSize);
            resizer.Measure(availableSize);
            Size childDesiredSizeOriented = isHorizontal ? new Size(child.DesiredSize.Height, child.DesiredSize.Width) : child.DesiredSize;
            Size resizerDesiredSizeOriented = isHorizontal ? new Size(resizer.DesiredSize.Height, resizer.DesiredSize.Width) : resizer.DesiredSize;

            if (resizer.Size == 0)
            {
                resizer.Size = childDesiredSizeOriented.Height;
                if (totalStackSize > 0)
                {
                    resizer.OffsetAnimator.ChangeSizeOffset(-resizer.Size);
                }
            }

            resizer.Size = Math.Max(resizer.Size, childDesiredSizeOriented.Height);
            double resizerDictatedControlSize = resizer.Size + resizer.OffsetAnimator.SizeOffset;
            totalStackHeight += (IsInStretchMode() ? childDesiredSizeOriented.Height : resizerDictatedControlSize) + resizerDesiredSizeOriented.Height + resizer.OffsetAnimator.SizeOffset + resizer.OffsetAnimator.PositionOffsetAfter;
            maximumStackDesiredWidth = Math.Max(maximumStackDesiredWidth, childDesiredSizeOriented.Width);

            resizableSpaceBeforeControls[i] = (i == 0 ? 0 : resizableSpaceBeforeControls[i - 1]) + resizerDictatedControlSize - childDesiredSizeOriented.Height;
        }

        if (_currentResizeAmount.HasValue && _lastChangedResizerIndex.HasValue && ResizeGesture.TryGetGesture(currentHoverResizer?.Mode, _resizerModifier, out ResizeGesture gesture))
        {
            totalStackHeight += gesture.Execute(Children, _lastChangedResizerIndex.Value, _currentResizeAmount.Value, resizableSpaceBeforeControls, ControlResizingHarness.GetHarness(Orientation), CurrentStackResizeFlags());
        }

        return isHorizontal ? new Size(totalStackHeight, maximumStackDesiredWidth) : new Size(maximumStackDesiredWidth, totalStackHeight);
    }

    private void Children_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        double spaceRemoved = e.OldItems is null ? 0 : ProcessItemsRemoved(e);
        double spaceAdded = e.NewItems is null ? 0 : ProcessItemsAdded(e);

        double totalOffset = 0;
        for (int i = -1; i < Children.Count; i++)
        {
            int index = i;
            ResizeWidget resizer = ResizerAtIndex(i);
            resizer.ModeAccessibleCheck = (mode) => ModeAccessibleCheck(mode, index);
            double currentOffset = (i + 2 > e.OldStartingIndex ? spaceRemoved : 0) - (i + 1 > e.NewStartingIndex ? spaceAdded : 0);
            resizer.OffsetAnimator.ChangePositionOffset(currentOffset - totalOffset);
            totalOffset = currentOffset;
        }
    }

    private double ProcessItemsAdded(NotifyCollectionChangedEventArgs e)
    {
        _sizeChangeInvalidatesMeasure = false;

        double addedSize = 0;
        int totalItemsBeforeChange = Children.Count - e.NewItems!.Count;
        foreach (object item in e.NewItems!)
        {
            if (item is not Control addedControl)
            {
                continue;
            }

            ResizeWidget resizer = ResizeWidget.GetOrCreateResizer(addedControl);
            resizer[!ResizeWidget.OrientationProperty] = this[!OrientationProperty];
            LogicalChildren.Add(resizer);
            VisualChildren.Add(resizer);
            resizer.OffsetAnimator.BindTransitionProperties(TransitionDurationProperty, TransitionEasingProperty, this);

            if (resizer.Size == 0 && totalItemsBeforeChange > 0 && IsInStretchMode())
            {
                resizer.Size = totalStackSize / Math.Max(totalItemsBeforeChange, 1);
                resizer.OffsetAnimator.ChangeSizeOffset(-resizer.Size);
            }

            addedSize += resizer.Size + resizer.OffsetAnimator.SizeOffset + (Orientation == Orientation.Horizontal ? resizer.DesiredSize.Width : resizer.DesiredSize.Height);
        }

        return addedSize;
    }

    private double ProcessItemsRemoved(NotifyCollectionChangedEventArgs e)
    {
        _sizeChangeInvalidatesMeasure = false;

        double removedSize = 0;
        foreach (object item in e.OldItems!)
        {
            if (item is not Control removedControl)
            {
                continue;
            }

            ResizeWidget resizer = ResizeWidget.GetOrCreateResizer(removedControl);
            removedSize += resizer.Size + resizer.OffsetAnimator.SizeOffset;
            removedSize += Orientation == Orientation.Horizontal ? resizer.DesiredSize.Width : resizer.DesiredSize.Height;
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
                HorizontalAlignment.Left => ResizeFlags.DisableResizeAfter,
                HorizontalAlignment.Right => ResizeFlags.DisableResizeBefore,
                HorizontalAlignment.Center => ResizeFlags.DisableResizeAfter | ResizeFlags.DisableResizeBefore,
                _ => ResizeFlags.None,
            };
        }
        else
        {
            return VerticalAlignment switch
            {
                VerticalAlignment.Top => ResizeFlags.DisableResizeAfter,
                VerticalAlignment.Bottom => ResizeFlags.DisableResizeBefore,
                VerticalAlignment.Center => ResizeFlags.DisableResizeAfter | ResizeFlags.DisableResizeBefore,
                _ => ResizeFlags.None,
            };
        }
    }

    private bool IsInStretchMode()
    {
        ResizeFlags flags = CurrentStackResizeFlags();
        return !flags.HasFlag(ResizeFlags.DisableResizeBefore)  && !flags.HasFlag(ResizeFlags.DisableResizeAfter);
    }

    private ResizeWidget ResizerAtIndex(int index)
    {
        if (index == -1)
        {
            return _originalResizer;
        }

        return ResizeWidget.GetOrCreateResizer(Children[index]);
    }

    private double ArrangeResizer(ResizeWidget resizer, double currentDepth, Size finalSize, bool isActive = true)
    {
        double resizerDepth = 0;
        if (isActive)
        {
            resizer.IsVisible = true;
            resizerDepth = Orientation == Orientation.Horizontal ? resizer.DesiredSize.Width : resizer.DesiredSize.Height;
            resizer.Arrange(OrientedArrangeRect(currentDepth, resizerDepth, finalSize));
        }
        else
        {
            resizer.IsVisible = false;
        }

        return resizerDepth + resizer.OffsetAnimator.PositionOffsetAfter;
    }

    private Rect OrientedArrangeRect(double depthStart, double depthSize, Size finalSize)
    {
        return Orientation == Orientation.Horizontal ? new Rect(depthStart, 0, depthSize, finalSize.Height) : new Rect(0, depthStart, finalSize.Width, depthSize);
    }
}
