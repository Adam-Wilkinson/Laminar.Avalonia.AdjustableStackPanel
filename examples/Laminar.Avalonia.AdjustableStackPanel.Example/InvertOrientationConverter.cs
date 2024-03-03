using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Layout;

namespace Laminar.Avalonia.AdjustableStackPanel.Example;
public class InvertOrientationConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Orientation orientation)
        {
            return orientation switch
            {
                Orientation.Vertical => Orientation.Horizontal,
                _ => Orientation.Vertical,
            };
        }

        return Orientation.Vertical;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Convert(value, targetType, parameter, culture);
}
