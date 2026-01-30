using System;
using System.ComponentModel;
using System.Globalization;

namespace PCL.Core.UI.Converters;

public class NColorConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) =>
        destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string s)
        {
            return new NColor(s);
        }
        
        return base.ConvertFrom(context, culture, value);
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (value is NColor color && destinationType == typeof(string))
        {
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}";
        }
        return base.ConvertTo(context, culture, value, destinationType);
    }
}