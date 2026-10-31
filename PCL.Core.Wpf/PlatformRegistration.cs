using PCL.Core.Platform;

namespace PCL.Core.Wpf;

/// <summary>
///     将 PCL.Core.Wpf 的平台实现注册到 <see cref="PlatformServices" />。
///     必须在 PCL.Core 的生命周期启动之前调用。
/// </summary>
public static class PlatformRegistration
{
    public static void Register()
    {
        PlatformServices.UiDispatcher = new WpfUiDispatcher();
        PlatformServices.FileDialogs = new WpfFileDialogService();
        PlatformServices.DialogService = new WpfDialogService();
        PlatformServices.ResourceStore = new WpfResourceStore();
        PlatformServices.Localization = new WpfLocalizationBackend();
        PlatformServices.Theme = new WpfThemeBackend();
        PlatformServices.Clipboard = new WpfClipboardService();
    }
}