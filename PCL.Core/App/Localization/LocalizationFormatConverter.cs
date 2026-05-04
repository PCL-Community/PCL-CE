using System;
using System.Globalization;
using System.Windows.Data;

namespace PCL.Core.App.Localization;

/// <summary>
///     使用当前展示区域性格式化绑定值。
/// </summary>
public sealed class LocalizationFormatConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null) return null;
        if (parameter is not string format || string.IsNullOrWhiteSpace(format)) return value;
        return value switch
        {
            IFormattable formattable => formattable.ToString(format, CultureInfo.CurrentCulture),
            _ => string.Format(CultureInfo.CurrentCulture, "{0}", value)
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}