using System.Collections.Concurrent;
using PCL.Core.Logging;

namespace PCL.Platform;

/// <summary>原生 Wayland 游戏窗口交互；按可用协议选择 KWin 或 wlroots 后端。</summary>
internal sealed class LinuxWaylandWindowInterop : WindowInteropBase
{
    private const ulong HandleTag = 0x574C_0000_0000_0000UL;
    private const ulong HandleTagMask = 0xFFFF_0000_0000_0000UL;

    private readonly LinuxForeignToplevelBackend _foreignToplevel = new();
    private readonly ConcurrentDictionary<(WaylandWindowKind Kind, nint Handle), nint> _handles = new();
    private readonly LinuxPlasmaWindowBackend _plasma = new();
    private readonly ConcurrentDictionary<nint, WaylandWindowRef> _windows = new();

    private long _nextHandle;
    private int _titleUnsupportedLogged;

    public static bool IsWaylandHandle(nint handle)
    {
        return ((ulong)handle & HandleTagMask) == HandleTag;
    }

    protected override (nint Handle, string Title)? FindGameWindowCore(int processId)
    {
        if (_plasma.TryFindWindow(processId) is { } plasma)
            return _Register(WaylandWindowKind.Plasma, plasma.Handle, plasma.Title);

        if (_foreignToplevel.TryFindWindow() is { } foreign)
            return _Register(WaylandWindowKind.ForeignToplevel, foreign.Handle, foreign.Title);

        return null;
    }

    protected override void SetWindowTitleCore(nint handle, string title)
    {
        if (!_windows.ContainsKey(handle))
            return;

        if (Interlocked.Exchange(ref _titleUnsupportedLogged, 1) == 0)
            LogWrapper.Debug(
                ModuleName,
                "原生 Wayland 不允许修改其他客户端窗口标题，已跳过自定义游戏窗口标题");
    }

    protected override void MaximizeWindowCore(nint handle)
    {
        if (!_windows.TryGetValue(handle, out var window))
            return;

        switch (window.Kind)
        {
            case WaylandWindowKind.Plasma:
                _plasma.Maximize(window.Handle);
                break;
            case WaylandWindowKind.ForeignToplevel:
                _foreignToplevel.Maximize(window.Handle);
                break;
        }
    }

    private (nint Handle, string Title) _Register(
        WaylandWindowKind kind,
        nint backendHandle,
        string title)
    {
        var externalHandle = _handles.GetOrAdd(
            (kind, backendHandle),
            _ => unchecked(
                (nint)(HandleTag | (ulong)Interlocked.Increment(ref _nextHandle))));

        _windows[externalHandle] = new WaylandWindowRef(kind, backendHandle);
        return (externalHandle, title);
    }

    private readonly record struct WaylandWindowRef(
        WaylandWindowKind Kind,
        nint Handle);

    private enum WaylandWindowKind
    {
        Plasma,
        ForeignToplevel
    }
}