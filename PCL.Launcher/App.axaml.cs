using Avalonia;
using Avalonia.Markup.Xaml;
using PCL.Core.UI;

namespace PCL;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // 通知应用容器：界面框架已就绪，PCL.Core 的加载流程由此继续
        ApplicationHost.NotifyStartup(this);
        base.OnFrameworkInitializationCompleted();
    }
}