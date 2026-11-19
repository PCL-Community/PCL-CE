using System;

namespace PCL.Core.Platform;

/// <summary>Linux 显示协议偏好。</summary>
public enum LinuxDisplayProtocol
{
    /// <summary>根据当前桌面会话自动选择。</summary>
    Auto = 0,

    /// <summary>使用 X11 或 XWayland。</summary>
    X11 = 1,

    /// <summary>使用原生 Wayland。</summary>
    Wayland = 2
}

/// <summary>
///     Linux 显示协议解析。自动模式按实际桌面会话判断：
///     Wayland 会话优先原生 Wayland，X11 会话保持 X11/XWayland；
///     会话信息缺失时保守回退到 X11。
/// </summary>
public static class LinuxDisplayProtocolResolver
{
    public static LinuxDisplayProtocol ResolveConfigured(int value)
    {
        return value switch
        {
            1 => LinuxDisplayProtocol.X11,
            2 => LinuxDisplayProtocol.Wayland,
            _ => LinuxDisplayProtocol.Auto
        };
    }

    public static LinuxDisplayProtocol ResolveAuto()
    {
        return ResolveAuto(
            Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"),
            Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"),
            Environment.GetEnvironmentVariable("DISPLAY"));
    }

    public static LinuxDisplayProtocol ResolveAuto(
        string? sessionType,
        string? waylandDisplay,
        string? display)
    {
        var session = sessionType?.Trim();
        if (string.Equals(session, "wayland", StringComparison.OrdinalIgnoreCase))
            return LinuxDisplayProtocol.Wayland;
        if (string.Equals(session, "x11", StringComparison.OrdinalIgnoreCase))
            return LinuxDisplayProtocol.X11;

        if (!string.IsNullOrWhiteSpace(waylandDisplay)
            && string.IsNullOrWhiteSpace(display))
            return LinuxDisplayProtocol.Wayland;

        return LinuxDisplayProtocol.X11;
    }

    public static LinuxDisplayProtocol ResolveEffective(int configured)
    {
        var protocol = ResolveConfigured(configured);
        return protocol == LinuxDisplayProtocol.Auto ? ResolveAuto() : protocol;
    }
}