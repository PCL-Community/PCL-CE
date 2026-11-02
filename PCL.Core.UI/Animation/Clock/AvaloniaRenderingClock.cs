using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using PCL.Core.Utils;

namespace PCL.Core.UI.Animation.Clock;

/// <summary>
///     基于 Avalonia <see cref="TopLevel.RequestAnimationFrame(Action{TimeSpan})" /> 的 UI 帧钟。
///     所有事件均在 UI 线程上执行；窗口尚不可用时回退到
///     <see cref="DispatcherTimer" />（约 60 FPS）。
/// </summary>
/// <param name="fps">帧率；<see cref="int.MaxValue" /> 表示每个渲染帧都触发。</param>
public class AvaloniaRenderingClock(int fps = 60) : IUiClock, IDisposable
{
    private bool _disposed;
    private DispatcherTimer? _fallbackTimer;
    private long _lastFrame;
    private TimeSpan _lastTime = TimeSpan.Zero;
    private bool _rafScheduled;
    private volatile bool _running;
    private TopLevel? _topLevel;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        GC.SuppressFinalize(this);
    }

    public event EventHandler<long>? Tick;

    public int Fps { get; set; } = fps;

    public bool IsRunning => _running;

    public void Start()
    {
        if (_running || _disposed) return;
        _running = true;

        if (Dispatcher.UIThread.CheckAccess())
            _StartCore();
        else
            Dispatcher.UIThread.Post(_StartCore);
    }

    public void Stop()
    {
        if (!_running) return;
        _running = false;
        _rafScheduled = false;
        _topLevel = null;

        if (Dispatcher.UIThread.CheckAccess())
            _StopCore();
        else
            Dispatcher.UIThread.Post(_StopCore);
    }

    ~AvaloniaRenderingClock()
    {
        Dispose();
    }

    private void _StartCore()
    {
        if (!_running) return;

        _topLevel = _ResolveTopLevel();
        if (_topLevel is null)
        {
            _StartFallbackTimer();
            return;
        }

        _ScheduleAnimationFrame();
    }

    private void _StopCore()
    {
        if (_fallbackTimer is null) return;
        _fallbackTimer.Stop();
        _fallbackTimer.Tick -= _OnFallbackTick;
        _fallbackTimer = null;
    }

    private static TopLevel? _ResolveTopLevel()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime
            desktop)
            return null;

        if (desktop.MainWindow is { IsVisible: true } mainWindow)
            return mainWindow;

        return desktop.Windows.FirstOrDefault(window => window.IsVisible);
    }

    private void _ScheduleAnimationFrame()
    {
        if (!_running || _rafScheduled || _topLevel is null) return;

        _rafScheduled = true;
        try
        {
            _topLevel.RequestAnimationFrame(_OnAnimationFrame);
        }
        catch
        {
            // 窗口关闭等异常情况下退回 DispatcherTimer
            _rafScheduled = false;
            _topLevel = null;
            _StartFallbackTimer();
        }
    }

    private void _OnAnimationFrame(TimeSpan renderingTime)
    {
        _rafScheduled = false;
        if (!_running) return;

        _RaiseTick(renderingTime);
        _ScheduleAnimationFrame();
    }

    private void _StartFallbackTimer()
    {
        if (_fallbackTimer is not null) return;

        var effectiveFps = Fps == int.MaxValue ? 60 : Math.Max(1, Fps);
        _fallbackTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(Math.Max(1d, 1000d / effectiveFps))
        };
        _fallbackTimer.Tick += _OnFallbackTick;
        _fallbackTimer.Start();
    }

    private void _OnFallbackTick(object? sender, EventArgs e)
    {
        _RaiseTick(null);
    }

    private void _RaiseTick(TimeSpan? renderingTime)
    {
        long frame;

        if (Fps == int.MaxValue)
        {
            frame = ++_lastFrame;
        }
        else if (renderingTime is { } time)
        {
            if (_lastTime == TimeSpan.Zero) _lastTime = time;

            frame = FrameUtils.TimeSpanToFrameIndex(_lastTime, time, Fps);
            if (frame == _lastFrame) return;

            _lastFrame = frame;
            _lastTime = time;
        }
        else
        {
            // DispatcherTimer 回退：定时器间隔本身已按 Fps 折算，直接递增帧号
            frame = ++_lastFrame;
        }

        Tick?.Invoke(this, frame);
    }
}