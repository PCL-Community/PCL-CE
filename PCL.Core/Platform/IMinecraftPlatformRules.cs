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
}