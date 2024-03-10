using System.Collections.Specialized;
using System.Net.Security;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Reactive;
using Avalonia.VisualTree;
using Laminar.Avalonia.AdjustableStackPanel.ResizeLogic;

namespace Laminar.Avalonia.AdjustableStackPanel;

public class AdjustableStackPanel : StackPanel
{
    public static readonly StyledProperty<DoubleTransition> ResizerSizeTransitionProperty = AvaloniaProperty.Register<AdjustableStackPanel, DoubleTransition>("ResizerSizeTransition");
    public DoubleTransition ResizerSizeTransition
    {
        get => GetValue(ResizerSizeTransitionProperty);
        set => SetValue(ResizerSizeTransitionProperty, value);
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
                currentDepth += _originalResizer.DesiredSize.Height;
                _originalResizer.Arrange(new Rect(0, 0, finalSize.Width, _originalResizer.DesiredSize.Height));
            }

            for (int i = 0, count = children.Count; i < count; i++)
            {
                Control child = children[i];

                if (child is null || !child.IsVisible)
                {
                    continue;
                }

                ResizeWidget currentResizer = ResizeWidget.GetOrCreateResizer(child);
                child.Arrange(new Rect(0, currentDepth, finalSize.Width, currentResizer.Size));
                currentDepth += currentResizer.Size;

                if (CurrentStackResizeFlags().HasFlag(ResizeFlags.CanConsumeSpaceAfterStack) || i != count - 1)
                {
                    currentResizer.Arrange(new Rect(0, currentDepth, finalSize.Width, currentResizer.DesiredSize.Height));
                    currentDepth += currentResizer.DesiredSize.Height;
                }
            }

            return finalSize;
        }
        else
        {
            if (CurrentStackResizeFlags().HasFlag(ResizeFlags.CanConsumeSpaceBeforeStack))
            {
                currentDepth += _originalResizer.DesiredSize.Width;
                _originalResizer.Arrange(new Rect(0, 0, finalSize.Height, _originalResizer.DesiredSize.Width));
            }

            for (int i = 0, count = children.Count; i < count; i++)
            {
                Control child = children[i];

                if (child is null || !child.IsVisible)
                {
                    continue;
                }

                ResizeWidget currentResizer = ResizeWidget.GetOrCreateResizer(child);
                child.Arrange(new Rect(currentDepth, 0, currentResizer.Size, finalSize.Height));
                currentDepth += currentResizer.Size;

                if (CurrentStackResizeFlags().HasFlag(ResizeFlags.CanConsumeSpaceAfterStack) || i != count - 1)
                {
                    currentResizer.Arrange(new Rect(currentDepth, 0, currentResizer.DesiredSize.Width, finalSize.Height));
                    currentDepth += currentResizer.DesiredSize.Width;
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

            resizer.Size = Math.Max(resizer.Size, childDesiredSizeOriented.Height);
            spaceToExpandInto -= resizer.Size + resizerDesiredSizeOriented.Height;
            maximumStackDesiredWidth = Math.Max(maximumStackDesiredWidth, childDesiredSizeOriented.Width);
            totalResizableSpaces[i] = (i == 0 ? 0 : totalResizableSpaces[i - 1]) + resizer.Size - childDesiredSizeOriented.Height;
        }

        if (_currentResizeAmount.HasValue && _lastChangedResizerIndex.HasValue && ResizeGesture.TryGetGesture(currentHoverResizer?.Mode, ResizerModifier, out ResizeGesture gesture))
        {
            spaceToExpandInto -= gesture.Execute(Children, _lastChangedResizerIndex.Value, _currentResizeAmount.Value, totalResizableSpaces, spaceToExpandInto, ControlResizingHarness.GetHarness(Orientation), CurrentStackResizeFlags());
            _maximumStackSize = totalSlotSpace - spaceToExpandInto;
        }

        if (totalSlotSpace < _maximumStackSize || (spaceToExpandInto != 0 && !CurrentStackResizeFlags().HasFlag(ResizeFlags.CanConsumeSpaceAfterStack) && !CurrentStackResizeFlags().HasFlag(ResizeFlags.CanConsumeSpaceBeforeStack)))
        {
            spaceToExpandInto -= ResizeGesture.GetGesture(ResizerMode.ArrowBefore, ResizerModifier.None).Execute(Children, Children.Count - 1, spaceToExpandInto, totalResizableSpaces, spaceToExpandInto, ControlResizingHarness.GetHarness(Orientation), ResizeFlags.CanConsumeSpaceAfterStack);
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
        if (e.OldItems is not null)
        {
            ProcessItemsRemoved(e);
        }

        if (e.NewItems is not null)
        {
            ProcessItemsAdded(e);
        }

        int minimumStartingIndex = (e.NewStartingIndex == -1 || e.OldStartingIndex == -1) ? Math.Max(e.OldStartingIndex, e.NewStartingIndex) : Math.Min(e.OldStartingIndex, e.NewStartingIndex);
        for (int i = 0; i < Children.Count; i++)
        {
            int index = i;
            ResizerAtIndex(i).ModeAccessibleCheck = (mode) => ModeAccessibleCheck(mode, index);
        }
    }

    private void ProcessItemsAdded(NotifyCollectionChangedEventArgs e)
    {
        _sizeChangeInvalidatesMeasure = false;

        double addedSize = 0;
        int totalItemsBeforeChange = Children.Count - e.NewItems!.Count;
        int itemsBeforeAdded = e.NewStartingIndex;
        int itemsAfterAdded = Children.Count - (e.NewStartingIndex + e.NewItems!.Count);
        foreach (object item in e.NewItems!)
        {
            if (item is not Control addedControl)
            {
                continue;
            }

            ResizeWidget resizer = ResizeWidget.GetOrCreateResizer(addedControl);
            LogicalChildren.Add(resizer);
            VisualChildren.Add(resizer);

            if (resizer.Size == 0 && totalItemsBeforeChange > 0)
            {
                resizer.Size = (Orientation == Orientation.Horizontal ? DesiredSize.Width : DesiredSize.Height) / totalItemsBeforeChange;
            }

            addedSize += resizer.Size;
        }

        if (itemsBeforeAdded > 0)
        {
            ResizeMethod.SqueezeExpand.RunMethod(Children.CreateForwardsSlice(e.NewStartingIndex), ControlResizingHarness.GetHarness(Orientation), addedSize * itemsBeforeAdded / totalItemsBeforeChange, 0, 0);
        }
        if (itemsAfterAdded > 0)
        {
            ResizeMethod.SqueezeExpand.RunMethod(Children.CreateBackwardsSlice(e.NewStartingIndex + e.NewItems!.Count), ControlResizingHarness.GetHarness(Orientation), addedSize * itemsAfterAdded / totalItemsBeforeChange, 0, 0);
        }
    }

    private void ProcessItemsRemoved(NotifyCollectionChangedEventArgs e)
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
            resizer.ModeAccessibleCheck = null;
            LogicalChildren.Remove(resizer);
            VisualChildren.Remove(resizer);
        }

        int totalItemsAfterRemoval = Children.Count;
        int itemsBeforeRemoved = e.OldStartingIndex;
        int itemsAfterRemoved = Children.Count - e.OldStartingIndex;
        if (itemsBeforeRemoved > 0)
        {
            ResizeMethod.SqueezeExpand.RunMethod(Children.CreateBackwardsSlice(e.OldStartingIndex - 1), ControlResizingHarness.GetHarness(Orientation), (removedSize * itemsBeforeRemoved) / totalItemsAfterRemoval, 0, 0);
        }
        if (itemsAfterRemoved > 0)
        {
            ResizeMethod.SqueezeExpand.RunMethod(Children.CreateForwardsSlice(e.OldStartingIndex + e.OldItems!.Count - 1), ControlResizingHarness.GetHarness(Orientation), (removedSize * itemsAfterRemoved) / totalItemsAfterRemoval, 0, 0);
        }
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

    private ResizeWidget ResizerAtIndex(int index)
    {
        if (index == -1)
        {
            return _originalResizer;
        }

        return ResizeWidget.GetOrCreateResizer(Children[index]);
    }
}
