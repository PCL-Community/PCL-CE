using System.Diagnostics;
using PCL.Core.App.Localization;

namespace PCL;

public static partial class GameWatcher
{
    public partial class Watcher
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

        private readonly int _pid;

        private readonly object _waitingLogLock = new();

        private bool _isWindowAppeared;

        /// <summary>
        ///     窗口检查是否已经完成。这不一定代表着找到了窗口（如果没有找到，IsWindowAppeared 仍为 False）。
        /// </summary>
        private bool _isWindowFinished;

        /// <summary>
        ///     上一行日志级别。
        /// </summary>
        private GameLogLevel _lastLevel = GameLogLevel.Info;

        private int _logProgress;
        private nint _windowHandle;
        private readonly string _windowTitle;
        public uint countDebug;
        public uint countError;
        public uint countFatal;
        public uint countInfo;
        public uint countWarn;

        /// <summary>
        ///     游戏的所有日志输出，只有处理实时日志的情况下才会记录。
        /// </summary>
        public List<string> fullLog = [];

        public Process gameProcess;

        public string jStackPath;

        public Queue<string> latestLog = new();
        public Loading.LoaderTask<Process, int> loader;
        public McInstance version;

        public List<string> waitingLog = new(1000);

        public Watcher(
            Loading.LoaderTask<Process, int> loader,
            McInstance version,
            string windowTitle,
            string jStackPath,
            bool outputRealTime = false)
        {
            this.loader = loader;
            this.version = version;
            _windowTitle = windowTitle;
            RealTimeLog = outputRealTime;
            var process = loader.input ?? throw new InvalidOperationException("加载器输入为空");
            _pid = process.Id;
            this.jStackPath = jStackPath;

            WatcherLog(Lang.Text("Watcher.Start"));
            if (string.IsNullOrWhiteSpace(windowTitle))
                WatcherLog("要求窗口标题：" + windowTitle);

            _RegisterWatcher();

            gameProcess = process;
            gameProcess.BeginOutputReadLine();
            gameProcess.BeginErrorReadLine();
            gameProcess.OutputDataReceived += LogReceived;
            gameProcess.ErrorDataReceived += LogReceived;

            Common.RunInNewThread(
                () => _MonitorLoop(loader),
                "Minecraft Watcher PID " + _pid);
        }

        public MinecraftState State
        {
            get;
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
        public bool RealTimeLog { get; }

        private void _RegisterWatcher()
        {
            var newWatcherList = mcWatcherList
                .Where(watch => watch.State is not (MinecraftState.Crashed
                    or MinecraftState.Ended
                    or MinecraftState.Canceled))
                .ToList();
            newWatcherList.Add(this);
            mcWatcherList = newWatcherList;
            WatcherStateChanged();
        }

        private void _MonitorLoop(Loading.LoaderTask<Process, int> monitorLoader)
        {
            try
            {
                while (State is not (MinecraftState.Ended
                           or MinecraftState.Crashed
                           or MinecraftState.Canceled)
                       && monitorLoader.State != Common.LoadState.Aborted)
                {
                    TimerWindow();
                    TimerLog();
                    if (!string.IsNullOrWhiteSpace(_windowTitle))
                        _SetWindowTitleRepeatedly();

                    Thread.Sleep(10);
                }

                WatcherLog(Lang.Text("Watcher.Exited"));
            }
            catch (Exception ex)
            {
                Common.Log(
                    ex,
                    "Minecraft 日志监控主循环出错",
                    Common.LogLevel.Feedback,
                    userSummary: Lang.Text(
                        "Minecraft.Launch.Error.WatcherOperationFailed"));
                State = MinecraftState.Ended;
            }
        }

        private void _SetWindowTitleRepeatedly()
        {
            for (var i = 1; i <= 3; i++)
            {
                if (State == MinecraftState.Running && !gameProcess.HasExited)
                {
                    var realTitle = _windowTitle
                        .Replace("{date}", Lang.Date(DateTime.Now, "d"))
                        .Replace("{time}", Lang.Date(DateTime.Now, "T"));
                    SetWindowText(_windowHandle, realTitle);
                }

                Thread.Sleep(64);
            }
        }

        /// <summary>
        ///     游戏退出时触发。
        /// </summary>
        public event GameExitEventHandler? GameExit;
    }
}
