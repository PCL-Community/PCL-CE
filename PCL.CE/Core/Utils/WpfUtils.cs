using System.Windows;

namespace PCL.CE.Core.Utils;



public static class WpfUtils
{
    public static bool IsDependencyPropertySet(DependencyObject obj, DependencyProperty dp)
    {
        return obj.ReadLocalValue(dp) != DependencyProperty.UnsetValue;
    }
}