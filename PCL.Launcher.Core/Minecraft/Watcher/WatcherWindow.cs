using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using PCL.Core.App;
using PCL.Core.App.Localization;

namespace PCL;

public static partial class GameWatcher
{
    public partial class Watcher
    {
        private void ProgressUpdate()
        {
            var isLoadComplete = _isWindowAppeared || _logProgress >= 4;

            if (isLoadComplete)
            {
                WatcherLog(Lang.Text("Watcher.LoadComplete"));
                State = MinecraftState.Running;
            }

            loader.Progress = isLoadComplete
                ? 0.95d
                : Math.Min(_logProgress, 3) / 3d * 0.9d;
        }

        private void TimerWindow()
        {
            try
            {
                if (gameProcess.HasExited || _isWindowFinished)
                    return;

                var minecraftWindow = TryGetMinecraftWindowSafely();
                if (minecraftWindow is null)
                    return;

                HandleMinecraftWindow(minecraftWindow.Value);
            }
            catch (Exception ex)
            {
                LauncherCommon.Log(
                    ex,
                    "检查 Minecraft 窗口失败",
                    LauncherLogLevel.Feedback,
                    userSummary: Lang.Text("Minecraft.Launch.Error.WatcherOperationFailed"));
            }
        }

        private KeyValuePair<nint, string>? TryGetMinecraftWindowSafely()
        {
            try
            {
                return TryGetMinecraftWindow();
            }
            catch (Win32Exception ex)
            {
                LauncherCommon.Log(
                    ex,
                    Lang.Text("Watcher.SecurityBlocked"),
                    LauncherLogLevel.Hint,
                    userSummary: Lang.Text("Watcher.SecurityBlocked"));

                _isWindowFinished = true;
                return null;
            }
        }

        private void HandleMinecraftWindow(
            KeyValuePair<nint, string> minecraftWindow)
        {
            var windowHandle = minecraftWindow.Key;
            var windowTitle = minecraftWindow.Value;

            if (IsLoaderWindow(windowTitle))
            {
                if (!_isWindowAppeared)
                    WatcherLog(
                        Lang.Text(
                            "Watcher.FmlWindowLoaded",
                            windowTitle,
                            windowHandle.ToInt64()));
            }
            else
            {
                _windowHandle = windowHandle;

                WatcherLog(
                    Lang.Text(
                        "Watcher.WindowLoaded",
                        windowTitle,
                        windowHandle.ToInt64()));

                _isWindowFinished = true;

                if (Config.Launch.GameWindowMode == GameWindowSizeMode.Maximized)
                    ScheduleMinecraftWindowMaximize(windowHandle);
            }

            _isWindowAppeared = true;
        }

        private static bool IsLoaderWindow(string windowTitle)
        {
            return windowTitle.StartsWithF("FML")
                || windowTitle.StartsWithF("Quilt Loader");
        }

        private void ScheduleMinecraftWindowMaximize(nint windowHandle)
        {
            LauncherCommon.RunInNewThread(
                () =>
                {
                    try
                    {
                        Thread.Sleep(2000);
                        ShowWindow(_windowHandle, 3U);

                        WatcherLog(
                            Lang.Text(
                                "Watcher.WindowMaximized",
                                windowHandle.ToInt64()));
                    }
                    catch (Exception ex)
                    {
                        LauncherCommon.Log(ex, "最大化 Minecraft 窗口时出现错误");
                    }
                },
                "MinecraftWindowMaximize");
        }

        /// <summary>
        ///     获取可能是当前进程对应的 Minecraft 窗口的句柄和标题。
        ///     Nothing 代表未找到。
        /// </summary>
        private KeyValuePair<nint, string>? TryGetMinecraftWindow()
        {
            KeyValuePair<nint, string>? foundWindow = null;

            EnumWindows(
                (windowHandle, _) =>
                {
                    if (foundWindow is not null)
                        return false;

                    if (!TryGetMinecraftWindowInfo(
                            windowHandle,
                            out var windowTitle))
                        return true;

                    foundWindow = new KeyValuePair<nint, string>(
                        windowHandle,
                        windowTitle);

                    return false;
                },
                nint.Zero);

            return foundWindow;
        }

        private bool TryGetMinecraftWindowInfo(
            nint windowHandle,
            out string windowTitle)
        {
            var buffer = new StringBuilder(512);
            _ = GetClassName(windowHandle, buffer, buffer.Capacity);

            var className = buffer.ToString();
            if (className is not ("GLFW30" or "LWJGL" or "SunAwtFrame"))
            {
                windowTitle = string.Empty;
                return false;
            }

            buffer = new StringBuilder(512);
            _ = GetWindowText(windowHandle, buffer, buffer.Capacity);

            windowTitle = buffer.ToString();

            if (!(windowTitle.StartsWithF("FML")
                    || (windowTitle != "PopupMessageWindow"
                        && !windowTitle.StartsWithF("GLFW"))))
                return false;

            var processId = 0;
            _ = GetWindowThreadProcessId(windowHandle, ref processId);

            try
            {
                return processId == gameProcess.Id;
            }
            catch (Exception)
            {
                return false;
            }
        }

        [DllImport("user32")]
        private static extern bool EnumWindows(
            EnumWindowsSub lpEnumFunc,
            nint lParam);

        [DllImport("user32", EntryPoint = "GetClassNameW", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(
            nint hWnd,
            StringBuilder lpClassName,
            int nMaxCount);

        [DllImport("user32", EntryPoint = "GetWindowTextW", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(
            nint hWnd,
            StringBuilder lpString,
            int nMaxCount);

        [DllImport("user32", EntryPoint = "SetWindowTextW", CharSet = CharSet.Unicode)]
        private static extern bool SetWindowText(nint hWnd, string lpString);

        [DllImport("user32")]
        private static extern bool ShowWindow(nint hWnd, uint cmdWindow);

        [DllImport("user32")]
        private static extern int GetWindowThreadProcessId(
            nint hWnd,
            ref int lpdwProcessId);
    }
}