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
        HorizontalAlignmentSelector.ItemsSource = (HorizontalAlignment[])typeof(HorizontalAlignment).GetEnumValues();
        HorizontalAlignmentSelector.SelectedItem = AdjustableStackPanelExample.HorizontalAlignment;

        VerticalAlignmentSelector.ItemsSource = (VerticalAlignment[])typeof(VerticalAlignment).GetEnumValues();
        VerticalAlignmentSelector.SelectedItem = AdjustableStackPanelExample.VerticalAlignment;

        OrientationSelector.ItemsSource = (Orientation[])typeof(Orientation).GetEnumValues();
        OrientationSelector.SelectedItem = AdjustableStackPanelExample.Orientation;
        AnimationDurationSelector.Value = AdjustableStackPanelExample.TransitionDuration.TotalMilliseconds;
    }

    private void AnimationDurationChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        AdjustableStackPanelExample.TransitionDuration = TimeSpan.FromMilliseconds(e.NewValue);
    }
}
