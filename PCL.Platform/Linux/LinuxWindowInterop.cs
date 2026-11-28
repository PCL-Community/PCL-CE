namespace PCL.Platform;

/// <summary>Linux 游戏窗口交互：优先 X11/XWayland，未找到时再尝试原生 Wayland。</summary>
public sealed class LinuxWindowInterop : WindowInteropBase
{
    private readonly LinuxWaylandWindowInterop _wayland = new();
    private readonly LinuxX11WindowInterop _x11 = new();

    protected override (nint Handle, string Title)? FindGameWindowCore(int processId)
    {
        return _x11.TryFindGameWindow(processId) ?? _wayland.TryFindGameWindow(processId);
    }

    protected override void SetWindowTitleCore(nint handle, string title)
    {
        if (LinuxWaylandWindowInterop.IsWaylandHandle(handle))
            _wayland.SetWindowTitle(handle, title);
        else
            _x11.SetWindowTitle(handle, title);
    }

    protected override void MaximizeWindowCore(nint handle)
    {
        if (LinuxWaylandWindowInterop.IsWaylandHandle(handle))
            _wayland.MaximizeWindow(handle);
        else
            _x11.MaximizeWindow(handle);
    }
}