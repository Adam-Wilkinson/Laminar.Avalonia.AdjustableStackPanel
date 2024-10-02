using System;
using System.Security.Cryptography;
using Avalonia.Controls;
using Avalonia.Media;

namespace Laminar.Avalonia.AdjustableStackPanel.Example;

public partial class ExamplePanelChild : UserControl
{
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
}
