using System.Collections.Generic;

namespace PCL.Core.Platform;

/// <summary>平台能力。每个平台模块必须显式返回值，不允许共享代码推导。</summary>
public interface IPlatformCapabilities
{
    bool SupportsBundledLauncher { get; }
    bool SupportsShortcutIcon { get; }
    IReadOnlyList<string> NativeLibraryExtensions { get; }
    bool SupportsWindowsStoreDependencies { get; }
    bool SuppressAltMenu { get; }
    bool SupportsDisplayProtocolSelection { get; }
}