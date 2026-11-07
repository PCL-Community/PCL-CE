using Avalonia;
using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.App.Configuration;

namespace PCL.UI.Animations;

/// <summary>
///     按配置里的动画速度折算时长的淡入淡出辅助。
///     动画由 <see cref="PCL.Animations" /> 播放；速度倍率统一取 <see cref="PCL.Animations.AniSpeed" />
///     （配置 0 表示无动画，30 及以上表示瞬时），速度越大时长越短。
/// </summary>
public static class AnimationService
{
    private const int DefaultSpeedValue = 9;

    public static int SpeedValue =>
        ConfigService.IsInitialized ? Config.Debug.AnimationSpeed : DefaultSpeedValue;

    public static bool IsEnabled => Speed > 0d;

    public static double Speed => PCL.Animations.AniSpeed;

    public static TimeSpan Duration(double baseMilliseconds)
    {
        var speed = Speed;
        return speed <= 0d
            ? TimeSpan.Zero
            : TimeSpan.FromMilliseconds(baseMilliseconds / speed);
    }

    public static Task FadeInAsync(Visual target, double baseMilliseconds = 180)
    {
        return FadeAsync(target, 0d, 1d, baseMilliseconds);
    }

    public static Task FadeOutAsync(Visual target, double baseMilliseconds = 150)
    {
        return FadeAsync(target, target.Opacity, 0d, baseMilliseconds);
    }

    public static Task FadeAsync(
        Visual target,
        double from,
        double to,
        double baseMilliseconds = 180)
    {
        var speed = Speed;
        if (speed is <= 0d or >= 200d)
        {
            target.Opacity = to;
            return Task.CompletedTask;
        }

        var duration = Math.Max(1, (int)Math.Round(baseMilliseconds / speed));
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        if (Dispatcher.UIThread.CheckAccess())
            Start();
        else
            Dispatcher.UIThread.Post(Start);

        return tcs.Task;

        void Start()
        {
            target.Opacity = from;
            PCL.Animations.AniStart(
                new List<PCL.Animations.AniData>
                {
                    PCL.Animations.AaOpacity(
                        target,
                        to - from,
                        duration,
                        0,
                        new PCL.Animations.AniEaseOutFluent()),
                    PCL.Animations.AaCode(() => tcs.TrySetResult(), after: true)
                });
        }
    }
}