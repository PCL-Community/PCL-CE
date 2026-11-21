using System.Collections.Generic;

namespace PCL.Core.Platform;

/// <summary>平台能力值集合。仅数据，不包含平台行为。</summary>
public sealed record PlatformCapabilities : IPlatformCapabilities
{
    public static PlatformCapabilities Windows { get; } = new()
    {
        SupportsBundledLauncher = true,
        SupportsShortcutIcon = false,
        NativeLibraryExtensions = [".dll"],
        SupportsWindowsStoreDependencies = true,
        SuppressAltMenu = true,
        SupportsDisplayProtocolSelection = false
    };

    public static PlatformCapabilities Linux { get; } = new()
    {
        SupportsBundledLauncher = false,
        SupportsShortcutIcon = true,
        NativeLibraryExtensions = [".dll", ".so", ".dylib"],
        SupportsWindowsStoreDependencies = false,
        SuppressAltMenu = false,
        SupportsDisplayProtocolSelection = true
    };

    public static PlatformCapabilities MacOs { get; } = new()
    {
        SupportsBundledLauncher = false,
        SupportsShortcutIcon = false,
        NativeLibraryExtensions = [".dll", ".so", ".dylib"],
        SupportsWindowsStoreDependencies = false,
        SuppressAltMenu = false,
        SupportsDisplayProtocolSelection = false
    };

    public required bool SupportsBundledLauncher { get; init; }
    public required bool SupportsShortcutIcon { get; init; }
    public required IReadOnlyList<string> NativeLibraryExtensions { get; init; }
    public required bool SupportsWindowsStoreDependencies { get; init; }
    public required bool SuppressAltMenu { get; init; }
    public required bool SupportsDisplayProtocolSelection { get; init; }
}