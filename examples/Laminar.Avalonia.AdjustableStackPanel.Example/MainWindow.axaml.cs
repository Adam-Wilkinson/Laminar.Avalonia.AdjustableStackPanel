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
        horizontalAlignmentSelector.SelectedItem = AdjustableStackPanelExample.HorizontalAlignment;

        verticalAlignmentSelector.ItemsSource = (VerticalAlignment[])typeof(VerticalAlignment).GetEnumValues();
        verticalAlignmentSelector.SelectedItem = AdjustableStackPanelExample.VerticalAlignment;

        orientationSelector.ItemsSource = (Orientation[])typeof(Orientation).GetEnumValues();
        orientationSelector.SelectedItem = AdjustableStackPanelExample.Orientation;
        animationDurationSelector.Value = AdjustableStackPanelExample.TransitionDuration.TotalMilliseconds;
    }

    private void AnimationDurationChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        AdjustableStackPanelExample.TransitionDuration = TimeSpan.FromMilliseconds(e.NewValue);
    }
}
