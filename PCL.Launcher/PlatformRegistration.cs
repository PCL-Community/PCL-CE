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
        ArgumentNullException.ThrowIfNull(services.Platform);
        ArgumentNullException.ThrowIfNull(services.Ui);
    }
}