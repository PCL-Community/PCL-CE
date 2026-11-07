using System.Runtime.InteropServices;
using Avalonia;
using PCL.Core.Logging;

namespace PCL.UI.SystemIntegration;

internal sealed class WindowsTaskbarProgress : ITaskbarProgressBackend
{
    public static readonly WindowsTaskbarProgress Instance = new();

    private bool _failed;
    private ITaskbarList3? _taskbar;

    public void Update(TaskbarProgressState state, double progress)
    {
        if (_failed) return;
        try
        {
            var handle = TaskbarProgress.MainWindow?.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
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
            _failed = true;
            LogWrapper.Warn("UI", $"Windows 任务栏进度不可用：{ex.Message}");
        }
    }

    public void SetThumbnailClip(Thickness margin)
    {
        if (_failed) return;
        try
        {
            var window = TaskbarProgress.MainWindow;
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
            _failed = true;
            LogWrapper.Warn("UI", $"Windows 任务栏缩略图裁剪不可用：{ex.Message}");
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
    private class TaskbarList;

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