using System;
using System.Windows;

namespace PCL.Core.App;

[LifecycleService(LifecycleState.WindowCreating, Priority = int.MaxValue)]
[LifecycleScope("window", "主窗体", false)]
public sealed partial class MainWindowService
{
    public static Func<Window>? Loading { private get; set; }
    
    [LifecycleStart]
    private static void _Start()
    {
        Context.Debug("正在初始化 WPF 窗体");
        var window = Loading!.Invoke();
        window.Loaded += (_, _) => Lifecycle.OnWindowCreated();
        Lifecycle.CurrentApplication.MainWindow = window;
        Context.Trace("窗体创建完毕");
    }
}
