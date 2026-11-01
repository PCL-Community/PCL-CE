using Avalonia;
using PCL.Avalonia.Views;
using PCL.Core.App.Essentials;
using PCL.Core.App.IoC;
using PCL.Core.Avalonia;

namespace PCL.Avalonia;

internal static class Program
{
    /// <summary>
    ///     程序启动点。
    /// </summary>
    [STAThread]
    public static void Main(string[] args)
    {
        var builder = BuildAvaloniaApp();
        // PCL.Core 通过 PlatformServices 与 UI 解耦，需在生命周期启动前注册 Avalonia 实现
        PlatformRegistration.Register();
        // 预加载任务：创建 Avalonia 应用容器，消息循环由该容器启动
        ApplicationService.Loading = () =>
            new AvaloniaApplicationHost(() => builder.StartWithClassicDesktopLifetime(args));
        // 主窗体
        MainWindowService.Loading = () => new AvaloniaWindowHost(new MainWindow());
        // 启动 PCL.Core 生命周期
        Lifecycle.OnInitialize();
    }

    /// <summary>
    ///     Avalonia 配置入口，同时供可视化设计器使用。
    /// </summary>
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }
}