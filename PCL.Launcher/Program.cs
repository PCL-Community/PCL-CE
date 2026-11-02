using Avalonia;
using PCL.Core.App.Essentials;
using PCL.Core.App.IoC;
using PCL.Core.UI;
using PCL.UI;
using PCL.UI.Seams;
using PCL.Views;

namespace PCL;

internal static class Program
{
    /// <summary>
    ///     程序启动点。
    /// </summary>
    [STAThread]
    public static void Main(string[] args)
    {
        var builder = BuildAvaloniaApp();
        // PCL.Core 通过 PlatformServices 与 UI 解耦，需在生命周期启动前注册界面实现
        PlatformRegistration.Register();
        // 预加载任务：创建界面应用容器，消息循环由该容器启动
        ApplicationService.Loading = () =>
            new ApplicationHost(() => builder.StartWithClassicDesktopLifetime(args));
        // 主窗体
        MainWindowService.Loading = () => new WindowHost(new MainWindow());
        // 注册 Core 弹窗与提示的界面呈现器
        DialogPresenter.Install();
        HintPresenter.Install();
        // 注册业务层 UI seam 的界面实现，并校验必需项
        UiSeamRegistration.Register();
        UiSeamValidation.ValidateRequiredSeams();
        // 启动 PCL.Core 生命周期
        Lifecycle.OnInitialize();
    }

    /// <summary>
    ///     界面配置入口，同时供可视化设计器使用。
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