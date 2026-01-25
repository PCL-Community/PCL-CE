using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using PCL.Core.Utils;

namespace PCL.Core.UI.Animation.Clock;

/// <summary>
/// 一个基于 WPF CompositionTarget.Rendering 事件的时钟实现。
/// 该时钟引发的所有事件均在 UI 线程上执行。
/// </summary>
public class WpfCompositionTargetRenderingClock(int fps = 60) : IUIClock, IDisposable
{
    private CancellationTokenSource? _cts;
    
    private TimeSpan _lastTime = TimeSpan.Zero;
    private long _lastFrame;
    
    public event EventHandler<long>? Tick;
    
    public int Fps { get; set; } = fps;

    public bool IsRunning => _cts is not null && !_cts.IsCancellationRequested;
    
    ~WpfCompositionTargetRenderingClock()
    {
        Dispose();
    }
    
    public void Start()
    {
        if (IsRunning) return;

        _cts = new CancellationTokenSource();

        CompositionTarget.Rendering += _OnCompositionTargetOnRendering;
    }

    private void _OnCompositionTargetOnRendering(object? _, EventArgs args)
    {
        if (args is not RenderingEventArgs renderingEventArgs) return;

        if (_cts!.IsCancellationRequested)
        {
            CompositionTarget.Rendering -= _OnCompositionTargetOnRendering;
            return;
        }

        if (Fps == int.MaxValue)
        {
            _lastFrame++;
            Tick?.Invoke(this, _lastFrame);
        }
        else
        {
            if (_lastTime == TimeSpan.Zero)
            {
                _lastTime = renderingEventArgs.RenderingTime;
            }

            var frame = FrameUtils.TimeSpanToFrameIndex(_lastTime, renderingEventArgs.RenderingTime, Fps);
            if (frame == _lastFrame) return;
            _lastFrame = frame;

            _lastTime = renderingEventArgs.RenderingTime;

            Tick?.Invoke(this, frame);
        }
    }
    
    public void Stop()
    {
        CompositionTarget.Rendering -= _OnCompositionTargetOnRendering;
        
        if (_cts is null) return;
        _cts.Cancel();
        _cts = null;
    }
    
    public void Dispose()
    {
        CompositionTarget.Rendering -= _OnCompositionTargetOnRendering;
        
        _cts?.Cancel();
        _cts?.Dispose();
        
        GC.SuppressFinalize(this);
    }
}