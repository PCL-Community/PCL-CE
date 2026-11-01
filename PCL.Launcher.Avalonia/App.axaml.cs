using Avalonia;
using Avalonia.Markup.Xaml;
using PCL.Core.Avalonia;

namespace PCL.Avalonia;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // 通知应用容器：Avalonia 已就绪，PCL.Core 的加载流程由此继续
        AvaloniaApplicationHost.NotifyStartup(this);
        base.OnFrameworkInitializationCompleted();
    }
}