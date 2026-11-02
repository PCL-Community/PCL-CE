using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace PCL;

public static partial class Animations
{
    /// <summary>
    ///     淡入：把控件透明度从 0 动画到 1。
    /// </summary>
    public static AniData AaFadeIn(
        Visual target,
        int time = 200,
        int delay = 0,
        AniEase? ease = null,
        bool after = false)
    {
        target.Opacity = 0d;
        return AaOpacity(target, 1d, time, delay, ease ?? new AniEaseOutFluent(), after);
    }

    /// <summary>
    ///     淡出：把控件透明度动画到 0。
    /// </summary>
    public static AniData AaFadeOut(
        Visual target,
        int time = 150,
        int delay = 0,
        AniEase? ease = null,
        bool after = false)
    {
        return AaOpacity(
            target,
            -target.Opacity,
            time,
            delay,
            ease ?? new AniEaseOutFluent(),
            after);
    }

    /// <summary>
    ///     展开/收起高度：先设置起始高度，再把高度动画到目标高度。
    /// </summary>
    public static AniData AaExpandHeight(
        Control target,
        double fromHeight,
        double toHeight,
        int time = 240,
        int delay = 0,
        AniEase? ease = null,
        bool after = false)
    {
        target.Height = fromHeight;
        return AaHeight(
            target,
            toHeight - fromHeight,
            time,
            delay,
            ease ?? new AniEaseOutFluent(),
            after);
    }

    /// <summary>
    ///     收起高度：把高度动画到 0。
    /// </summary>
    public static AniData AaCollapseHeight(
        Control target,
        double fromHeight,
        int time = 200,
        int delay = 0,
        AniEase? ease = null,
        bool after = false)
    {
        return AaExpandHeight(
            target,
            fromHeight,
            0d,
            time,
            delay,
            ease ?? new AniEaseInFluent(),
            after);
    }

    /// <summary>
    ///     页面进入转场：透明度和水平位移同时进行。
    /// </summary>
    public static List<AniData> AaPageEnter(
        Control page,
        bool fromRight = true,
        int time = 240,
        AniEase? ease = null)
    {
        const double distance = 24d;
        var direction = fromRight ? 1d : -1d;
        var easeValue = ease ?? new AniEaseOutFluent();

        page.Opacity = 0d;
        if (page.RenderTransform is not TranslateTransform transform)
        {
            transform = new TranslateTransform();
            page.RenderTransform = transform;
        }

        transform.X = distance * direction;

        return
        [
            AaOpacity(page, 1d, time, 0, easeValue),
            AaTranslateX(page, -distance * direction, time, 0, easeValue)
        ];
    }

    /// <summary>
    ///     页面离开转场：透明度和水平位移同时进行。
    /// </summary>
    public static List<AniData> AaPageLeave(
        Control page,
        bool toRight = true,
        int time = 180,
        AniEase? ease = null)
    {
        const double distance = 24d;
        var direction = toRight ? 1d : -1d;
        var easeValue = ease ?? new AniEaseInFluent();

        return
        [
            AaOpacity(page, -page.Opacity, time, 0, easeValue),
            AaTranslateX(page, distance * direction, time, 0, easeValue)
        ];
    }

    /// <summary>
    ///     按钮按下反馈：先缩小再弹回。假设控件当前缩放为 1。
    /// </summary>
    public static List<AniData> AaButtonPress(Control button, double scale = 0.94d)
    {
        return
        [
            AaScaleTransform(button, scale - 1d, 90, 0, new AniEaseOutFluent()),
            AaScaleTransform(button, 1d - scale, 180, 0, new AniEaseOutCar(), true)
        ];
    }
}