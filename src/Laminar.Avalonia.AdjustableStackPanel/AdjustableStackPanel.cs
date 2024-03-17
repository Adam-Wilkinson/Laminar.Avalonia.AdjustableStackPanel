using System.Collections.Specialized;
using System.Runtime.Intrinsics.Arm;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Platform;
using Avalonia.Reactive;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Animations;
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

    private double _maximumStackSize = 0;
    private int? _lastChangedResizerIndex = null;
    private double? _currentResizeAmount = null;
    private ResizeWidget? _lastChangedResizer = null;
    private ResizerModifier ResizerModifier = ResizerModifier.None;
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
        _originalResizer.Orientation = Orientation;
        LogicalChildren.Add(_originalResizer);
        VisualChildren.Add(_originalResizer);
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

        if (newResizerModifier != ResizerModifier || _modeChanging)
        {
            ResizerModifier = newResizerModifier;
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

        foreach ((int currentResizerIndex, Resize resize) in ResizeGesture.GetGesture(_lastChangedResizer?.Mode, ResizerModifier).AccessibleResizes(Children, _lastChangedResizerIndex!.Value))
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
        double currentDepth = 0.0;

        if (Orientation == Orientation.Vertical)
        {
            if (CurrentStackResizeFlags().HasFlag(ResizeFlags.CanConsumeSpaceBeforeStack))
            {
                _originalResizer.IsVisible = true;
                currentDepth += _originalResizer.DesiredSize.Height;
                _originalResizer.Arrange(new Rect(0, 0, finalSize.Width, _originalResizer.DesiredSize.Height));
                currentDepth += _originalResizer.OffsetAnimator.PositionOffsetAfter;
            }
            else
            {
                _originalResizer.IsVisible = false;
            }

            for (int i = 0, count = children.Count; i < count; i++)
            {
                Control child = children[i];

                if (child is null || !child.IsVisible)
                {
                    continue;
                }

                ResizeWidget currentResizer = ResizeWidget.GetOrCreateResizer(child);
                double controlSize = Math.Max(0, currentResizer.Size + currentResizer.OffsetAnimator.SizeOffset);
                child.Arrange(new Rect(0, currentDepth, finalSize.Width, controlSize));
                currentDepth += controlSize;

                if (i == count - 1 && !CurrentStackResizeFlags().HasFlag(ResizeFlags.CanConsumeSpaceAfterStack))
                {
                    currentResizer.IsVisible = false;
                }
                else
                {
                    currentResizer.IsVisible = child.IsVisible;
                    currentResizer.Arrange(new Rect(0, currentDepth, finalSize.Width, currentResizer.DesiredSize.Height));
                    currentDepth += currentResizer.DesiredSize.Height + currentResizer.OffsetAnimator.PositionOffsetAfter;
                }
            }

            return finalSize;
        }
        else
        {
            if (CurrentStackResizeFlags().HasFlag(ResizeFlags.CanConsumeSpaceBeforeStack))
            {
                _originalResizer.IsVisible = true;
                currentDepth += _originalResizer.DesiredSize.Width;
                _originalResizer.Arrange(new Rect(0, 0, finalSize.Height, _originalResizer.DesiredSize.Width));
                currentDepth += _originalResizer.OffsetAnimator.PositionOffsetAfter;
            }
            else
            {
                _originalResizer.IsVisible = false;
            }

            for (int i = 0, count = children.Count; i < count; i++)
            {
                Control child = children[i];

                if (child is null || !child.IsVisible)
                {
                    continue;
                }

                ResizeWidget currentResizer = ResizeWidget.GetOrCreateResizer(child);
                double controlSize = Math.Max(0, currentResizer.Size + currentResizer.OffsetAnimator.SizeOffset);
                child.Arrange(new Rect(currentDepth, 0, controlSize, finalSize.Height));
                currentDepth += controlSize;

                if (i == count - 1 && !CurrentStackResizeFlags().HasFlag(ResizeFlags.CanConsumeSpaceAfterStack))
                {
                    currentResizer.IsVisible = false;
                }
                else
                {
                    currentResizer.IsVisible = child.IsVisible;
                    currentResizer.Arrange(new Rect(currentDepth, 0, currentResizer.DesiredSize.Width, finalSize.Height));
                    currentDepth += currentResizer.DesiredSize.Width + currentResizer.OffsetAnimator.PositionOffsetAfter;
                }
            }

            return finalSize;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _sizeChangeInvalidatesMeasure = false;
        Controls children = Children;
        ResizeWidget? currentHoverResizer = _lastChangedResizer;
        bool isHorizontal = Orientation == Orientation.Horizontal;
        double totalSlotSpace = isHorizontal ? availableSize.Width : availableSize.Height;
        double spaceToExpandInto = totalSlotSpace;
        double maximumStackDesiredWidth = 0.0;
        Span<double> totalResizableSpaces = stackalloc double[children.Count];

        if (CurrentStackResizeFlags().HasFlag(ResizeFlags.CanConsumeSpaceBeforeStack))
        {
            _originalResizer.Measure(availableSize);
            spaceToExpandInto -= isHorizontal ? _originalResizer.DesiredSize.Width : _originalResizer.DesiredSize.Height;
        }

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
                resizer.OffsetAnimator.ChangeSizeOffset(-childDesiredSizeOriented.Height);
            }

            resizer.Size = Math.Max(resizer.Size, childDesiredSizeOriented.Height);
            double controlSize = resizer.Size + resizer.OffsetAnimator.SizeOffset;
            spaceToExpandInto -= controlSize + resizerDesiredSizeOriented.Height + resizer.OffsetAnimator.PositionOffsetAfter;
            maximumStackDesiredWidth = Math.Max(maximumStackDesiredWidth, childDesiredSizeOriented.Width);
            totalResizableSpaces[i] = (i == 0 ? 0 : totalResizableSpaces[i - 1]) + controlSize - childDesiredSizeOriented.Height;
        }

        if (_currentResizeAmount.HasValue && _lastChangedResizerIndex.HasValue && ResizeGesture.TryGetGesture(currentHoverResizer?.Mode, ResizerModifier, out ResizeGesture gesture))
        {
            spaceToExpandInto -= gesture.Execute(Children, _lastChangedResizerIndex.Value, _currentResizeAmount.Value, totalResizableSpaces, spaceToExpandInto, ControlResizingHarness.GetHarness(Orientation), CurrentStackResizeFlags());
            _maximumStackSize = totalSlotSpace - spaceToExpandInto;
        }

        if (totalSlotSpace < _maximumStackSize || (spaceToExpandInto != 0 && !CanChangeStackSize()))
        {
            double resizeAmountToFill = spaceToExpandInto + (isHorizontal ? ResizerAtIndex(children.Count - 1).DesiredSize.Width : ResizerAtIndex(children.Count - 1).DesiredSize.Height);
            spaceToExpandInto -= ResizeGesture.GetGesture(ResizerMode.ArrowBefore, ResizerModifier.None).Execute(Children, Children.Count - 1, resizeAmountToFill, totalResizableSpaces, spaceToExpandInto, ControlResizingHarness.GetHarness(Orientation), ResizeFlags.CanConsumeSpaceAfterStack);
        }

        if (spaceToExpandInto > 0)
        {
            _maximumStackSize = totalSlotSpace - spaceToExpandInto;
        }

        _currentResizeAmount = null;
        _sizeChangeInvalidatesMeasure = true;
        return isHorizontal ? new Size(availableSize.Width - spaceToExpandInto, maximumStackDesiredWidth) : new Size(maximumStackDesiredWidth, availableSize.Height - spaceToExpandInto);
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
            LogicalChildren.Add(resizer);
            VisualChildren.Add(resizer);
            resizer.OffsetAnimator.BindTransitionProperties(TransitionDurationProperty, TransitionEasingProperty, this);

            if (resizer.Size == 0 && totalItemsBeforeChange > 0 && !CanChangeStackSize())
            {
                resizer.Size = (Orientation == Orientation.Horizontal ? DesiredSize.Width : DesiredSize.Height) / Math.Max(totalItemsBeforeChange, 1);
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
                HorizontalAlignment.Left => ResizeFlags.CanConsumeSpaceAfterStack,
                HorizontalAlignment.Right => ResizeFlags.CanConsumeSpaceBeforeStack,
                HorizontalAlignment.Center => ResizeFlags.CanConsumeSpaceAfterStack | ResizeFlags.CanConsumeSpaceBeforeStack,
                _ => ResizeFlags.None,
            };
        }
        else
        {
            return VerticalAlignment switch
            {
                VerticalAlignment.Top => ResizeFlags.CanConsumeSpaceAfterStack,
                VerticalAlignment.Bottom => ResizeFlags.CanConsumeSpaceBeforeStack,
                VerticalAlignment.Center => ResizeFlags.CanConsumeSpaceAfterStack | ResizeFlags.CanConsumeSpaceBeforeStack,
                _ => ResizeFlags.None,
            };
        }
    }

    private bool CanChangeStackSize()
    {
        ResizeFlags flags = CurrentStackResizeFlags();
        return flags.HasFlag(ResizeFlags.CanConsumeSpaceBeforeStack) || flags.HasFlag(ResizeFlags.CanConsumeSpaceAfterStack);
    }

    private ResizeWidget ResizerAtIndex(int index)
    {
        if (index == -1)
        {
            return _originalResizer;
        }

        return ResizeWidget.GetOrCreateResizer(Children[index]);
    }
}
