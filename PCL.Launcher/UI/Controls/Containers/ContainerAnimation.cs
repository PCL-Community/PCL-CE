using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PCL.UI.Animations;
using MyColor = PCL.UI.Controls.Common.MyColor;

namespace PCL.UI.Controls;

/// <summary>
///     容器类控件共用的动画判定与取值辅助。
///     把“已加载、存在 TopLevel、未被页面切换抑制”的判定集中到一处；
///     动画关闭时统一走瞬时赋值。
/// </summary>
internal static class ContainerAnimation
{
    /// <summary>动画总开关。</summary>
    internal static bool IsEnabled => AnimationService.IsEnabled;

    /// <summary>控件当前是否适合播放动画：已加载、存在 TopLevel、未被页面切换抑制、动画未关闭。</summary>
    internal static bool CanAnimate(Control control)
    {
        return CanAnimatePage(control)
            && !PCL.Animations.AniExitsAnyControl();
    }

    /// <summary>页面级显式动画：只检查动画总开关/加载状态，不受 AniBeginControl 抑制。</summary>
    internal static bool CanAnimatePage(Control control)
    {
        return IsEnabled
            && control.IsLoaded
            && TopLevel.GetTopLevel(control) is not null;
    }

    /// <summary>把画刷转成可插值的颜色；非纯色画刷按透明处理。</summary>
    internal static MyColor ColorOf(IBrush? brush)
    {
        return brush is ISolidColorBrush
            ? new MyColor(brush)
            : new MyColor(0d, 0d, 0d, 0d);
    }

    /// <summary>按资源键取页面主题画刷并转成可插值的颜色。</summary>
    internal static MyColor ThemeColor(StyledElement element, string key, IBrush fallback)
    {
        return ColorOf(ContainerTheme.Brush(element, key, fallback));
    }

    /// <summary>确保控件使用 <see cref="ScaleTransform" />，返回可读写的缩放对象。</summary>
    internal static ScaleTransform EnsureScale(
        Visual visual,
        double scaleX = 1d,
        double scaleY = 1d)
    {
        if (visual.RenderTransform is ScaleTransform scale)
            return scale;

        scale = new ScaleTransform(scaleX, scaleY);
        visual.RenderTransformOrigin = RelativePoint.Center;
        visual.RenderTransform = scale;
        return scale;
    }

    /// <summary>确保控件使用 <see cref="RotateTransform" />，返回可读写的旋转对象。</summary>
    internal static RotateTransform EnsureRotate(Visual visual, double angle = 0d)
    {
        if (visual.RenderTransform is RotateTransform rotate)
            return rotate;

        rotate = new RotateTransform(angle);
        visual.RenderTransformOrigin = RelativePoint.Center;
        visual.RenderTransform = rotate;
        return rotate;
    }
}