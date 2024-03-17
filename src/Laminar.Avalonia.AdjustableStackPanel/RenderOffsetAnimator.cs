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
    public double PositionOffset
    {
        get => GetValue(PositionOffsetProperty);
        set => SetValue(PositionOffsetProperty, value);
    }

    private IDisposable? _durationObservable;
    private IDisposable? _easingObservable;
    private IDisposable? _sizeOffsetChangedObservable;
    private IDisposable? _positionOffsetChangedObservable;

    private Easing _easing = new LinearEasing();
    private TimeSpan _duration = new();
    private bool _transitionsEnabled = false;

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

    public void UpdateTransitions()
    {
        if (_transitionsEnabled)
        {
            EnableTransitions();
        }
        else
        {
            DisableTransitions();
        }
    }

    public void DisableTransitions()
    {
        Transitions = null;
        _transitionsEnabled = false;
    }


    public void EnableTransitions()
    {
        Transitions = [
            new DoubleTransition { Property = SizeOffsetProperty, Duration = _duration, Easing = _easing },
            new DoubleTransition { Property = PositionOffsetProperty, Duration = _duration, Easing = _easing }];

        _transitionsEnabled = true;
    }
}
