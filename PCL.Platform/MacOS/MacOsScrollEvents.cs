using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using PCL.Core.Logging;
using PlainToolkit.ObjectiveC.Runtime;

namespace PCL.Platform;

/// <summary>滚动手势阶段，对应 AppKit 的 NSEventPhase。</summary>
[Flags]
public enum MacOsScrollPhase : ulong
{
    None = 0,
    Began = 1,
    Stationary = 2,
    Changed = 4,
    Ended = 8,
    Cancelled = 16,
    MayBegin = 32
}

public readonly record struct MacOsScrollEventInfo(
    bool HasPreciseDeltas,
    MacOsScrollPhase Phase,
    MacOsScrollPhase MomentumPhase)
{
    public bool IsMomentum => MomentumPhase != MacOsScrollPhase.None;
}

/// <summary>
///     补齐 Avalonia.Native 没有传给托管层的触控板滚动信息。
///     带增量的滚动事件在 -[NSView scrollWheel:] 中同步派发，可以直接读 NSApp.currentEvent；
///     手指抬起、惯性结束是零增量事件，会被 Avalonia.Native 丢弃，只能通过 NSEvent 本地监视器接收。
/// </summary>
[SupportedOSPlatform("macos")]
public static class MacOsScrollEvents
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

    /// <summary>精确滚动设备的手指按下、抬起、取消及惯性开始、结束。在 UI 线程上、事件派发给窗口之前触发。</summary>
    public static event Action<MacOsScrollEventInfo>? PhaseChanged;

    /// <summary>读取正在派发的滚动事件；不在滚动事件派发过程中时返回 false。</summary>
    public static bool TryGetCurrent(out MacOsScrollEventInfo info)
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
    public static unsafe bool EnsurePhaseMonitor()
    {
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

    private static MacOsScrollEventInfo _Read(ObjCObject scrollEvent)
    {
        return new MacOsScrollEventInfo(
            scrollEvent.SendMessage<bool>(_HasPreciseScrollingDeltas),
            (MacOsScrollPhase)scrollEvent.SendMessage<ulong>(_Phase),
            (MacOsScrollPhase)scrollEvent.SendMessage<ulong>(_MomentumPhase));
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static nint _OnMonitorEvent(nint block, nint scrollEvent)
    {
        try
        {
            if (scrollEvent != 0 && PhaseChanged is { } handler)
            {
                var info = _Read(new ObjCObject(scrollEvent));
                const MacOsScrollPhase reported = MacOsScrollPhase.Began | MacOsScrollPhase.Ended
                    | MacOsScrollPhase.Cancelled | MacOsScrollPhase.MayBegin;
                if (info.HasPreciseDeltas
                    && ((info.Phase & reported) != 0
                        || (info.MomentumPhase & (MacOsScrollPhase.Began | MacOsScrollPhase.Ended)) != 0))
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
