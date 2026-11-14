using PCL.Core.App;
using PCL.Core.App.Region;
using PCL.Core.IO;
using PCL.Core.Minecraft.Java;
using PCL.Core.Platform;
using PCL.Core.Utils.OS;
using PCL.Core.Utils.Secret;

namespace PCL.Core.UI;

/// <summary>唯一的平台选择入口。只负责组装平台模块，不承载业务逻辑。</summary>
public static class PlatformModuleFactory
{
    public static PlatformModule Create()
    {
        if (OperatingSystem.IsWindows()) return CreateWindows();
        if (OperatingSystem.IsMacOS()) return CreateMacOs();
        return CreateLinux();
    }

    private static PlatformModule CreateWindows()
    {
        return new PlatformModule
        {
            Kind = PlatformKind.Windows,
            Capabilities = PlatformCapabilities.Windows,
            FileDialogs = new FileDialogService(),
            SystemProxy = new WindowsSystemProxyProvider(),
            SystemTheme = new SystemThemeProvider(),
            Notifications = new WindowsNotificationService(),
            WindowInterop = new WindowsWindowInterop(),
            Shortcuts = new WindowsShortcutService(),
            Java = new WindowsJavaPlatformInfo(),
            ProcessCleanup = new WindowsProcessCleanupService(),
            GpuPreference = new WindowsGpuPreferenceService(),
            SystemFacts = new WindowsSystemFacts(),
            Hardware = new WindowsHardwareInfo(),
            Shell = new WindowsShellLauncher(),
            Region = new WindowsRegionSignalProvider(),
            RecycleBin = new WindowsRecycleBinService(),
            FatalDialog = new FatalDialogService(WindowsNativeFatalDialog.Show),
            KeyProtectorFactory = key => new WindowsKeyProtector(key)
        };
    }

    private static PlatformModule CreateLinux()
    {
        return new PlatformModule
        {
            Kind = PlatformKind.Linux,
            Capabilities = PlatformCapabilities.Linux,
            FileDialogs = new LinuxFileDialogService(),
            SystemProxy = new LinuxSystemProxyProvider(),
            SystemTheme = new LinuxSystemThemeProvider(),
            Notifications = new LinuxNotificationService(),
            WindowInterop = new LinuxWindowInterop(),
            Shortcuts = new LinuxShortcutService(),
            Java = new LinuxJavaPlatformInfo(),
            ProcessCleanup = new PosixProcessCleanupService(),
            GpuPreference = new UnsupportedGpuPreferenceService(),
            SystemFacts = new LinuxSystemFacts(),
            Hardware = new LinuxHardwareInfo(),
            Shell = new PosixShellLauncher("xdg-open"),
            Region = new PosixRegionSignalProvider(),
            RecycleBin = new LinuxRecycleBinService(),
            FatalDialog = new FatalDialogService(LinuxNativeFatalDialog.Show),
            KeyProtectorFactory = key => new UnixKeyProtector(key)
        };
    }

    private static PlatformModule CreateMacOs()
    {
        return new PlatformModule
        {
            Kind = PlatformKind.MacOs,
            Capabilities = PlatformCapabilities.MacOs,
            FileDialogs = new FileDialogService(),
            SystemProxy = new MacOsSystemProxyProvider(),
            SystemTheme = new SystemThemeProvider(),
            Notifications = new MacOsNotificationService(),
            WindowInterop = new MacOsWindowInterop(),
            Shortcuts = new MacOsShortcutService(),
            Java = new MacOsJavaPlatformInfo(),
            ProcessCleanup = new PosixProcessCleanupService(),
            GpuPreference = new UnsupportedGpuPreferenceService(),
            SystemFacts = new MacOsSystemFacts(),
            Hardware = new MacOsHardwareInfo(),
            Shell = new PosixShellLauncher("open"),
            Region = new PosixRegionSignalProvider(),
            RecycleBin = new MacOsRecycleBinService(),
            FatalDialog = new FatalDialogService(MacOsNativeFatalDialog.Show),
            KeyProtectorFactory = key => new UnixKeyProtector(key)
        };
    }
}