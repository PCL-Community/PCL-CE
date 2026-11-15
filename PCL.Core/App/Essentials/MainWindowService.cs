using System;
using PCL.Core.App.IoC;
using PCL.Core.Platform;

namespace PCL.Core.App.Essentials;

[LifecycleService(LifecycleState.WindowCreating, Priority = int.MaxValue)]
public sealed class MainWindowService : GeneralService
{
    private static LifecycleContext? _context;

    private MainWindowService() : base("window", "主窗体", false)
    {
        _context = ServiceContext;
    }

    public static Func<IWindowHost>? Loading { private get; set; }
    private static LifecycleContext Context => _context!;

    public override void Start()
    {
        Context.Debug("正在初始化应用程序窗体");
        var window = Loading!.Invoke();
        window.Loaded += Lifecycle.OnWindowCreated;
        Lifecycle.CurrentApplication.MainWindow = window;
        Context.Trace("窗体创建完毕");
    }
}