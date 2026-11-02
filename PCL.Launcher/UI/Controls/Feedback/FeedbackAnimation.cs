using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PCL.UI.Animations;
using MyColor = PCL.UI.Controls.Common.MyColor;

namespace PCL.UI.Controls;

/// <summary>
///     反馈类控件（MyHint / MyToast / MyLoading，AN-3c）共用的动画辅助。
///     与按钮族保持同一套判断：控件已加载、存在 TopLevel、页面切换未抑制动画，
///     且 <c>Config.Debug.AnimationSpeed != 0</c>；关闭动画时调用方直接落到终值。
/// </summary>
internal static class FeedbackAnimation
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

    /// <summary>生成带控件编号的动画组名。</summary>
    internal static string Name(string prefix, object suffix)
    {
        return $"{prefix} {suffix}";
    }

    /// <summary>把资源键解析为可插值颜色；资源缺失或非纯色画刷时返回 <c>null</c>。</summary>
    internal static MyColor? ThemeColor(StyledElement element, string key)
    {
        if (!element.TryFindResource(key, out var resource) || resource is null)
            return null;

        return resource switch
        {
            ISolidColorBrush brush => new MyColor(brush.Color),
            Color color => new MyColor(color),
            MyColor color => color,
            _ => null
        };
    }

    /// <summary>读取控件当前颜色，非纯色画刷按透明处理。</summary>
    internal static MyColor ColorOf(AvaloniaObject target, AvaloniaProperty property)
    {
        return target.GetValue(property) switch
        {
            ISolidColorBrush brush => new MyColor(brush.Color),
            Color color => new MyColor(color),
            MyColor color => color,
            _ => new MyColor(0d, 0d, 0d, 0d)
        };
    }

    /// <summary>生成“从当前颜色动画到 <paramref name="target" />”的动画数据。</summary>
    internal static PCL.Animations.AniData ColorTo(
        Control control,
        AvaloniaProperty property,
        MyColor target,
        int time,
        PCL.Animations.AniEase? ease = null)
    {
        return PCL.Animations.AaColor(
            control,
            property,
            target - ColorOf(control, property),
            time,
            ease: ease);
    }

    /// <summary>读取当前缩放比例（非 <see cref="ScaleTransform" /> 时按 1 处理）。</summary>
    internal static double CurrentScale(Visual visual)
    {
        return visual.RenderTransform is ScaleTransform transform ? transform.ScaleX : 1d;
    }

    /// <summary>读取当前旋转角度（非 <see cref="RotateTransform" /> 时按 0 处理）。</summary>
    internal static double CurrentAngle(Visual visual)
    {
        return visual.RenderTransform is RotateTransform transform ? transform.Angle : 0d;
    }
}