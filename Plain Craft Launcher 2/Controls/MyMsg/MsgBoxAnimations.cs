using PCL.Core.UI;
using System.Windows;
using System.Windows.Media;

namespace PCL.Controls.MyMsg;

/// <summary>
/// MsgBox 控件的 Show / Close 动画静态实现。
/// 所有 MyMsg* 控件通过此类执行统一动画，避免在每个控件中重复 30+ 行动画代码。
/// </summary>
public static class MsgBoxAnimations
{
    public static void AnimateShow(
        UIElement element,
        TranslateTransform pos,
        RotateTransform rot,
        MsgBoxAnimationProfile profile,
        string animationGroup)
    {
        element.Opacity = 0d;
        ModAnimation.AniStart((ModAnimation.AniData[])
        [
            ModAnimation.AaOpacity(element, 1d, (int)profile.ShowFadeMs, (int)profile.ShowDelayMs),
            ModAnimation.AaDouble(i => pos.Y += (double)i,
                -pos.Y, (int)profile.ShowSlideMs, (int)profile.ShowDelayMs, profile.ShowSlideEase),
            ModAnimation.AaDouble(i => rot.Angle += (double)i,
                -rot.Angle, (int)profile.ShowRotateMs, (int)profile.ShowDelayMs, profile.ShowRotateEase)
        ], animationGroup);
    }

    public static Task AnimateCloseAsync(
        UIElement element,
        TranslateTransform pos,
        RotateTransform rot,
        MsgBoxAnimationProfile profile,
        string animationGroup)
    {
        var tcs = new TaskCompletionSource();
        ModAnimation.AniStart((ModAnimation.AniData[])
        [
            ModAnimation.AaOpacity(element, -element.Opacity,
                (int)profile.CloseFadeMs, (int)profile.CloseFadeDelayMs),
            ModAnimation.AaDouble(i => pos.Y += (double)i,
                profile.CloseSlideDistance - pos.Y,
                (int)profile.CloseSlideMs, 0, profile.CloseSlideEase),
            ModAnimation.AaDouble(i => rot.Angle += (double)i,
                profile.CloseAngle - rot.Angle,
                (int)profile.CloseSlideMs, 0, profile.CloseRotateEase),
            ModAnimation.AaCode(tcs.SetResult, after: true)
        ], animationGroup);
        return tcs.Task;
    }
}
