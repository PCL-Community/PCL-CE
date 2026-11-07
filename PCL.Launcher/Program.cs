using Avalonia;
using PCL.Core.App.Essentials;
using PCL.Core.App.IoC;
using PCL.Core.UI;
using PCL.Core.UI.Animation;
using PCL.UI;
using PCL.UI.Bridges;
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
        PlatformStartup.Initialize(args);
        var builder = PlatformStartup.Configure();

        PlatformRegistration.Register();

        AnimationRegistration.Register();

        ApplicationService.Loading = () =>
            new ApplicationHost(() => builder.StartWithClassicDesktopLifetime(args));

        MainWindowService.Loading = () => new WindowHost(new MainWindow());

        DialogPresenter.Install();

        HintPresenter.Install();

        UiBridgeRegistration.Register();

        Lifecycle.OnInitialize();
    }

    /// <summary>
    ///     界面配置入口，同时供可视化设计器使用。
    /// </summary>
    public static AppBuilder BuildAvaloniaApp()
    {
        return PlatformStartup.Configure();
    }
}