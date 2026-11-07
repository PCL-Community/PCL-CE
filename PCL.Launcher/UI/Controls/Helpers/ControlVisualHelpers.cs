using Avalonia;
using Avalonia.Controls;

namespace PCL.UI.Controls;

/// <summary>
///     控件视觉状态辅助：判断当前是否适合播放颜色过渡，并在动画与直接设置资源之间切换。
/// </summary>
internal static class ControlVisualHelpers
{
    internal static bool ShouldAnimate(Control control, object? animationOverride = null)
    {
        return TopLevel.GetTopLevel(control) is not null
            && !PCL.Animations.AniExitsAnyControl()
            && !false.Equals(animationOverride);
    }

    internal static void AnimateColorOrSetResource(
        Control target,
        AvaloniaProperty property,
        string resourceKey,
        int duration,
        string animationKey,
        bool shouldAnimate)
    {
        if (shouldAnimate)
        {
            PCL.Animations.AniStart(
                PCL.Animations.AaColor(target, property, resourceKey, duration),
                animationKey);
            return;
        }

        PCL.Animations.AniStop(animationKey);
        if (Application.Current is { } app
            && app.TryFindResource(resourceKey, out var value))
            target.SetValue(property, value);
    }
}