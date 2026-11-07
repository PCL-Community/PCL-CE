using System.Diagnostics;
using Avalonia.Threading;
using PCL.Core.Logging;
using PCL.Core.UI.Animation.Clock;

namespace PCL;

/// <summary>
///     过程式动画 API：用 <c>Aa*</c> 系列方法拼出动画组，交给 <c>AniStart</c> 播放。
///     帧循环由 <see cref="AvaloniaRenderingClock" /> 驱动，窗口不可用时回退到 <see cref="DispatcherTimer" />。
///     所有接口都应在 UI 线程调用；帧回调只处理到期的动画组。
/// </summary>
public static partial class Animations
{
    private static bool _aniInFrame;
    private static AvaloniaRenderingClock? _aniClock;

    /// <summary>
    ///     动画模块的帧循环是否正在运行。
    /// </summary>
    public static bool IsRunning => _aniClock?.IsRunning == true;

    /// <summary>
    ///     开始动画执行。帧循环与动画引擎共用同一个帧源，不会另建定时器。
    /// </summary>
    public static void AniStart()
    {
        if (_aniRunning) return;

        _aniLastTick = Stopwatch.GetTimestamp();
        _aniRunning = true;

        RunOnUi(() =>
        {
            if (_aniClock is null)
            {
                _aniClock = new AvaloniaRenderingClock(int.MaxValue);
                _aniClock.Tick += _AniOnRendering;
            }

            _aniClock.Start();
#if DEBUG
            LogWrapper.Debug("Animation", "[Animation] 动画帧循环已启动（与动画引擎共用帧源）");
#endif
        });
    }

    /// <summary>
    ///     停止动画执行并解除帧循环。
    /// </summary>
    public static void AniStopAll()
    {
        _aniRunning = false;

        RunOnUi(() =>
        {
            if (_aniClock is null) return;
            _aniClock.Tick -= _AniOnRendering;
            _aniClock.Stop();
            _aniClock = null;
        });

        lock (AniGroupsLock)
        {
            foreach (var entry in AniByName.Values)
                entry.Dead = true;
            AniByName.Clear();
            AniPendingAdds.Clear();
            _aniOrderDirty = true;
        }
    }

    /// <summary>
    ///     Advance the animations by one frame. Raised once per rendered frame, on the UI thread.
    /// </summary>
    private static void _AniOnRendering(object? sender, long frameIndex)
    {
        if (_aniInFrame) return; // 部分平台会同步重入
        _aniInFrame = true;

        try
        {
            var now = Stopwatch.GetTimestamp();
            var speed = AniSpeed;
            if (speed <= 0d)
            {
                _AniFlushGroups();
                _AniFinishAllGroupsInstantly();
                return;
            }

            var deltaTick = (int)Math.Round(
                Math.Clamp(_AniMs(now - _aniLastTick), 0d, 100000d) * speed);
            _aniLastTick = now;

            if (deltaTick <= 0)
            {
                _AniFlushGroups(); // still publish starts and stops, so nothing piles up
                return;
            }

            AniTimer(deltaTick);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Animation", "动画帧执行失败");
        }
        finally
        {
            _aniInFrame = false;
        }
    }

    /// <summary>
    ///     执行一次动画刻（默认由帧循环调用，也可手动驱动以便测试）。
    /// </summary>
    /// <param name="deltaTick">距离上一刻的毫秒数（已乘上 <see cref="AniSpeed" />）。</param>
    public static void AniTimer(int deltaTick)
    {
        try
        {
            _AniFlushGroups();

            var groups = AniOrder;
            for (var i = 0; i < groups.Count; i++)
            {
                var entry = groups[i];
                if (entry.Dead) continue;
                if (entry.startTick > _aniLastTick) continue;

                try
                {
                    _AniRunGroup(entry, deltaTick);
                }
                catch (Exception ex)
                {
                    LogWrapper.Warn(ex, "Animation", "动画组执行失败");
                }
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Animation", "动画刻执行失败");
        }
    }

    /// <summary>
    ///     Apply the changes collected since the last frame.
    /// </summary>
    private static void _AniFlushGroups()
    {
        lock (AniGroupsLock)
        {
            if (AniPendingAdds.Count > 0)
            {
                foreach (var entry in AniPendingAdds)
                    if (!entry.Dead)
                        AniOrder.Add(entry);
                AniPendingAdds.Clear();
            }

            if (_aniOrderDirty)
            {
                AniOrder.RemoveAll(static e => e.Dead);
                _aniOrderDirty = false;
            }
        }
    }

    /// <summary>
    ///     在 UI 线程执行；已在 UI 线程时直接执行，否则排队。
    /// </summary>
    private static void RunOnUi(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.Post(action);
    }

    /// <summary>Convert a stopwatch tick delta to milliseconds.</summary>
    internal static double _AniMs(long ticks)
    {
        return ticks * 1000d / Stopwatch.Frequency;
    }
}