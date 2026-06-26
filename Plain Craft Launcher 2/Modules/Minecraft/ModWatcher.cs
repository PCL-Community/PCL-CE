using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Media;
using PCL.Core.App;
using PCL.Core.Logging;
using PCL.Core.App.Localization;

namespace PCL;

public static class ModWatcher
{
    // 对全体的监视
    public static List<Watcher> mcWatcherList = new();
    private static bool isWatcherRunning;
    public static bool hasRunningMinecraft;

    private static void WatcherStateChanged()
    {
        var isRunning = false;
        var triggerLauncherShutdown = true;
        foreach (var Watcher in mcWatcherList)
        {
            if (Watcher.State == Watcher.MinecraftState.Loading || Watcher.State == Watcher.MinecraftState.Running)
            {
                isRunning = true;
                break;
            }

            if (Watcher.State == Watcher.MinecraftState.Crashed || Watcher.State == Watcher.MinecraftState.Canceled)
                triggerLauncherShutdown = false;
        }

        if (isWatcherRunning == isRunning)
            return;
        isWatcherRunning = isRunning;
        if (isWatcherRunning)
            MinecraftStart();
        else
            MinecraftStop(triggerLauncherShutdown);
    }

    private static void MinecraftStart()
    {
        ModLaunch.McLaunchLog("[全局] 出现运行中的 Minecraft");
        hasRunningMinecraft = true;
        ModMain.frmMain.BtnExtraShutdown.ShowRefresh();
    }

    private static void MinecraftStop(bool triggerLauncherShutdown)
    {
        ModLaunch.McLaunchLog("[全局] 已无运行中的 Minecraft");
        hasRunningMinecraft = false;
        ModMain.frmMain.BtnExtraShutdown.ShowRefresh();
        // 音乐播放
        if (Config.Preference.Music.StopInGame)
            ModBase.RunInUi(() =>
            {
                if (ModMusic.MusicResume()) ModBase.Log("[Music] 已根据设置，在结束后开始音乐播放");
            });
        else if (Config.Preference.Music.StartInGame)
            ModBase.RunInUi(() =>
            {
                if (ModMusic.MusicPause()) ModBase.Log("[Music] 已根据设置，在结束后暂停音乐播放");
            });
        // 开始视频背景播放
        ModVideoBack.IsGaming = false;
        ModVideoBack.VideoPlay();
        // 启动器可见性
        switch (Config.Launch.LauncherVisibility)
        {
            case LauncherVisibility.HideAndExit:
                // 直接关闭
                if (triggerLauncherShutdown)
                    ModBase.RunInUi(() => ModMain.frmMain.EndProgram(false));
                else
                    ModBase.RunInUi(() => ModMain.frmMain.Hidden = false);
                break;
            case LauncherVisibility.HideAndReopen:
                // 恢复
                ModBase.RunInUi(() => ModMain.frmMain.Hidden = false);
                break;
        }
    }

    private static GameLogLevel GetLevel(string line, GameLogLevel lastLevel)
    {
        Func<string, SolidColorBrush> getColorBrush =
            name => (SolidColorBrush)System.Windows.Application.Current.Resources[name];
        var starting = line.Split(": ")[0];
        if (starting.ContainsF("FATAL"))
            return GameLogLevel.Fatal;
        if (starting.ContainsF("ERROR"))
            return GameLogLevel.Error;
        if (starting.ContainsF("WARN"))
            return GameLogLevel.Warn;
        if (starting.ContainsF("INFO"))
            return GameLogLevel.Info;
        if (starting.ContainsF("DEBUG"))
            return GameLogLevel.Debug;
        if (line.StartsWithF("Exception in thread \""))
            return GameLogLevel.Error;
        if ((line.ContainsF("Exception") || line.ContainsF("Realms authentication error with message ")) &&
            lastLevel >= GameLogLevel.Warn)
            return lastLevel;
        if (line.StartsWithF("	at ") && lastLevel >= GameLogLevel.Warn)
            return lastLevel;
        return GameLogLevel.Info;
    }

    private static SolidColorBrush GetColor(GameLogLevel level)
    {
        Func<string, SolidColorBrush> getColorBrush =
            name => (SolidColorBrush)System.Windows.Application.Current.Resources[name];
        switch (level)
        {
            case GameLogLevel.Debug:
            {
                return getColorBrush("ColorBrushDebug");
            }
            case GameLogLevel.Info:
            {
                getColorBrush(ThemeManager.IsDarkMode ? "ColorBrushInfoDark" : "ColorBrushInfo");
                break;
            }
            case GameLogLevel.Warn:
            {
                return getColorBrush("ColorBrushWarn");
            }
            case GameLogLevel.Error:
            {
                return getColorBrush("ColorBrushError");
            }
            case GameLogLevel.Fatal:
            {
                return getColorBrush("ColorBrushFatal");
            }
        }

        return getColorBrush(ThemeManager.IsDarkMode ? "ColorBrushInfoDark" : "ColorBrushInfo");
    }

    // 实时日志处理
    public class LogOutputEventArgs : EventArgs
    {
        public SolidColorBrush color;
        public string logText;

        public LogOutputEventArgs(string logText, SolidColorBrush color)
        {
            this.logText = logText;
            this.color = color;
        }
    }

    private enum GameLogLevel
    {
        Debug = 0,
        Info = 1,
        Warn = 2,
        Error = 3,
        Fatal = 4
    }

    // 对单个进程的监视
    public class Watcher
    {
        public delegate void GameExitEventHandler();

        public delegate void LogOutputEventHandler(Watcher sender, LogOutputEventArgs e);

        public enum MinecraftState
        {
            Loading,
            Running,
            Crashed,
            Ended,
            Canceled
        }

        private readonly int pid;

        /// <summary>
        ///     是否处理实时日志。
        /// </summary>
        private readonly bool realTime;

        private readonly object waitingLogLock = new();
        public uint countDebug;
        public uint countError;
        public uint countFatal;
        public uint countInfo;
        public uint countWarn;

        /// <summary>
        ///     游戏的所有日志输出，只有处理实时日志的情况下才会记录。
        /// </summary>
        public List<string> fullLog = new();

        // 初始化
        public Process gameProcess;

        // 窗口检查
        private bool isWindowAppeared;

        /// <summary>
        ///     窗口检查是否已经完成。这不一定代表着找到了窗口（如果没有找到，IsWindowAppeared 仍为 False）。
        /// </summary>
        private bool isWindowFinished;

        public string jStackPath;

        /// <summary>
        ///     上一行日志级别。
        /// </summary>
        private GameLogLevel lastLevel = GameLogLevel.Info;

        public Queue<string> latestLog = new();
        public ModLoader.LoaderTask<Process, int> loader;

        // 进度更新
        private int logProgress;
        public McInstance version;

        // 日志
        public List<string> waitingLog = new(1000);
        private nint windowHandle;
        private string windowTitle = "";

        public Watcher(ModLoader.LoaderTask<Process, int> loader, McInstance version, string windowTitle,
            string jStackPath, bool outputRealTime = false)
        {
            this.loader = loader;
            this.version = version;
            this.windowTitle = windowTitle;
            realTime = outputRealTime;
            pid = loader.input.Id;
            this.jStackPath = jStackPath;

            WatcherLog(Lang.Text("Watcher.Start"));
            if (string.IsNullOrWhiteSpace(windowTitle))
                WatcherLog("要求窗口标题：" + windowTitle);

            // 更改列表
            var newWatcherList = new List<Watcher>();
            foreach (var Watch in mcWatcherList)
            {
                if (Watch.State == MinecraftState.Crashed || Watch.State == MinecraftState.Ended ||
                    Watch.State == MinecraftState.Canceled)
                    continue;
                newWatcherList.Add(Watch);
            }

            newWatcherList.Add(this);
            mcWatcherList = newWatcherList;
            WatcherStateChanged();

            // 初始化进程与日志读取
            gameProcess = loader.input;
            gameProcess.BeginOutputReadLine();
            gameProcess.BeginErrorReadLine();
            gameProcess.OutputDataReceived += LogReceived;
            gameProcess.ErrorDataReceived += LogReceived;

            // 初始化时钟
            // 设置窗口标题

            ModBase.RunInNewThread(() =>
            {
                try
                {
                    while (State != MinecraftState.Ended && State != MinecraftState.Crashed &&
                           State != MinecraftState.Canceled && loader.State != ModBase.LoadState.Aborted)
                    {
                        TimerWindow();
                        TimerLog();
                        if (!string.IsNullOrWhiteSpace(windowTitle))
                            for (var i = 1; i <= 3; i++)
                            {
                                if (State == MinecraftState.Running && !gameProcess.HasExited)
                                {
                                    var realTitle = windowTitle.Replace("{date}", Lang.Date(DateTime.Now, "d"))
                                        .Replace("{time}", Lang.Date(DateTime.Now, "T"));
                                    SetWindowText(windowHandle, realTitle);
                                }

                                Thread.Sleep(64);
                            }

                        Thread.Sleep(10);
                    }

                    WatcherLog(Lang.Text("Watcher.Exited"));
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "Minecraft 日志监控主循环出错", ModBase.LogLevel.Feedback);
                    State = MinecraftState.Ended;
                }
            }, "Minecraft Watcher PID " + pid);
        }

        public MinecraftState State
        {
            get => field;
            set
            {
                if (field == value)
                    return;
                field = value;
                WatcherStateChanged();
            }
        } = MinecraftState.Loading;

        /// <summary>
        ///     是否处理实时日志。
        /// </summary>
        public bool RealTimeLog => realTime;

        // 状态
        /// <summary>
        ///     游戏退出时触发。
        /// </summary>
        public event GameExitEventHandler? GameExit;

        private void LogReceived(object sender, DataReceivedEventArgs e)
        {
            lock (waitingLogLock)
            {
                waitingLog.Add(e.Data);
            }

            if (realTime)
            {
                LogRealTime(e.Data, ref lastLevel);
                if (e.Data is not null)
                    fullLog.Add(e.Data);
            }
        }

        /// <summary>
        ///     触发日志改变事件，并统计日志行数。
        /// </summary>
        private void LogRealTime(string line, ref GameLogLevel level)
        {
            if (line is null)
                return; // 杀游戏进程时有概率传 null
            level = line.StartsWithF("	at ") || line.StartsWithF("Caused by: ") || line.StartsWithF("	... ")
                ? level
                : GetLevel(line, level);

            // “	... 4 more”
            var color = GetColor(level);
            switch (level)
            {
                case GameLogLevel.Debug:
                {
                    countDebug = (uint)(countDebug + 1L);
                    break;
                }
                case GameLogLevel.Info:
                {
                    countInfo = (uint)(countInfo + 1L);
                    break;
                }
                case GameLogLevel.Warn:
                {
                    countWarn = (uint)(countWarn + 1L);
                    break;
                }
                case GameLogLevel.Error:
                {
                    countError = (uint)(countError + 1L);
                    break;
                }
                case GameLogLevel.Fatal:
                {
                    countFatal = (uint)(countFatal + 1L);
                    break;
                }
            }

            LogOutput?.Invoke(this, new LogOutputEventArgs(line, color));
        }

        /// <summary>
        ///     有新的日志输出，日志计数器发生改变时触发。
        /// </summary>
        public event LogOutputEventHandler? LogOutput;

        private void TimerLog()
        {
            try
            {
                // 输出文本
                var copyed = new List<string>();
                lock (waitingLogLock)
                {
                    if (!waitingLog.Any())
                        return;
                    copyed = waitingLog;
                    waitingLog = new List<string>(1000);
                }

                foreach (var Str in copyed)
                    GameLog(Str);
                if (State == MinecraftState.Loading)
                    ProgressUpdate();
                // 游戏退出检查
                if (gameProcess.HasExited)
                {
                    WatcherLog(Lang.Text("Watcher.ProcessExited", gameProcess.ExitCode));
                    // 实时日志输出
                    if (realTime)
                    {
                        var arglevel = GameLogLevel.Info;
                        LogRealTime(Lang.Text("Watcher.ProcessExited", gameProcess.ExitCode), ref arglevel);
                    }

                    GameExit?.Invoke();
                    // If Process.ExitCode = 1 Then
                    // '返回值为 1，考虑是任务管理器结束
                    // WatcherLog("Minecraft 返回值为 1，考虑为任务管理器结束") '并不，崩了照样是 1
                    // State = MinecraftState.Ended
                    // Else
                    if (State == MinecraftState.Loading)
                    {
                        // 窗口未出现
                        WatcherLog(Lang.Text("Watcher.Crash.Suspected"));
                        Crashed();
                    }
                    else if (gameProcess.ExitCode != 0 && State == MinecraftState.Running &&
                             version.releaseTime.Year >= 2012)
                    {
                        // 返回值不为 0 且未结束
                        WatcherLog(Lang.Text("Watcher.Crash.AbnormalExit"));
                        Crashed();
                    }
                    else if (State != MinecraftState.Crashed)
                    {
                        // 正常关闭
                        State = MinecraftState.Ended;
                    }
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "输出 Minecraft 日志失败", ModBase.LogLevel.Feedback);
            }
        }

        private void GameLog(string text)
        {
            // 预处理
            if (text is null)
                return;
            text = text.Replace("\r\n", "\r").Replace("\n", "\r")
                .Replace("\r", "\r\n");
            // If Text.Contains("�����") Then Hint("检测到错误的日志编码：" & Text)
            // 加入预存储
            latestLog.Enqueue(text);
            if (latestLog.Count >= 501)
                latestLog.Dequeue();
            // 进度处理
            if (logProgress < 1)
            {
                WatcherLog(Lang.Text("Watcher.Progress.LogAppeared"));
                logProgress = 1;
            } // 可能第一句就是后面需要判断的 Log（重现：启动 1.15.2 原版）

            if (logProgress < 2 && text.Contains("Setting user:"))
            {
                WatcherLog(Lang.Text("Watcher.Progress.UserSet")); // 仅确保支持 Minecraft 1.7+
                logProgress = 2;
            }
            else if (logProgress < 3 && text.ContainsF("lwjgl version", true))
            {
                WatcherLog(Lang.Text("Watcher.Progress.LwjglConfirmed"));
                logProgress = 3;
            }
            else if (logProgress < 4 &&
                     (text.Contains("OpenAL initialized") || text.Contains("Starting up SoundSystem")))
            {
                WatcherLog(Lang.Text("Watcher.Progress.OpenAlLoaded")); // 仅确保支持 Minecraft 1.7+
                logProgress = 4;
            }
            else if (logProgress < 5 &&
                     ((text.Contains("Created") && text.Contains("textures") && text.Contains("-atlas")) ||
                      text.Contains("Found animation info")))
            {
                WatcherLog(Lang.Text("Watcher.Progress.TexturesLoaded")); // 仅确保支持 Minecraft 1.7+
                logProgress = 5;
            }

            // 输出日志
            // Log(Text)
            // 关闭与崩溃检测
            if (!text.Contains("[CHAT]"))
            {
                if (text.Contains("Someone is closing me!") ||
                    text.Contains("Restarting Minecraft with command")) // #1258
                {
                    WatcherLog(Lang.Text("Watcher.Log.CloseDetected", text));
                    State = MinecraftState.Ended;
                }
                else if (text.Contains("Crash report saved to") ||
                         text.Contains("This crash report has been saved to:"))
                {
                    // Text.Contains("Minecraft ran into a problem! Report saved to:") Then
                    // Minecraft 崩溃，忽略 VanillaFix
                    WatcherLog(Lang.Text("Watcher.Log.CrashDetected", text));
                    Crashed();
                }
                else if (text.Contains("Could not save crash report to"))
                {
                    WatcherLog(Lang.Text("Watcher.Log.CrashDetected", text));
                    Crashed();
                }
                else if (text.Contains("/ERROR]: Unable to launch") ||
                         text.Contains("An exception was thrown, the game will display an error screen and halt."))
                {
                    WatcherLog(Lang.Text("Watcher.Log.CrashDetected", text));
                    Crashed();
                }
            }
        }

        private void WatcherLog(string text)
        {
            ModLaunch.McLaunchLog("[" + pid + "] " + text);
        }

        private void ProgressUpdate()
        {
            double currentProgress;
            if (isWindowAppeared || logProgress >= 4)
            {
                currentProgress = 0.95d;
                WatcherLog(Lang.Text("Watcher.LoadComplete"));
                State = MinecraftState.Running;
            }
            else
            {
                currentProgress = Math.Min(logProgress, 3) / 3d * 0.9d;
            }

            loader.Progress = currentProgress;
        }

        private void TimerWindow()
        {
            try
            {
                if (gameProcess.HasExited)
                    return;
                if (isWindowFinished)
                    return;
                // 获取全部窗口，检查是否有新增的
                KeyValuePair<nint, string>? minecraftWindow = default;
                try
                {
                    minecraftWindow = TryGetMinecraftWindow();
                }
                catch (Win32Exception ex)
                {
                    // 拒绝访问（#1062）
                    ModBase.Log(ex, Lang.Text("Watcher.SecurityBlocked"), ModBase.LogLevel.Hint);
                    isWindowFinished = true;
                }

                if (minecraftWindow is null)
                    return;
                var minecraftWindowName = minecraftWindow.Value.Value;
                var minecraftWindowHandle = minecraftWindow.Value.Key;
                // 已找到窗口
                if (!minecraftWindowName.StartsWithF("FML") && !minecraftWindowName.StartsWithF("Quilt Loader"))
                {
                    // 已找到 Minecraft 窗口
                    windowHandle = minecraftWindowHandle;
                    WatcherLog(Lang.Text("Watcher.WindowLoaded", minecraftWindowName, minecraftWindowHandle.ToInt64()));
                    isWindowFinished = true;
                    // 最大化
                    if (Config.Launch.GameWindowMode == GameWindowSizeMode.Maximized)
                        // 如果最大化导致屏幕渲染大小不对，那是 MC 的 Bug，不是我的 Bug
                        // ……虽然我很想这样说，但总有人反馈，算了
                        ModBase.RunInNewThread(() =>
                        {
                            try
                            {
                                Thread.Sleep(2000);
                                ShowWindow(windowHandle, 3U);
                                 WatcherLog(Lang.Text("Watcher.WindowMaximized", minecraftWindowHandle.ToInt64()));
                            }
                            catch (Exception ex)
                            {
                                ModBase.Log(ex, "最大化 Minecraft 窗口时出现错误");
                            }
                        }, "MinecraftWindowMaximize");
                }
                else if (!isWindowAppeared)
                {
                    // 已找到 FML 窗口
                    WatcherLog(Lang.Text("Watcher.FmlWindowLoaded", minecraftWindowName, minecraftWindowHandle.ToInt64()));
                }

                isWindowAppeared = true;
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "检查 Minecraft 窗口失败", ModBase.LogLevel.Feedback);
            }
        }

        /// <summary>
        ///     获取可能是当前进程对应的 Minecraft 窗口的句柄和标题。
        ///     Nothing 代表未找到。
        /// </summary>
        private KeyValuePair<nint, string>? TryGetMinecraftWindow()
        {
            KeyValuePair<nint, string>? tryGetMinecraftWindowRet = default;
            tryGetMinecraftWindowRet = default;
            EnumWindows((hwnd, lParam) =>
            {
                if (tryGetMinecraftWindowRet is not null)
                    return false; // 找到后停止枚举

                var str = new StringBuilder(512);
                GetClassName(hwnd, str, str.Capacity);
                var className = str.ToString();

                if (!(className == "GLFW30" || className == "LWJGL" || className == "SunAwtFrame"))
                    return true;

                // 获取窗口标题名
                str = new StringBuilder(512);
                GetWindowText(hwnd, str, str.Capacity);
                var windowText = str.ToString();

                // 部分版本会搞个 GLFW message window 出来所以得反选
                if (!(windowText.StartsWithF("FML") ||
                      (windowText != "PopupMessageWindow" && !windowText.StartsWithF("GLFW"))))
                    return true;

                // 获取窗口关联的进程
                var processId = default(int);
                GetWindowThreadProcessId(hwnd, ref processId);
                try
                {
                    if (processId != gameProcess.Id)
                        return true;
                }
                catch (Exception ex)
                {
                    return true;
                }

                // 找到目标，赋值并停止枚举
                tryGetMinecraftWindowRet = new KeyValuePair<nint, string>(hwnd, windowText);
                return false;
            }, nint.Zero);
            return tryGetMinecraftWindowRet;
        }

        [DllImport("user32")]
        private static extern bool EnumWindows(EnumWindowsSub lpEnumFunc, nint lParam);

        [DllImport("user32", EntryPoint = "GetClassNameW", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(nint hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32", EntryPoint = "GetWindowTextW", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32", EntryPoint = "SetWindowTextW", CharSet = CharSet.Unicode)]
        private static extern bool SetWindowText(nint hWnd, string lpString);

        [DllImport("user32")]
        private static extern bool ShowWindow(nint hWnd, uint cmdWindow);

        [DllImport("user32")]
        private static extern int GetWindowThreadProcessId(nint hWnd, ref int lpdwProcessId);

        // 崩溃处理
        private void Crashed()
        {
            if (State is MinecraftState.Crashed or MinecraftState.Ended)
                return;
            State = MinecraftState.Crashed;
            // 崩溃分析
            if (!Config.Launch.DisableCrashAnalysis)
            {
                WatcherLog(Lang.Text("Watcher.Crash.Detected"));
                HintService.Hint(Lang.Text("Watcher.Crash.Hint"));

                ModBase.FeedbackInfo();
                ModBase.RunInNewThread(() =>
                {
                    try
                    {
                        Thread.Sleep(2000);
                        WatcherLog(Lang.Text("Watcher.Crash.AnalysisStart"));
                        var analyzer = new CrashAnalyzer(pid);
                        analyzer.Collect(version.PathIndie, latestLog.ToList());
                        analyzer.Prepare();
                        analyzer.Analyze(version);
                        analyzer.Output(
                            false,
                            [
                                version.PathInstance + version.Name + ".json",
                                LogWrapper.CurrentLogger.CurrentLogFiles.Last(),
                                ModBase.exePath + @"PCL\LatestLaunch.bat"
                            ]);
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, "崩溃分析失败", ModBase.LogLevel.Feedback);
                    }
                }, "Crash Analyzer");
            }
            else
            {
                WatcherLog(Lang.Text("Watcher.Crash.DetectedDisabled"));
            }
        }

        // 强制关闭
        public bool CheckAlive(Process p)
        {
            if (!p.HasExited)
                return true;
            var exists = Array.Exists(Process.GetProcesses(), item => item.Id == p.Id);
            if (exists)
                return true;
            return false;
        }

        public void Kill()
        {
            State = MinecraftState.Canceled;
            ModBase.RunInNewThread(() =>
            {
                WatcherLog(Lang.Text("Watcher.Kill.Attempt"));
                try
                {
                    if (CheckAlive(gameProcess))
                        gameProcess.Kill();
                    gameProcess.WaitForExit(5000);
                    if (CheckAlive(gameProcess))
                    {
                        WatcherLog(Lang.Text("Watcher.Kill.TaskkillAttempt"));
                        var taskkillInfo = new ProcessStartInfo
                        {
                            FileName = "taskkill.exe",
                            Arguments = $"/PID {gameProcess.Id} /F /T",
                            RedirectStandardOutput = true,
                            UseShellExecute = false
                        };
                        var taskkillProcess = Process.Start(taskkillInfo);
                        var output = taskkillProcess.StandardOutput.ReadToEnd();
                        WatcherLog(Lang.Text("Watcher.Kill.TaskkillResult", output));
                        gameProcess.WaitForExit(5000);
                        if (CheckAlive(gameProcess))
                        {
                            WatcherLog(Lang.Text("Watcher.Kill.Timeout"));
                            return;
                        }
                    }

                    WatcherLog(Lang.Text("Watcher.Kill.Success"));
                    if (realTime)
                    {
                        var arglevel = GameLogLevel.Info;
                        LogRealTime(Lang.Text("Watcher.ProcessExited", gameProcess.ExitCode), ref arglevel);
                    }

                    GameExit?.Invoke();
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, Lang.Text("Watcher.Kill.Failed"), ModBase.LogLevel.Hint);
                }
            });
        }

        // 导出运行栈
        public List<string> ExportStackDump(string savePath)
        {
            var dump = new List<string>();
            for (var i = 1; i <= 3; i++)
            {
                dump.Add(ModBase.ShellAndGetOutput(jStackPath, "-l -e " + gameProcess.Id));
                Thread.Sleep(3000);
            }

            return dump;
        }

        private delegate bool EnumWindowsSub(nint hwnd, nint lParam);
    }
}
