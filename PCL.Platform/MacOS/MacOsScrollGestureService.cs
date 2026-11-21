using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using PCL.Core.Logging;
using PCL.Core.Platform;
using PlainToolkit.ObjectiveC.Runtime;

namespace PCL.Platform;

/// <summary>
///     补齐 Avalonia.Native 没有传给托管层的触控板滚动信息。
///     带增量的滚动事件在 -[NSView scrollWheel:] 中同步派发，可以直接读 NSApp.currentEvent；
///     手指抬起、惯性结束是零增量事件，会被 Avalonia.Native 丢弃，只能通过 NSEvent 本地监视器接收。
/// </summary>
public sealed class MacOsScrollGestureService : IScrollGestureService
{
    private const string SystemLibrary = "/usr/lib/libSystem.B.dylib";
    private const ulong ScrollWheelEventType = 22; // NSEventTypeScrollWheel
    private const ulong ScrollWheelEventMask = 1UL << 22;
    private const int BlockIsGlobal = 1 << 28;

    private static readonly ObjCSel _SharedApplication = ObjCSel.FromString("sharedApplication");
    private static readonly ObjCSel _CurrentEvent = ObjCSel.FromString("currentEvent");
    private static readonly ObjCSel _Type = ObjCSel.FromString("type");
    private static readonly ObjCSel _HasPreciseScrollingDeltas = ObjCSel.FromString("hasPreciseScrollingDeltas");
    private static readonly ObjCSel _Phase = ObjCSel.FromString("phase");
    private static readonly ObjCSel _MomentumPhase = ObjCSel.FromString("momentumPhase");
    private static readonly ObjCSel _AddLocalMonitor =
        ObjCSel.FromString("addLocalMonitorForEventsMatchingMask:handler:");
    private static readonly ObjCSel _Retain = ObjCSel.FromString("retain");

    private static bool _monitorInstalled;
    private static MacOsScrollGestureService? _phaseListener;

    public double PreciseDeltaScale => 50d; // Avalonia.Native 会把精确滚动的像素增量除以 50

    /// <summary>精确滚动设备的手指按下、抬起、取消及惯性开始、结束。在 UI 线程上、事件派发给窗口之前触发。</summary>
    public event Action<ScrollGestureInfo>? PhaseChanged;

    /// <summary>读取正在派发的滚动事件；不在滚动事件派发过程中时返回 false。</summary>
    public bool TryGetCurrent(out ScrollGestureInfo info)
    {
        info = default;
        var application = ObjCObject.GetClass("NSApplication").SendMessage<ObjCObject>(_SharedApplication);
        if (application.IsNull) return false;

        var current = application.SendMessage<ObjCObject>(_CurrentEvent);
        if (current.IsNull || current.SendMessage<ulong>(_Type) != ScrollWheelEventType) return false;

        info = _Read(current);
        return true;
    }

    /// <summary>安装 NSEvent 本地监视器，进程内只安装一次，之后常驻。</summary>
    public unsafe bool EnsurePhaseTracking()
    {
        _phaseListener = this;
        if (_monitorInstalled) return true;

        try
        {
            var block = (BlockLiteral*)NativeMemory.Alloc((nuint)sizeof(BlockLiteral));
            var descriptor = (BlockDescriptor*)NativeMemory.Alloc((nuint)sizeof(BlockDescriptor));
            *descriptor = new BlockDescriptor { Size = (nuint)sizeof(BlockLiteral) };
            *block = new BlockLiteral
            {
                Isa = NativeLibrary.GetExport(NativeLibrary.Load(SystemLibrary), "_NSConcreteGlobalBlock"),
                Flags = BlockIsGlobal,
                Invoke = (nint)(delegate* unmanaged[Cdecl]<nint, nint, nint>)&_OnMonitorEvent,
                Descriptor = (nint)descriptor
            };

            var monitor = ObjCObject.GetClass("NSEvent")
                .SendMessage<ulong, nint, ObjCObject>(_AddLocalMonitor, ScrollWheelEventMask, (nint)block);
            if (monitor.IsNull) return false;

            monitor.SendMessage(_Retain);
            _monitorInstalled = true;
            return true;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Platform", "安装 macOS 滚动手势监视器失败");
            return false;
        }
    }

    private static ScrollGestureInfo _Read(ObjCObject scrollEvent)
    {
        return new ScrollGestureInfo(
            scrollEvent.SendMessage<bool>(_HasPreciseScrollingDeltas),
            (ScrollGesturePhase)scrollEvent.SendMessage<ulong>(_Phase),
            (ScrollGesturePhase)scrollEvent.SendMessage<ulong>(_MomentumPhase));
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static nint _OnMonitorEvent(nint block, nint scrollEvent)
    {
        try
        {
            if (scrollEvent != 0 && _phaseListener?.PhaseChanged is { } handler)
            {
                var info = _Read(new ObjCObject(scrollEvent));
                const ScrollGesturePhase reported = ScrollGesturePhase.Began | ScrollGesturePhase.Ended
                    | ScrollGesturePhase.Cancelled | ScrollGesturePhase.MayBegin;
                if (info.HasPreciseDeltas
                    && ((info.Phase & reported) != 0
                        || (info.MomentumPhase & (ScrollGesturePhase.Began | ScrollGesturePhase.Ended)) != 0))
                    handler(info);
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Platform", "处理 macOS 滚动手势阶段出错");
        }

        return scrollEvent;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BlockLiteral
    {
        public nint Isa;
        public int Flags;
        public int Reserved;
        public nint Invoke;
        public nint Descriptor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BlockDescriptor
    {
        public nuint Reserved;
        public nuint Size;
    }
}
