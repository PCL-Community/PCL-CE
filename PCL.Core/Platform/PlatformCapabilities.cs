using System;
using System.Collections.Generic;
namespace PCL.Core.Platform;

/// <summary>平台能力值集合。仅数据，不包含平台行为。</summary>
public sealed record PlatformCapabilities : IPlatformCapabilities
{
    public required string JavaExecutableName { get; init; }
    public required bool SupportsJavaw { get; init; }
    public required bool SupportsNativeMenu { get; init; }
    public required bool SupportsTaskbarProgress { get; init; }
    public required bool SupportsInAppUpdate { get; init; }
    public required bool SupportsBundledLauncher { get; init; }
    public required bool SupportsShortcutIcon { get; init; }
    public required IReadOnlyList<string> NativeLibraryExtensions { get; init; }
    public required bool SupportsWindowsStoreDependencies { get; init; }
    public required bool UsesWindowsPaths { get; init; }
    public required bool SupportsGpuPreference { get; init; }
    public required bool SuppressAltMenu { get; init; }
    public required bool SupportsDisplayProtocolSelection { get; init; }

    public static PlatformCapabilities Windows { get; } = new()
    {
        JavaExecutableName = "java.exe",
        SupportsJavaw = true,
        SupportsNativeMenu = false,
        SupportsTaskbarProgress = true,
        SupportsInAppUpdate = true,
        SupportsBundledLauncher = true,
        SupportsShortcutIcon = false,
        NativeLibraryExtensions = [".dll"],
        SupportsWindowsStoreDependencies = true,
        UsesWindowsPaths = true,
        SupportsGpuPreference = true,
        SuppressAltMenu = true,
        SupportsDisplayProtocolSelection = false
    };

    public static PlatformCapabilities Linux { get; } = new()
    {
        JavaExecutableName = "java",
        SupportsJavaw = false,
        SupportsNativeMenu = false,
        SupportsTaskbarProgress = true,
        SupportsInAppUpdate = false,
        SupportsBundledLauncher = false,
        SupportsShortcutIcon = true,
        NativeLibraryExtensions = [".dll", ".so", ".dylib"],
        SupportsWindowsStoreDependencies = false,
        UsesWindowsPaths = false,
        SupportsGpuPreference = false,
        SuppressAltMenu = false,
        SupportsDisplayProtocolSelection = true
    };

    public static PlatformCapabilities MacOs { get; } = new()
    {
        JavaExecutableName = "java",
        SupportsJavaw = false,
        SupportsNativeMenu = true,
        SupportsTaskbarProgress = false,
        SupportsInAppUpdate = false,
        SupportsBundledLauncher = false,
        SupportsShortcutIcon = false,
        NativeLibraryExtensions = [".dll", ".so", ".dylib"],
        SupportsWindowsStoreDependencies = false,
        UsesWindowsPaths = false,
        SupportsGpuPreference = false,
        SuppressAltMenu = false,
        SupportsDisplayProtocolSelection = false
    };
}
