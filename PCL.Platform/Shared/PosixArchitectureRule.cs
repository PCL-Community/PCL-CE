using System.Runtime.InteropServices;

namespace PCL.Platform;

/// <summary>Unix 上 Minecraft 版本 JSON 的架构匹配。</summary>
internal static class PosixArchitectureRule
{
    public static bool Matches(string arch)
    {
        var osArch = RuntimeInformation.OSArchitecture;
        return arch switch
        {
            "x86" => osArch is Architecture.X86,
            "x86_64" or "amd64" or "x64" => osArch is Architecture.X64,
            "arm64" or "aarch64" => osArch is Architecture.Arm64,
            "arm" => osArch is Architecture.Arm or Architecture.Armv6,
            _ => false
        };
    }
}