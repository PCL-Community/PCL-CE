using Avalonia;
using PCL.Core.Platform;

namespace PCL;

/// <summary>平台启动组合根。唯一允许选择具体平台启动实现的位置。</summary>
internal static class PlatformStartup
{
    private static IPlatformStartup? _current;
    private static PlatformKind? _kind;
    private static bool _initialized;

    private static IPlatformStartup Current =>
        _current ?? throw new InvalidOperationException("平台启动尚未初始化");

    public static IPlatformShellIntegration Shell => Current.Shell;

    public static void Initialize(PlatformKind kind, string[] args)
    {
        if (_current is null)
        {
            _kind = kind;
            _current = Create(kind);
        }
        else if (_kind != kind)
        {
            throw new InvalidOperationException("平台启动已使用其他平台初始化");
        }

        if (_initialized) return;
        _initialized = true;
        _current.Initialize(args);
    }

    public static AppBuilder Configure(PlatformKind kind)
    {
        if (_current is null)
        {
            _kind = kind;
            _current = Create(kind);
        }
        else if (_kind != kind)
        {
            throw new InvalidOperationException("平台启动已使用其他平台初始化");
        }

        return _current.Configure();
    }

    private static IPlatformStartup Create(PlatformKind kind)
    {
        return kind switch
        {
            PlatformKind.Windows => new WindowsPlatformStartup(),
            PlatformKind.Linux => new LinuxPlatformStartup(),
            PlatformKind.MacOs => new MacOsPlatformStartup(),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "不支持的平台")
        };
    }
}

internal interface IPlatformStartup
{
    IPlatformShellIntegration Shell { get; }
    void Initialize(string[] args);
    AppBuilder Configure();
}