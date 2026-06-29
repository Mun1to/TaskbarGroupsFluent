using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TaskbarGroups.App.Converters;

/// <summary>
/// Returns Visible when the bound value is null, Collapsed otherwise.
/// Used to show a placeholder icon only when a group has no image.
/// </summary>
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is null ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
