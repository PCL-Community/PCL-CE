using PCL.Core.Platform;

namespace PCL.Platform;

/// <summary>根据已检测到的平台组装平台模块。只负责组装，不承载业务逻辑。</summary>
public static class PlatformModuleFactory
{
    public static PlatformModule Create(PlatformKind kind)
    {
        return kind switch
        {
            PlatformKind.Windows => new PlatformModule
            {
                Kind = PlatformKind.Windows,
                Capabilities = PlatformCapabilities.Windows
            },
            PlatformKind.Linux => new PlatformModule
            {
                Kind = PlatformKind.Linux,
                Capabilities = PlatformCapabilities.Linux
            },
            PlatformKind.MacOs => new PlatformModule
            {
                Kind = PlatformKind.MacOs,
                Capabilities = PlatformCapabilities.MacOs
            },
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "不支持的平台")
        };
    }
}