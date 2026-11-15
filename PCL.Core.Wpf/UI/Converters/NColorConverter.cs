using System;
using System.ComponentModel;
using System.Globalization;

namespace PCL.Core.UI.Converters;

public class NColorConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string)
            || base.CanConvertFrom(context, sourceType);
    }

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
    {
        return destinationType == typeof(string)
            || base.CanConvertTo(context, destinationType);
    }

    public override object? ConvertFrom(
        ITypeDescriptorContext? context,
        CultureInfo? culture,
        object value)
    {
        return value is string s
            ? new NColor(s)
            : base.ConvertFrom(context, culture, value);
    }

    public override object? ConvertTo(
        ITypeDescriptorContext? context,
        CultureInfo? culture,
        object? value,
        Type destinationType)
    {
        return value is NColor color && destinationType == typeof(string)
            ? $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}"
            : base.ConvertTo(context, culture, value, destinationType);
    }
}