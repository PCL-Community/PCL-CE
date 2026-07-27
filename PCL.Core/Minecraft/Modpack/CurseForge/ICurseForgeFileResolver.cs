using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Minecraft.Modpack.CurseForge;

/// <summary>
/// CurseForge 文件信息的解析入口。
/// <para>
/// CurseForge 整合包的清单只给出 <c>projectID</c> 与 <c>fileID</c>，
/// 必须调用官方 API 才能得到文件名与下载地址。API 需要密钥且通常经镜像源访问，
/// 这些属于宿主的网络策略，因此以接口形式外置，由宿主实现具体传输。
/// </para>
/// </summary>
public interface ICurseForgeFileResolver
{
    /// <summary>
    /// 批量解析文件信息。
    /// </summary>
    /// <param name="keys">待解析的项目与文件 ID。</param>
    /// <returns>
    /// 解析结果。允许少于请求数量 —— 文件被作者删除时 API 不会返回对应条目，
    /// 调用方据此向用户报告缺失的文件。
    /// </returns>
    Task<IReadOnlyList<CurseForgeFileDescriptor>> ResolveAsync(
        IReadOnlyList<CurseForgeFileKey> keys, CancellationToken cancellationToken = default);
}

/// <summary>
/// CurseForge 文件的唯一标识。
/// </summary>
/// <param name="ProjectId">项目 ID。</param>
/// <param name="FileId">文件 ID。</param>
public readonly record struct CurseForgeFileKey(int ProjectId, int FileId);

/// <summary>
/// CurseForge API 返回的文件信息。
/// <para>
/// 只承载 API 的原始事实，不含「该文件应放到哪个目录」这类判断 ——
/// 后者由 <see cref="CurseForgeResourceClassifier"/> 统一处理，
/// 以免每个宿主实现各自推断出不一致的结果。
/// </para>
/// </summary>
public sealed record CurseForgeFileDescriptor
{
    /// <summary>项目 ID。</summary>
    public required int ProjectId { get; init; }

    /// <summary>文件 ID。</summary>
    public required int FileId { get; init; }

    /// <summary>文件名。</summary>
    public required string FileName { get; init; }

    /// <summary>官方下载地址。为 <c>null</c> 时可用 <see cref="CurseForgeResourceClassifier.BuildCdnUrl"/> 兜底。</summary>
    public string? DownloadUrl { get; init; }

    /// <summary>展示名称。</summary>
    public string? DisplayName { get; init; }

    /// <summary>SHA-1 校验值。</summary>
    public string? Sha1 { get; init; }

    /// <summary>文件大小（字节）。</summary>
    public long? FileSize { get; init; }

    /// <summary>所属项目的分类 ID，未知时为 <c>null</c>。</summary>
    public int? ClassId { get; init; }

    /// <summary>压缩包内的顶层条目名，用于在缺少分类 ID 时推断资源种类。</summary>
    public IReadOnlyList<string> ModuleNames { get; init; } = [];

    /// <summary>本条目的标识。</summary>
    public CurseForgeFileKey Key => new(ProjectId, FileId);
}
