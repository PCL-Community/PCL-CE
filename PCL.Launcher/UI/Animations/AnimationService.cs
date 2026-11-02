using Avalonia;
using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using static PCL.Animations;

namespace PCL.UI.Animations;

/// <summary>
///     按配置里的动画速度折算时长的淡入淡出辅助。
///     动画由 <see cref="PCL.Animations" /> 播放；时长取 <see cref="Config.Debug" /> 的动画速度做除数，速度越大时长越短，
///     速度为 0 或达到 30 以上时直接跳到终值。
/// </summary>
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

    public static Task FadeAsync(
        Visual target,
        double from,
        double to,
        double baseMilliseconds = 180)
    {
        // AnimationSpeed = 0 或 >= 30 时立即完成
        if (!IsEnabled || Speed >= 200d)
        {
            target.Opacity = to;
            return Task.CompletedTask;
        }

        var duration = Math.Max(1, (int)Math.Round(baseMilliseconds / Speed));
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        if (Dispatcher.UIThread.CheckAccess())
            Start();
        else
            Dispatcher.UIThread.Post(Start);

        return tcs.Task;

        void Start()
        {
            target.Opacity = from;
            AniStart(
                new List<AniData>
                {
                    AaOpacity(target, to - from, duration, 0, new AniEaseOutFluent()),
                    AaCode(() => tcs.TrySetResult(), after: true)
                });
        }
    }
}
