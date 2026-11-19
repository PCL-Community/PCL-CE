using Microsoft.Extensions.DependencyInjection;
using PCL.Core.Platform;

namespace PCL.Platform;

/// <summary>按平台注册 OS 原生服务。</summary>
public static class PlatformServiceRegistration
{
    public static IServiceCollection AddPlatformServices(
        this IServiceCollection services,
        PlatformKind kind)
    {
        services.AddSingleton<IExternalCommandRunner, ExternalCommandRunner>();
        services.AddSingleton<IElevationService, PlatformElevationService>();
        services.AddSingleton<IProcessLauncher, PlatformProcessLauncher>();

        services.AddSingleton<ISystemProxyProvider>(_ => kind switch
        {
            PlatformKind.Windows => new WindowsSystemProxyProvider(),
            PlatformKind.Linux => new LinuxSystemProxyProvider(),
            PlatformKind.MacOs => new MacOsSystemProxyProvider(),
            _ => throw _Throw(kind)
        });
        services.AddSingleton<IWindowInterop>(_ => kind switch
        {
            PlatformKind.Windows => new WindowsWindowInterop(),
            PlatformKind.Linux => new LinuxWindowInterop(),
            PlatformKind.MacOs => new MacOsWindowInterop(),
            _ => throw _Throw(kind)
        });
        services.AddSingleton<IShortcutService>(_ => kind switch
        {
            PlatformKind.Windows => new WindowsShortcutService(),
            PlatformKind.Linux => new LinuxShortcutService(),
            PlatformKind.MacOs => new MacOsShortcutService(),
            _ => throw _Throw(kind)
        });
        services.AddSingleton<IJavaPlatformInfo>(_ => kind switch
        {
            PlatformKind.Windows => new WindowsJavaPlatformInfo(),
            PlatformKind.Linux => new LinuxJavaPlatformInfo(),
            PlatformKind.MacOs => new MacOsJavaPlatformInfo(),
            _ => throw _Throw(kind)
        });
        services.AddSingleton<IProcessCleanupService>(_ => kind switch
        {
            PlatformKind.Windows => new WindowsProcessCleanupService(),
            PlatformKind.Linux or PlatformKind.MacOs => new PosixProcessCleanupService(),
            _ => throw _Throw(kind)
        });
        services.AddSingleton<IGpuPreferenceService>(_ => kind switch
        {
            PlatformKind.Windows => new WindowsGpuPreferenceService(),
            PlatformKind.Linux or PlatformKind.MacOs => new UnsupportedGpuPreferenceService(),
            _ => throw _Throw(kind)
        });
        services.AddSingleton<ILaunchEnvironmentService>(_ => kind switch
        {
            PlatformKind.Linux => new LinuxLaunchEnvironmentService(),
            PlatformKind.Windows or PlatformKind.MacOs => new UnsupportedLaunchEnvironmentService(),
            _ => throw _Throw(kind)
        });
        services.AddSingleton<ISystemFacts>(_ => kind switch
        {
            PlatformKind.Windows => new WindowsSystemFacts(),
            PlatformKind.Linux => new LinuxSystemFacts(),
            PlatformKind.MacOs => new MacOsSystemFacts(),
            _ => throw _Throw(kind)
        });
        services.AddSingleton<IRuntimeEnvironmentService>(_ => kind switch
        {
            PlatformKind.Windows => new WindowsRuntimeEnvironmentService(),
            PlatformKind.Linux => new LinuxRuntimeEnvironmentService(),
            PlatformKind.MacOs => new MacOsRuntimeEnvironmentService(),
            _ => throw _Throw(kind)
        });
        services.AddSingleton<IHardwareInfo>(_ => kind switch
        {
            PlatformKind.Windows => new WindowsHardwareInfo(),
            PlatformKind.Linux => new LinuxHardwareInfo(),
            PlatformKind.MacOs => new MacOsHardwareInfo(),
            _ => throw _Throw(kind)
        });
        services.AddSingleton<IShellLauncher>(_ => kind switch
        {
            PlatformKind.Windows => new WindowsShellLauncher(),
            PlatformKind.Linux => new PosixShellLauncher("xdg-open"),
            PlatformKind.MacOs => new PosixShellLauncher("open"),
            _ => throw _Throw(kind)
        });
        services.AddSingleton<IRegionSignalProvider>(_ => kind switch
        {
            PlatformKind.Windows => new WindowsRegionSignalProvider(),
            PlatformKind.Linux or PlatformKind.MacOs => new PosixRegionSignalProvider(),
            _ => throw _Throw(kind)
        });
        services.AddSingleton<IRecycleBinService>(_ => kind switch
        {
            PlatformKind.Windows => new WindowsRecycleBinService(),
            PlatformKind.Linux => new LinuxRecycleBinService(),
            PlatformKind.MacOs => new MacOsRecycleBinService(),
            _ => throw _Throw(kind)
        });
        services.AddSingleton<IFatalDialogService>(_ => kind switch
        {
            PlatformKind.Windows => new WindowsFatalDialogService(),
            PlatformKind.Linux => new LinuxFatalDialogService(),
            PlatformKind.MacOs => new MacOsFatalDialogService(),
            _ => throw _Throw(kind)
        });
        services.AddSingleton<IKeyProtectorFactory>(_ => kind switch
        {
            PlatformKind.Windows => new WindowsKeyProtectorFactory(),
            PlatformKind.Linux or PlatformKind.MacOs => new UnixKeyProtectorFactory(),
            _ => throw _Throw(kind)
        });
        services.AddSingleton<IPlatformPathService>(_ => kind switch
        {
            PlatformKind.Windows => new WindowsPlatformPathService(),
            PlatformKind.Linux => new LinuxPlatformPathService(),
            PlatformKind.MacOs => new MacOsPlatformPathService(),
            _ => throw _Throw(kind)
        });
        services.AddSingleton<ILaunchScriptService>(_ => kind switch
        {
            PlatformKind.Windows => new WindowsLaunchScriptService(),
            PlatformKind.Linux or PlatformKind.MacOs => new PosixLaunchScriptService(),
            _ => throw _Throw(kind)
        });
        services.AddSingleton<IServerLaunchScriptService>(_ => kind switch
        {
            PlatformKind.Windows => new WindowsServerLaunchScriptService(),
            PlatformKind.Linux => new PosixServerLaunchScriptService(".sh"),
            PlatformKind.MacOs => new PosixServerLaunchScriptService(".command"),
            _ => throw _Throw(kind)
        });
        services.AddSingleton<IShellCommandService>(_ => kind switch
        {
            PlatformKind.Windows => new WindowsShellCommandService(),
            PlatformKind.Linux or PlatformKind.MacOs => new PosixShellCommandService(),
            _ => throw _Throw(kind)
        });
        services.AddSingleton<IProcessEnvironmentService>(_ => kind switch
        {
            PlatformKind.Windows => new WindowsProcessEnvironmentService(),
            PlatformKind.Linux or PlatformKind.MacOs => new PosixProcessEnvironmentService(),
            _ => throw _Throw(kind)
        });
        services.AddSingleton<IMinecraftPlatformRules>(_ => kind switch
        {
            PlatformKind.Windows => new WindowsMinecraftPlatformRules(),
            PlatformKind.Linux => new LinuxMinecraftPlatformRules(),
            PlatformKind.MacOs => new MacOsMinecraftPlatformRules(),
            _ => throw _Throw(kind)
        });
        return services;
    }

    private static PlatformNotSupportedException _Throw(PlatformKind kind)
    {
        return new PlatformNotSupportedException($"Platform {kind.ToString()} is not supported.");
    }
}