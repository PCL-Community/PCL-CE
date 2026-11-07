using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PCL.UI.Animations;
using MyColor = PCL.UI.Controls.Common.MyColor;

namespace PCL.UI.Controls;

/// <summary>
///     输入/选择控件共用的动画接线：颜色、透明度、尺寸与缩放过渡。
///     动画关闭（<c>Config.Debug.AnimationSpeed = 0</c>）、控件尚未加载或页面正在切换时
///     直接写入终值，不启动动画组。
/// </summary>
internal static class InputAnimation
{
    /// <summary>动画总开关，与 <see cref="AnimationService" /> 使用同一份配置。</summary>
    internal static bool IsEnabled => AnimationService.IsEnabled;

    /// <summary>控件当前是否适合播放动画。</summary>
    internal static bool CanAnimate(Control control)
    {
        return IsEnabled
            && control.IsLoaded
            && TopLevel.GetTopLevel(control) is not null
            && !PCL.Animations.AniExitsAnyControl();
    }

    /// <summary>生成带控件编号的动画组名，避免同一控件的状态动画互相覆盖。</summary>
    internal static string Key(string prefix, int uuid)
    {
        return $"{prefix} {uuid}";
    }

    /// <summary>停止若干动画组；未启动过的组名会被忽略。</summary>
    internal static void Stop(params string[] names)
    {
        foreach (var name in names)
            PCL.Animations.AniStop(name);
    }

    /// <summary>读取控件当前颜色，非纯色画刷按全透明处理。</summary>
    internal static MyColor ReadColor(AvaloniaObject target, AvaloniaProperty property)
    {
        return target.GetValue(property) switch
        {
            ISolidColorBrush brush => new MyColor(brush.Color),
            Color color => new MyColor(color),
            MyColor color => color,
            _ => new MyColor(0d, 0d, 0d, 0d)
        };
    }

    /// <summary>
    ///     把颜色属性动画到资源键对应的颜色；<paramref name="animate" /> 为 <c>false</c>、
    ///     资源缺失或控件不可动画时直接赋终值。
    /// </summary>
    internal static void Color(
        Control target,
        AvaloniaProperty property,
        string key,
        IBrush fallback,
        int time,
        string name,
        bool animate = true,
        PCL.Animations.AniEase? ease = null)
    {
        if (animate && InputTheme.TryColor(target, key, out var targetColor) && CanAnimate(target))
        {
            AnimateColor(target, property, targetColor, time, name, ease);
            return;
        }

        PCL.Animations.AniStop(name);
        target.SetValue(property, InputTheme.Brush(target, key, fallback));
    }

    /// <summary>
    ///     把颜色属性动画到固定颜色；<paramref name="animate" /> 为 <c>false</c> 或控件不可动画时直接赋终值。
    /// </summary>
    internal static void ColorLiteral(
        Control target,
        AvaloniaProperty property,
        Color color,
        int time,
        string name,
        bool animate = true,
        PCL.Animations.AniEase? ease = null)
    {
        if (animate && CanAnimate(target))
        {
            AnimateColor(target, property, new MyColor(color), time, name, ease);
            return;
        }

        PCL.Animations.AniStop(name);
        target.SetValue(property, new SolidColorBrush(color));
    }

    private static void AnimateColor(
        Control target,
        AvaloniaProperty property,
        MyColor targetColor,
        int time,
        string name,
        PCL.Animations.AniEase? ease)
    {
        var delta = targetColor - ReadColor(target, property);
        if (Math.Abs(delta.a) < 0.001d
            && Math.Abs(delta.r) < 0.001d
            && Math.Abs(delta.g) < 0.001d
            && Math.Abs(delta.b) < 0.001d)
            return;

        PCL.Animations.AniStart(
            PCL.Animations.AaColor(target, property, delta, time, 0, ease),
            name);
    }

    /// <summary>透明度过渡；不可动画时直接赋终值。</summary>
    internal static void Opacity(
        Control target,
        double value,
        int time,
        string name,
        bool animate = true,
        int delay = 0,
        PCL.Animations.AniEase? ease = null)
    {
        if (animate && CanAnimate(target))
        {
            var delta = value - target.Opacity;
            if (Math.Abs(delta) < 0.0001d)
                return;
            PCL.Animations.AniStart(
                PCL.Animations.AaOpacity(target, delta, time, delay, ease),
                name);
            return;
        }

        PCL.Animations.AniStop(name);
        target.Opacity = value;
    }

    /// <summary>宽度过渡；不可动画时直接赋终值。</summary>
    internal static void Width(
        Control target,
        double value,
        int time,
        string name,
        bool animate = true,
        PCL.Animations.AniEase? ease = null)
    {
        if (animate && CanAnimate(target))
        {
            var delta = value - (double.IsNaN(target.Width) ? target.Bounds.Width : target.Width);
            if (Math.Abs(delta) < 0.0001d)
                return;
            PCL.Animations.AniStart(
                PCL.Animations.AaWidth(target, delta, time, 0, ease),
                name);
            return;
        }

        PCL.Animations.AniStop(name);
        target.Width = value;
    }

    /// <summary>水平位移过渡（左对齐控件按左边距处理）；不可动画时直接赋终值。</summary>
    internal static void X(
        Control target,
        double value,
        int time,
        string name,
        bool animate = true,
        PCL.Animations.AniEase? ease = null)
    {
        if (animate && CanAnimate(target))
        {
            var delta = value - target.Margin.Left;
            if (Math.Abs(delta) < 0.0001d)
                return;
            PCL.Animations.AniStart(
                PCL.Animations.AaX(target, delta, time, 0, ease),
                name);
            return;
        }

        PCL.Animations.AniStop(name);
        var margin = target.Margin;
        target.Margin = new Thickness(value, margin.Top, margin.Right, margin.Bottom);
    }

    /// <summary>渲染缩放过渡；不可动画时直接赋终值。</summary>
    internal static void Scale(
        Control target,
        double value,
        int time,
        string name,
        bool animate = true,
        int delay = 0,
        PCL.Animations.AniEase? ease = null)
    {
        if (animate && CanAnimate(target))
        {
            var delta = value - CurrentScale(target);
            if (Math.Abs(delta) < 0.0001d)
                return;
            PCL.Animations.AniStart(
                PCL.Animations.AaScaleTransform(target, delta, time, delay, ease),
                name);
            return;
        }

        PCL.Animations.AniStop(name);
        SetScale(target, value);
    }

    /// <summary>读取控件当前的渲染缩放；非 <see cref="ScaleTransform" /> 时按 1 处理。</summary>
    internal static double CurrentScale(Control target)
    {
        return target.RenderTransform is ScaleTransform transform ? transform.ScaleX : 1d;
    }

    /// <summary>直接写入渲染缩放，供动画关闭时立即落到终值。</summary>
    internal static void SetScale(Control target, double scale)
    {
        if (target.RenderTransform is ScaleTransform transform)
        {
            transform.ScaleX = scale;
            transform.ScaleY = scale;
            return;
        }

        target.RenderTransformOrigin = RelativePoint.Center;
        target.RenderTransform = new ScaleTransform(scale, scale);
    }
}