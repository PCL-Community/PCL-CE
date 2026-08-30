using System;
using System.ComponentModel;
using System.Globalization;

namespace PCL.Core.UI.Converters;

public class NColorConverter : TypeConverter
{
    public override bool CanConvertFrom(
        ITypeDescriptorContext? context,
        Type sourceType)
    {
        return sourceType == typeof(string) ||
               base.CanConvertFrom(context, sourceType);
    }

    public override bool CanConvertTo(
        ITypeDescriptorContext? context,
        Type? destinationType)
    {
        return destinationType == typeof(string) ||
               base.CanConvertTo(context, destinationType);
    }

    public override object? ConvertFrom(
        ITypeDescriptorContext? context,
        CultureInfo? culture,
        object value)
    {
        if (value is string s) return new NColor(s);

        return base.ConvertFrom(context, culture, value);
    }

    public override object? ConvertTo(
        ITypeDescriptorContext? context,
        CultureInfo? culture,
        object? value,
        Type destinationType)
    {
        if (value is not NColor color || destinationType != typeof(string))
            return base.ConvertTo(context, culture, value, destinationType);

        var r = (byte)Math.Clamp(color.R, 0f, 255f);
        var g = (byte)Math.Clamp(color.G, 0f, 255f);
        var b = (byte)Math.Clamp(color.B, 0f, 255f);
        var a = (byte)Math.Clamp(color.A, 0f, 255f);

        return $"#{r:X2}{g:X2}{b:X2}{a:X2}";
    }
}