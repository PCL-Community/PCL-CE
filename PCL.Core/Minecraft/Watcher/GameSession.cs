using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.Platform;

namespace PCL.Core.Minecraft.Watcher;

/// <summary>监视单个 Minecraft 进程，负责日志采集、加载进度、窗口发现、崩溃检测、强制结束与运行栈导出。</summary>
public sealed class GameSession : IDisposable
{
    private const string Module = "Watcher";
    private readonly CancellationTokenRegistration _abortRegistration;
    private readonly IExternalCommandRunner? _commandRunner;
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly List<string> _fullLog = [];
    private readonly Queue<GameLogLineEventArgs> _history;
    private readonly Channel<string> _logChannel;
    private readonly GameSessionOptions _options;
    private readonly IGameProcess _process;
    private readonly Queue<string> _recentLog;
    private readonly Lock _sync = new();
    private readonly IWindowInterop? _windowInterop;
    private bool _anyWindowAppeared;
    private GameLogCounters _counters;
    private GameCrashReason? _crashReason;
    private int _disposed;
    private int _exitCompleted;
    private int _exitHandled;
    private int _exitRaised;
    private int _killStarted;
    private GameLogLevel _lastLevel = GameLogLevel.Info;
    private Task? _monitorWindowTask;
    private Task? _outputTask;
    private Task? _refreshWindowTitleTask;
    private GameLogStage _stage;
    private int _started;
    private GameSessionState _state = GameSessionState.Loading;
    private Task? _waitForExitTask;
    private nint _windowHandle;
    private int _windowProbeFailures;
    private bool _windowResolved;

    internal GameSession(IGameProcess process, GameSessionOptions options)
    {
        _process = process
            ?? throw new ArgumentNullException(nameof(process));
        _options = options
            ?? throw new ArgumentNullException(nameof(options));
        if (options.WindowPollInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "WindowPollInterval must be greater than zero.");
        if (options.WindowTitleRefreshInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "WindowTitleRefreshInterval must be greater than zero.");
        if (options.MaximizeDelay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "MaximizeDelay cannot be negative.");
        if (options.StackDumpInterval < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "StackDumpInterval cannot be negative.");

        _windowInterop = options.WindowInterop
            ?? ApplicationRuntime.GetService<IWindowInterop>();
        _commandRunner = options.CommandRunner
            ?? ApplicationRuntime.GetService<IExternalCommandRunner>();
        StartedAt = process.StartTime
            ?? DateTime.Now;
        _recentLog = new Queue<string>(Math.Max(1, options.RecentLogCapacity));
        _history = new Queue<GameLogLineEventArgs>(Math.Max(1, options.RecentLogCapacity));
        _logChannel = Channel.CreateUnbounded<string>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });

        _abortRegistration = options.AbortToken.CanBeCanceled
            ? options.AbortToken.Register(OnAbortRequested)
            : default;
    }

    public int ProcessId => _process.Id;

    public GameSessionMetadata Metadata => _options.Metadata;

    public GameSessionState State
    {
        get
        {
            lock (_sync)
            {
                return _state;
            }
        }
    }

    public DateTime StartedAt { get; }

    public bool IsLiveLogEnabled => _options.CaptureLiveLog;

    public bool HasExited => _process.HasExited;

    public bool IsExitCompleted => Volatile.Read(ref _exitCompleted) != 0;

    public bool CanTerminate => !HasExited;

    public bool CanCaptureStackDump =>
        !string.IsNullOrWhiteSpace(_options.StackDumpToolPath) && !HasExited;

    public GameLogCounters Counters
    {
        get
        {
            lock (_sync)
            {
                return _counters;
            }
        }
    }

    public IReadOnlyList<string> RecentLog
    {
        get
        {
            lock (_sync)
            {
                return [.. _recentLog];
            }
        }
    }

    public IReadOnlyList<string> FullLog
    {
        get
        {
            lock (_sync)
            {
                return [.. _fullLog];
            }
        }
    }

    public IReadOnlyList<GameLogLineEventArgs> History
    {
        get
        {
            lock (_sync)
            {
                return [.. _history];
            }
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _abortRegistration.Dispose();
        _disposeCts.Cancel();
        _process.OutputLineReceived -= OnOutputLine;
        _process.ErrorLineReceived -= OnOutputLine;
        _logChannel.Writer.TryComplete();
        _process.Dispose();

        _ObserveTask(_outputTask);
        _ObserveTask(_monitorWindowTask);
        _ObserveTask(_refreshWindowTitleTask);
        _ObserveTask(_waitForExitTask);
    }

    private static void _ObserveTask(Task? task)
    {
        if (task is null)
            return;

        _ = task.ContinueWith(
            static completed => _ = completed.Exception,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
    }

    public event EventHandler<GameLogLineEventArgs>? LogLineReceived;

    public event EventHandler<GameSessionStateChangedEventArgs>? StateChanged;

    public event EventHandler<GameSessionCrashedEventArgs>? Crashed;

    public event EventHandler<GameSessionExitedEventArgs>? Exited;

    internal void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
            return;
        if (Volatile.Read(ref _disposed) != 0 || _IsTerminal(State))
            return;

        _process.OutputLineReceived += OnOutputLine;
        _process.ErrorLineReceived += OnOutputLine;

        LogWrapper.Info(Module, $"[{ProcessId}] {Lang.Text("Watcher.Start")}");
        if (!string.IsNullOrWhiteSpace(_options.WindowTitleTemplate))
            LogWrapper.Info(
                Module,
                $"[{ProcessId}] 要求窗口标题：{_options.WindowTitleTemplate}");

        try
        {
            _process.StartReading();
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, Module, $"[{ProcessId}] 启动标准输出读取失败");
        }

        _outputTask = Task.Run(_ConsumeLogsAsync);
        _monitorWindowTask = Task.Run(_MonitorWindowAsync);
        _refreshWindowTitleTask = Task.Run(_RefreshWindowTitleAsync);
        _waitForExitTask = Task.Run(_WaitForExitAsync);
    }

    /// <summary>请求终止游戏进程。加载或运行中的会话会切换为 Canceled，已结束的会话保留原状态。</summary>
    public void Kill()
    {
        if (Interlocked.Exchange(ref _killStarted, 1) != 0)
            return;

        _TransitionTo(GameSessionState.Canceled);
        _ = Task.Run(_KillCoreAsync);
    }

    /// <summary>导出最多三次 jstack 输出。每次最多等待 30 秒，间隔 3 秒。</summary>
    public async Task<IReadOnlyList<string>> CaptureStackDumpsAsync(
        CancellationToken cancellationToken = default)
    {
        var result = new List<string>(3);
        var runner = _commandRunner;
        if (runner is null || string.IsNullOrWhiteSpace(_options.StackDumpToolPath))
            return result;

        for (var i = 0; i < 3; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var output = string.Empty;
            try
            {
                var commandResult = await runner
                    .RunAsync(
                        _options.StackDumpToolPath,
                        ["-l", "-e", ProcessId.ToString()],
                        TimeSpan.FromSeconds(30),
                        cancellationToken)
                    .ConfigureAwait(false);
                output = commandResult.StandardOutput;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogWrapper.Debug(ex, Module, $"[{ProcessId}] 导出运行栈失败");
            }

            result.Add(output);
            if (i < 2)
                await Task
                    .Delay(_options.StackDumpInterval, cancellationToken)
                    .ConfigureAwait(false);
        }

        return result;
    }

    private async Task _ConsumeLogsAsync()
    {
        try
        {
            await foreach (
                var line in _logChannel
                    .Reader
                    .ReadAllAsync(_disposeCts.Token)
                    .ConfigureAwait(false))
                _ProcessLogLine(line);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, Module, $"[{ProcessId}] 处理 Minecraft 日志失败");
        }
    }

    private void _ProcessLogLine(string line)
    {
        if (_options.CaptureLiveLog)
        {
            var level = GameLogParser.IsContinuationLine(line)
                ? _lastLevel
                : GameLogParser.ClassifyLevel(line, _lastLevel);
            _lastLevel = level;
            var entry = new GameLogLineEventArgs(line, level);

            lock (_sync)
            {
                _fullLog.Add(line);
                _history.Enqueue(entry);
                var capacity = Math.Max(1, _options.RecentLogCapacity);
                while (_history.Count > capacity)
                    _history.Dequeue();
                _counters = _IncrementCounter(_counters, level);
            }

            _Raise(LogLineReceived, this, entry);
        }

        lock (_sync)
        {
            _recentLog.Enqueue(line);
            var capacity = Math.Max(1, _options.RecentLogCapacity);
            while (_recentLog.Count > capacity)
                _recentLog.Dequeue();
        }

        if (_IsTerminal(State))
            return;

        GameLogStage stage;
        bool stageChanged;
        lock (_sync)
        {
            stage = GameLogParser.DetectProgress(line, _stage);
            stageChanged = stage != _stage;
            if (stageChanged)
                _stage = stage;
        }

        if (stageChanged)
        {
            _LogStage(stage);
            _ReportProgress();
        }

        switch (GameLogParser.DetectSignal(line))
        {
            case GameLogSignal.Close:
                LogWrapper.Info(
                    Module,
                    $"[{ProcessId}] {Lang.Text("Watcher.Log.CloseDetected", line)}");
                _TransitionTo(GameSessionState.Ended);
                break;

            case GameLogSignal.Crash:
                LogWrapper.Info(
                    Module,
                    $"[{ProcessId}] {Lang.Text("Watcher.Log.CrashDetected", line)}");
                _TriggerCrash(GameCrashReason.CrashLogDetected, _process.ExitCode);
                break;
        }
    }

    private void _ReportProgress()
    {
        if (State != GameSessionState.Loading)
            return;

        GameLogStage stage;
        bool anyWindowAppeared;
        lock (_sync)
        {
            stage = _stage;
            anyWindowAppeared = _anyWindowAppeared;
        }

        double progress;
        if (anyWindowAppeared || (int)stage >= (int)GameLogStage.Sound)
        {
            progress = 0.95d;
            if (_TransitionTo(GameSessionState.Running))
                LogWrapper.Info(Module, $"[{ProcessId}] {Lang.Text("Watcher.LoadComplete")}");
        }
        else
        {
            progress = Math.Min((int)stage, 3) / 3d * 0.9d;
        }

        try
        {
            _options.ReportProgress?.Invoke(progress);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, Module, $"[{ProcessId}] 汇报启动进度失败");
        }
    }

    private static void _LogStage(GameLogStage stage)
    {
        var key = stage switch
        {
            GameLogStage.Output => "Watcher.Progress.LogAppeared",
            GameLogStage.User => "Watcher.Progress.UserSet",
            GameLogStage.Lwjgl => "Watcher.Progress.LwjglConfirmed",
            GameLogStage.Sound => "Watcher.Progress.OpenAlLoaded",
            GameLogStage.Textures => "Watcher.Progress.TexturesLoaded",
            _ => null
        };

        if (key is not null)
            LogWrapper.Info(Module, Lang.Text(key));
    }

    private async Task _MonitorWindowAsync()
    {
        var interop = _windowInterop;
        if (interop is null || _IsTerminal(State) || _process.HasExited)
            return;

        try
        {
            _TryProbeWindow(interop, out _);
            using var timer = new PeriodicTimer(_options.WindowPollInterval);
            while (await timer.WaitForNextTickAsync(_disposeCts.Token).ConfigureAwait(false))
            {
                if (Volatile.Read(ref _windowResolved)
                    || _IsTerminal(State)
                    || _process.HasExited)
                    break;

                _TryProbeWindow(interop, out var failed);
                if (!failed)
                {
                    _windowProbeFailures = 0;
                    continue;
                }

                if (_windowProbeFailures < 3)
                    continue;

                LogWrapper.Warn(
                    Module,
                    $"[{ProcessId}] 连续检查 Minecraft 窗口失败，停止窗口检测");
                Volatile.Write(ref _windowResolved, true);
                break;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, Module, $"[{ProcessId}] 检查 Minecraft 窗口失败");
        }
    }

    private void _TryProbeWindow(IWindowInterop interop, out bool failed)
    {
        try
        {
            var window = interop.TryFindGameWindow(ProcessId);
            if (window is { } found)
                _HandleWindow(found);
            failed = false;
        }
        catch (Exception ex)
        {
            failed = true;
            _windowProbeFailures++;
            if (_windowProbeFailures == 1)
                LogWrapper.Warn(ex, Module, $"[{ProcessId}] 查找 Minecraft 窗口失败");
        }
    }

    private void _HandleWindow((nint Handle, string Title) window)
    {
        if (_IsLoaderWindow(window.Title))
        {
            bool appeared;
            lock (_sync)
            {
                appeared = _anyWindowAppeared;
                _anyWindowAppeared = true;
            }

            if (!appeared)
                LogWrapper.Info(
                    Module,
                    $"[{ProcessId}] {Lang.Text(
                        "Watcher.FmlWindowLoaded",
                        window.Title,
                        window.Handle.ToInt64())}");

            _ReportProgress();
            return;
        }

        lock (_sync)
        {
            _windowHandle = window.Handle;
            _anyWindowAppeared = true;
        }

        Volatile.Write(ref _windowResolved, true);

        LogWrapper.Info(
            Module,
            $"[{ProcessId}] {Lang.Text(
                "Watcher.WindowLoaded",
                window.Title,
                window.Handle.ToInt64())}");

        _ReportProgress();
        _UpdateWindowTitle();

        if (_options.MaximizeWindow)
            _ = _MaximizeWindowAsync();
    }

    private async Task _MaximizeWindowAsync()
    {
        try
        {
            await Task.Delay(_options.MaximizeDelay, _disposeCts.Token).ConfigureAwait(false);
            if (State != GameSessionState.Running || _process.HasExited)
                return;

            nint handle;
            lock (_sync)
            {
                handle = _windowHandle;
            }

            if (handle == 0)
                return;

            _windowInterop?.MaximizeWindow(handle);
            LogWrapper.Info(
                Module,
                $"[{ProcessId}] {Lang.Text("Watcher.WindowMaximized", handle.ToInt64())}");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, Module, $"[{ProcessId}] 最大化 Minecraft 窗口失败");
        }
    }

    private async Task _RefreshWindowTitleAsync()
    {
        if (string.IsNullOrWhiteSpace(_options.WindowTitleTemplate))
            return;

        try
        {
            using var timer = new PeriodicTimer(_options.WindowTitleRefreshInterval);
            while (await timer.WaitForNextTickAsync(_disposeCts.Token).ConfigureAwait(false))
            {
                if (_IsTerminal(State))
                    break;

                if (State == GameSessionState.Running && !_process.HasExited)
                    _UpdateWindowTitle();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, Module, $"[{ProcessId}] 刷新 Minecraft 窗口标题失败");
        }
    }

    private void _UpdateWindowTitle()
    {
        if (string.IsNullOrWhiteSpace(_options.WindowTitleTemplate))
            return;

        nint handle;
        lock (_sync)
        {
            handle = _windowHandle;
        }

        if (handle == 0)
            return;

        var now = DateTime.Now;
        var title = _options
            .WindowTitleTemplate
            .Replace("{date}", Lang.Date(now, "d"), StringComparison.Ordinal)
            .Replace("{time}", Lang.Date(now, "T"), StringComparison.Ordinal);

        _windowInterop?.SetWindowTitle(handle, title);
    }

    private static bool _IsLoaderWindow(string title)
    {
        return title.StartsWith("FML", StringComparison.Ordinal)
            || title.StartsWith("Quilt Loader", StringComparison.Ordinal);
    }

    private async Task _WaitForExitAsync()
    {
        while (true)
            try
            {
                await _process.WaitForExitAsync(_disposeCts.Token).ConfigureAwait(false);
                break;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                LogWrapper.Warn(ex, Module, $"[{ProcessId}] 等待 Minecraft 退出失败");
                if (_process.HasExited)
                    break;

                try
                {
                    await Task
                        .Delay(TimeSpan.FromSeconds(1), _disposeCts.Token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

        _logChannel.Writer.TryComplete();
        await _DrainOutputAsync().ConfigureAwait(false);

        if (Volatile.Read(ref _disposed) != 0)
            return;

        _HandleProcessExit();
    }

    private async Task _DrainOutputAsync()
    {
        var outputTask = _outputTask;
        if (outputTask is null)
            return;

        try
        {
            await outputTask
                .WaitAsync(TimeSpan.FromSeconds(5), _disposeCts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (TimeoutException)
        {
            LogWrapper.Warn(Module, $"[{ProcessId}] 等待日志消费超时");
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, Module, $"[{ProcessId}] 等待日志消费失败");
        }
    }

    private void _HandleProcessExit()
    {
        if (Interlocked.Exchange(ref _exitHandled, 1) != 0)
            return;

        var exitCode = _process.ExitCode;
        var exitCodeText = exitCode?.ToString() ?? "-1";
        LogWrapper.Info(
            Module,
            $"[{ProcessId}] {Lang.Text("Watcher.ProcessExited", exitCodeText)}");

        if (_options.CaptureLiveLog)
            _EmitLogLine(Lang.Text("Watcher.ProcessExited", exitCodeText), GameLogLevel.Info);

        var state = State;
        switch (state)
        {
            case GameSessionState.Loading:
            {
                LogWrapper.Info(Module, $"[{ProcessId}] {Lang.Text("Watcher.Crash.Suspected")}");
                _TriggerCrash(GameCrashReason.StartupFailure, exitCode);
                break;
            }

            case GameSessionState.Running when
                exitCode is not 0
                && Metadata.ReleaseTime.Year >= 2012:
            {
                LogWrapper.Info(Module, $"[{ProcessId}] {Lang.Text("Watcher.Crash.AbnormalExit")}");
                _TriggerCrash(GameCrashReason.AbnormalExit, exitCode);
                break;
            }

            default:
            {
                if (state is not (GameSessionState.Crashed
                    or GameSessionState.Ended
                    or GameSessionState.Canceled))
                    _TransitionTo(GameSessionState.Ended);

                break;
            }
        }

        GameCrashReason? crashReason;
        lock (_sync)
        {
            crashReason = _crashReason;
        }

        var reason = State switch
        {
            GameSessionState.Canceled => GameExitReason.Canceled,
            GameSessionState.Crashed => crashReason switch
            {
                GameCrashReason.StartupFailure => GameExitReason.StartupFailure,
                GameCrashReason.AbnormalExit => GameExitReason.AbnormalExit,
                GameCrashReason.CrashLogDetected => GameExitReason.CrashLogDetected,
                _ => GameExitReason.AbnormalExit
            },
            _ => GameExitReason.Normal
        };

        _RaiseExited(exitCode, reason);
        Volatile.Write(ref _exitCompleted, 1);
    }

    private void _TriggerCrash(GameCrashReason reason, int? exitCode)
    {
        GameSessionState oldState;
        GameSessionCrashedEventArgs args;

        lock (_sync)
        {
            if (_state is GameSessionState.Crashed
                or GameSessionState.Ended
                or GameSessionState.Canceled)
                return;

            oldState = _state;
            _state = GameSessionState.Crashed;
            _crashReason = reason;
            args = new GameSessionCrashedEventArgs(this, reason, exitCode, _recentLog.ToArray());
        }

        _Raise(
            StateChanged,
            this,
            new GameSessionStateChangedEventArgs(this, oldState, GameSessionState.Crashed));
        _Raise(Crashed, this, args);
    }

    private bool _TransitionTo(GameSessionState newState)
    {
        GameSessionState oldState;

        lock (_sync)
        {
            if (_state == newState || _IsTerminal(_state))
                return false;

            if (!_IsAllowedTransition(_state, newState))
                return false;

            oldState = _state;
            _state = newState;
        }

        _Raise(
            StateChanged,
            this,
            new GameSessionStateChangedEventArgs(this, oldState, newState));
        return true;
    }

    private void _RaiseExited(int? exitCode, GameExitReason reason)
    {
        if (Interlocked.Exchange(ref _exitRaised, 1) != 0)
            return;

        _Raise(Exited, this, new GameSessionExitedEventArgs(this, exitCode, reason));
    }

    private void _EmitLogLine(string text, GameLogLevel level)
    {
        if (!_options.CaptureLiveLog)
            return;

        var entry = new GameLogLineEventArgs(text, level);
        lock (_sync)
        {
            _counters = _IncrementCounter(_counters, level);
            _history.Enqueue(entry);
            var capacity = Math.Max(1, _options.RecentLogCapacity);
            while (_history.Count > capacity)
                _history.Dequeue();
        }

        _Raise(LogLineReceived, this, entry);
    }

    private static GameLogCounters _IncrementCounter(GameLogCounters counters, GameLogLevel level)
    {
        return level switch
        {
            GameLogLevel.Debug => counters with
            {
                Debug = counters.Debug + 1
            },
            GameLogLevel.Info => counters with
            {
                Info = counters.Info + 1
            },
            GameLogLevel.Warn => counters with
            {
                Warn = counters.Warn + 1
            },
            GameLogLevel.Error => counters with
            {
                Error = counters.Error + 1
            },
            GameLogLevel.Fatal => counters with
            {
                Fatal = counters.Fatal + 1
            },
            _ => counters
        };
    }

    private void OnOutputLine(string line)
    {
        if (Volatile.Read(ref _disposed) != 0)
            return;

        _logChannel.Writer.TryWrite(line);
    }

    private void OnAbortRequested()
    {
        if (State is not (GameSessionState.Loading
            or GameSessionState.Running
            or GameSessionState.Crashed))
            return;

        LogWrapper.Info(Module, $"[{ProcessId}] 启动流程已取消，停止监视游戏");
        Kill();
    }

    private async Task _KillCoreAsync()
    {
        LogWrapper.Info(Module, $"[{ProcessId}] {Lang.Text("Watcher.Kill.Attempt")}");

        try
        {
            if (!_process.HasExited)
                _process.Kill(false);

            if (!_process.WaitForExit(TimeSpan.FromSeconds(5)))
            {
                LogWrapper.Warn(
                    Module,
                    $"[{ProcessId}] {Lang.Text("Watcher.Kill.TaskkillAttempt")}");
                _process.Kill(true);

                if (!_process.WaitForExit(TimeSpan.FromSeconds(5)))
                {
                    LogWrapper.Warn(Module, $"[{ProcessId}] {Lang.Text("Watcher.Kill.Timeout")}");
                    return;
                }
            }

            _logChannel.Writer.TryComplete();
            await _DrainOutputAsync().ConfigureAwait(false);

            if (Volatile.Read(ref _disposed) == 0)
                _HandleProcessExit();

            LogWrapper.Info(Module, $"[{ProcessId}] {Lang.Text("Watcher.Kill.Success")}");
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, Module, $"[{ProcessId}] {Lang.Text("Watcher.Kill.Failed")}");
        }
    }

    private static bool _IsTerminal(GameSessionState state)
    {
        return state is GameSessionState.Crashed
            or GameSessionState.Ended
            or GameSessionState.Canceled;
    }

    private static bool _IsAllowedTransition(GameSessionState oldState, GameSessionState newState)
    {
        return oldState switch
        {
            GameSessionState.Loading => newState is GameSessionState.Running
                or GameSessionState.Crashed
                or GameSessionState.Ended
                or GameSessionState.Canceled,
            GameSessionState.Running => newState is GameSessionState.Crashed
                or GameSessionState.Ended
                or GameSessionState.Canceled,
            _ => false
        };
    }

    private static void _Raise<T>(EventHandler<T>? handler, object sender, T args)
        where T : EventArgs
    {
        if (handler is null)
            return;

        foreach (var target in handler.GetInvocationList())
            try
            {
                ((EventHandler<T>)target)(sender, args);
            }
            catch (Exception ex)
            {
                LogWrapper.Warn(ex, Module, "游戏会话事件处理器执行失败");
            }
    }
}