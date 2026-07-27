using System.Collections.Generic;

namespace PCL.Core.Minecraft.Modpack.Model;

/// <summary>
/// 文件在客户端环境下的需求级别。
/// </summary>
public enum ModpackFileRequirement
{
    /// <summary>必需，必须下载。</summary>
    Required,

    /// <summary>可选，应询问用户是否下载。</summary>
    Optional,

    /// <summary>客户端不支持，应跳过。</summary>
    Unsupported
}

/// <summary>
/// 资源种类，决定文件的默认落地目录。
/// </summary>
public enum ModpackResourceKind
{
    Mod,
    ResourcePack,
    DataPack,
    ShaderPack,
    World,

    /// <summary>种类未知 —— 清单已给出明确路径，无需按种类推断目录。</summary>
    Unknown
}

/// <summary>
/// 整合包声明的一个待安装文件。
/// <para>
/// 分为两种来源：<see cref="ModpackDirectFile"/> 已给出下载地址，可离线安装；
/// <see cref="ModpackCurseForgeFile"/> 只给出项目与文件 ID，需先经 CurseForge API 解析。
/// </para>
/// </summary>
public abstract record ModpackFile
{
    /// <summary>需求级别。</summary>
    public ModpackFileRequirement Requirement { get; init; } = ModpackFileRequirement.Required;

    /// <summary>资源种类。</summary>
    public ModpackResourceKind Kind { get; init; } = ModpackResourceKind.Unknown;

    /// <summary>用于向用户展示的名称。</summary>
    public abstract string DisplayName { get; }
}

/// <summary>
/// 已知下载地址的文件。Modrinth、MCBBS 的 <c>addon</c> 条目与 Server 格式均属此类。
/// </summary>
public sealed record ModpackDirectFile : ModpackFile
{
    /// <summary>相对于实例目录的目标路径，已通过 <see cref="ModpackPathPolicy"/> 校验。</summary>
    public required string TargetPath { get; init; }

    /// <summary>候选下载地址，按优先级排列。</summary>
    public required IReadOnlyList<string> Urls { get; init; }

    /// <summary>SHA-1 校验值（十六进制小写）。</summary>
    public string? Sha1 { get; init; }

    /// <summary>SHA-512 校验值（十六进制小写）。</summary>
    public string? Sha512 { get; init; }

    /// <summary>文件大小（字节），未知时为 <c>null</c>。</summary>
    public long? FileSize { get; init; }

    public override string DisplayName => TargetPath;
}

/// <summary>
/// 需要经 CurseForge API 解析后才能下载的文件。
/// </summary>
public sealed record ModpackCurseForgeFile : ModpackFile
{
    /// <summary>CurseForge 项目 ID。</summary>
    public required int ProjectId { get; init; }

    /// <summary>CurseForge 文件 ID。</summary>
    public required int FileId { get; init; }

    /// <summary>清单中给出的文件名，导出时常缺失。</summary>
    public string? FileName { get; init; }

    /// <summary>清单中给出的下载地址，导出时常缺失。</summary>
    public string? Url { get; init; }

    /// <summary>
    /// 清单已指定的目标路径。MCBBS 的 <c>curse</c> 条目可能给出，
    /// 为 <c>null</c> 时按解析结果的资源种类推断目录。
    /// </summary>
    public string? TargetPath { get; init; }

    public override string DisplayName => FileName ?? $"CurseForge {ProjectId}/{FileId}";
}
