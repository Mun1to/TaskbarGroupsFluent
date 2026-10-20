using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TaskbarGroups.App.Converters;

/// <summary>Hides group-only controls for taskbar separators.</summary>
public class NonSeparatorToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
