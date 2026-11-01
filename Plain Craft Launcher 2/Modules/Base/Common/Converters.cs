using System.Globalization;
using System.Windows;
using System.Windows.Data;
using static System.Double;

namespace PCL;

/// <summary>
///     对数据绑定进行加法运算，使用参数决定加数。
/// </summary>
public class AdditionConverter : IValueConverter
{
    public object Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        if (value is null
            || !TryParse(value.ToString(), out var before))
            return 0;
        var scale = 1d;
        if (parameter is not null)
            _ = TryParse(parameter.ToString(), out scale);
        return before + scale;
    }

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        if (value is null
            || !TryParse(value.ToString(), out var before))
            return Binding.DoNothing;
        var scale = 1d;
        if (parameter is not null)
            _ = TryParse(parameter.ToString(), out scale);
        if (scale == 0d)
            return Binding.DoNothing;
        return before - scale;
    }
}

/// <summary>
///     对数据绑定进行乘法运算，使用参数决定乘数。
/// </summary>
public class MultiplicationConverter : IValueConverter
{
    public object Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        if (value is null
            || !TryParse(value.ToString(), out var before))
            return 0;
        var scale = 1d;
        if (parameter is not null)
            _ = TryParse(parameter.ToString(), out scale);
        return before * scale;
    }

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        if (value is null
            || !TryParse(value.ToString(), out var before))
            return Binding.DoNothing;
        var scale = 1d;
        if (parameter is not null)
            _ = TryParse(parameter.ToString(), out scale);
        if (scale == 0d)
            return Binding.DoNothing;
        return before / scale;
    }
}

/// <summary>
///     将取反的 Boolean 绑定到 Visibility。
/// </summary>
public class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        return value is null
            ? Visibility.Visible
            : bool.TryParse(value.ToString(), out var boolValue)
                ? boolValue
                    ? Visibility.Collapsed
                    : Visibility.Visible
                : Visibility.Visible;
    }

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        return value is Visibility v && v != Visibility.Visible;
    }
}

/// <summary>
///     将 Boolean 取反。
/// </summary>
public class InverseBooleanConverter : IValueConverter
{
    public object Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        return value is not null
            && bool.TryParse(value.ToString(), out var boolValue)
            && !boolValue;
    }

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        return value is not null && bool.TryParse(value.ToString(), out var result) && !result;
    }
}