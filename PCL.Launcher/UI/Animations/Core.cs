using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.Core.Logging;
using PCL.Core.Utils;
using MyColor = PCL.UI.Controls.Common.MyColor;

namespace PCL;

public static partial class Animations
{
    private static readonly Dictionary<(Type Type, string Name), PropertyInfo> PropertyCache = [];

    /// <summary>
    ///     Advance one animation group.
    /// </summary>
    private static void _AniRunGroup(AniGroupEntry entry, int deltaTick)
    {
        var data = entry.data;
        var span = CollectionsMarshal.AsSpan(data);
        var write = 0;
        var blocked = false;
        int read;

        for (read = 0; read < span.Length; read++)
        {
            ref var anim = ref span[read];

            if (anim.isAfter)
            {
                if (blocked) break;
                anim.isAfter = false;
            }

            blocked = true;

            anim.timeFinished += deltaTick;
            if (anim.timeFinished > 0)
                _AniRun(ref anim);

            if (anim.timeFinished >= anim.timeTotal)
            {
                _AniFinishColor(ref anim);
                continue;
            }

            if (write != read) span[write] = anim;
            write++;
        }

        if (write < read) data.RemoveRange(write, read - write);
        if (data.Count == 0) _AniKillGroup(entry);
    }

    /// <summary>
    ///     Release a finished color animation by re-applying the resource it targets.
    /// </summary>
    private static void _AniFinishColor(ref AniData anim)
    {
        if (anim.typeMain != AniType.Color) return;
        if (anim.obj is not object[] { Length: >= 3 } target) return;
        if (target[2] is not string { Length: > 0 } resourceKey) return;
        if (target[0] is not AvaloniaObject element
            || target[1] is not AvaloniaProperty property) return;

        try
        {
            ThemeResourceRestorer.Restore(element, property, resourceKey);
        }
        catch (Exception ex)
        {
            LogWrapper.Debug(ex, "Animation", $"颜色动画结束时恢复资源失败：{resourceKey}");
        }
    }

    /// <summary>
    ///     Mark a group as dead and unregister it by name.
    /// </summary>
    private static void _AniKillGroup(AniGroupEntry entry)
    {
        entry.Dead = true;
        lock (AniGroupsLock)
        {
            if (AniByName.TryGetValue(entry.Key, out var current)
                && ReferenceEquals(current, entry))
                AniByName.Remove(entry.Key);
            _aniOrderDirty = true;
        }
    }

    /// <summary>
    ///     执行一个动画。
    /// </summary>
    /// <param name="ani">要执行的动画对象。</param>
    private static void _AniRun(ref AniData ani)
    {
        try
        {
            switch (ani.typeMain)
            {
                case AniType.Number:
                    _RunNumberAnimation(ani);
                    break;

                case AniType.Color:
                    _RunColorAnimation(ref ani);
                    break;

                case AniType.Scale:
                    _RunScaleAnimation(ani);
                    break;

                case AniType.TextAppear:
                    _RunTextAppearAnimation(ani);
                    break;

                case AniType.Code:
                    if (ani.value is Action action) action();
                    break;

                case AniType.ScaleTransform:
                    _RunScaleTransformAnimation(ani);
                    break;

                case AniType.RotateTransform:
                    _RunRotateTransformAnimation(ani);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            ani.timePercent = _GetAnimationProgress(ani);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Animation", $"执行动画失败：{ani}");
        }
    }

    private static double _GetAnimationProgress(AniData ani)
    {
        return ani.timeTotal <= 0 ? 1d : ani.timeFinished / (double)ani.timeTotal;
    }

    private static double _GetAnimationDelta(AniData ani)
    {
        var progress = _GetAnimationProgress(ani);
        return ani.ease?.GetDelta(progress, ani.timePercent) ?? progress - ani.timePercent;
    }

    #region Number

    private static void _RunNumberAnimation(AniData ani)
    {
        var delta = MathPercent(0d, Convert.ToDouble(ani.value), _GetAnimationDelta(ani));

        if (delta == 0d) return;

        switch (ani.typeSub)
        {
            case AniTypeSub.X:
                if (ani.obj is Control xControl) _DeltaLeft(xControl, delta);
                break;

            case AniTypeSub.Y:
                if (ani.obj is Control yControl) _DeltaTop(yControl, delta);
                break;

            case AniTypeSub.Opacity:
                if (ani.obj is Visual visual)
                    visual.Opacity = Math.Clamp(visual.Opacity + delta, 0d, 1d);
                else if (ani.obj is not null)
                    _SetNumericMember(
                        ani.obj,
                        "Opacity",
                        Math.Clamp(_GetNumericMember(ani.obj, "Opacity") + delta, 0d, 1d));
                break;

            case AniTypeSub.Width:
                if (ani.obj is Control widthControl)
                    widthControl.Width = Math.Max(_GetEffectiveWidth(widthControl) + delta, 0d);
                break;

            case AniTypeSub.Height:
                if (ani.obj is Control heightControl)
                    heightControl.Height = Math.Max(_GetEffectiveHeight(heightControl) + delta, 0d);
                break;

            case AniTypeSub.Value:
                if (ani.obj is not null)
                    _AddNumericMember(ani.obj, "Value", delta);
                break;

            case AniTypeSub.Radius:
                if (ani.obj is not null)
                    _AddNumericMember(ani.obj, "Radius", delta);
                break;

            case AniTypeSub.StrokeThickness:
                if (ani.obj is not null)
                    _SetNumericMember(
                        ani.obj,
                        "StrokeThickness",
                        Math.Max(_GetNumericMember(ani.obj, "StrokeThickness") + delta, 0d));
                break;

            case AniTypeSub.BorderThickness:
                if (ani.obj is not null)
                {
                    var thickness =
                        (Thickness)(_GetMember(ani.obj, "BorderThickness") ?? default(Thickness));
                    _SetMember(ani.obj, "BorderThickness", new Thickness(thickness.Bottom + delta));
                }

                break;

            case AniTypeSub.TranslateX:
            case AniTypeSub.TranslateY:
                if (ani.obj is Visual translateTarget)
                {
                    if (translateTarget.RenderTransform is not TranslateTransform)
                        translateTarget.RenderTransform = new TranslateTransform(0d, 0d);

                    var transform = (TranslateTransform)translateTarget.RenderTransform;

                    if (ani.typeSub == AniTypeSub.TranslateX)
                        transform.X += delta;
                    else
                        transform.Y += delta;
                }

                break;

            case AniTypeSub.Double:
                if (ani.obj is object[] { Length: >= 2 } args
                    && args[0] is AvaloniaObject doubleTarget
                    && args[1] is AvaloniaProperty doubleProperty)
                {
                    var value = Convert.ToDouble(doubleTarget.GetValue(doubleProperty)) + delta;
                    doubleTarget.SetValue(doubleProperty, value);
                }

                break;

            case AniTypeSub.DoubleParam:
                if (ani.obj is Action<double> doubleParam) doubleParam(delta);
                break;

            case AniTypeSub.GridLengthWidth:
                if (ani.obj is not null && _GetMember(ani.obj, "Width") is GridLength gridLength)
                    _SetMember(
                        ani.obj,
                        "Width",
                        new GridLength(Math.Max(gridLength.Value + delta, 0d), GridUnitType.Star));
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    #endregion

    #region Scale

    private static void _RunScaleAnimation(AniData ani)
    {
        if (ani.obj is not Control element || ani.value is not MyRect scale) return;

        var progressDelta = _GetAnimationDelta(ani);

        var marginX = MathPercent(0d, scale.Left, progressDelta);
        var marginY = MathPercent(0d, scale.Top, progressDelta);
        var widthDelta = MathPercent(0d, scale.Width, progressDelta);
        var heightDelta = MathPercent(0d, scale.Height, progressDelta);

        var margin = element.Margin;

        element.Margin = new Thickness(
            margin.Left + marginX,
            margin.Top + marginY,
            margin.Right + marginX,
            margin.Bottom + marginY);

        element.Width = Math.Max(_GetEffectiveWidth(element) + widthDelta, 0d);
        element.Height = Math.Max(_GetEffectiveHeight(element) + heightDelta, 0d);
    }

    #endregion

    #region Color

    private static void _RunColorAnimation(ref AniData ani)
    {
        if (ani.obj is not object[] { Length: >= 2 } args) return;
        if (args[0] is not AvaloniaObject element
            || args[1] is not AvaloniaProperty property) return;

        var delta = MathPercent(
            new MyColor(0d, 0d, 0d, 0d),
            (MyColor)ani.value!,
            _GetAnimationDelta(ani));

        delta += (MyColor)ani.valueLast!;

        var newColor = _ReadColor(element, property) + delta;

        _WriteColor(element, property, newColor);

        ani.valueLast = newColor - _ReadColor(element, property);
    }

    private static MyColor _ReadColor(AvaloniaObject element, AvaloniaProperty property)
    {
        var value = element.GetValue(property);
        return value switch
        {
            MyColor color => color,
            Color color => new MyColor(color),
            ISolidColorBrush brush => new MyColor(brush.Color),
            _ => new MyColor(value)
        };
    }

    private static void _WriteColor(
        AvaloniaObject element,
        AvaloniaProperty property,
        MyColor color)
    {
        element.SetValue(property, _ConvertColorValue(color, property));
    }

    /// <summary>
    ///     将颜色资源或颜色值转换为目标属性可接受的类型。
    /// </summary>
    private static object _ConvertColorValue(object? value, AvaloniaProperty property)
    {
        var color = value switch
        {
            MyColor myColor => (Color)myColor,
            Color avaloniaColor => avaloniaColor,
            ISolidColorBrush brush => brush.Color,
            _ => throw new InvalidOperationException($"不支持的颜色值类型：{value?.GetType()}")
        };

        var propertyType = property.PropertyType;
        if (propertyType == typeof(Color)) return color;
        if (typeof(IBrush).IsAssignableFrom(propertyType)) return new SolidColorBrush(color);
        if (propertyType == typeof(MyColor)) return new MyColor(color);
        return color;
    }

    #endregion

    #region TextAppear

    private static void _RunTextAppearAnimation(AniData ani)
    {
        if (ani.value is not object[] { Length: >= 2 } args) return;

        var originalText = args[0]?.ToString() ?? string.Empty;
        var isHiding = args[1] is true;
        var textLength = originalText.Length;

        if (textLength == 0) return;

        var progress = ani.ease?.GetDelta(_GetAnimationProgress(ani), 0d)
            ?? _GetAnimationProgress(ani);

        var startCount = isHiding ? textLength : 0;
        var direction = isHiding ? -1 : 1;

        var visibleCount = (int)Math.Round(
            startCount + Math.Round(textLength * direction * progress));

        var clampedCount = Math.Clamp(visibleCount, 0, textLength);
        var newText = originalText[..clampedCount];

        if (visibleCount < textLength && clampedCount > 0)
            newText += _GetRandomTextCharacter(originalText[clampedCount]);

        if (ani.obj is TextBlock textBlock)
            textBlock.Text = newText;
        else if (ani.obj is not null)
            _SetMember(ani.obj, "Context", newText);
    }

    private static string _GetRandomTextCharacter(char character)
    {
        if (character >= 128)
            try
            {
                return Encoding
                    .GetEncoding("GB18030")
                    .GetString(
                        new[]
                        {
                            (byte)RandomUtils.NextInt(176, 247),
                            (byte)RandomUtils.NextInt(161, 249)
                        });
            }
            catch
            {
                return ((char)RandomUtils.NextInt(0x4E00, 0x9FA5)).ToString();
            }

        const string characters =
            @"0123456789./*-+\[]{};':/?,!@#$%^&*()_+-=qwwertyuiopasdfghjklzxcvbnmQWERTYUIOPASDFGHJKLZXCVBNM";

        return RandomUtils.PickRandom(characters.ToCharArray()).ToString();
    }

    #endregion

    #region Transform

    private static void _RunScaleTransformAnimation(AniData ani)
    {
        if (ani.obj is not Visual element) return;

        var center = new RelativePoint(0.5d, 0.5d, RelativeUnit.Relative);
        if (element.RenderTransformOrigin != center)
            element.RenderTransformOrigin = center;

        if (element.RenderTransform is not ScaleTransform transform)
        {
            transform = new ScaleTransform(1d, 1d);
            element.RenderTransform = transform;
        }

        var delta = MathPercent(0d, Convert.ToDouble(ani.value), _GetAnimationDelta(ani));

        transform.ScaleX = Math.Max(transform.ScaleX + delta, 0d);
        transform.ScaleY = Math.Max(transform.ScaleY + delta, 0d);
    }

    private static void _RunRotateTransformAnimation(AniData ani)
    {
        if (ani.obj is not Visual element) return;

        if (element.RenderTransform is not RotateTransform transform)
        {
            element.RenderTransformOrigin = new RelativePoint(0.5d, 0.5d, RelativeUnit.Relative);
            transform = new RotateTransform(0d);
            element.RenderTransform = transform;
        }

        var delta = MathPercent(0d, Convert.ToDouble(ani.value), _GetAnimationDelta(ani));

        transform.Angle += delta;
    }

    #endregion

    #region 工具

    internal static double MathPercent(double valueA, double valueB, double percent)
    {
        return Math.Round(valueA * (1d - percent) + valueB * percent, 6);
    }

    internal static MyColor MathPercent(MyColor valueA, MyColor valueB, double percent)
    {
        return new MyColor(
            Math.Round(valueA.a * (1d - percent) + valueB.a * percent, 6),
            Math.Round(valueA.r * (1d - percent) + valueB.r * percent, 6),
            Math.Round(valueA.g * (1d - percent) + valueB.g * percent, 6),
            Math.Round(valueA.b * (1d - percent) + valueB.b * percent, 6));
    }

    /// <summary>
    ///     相对增减控件的左边距（Canvas 子元素走 Canvas.Left，窗口走位置）。
    /// </summary>
    internal static void _DeltaLeft(Control control, double newValue)
    {
        if (double.IsNaN(newValue) || double.IsInfinity(newValue)) return;

        if (control is Window window)
        {
            window.Position = new PixelPoint(
                window.Position.X + (int)Math.Round(newValue),
                window.Position.Y);
            return;
        }

        if (control.Parent is Canvas)
        {
            var left = Canvas.GetLeft(control);
            Canvas.SetLeft(control, (double.IsNaN(left) ? 0d : left) + newValue);
            return;
        }

        var margin = control.Margin;
        switch (control.HorizontalAlignment)
        {
            case HorizontalAlignment.Left:
            case HorizontalAlignment.Stretch:
                control.Margin = new Thickness(
                    margin.Left + newValue,
                    margin.Top,
                    margin.Right,
                    margin.Bottom);
                break;
            case HorizontalAlignment.Right:
                control.Margin = new Thickness(
                    margin.Left,
                    margin.Top,
                    margin.Right - newValue,
                    margin.Bottom);
                break;
        }
    }

    /// <summary>
    ///     相对增减控件的上边距（Canvas 子元素走 Canvas.Top，窗口走位置）。
    /// </summary>
    internal static void _DeltaTop(Control control, double newValue)
    {
        if (double.IsNaN(newValue) || double.IsInfinity(newValue)) return;

        if (control is Window window)
        {
            window.Position = new PixelPoint(
                window.Position.X,
                window.Position.Y + (int)Math.Round(newValue));
            return;
        }

        if (control.Parent is Canvas)
        {
            var top = Canvas.GetTop(control);
            Canvas.SetTop(control, (double.IsNaN(top) ? 0d : top) + newValue);
            return;
        }

        var margin = control.Margin;
        control.Margin = control.VerticalAlignment switch
        {
            VerticalAlignment.Top => new Thickness(
                margin.Left,
                margin.Top + newValue,
                margin.Right,
                margin.Bottom),

            VerticalAlignment.Bottom => new Thickness(
                margin.Left,
                margin.Top,
                margin.Right,
                margin.Bottom - newValue),

            _ => control.Margin
        };
    }

    internal static double _GetEffectiveWidth(Control control)
    {
        return double.IsNaN(control.Width) ? control.Bounds.Width : control.Width;
    }

    internal static double _GetEffectiveHeight(Control control)
    {
        return double.IsNaN(control.Height) ? control.Bounds.Height : control.Height;
    }

    private static PropertyInfo _GetProperty(object target, string name)
    {
        var key = (target.GetType(), name);
        if (PropertyCache.TryGetValue(key, out var cached))
            return cached;

        var property =
            target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"类型 {target.GetType()} 没有公开属性 {name}。");
        PropertyCache[key] = property;
        return property;
    }

    private static object? _GetMember(object target, string name)
    {
        return _GetProperty(target, name).GetValue(target);
    }

    private static void _SetMember(object target, string name, object? value)
    {
        _GetProperty(target, name).SetValue(target, value);
    }

    private static double _GetNumericMember(object target, string name)
    {
        return Convert.ToDouble(_GetMember(target, name));
    }

    private static void _SetNumericMember(object target, string name, double value)
    {
        var property = _GetProperty(target, name);
        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        property.SetValue(target, Convert.ChangeType(value, type));
    }

    private static void _AddNumericMember(object target, string name, double delta)
    {
        _SetNumericMember(target, name, _GetNumericMember(target, name) + delta);
    }

    private static object? _TryFindResource(string key)
    {
        try
        {
            var application = Application.Current;
            return application is not null && application.TryFindResource(key, out var value)
                ? value
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static object _FindResource(string key)
    {
        return _TryFindResource(key)
            ?? throw new InvalidOperationException($"找不到资源：{key}");
    }

    #endregion
}