using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace PCL.Core.Platform;

/// <summary>Minecraft 版本 JSON 中的平台规则与原生库命名。</summary>
public interface IMinecraftPlatformRules
{
    IReadOnlyList<string> NativeOsNames { get; }

    IReadOnlyList<string> GetNativeClassifierNames(Architecture architecture);

    bool MatchesOsRule(string osName, string? versionPattern);

    bool MatchesArchRule(string arch);

    /// <summary>游戏窗口尺寸参数是否按物理像素计算；为 <c>true</c> 时，按启动器窗口设置游戏窗口大小需要乘以显示缩放比。</summary>
    bool GameWindowUsesPhysicalPixels { get; }
}