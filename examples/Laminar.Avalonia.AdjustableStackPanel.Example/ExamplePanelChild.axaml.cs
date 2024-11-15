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
        double size = resizer.Size;
        bool isEnabled = resizer.CanChangeSize;

        CanChangeSize = false;

        resizer.SetSizeTo(0, true);
        Opacity = 0.0;
        await Task.Delay(2000);

        Opacity = 1.0;
        resizer.SetSizeTo(size, true);
        await Task.Delay((int)panel.TransitionDuration.TotalMilliseconds);

        CanChangeSize = isEnabled;
        Transitions.Remove(opacityTransition);
    }

    public void SetSizeTo400()
    {
        ResizeWidget.SetResizerTargetSize(this, 400);
    }
}
