using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using PCL.Core.Platform;

namespace PCL.UI.SystemIntegration;

/// <summary>任务栏进度门面。具体平台由 backend 实现。</summary>
public static class TaskbarProgress
{
    private static TaskbarProgressState _lastState = TaskbarProgressState.None;
    private static double _lastProgress;

    private static ITaskbarProgressBackend Backend =>
        PlatformRuntime.Current.Kind switch
        {
            PlatformKind.Windows => WindowsTaskbarProgress.Instance,
            PlatformKind.Linux => LinuxTaskbarProgress.Instance,
            PlatformKind.MacOs => UnsupportedTaskbarProgressBackend.Instance,
            _ => throw new ArgumentOutOfRangeException()
        };

    internal static Window? MainWindow =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
        ?.MainWindow;

    public static void Update(TaskbarProgressState state, double progress)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => Update(state, progress));
            return;
        }

        progress = Math.Clamp(progress, 0d, 1d);
        if (state == _lastState && Math.Abs(progress - _lastProgress) < 0.001d) return;
        _lastState = state;
        _lastProgress = progress;
        Backend.Update(state, progress);
    }

    public static void SetThumbnailClip(Thickness margin)
    {
        Backend.SetThumbnailClip(margin);
    }
}