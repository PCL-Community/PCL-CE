using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Styling;
using PCL.Core.App;
using PCL.Core.App.Configuration;

namespace PCL.UI.Animations;

/// <summary>
///     与 WPF 端动画速度设置保持一致的动画辅助。
///     时长按 <see cref="Config.Debug" /> 中动画速度折算，速度越大时长越短。
/// </summary>
// TODO: 尚未移植 WPF 的动画引擎与 Animations API（控件级过渡、页面转场、展开/收起等），
// 当前仅有速度映射与页面淡入；后续页面迁移时按控件/页面需求补齐原语，不移植 WPF 引擎本身。
public static class AnimationService
{
    private const int DefaultSpeedValue = 9;

    public static int SpeedValue =>
        ConfigService.IsInitialized ? Config.Debug.AnimationSpeed : DefaultSpeedValue;

    public static bool IsEnabled => SpeedValue > 0;

    public static double Speed =>
        SpeedValue >= 30
            ? 200d
            : Math.Clamp(SpeedValue * 0.1d + 0.1d, 0.1d, 3d);

    public static TimeSpan Duration(double baseMilliseconds)
    {
        return TimeSpan.FromMilliseconds(baseMilliseconds / Speed);
    }

    public static Task FadeInAsync(Visual target, double baseMilliseconds = 180)
    {
        return FadeAsync(target, 0d, 1d, baseMilliseconds);
    }

    public static Task FadeOutAsync(Visual target, double baseMilliseconds = 150)
    {
        return FadeAsync(target, target.Opacity, 0d, baseMilliseconds);
    }

    public static async Task FadeAsync(
        Visual target,
        double from,
        double to,
        double baseMilliseconds = 180)
    {
        if (!IsEnabled || Speed >= 200d)
        {
            target.Opacity = to;
            return;
        }

        var animation = new Animation
        {
            Duration = Duration(baseMilliseconds),
            Easing = new CubicEaseOut(),
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0d),
                    Setters =
                    {
                        new Setter(Visual.OpacityProperty, from)
                    }
                },
                new KeyFrame
                {
                    Cue = new Cue(1d),
                    Setters =
                    {
                        new Setter(Visual.OpacityProperty, to)
                    }
                }
            }
        };

        await animation.RunAsync(target);
        target.Opacity = to;
    }
}