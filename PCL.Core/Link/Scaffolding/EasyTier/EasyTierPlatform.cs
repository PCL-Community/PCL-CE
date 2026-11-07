using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using PCL.Core.Platform;

namespace PCL.Core.Link.Scaffolding.EasyTier;

/// <summary>
///     某个平台/架构对应的 EasyTier 依赖包描述。
/// </summary>
/// <param name="Os">宿主平台。</param>
/// <param name="Architecture">宿主架构。</param>
/// <param name="FolderName">压缩包内顶层目录，同时也是安装目录名。</param>
/// <param name="CoreFileName">EasyTier 核心可执行文件名。</param>
/// <param name="CliFileName">EasyTier CLI 可执行文件名。</param>
/// <param name="RequiredFiles">安装完成后必须存在的文件。</param>
public sealed record EasyTierPackage(
    PlatformKind Os,
    Architecture Architecture,
    string FolderName,
    string CoreFileName,
    string CliFileName,
    IReadOnlyList<string> RequiredFiles)
{
    /// <summary>
    ///     生成与官方发布命名一致的压缩包文件名。
    /// </summary>
    public string GetArchiveName(string version)
    {
        return $"{FolderName}-v{version}.zip";
    }
}

/// <summary>
///     EasyTier 平台/架构解析。功能代码只依赖本类的结果，不应直接判断操作系统。
/// </summary>
public static class EasyTierPlatform
{
    /// <summary>
    ///     当前宿主平台。仅本类允许根据操作系统选择平台。
    /// </summary>
    public static PlatformKind CurrentOs => PlatformRuntime.Current.Kind;

    /// <summary>
    ///     当前宿主使用的 EasyTier 依赖包。
    ///     使用系统架构以保持与 WPF 旧下载逻辑一致：Windows ARM64 上即使启动器以
    ///     x64 兼容模式运行，也优先使用原生 arm64 依赖包。
    /// </summary>
    public static EasyTierPackage Current { get; } =
        Resolve(CurrentOs, RuntimeInformation.OSArchitecture);

    /// <summary>
    ///     将平台与架构解析为官方依赖包描述。纯函数，便于单元测试。
    /// </summary>
    /// <exception cref="PlatformNotSupportedException">官方没有对应架构的依赖包。</exception>
    public static EasyTierPackage Resolve(PlatformKind os, Architecture architecture)
    {
        var osToken = os switch
        {
            PlatformKind.Windows => "windows",
            PlatformKind.Linux => "linux",
            PlatformKind.MacOs => "macos",
            _ => throw new ArgumentOutOfRangeException(nameof(os), os, "未知的 EasyTier 宿主平台")
        };

        var archToken = (os, architecture) switch
        {
            (PlatformKind.Windows, Architecture.X64) => "x86_64",
            (PlatformKind.Windows, Architecture.Arm64) => "arm64",
            (PlatformKind.Windows, Architecture.X86) => "i686",
            (PlatformKind.Linux, Architecture.X64) => "x86_64",
            (PlatformKind.Linux, Architecture.Arm64) => "aarch64",
            (PlatformKind.MacOs, Architecture.X64) => "x86_64",
            (PlatformKind.MacOs, Architecture.Arm64) => "aarch64",
            _ => throw new PlatformNotSupportedException(
                $"EasyTier 没有适用于 {os}/{architecture} 的官方依赖包")
        };

        var folderName = $"easytier-{osToken}-{archToken}";
        var isWindows = os == PlatformKind.Windows;
        var coreFileName = isWindows ? "easytier-core.exe" : "easytier-core";
        var cliFileName = isWindows ? "easytier-cli.exe" : "easytier-cli";

        IReadOnlyList<string> requiredFiles = isWindows
            ? [coreFileName, cliFileName, "Packet.dll"]
            : [coreFileName, cliFileName];

        return new EasyTierPackage(
            os,
            architecture,
            folderName,
            coreFileName,
            cliFileName,
            requiredFiles);
    }
}