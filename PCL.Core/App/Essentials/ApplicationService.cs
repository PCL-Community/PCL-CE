using System;
using PCL.Core.App.IoC;
using PCL.Core.Platform;

namespace PCL.Core.App.Essentials;

[LifecycleService(LifecycleState.BeforeLoading, Priority = int.MinValue)]
[LifecycleScope("application", "应用程序", false)]
public sealed partial class ApplicationService
{
    public static Func<IApplicationHost>? Loading { private get; set; }

    [LifecycleStart]
    private static void _Start()
    {
        Context.Debug("正在初始化应用程序容器");
        var app = Loading!.Invoke();
        app.DispatcherUnhandledException += Lifecycle.OnException;
        app.Startup += Lifecycle.OnLoading;
        Lifecycle.CurrentApplication = app;
        Loading = null;
        Context.Trace("应用程序容器初始化完毕");
    }

    [LifecycleStop]
    private static void _Stop()
    {
        if (Lifecycle.IsForceShutdown)
        {
            Context.Warn("已指定强制关闭，跳过应用程序关闭流程");
            return;
        }

        Lifecycle.CurrentApplication?.Shutdown();
    }
}