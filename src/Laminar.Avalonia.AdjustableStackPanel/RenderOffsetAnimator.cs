using System.Diagnostics;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Layout;
using Avalonia.Reactive;

namespace Laminar.Avalonia.AdjustableStackPanel;

public class RenderOffsetAnimator : Animatable
{
    public static readonly StyledProperty<double> SizeOffsetProperty = AvaloniaProperty.Register<RenderOffsetAnimator, double>("SizeOffset");
    public double SizeOffset
    {
        get => GetValue(SizeOffsetProperty);
        set => SetValue(SizeOffsetProperty, value);
    }

    public static readonly StyledProperty<double> PositionOffsetProperty = AvaloniaProperty.Register<RenderOffsetAnimator, double>("PositionOffset");
    public double PositionOffsetAfter
    {
        get => GetValue(PositionOffsetProperty);
        set => SetValue(PositionOffsetProperty, value);
    }

    private DoubleTransition _positionTransition = new() { Property = PositionOffsetProperty };
    private DoubleTransition _sizeTransition = new() { Property = SizeOffsetProperty };

    private IDisposable? _durationObservable;
    private IDisposable? _easingObservable;
    private IDisposable? _sizeOffsetChangedObservable;
    private IDisposable? _positionOffsetChangedObservable;

    private Easing _easing = new LinearEasing();
    private TimeSpan _duration = new();

    public void BindTransitionProperties(AvaloniaProperty<TimeSpan> durationProperty, AvaloniaProperty<Easing> easingProperty, Layoutable layoutableOwner)
    {
        _durationObservable?.Dispose();
        _durationObservable = layoutableOwner.GetObservable(durationProperty).Subscribe(new AnonymousObserver<TimeSpan>(x =>
        {
            _duration = x;
            UpdateTransitions();
        }));

        _easingObservable?.Dispose();
        _easingObservable = layoutableOwner.GetObservable(easingProperty).Subscribe(new AnonymousObserver<Easing>(x =>
        {
            _easing = x;
            UpdateTransitions();
        }));

        _sizeOffsetChangedObservable?.Dispose();
        _sizeOffsetChangedObservable = this.GetObservable(SizeOffsetProperty).Subscribe(new AnonymousObserver<double>(_ => layoutableOwner.InvalidateMeasure()));

        _positionOffsetChangedObservable?.Dispose();
        _positionOffsetChangedObservable = this.GetObservable(PositionOffsetProperty).Subscribe(new AnonymousObserver<double>(_ => layoutableOwner.InvalidateMeasure()));

        UpdateTransitions();
    }

    public void ChangePositionOffset(double offsetChange)
    {
        Transitions ??= [];
        PositionOffsetAfter = PositionOffsetAfter;
        Transitions!.Remove(_positionTransition);
        PositionOffsetAfter += offsetChange;
        Transitions!.Add(_positionTransition);
        PositionOffsetAfter = 0;
    }

    public void ChangeSizeOffset(double sizeChange)
    {
        Transitions ??= [];
        SizeOffset = SizeOffset;
        Transitions!.Remove(_sizeTransition);
        SizeOffset += sizeChange;
        Transitions!.Add(_sizeTransition);
        SizeOffset = 0;
    }

    public void UpdateTransitions()
    {
        _positionTransition = new() { Property = PositionOffsetProperty, Duration = _duration, Easing = _easing };
        _sizeTransition = new() { Property = SizeOffsetProperty, Duration = _duration, Easing = _easing };
    }
}
