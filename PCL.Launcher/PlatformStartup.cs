using Avalonia;

namespace PCL;

/// <summary>平台启动组合根。唯一允许选择具体平台启动实现的位置。</summary>
internal static class PlatformStartup
{
    private static IPlatformStartup Current =>
        OperatingSystem.IsWindows() ? new WindowsPlatformStartup()
        : OperatingSystem.IsMacOS() ? new MacOsPlatformStartup()
        : new LinuxPlatformStartup();

    public static IPlatformShellIntegration Shell => Current.Shell;

    public static void Initialize(string[] args)
    {
        Current.Initialize(args);
    }

    public static AppBuilder Configure()
    {
        return Current.Configure();
    }
}

internal interface IPlatformStartup
{
    IPlatformShellIntegration Shell { get; }
    void Initialize(string[] args);
    AppBuilder Configure();
}