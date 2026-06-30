using System.Globalization;
using System.Windows.Data;

namespace PCL;

/// <summary>
///     将 Boolean 取反。
/// </summary>
public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null)
            return false;
        return bool.TryParse(value.ToString(), out var boolValue) && !boolValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null) return false;

        if (bool.TryParse(value.ToString(), out var result)) return !result;

        return false;
    }
}