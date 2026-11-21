using System;

namespace PCL.Core.Platform;

/// <summary>唯一的运行时平台识别入口。</summary>
public static class PlatformDetector
{
    /// <summary>检测当前操作系统对应的平台。</summary>
    /// <exception cref="PlatformNotSupportedException">当前操作系统不受支持。</exception>
    public static PlatformKind Detect()
    {
        if (OperatingSystem.IsWindows()) return PlatformKind.Windows;
        if (OperatingSystem.IsLinux()) return PlatformKind.Linux;
        if (OperatingSystem.IsMacOS()) return PlatformKind.MacOs;
        throw new PlatformNotSupportedException("当前操作系统不受 PCL CE 支持。");
    }
}