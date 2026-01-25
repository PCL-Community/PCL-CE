using System;
using System.Windows;

namespace PCL.Core.App;

[LifecycleService(LifecycleState.WindowCreating, Priority = int.MaxValue)]
public sealed class MainWindowService : GeneralService
{
    public static Func<Window>? Loading { private get; set; }

    private static LifecycleContext? _context;
    private static LifecycleContext Context => _context!;
    private MainWindowService() : base("window", "主窗体", false) { _context = ServiceContext; }
    
    public override void Start()
    {
        Context.Debug("正在初始化 WPF 窗体");
        var window = Loading!.Invoke();
        window.Loaded += (_, _) => Lifecycle.OnWindowCreated();
        Lifecycle.CurrentApplication.MainWindow = window;
        Context.Trace("窗体创建完毕");
    }
}
