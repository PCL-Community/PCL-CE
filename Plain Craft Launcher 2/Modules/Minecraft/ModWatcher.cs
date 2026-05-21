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
    public static List<Watcher> McWatcherList = new();
    private static bool IsWatcherRunning;
    public static bool HasRunningMinecraft;

    private static void WatcherStateChanged()
    {
        var IsRunning = false;
        var TriggerLauncherShutdown = true;
        foreach (var Watcher in McWatcherList)
        {
            if (Watcher.State == Watcher.MinecraftState.Loading || Watcher.State == Watcher.MinecraftState.Running)
            {
                IsRunning = true;
                break;
            }

            if (Watcher.State == Watcher.MinecraftState.Crashed || Watcher.State == Watcher.MinecraftState.Canceled)
                TriggerLauncherShutdown = false;
        }

        if (IsWatcherRunning == IsRunning)
            return;
        IsWatcherRunning = IsRunning;
        if (IsWatcherRunning)
            MinecraftStart();
        else
            MinecraftStop(TriggerLauncherShutdown);
    }

    private static void MinecraftStart()
    {
        ModLaunch.McLaunchLog("[全局] 出现运行中的 Minecraft");
        HasRunningMinecraft = true;
        ModMain.FrmMain.BtnExtraShutdown.ShowRefresh();
    }

    private static void MinecraftStop(bool TriggerLauncherShutdown)
    {
        ModLaunch.McLaunchLog("[全局] 已无运行中的 Minecraft");
        HasRunningMinecraft = false;
        ModMain.FrmMain.BtnExtraShutdown.ShowRefresh();
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
                if (TriggerLauncherShutdown)
                    ModBase.RunInUi(() => ModMain.FrmMain.EndProgram(false));
                else
                    ModBase.RunInUi(() => ModMain.FrmMain.Hidden = false);
                break;
            case LauncherVisibility.HideAndReopen:
                // 恢复
                ModBase.RunInUi(() => ModMain.FrmMain.Hidden = false);
                break;
        }
    }

    private static GameLogLevel GetLevel(string line, GameLogLevel lastLevel)
    {
        Func<string, SolidColorBrush> GetColorBrush =
            name => (SolidColorBrush)System.Windows.Application.Current.Resources[name];
        var Starting = line.Split(": ")[0];
        if (Starting.ContainsF("FATAL"))
            return GameLogLevel.Fatal;
        if (Starting.ContainsF("ERROR"))
            return GameLogLevel.Error;
        if (Starting.ContainsF("WARN"))
            return GameLogLevel.Warn;
        if (Starting.ContainsF("INFO"))
            return GameLogLevel.Info;
        if (Starting.ContainsF("DEBUG"))
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
        Func<string, SolidColorBrush> GetColorBrush =
            name => (SolidColorBrush)System.Windows.Application.Current.Resources[name];
        switch (level)
        {
            case GameLogLevel.Debug:
            {
                return GetColorBrush("ColorBrushDebug");
            }
            case GameLogLevel.Info:
            {
                GetColorBrush(ThemeManager.IsDarkMode ? "ColorBrushInfoDark" : "ColorBrushInfo");
                break;
            }
            case GameLogLevel.Warn:
            {
                return GetColorBrush("ColorBrushWarn");
            }
            case GameLogLevel.Error:
            {
                return GetColorBrush("ColorBrushError");
            }
            case GameLogLevel.Fatal:
            {
                return GetColorBrush("ColorBrushFatal");
            }
        }

        return GetColorBrush(ThemeManager.IsDarkMode ? "ColorBrushInfoDark" : "ColorBrushInfo");
    }

    // 实时日志处理
    public class LogOutputEventArgs : EventArgs
    {
        public SolidColorBrush Color;
        public string LogText;

        public LogOutputEventArgs(string LogText, SolidColorBrush Color)
        {
            this.LogText = LogText;
            this.Color = Color;
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

        private readonly int PID;

        /// <summary>
        ///     是否处理实时日志。
        /// </summary>
        private readonly bool RealTime;

        private readonly object WaitingLogLock = new();
        private MinecraftState _State = MinecraftState.Loading;
        public uint CountDebug;
        public uint CountError;
        public uint CountFatal;
        public uint CountInfo;
        public uint CountWarn;

        /// <summary>
        ///     游戏的所有日志输出，只有处理实时日志的情况下才会记录。
        /// </summary>
        public List<string> FullLog = new();

        // 初始化
        public Process GameProcess;

        // 窗口检查
        private bool IsWindowAppeared;

        /// <summary>
        ///     窗口检查是否已经完成。这不一定代表着找到了窗口（如果没有找到，IsWindowAppeared 仍为 False）。
        /// </summary>
        private bool IsWindowFinished;

        public string JStackPath;

        /// <summary>
        ///     上一行日志级别。
        /// </summary>
        private GameLogLevel LastLevel = GameLogLevel.Info;

        public Queue<string> LatestLog = new();
        public ModLoader.LoaderTask<Process, int> Loader;

        // 进度更新
        private int LogProgress;
        public ModMinecraft.McInstance Version;

        // 日志
        public List<string> WaitingLog = new(1000);
        private nint WindowHandle;
        private string WindowTitle = "";

        public Watcher(ModLoader.LoaderTask<Process, int> Loader, ModMinecraft.McInstance Version, string WindowTitle,
            string JStackPath, bool OutputRealTime = false)
        {
            this.Loader = Loader;
            this.Version = Version;
            this.WindowTitle = WindowTitle;
            RealTime = OutputRealTime;
            PID = Loader.Input.Id;
            this.JStackPath = JStackPath;

            WatcherLog("开始 Minecraft 日志监控");
            if (string.IsNullOrWhiteSpace(WindowTitle))
                WatcherLog("要求窗口标题：" + WindowTitle);

            // 更改列表
            var NewWatcherList = new List<Watcher>();
            foreach (var Watch in McWatcherList)
            {
                if (Watch.State == MinecraftState.Crashed || Watch.State == MinecraftState.Ended ||
                    Watch.State == MinecraftState.Canceled)
                    continue;
                NewWatcherList.Add(Watch);
            }

            NewWatcherList.Add(this);
            McWatcherList = NewWatcherList;
            WatcherStateChanged();

            // 初始化进程与日志读取
            GameProcess = Loader.Input;
            GameProcess.BeginOutputReadLine();
            GameProcess.BeginErrorReadLine();
            GameProcess.OutputDataReceived += LogReceived;
            GameProcess.ErrorDataReceived += LogReceived;

            // 初始化时钟
            // 设置窗口标题

            ModBase.RunInNewThread(() =>
            {
                try
                {
                    while (State != MinecraftState.Ended && State != MinecraftState.Crashed &&
                           State != MinecraftState.Canceled && Loader.State != ModBase.LoadState.Aborted)
                    {
                        TimerWindow();
                        TimerLog();
                        if (!string.IsNullOrWhiteSpace(WindowTitle))
                            for (var i = 1; i <= 3; i++)
                            {
                                if (State == MinecraftState.Running && !GameProcess.HasExited)
                                {
                                    var RealTitle = WindowTitle.Replace("{date}", Lang.Date(DateTime.Now, "d"))
                                        .Replace("{time}", Lang.Date(DateTime.Now, "T"));
                                    SetWindowText(WindowHandle, RealTitle);
                                }

                                Thread.Sleep(64);
                            }

                        Thread.Sleep(10);
                    }

                    WatcherLog("Minecraft 日志监控已退出");
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "Minecraft 日志监控主循环出错", ModBase.LogLevel.Feedback);
                    State = MinecraftState.Ended;
                }
            }, "Minecraft Watcher PID " + PID);
        }

        public MinecraftState State
        {
            get => _State;
            set
            {
                if (_State == value)
                    return;
                _State = value;
                WatcherStateChanged();
            }
        }

        /// <summary>
        ///     是否处理实时日志。
        /// </summary>
        public bool RealTimeLog => RealTime;

        // 状态
        /// <summary>
        ///     游戏退出时触发。
        /// </summary>
        public event GameExitEventHandler? GameExit;

        private void LogReceived(object sender, DataReceivedEventArgs e)
        {
            lock (WaitingLogLock)
            {
                WaitingLog.Add(e.Data);
            }

            if (RealTime)
            {
                LogRealTime(e.Data, ref LastLevel);
                if (e.Data is not null)
                    FullLog.Add(e.Data);
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
                    CountDebug = (uint)(CountDebug + 1L);
                    break;
                }
                case GameLogLevel.Info:
                {
                    CountInfo = (uint)(CountInfo + 1L);
                    break;
                }
                case GameLogLevel.Warn:
                {
                    CountWarn = (uint)(CountWarn + 1L);
                    break;
                }
                case GameLogLevel.Error:
                {
                    CountError = (uint)(CountError + 1L);
                    break;
                }
                case GameLogLevel.Fatal:
                {
                    CountFatal = (uint)(CountFatal + 1L);
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
                var Copyed = new List<string>();
                lock (WaitingLogLock)
                {
                    if (!WaitingLog.Any())
                        return;
                    Copyed = WaitingLog;
                    WaitingLog = new List<string>(1000);
                }

                foreach (var Str in Copyed)
                    GameLog(Str);
                if (State == MinecraftState.Loading)
                    ProgressUpdate();
                // 游戏退出检查
                if (GameProcess.HasExited)
                {
                    WatcherLog("Minecraft 已退出，返回值：" + GameProcess.ExitCode);
                    // 实时日志输出
                    if (RealTime)
                    {
                        var arglevel = GameLogLevel.Info;
                        LogRealTime($"Minecraft 已退出，返回值：{GameProcess.ExitCode}", ref arglevel);
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
                        WatcherLog("Minecraft 尚未加载完成，可能已崩溃");
                        Crashed();
                    }
                    else if (GameProcess.ExitCode != 0 && State == MinecraftState.Running &&
                             Version.ReleaseTime.Year >= 2012)
                    {
                        // 返回值不为 0 且未结束
                        WatcherLog("Minecraft 返回值异常，可能已崩溃");
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

        private void GameLog(string Text)
        {
            // 预处理
            if (Text is null)
                return;
            Text = Text.Replace("\r\n", "\r").Replace("\n", "\r")
                .Replace("\r", "\r\n");
            // If Text.Contains("�����") Then Hint("检测到错误的日志编码：" & Text)
            // 加入预存储
            LatestLog.Enqueue(Text);
            if (LatestLog.Count >= 501)
                LatestLog.Dequeue();
            // 进度处理
            if (LogProgress < 1)
            {
                WatcherLog("日志 1/5：已出现日志输出");
                LogProgress = 1;
            } // 可能第一句就是后面需要判断的 Log（重现：启动 1.15.2 原版）

            if (LogProgress < 2 && Text.Contains("Setting user:"))
            {
                WatcherLog("日志 2/5：游戏用户已设置"); // 仅确保支持 Minecraft 1.7+
                LogProgress = 2;
            }
            else if (LogProgress < 3 && Text.ContainsF("lwjgl version", true))
            {
                WatcherLog("日志 3/5：LWJGL 版本已确认");
                LogProgress = 3;
            }
            else if (LogProgress < 4 &&
                     (Text.Contains("OpenAL initialized") || Text.Contains("Starting up SoundSystem")))
            {
                WatcherLog("日志 4/5：OpenAL 已加载"); // 仅确保支持 Minecraft 1.7+
                LogProgress = 4;
            }
            else if (LogProgress < 5 &&
                     ((Text.Contains("Created") && Text.Contains("textures") && Text.Contains("-atlas")) ||
                      Text.Contains("Found animation info")))
            {
                WatcherLog("日志 5/5：材质已加载"); // 仅确保支持 Minecraft 1.7+
                LogProgress = 5;
            }

            // 输出日志
            // Log(Text)
            // 关闭与崩溃检测
            if (!Text.Contains("[CHAT]"))
            {
                if (Text.Contains("Someone is closing me!") ||
                    Text.Contains("Restarting Minecraft with command")) // #1258
                {
                    WatcherLog("识别为关闭的 Log：" + Text);
                    State = MinecraftState.Ended;
                }
                else if (Text.Contains("Crash report saved to") ||
                         Text.Contains("This crash report has been saved to:"))
                {
                    // Text.Contains("Minecraft ran into a problem! Report saved to:") Then
                    // Minecraft 崩溃，忽略 VanillaFix
                    WatcherLog("识别为崩溃的 Log：" + Text);
                    Crashed();
                }
                else if (Text.Contains("Could not save crash report to"))
                {
                    // Minecraft 崩溃，无法保存崩溃日志
                    WatcherLog("识别为崩溃的 Log：" + Text);
                    Crashed();
                }
                else if (Text.Contains("/ERROR]: Unable to launch") ||
                         Text.Contains("An exception was thrown, the game will display an error screen and halt."))
                {
                    // Forge 崩溃
                    WatcherLog("识别为崩溃的 Log：" + Text);
                    Crashed();
                    // ElseIf Text.Contains("Shutdown failure!") Then
                    // 'Minecraft 强行崩溃，由于点 X 强行关闭也会触发这句话，所以不可用
                    // Crashed(Nothing)
                }
            }
        }

        private void WatcherLog(string Text)
        {
            ModLaunch.McLaunchLog("[" + PID + "] " + Text);
        }

        private void ProgressUpdate()
        {
            double CurrentProgress;
            if (IsWindowAppeared || LogProgress >= 4)
            {
                CurrentProgress = 0.95d;
                WatcherLog("Minecraft 加载已完成");
                State = MinecraftState.Running;
            }
            else
            {
                CurrentProgress = Math.Min(LogProgress, 3) / 3d * 0.9d;
            }

            Loader.Progress = CurrentProgress;
        }

        private void TimerWindow()
        {
            try
            {
                if (GameProcess.HasExited)
                    return;
                if (IsWindowFinished)
                    return;
                // 获取全部窗口，检查是否有新增的
                KeyValuePair<nint, string>? MinecraftWindow = default;
                try
                {
                    MinecraftWindow = TryGetMinecraftWindow();
                }
                catch (Win32Exception ex)
                {
                    // 拒绝访问（#1062）
                    ModBase.Log(ex, "由于反作弊或安全软件拦截，PCL 无法操作游戏窗口", ModBase.LogLevel.Hint);
                    IsWindowFinished = true;
                }

                if (MinecraftWindow is null)
                    return;
                var MinecraftWindowName = MinecraftWindow.Value.Value;
                var MinecraftWindowHandle = MinecraftWindow.Value.Key;
                // 已找到窗口
                if (!MinecraftWindowName.StartsWithF("FML") && !MinecraftWindowName.StartsWithF("Quilt Loader"))
                {
                    // 已找到 Minecraft 窗口
                    WindowHandle = MinecraftWindowHandle;
                    WatcherLog($"Minecraft 窗口已加载：{MinecraftWindowName}（{MinecraftWindowHandle.ToInt64()}）");
                    IsWindowFinished = true;
                    // 最大化
                    if (Config.Launch.GameWindowMode == GameWindowSizeMode.Maximized)
                        // 如果最大化导致屏幕渲染大小不对，那是 MC 的 Bug，不是我的 Bug
                        // ……虽然我很想这样说，但总有人反馈，算了
                        ModBase.RunInNewThread(() =>
                        {
                            try
                            {
                                Thread.Sleep(2000);
                                ShowWindow(WindowHandle, 3U);
                                WatcherLog($"已最大化 Minecraft 窗口：{MinecraftWindowHandle.ToInt64()}");
                            }
                            catch (Exception ex)
                            {
                                ModBase.Log(ex, "最大化 Minecraft 窗口时出现错误");
                            }
                        }, "MinecraftWindowMaximize");
                }
                else if (!IsWindowAppeared)
                {
                    // 已找到 FML 窗口
                    WatcherLog("FML 窗口已加载：" + MinecraftWindowName + "（" + MinecraftWindowHandle.ToInt64() + "）");
                }

                IsWindowAppeared = true;
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
            KeyValuePair<nint, string>? TryGetMinecraftWindowRet = default;
            TryGetMinecraftWindowRet = default;
            EnumWindows((hwnd, lParam) =>
            {
                if (TryGetMinecraftWindowRet is not null)
                    return false; // 找到后停止枚举

                var str = new StringBuilder(512);
                GetClassName(hwnd, str, str.Capacity);
                var ClassName = str.ToString();

                if (!(ClassName == "GLFW30" || ClassName == "LWJGL" || ClassName == "SunAwtFrame"))
                    return true;

                // 获取窗口标题名
                str = new StringBuilder(512);
                GetWindowText(hwnd, str, str.Capacity);
                var WindowText = str.ToString();

                // 部分版本会搞个 GLFW message window 出来所以得反选
                if (!(WindowText.StartsWithF("FML") ||
                      (WindowText != "PopupMessageWindow" && !WindowText.StartsWithF("GLFW"))))
                    return true;

                // 获取窗口关联的进程
                var ProcessId = default(int);
                GetWindowThreadProcessId(hwnd, ref ProcessId);
                try
                {
                    if (ProcessId != GameProcess.Id)
                        return true;
                }
                catch (Exception ex)
                {
                    return true;
                }

                // 找到目标，赋值并停止枚举
                TryGetMinecraftWindowRet = new KeyValuePair<nint, string>(hwnd, WindowText);
                return false;
            }, nint.Zero);
            return TryGetMinecraftWindowRet;
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
            if (State == MinecraftState.Crashed || State == MinecraftState.Ended)
                return;
            State = MinecraftState.Crashed;
            // 崩溃分析
            WatcherLog("Minecraft 已崩溃，将在 2 秒后开始崩溃分析");
            ModMain.Hint("检测到 Minecraft 出现错误，错误分析已开始……");
            ModBase.FeedbackInfo();
            ModBase.RunInNewThread(() =>
            {
                try
                {
                    Thread.Sleep(2000);
                    WatcherLog("崩溃分析开始");
                    ;
                    var Analyzer = new CrashAnalyzer(PID);
                    Analyzer.Collect(Version.PathIndie, LatestLog.ToList());
                    Analyzer.Prepare();
                    Analyzer.Analyze(Version);
                    Analyzer.Output(false,
                        new List<string>
                        {
                            Version.PathInstance + Version.Name + ".json",
                            LogWrapper.CurrentLogger.CurrentLogFiles.Last(), ModBase.ExePath + @"PCL\LatestLaunch.bat"
                        });
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "崩溃分析失败", ModBase.LogLevel.Feedback);
                }
            }, "Crash Analyzer");
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
                WatcherLog("尝试强制结束 Minecraft 进程");
                try
                {
                    if (CheckAlive(GameProcess))
                        GameProcess.Kill();
                    GameProcess.WaitForExit(5000);
                    if (CheckAlive(GameProcess))
                    {
                        WatcherLog("进程仍未退出，尝试使用 taskkill.exe");
                        var taskkillInfo = new ProcessStartInfo
                        {
                            FileName = "taskkill.exe",
                            Arguments = $"/PID {GameProcess.Id} /F /T",
                            RedirectStandardOutput = true,
                            UseShellExecute = false
                        };
                        var taskkillProcess = Process.Start(taskkillInfo);
                        var output = taskkillProcess.StandardOutput.ReadToEnd();
                        WatcherLog($"执行 taskkill.exe 结果:\n{output}");
                        GameProcess.WaitForExit(5000);
                        if (CheckAlive(GameProcess))
                        {
                            WatcherLog("强制结束 Minecraft 进程失败: 等待进程退出超时");
                            return;
                        }
                    }

                    WatcherLog("已强制结束 Minecraft 进程");
                    if (RealTime)
                    {
                        var arglevel = GameLogLevel.Info;
                        LogRealTime($"Minecraft 已退出，返回值：{GameProcess.ExitCode}", ref arglevel);
                    }

                    GameExit?.Invoke();
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "强制结束 Minecraft 进程失败", ModBase.LogLevel.Hint);
                }
            });
        }

        // 导出运行栈
        public List<string> ExportStackDump(string SavePath)
        {
            var Dump = new List<string>();
            for (var i = 1; i <= 3; i++)
            {
                Dump.Add(ModBase.ShellAndGetOutput(JStackPath, "-l -e " + GameProcess.Id));
                Thread.Sleep(3000);
            }

            return Dump;
        }

        private delegate bool EnumWindowsSub(nint hwnd, nint lParam);
    }
}
