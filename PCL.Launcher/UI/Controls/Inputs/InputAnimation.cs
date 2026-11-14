using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

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
            && !AnimationService.IsSuspended;
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
            AnimationService.CancelAnimationByName(name);
    }

    /// <summary>读取控件当前颜色，非纯色画刷按全透明处理。</summary>
    internal static NColor ReadColor(AvaloniaObject target, AvaloniaProperty property)
    {
        return target.GetValue(property) switch
        {
            ISolidColorBrush brush => new NColor(brush.Color),
            Color color => new NColor(color),
            NColor color => color,
            _ => new NColor(0d, 0d, 0d, 0d)
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
        IEasing? ease = null)
    {
        if (animate && InputTheme.TryColor(target, key, out var targetColor) && CanAnimate(target))
        {
            AnimateColor(target, property, targetColor, time, name, ease);
            return;
        }

        AnimationService.CancelAnimationByName(name);
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
        IEasing? ease = null)
    {
        if (animate && CanAnimate(target))
        {
            AnimateColor(target, property, new NColor(color), time, name, ease);
            return;
        }

        AnimationService.CancelAnimationByName(name);
        target.SetValue(property, new SolidColorBrush(color));
    }

    private static void AnimateColor(
        Control target,
        AvaloniaProperty property,
        NColor targetColor,
        int time,
        string name,
        IEasing? ease)
    {
        var delta = targetColor - ReadColor(target, property);
        if (Math.Abs(delta.A) < 0.001d
            && Math.Abs(delta.R) < 0.001d
            && Math.Abs(delta.G) < 0.001d
            && Math.Abs(delta.B) < 0.001d)
            return;

        AnimationService.Run(
            new ColorAnimation(target, property, delta, time, 0, ease),
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
        IEasing? ease = null)
    {
        if (animate && CanAnimate(target))
        {
            var delta = value - target.Opacity;
            if (Math.Abs(delta) < 0.0001d)
                return;
            AnimationService.Run(
                new OpacityAnimation(target, delta, time, delay, ease),
                name);
            return;
        }

        AnimationService.CancelAnimationByName(name);
        target.Opacity = value;
    }

    /// <summary>宽度过渡；不可动画时直接赋终值。</summary>
    internal static void Width(
        Control target,
        double value,
        int time,
        string name,
        bool animate = true,
        IEasing? ease = null)
    {
        if (animate && CanAnimate(target))
        {
            var delta = value - (double.IsNaN(target.Width) ? target.Bounds.Width : target.Width);
            if (Math.Abs(delta) < 0.0001d)
                return;
            AnimationService.Run(
                new WidthAnimation(target, delta, time, 0, ease),
                name);
            return;
        }

        AnimationService.CancelAnimationByName(name);
        target.Width = value;
    }

    /// <summary>水平位移过渡（左对齐控件按左边距处理）；不可动画时直接赋终值。</summary>
    internal static void X(
        Control target,
        double value,
        int time,
        string name,
        bool animate = true,
        IEasing? ease = null)
    {
        if (animate && CanAnimate(target))
        {
            var delta = value - target.Margin.Left;
            if (Math.Abs(delta) < 0.0001d)
                return;
            AnimationService.Run(
                new XAnimation(target, delta, time, 0, ease),
                name);
            return;
        }

        AnimationService.CancelAnimationByName(name);
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
        IEasing? ease = null)
    {
        if (animate && CanAnimate(target))
        {
            var delta = value - CurrentScale(target);
            if (Math.Abs(delta) < 0.0001d)
                return;
            AnimationService.Run(
                new ScaleTransformAnimation(target, delta, time, delay, ease),
                name);
            return;
        }

        AnimationService.CancelAnimationByName(name);
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