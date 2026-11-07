using System.Text.Json;
using Avalonia;
using Avalonia.Rendering.Composition;
using PCL.Core.App;
using PCL.Core.App.Essentials;
using PCL.Core.App.IoC;
using PCL.Core.UI;
using PCL.Core.UI.Animation;
using PCL.Core.Utils.OS;
using PCL.UI;
using PCL.UI.Bridges;
using PCL.UI.SystemIntegration;
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
        if (Basics.CommandLineArguments.Contains("--console") && OperatingSystem.IsWindows())
            KernelInterop.AllocateConsole();

        if (ShouldUseWayland())
            LinuxDesktopEntry.Ensure();

        var builder = BuildAvaloniaApp();

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
        var builder = AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .With(
                new MacOSPlatformOptions
                {
                    ShowInDock = true,
                    DisableDefaultApplicationMenuItems = true,
                    DisableNativeMenus = false
                })
            .WithInterFont()
            .LogToTrace();

        // Linux 默认走 X11/XWayland，避免原生 Wayland 帧调度造成动画卡顿。
        // 原生 Wayland 作为显式实验入口：PCL_USE_WAYLAND=1 或调试选项。
        // PCL_FORCE_X11=1 仍可用于强制 X11，不参与任何功能判断。
        if (ShouldUseWayland())
        {
            builder = builder
                .With(
                    new WaylandPlatformOptions
                    {
                        AppId = LinuxDesktopEntry.AppId
                    })
                .UseWaylandWithFallback();
            // > Avalonia 12 的 region dirty-rect clipping 在部分 Linux/Skia
            // > 渲染路径上会造成明显掉帧；仅对显式 Wayland 路径关闭。
            // ^存疑
            builder = builder.With(
                new CompositionOptions
                {
                    UseRegionDirtyRectClipping = false
                });
        }

        return builder;
    }

    private static bool ShouldUseWayland()
    {
        if (!OperatingSystem.IsLinux())
            return false;
        if (Environment.GetEnvironmentVariable("PCL_FORCE_X11") is not null)
            return false;
        if (Environment.GetEnvironmentVariable("PCL_USE_WAYLAND") == "1")
            return true;

        return ReadLinuxProtocolFromConfig() == 2;
    }

    private static int ReadLinuxProtocolFromConfig()
    {
        try
        {
            var path = Path.Combine(Paths.SharedData, "config.v1.json");
            if (!File.Exists(path))
                return 0;

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.TryGetProperty(
                    "SystemDebugLinuxProtocol",
                    out var value)
                && value.TryGetInt32(out var protocol)
                    ? Math.Clamp(protocol, 0, 2)
                    : 0;
        }
        catch
        {
            // 配置尚未生成或格式异常时回退到默认 X11。
            return 0;
        }
    }
}