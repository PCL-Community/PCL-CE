using System;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Utils;

namespace PCL.Core.UI.Animation.Clock;

/// <summary>
/// 一个基于 Stopwatch 的时钟实现。
/// </summary>
/// <param name="fps">帧率。</param>
public class StopwatchClock(int fps = 60) : IClock, IDisposable
{
    private CancellationTokenSource? _cts;
    
    private long _lastStamp;
    private long _lastFrame;

    public event EventHandler<long>? Tick;
    
    public int Fps { get; set; } = fps;

    public bool IsRunning => _cts is not null && !_cts.IsCancellationRequested;
    
    ~StopwatchClock()
    {
        Dispose();
    }
    
    public void Start()
    {
        if (IsRunning) return;

        var cts = new CancellationTokenSource();
        _cts = cts;

        _ = Task.Run(() =>
        {
            var token = cts.Token;
            // 提前取出 WaitHandle，避免在循环中反复分配
            var stopSignal = token.WaitHandle;
            _lastStamp = FrameUtils.NowStamp();

            while (!token.IsCancellationRequested)
            {
                if (Fps == int.MaxValue)
                {
                    _lastFrame++;
                    Tick?.Invoke(this, _lastFrame);
                }
                else
                {
                    var frame = FrameUtils.StampToFrameIndex(_lastStamp, Fps);
                    if (frame == _lastFrame)
                    {
                        // 距下一帧边界还有一段时间，必须让出线程。
                        // 原先的 continue 是纯忙等：一个帧间隔内空转占满整个线程池线程。
                        var nextFrameStamp = _lastStamp + (frame + 1) * FrameUtils.Frequency / Fps;
                        var remainingMs = (nextFrameStamp - FrameUtils.NowStamp()) * 1000d / FrameUtils.Frequency;
                        if (remainingMs > 0)
                        {
                            bool canceled;
                            try
                            {
                                canceled = stopSignal.WaitOne(TimeSpan.FromMilliseconds(Math.Min(remainingMs, 100)));
                            }
                            catch (ObjectDisposedException)
                            {
                                // Dispose 与本循环存在竞争，此时已无需继续
                                return;
                            }

                            if (canceled) return;
                        }

                        continue;
                    }

                    _lastFrame = frame;

                    Tick?.Invoke(this, frame);
                }
            }
        }, cts.Token);
    }
    
    public void Stop()
    {
        if (_cts is null) return;
        _cts.Cancel();
        _cts = null;
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        
        GC.SuppressFinalize(this);
    }
}