using PCL.Core.Platform;
using PCL.Core.UI;
using PCL.Platform;
// ReSharper disable once RedundantUsingDirective
using PCL.Core.Logging;

namespace PCL;

/// <summary>组装指定平台的服务并初始化应用运行时。</summary>
public static class PlatformRegistration
{
    public static ApplicationServices Register(PlatformKind kind)
    {
        var platform = PlatformModuleFactory.Create(kind);
        var ui = UiModuleFactory.Create(kind);
        var services = new ApplicationServices
        {
            Platform = platform,
            Ui = ui
        };

        ValidateRequiredServices(services);
        ApplicationRuntime.Initialize(services);
        return services;
    }

    public static void ValidateRequiredServices(ApplicationServices services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var missing = new List<string>();
        Check(nameof(services.Platform), services.Platform);
        Check(nameof(services.Ui), services.Ui);

        if (services.Platform is { } platform)
        {
            Check(nameof(platform.FatalDialog), platform.FatalDialog);
            Check(nameof(platform.SystemProxy), platform.SystemProxy);
            Check(nameof(platform.WindowInterop), platform.WindowInterop);
        }

        if (services.Ui is { } ui)
        {
            Check(nameof(ui.Dispatcher), ui.Dispatcher);
            Check(nameof(ui.Clipboard), ui.Clipboard);
            Check(nameof(ui.FileDialogs), ui.FileDialogs);
            Check(nameof(ui.ResourceStore), ui.ResourceStore);
            Check(nameof(ui.Localization), ui.Localization);
            Check(nameof(ui.Theme), ui.Theme);
            Check(nameof(ui.SystemTheme), ui.SystemTheme);
            Check(nameof(ui.Notifications), ui.Notifications);
        }

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