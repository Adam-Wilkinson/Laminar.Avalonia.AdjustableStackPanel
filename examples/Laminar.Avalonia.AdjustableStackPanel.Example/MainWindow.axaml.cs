using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Themes.Fluent;

namespace Laminar.Avalonia.AdjustableStackPanel.Example;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        horizontalAlignmentSelector.ItemsSource = (HorizontalAlignment[])typeof(HorizontalAlignment).GetEnumValues();
        horizontalAlignmentSelector.SelectedItem = AdjustableStackPanel.HorizontalAlignment;

        verticalAlignmentSelector.ItemsSource = (VerticalAlignment[])typeof(VerticalAlignment).GetEnumValues();
        verticalAlignmentSelector.SelectedItem = AdjustableStackPanel.VerticalAlignment;

        orientationSelector.ItemsSource = (Orientation[])typeof(Orientation).GetEnumValues();
        orientationSelector.SelectedItem = AdjustableStackPanel.Orientation;
        animationDurationSelector.Value = AdjustableStackPanel.TransitionDuration.TotalMilliseconds;
    }

    private void AnimationDurationChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        AdjustableStackPanel.TransitionDuration = TimeSpan.FromMilliseconds(e.NewValue);
    }
}
