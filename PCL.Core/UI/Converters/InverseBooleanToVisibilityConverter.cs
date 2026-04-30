using Microsoft.VisualBasic.CompilerServices;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PCL.Core.UI.Converters;

/// <summary>
///     将取反的 Boolean 绑定到 Visibility。
/// </summary>
public class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return Visibility.Visible;
        }

        return bool.TryParse(value.ToString(), out var boolValue)
            ? boolValue ? Visibility.Collapsed : Visibility.Visible
            : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return false;
        }

        return value is Visibility && Operators.ConditionalCompareObjectNotEqual(value, Visibility.Visible, false);
    }
}
