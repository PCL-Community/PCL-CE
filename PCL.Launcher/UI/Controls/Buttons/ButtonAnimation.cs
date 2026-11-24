using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace PCL.UI.Controls;

/// <summary>
///     按钮族（AN-3c）动画接线的共用辅助。
///     集中“是否播放动画”的判断：控件已加载、存在 TopLevel、未被页面切换抑制，
///     且动画服务未关闭（<c>Config.Debug.AnimationSpeed = 0</c> 时直接落到终值，
///     而不是让引擎把动画拉长 10 倍）。颜色统一从核心 <c>ColorBrush*</c> 动态资源解析，
///     不在控件里写死颜色。
/// </summary>
internal static class ButtonAnimation
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

    /// <summary>生成带控件编号的动画组名，避免同一控件的状态动画互相踩踏。</summary>
    internal static string Name(string prefix, int uuid)
    {
        return $"{prefix} {uuid}";
    }

    /// <summary>把资源键解析为可插值颜色；资源缺失或非纯色画刷时返回 <c>null</c>。</summary>
    internal static NColor? ThemeColor(StyledElement element, string key)
    {
        if (!element.TryFindResource(key, out var resource) || resource is null)
            return null;

        return resource switch
        {
            ISolidColorBrush brush => new NColor(brush.Color),
            Color color => new NColor(color),
            NColor color => color,
            _ => null
        };
    }

    /// <summary>读取控件当前颜色，非纯色画刷按透明处理。</summary>
    internal static NColor ColorOf(AvaloniaObject target, AvaloniaProperty property)
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
    ///     把颜色动画到目标值；<paramref name="animate" /> 为 <c>false</c> 或资源缺失时直接赋终值。
    /// </summary>
    internal static void ApplyColor(
        Control control,
        AvaloniaProperty property,
        string key,
        NColor? literal,
        int time,
        bool animate,
        string name,
        IEasing? ease = null)
    {
        var target = literal ?? ThemeColor(control, key);
        if (target is null)
        {
            control.ClearValue(property);
            return;
        }

        if (animate && CanAnimate(control))
        {
            var delta = target.Value - ColorOf(control, property);
            AnimationService.Run(
                new ColorAnimation(control, property, delta, time, ease: ease),
                name);
            return;
        }

        control.SetValue(property, new SolidColorBrush((Color)target));
    }

    /// <summary>读取控件当前的缩放比例（非 <see cref="ScaleTransform" /> 时按 1 处理）。</summary>
    internal static double CurrentScale(Visual visual)
    {
        return visual.RenderTransform is ScaleTransform transform ? transform.ScaleX : 1d;
    }

    /// <summary>直接设置控件缩放，供动画关闭时立即落到终值。</summary>
    internal static void SetScale(Visual visual, double scale)
    {
        visual.RenderTransformOrigin = RelativePoint.Center;
        if (visual.RenderTransform is ScaleTransform transform)
        {
            transform.ScaleX = scale;
            transform.ScaleY = scale;
            return;
        }

        visual.RenderTransform = new ScaleTransform(scale, scale);
    }

    /// <summary>以“目标值 - 当前值”的增量播放缩放动画；已到位时不做任何事。</summary>
    internal static void AnimateScale(
        Control control,
        double target,
        int time,
        string name,
        int delay = 0,
        IEasing? ease = null)
    {
        if (!CanAnimate(control))
        {
            SetScale(control, target);
            return;
        }

        var delta = target - CurrentScale(control);
        if (Math.Abs(delta) < 0.0001d)
            return;

        control.RenderTransformOrigin = RelativePoint.Center;
        AnimationService.Run(
            new ScaleTransformAnimation(control, delta, time, delay, ease),
            name);
    }
}