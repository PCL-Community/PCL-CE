using PCL.Core.Platform;

namespace PCL.Core.Avalonia;

/// <summary>
///     将 PCL.Core.Avalonia 的平台实现注册到 <see cref="PlatformServices" />。
///     必须在 PCL.Core 的生命周期启动之前调用。
/// </summary>
public static class PlatformRegistration
{
    public static void Register()
    {
        PlatformServices.UiDispatcher = new AvaloniaUiDispatcher();
        PlatformServices.FileDialogs = new AvaloniaFileDialogService();
        PlatformServices.DialogService = new AvaloniaDialogService();
        PlatformServices.ResourceStore = new AvaloniaResourceStore();
        PlatformServices.Localization = new AvaloniaLocalizationBackend();
        PlatformServices.Theme = new AvaloniaThemeBackend();
        PlatformServices.Clipboard = new AvaloniaClipboardService();
        PlatformServices.SystemProxy = new UnixSystemProxyProvider();
        PlatformServices.SystemTheme = new AvaloniaSystemThemeProvider();
        PlatformServices.Notification = new AvaloniaNotificationService();
    }
}