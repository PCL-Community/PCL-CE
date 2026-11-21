using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.Core.UI.Animation.Animatable;
using PCL.Core.UI.Animation.Core;
using PCL.Core.UI.Animation.Easings;

namespace PCL.Core.UI.Animation;

/// <summary>常用 Double 动画的目标与构造辅助。</summary>
internal static class AnimationTargets
{
    public static IAnimatable Opacity(object target)
    {
        if (target is Visual visual)
            return new RelayAnimatable(
                () => visual.Opacity,
                value => visual.Opacity = Math.Clamp(value, 0d, 1d));

        return ReflectionNumeric(target, "Opacity", true, 1d);
    }

    public static IAnimatable Size(object target, bool width)
    {
        if (target is Control control)
            return new RelayAnimatable(
                () => width ? EffectiveWidth(control) : EffectiveHeight(control),
                value =>
                {
                    value = Math.Max(0d, value);
                    if (width)
                        control.Width = value;
                    else
                        control.Height = value;
                });

        return ReflectionNumeric(target, width ? "Width" : "Height", true);
    }

    public static IAnimatable Offset(object target, bool horizontal)
    {
        if (target is not Control control)
            return EmptyAnimatable.Instance;

        return new RelayAnimatable(
            () => 0d,
            value =>
            {
                if (horizontal)
                    ApplyDeltaX(control, value);
                else
                    ApplyDeltaY(control, value);
            });
    }

    public static IAnimatable LayoutScale(object target, double value, bool absolute)
    {
        if (target is not Control control)
            return EmptyAnimatable.Instance;

        var deltaWidth = absolute ? value : EffectiveWidth(control) * value;
        var deltaHeight = absolute ? value : EffectiveHeight(control) * value;

        return new RelayAnimatable(
            () => 0d,
            percentDelta =>
            {
                var offsetX = -0.5d * deltaWidth * percentDelta;
                var offsetY = -0.5d * deltaHeight * percentDelta;
                control.Width = Math.Max(0d, EffectiveWidth(control) + deltaWidth * percentDelta);
                control.Height = Math.Max(
                    0d,
                    EffectiveHeight(control) + deltaHeight * percentDelta);
                var margin = control.Margin;
                control.Margin = new Thickness(
                    margin.Left + offsetX,
                    margin.Top + offsetY,
                    margin.Right + offsetX,
                    margin.Bottom + offsetY);
            });
    }

    public static IAnimatable ScaleTransform(Visual visual)
    {
        visual.RenderTransformOrigin = RelativePoint.Center;
        var transform = EnsureScale(visual);
        return new RelayAnimatable(
            () => 0d,
            delta =>
            {
                transform.ScaleX = Math.Max(0d, transform.ScaleX + delta);
                transform.ScaleY = Math.Max(0d, transform.ScaleY + delta);
            });
    }

    public static IAnimatable RotateTransform(Visual visual)
    {
        var transform = EnsureRotate(visual);
        return new RelayAnimatable(() => 0d, delta => transform.Angle += delta);
    }

    public static IAnimatable TranslateX(Visual visual)
    {
        var transform = EnsureTranslate(visual);
        return new RelayAnimatable(() => 0d, delta => transform.X += delta);
    }

    public static IAnimatable TranslateY(Visual visual)
    {
        var transform = EnsureTranslate(visual);
        return new RelayAnimatable(() => 0d, delta => transform.Y += delta);
    }

    public static IAnimatable GridLength(ColumnDefinition column)
    {
        return new RelayAnimatable(
            () => column.Width.Value,
            value => column.Width = new GridLength(Math.Max(0d, value), GridUnitType.Star));
    }

    public static IAnimatable Callback(Action<double> action)
    {
        return new RelayAnimatable(() => 0d, action);
    }

    public static IAnimatable Property(object target, AvaloniaProperty property)
    {
        return target is AvaloniaObject avaloniaObject
            ? new AvaloniaAnimatable(avaloniaObject, property)
            : EmptyAnimatable.Instance;
    }

    public static NColor ReadColor(AvaloniaObject target, AvaloniaProperty property)
    {
        return target.GetValue(property) switch
        {
            Color color => new NColor(color),
            ISolidColorBrush brush => new NColor(brush.Color),
            NColor color => color,
            _ => new NColor(0f, 0f, 0f, 0f)
        };
    }

    public static NColor ResolveColor(StyledElement element, string key)
    {
        object? value = null;
        try
        {
            if (!element.TryFindResource(key, element.ActualThemeVariant, out value))
                Application.Current?.TryFindResource(key, out value);
        }
        catch
        {
            // 资源缺失按透明处理。
        }

        return value switch
        {
            Color color => new NColor(color),
            ISolidColorBrush brush => new NColor(brush.Color),
            NColor color => color,
            _ => new NColor(0f, 0f, 0f, 0f)
        };
    }

    public static double EffectiveWidth(Control control)
    {
        return double.IsNaN(control.Width) ? control.Bounds.Width : control.Width;
    }

    private static double EffectiveHeight(Control control)
    {
        return double.IsNaN(control.Height) ? control.Bounds.Height : control.Height;
    }

    private static IAnimatable ReflectionNumeric(
        object target,
        string name,
        bool clampZero,
        double? clampMax = null)
    {
        var property = target
            .GetType()
            .GetProperty(
                name,
                BindingFlags.Public | BindingFlags.Instance);
        if (property is null)
            return EmptyAnimatable.Instance;

        return new RelayAnimatable(
            () => Convert.ToDouble(property.GetValue(target)),
            value =>
            {
                if (clampZero)
                    value = Math.Max(0d, value);
                if (clampMax is not null)
                    value = Math.Min(value, clampMax.Value);

                var type = Nullable.GetUnderlyingType(property.PropertyType)
                    ?? property.PropertyType;
                property.SetValue(target, Convert.ChangeType(value, type));
            });
    }

    private static ScaleTransform EnsureScale(Visual visual)
    {
        if (visual.RenderTransform is ScaleTransform scale)
            return scale;

        scale = new ScaleTransform(1d, 1d);
        visual.RenderTransformOrigin = RelativePoint.Center;
        visual.RenderTransform = scale;
        return scale;
    }

    private static RotateTransform EnsureRotate(Visual visual)
    {
        if (visual.RenderTransform is RotateTransform rotate)
            return rotate;

        rotate = new RotateTransform();
        visual.RenderTransformOrigin = RelativePoint.Center;
        visual.RenderTransform = rotate;
        return rotate;
    }

    private static TranslateTransform EnsureTranslate(Visual visual)
    {
        if (visual.RenderTransform is TranslateTransform translate)
            return translate;

        translate = new TranslateTransform();
        visual.RenderTransform = translate;
        return translate;
    }

    private static void ApplyDeltaX(Control control, double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return;

        if (control is Window window)
        {
            window.Position = new PixelPoint(
                window.Position.X + (int)Math.Round(value),
                window.Position.Y);
            return;
        }

        if (control.Parent is Canvas)
        {
            var left = Canvas.GetLeft(control);
            Canvas.SetLeft(control, (double.IsNaN(left) ? 0d : left) + value);
            return;
        }

        var margin = control.Margin;
        control.Margin = control.HorizontalAlignment switch
        {
            HorizontalAlignment.Left or HorizontalAlignment.Stretch => new Thickness(
                margin.Left + value,
                margin.Top,
                margin.Right,
                margin.Bottom),
            HorizontalAlignment.Right => new Thickness(
                margin.Left,
                margin.Top,
                margin.Right - value,
                margin.Bottom),
            _ => margin
        };
    }

    private static void ApplyDeltaY(Control control, double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return;

        if (control is Window window)
        {
            window.Position = new PixelPoint(
                window.Position.X,
                window.Position.Y + (int)Math.Round(value));
            return;
        }

        if (control.Parent is Canvas)
        {
            var top = Canvas.GetTop(control);
            Canvas.SetTop(control, (double.IsNaN(top) ? 0d : top) + value);
            return;
        }

        var margin = control.Margin;
        control.Margin = control.VerticalAlignment switch
        {
            VerticalAlignment.Top => new Thickness(
                margin.Left,
                margin.Top + value,
                margin.Right,
                margin.Bottom),
            VerticalAlignment.Bottom => new Thickness(
                margin.Left,
                margin.Top,
                margin.Right,
                margin.Bottom - value),
            _ => margin
        };
    }

    internal sealed class RelayAnimatable(
        Func<double> getter,
        Action<double> setter) : IAnimatable
    {
        public object? GetValue()
        {
            return getter();
        }

        public void SetValue(object value)
        {
            setter(Convert.ToDouble(value));
        }

        public void SetValue<T>(T value)
        {
            setter(Convert.ToDouble(value!));
        }
    }
}

public sealed class OpacityAnimation : DoubleFromToAnimation
{
    public OpacityAnimation(
        object target,
        double value,
        int time = 400,
        int delay = 0,
        IEasing? ease = null,
        bool after = false)
    {
        Target = AnimationTargets.Opacity(target);
        To = value;
        ValueType = AnimationValueType.Relative;
        Duration = TimeSpan.FromMilliseconds(Math.Max(0, time));
        Delay = TimeSpan.FromMilliseconds(Math.Max(0, delay));
        Easing = ease ?? new LinearEasing();
        After = after;
    }
}

public sealed class WidthAnimation : DoubleFromToAnimation
{
    public WidthAnimation(
        object target,
        double value,
        int time = 400,
        int delay = 0,
        IEasing? ease = null,
        bool after = false)
    {
        Target = AnimationTargets.Size(target, true);
        To = value;
        ValueType = AnimationValueType.Relative;
        Duration = TimeSpan.FromMilliseconds(Math.Max(0, time));
        Delay = TimeSpan.FromMilliseconds(Math.Max(0, delay));
        Easing = ease ?? new LinearEasing();
        After = after;
    }
}

public sealed class HeightAnimation : DoubleFromToAnimation
{
    public HeightAnimation(
        object target,
        double value,
        int time = 400,
        int delay = 0,
        IEasing? ease = null,
        bool after = false)
    {
        Target = AnimationTargets.Size(target, false);
        To = value;
        ValueType = AnimationValueType.Relative;
        Duration = TimeSpan.FromMilliseconds(Math.Max(0, time));
        Delay = TimeSpan.FromMilliseconds(Math.Max(0, delay));
        Easing = ease ?? new LinearEasing();
        After = after;
    }
}

public sealed class XAnimation : DoubleFromToAnimation
{
    public XAnimation(
        object target,
        double value,
        int time = 400,
        int delay = 0,
        IEasing? ease = null,
        bool after = false)
    {
        Target = AnimationTargets.Offset(target, true);
        To = value;
        ValueType = AnimationValueType.Relative;
        Duration = TimeSpan.FromMilliseconds(Math.Max(0, time));
        Delay = TimeSpan.FromMilliseconds(Math.Max(0, delay));
        Easing = ease ?? new LinearEasing();
        After = after;
    }
}

public sealed class YAnimation : DoubleFromToAnimation
{
    public YAnimation(
        object target,
        double value,
        int time = 400,
        int delay = 0,
        IEasing? ease = null,
        bool after = false)
    {
        Target = AnimationTargets.Offset(target, false);
        To = value;
        ValueType = AnimationValueType.Relative;
        Duration = TimeSpan.FromMilliseconds(Math.Max(0, time));
        Delay = TimeSpan.FromMilliseconds(Math.Max(0, delay));
        Easing = ease ?? new LinearEasing();
        After = after;
    }
}

public sealed class ScaleAnimation : DoubleFromToAnimation
{
    public ScaleAnimation(
        object target,
        double value,
        int time = 400,
        int delay = 0,
        IEasing? ease = null,
        bool after = false,
        bool absolute = false)
    {
        Target = AnimationTargets.LayoutScale(target, value, absolute);
        To = 1d;
        ValueType = AnimationValueType.Relative;
        Duration = TimeSpan.FromMilliseconds(Math.Max(0, time));
        Delay = TimeSpan.FromMilliseconds(Math.Max(0, delay));
        Easing = ease ?? new LinearEasing();
        After = after;
    }
}

public sealed class ScaleTransformAnimation : DoubleFromToAnimation
{
    public ScaleTransformAnimation(
        object target,
        double value,
        int time = 400,
        int delay = 0,
        IEasing? ease = null,
        bool after = false)
    {
        Target = target is Visual visual
            ? AnimationTargets.ScaleTransform(visual)
            : EmptyAnimatable.Instance;
        To = value;
        ValueType = AnimationValueType.Relative;
        Duration = TimeSpan.FromMilliseconds(Math.Max(0, time));
        Delay = TimeSpan.FromMilliseconds(Math.Max(0, delay));
        Easing = ease ?? new LinearEasing();
        After = after;
    }
}

public sealed class RotateTransformAnimation : DoubleFromToAnimation
{
    public RotateTransformAnimation(
        object target,
        double value,
        int time = 400,
        int delay = 0,
        IEasing? ease = null,
        bool after = false)
    {
        Target = target is Visual visual
            ? AnimationTargets.RotateTransform(visual)
            : EmptyAnimatable.Instance;
        To = value;
        ValueType = AnimationValueType.Relative;
        Duration = TimeSpan.FromMilliseconds(Math.Max(0, time));
        Delay = TimeSpan.FromMilliseconds(Math.Max(0, delay));
        Easing = ease ?? new LinearEasing();
        After = after;
    }
}

public sealed class TranslateXAnimation : DoubleFromToAnimation
{
    public TranslateXAnimation(
        object target,
        double value,
        int time = 400,
        int delay = 0,
        IEasing? ease = null,
        bool after = false)
    {
        Target = target is Visual visual
            ? AnimationTargets.TranslateX(visual)
            : EmptyAnimatable.Instance;
        To = value;
        ValueType = AnimationValueType.Relative;
        Duration = TimeSpan.FromMilliseconds(Math.Max(0, time));
        Delay = TimeSpan.FromMilliseconds(Math.Max(0, delay));
        Easing = ease ?? new LinearEasing();
        After = after;
    }
}

public sealed class TranslateYAnimation : DoubleFromToAnimation
{
    public TranslateYAnimation(
        object target,
        double value,
        int time = 400,
        int delay = 0,
        IEasing? ease = null,
        bool after = false)
    {
        Target = target is Visual visual
            ? AnimationTargets.TranslateY(visual)
            : EmptyAnimatable.Instance;
        To = value;
        ValueType = AnimationValueType.Relative;
        Duration = TimeSpan.FromMilliseconds(Math.Max(0, time));
        Delay = TimeSpan.FromMilliseconds(Math.Max(0, delay));
        Easing = ease ?? new LinearEasing();
        After = after;
    }
}

public sealed class GridLengthWidthAnimation : DoubleFromToAnimation
{
    public GridLengthWidthAnimation(
        object target,
        double value,
        int time = 400,
        int delay = 0,
        IEasing? ease = null,
        bool after = false)
    {
        Target = target is ColumnDefinition column
            ? AnimationTargets.GridLength(column)
            : EmptyAnimatable.Instance;
        To = value;
        ValueType = AnimationValueType.Relative;
        Duration = TimeSpan.FromMilliseconds(Math.Max(0, time));
        Delay = TimeSpan.FromMilliseconds(Math.Max(0, delay));
        Easing = ease ?? new LinearEasing();
        After = after;
    }
}

public sealed class DoubleAnimation : DoubleFromToAnimation
{
    public DoubleAnimation(
        object target,
        AvaloniaProperty property,
        double value,
        int time = 400,
        int delay = 0,
        IEasing? ease = null,
        bool after = false)
    {
        Target = AnimationTargets.Property(target, property);
        To = value;
        ValueType = AnimationValueType.Relative;
        Duration = TimeSpan.FromMilliseconds(Math.Max(0, time));
        Delay = TimeSpan.FromMilliseconds(Math.Max(0, delay));
        Easing = ease ?? new LinearEasing();
        After = after;
    }

    public DoubleAnimation(
        Action<double> action,
        double value,
        int time = 400,
        int delay = 0,
        IEasing? ease = null,
        bool after = false)
    {
        Target = AnimationTargets.Callback(action);
        To = value;
        ValueType = AnimationValueType.Relative;
        Duration = TimeSpan.FromMilliseconds(Math.Max(0, time));
        Delay = TimeSpan.FromMilliseconds(Math.Max(0, delay));
        Easing = ease ?? new LinearEasing();
        After = after;
    }
}

public sealed class ColorAnimation : NColorFromToAnimation
{
    public ColorAnimation(
        Control target,
        AvaloniaProperty property,
        NColor value,
        int time = 400,
        int delay = 0,
        IEasing? ease = null,
        bool after = false)
    {
        Target = new AvaloniaAnimatable(target, property);
        From = new NColor(0f, 0f, 0f, 0f);
        HasFrom = true;
        To = value;
        ValueType = AnimationValueType.Relative;
        Duration = TimeSpan.FromMilliseconds(Math.Max(0, time));
        Delay = TimeSpan.FromMilliseconds(Math.Max(0, delay));
        Easing = ease ?? new LinearEasing();
        After = after;
    }

    public ColorAnimation(
        Control target,
        AvaloniaProperty property,
        string resource,
        int time = 400,
        int delay = 0,
        IEasing? ease = null,
        bool after = false)
    {
        var targetColor = AnimationTargets.ResolveColor(target, resource);
        var current = AnimationTargets.ReadColor(target, property);
        Target = new AvaloniaAnimatable(target, property);
        From = new NColor(0f, 0f, 0f, 0f);
        HasFrom = true;
        To = targetColor - current;
        ValueType = AnimationValueType.Relative;
        Duration = TimeSpan.FromMilliseconds(Math.Max(0, time));
        Delay = TimeSpan.FromMilliseconds(Math.Max(0, delay));
        Easing = ease ?? new LinearEasing();
        After = after;
        ResourceKey = resource;
    }

    public string? ResourceKey { get; }

    protected override void OnAnimationCompleted()
    {
        if (Status != AnimationStatus.Completed || ResourceKey is null)
            return;

        if (Target is AvaloniaAnimatable { Owner: { } owner, Property: { } property })
            ThemeResourceRestorer.Restore(owner, property, ResourceKey);
    }
}