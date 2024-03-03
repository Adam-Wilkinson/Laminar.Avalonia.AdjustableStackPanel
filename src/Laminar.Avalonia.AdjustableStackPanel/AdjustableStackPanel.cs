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
    private bool _measureChanging = false;

    private readonly ResizeWidget _originalResizer = new();

    static AdjustableStackPanel()
    {
        ResizeWidget.SizeProperty.Changed.Subscribe(new AnonymousObserver<AvaloniaPropertyChangedEventArgs<double>>(ResizeWidgetSizeChanged));
        ResizeWidget.ModeProperty.Changed.Subscribe(new AnonymousObserver<AvaloniaPropertyChangedEventArgs<ResizerMode>>(ResizeWidgetModeChanged));
    }

    private static void ResizeWidgetSizeChanged(AvaloniaPropertyChangedEventArgs<double> e)
    {
        if (e.Sender is not ResizeWidget resizer || resizer.GetVisualParent() is not AdjustableStackPanel adjustableStackPanel || adjustableStackPanel._measureChanging)
        {
            return;
        }

        adjustableStackPanel._measureChanging = true;
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
        Children.Events().ItemAdded += ChildAdded;
        Children.Events().ItemRemoved += ChildRemoved;
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
        _measureChanging = true;
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
        _measureChanging = false;
        return isHorizontal ? new Size(availableSize.Width - spaceToExpandInto, maximumStackDesiredWidth) : new Size(maximumStackDesiredWidth, availableSize.Height - spaceToExpandInto);
    }

    private void ChildAdded(object? sender, ItemAddedEventArgs<Control> childAddedArgs)
    {
        ResizeWidget resizer = ResizeWidget.GetOrCreateResizer(childAddedArgs.Item);
        resizer.Orientation = Orientation;
        LogicalChildren.Add(resizer);
        VisualChildren.Add(resizer);
        for (int i = childAddedArgs.Index; i < Children.Count; i++)
        {
            ResizerAtIndex(i).ModeAccessibleCheck = (mode) => ModeAccessibleCheck(mode, i - 1);
        }

        if (resizer.Size == 0 && Children.Count > childAddedArgs.TotalItemsAdded)
        {
            _measureChanging = true;
            resizer.Size = (Orientation == Orientation.Horizontal ? DesiredSize.Width : DesiredSize.Height) / (Children.Count - childAddedArgs.TotalItemsAdded);
        }
    }

    private void ChildRemoved(object? sender, ItemRemovedEventArgs<Control> childRemovedArgs)
    {
        ResizeWidget resizer = ResizeWidget.GetOrCreateResizer(childRemovedArgs.Item);
        resizer.ModeAccessibleCheck = null;
        LogicalChildren.Remove(resizer);
        VisualChildren.Remove(resizer);
        for (int i = childRemovedArgs.Index; i < Children.Count; i++)
        {
            ResizerAtIndex(i).ModeAccessibleCheck = (mode) => ModeAccessibleCheck(mode, i);
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
