using System.ComponentModel;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Platform;

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
                        PlatformServices.WindowInterop?.MaximizeWindow(_windowHandle);

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
        ///     <c>null</c> 代表未找到；非 Windows 平台未注册窗口交互实现时始终返回 <c>null</c>。
        /// </summary>
        private KeyValuePair<nint, string>? TryGetMinecraftWindow()
        {
            return PlatformServices.WindowInterop?.TryFindGameWindow(gameProcess.Id) is { } window
                ? new KeyValuePair<nint, string>(window.Handle, window.Title)
                : null;
        }
    }
}