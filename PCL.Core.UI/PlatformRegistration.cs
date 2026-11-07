using PCL.Core.Logging;
using PCL.Core.Platform;

namespace PCL.Core.UI;

/// <summary>
///     将 PCL.Core.UI 的平台实现注册到 <see cref="PlatformServices" />。
///     必须在 PCL.Core 的生命周期启动之前调用。
/// </summary>
public static class PlatformRegistration
{
    public static void Register()
    {
        PlatformServices.UiDispatcher = new UiDispatcher();
        PlatformServices.FileDialogs = new FileDialogService();
        PlatformServices.DialogService = new FatalDialogService();
        PlatformServices.ResourceStore = new ResourceStore();
        PlatformServices.WindowInterop = new WindowInterop();
        PlatformServices.Localization = new LocalizationBackend();
        PlatformServices.Theme = new ThemeBackend();
        PlatformServices.Clipboard = new ClipboardService();
        PlatformServices.SystemProxy = new UnixSystemProxyProvider();
        PlatformServices.SystemTheme = new SystemThemeProvider();
        PlatformServices.Notification = new NotificationService();
        ValidateRequiredServices();
    }

    /// <summary>
    ///     校验全部平台服务均已注册。Debug/CI 构建直接抛异常以便开发期尽早发现，Release 仅记录错误日志。
    /// </summary>
    public static void ValidateRequiredServices()
    {
        var missing = new List<string>();
        Check(nameof(PlatformServices.UiDispatcher), PlatformServices.UiDispatcher);
        Check(nameof(PlatformServices.FileDialogs), PlatformServices.FileDialogs);
        Check(nameof(PlatformServices.DialogService), PlatformServices.DialogService);
        Check(nameof(PlatformServices.ResourceStore), PlatformServices.ResourceStore);
        Check(nameof(PlatformServices.WindowInterop), PlatformServices.WindowInterop);
        Check(nameof(PlatformServices.Localization), PlatformServices.Localization);
        Check(nameof(PlatformServices.Theme), PlatformServices.Theme);
        Check(nameof(PlatformServices.Clipboard), PlatformServices.Clipboard);
        Check(nameof(PlatformServices.SystemProxy), PlatformServices.SystemProxy);
        Check(nameof(PlatformServices.SystemTheme), PlatformServices.SystemTheme);
        Check(nameof(PlatformServices.Notification), PlatformServices.Notification);

        if (missing.Count == 0) return;

        var message = $"平台服务未注册：{string.Join("、", missing)}";
#if DEBUG
        throw new InvalidOperationException(message);
#else
        LogWrapper.Error("UI", message);
#endif

        void Check(string name, object? service)
        {
            if (service is null) missing.Add(name);
        }
    }
}