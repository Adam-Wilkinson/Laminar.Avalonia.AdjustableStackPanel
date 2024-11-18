using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Laminar.Avalonia.AdjustableStackPanel.Example;

public partial class ExamplePanelChild : UserControl
{
    public bool CanChangeSize
    {
        get => ResizeWidget.GetOrCreateResizer(this).CanChangeSize;
        set => ResizeWidget.GetOrCreateResizer(this).CanChangeSize = value;
    }

    public double ResizerTargetSize { get; set; } = 250;

    public ExamplePanelChild()
    {
        InitializeComponent();
        DataContext = this;
        Span<byte> rgb = stackalloc byte[3];
        Random.Shared.NextBytes(rgb);
        Background = new SolidColorBrush(new Color(10, rgb[0], rgb[1], rgb[2]));
    }

    public void InsertExampleBefore()
    {
        if (Parent is not AdjustableStackPanel panel)
        {
            return;
        }

        panel.Children.Insert(panel.Children.IndexOf(this), new ExamplePanelChild());
    }

    public void InsertExampleAfter()
    {
        if (Parent is not AdjustableStackPanel panel)
        {
            return;
        }

        panel.Children.Insert(panel.Children.IndexOf(this) + 1, new ExamplePanelChild());
    }

    public void RemoveSelf() => (Parent as AdjustableStackPanel)?.Children.Remove(this);

    public async void Hide()
    {
        if (Parent is not AdjustableStackPanel panel)
        {
            return;
        }

        DoubleTransition opacityTransition = new() { Property = OpacityProperty, Duration = panel.TransitionDuration, Easing = panel.TransitionEasing };
        ResizeWidget resizer = ResizeWidget.GetOrCreateResizer(this);
        Transitions ??= [];
        Transitions.Add(opacityTransition);
        double size = ResizeWidget.GetTargetSize(this);
        bool isEnabled = ResizeWidget.GetOrCreateResizer(resizer).CanChangeSize;

        CanChangeSize = false;

        ResizeWidget.SetTargetSize(this, 0);
        Opacity = 0.0;
        await Task.Delay(2000);

        Opacity = 1.0;
        ResizeWidget.SetTargetSize(this, size);
        await Task.Delay((int)panel.TransitionDuration.TotalMilliseconds);

        CanChangeSize = isEnabled;
        Transitions.Remove(opacityTransition);
    }

    public void SetSizeTo400()
    {
        ResizeWidget.SetTargetSize(this, 400);
    }
}
