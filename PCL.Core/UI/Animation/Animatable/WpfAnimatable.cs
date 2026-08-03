using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using PCL.Core.UI.Animation.ValueProcessor;

namespace PCL.Core.UI.Animation.Animatable;

public sealed class WpfAnimatable(DependencyObject owner, DependencyProperty? property) : IAnimatable
{
    public DependencyObject Owner { get; set; } = owner;
    public DependencyProperty? Property { get; set; } = property;

    public object? GetValue()
    {
        DependencyProperty? actualProperty;

        if (Property == FrameworkElement.WidthProperty)
        {
            actualProperty = FrameworkElement.ActualWidthProperty;
        }
        else if (Property == FrameworkElement.HeightProperty)
        {
            actualProperty = FrameworkElement.ActualHeightProperty;
        }
        else
        {
            actualProperty = Property;
        }

        ArgumentNullException.ThrowIfNull(actualProperty);
        
        var value  = Owner.GetValue(actualProperty);
        return value switch
        {
            SolidColorBrush brush => (NColor)brush,
            Color color => (NColor)color,
            ScaleTransform scaleTransform => (NScaleTransform)scaleTransform,
            RotateTransform rotateTransform => (NRotateTransform)rotateTransform,
            _ => value
        };
    }

    public void SetValue(object value)
    {
        value = ValueProcessorManager.Filter(value);
        ArgumentNullException.ThrowIfNull(Property);
        
        value = value switch
        {
            NColor color => Property.Name switch
            {
                "Color" => (Color)color,
                _ => (SolidColorBrush)color
            },
            NScaleTransform st => (ScaleTransform)st,
            NRotateTransform rt => (RotateTransform)rt,
            _ => value
        };

        Owner.SetValue(Property, value);
    }

    public void SetValue<T>(T value)
    {
        value = ValueProcessorManager.Filter(value);
        ArgumentNullException.ThrowIfNull(Property);
        _SetValueCore(value);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void _SetValueCore<T>(T value)
    {
        ArgumentNullException.ThrowIfNull(Property);
        
        if (typeof(T) == typeof(NColor))
        {
            var color = Unsafe.As<T, NColor>(ref value);

            Owner.SetValue(
                Property,
                Property.Name == "Color"
                    ? (Color)color
                    : (SolidColorBrush)color);

            return;
        }

        if (typeof(T) == typeof(NScaleTransform))
        {
            var st = Unsafe.As<T, NScaleTransform>(ref value);
            Owner.SetValue(Property, (ScaleTransform)st);
            return;
        }

        if (typeof(T) == typeof(NRotateTransform))
        {
            var rt = Unsafe.As<T, NRotateTransform>(ref value);
            Owner.SetValue(Property, (RotateTransform)rt);
            return;
        }

        Owner.SetValue(Property, value!);
    }
}