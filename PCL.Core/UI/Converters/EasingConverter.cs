using System;
using System.ComponentModel;
using System.Globalization;
using PCL.Core.UI.Animation.Easings;

namespace PCL.Core.UI.Converters;

public class EasingConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) => 
        sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) =>
        destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string s)
        {
            return s switch
            {
                "BackEaseIn" => new BackEaseIn(),
                "BackEaseOut" => new BackEaseOut(),
                "BackEaseInOut" => new BackEaseInOut(),
                "BounceEaseIn" => new BounceEaseIn(),
                "BounceEaseOut" => new BounceEaseOut(),
                "BounceEaseInOut" => new BounceEaseInOut(),
                "CircularEaseIn" => new CircularEaseIn(),
                "CircularEaseOut" => new CircularEaseOut(),
                "CircularEaseInOut" => new CircularEaseInOut(),
                "CubicEaseIn" => new CubicEaseIn(),
                "CubicEaseOut" => new CubicEaseOut(),
                "CubicEaseInOut" => new CubicEaseInOut(),
                "ElasticEaseIn" => new ElasticEaseIn(),
                "ElasticEaseOut" => new ElasticEaseOut(),
                "ElasticEaseInOut" => new ElasticEaseInOut(),
                "ExponentialEaseIn" => new ExponentialEaseIn(),
                "ExponentialEaseOut" => new ExponentialEaseOut(),
                "ExponentialEaseInOut" => new ExponentialEaseInOut(),
                "LinearEasing" => new LinearEasing(),
                "QuadEaseIn" => new QuadEaseIn(),
                "QuadEaseOut" => new QuadEaseOut(),
                "QuadEaseInOut" => new QuadEaseInOut(),
                "QuarticEaseIn" => new QuarticEaseIn(),
                "QuarticEaseOut" => new QuarticEaseOut(),
                "QuarticEaseInOut" => new QuarticEaseInOut(),
                "QuinticEaseIn" => new QuinticEaseIn(),
                "QuinticEaseOut" => new QuinticEaseOut(),
                "QuinticEaseInOut" => new QuinticEaseInOut(),
                "SineEaseIn" => new SineEaseIn(),
                "SineEaseOut" => new SineEaseOut(),
                "SineEaseInOut" => new SineEaseInOut(),
                _ => throw new NotSupportedException($"不支持的缓动: {s}")
            };
        }
        return base.ConvertFrom(context, culture, value);
    }

    
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is IEasing easing)
        {
            return easing switch
            {
                BackEaseIn => "BackEaseIn",
                BackEaseOut => "BackEaseOut",
                BackEaseInOut => "BackEaseInOut",
                BounceEaseIn => "BounceEaseIn",
                BounceEaseOut => "BounceEaseOut",
                BounceEaseInOut => "BounceEaseInOut",
                CircularEaseIn => "CircularEaseIn",
                CircularEaseOut => "CircularEaseOut",
                CircularEaseInOut => "CircularEaseInOut",
                CubicEaseIn => "CubicEaseIn",
                CubicEaseOut => "CubicEaseOut",
                CubicEaseInOut => "CubicEaseInOut",
                ElasticEaseIn => "ElasticEaseIn",
                ElasticEaseOut => "ElasticEaseOut",
                ElasticEaseInOut => "ElasticEaseInOut",
                ExponentialEaseIn => "ExponentialEaseIn",
                ExponentialEaseOut => "ExponentialEaseOut",
                ExponentialEaseInOut => "ExponentialEaseInOut",
                LinearEasing => "LinearEasing",
                QuadEaseIn => "QuadEaseIn",
                QuadEaseOut => "QuadEaseOut",
                QuadEaseInOut => "QuadEaseInOut",
                QuarticEaseIn => "QuarticEaseIn",
                QuarticEaseOut => "QuarticEaseOut",
                QuarticEaseInOut => "QuarticEaseInOut",
                QuinticEaseIn => "QuinticEaseIn",
                QuinticEaseOut => "QuinticEaseOut",
                QuinticEaseInOut => "QuinticEaseInOut",
                SineEaseIn => "SineEaseIn",
                SineEaseOut => "SineEaseOut",
                SineEaseInOut => "SineEaseInOut",
                _ => throw new NotSupportedException($"不支持的缓动: {easing.GetType().Name}")
            };
        }
    
        return base.ConvertTo(context, culture, value, destinationType);
    }
}