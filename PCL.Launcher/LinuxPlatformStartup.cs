using System.Text.Json;
using Avalonia;
using Avalonia.Rendering.Composition;
using PCL.Core.App;
using PCL.Core.Platform;
using PCL.UI.SystemIntegration;

namespace PCL;

internal sealed class LinuxPlatformStartup : IPlatformStartup
{
    public IPlatformShellIntegration Shell { get; } = new LinuxShellIntegration();

    public void Initialize(string[] args)
    {
    }

    public AppBuilder Configure()
    {
        // 让 X11 的 WM_CLASS 与 desktop entry 的 StartupWMClass 使用同一个 app id，
        // 这样任务栏可以正确归组，而不是退回到进程名。
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RESOURCE_NAME")))
            Environment.SetEnvironmentVariable("RESOURCE_NAME", LinuxDesktopEntry.AppId);

        var builder = AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

        if (!ShouldUseWayland())
            return builder;

        LinuxDesktopEntry.Ensure();
        // 原生 Wayland 的合成器会让持续动画期间的 RAF 回调远高于刷新率，
        // 直接使用固定 60Hz 定时器，避免 UI 线程在启动动画阶段自旋。
        AnimationService.PreferTimerClock = true;
        builder = builder
            .With(
                new WaylandPlatformOptions
                {
                    AppId = LinuxDesktopEntry.AppId
                })
            .UseWaylandWithFallback()
            .With(
                new CompositionOptions
                {
                    UseRegionDirtyRectClipping = false
                });

        return builder;
    }

    private static bool ShouldUseWayland()
    {
        if (Environment.GetEnvironmentVariable("PCL_FORCE_X11") is not null) return false;
        if (Environment.GetEnvironmentVariable("PCL_USE_WAYLAND") == "1") return true;

        return LinuxDisplayProtocolResolver.ResolveEffective(ReadProtocolFromConfig())
            == LinuxDisplayProtocol.Wayland;
    }

    private static int ReadProtocolFromConfig()
    {
        try
        {
            var path = Path.Combine(Paths.SharedData, "config.v1.json");
            if (!File.Exists(path)) return 0;
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.TryGetProperty("SystemDebugLinuxProtocol", out var value)
                && value.TryGetInt32(out var protocol)
                    ? Math.Clamp(protocol, 0, 2)
                    : 0;
        }
        catch
        {
            return 0;
        }
    }
}