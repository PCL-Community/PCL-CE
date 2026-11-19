using System.Runtime.InteropServices;
using PCL.Core.Platform;

namespace PCL.Platform;

/// <summary>Linux 的 Minecraft 原生库规则。</summary>
public sealed class LinuxMinecraftPlatformRules : IMinecraftPlatformRules
{
    public IReadOnlyList<string> NativeOsNames => ["linux"];

    public bool GameWindowUsesPhysicalPixels => false;

    public IReadOnlyList<string> GetNativeClassifierNames(Architecture architecture)
    {
        return architecture switch
        {
            Architecture.Arm64 => ["natives-linux-arm64", "linux-aarch_64", "natives-linux"],
            Architecture.X64 => ["natives-linux", "linux-x86_64"],
            _ => ["natives-linux"]
        };
    }

    public bool MatchesOsRule(string osName, string? versionPattern)
    {
        return osName == "linux";
    }

    public bool MatchesArchRule(string arch)
    {
        return PosixArchitectureRule.Matches(arch);
    }
}