using System;
using PCL.Core.Utils.Secret;

namespace PCL.Core.Platform;

/// <summary>平台服务门面。服务实例由组合根注册到 <see cref="ApplicationRuntime" />， 本类型不再保存服务袋数据，只保留平台元数据。</summary>
public sealed class PlatformModule : IDisposable
{
    public required PlatformKind Kind { get; init; }
    public required IPlatformCapabilities Capabilities { get; init; }

    public ISystemProxyProvider SystemProxy => _Get<ISystemProxyProvider>();
    public IWindowInterop WindowInterop => _Get<IWindowInterop>();
    public IShortcutService Shortcuts => _Get<IShortcutService>();
    public IJavaPlatformInfo Java => _Get<IJavaPlatformInfo>();
    public IProcessCleanupService ProcessCleanup => _Get<IProcessCleanupService>();
    public IGpuPreferenceService GpuPreference => _Get<IGpuPreferenceService>();
    public ILaunchEnvironmentService LaunchEnvironment => _Get<ILaunchEnvironmentService>();
    public ISystemFacts SystemFacts => _Get<ISystemFacts>();
    public IHardwareInfo Hardware => _Get<IHardwareInfo>();
    public IShellLauncher Shell => _Get<IShellLauncher>();
    public IRegionSignalProvider Region => _Get<IRegionSignalProvider>();
    public IRecycleBinService RecycleBin => _Get<IRecycleBinService>();
    public IFatalDialogService FatalDialog => _Get<IFatalDialogService>();

    public void Dispose()
    {
        // 服务的释放由 DI 容器负责。
    }

    public IKeyProtector CreateKeyProtector(byte[] key)
    {
        return _Get<IKeyProtectorFactory>().Create(key);
    }

    private static T _Get<T>() where T : class
    {
        return ApplicationRuntime.GetRequiredService<T>();
    }
}