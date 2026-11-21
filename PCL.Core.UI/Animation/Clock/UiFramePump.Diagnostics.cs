#if DEBUG
using System.Diagnostics;
using PCL.Core.Logging;

namespace PCL.Core.UI.Animation.Clock;

internal static partial class UiFramePump
{
    private const int StatWindowMilliseconds = 5000;
    private const double SlowFrameMilliseconds = 100d;

    private static long _statStartStamp;
    private static long _lastTickStamp;
    private static int _statTicks;
    private static int _statRafCalls;
    private static int _statTimerCalls;
    private static int _statSlowTicks;
    private static double _statMaxIntervalMs;
    private static double _statMaxTickMs;

    private static void DiagStart()
    {
        _statStartStamp = Stopwatch.GetTimestamp();
        _statTicks = 0;
        _statRafCalls = 0;
        _statTimerCalls = 0;
        _statSlowTicks = 0;
        _statMaxIntervalMs = 0d;
        _statMaxTickMs = 0d;
        _lastTickStamp = 0;
    }

    private static void DiagStop()
    {
        _lastTickStamp = 0;
        _statSlowTicks = 0;
        _statMaxIntervalMs = 0d;
        _statMaxTickMs = 0d;
        LogWrapper.Debug(Module, "[Animation] 帧源已停止 clock=stopped");
    }

    private static void DiagRafCall()
    {
        _statRafCalls++;
    }

    private static void DiagTimerCall()
    {
        _statTimerCalls++;
    }

    private static long DiagTickStart()
    {
        _statTicks++;
        var tickStamp = Stopwatch.GetTimestamp();
        if (_lastTickStamp != 0)
        {
            var intervalMs =
                (tickStamp - _lastTickStamp) * 1000d / Stopwatch.Frequency;
            if (intervalMs > _statMaxIntervalMs)
                _statMaxIntervalMs = intervalMs;
            if (intervalMs > SlowFrameMilliseconds)
                _statSlowTicks++;
        }

        _lastTickStamp = tickStamp;
        return tickStamp;
    }

    private static void DiagTickEnd(long tickStamp)
    {
        var tickMs = (Stopwatch.GetTimestamp() - tickStamp) * 1000d / Stopwatch.Frequency;
        if (tickMs > _statMaxTickMs)
            _statMaxTickMs = tickMs;

        var elapsed = Stopwatch.GetTimestamp() - _statStartStamp;
        if (elapsed < StatWindowMilliseconds * (Stopwatch.Frequency / 1000d)) return;

        var fps = _statTicks * 1000d / (elapsed * 1000d / Stopwatch.Frequency);
        _statStartStamp = Stopwatch.GetTimestamp();
        _statTicks = 0;
        LogWrapper.Debug(
            Module,
            $"[Animation] 帧驱动模式 clock={(_frameTimer is null ? "raf" : "timer")} "
            + $"fps≈{Math.Round(fps, 1)} "
            + $"rafCalls={_statRafCalls} timerCalls={_statTimerCalls} "
            + $"maxFrame={_statMaxIntervalMs:0.#}ms "
            + $"maxTick={_statMaxTickMs:0.#}ms "
            + $"slowFrames={_statSlowTicks}");
        _statMaxIntervalMs = 0d;
        _statMaxTickMs = 0d;
        _statSlowTicks = 0;
    }
}
#endif