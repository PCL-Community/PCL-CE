using System.Diagnostics;
using PCL.Core.Utils;

namespace PCL.Core.UI.Animation.Clock;

/// <summary>
///     UI 帧钟。所有实例共享 <see cref="UiFramePump" /> 的同一条渲染帧链与同一个回退定时器，
///     因此命令式动画 API 与动画引擎使用同一帧源；事件均在 UI 线程触发。
///     窗口可见前帧源以 Render 优先级的定时器维持并周期性重试解析 <see cref="TopLevel" />，
///     驱动模式与降级由帧源写日志。
/// </summary>
/// <param name="fps">帧率；<see cref="int.MaxValue" /> 表示每个渲染帧都触发。</param>
public class AvaloniaRenderingClock(int fps = 60) : IUiClock, IDisposable
{
    private bool _disposed;
    private long _lastFrame;
    private TimeSpan _lastTime = TimeSpan.Zero;
    private volatile bool _running;
    private long _timerStartStamp;

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        Stop();
        GC.SuppressFinalize(this);
    }

    public bool IsRunning => _running;

    public int Fps { get; set; } = fps;

    public event EventHandler<long>? Tick;

    public void Start()
    {
        if (_running || _disposed) return;

        _running = true;
        _lastFrame = 0;
        _lastTime = TimeSpan.Zero;
        _timerStartStamp = 0;
        UiFramePump.Subscribe(this);
    }

    public void Stop()
    {
        if (!_running) return;

        _running = false;
        UiFramePump.Unsubscribe(this);
    }

    internal void OnFrame(TimeSpan? renderingTime)
    {
        if (!_running) return;

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
            if (_timerStartStamp == 0) _timerStartStamp = Stopwatch.GetTimestamp();

            frame = FrameUtils.StampToFrameIndex(_timerStartStamp, Fps);
            if (frame == _lastFrame) return;

            _lastFrame = frame;
        }

        Tick?.Invoke(this, frame);
    }
}