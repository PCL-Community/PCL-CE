using System.Text.Json;
using Avalonia;
using Avalonia.Rendering.Composition;
using PCL.Core.App;
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
        var builder = AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

        if (!ShouldUseWayland())
            return builder;

        LinuxDesktopEntry.Ensure();
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
        return ReadProtocolFromConfig() == 2;
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