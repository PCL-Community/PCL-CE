using System.Diagnostics;
using System.Globalization;
using Avalonia;
using PCL.Core.Logging;

namespace PCL.UI.SystemIntegration;

internal sealed class LinuxTaskbarProgress : ITaskbarProgressBackend
{
    public static readonly LinuxTaskbarProgress Instance = new();

    private readonly Lock _sync = new();
    private bool _dirty;
    private double _pendingProgress;
    private TaskbarProgressState _pendingState;
    private bool _running;
    private bool _unavailable;

    public void Update(TaskbarProgressState state, double progress)
    {
        lock (_sync)
        {
            _pendingState = state;
            _pendingProgress = progress;
            _dirty = true;
            if (_running) return;
            _running = true;
        }

        _ = Task.Run(async () =>
        {
            while (true)
            {
                TaskbarProgressState pendingState;
                double pendingProgress;
                lock (_sync)
                {
                    if (!_dirty)
                    {
                        _running = false;
                        return;
                    }

                    pendingState = _pendingState;
                    pendingProgress = _pendingProgress;
                    _dirty = false;
                }

                UpdateCore(pendingState, pendingProgress);
                await Task.Delay(120);
            }
        });
    }

    public void SetThumbnailClip(Thickness margin)
    {
    }

    private void UpdateCore(TaskbarProgressState state, double progress)
    {
        if (_unavailable) return;
        try
        {
            var visible = state is TaskbarProgressState.Normal
                or TaskbarProgressState.Error
                or TaskbarProgressState.Paused
                or TaskbarProgressState.Indeterminate;
            var value = state == TaskbarProgressState.Indeterminate ? 0d : progress;

            var info = new ProcessStartInfo
            {
                FileName = "gdbus",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            info.ArgumentList.Add("call");
            info.ArgumentList.Add("--session");
            info.ArgumentList.Add("--dest");
            info.ArgumentList.Add("com.canonical.Unity");
            info.ArgumentList.Add("--object-path");
            info.ArgumentList.Add("/com/canonical/Unity/LauncherEntry");
            info.ArgumentList.Add("--method");
            info.ArgumentList.Add("com.canonical.Unity.LauncherEntry.Update");
            info.ArgumentList.Add("application://pcl-community-edition.desktop");
            info.ArgumentList.Add(
                $"{{'progress': <double {value.ToString(CultureInfo.InvariantCulture)}>, 'progress-visible': <{visible.ToString().ToLowerInvariant()}>}}");

            using var process = Process.Start(info);
            if (process is null)
            {
                _unavailable = true;
                return;
            }

            process.WaitForExit(800);
            if (process.ExitCode != 0)
            {
                _unavailable = true;
                LogWrapper.Info("UI", "当前桌面环境不支持 Unity LauncherEntry 任务栏进度");
            }
        }
        catch (Exception ex)
        {
            _unavailable = true;
            LogWrapper.Info("UI", $"任务栏进度不可用：{ex.Message}");
        }
    }
}