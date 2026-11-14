using PCL.Core.Platform;
// ReSharper disable once RedundantUsingDirective
using PCL.Core.Logging;

namespace PCL.Core.UI;

/// <summary>将当前平台模块注册到全局运行时。</summary>
public static class PlatformRegistration
{
    public static PlatformModule Register()
    {
        var module = PlatformModuleFactory.Create();
        PlatformRuntime.Initialize(module);

        PlatformServices.UiDispatcher = new UiDispatcher();
        PlatformServices.FileDialogs = module.FileDialogs;
        PlatformServices.DialogService = module.FatalDialog;
        PlatformServices.ResourceStore = new ResourceStore();
        PlatformServices.WindowInterop = module.WindowInterop;
        PlatformServices.Localization = new LocalizationBackend();
        PlatformServices.Theme = new ThemeBackend();
        PlatformServices.Clipboard = new ClipboardService();
        PlatformServices.SystemProxy = module.SystemProxy;
        PlatformServices.SystemTheme = module.SystemTheme;
        PlatformServices.Notification = module.Notifications;

        ValidateRequiredServices();
        return module;
    }

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