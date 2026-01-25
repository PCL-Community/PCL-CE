using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.UI.Animation.Core;
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

        _cts = new CancellationTokenSource();
        
        _ = Task.Run(() =>
        {
            _lastStamp = FrameUtils.NowStamp();
            
            while (!_cts.IsCancellationRequested)
            {
                if (Fps == int.MaxValue)
                {
                    _lastFrame++;
                    Tick?.Invoke(this, _lastFrame);
                }
                else
                {
                    var frame = FrameUtils.StampToFrameIndex(_lastStamp, Fps);
                    if (frame == _lastFrame) continue;
                    _lastFrame = frame;
                
                    Tick?.Invoke(this, frame);
                }
            }
        }, _cts.Token);
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