using System.Globalization;
using System.Windows.Data;
using static System.Double;

namespace PCL;

/// <summary>
///     对数据绑定进行加法运算，使用参数决定加数。
/// </summary>
public class AdditionConverter : IValueConverter
{
    public object Convert(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        if (value is null)
            return 0;
        if (!TryParse(value.ToString(), out var before))
            return 0;
        var scale = 1d;
        if (parameter is not null)
            TryParse(parameter.ToString(), out scale);
        return before + scale;
    }

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        if (value is null || !TryParse(value.ToString(), out var before))
            return Binding.DoNothing;
        var scale = 1d;
        if (parameter is not null)
            TryParse(parameter.ToString(), out scale);
        return before - scale;
    }
}