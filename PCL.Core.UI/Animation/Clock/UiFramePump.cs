using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using PCL.Core.Logging;

namespace PCL.Core.UI.Animation.Clock;

/// <summary>
///     UI 动画共用的帧源：窗口可见时使用渲染帧，否则回退到 Render 优先级定时器。
/// </summary>
internal static class UiFramePump
{
    private const string Module = "Animation";

    private const int FallbackFps = 60;

    private const int RetryIntervalMilliseconds = 250;

    private const int RafStallMilliseconds = 1000;

    private const int StatWindowMilliseconds = 5000;

    private static readonly List<AvaloniaRenderingClock> Subscribers = [];
    private static readonly Lock SyncRoot = new();

    private static TopLevel? _topLevel;
    private static DispatcherTimer? _frameTimer;
    private static DispatcherTimer? _retryTimer;
    private static bool _rafScheduled;
    private static int _rafToken;
    private static long _rafRequestStamp;
    private static string _loggedMode = string.Empty;
    private static long _statStartStamp;
    private static int _statTicks;

    public static bool IsRunning { get; private set; }

    public static void Subscribe(AvaloniaRenderingClock clock)
    {
        lock (SyncRoot)
        {
            if (Subscribers.Contains(clock)) return;
            Subscribers.Add(clock);
        }

        if (Dispatcher.UIThread.CheckAccess())
            Start();
        else
            Dispatcher.UIThread.Post(Start);
    }

    public static void Unsubscribe(AvaloniaRenderingClock clock)
    {
        bool empty;
        lock (SyncRoot)
        {
            Subscribers.Remove(clock);
            empty = Subscribers.Count == 0;
        }

        if (!empty) return;

        if (Dispatcher.UIThread.CheckAccess())
            Stop();
        else
            Dispatcher.UIThread.Post(Stop);
    }

    private static void Start()
    {
        if (IsRunning)
        {
            _ResolveAndDrive();
            return;
        }

        IsRunning = true;
        _statStartStamp = Stopwatch.GetTimestamp();
        _statTicks = 0;
        _ResolveAndDrive();
    }

    private static void Stop()
    {
        if (!IsRunning) return;

        IsRunning = false;
        _rafToken++;
        _rafScheduled = false;
        _StopFrameTimer();
        _StopRetryTimer();
        _DetachTopLevel();
        _loggedMode = string.Empty;
        LogWrapper.Debug(Module, "[Animation] 帧源已停止 clock=stopped");
    }

    private static void _ResolveAndDrive()
    {
        if (!IsRunning) return;

        var topLevel = _ResolveTopLevel();
        if (topLevel is not null)
        {
            _AttachTopLevel(topLevel);
            _RequestRaf();
        }
        else
        {
            _DetachTopLevel();
            _rafToken++;
            _rafScheduled = false;
            _StartFrameTimer();
            _LogMode("timer", "TopLevel 不可用，等待窗口创建");
        }

        _StartRetryTimer();
    }

    private static TopLevel? _ResolveTopLevel()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime
            desktop)
            return null;

        return desktop.MainWindow is { IsVisible: true } mainWindow
            ? mainWindow
            : desktop.Windows.FirstOrDefault(window => window.IsVisible);
    }

    private static void _AttachTopLevel(TopLevel topLevel)
    {
        if (ReferenceEquals(_topLevel, topLevel)) return;

        _DetachTopLevel();
        _topLevel = topLevel;
        topLevel.Opened += _OnTopLevelOpened;
        topLevel.Closed += _OnTopLevelClosed;
    }

    private static void _DetachTopLevel()
    {
        if (_topLevel is null) return;

        _topLevel.Opened -= _OnTopLevelOpened;
        _topLevel.Closed -= _OnTopLevelClosed;
        _topLevel = null;
    }

    private static void _OnTopLevelOpened(object? sender, EventArgs e)
    {
        if (!IsRunning) return;
        LogWrapper.Debug(Module, $"[Animation] TopLevel 已打开，重新解析帧驱动：{_Describe(_topLevel)}");
        _ResolveAndDrive();
    }

    private static void _OnTopLevelClosed(object? sender, EventArgs e)
    {
        if (!IsRunning) return;
        LogWrapper.Debug(Module, $"[Animation] TopLevel 已关闭，重新解析帧驱动：{_Describe(_topLevel)}");
        _DetachTopLevel();
        _rafToken++;
        _rafScheduled = false;
        _ResolveAndDrive();
    }

    private static void _RequestRaf()
    {
        if (!IsRunning || _rafScheduled || _topLevel is null) return;

        var token = ++_rafToken;
        _rafScheduled = true;
        _rafRequestStamp = Stopwatch.GetTimestamp();

        try
        {
            _topLevel.RequestAnimationFrame(time => _OnRaf(token, time));
            _StopFrameTimer();
            _LogMode("raf", _Describe(_topLevel));
        }
        catch (Exception ex)
        {
            _rafScheduled = false;
            LogWrapper.Warn(ex, Module, "[Animation] RequestAnimationFrame 请求失败，退回定时器 clock=timer");
            _StartFrameTimer();
        }
    }

    private static void _OnRaf(int token, TimeSpan renderingTime)
    {
        if (token != _rafToken) return;

        _rafScheduled = false;
        if (!IsRunning) return;

        _StopFrameTimer();
        _Tick(renderingTime);
        _RequestRaf();
    }

    private static void _StartFrameTimer()
    {
        if (_frameTimer is not null) return;

        _frameTimer = new DispatcherTimer(DispatcherPriority.Render, Dispatcher.UIThread)
        {
            Interval = TimeSpan.FromMilliseconds(Math.Max(1d, 1000d / FallbackFps))
        };
        _frameTimer.Tick += _OnFrameTimerTick;
        _frameTimer.Start();
    }

    private static void _StopFrameTimer()
    {
        if (_frameTimer is null) return;

        _frameTimer.Stop();
        _frameTimer.Tick -= _OnFrameTimerTick;
        _frameTimer = null;
    }

    private static void _OnFrameTimerTick(object? sender, EventArgs e)
    {
        if (!IsRunning) return;
        _Tick(null);
    }

    private static void _StartRetryTimer()
    {
        if (_retryTimer is not null) return;

        _retryTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher.UIThread)
        {
            Interval = TimeSpan.FromMilliseconds(RetryIntervalMilliseconds)
        };
        _retryTimer.Tick += _OnRetryTimerTick;
        _retryTimer.Start();
    }

    private static void _StopRetryTimer()
    {
        if (_retryTimer is null) return;

        _retryTimer.Stop();
        _retryTimer.Tick -= _OnRetryTimerTick;
        _retryTimer = null;
    }

    private static void _OnRetryTimerTick(object? sender, EventArgs e)
    {
        if (!IsRunning) return;

        var resolved = _ResolveTopLevel();
        if (!ReferenceEquals(resolved, _topLevel))
        {
            if (resolved is not null)
                _AttachTopLevel(resolved);
            else
                _DetachTopLevel();

            _rafToken++;
            _rafScheduled = false;
            _ResolveAndDrive();
        }

        if (_topLevel is null) return;

        if (_rafScheduled
            && Stopwatch.GetTimestamp() - _rafRequestStamp
            > RafStallMilliseconds * (Stopwatch.Frequency / 1000d))
        {
            _rafToken++;
            _rafScheduled = false;
            _StartFrameTimer();
            _LogMode("timer", $"渲染帧 {RafStallMilliseconds} ms 未回调，{_Describe(_topLevel)}");
        }

        if (!_rafScheduled)
            _RequestRaf();
    }

    private static void _Tick(TimeSpan? renderingTime)
    {
        _statTicks++;

        AvaloniaRenderingClock[] subscribers;
        lock (SyncRoot)
        {
            if (Subscribers.Count == 0) return;
            subscribers = [.. Subscribers];
        }

        foreach (var clock in subscribers)
            try
            {
                clock.OnFrame(renderingTime);
            }
            catch (Exception ex)
            {
                LogWrapper.Warn(ex, Module, "[Animation] 帧回调执行失败");
            }

        var elapsed = Stopwatch.GetTimestamp() - _statStartStamp;
        if (elapsed < StatWindowMilliseconds * (Stopwatch.Frequency / 1000d)) return;

        var fps = _statTicks * 1000d / (elapsed * 1000d / Stopwatch.Frequency);
        _statStartStamp = Stopwatch.GetTimestamp();
        _statTicks = 0;
        LogWrapper.Debug(
            Module,
            $"[Animation] 帧驱动模式 clock={(_frameTimer is null ? "raf" : "timer")} fps≈{Math.Round(fps, 1)} "
            + $"TopLevel={_Describe(_topLevel)}");
    }

    private static void _LogMode(string mode, string detail)
    {
        if (_loggedMode == mode) return;

        var message = $"[Animation] 帧驱动模式 clock={mode}，{detail}";
        if (mode == "raf")
            LogWrapper.Debug(Module, message);
        else
            LogWrapper.Warn(Module, message);
        _loggedMode = mode;
    }

    private static string _Describe(TopLevel? topLevel)
    {
        if (topLevel is null) return "null";

        var title = topLevel is Window { Title: { Length: > 0 } windowTitle }
            ? $"，Title=\"{windowTitle}\""
            : string.Empty;
        return $"{topLevel.GetType().Name}(IsVisible={topLevel.IsVisible}{title})";
    }
}