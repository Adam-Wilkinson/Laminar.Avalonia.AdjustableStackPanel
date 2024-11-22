using System.Diagnostics;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Reactive;
using Laminar.Avalonia.AdjustableStackPanel.ResizeLogic;

namespace Laminar.Avalonia.AdjustableStackPanel;

public class ResizeWidget : TemplatedControl
{
    private static readonly Dictionary<ResizeWidget, Control> ResizerControls = [];
    
    public static readonly StyledProperty<Orientation> OrientationProperty = AvaloniaProperty.Register<ResizeWidget, Orientation>(nameof(Orientation));

    public static readonly StyledProperty<bool> CanChangeSizeProperty = AvaloniaProperty.Register<ResizeWidget, bool>(nameof(CanChangeSize), defaultValue: true);

    public static readonly DirectProperty<ResizeWidget, ResizerMode?> ModeProperty = AvaloniaProperty.RegisterDirect<ResizeWidget, ResizerMode?>(nameof(Mode), r => r._mode, (r, v) => r._mode = v);

    public static readonly AttachedProperty<ResizeWidget?> ResizeWidgetProperty = AvaloniaProperty.RegisterAttached<ResizeWidget, Control, ResizeWidget?>("ResizeWidget");
    
    public static readonly AttachedProperty<double> TargetSizeProperty = AvaloniaProperty.RegisterAttached<ResizeWidget, Control, double>("TargetSize", defaultValue: double.NaN);
    public static double GetTargetSize(Control control) => control.GetValue(TargetSizeProperty);
    public static void SetTargetSize(Control control, double value) => control.SetValue(TargetSizeProperty, value);

    public static readonly AttachedProperty<double> AnimatedSizeProperty = AvaloniaProperty.RegisterAttached<ResizeWidget, Control, double>("AnimatedSize", defaultValue: double.NaN);
    public static double GetAnimatedSize(Control control) => control.GetValue(AnimatedSizeProperty);
    public static void SetAnimatedSize(Control control, double value) => control.SetValue(AnimatedSizeProperty, value);

    public static readonly RoutedEvent<ResizeEventArgs> ResizeEvent = RoutedEvent.Register<ResizeWidget, ResizeEventArgs>(nameof(Resize), RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
    public static ResizeWidget? GetResizeWidget(Control control) => control.GetValue(ResizeWidgetProperty);
    public static void SetResizeWidget(Control control, ResizeWidget? resizeWidget) => control.SetValue(ResizeWidgetProperty, resizeWidget);

    private readonly RenderOffsetAnimator _offsetAnimator = new();
    private ResizerMode? _mode;
    private Point? _lastClickPoint;
    private string? _currentModePseudoclass;
    private bool _sizeChanging;

    public event EventHandler<ResizeEventArgs> Resize
    {
        add => AddHandler(ResizeEvent, value);
        remove => RemoveHandler(ResizeEvent, value);
    }

    static ResizeWidget()
    {
        ModeProperty.Changed.AddClassHandler<ResizeWidget>((widget, _) => widget.ModeChanged());
        TargetSizeProperty.Changed.AddClassHandler<Control>((control, e) => GetOrCreateResizer(control).TargetSizeChanged(e));
        AnimatedSizeProperty.Changed.AddClassHandler<Control>((control, e) => GetOrCreateResizer(control).AnimatedSizeChanged(e));
        // Load the themes manually
        Uri? nullUri = null;
        ResourceInclude themes = new(nullUri)
        {
            Source = new Uri("avares://Laminar.Avalonia.AdjustableStackpanel/ResizeWidgetThemes.axaml"),
        };

        Application.Current?.Resources.MergedDictionaries.Add(themes);
    }

    private ResizeWidget()
    {
        Resize += OnResize;
        _offsetAnimator.GetPropertyChangedObservable(RenderOffsetAnimator.SizeOffsetProperty).Subscribe(
            new AnonymousObserver<AvaloniaPropertyChangedEventArgs>(SizeOffsetChanged));
    }

    public Func<ResizerMode, bool>? ModeAccessibleCheck { get; set; }

    public double AnimatedSize
    {
        get => GetAnimatedSize(ResizerControls[this]);
        set => SetAnimatedSize(ResizerControls[this], value);
    }

    public double TargetSize
    {
        get => GetTargetSize(ResizerControls[this]);
        set => SetTargetSize(ResizerControls[this], value);
    }

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

    public bool CanChangeSize
    {
        get => GetValue(CanChangeSizeProperty);
        set => SetValue(CanChangeSizeProperty, value);
    }

    public double PositionOffset
    {
        get => _offsetAnimator.PositionOffsetAfter;
        set => _offsetAnimator.ChangePositionOffset(value);
    }

    
    private void AnimatedSizeChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is not double newAnimatedSize || _sizeChanging) return;

        _sizeChanging = true;
        TargetSize = newAnimatedSize;
        _sizeChanging = false;
    }

    private void TargetSizeChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is not double newTargetSize || _sizeChanging) return;
        
        _sizeChanging = true;
        var deltaSize = (e.OldValue is not double oldTargetSize || double.IsNaN(oldTargetSize) ? 0 : oldTargetSize) - newTargetSize;
        if (deltaSize == 0)
        {
            AnimatedSize = newTargetSize;
        }
        else
        {
            _offsetAnimator.ChangeSizeOffset(deltaSize);
        }
        _sizeChanging = false;
    }
    
    private void SizeOffsetChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is not double || _sizeChanging) return;

        _sizeChanging = true;
        AnimatedSize = TargetSize + _offsetAnimator.SizeOffset;
        _sizeChanging = false;
    }
    
    public void SetSizeTo(double newSize, bool animate)
    {
        if (animate)
        {
            TargetSize = newSize;
        }
        else
        {
            AnimatedSize = newSize;
        }
    }

    public void BindProperties(AvaloniaProperty<TimeSpan> durationProperty, AvaloniaProperty<Easing> easingProperty, AvaloniaProperty<Orientation> orientationProperty, Layoutable layoutableOwner)
    {
        this[!OrientationProperty] = layoutableOwner[!orientationProperty];
        _offsetAnimator.BindTransitionProperties(durationProperty, easingProperty, layoutableOwner);
    }

    public void ShowAccessibleModes()
    {
        foreach (var mode in GetAccessibleModes())
        {
            PseudoClasses.Add(mode.IsAccessiblePseudoclass);
        }
    }

    public void HideAccessibleModes()
    {
        foreach (var mode in ResizerMode.All.Values)
        {
            PseudoClasses.Remove(mode.IsAccessiblePseudoclass);
        }
    }

    public static ResizeWidget GetOrCreateResizer(Control control, AdjustableStackPanel? parent = null)
    {
        if (GetResizeWidget(control) is { } widget)
        {
            return widget;
        }

        ResizeWidget newResizer = new();
        ResizerControls.Add(newResizer, control);
        SetResizeWidget(control, newResizer);
        if (parent is not null)
        {
            newResizer.BindProperties(AdjustableStackPanel.TransitionDurationProperty,
                AdjustableStackPanel.TransitionEasingProperty, StackPanel.OrientationProperty, parent);
        }

        return newResizer;
    }

    protected void OnResize(object? sender, ResizeEventArgs e)
    {
        if (e.Handled) return;

        AnimatedSize += e.ResizeAmount;
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
        _lastClickPoint = e.GetPosition(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (_lastClickPoint is null || Mode is null)
        {
            return;
        }

        var delta = e.GetPosition(this) - _lastClickPoint.Value;
        RaiseEvent(new ResizeEventArgs(
            ResizeEvent,
            Orientation == Orientation.Vertical ? delta.Y : delta.X,
            Mode.Value, 
            this));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (_lastClickPoint is null) return;

        _lastClickPoint = null;
        e.Handled = true;
    }

    private void RegisterModeSwitchOnChildHover(TemplateAppliedEventArgs e, string childName, ResizerMode mode)
    {
        var child = e.NameScope.Find<Control>(childName);

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
