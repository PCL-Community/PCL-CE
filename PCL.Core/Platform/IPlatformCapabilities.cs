using System.Collections.Generic;

namespace PCL.Core.Platform;

/// <summary>平台能力。每个平台模块必须显式返回值，不允许共享代码推导。</summary>
public interface IPlatformCapabilities
{
    string JavaExecutableName { get; }
    bool SupportsJavaw { get; }
    bool SupportsNativeMenu { get; }
    bool SupportsTaskbarProgress { get; }
    bool SupportsInAppUpdate { get; }
    bool SupportsBundledLauncher { get; }
    bool SupportsShortcutIcon { get; }
    IReadOnlyList<string> NativeLibraryExtensions { get; }
    bool SupportsWindowsStoreDependencies { get; }
    bool UsesWindowsPaths { get; }
    bool SupportsGpuPreference { get; }
    bool SuppressAltMenu { get; }
    bool SupportsDisplayProtocolSelection { get; }
}