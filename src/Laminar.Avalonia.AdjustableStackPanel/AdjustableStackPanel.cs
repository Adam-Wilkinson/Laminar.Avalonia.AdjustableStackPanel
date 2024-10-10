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

    private double _totalStackSize = 0;
    private int? _lastChangedResizerIndex = null;
    private double? _currentResizeAmount = null;
    private ResizeWidget? _lastChangedResizer = null;
    private ResizerModifier _resizerModifier = ResizerModifier.None;
    private bool _modeChanging = false;
    private bool _sizeChangeInvalidatesMeasure = true;

    private readonly ResizeWidget _originalResizer = new();

    static AdjustableStackPanel()
    {
        ResizeWidget.ModeProperty.Changed.Subscribe(new AnonymousObserver<AvaloniaPropertyChangedEventArgs<ResizerMode>>(ResizeWidgetModeChanged));
    }

    private void OnResize(object? sender, ResizeEventArgs e)
    {
        if (!_sizeChangeInvalidatesMeasure) return;

        _sizeChangeInvalidatesMeasure = false;
        e.Handled = true;
        _currentResizeAmount = e.ResizeAmount;
        InvalidateMeasure();
    }

    private void OnResizerModeChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Sender is not ResizeWidget resizer) return;

        _lastChangedResizer = resizer;
        UpdateGesture();
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

        foreach ((int currentResizerIndex, ResizerMovement resize) in ResizeGesture.GetGesture(_lastChangedResizer?.Mode, _resizerModifier).AccessibleResizes(Children, _lastChangedResizerIndex!.Value))
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

        if (IsInStretchMode() && Children.Count > 0)
        {
            ResizeControlsToFillSpace(finalSize);
        }

        for (int i = 0, count = children.Count; i < count; i++)
        {
            Control child = children[i];
            ResizeWidget currentResizer = ResizeWidget.GetOrCreateResizer(child);

            if (child is null || !child.IsVisible)
            {
                currentResizer.IsVisible = false;
                continue;
            }

            double controlSize = Math.Max(currentResizer.Size, 0);
            child.Arrange(OrientedArrangeRect(currentDepth, controlSize, finalSize));
            currentDepth += controlSize;
            currentDepth += ArrangeResizer(currentResizer, currentDepth, finalSize, i < count - 1 || CurrentStackResizeFlags().HasFlag(ResizeFlags.DisableResizeAfter));
        }

        _currentResizeAmount = null;
        _sizeChangeInvalidatesMeasure = true;
        _totalStackSize = currentDepth;
        return finalSize;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _sizeChangeInvalidatesMeasure = false;
        Controls children = Children;
        ResizeWidget? currentHoverResizer = _lastChangedResizer;
        bool isHorizontal = Orientation == Orientation.Horizontal;
        double measuredStackHeight = 0;
        double maximumStackDesiredWidth = 0.0;
        Span<double> resizableSpaceBeforeControls = stackalloc double[children.Count];

        if (CurrentStackResizeFlags().HasFlag(ResizeFlags.DisableResizeBefore))
        {
            _originalResizer.Measure(availableSize);
            measuredStackHeight += isHorizontal ? _originalResizer.DesiredSize.Width : _originalResizer.DesiredSize.Height;
        }

        measuredStackHeight += _originalResizer.PositionOffset;
        double totalStackSize = measuredStackHeight;

        for (int i = 0, count = children.Count; i < count; ++i)
        {
            Control child = children[i];

            if (child is null || !child.IsVisible)
            {
                continue;
            }

            ResizeWidget resizer = ResizeWidget.GetOrCreateResizer(child);
            child.Measure(availableSize);
            resizer.Measure(availableSize);
            Size childDesiredSizeOriented = isHorizontal ? new Size(child.DesiredSize.Height, child.DesiredSize.Width) : child.DesiredSize;
            Size resizerDesiredSizeOriented = isHorizontal ? new Size(resizer.DesiredSize.Height, resizer.DesiredSize.Width) : resizer.DesiredSize;

            if (resizer.Size < 0 && !IsInStretchMode())
            {
                resizer.Size = 0;
                resizer.SetSizeTo(childDesiredSizeOriented.Height, IsLoaded);
            }

            measuredStackHeight += (IsInStretchMode() ? childDesiredSizeOriented.Height : resizer.Size) + resizerDesiredSizeOriented.Height + resizer.PositionOffset;
            maximumStackDesiredWidth = Math.Max(maximumStackDesiredWidth, childDesiredSizeOriented.Width);

            totalStackSize += resizer.Size + resizer.PositionOffset + resizerDesiredSizeOriented.Height;
            resizableSpaceBeforeControls[i] = (i == 0 ? 0 : resizableSpaceBeforeControls[i - 1]) + ControlResizingHarness.GetHarness(Orientation).GetResizableSpace(child);
        }

        if (_currentResizeAmount.HasValue && _lastChangedResizerIndex.HasValue && ResizeGesture.TryGetGesture(currentHoverResizer?.Mode, _resizerModifier, out ResizeGesture gesture))
        {
            double stackHeightChange = gesture.Execute(Children, _lastChangedResizerIndex.Value, _currentResizeAmount.Value, resizableSpaceBeforeControls, ControlResizingHarness.GetHarness(Orientation), CurrentStackResizeFlags());
            measuredStackHeight += stackHeightChange;
            totalStackSize += stackHeightChange;
        }

        _totalStackSize = totalStackSize;
        return isHorizontal ? new Size(measuredStackHeight, maximumStackDesiredWidth) : new Size(maximumStackDesiredWidth, measuredStackHeight);
    }

    private void ResizeControlsToFillSpace(Size availableSpace)
    {
        Controls children = Children;
        Span<double> resizableSpaceBeforeControls = stackalloc double[children.Count];
        double totalStackSize = 0;

        if (CurrentStackResizeFlags().HasFlag(ResizeFlags.DisableResizeBefore))
        {
            totalStackSize += Orientation == Orientation.Horizontal ? _originalResizer.DesiredSize.Width : _originalResizer.DesiredSize.Height;
        }

        totalStackSize += _originalResizer.PositionOffset;

        for (int i = 0, count = children.Count; i < count; i++)
        {
            Control child = children[i];

            if (child is null || !child.IsVisible)
            {
                continue;
            }

            ResizeWidget currentResizer = ResizeWidget.GetOrCreateResizer(child);

            double childDesiredDepth = Orientation == Orientation.Horizontal ? child.DesiredSize.Width : child.DesiredSize.Height;
            double resizerDesiredHeightOrientated = Orientation == Orientation.Horizontal ? currentResizer.DesiredSize.Width : currentResizer.DesiredSize.Height;

            double controlSize = Math.Max(currentResizer.Size, 0);
            totalStackSize += controlSize + currentResizer.PositionOffset + resizerDesiredHeightOrientated;
            resizableSpaceBeforeControls[i] = (i == 0 ? 0 : resizableSpaceBeforeControls[i - 1]) + ControlResizingHarness.GetHarness(Orientation).GetResizableSpace(child);
        }

        double freeSpace = (Orientation == Orientation.Horizontal ? availableSpace.Width : availableSpace.Height) - totalStackSize;
        _totalStackSize = totalStackSize + ResizeMethod.SqueezeExpand.RunMethod(Children.CreateForwardsSlice(0), ControlResizingHarness.GetHarness(Orientation), freeSpace, resizableSpaceBeforeControls[^1]);
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
            resizer.PositionOffset += currentOffset - totalOffset;
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
            LogicalChildren.Add(resizer);
            VisualChildren.Add(resizer);
            resizer.BindProperties(TransitionDurationProperty, TransitionEasingProperty, OrientationProperty, this);

            if (resizer.Size == 0)
            {
                if (IsInStretchMode())
                {
                    resizer.SetSizeTo(_totalStackSize / Math.Max(totalItemsBeforeChange, 1), IsLoaded);
                }
                else
                {
                    resizer.Size = -1;
                }
            }

            addedSize += resizer.Size + (Orientation == Orientation.Horizontal ? resizer.DesiredSize.Width : resizer.DesiredSize.Height);
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
            removedSize += resizer.Size;
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
        => Orientation == Orientation.Horizontal ? HorizontalAlignment == HorizontalAlignment.Stretch : VerticalAlignment == VerticalAlignment.Stretch;

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
        if (!isActive)
        {
            resizer.IsVisible = false;
            return resizer.PositionOffset;
        }

        resizer.IsVisible = true;
        double resizerDepth = Orientation == Orientation.Horizontal ? resizer.DesiredSize.Width : resizer.DesiredSize.Height;
        resizer.Arrange(OrientedArrangeRect(currentDepth, resizerDepth, finalSize));

        return resizerDepth + resizer.PositionOffset;
    }

    private Rect OrientedArrangeRect(double depthStart, double depthSize, Size finalSize)
    {
        return Orientation == Orientation.Horizontal ? new Rect(depthStart, 0, depthSize, finalSize.Height) : new Rect(0, depthStart, finalSize.Width, depthSize);
    }
}
