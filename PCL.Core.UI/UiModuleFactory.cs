using PCL.Core.Platform;

namespace PCL.Core.UI;

/// <summary>根据已检测到的平台组装 Avalonia UI 服务。</summary>
public static class UiModuleFactory
{
    public static UiModule Create(PlatformKind kind)
    {
        return new UiModule
        {
            Dispatcher = new AvaloniaUiDispatcher(),
            Clipboard = new ClipboardService(),
            FileDialogs = kind switch
            {
                PlatformKind.Windows or PlatformKind.MacOs => new FileDialogService(),
                PlatformKind.Linux => new LinuxFileDialogService(),
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "不支持的平台")
            },
            ResourceStore = new ResourceStore(),
            Localization = new LocalizationBackend(),
            Theme = new ThemeBackend(),
            SystemTheme = kind switch
            {
                PlatformKind.Windows or PlatformKind.MacOs => new SystemThemeProvider(),
                PlatformKind.Linux => new LinuxSystemThemeProvider(),
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "不支持的平台")
            },
            Notifications = kind switch
            {
                PlatformKind.Windows => new WindowsNotificationService(),
                PlatformKind.Linux => new LinuxNotificationService(),
                PlatformKind.MacOs => new MacOsNotificationService(),
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "不支持的平台")
            }
        };
    }
}