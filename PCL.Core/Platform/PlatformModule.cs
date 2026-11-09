using System;
using PCL.Core.Utils.Secret;

namespace PCL.Core.Platform;

/// <summary>一个平台的完整服务集合。由组合根在启动时构造一次。</summary>
public sealed class PlatformModule
{
    public required PlatformKind Kind { get; init; }
    public required IPlatformCapabilities Capabilities { get; init; }
    public required IFileDialogService FileDialogs { get; init; }
    public required ISystemProxyProvider SystemProxy { get; init; }
    public required ISystemThemeProvider SystemTheme { get; init; }
    public required ISystemNotificationService Notifications { get; init; }
    public required IWindowInterop WindowInterop { get; init; }
    public required IShortcutService Shortcuts { get; init; }
    public required IJavaPlatformInfo Java { get; init; }
    public required IProcessCleanupService ProcessCleanup { get; init; }
    public required IGpuPreferenceService GpuPreference { get; init; }
    public required ISystemFacts SystemFacts { get; init; }
    public required IHardwareInfo Hardware { get; init; }
    public required IShellLauncher Shell { get; init; }
    public required IRegionSignalProvider Region { get; init; }
    public required IRecycleBinService RecycleBin { get; init; }
    public required IFatalDialogService FatalDialog { get; init; }
    public required Func<byte[], IKeyProtector> KeyProtectorFactory { get; init; }

    public IKeyProtector CreateKeyProtector(byte[] key)
    {
        return KeyProtectorFactory(key);
    }
}