using PCL.Core.Platform;

namespace PCL.Core.UI;

/// <summary>根据平台创建 UI 服务门面；具体服务由组合根注册到 DI。</summary>
public static class UiModuleFactory
{
    public static UiModule Create(PlatformKind kind)
    {
        return kind switch
        {
            PlatformKind.Windows or PlatformKind.Linux or PlatformKind.MacOs => new UiModule(),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "不支持的平台")
        };
    }
}