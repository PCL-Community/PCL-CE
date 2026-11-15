using PCL.Core.Platform;
using PCL.Core.Utils.Secret;

namespace PCL.Platform;

/// <summary>根据已检测到的平台组装平台模块。只负责组装，不承载业务逻辑。</summary>
public static class PlatformModuleFactory
{
    public static PlatformModule Create(PlatformKind kind)
    {
        return kind switch
        {
            PlatformKind.Windows => CreateWindows(),
            PlatformKind.Linux => CreateLinux(),
            PlatformKind.MacOs => CreateMacOs(),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "不支持的平台")
        };
    }

    private static PlatformModule CreateWindows()
    {
        return new PlatformModule
        {
            Kind = PlatformKind.Windows,
            Capabilities = PlatformCapabilities.Windows,
            SystemProxy = new WindowsSystemProxyProvider(),
            WindowInterop = new WindowsWindowInterop(),
            Shortcuts = new WindowsShortcutService(),
            Java = new WindowsJavaPlatformInfo(),
            ProcessCleanup = new WindowsProcessCleanupService(),
            GpuPreference = new WindowsGpuPreferenceService(),
            LaunchEnvironment = new UnsupportedLaunchEnvironmentService(),
            SystemFacts = new WindowsSystemFacts(),
            Hardware = new WindowsHardwareInfo(),
            Shell = new WindowsShellLauncher(),
            Region = new WindowsRegionSignalProvider(),
            RecycleBin = new WindowsRecycleBinService(),
            FatalDialog = new WindowsFatalDialogService(),
            KeyProtectorFactory = key => new WindowsKeyProtector(key)
        };
    }

    private static PlatformModule CreateLinux()
    {
        return new PlatformModule
        {
            Kind = PlatformKind.Linux,
            Capabilities = PlatformCapabilities.Linux,
            SystemProxy = new LinuxSystemProxyProvider(),
            WindowInterop = new LinuxWindowInterop(),
            Shortcuts = new LinuxShortcutService(),
            Java = new LinuxJavaPlatformInfo(),
            ProcessCleanup = new PosixProcessCleanupService(),
            GpuPreference = new UnsupportedGpuPreferenceService(),
            LaunchEnvironment = new LinuxLaunchEnvironmentService(),
            SystemFacts = new LinuxSystemFacts(),
            Hardware = new LinuxHardwareInfo(),
            Shell = new PosixShellLauncher("xdg-open"),
            Region = new PosixRegionSignalProvider(),
            RecycleBin = new LinuxRecycleBinService(),
            FatalDialog = new LinuxFatalDialogService(),
            KeyProtectorFactory = key => new UnixKeyProtector(key)
        };
    }

    private static PlatformModule CreateMacOs()
    {
        return new PlatformModule
        {
            Kind = PlatformKind.MacOs,
            Capabilities = PlatformCapabilities.MacOs,
            SystemProxy = new MacOsSystemProxyProvider(),
            WindowInterop = new MacOsWindowInterop(),
            Shortcuts = new MacOsShortcutService(),
            Java = new MacOsJavaPlatformInfo(),
            ProcessCleanup = new PosixProcessCleanupService(),
            GpuPreference = new UnsupportedGpuPreferenceService(),
            LaunchEnvironment = new UnsupportedLaunchEnvironmentService(),
            SystemFacts = new MacOsSystemFacts(),
            Hardware = new MacOsHardwareInfo(),
            Shell = new PosixShellLauncher("open"),
            Region = new PosixRegionSignalProvider(),
            RecycleBin = new MacOsRecycleBinService(),
            FatalDialog = new MacOsFatalDialogService(),
            KeyProtectorFactory = key => new UnixKeyProtector(key)
        };
    }
}