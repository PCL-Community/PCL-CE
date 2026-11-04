using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using PCL.Core.Logging;

namespace PCL.UI.SystemIntegration;

/// <summary>任务栏进度：Windows 使用 ITaskbarList3，Linux 使用 Unity LauncherEntry，其它平台降级为无操作。</summary>
public static class TaskbarProgress
{
    private static TaskbarProgressState _lastState = TaskbarProgressState.None;
    private static double _lastProgress;
    private static bool _windowsFailed;
    private static bool _linuxUnavailable;
    private static ITaskbarList3? _taskbar;

    private static Window? MainWindow =>
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

        if (OperatingSystem.IsWindows())
        {
            _UpdateWindows(state, progress);
            return;
        }

        if (OperatingSystem.IsLinux())
            _UpdateLinux(state, progress);
    }

    public static void SetThumbnailClip(Thickness margin)
    {
        if (!OperatingSystem.IsWindows() || _windowsFailed) return;
        try
        {
            var window = MainWindow;
            var handle = window?.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (handle == IntPtr.Zero || window is null) return;

            var scale = window.RenderScaling <= 0d ? 1d : window.RenderScaling;
            var rect = new Rect(
                margin.Left * scale,
                margin.Top * scale,
                Math.Max(0d, window.ClientSize.Width - margin.Left - margin.Right) * scale,
                Math.Max(0d, window.ClientSize.Height - margin.Top - margin.Bottom) * scale);

            _taskbar ??= (ITaskbarList3)new TaskbarList();
            _taskbar.HrInit();
            var nativeRect = new NativeRect
            {
                Left = (int)Math.Round(rect.X),
                Top = (int)Math.Round(rect.Y),
                Right = (int)Math.Round(rect.Right),
                Bottom = (int)Math.Round(rect.Bottom)
            };
            _taskbar.SetThumbnailClip(handle, ref nativeRect);
        }
        catch (Exception ex)
        {
            _windowsFailed = true;
            LogWrapper.Warn("UI", $"Windows 任务栏缩略图裁剪不可用：{ex.Message}");
        }
    }

    private static void _UpdateWindows(TaskbarProgressState state, double progress)
    {
        if (_windowsFailed) return;
        try
        {
            var handle = MainWindow?.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (handle == IntPtr.Zero) return;

            _taskbar ??= (ITaskbarList3)new TaskbarList();
            _taskbar.HrInit();
            _taskbar.SetProgressState(
                handle,
                state switch
                {
                    TaskbarProgressState.Normal => TbpFlag.Normal,
                    TaskbarProgressState.Error => TbpFlag.Error,
                    TaskbarProgressState.Paused => TbpFlag.Paused,
                    TaskbarProgressState.Indeterminate => TbpFlag.Indeterminate,
                    _ => TbpFlag.NoProgress
                });

            if (state is TaskbarProgressState.Normal
                or TaskbarProgressState.Error
                or TaskbarProgressState.Paused)
                _taskbar.SetProgressValue(handle, (ulong)(progress * 1000d), 1000UL);
        }
        catch (Exception ex)
        {
            _windowsFailed = true;
            LogWrapper.Warn("UI", $"Windows 任务栏进度不可用：{ex.Message}");
        }
    }

    private static void _UpdateLinux(TaskbarProgressState state, double progress)
    {
        if (_linuxUnavailable) return;
        try
        {
            var visible = state is TaskbarProgressState.Normal
                or TaskbarProgressState.Error
                or TaskbarProgressState.Paused
                or TaskbarProgressState.Indeterminate;
            var value = state switch
            {
                TaskbarProgressState.Indeterminate => 0d,
                _ => progress
            };

            var startInfo = new ProcessStartInfo
            {
                FileName = "gdbus",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            startInfo.ArgumentList.Add("call");
            startInfo.ArgumentList.Add("--session");
            startInfo.ArgumentList.Add("--dest");
            startInfo.ArgumentList.Add("com.canonical.Unity");
            startInfo.ArgumentList.Add("--object-path");
            startInfo.ArgumentList.Add("/com/canonical/Unity/LauncherEntry");
            startInfo.ArgumentList.Add("--method");
            startInfo.ArgumentList.Add("com.canonical.Unity.LauncherEntry.Update");
            startInfo.ArgumentList.Add("application://pcl-community-edition.desktop");
            startInfo.ArgumentList.Add(
                $"{{'progress': <double {value.ToString(CultureInfo.InvariantCulture)}>, 'progress-visible': <{visible.ToString().ToLowerInvariant()}>}}");

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                _linuxUnavailable = true;
                return;
            }

            process.WaitForExit(800);
            if (process.ExitCode != 0)
            {
                _linuxUnavailable = true;
                LogWrapper.Info("UI", "当前桌面环境不支持 Unity LauncherEntry 任务栏进度");
            }
        }
        catch (Exception ex)
        {
            _linuxUnavailable = true;
            LogWrapper.Info("UI", $"任务栏进度不可用：{ex.Message}");
        }
    }

    private enum TbpFlag
    {
        NoProgress = 0,
        Indeterminate = 1,
        Normal = 2,
        Error = 4,
        Paused = 8
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [ComImport]
    [Guid("56FDF344-FD6D-11D0-958A-006097C9A090")]
    [ClassInterface(ClassInterfaceType.None)]
    private class TaskbarList
    {
    }

    [ComImport]
    [Guid("EA1AFB91-9E28-4B86-90E9-9E9F8A5EEFAF")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITaskbarList3
    {
        void HrInit();
        void AddTab(IntPtr hwnd);
        void DeleteTab(IntPtr hwnd);
        void ActivateTab(IntPtr hwnd);
        void SetActiveAlt(IntPtr hwnd);
        void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fullscreen);
        void SetProgressValue(IntPtr hwnd, ulong completed, ulong total);
        void SetProgressState(IntPtr hwnd, TbpFlag flags);
        void SetThumbnailClip(IntPtr hwnd, ref NativeRect prcClip);
    }
}