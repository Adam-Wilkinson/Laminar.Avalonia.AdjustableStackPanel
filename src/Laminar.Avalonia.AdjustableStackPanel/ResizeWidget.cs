using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.Styling;
using Laminar.Avalonia.AdjustableStackPanel.ResizeLogic;

namespace Laminar.Avalonia.AdjustableStackPanel;

public class ResizeWidget : TemplatedControl
{
    public static readonly StyledProperty<Orientation> OrientationProperty = AvaloniaProperty.Register<ResizeWidget, Orientation>(nameof(Orientation));

    public static readonly DirectProperty<ResizeWidget, double> SizeProperty = AvaloniaProperty.RegisterDirect<ResizeWidget, double>(nameof(Size), r => r.Size, (r, v) => r._size = v, double.NaN);

    public static readonly DirectProperty<ResizeWidget, ResizerMode?> ModeProperty = AvaloniaProperty.RegisterDirect<ResizeWidget, ResizerMode?>(nameof(Mode), r => r._mode, (r, v) => r._mode = v);

    public static readonly AttachedProperty<ResizeWidget?> ResizeWidgetProperty = AvaloniaProperty.RegisterAttached<ResizeWidget, Control, ResizeWidget?>("ResizeWidget");

    public static readonly RoutedEvent<ResizeEventArgs> ResizeEvent = RoutedEvent.Register<ResizeWidget, ResizeEventArgs>(nameof(Resize), RoutingStrategies.Tunnel | RoutingStrategies.Bubble);

    public static ResizeWidget? GetResizeWidget(Control control) => control.GetValue(ResizeWidgetProperty);
    public static void SetResizeWidget(Control control, ResizeWidget? resizeWidget) => control.SetValue(ResizeWidgetProperty, resizeWidget);

    private readonly RenderOffsetAnimator _offsetAnimator = new();
    private ResizerMode? _mode;
    private double _size = double.NaN;
    private Point? _originalClickPoint = null;
    private string? _currentModePseudoclass = null;

    public event EventHandler<ResizeEventArgs> Resize
    {
        add => AddHandler(ResizeEvent, value);
        remove => RemoveHandler(ResizeEvent, value);
    }

    static ResizeWidget()
    {
        ModeProperty.Changed.AddClassHandler<ResizeWidget>((widget, _) => widget.ModeChanged());

        // Load the themes manually
        Uri? nullUri = null;
        ResourceInclude themes = new(nullUri)
        {
            Source = new Uri("avares://Laminar.Avalonia.AdjustableStackpanel/ResizeWidgetThemes.axaml"),
        };

        Application.Current?.Resources.MergedDictionaries.Add(themes);
    }

    public ResizeWidget()
    {
        Resize += OnResize;
    }

    public Func<ResizerMode, bool>? ModeAccessibleCheck { get; set; } = null;

    public double Size
    {
        get => _size + _offsetAnimator.SizeOffset;
        set => SetAndRaise(SizeProperty, ref _size, value);
    }

    public double TargetSize => _size;

    public ResizerMode? Mode
    {
        get => _mode;
        set => SetAndRaise(ModeProperty, ref _mode, value);
    }

    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public double PositionOffset
    {
        get => _offsetAnimator.PositionOffsetAfter;
        set => _offsetAnimator.ChangePositionOffset(value);
    }

    public void SetSizeTo(double newSize, bool animate)
    {
        if (animate)
        {
            double sizeChange = newSize - Size;
            Size = newSize;
            _offsetAnimator.ChangeSizeOffset(-sizeChange);
        }
        else
        {
            Size = newSize;
        }
    }

    public void BindProperties(AvaloniaProperty<TimeSpan> durationProperty, AvaloniaProperty<Easing> easingProperty, AvaloniaProperty<Orientation> orientationProperty, Layoutable layoutableOwner)
    {
        this[!OrientationProperty] = layoutableOwner[!orientationProperty];
        _offsetAnimator.BindTransitionProperties(durationProperty, easingProperty, layoutableOwner);
    }

    public void ShowAccessibleModes()
    {
        foreach (ResizerMode mode in GetAccessibleModes())
        {
            PseudoClasses.Add(mode.IsAccessiblePseudoclass);
        }
    }

    public void HideAccessibleModes()
    {
        foreach (ResizerMode mode in ResizerMode.All.Values)
        {
            PseudoClasses.Remove(mode.IsAccessiblePseudoclass);
        }
    }

    public static ResizeWidget GetOrCreateResizer(Control control)
    {
        if (GetResizeWidget(control) is ResizeWidget widget)
        {
            return widget;
        }

        ResizeWidget newResizer = new();
        SetResizeWidget(control, newResizer);

        return newResizer;
    }

    protected void OnResize(object? sender, ResizeEventArgs e)
    {
        if (e.Handled) return;

        Size += e.ResizeAmount;
        e.Handled = true;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        foreach (var (name, mode) in ResizerMode.All) 
        {
            RegisterModeSwitchOnChildHover(e, name, mode);
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        ShowAccessibleModes();
        base.OnPointerEntered(e);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        HideAccessibleModes();
        Mode = null;
        base.OnPointerExited(e);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        _originalClickPoint = e.GetPosition(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (_originalClickPoint is null || Mode is null)
        {
            return;
        }

        Point deltaXY = e.GetPosition(this) - _originalClickPoint.Value;
        RaiseEvent(new ResizeEventArgs(
            ResizeEvent,
            Orientation == Orientation.Vertical ? deltaXY.Y : deltaXY.X,
            Mode.Value, 
            this));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (_originalClickPoint is null)
        {
            return;
        }

        _originalClickPoint = null;
        e.Handled = true;
    }

    private void RegisterModeSwitchOnChildHover(TemplateAppliedEventArgs e, string childName, ResizerMode mode)
    {
        Control? child = e.NameScope.Find<Control>(childName);

        if (child is not null)
        {
            child.PointerEntered += (_, _) =>
            {
                if (ModeAccessibleCheck is null || ModeAccessibleCheck(mode))
                {
                    Mode = mode;
                }
            };
        }
    }

    private void ModeChanged()
    {
        if (_currentModePseudoclass is not null)
        {
            PseudoClasses.Remove(_currentModePseudoclass);
        }

        if (Mode is not null)
        {
            _currentModePseudoclass = Mode.Value.IsActivePseudoclass;
            PseudoClasses.Add(_currentModePseudoclass);
        }
    }

    private IEnumerable<ResizerMode> GetAccessibleModes() => ModeAccessibleCheck is null ? ResizerMode.All.Values : ResizerMode.All.Values.Where(mode => ModeAccessibleCheck(mode));
}
