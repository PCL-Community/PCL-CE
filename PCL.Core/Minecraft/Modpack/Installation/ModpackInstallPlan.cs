using System.Collections.Generic;
using PCL.Core.Minecraft.Modpack.Model;

namespace PCL.Core.Minecraft.Modpack.Installation;

/// <summary>
/// 一份可直接执行的整合包安装方案。
/// <para>
/// 由 <see cref="ModpackInstallPlanner"/> 从 <see cref="ModpackDescriptor"/> 推导而来：
/// 相对路径已解析为绝对路径并完成安全校验，CurseForge 文件已解析出下载地址。
/// 宿主只需按字段派发任务，不必再做任何格式相关的判断。
/// </para>
/// </summary>
public sealed class ModpackInstallPlan
{
    /// <summary>整合包格式。</summary>
    public required ModpackFormat Format { get; init; }

    /// <summary>展示信息。</summary>
    public required ModpackMetadata Metadata { get; init; }

    /// <summary>游戏版本与加载器。</summary>
    public required ModpackComponents Components { get; init; }

    /// <summary>实例目录的绝对路径。</summary>
    public required string InstanceDirectory { get; init; }

    /// <summary>需要从压缩包释放的覆写目录。</summary>
    public IReadOnlyList<ModpackOverride> Overrides { get; init; } = [];

    /// <summary>需要下载的文件，路径已解析为绝对路径。</summary>
    public IReadOnlyList<ModpackPlannedDownload> Downloads { get; init; } = [];

    /// <summary>实例启动设置。</summary>
    public ModpackLaunchOptions LaunchOptions { get; init; } = ModpackLaunchOptions.None;

    /// <summary>需要叠加到实例 JSON 的版本补丁。</summary>
    public ModpackVersionPatch? VersionPatch { get; init; }

    /// <summary>需要特殊处理的内嵌目录。</summary>
    public IReadOnlyList<ModpackEmbeddedPayload> EmbeddedPayloads { get; init; } = [];

    /// <summary>
    /// 声明了但未能解析出下载地址的文件 —— 通常是作者已删除的 CurseForge 文件。
    /// 调用方应把这些名称提示给用户。
    /// </summary>
    public IReadOnlyList<string> UnresolvedFiles { get; init; } = [];

    /// <summary>规划与解析过程中产生的非致命问题。</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

/// <summary>
/// 一个已完成规划的下载任务。
/// </summary>
public sealed record ModpackPlannedDownload
{
    /// <summary>目标文件的绝对路径。</summary>
    public required string TargetPath { get; init; }

    /// <summary>候选下载地址，按优先级排列。</summary>
    public required IReadOnlyList<string> Urls { get; init; }

    /// <summary>用于向用户展示的名称。</summary>
    public required string DisplayName { get; init; }

    /// <summary>SHA-1 校验值。</summary>
    public string? Sha1 { get; init; }

    /// <summary>文件大小（字节）。</summary>
    public long? FileSize { get; init; }

    /// <summary>需求级别。<see cref="ModpackFileRequirement.Optional"/> 的条目应先询问用户。</summary>
    public ModpackFileRequirement Requirement { get; init; } = ModpackFileRequirement.Required;

    /// <summary>资源种类。</summary>
    public ModpackResourceKind Kind { get; init; } = ModpackResourceKind.Unknown;
}
