using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using PCL.Core.Platform;
using PCL.Core.Utils.OS;

namespace PCL.Platform;

/// <summary>Windows 的 Minecraft 原生库规则。</summary>
public sealed class WindowsMinecraftPlatformRules : IMinecraftPlatformRules
{
    private static readonly string OsVersion = Environment.OSVersion.Version.ToString();

    public IReadOnlyList<string> NativeOsNames => ["windows"];

    public bool GameWindowUsesPhysicalPixels => true;

    public IReadOnlyList<string> GetNativeClassifierNames(Architecture architecture)
    {
        return architecture switch
        {
            Architecture.Arm64 => ["natives-windows-arm64", "natives-windows"],
            Architecture.X64 => ["natives-windows", "natives-windows-64", "natives-windows-x86"],
            _ => ["natives-windows", "natives-windows-x86", "natives-windows-32"]
        };
    }

    public bool MatchesOsRule(string osName, string? versionPattern)
    {
        if (osName != "windows") return false;
        return versionPattern is null || Regex.IsMatch(OsVersion, versionPattern);
    }

    public bool MatchesArchRule(string arch)
    {
        return arch == "x86" == SystemInfo.Is32BitSystem;
    }
}