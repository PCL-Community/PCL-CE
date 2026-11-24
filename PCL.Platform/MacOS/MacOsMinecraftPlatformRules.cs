using System.Runtime.InteropServices;
using PCL.Core.Platform;

namespace PCL.Platform;

/// <summary>macOS 的 Minecraft 原生库规则。</summary>
public sealed class MacOsMinecraftPlatformRules : IMinecraftPlatformRules
{
    public IReadOnlyList<string> NativeOsNames => ["osx", "macos"];

    public bool GameWindowUsesPhysicalPixels => false;

    public IReadOnlyList<string> GetNativeClassifierNames(Architecture architecture)
    {
        return architecture is Architecture.Arm64
            ? ["natives-macos-arm64", "natives-macos", "natives-osx", "natives-macos-patch"]
            : ["natives-macos", "natives-osx", "natives-macos-patch"];
    }

    public bool MatchesOsRule(string osName, string? versionPattern)
    {
        return osName is "osx" or "macos";
    }

    public bool MatchesArchRule(string arch)
    {
        return PosixArchitectureRule.Matches(arch);
    }
}