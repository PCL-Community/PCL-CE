using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PCL.Core.UI.Converters;

/// <summary>
/// 将可 null 的值转换为 WPF Visibility 状态：若为 null 则折叠，否则可见
/// </summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value != null ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
