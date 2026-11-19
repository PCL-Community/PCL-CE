using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.Core.UI.Animation.ValueProcessor;

namespace PCL.Core.UI.Animation.Animatable;

/// <summary>
///     包装 <see cref="AvaloniaObject" /> 与 <see cref="AvaloniaProperty" /> 的动画目标。
///     读值时把颜色、缩放与旋转属性换算成 <see cref="NColor" />、<see cref="NScaleTransform" />、
///     <see cref="NRotateTransform" />，写回时再换算成属性要求的类型。
/// </summary>
public sealed class AvaloniaAnimatable(
    AvaloniaObject owner,
    AvaloniaProperty? property) : IAnimatable
{
    public AvaloniaObject Owner { get; set; } = owner;
    public AvaloniaProperty? Property { get; set; } = property;

    public object? GetValue()
    {
        ArgumentNullException.ThrowIfNull(Property);

        if (Property == Layoutable.WidthProperty && Owner is Layoutable widthOwner)
            return widthOwner.Bounds.Width;

        if (Property == Layoutable.HeightProperty && Owner is Layoutable heightOwner)
            return heightOwner.Bounds.Height;

        var value = Owner.GetValue(Property);
        return value switch
        {
            ISolidColorBrush brush => (NColor)brush.Color,
            Color color => (NColor)color,
            ScaleTransform scaleTransform => (NScaleTransform)scaleTransform,
            RotateTransform rotateTransform => (NRotateTransform)rotateTransform,
            _ => value
        };
    }

    public void SetValue(object value)
    {
        ArgumentNullException.ThrowIfNull(Property);

        value = ValueProcessorManager.Filter(value);
        Owner.SetValue(Property, _ConvertValue(value));
    }

    public void SetValue<T>(T value)
    {
        ArgumentNullException.ThrowIfNull(Property);

        value = ValueProcessorManager.Filter(value);
        _SetValueCore(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void _SetValueCore<T>(T value)
    {
        ArgumentNullException.ThrowIfNull(Property);

        if (typeof(T) == typeof(NColor))
        {
            var color = Unsafe.As<T, NColor>(ref value);
            Owner.SetValue(Property, _ConvertColor(color));
            return;
        }

        if (typeof(T) == typeof(NScaleTransform))
        {
            var scaleTransform = Unsafe.As<T, NScaleTransform>(ref value);
            Owner.SetValue(Property, _ConvertScaleTransform(scaleTransform));
            return;
        }

        if (typeof(T) == typeof(NRotateTransform))
        {
            var rotateTransform = Unsafe.As<T, NRotateTransform>(ref value);
            Owner.SetValue(Property, (RotateTransform)rotateTransform);
            return;
        }

        Owner.SetValue(Property, value!);
    }

    private object _ConvertValue(object value)
    {
        return value switch
        {
            NColor color => _ConvertColor(color),
            NScaleTransform scaleTransform => _ConvertScaleTransform(scaleTransform),
            NRotateTransform rotateTransform => (RotateTransform)rotateTransform,
            _ => value
        };
    }

    private object _ConvertColor(NColor color)
    {
        var propertyType = Property!.PropertyType;
        if (propertyType == typeof(Color)) return color.ToColor();
        if (typeof(IBrush).IsAssignableFrom(propertyType)) return color.ToBrush();
        return color;
    }

    private object _ConvertScaleTransform(NScaleTransform scaleTransform)
    {
        // ScaleTransform 没有 CenterX/CenterY，缩放中心改用 RenderTransformOrigin 表达
        if (Owner is Visual visual
            && (Math.Abs(scaleTransform.CenterX) > 1e-6 || Math.Abs(scaleTransform.CenterY) > 1e-6))
        {
            var bounds = visual.Bounds;
            var originX = bounds.Width > 0 ? scaleTransform.CenterX / bounds.Width : 0.5d;
            var originY = bounds.Height > 0 ? scaleTransform.CenterY / bounds.Height : 0.5d;
            visual.RenderTransformOrigin = new RelativePoint(
                Math.Clamp(originX, 0d, 1d),
                Math.Clamp(originY, 0d, 1d),
                RelativeUnit.Relative);
        }

        return (ScaleTransform)scaleTransform;
    }
}