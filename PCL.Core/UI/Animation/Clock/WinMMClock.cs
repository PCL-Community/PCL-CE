using System;
using System.Runtime.InteropServices;

namespace PCL.Core.UI.Animation.Clock;

public sealed partial class WinMMClock(int fps = 60) : IClock, IDisposable
{
    private uint _timerId;
    private long _frameIndex;
    private TimeProc? _callback;
    
    public event EventHandler<long>? Tick;

    public int Fps { get; set; } = fps;
    
    public bool IsRunning { get; private set; }
    
    ~WinMMClock()
    {
        Dispose();
    }
    
    public void Start()
    {
        if (IsRunning) return;
        IsRunning = true;
        
        _frameIndex = 0;

        // 计算帧间隔（毫秒）
        var delay = (uint)Math.Max(1, 1000.0 / Fps);

        // 定义回调函数
        _callback = (_, _, _, _, _) =>
        {
            _frameIndex++;
            Tick?.Invoke(this, _frameIndex);
        };
        
        // 设置定时器
        _timerId = _TimeSetEvent(
            delay, 
            0, 
            _callback, 
            IntPtr.Zero,
            TimePeriodic | TimeCallbackFunction
        );
    }

    public void Stop()
    {
        if (!IsRunning) return;
        IsRunning = false;

        // 停止定时器
        if (_timerId != 0)
        {
            _TimeKillEvent(_timerId);
            _timerId = 0;
        }
        _callback = null;
    }
    
    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
    
    [LibraryImport("winmm.dll", EntryPoint = "timeSetEvent", SetLastError = true)]
    private static partial uint _TimeSetEvent(uint uDelay, uint uResolution, TimeProc lpTimeProc, IntPtr dwUser, uint fuEvent);

    [LibraryImport("winmm.dll", EntryPoint = "timeKillEvent", SetLastError = true)]
    private static partial void _TimeKillEvent(uint uTimerId);
    
    private delegate void TimeProc(uint id, uint msg, IntPtr user, IntPtr dw1, IntPtr dw2);
    
    private const uint TimePeriodic = 0x0001;
    private const uint TimeCallbackFunction = 0x0000;
}