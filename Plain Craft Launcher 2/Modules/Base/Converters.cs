using System.Globalization;
using Microsoft.VisualBasic.CompilerServices;

namespace PCL
{
    public class AdditionConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is null)
                return 0;
            double before;
            if (!double.TryParse(value.ToString(), out before))
                return 0;
            var scale = 1d;
            if (parameter is not null)
                double.TryParse(parameter.ToString(), out scale);
            return before + scale;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is null)
                return System.Windows.Data.Binding.DoNothing;
            double before;
            if (!double.TryParse(value.ToString(), out before))
                return System.Windows.Data.Binding.DoNothing;
            var scale = 1d;
            if (parameter is not null)
                double.TryParse(parameter.ToString(), out scale);
            if (scale == 0d)
                return System.Windows.Data.Binding.DoNothing;
            return before - scale;
        }
    }

    public class MultiplicationConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is null)
                return 0;
            double before;
            if (!double.TryParse(value.ToString(), out before))
                return 0;
            var scale = 1d;
            if (parameter is not null)
                double.TryParse(parameter.ToString(), out scale);
            return before * scale;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is null)
                return System.Windows.Data.Binding.DoNothing;
            double before;
            if (!double.TryParse(value.ToString(), out before))
                return System.Windows.Data.Binding.DoNothing;
            var scale = 1d;
            if (parameter is not null)
                double.TryParse(parameter.ToString(), out scale);
            if (scale == 0d)
                return System.Windows.Data.Binding.DoNothing;
            return before / scale;
        }
    }

    public class InverseBooleanToVisibilityConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is null)
                return System.Windows.Visibility.Visible;
            bool boolValue;
            return bool.TryParse(value.ToString(), out boolValue)
                ? boolValue ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible
                : System.Windows.Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is null)
                return false;
            return value is System.Windows.Visibility
                ? Operators.ConditionalCompareObjectNotEqual(value, System.Windows.Visibility.Visible, false)
                : false;
        }
    }

    public class InverseBooleanConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is null)
                return false;
            bool boolValue;
            return bool.TryParse(value.ToString(), out boolValue) ? !boolValue : false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return false;
            if (bool.TryParse(value.ToString(), out var result)) return !result;
            return false;
        }
    }
}
