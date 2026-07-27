using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Logging;
using PCL.Core.Minecraft.Modpack.CurseForge;
using PCL.Core.Minecraft.Modpack.Model;

namespace PCL.Core.Minecraft.Modpack.Installation;

/// <summary>
/// 规划整合包安装 —— 把归一化描述转换为可直接执行的方案。
/// </summary>
public sealed class ModpackInstallPlanner
{
    /// <summary>共享实例。</summary>
    public static ModpackInstallPlanner Shared { get; } = new();

    /// <summary>
    /// 生成安装方案。
    /// </summary>
    /// <param name="descriptor">已解析的整合包描述。</param>
    /// <param name="options">安装选项。</param>
    /// <exception cref="ModpackUnsafePathException">整合包中存在越出实例目录的路径。</exception>
    public async Task<ModpackInstallPlan> CreateAsync(
        ModpackDescriptor descriptor,
        ModpackInstallOptions options,
        CancellationToken cancellationToken = default)
    {
        var instanceDirectory = Path.GetFullPath(options.InstanceDirectory);
        var warnings = new List<string>(descriptor.Warnings);
        var unresolved = new List<string>();

        var downloads = new List<ModpackPlannedDownload>(descriptor.Files.Count);
        var pendingCurseForge = new List<ModpackCurseForgeFile>();

        foreach (var file in descriptor.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            switch (file)
            {
                case ModpackDirectFile direct:
                    downloads.Add(_PlanDirectFile(direct, instanceDirectory));
                    break;

                case ModpackCurseForgeFile curseForge:
                    // 清单已给出文件名与地址时可直接下载，无需调用 API
                    if (_TryPlanSelfContainedCurseForgeFile(curseForge, instanceDirectory) is { } planned)
                        downloads.Add(planned);
                    else
                        pendingCurseForge.Add(curseForge);
                    break;
            }
        }

        if (pendingCurseForge.Count > 0)
        {
            var resolved = await _ResolveCurseForgeFilesAsync(
                pendingCurseForge, instanceDirectory, options, warnings, unresolved, cancellationToken)
                .ConfigureAwait(false);
            downloads.AddRange(resolved);
        }

        return new ModpackInstallPlan
        {
            Format = descriptor.Format,
            Metadata = descriptor.Metadata,
            Components = descriptor.Components,
            InstanceDirectory = instanceDirectory,
            Overrides = descriptor.Overrides,
            Downloads = _Deduplicate(downloads),
            LaunchOptions = descriptor.LaunchOptions,
            VersionPatch = descriptor.VersionPatch,
            EmbeddedPayloads = descriptor.EmbeddedPayloads,
            UnresolvedFiles = unresolved,
            Warnings = warnings
        };
    }

    private static ModpackPlannedDownload _PlanDirectFile(ModpackDirectFile file, string instanceDirectory)
        => new()
        {
            TargetPath = ModpackPathPolicy.ResolveWithin(instanceDirectory, file.TargetPath),
            Urls = file.Urls,
            DisplayName = Path.GetFileName(file.TargetPath),
            Sha1 = file.Sha1,
            FileSize = file.FileSize,
            Requirement = file.Requirement,
            Kind = file.Kind
        };

    /// <summary>
    /// 尝试在不调用 API 的情况下规划 CurseForge 文件。
    /// </summary>
    /// <returns>清单信息不足以确定文件名或地址时返回 <c>null</c>。</returns>
    private static ModpackPlannedDownload? _TryPlanSelfContainedCurseForgeFile(
        ModpackCurseForgeFile file, string instanceDirectory)
    {
        if (string.IsNullOrWhiteSpace(file.FileName)) return null;

        var urls = new List<string>();
        if (!string.IsNullOrWhiteSpace(file.Url)) urls.Add(file.Url.Trim());
        if (CurseForgeResourceClassifier.BuildCdnUrl(file.FileId, file.FileName) is { } cdnUrl) urls.Add(cdnUrl);
        if (urls.Count == 0) return null;

        // 清单未声明路径时，缺少分类信息，只能按模组处理
        var relativePath = file.TargetPath is not null &&
                           ModpackPathPolicy.TryNormalizeRelativePath(file.TargetPath, out var declared)
            ? declared
            : ModpackResourcePaths.CombineWithDirectory(ModpackResourceKind.Mod, file.FileName);

        return new ModpackPlannedDownload
        {
            TargetPath = ModpackPathPolicy.ResolveWithin(instanceDirectory, relativePath),
            Urls = urls.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            DisplayName = file.FileName,
            Requirement = file.Requirement,
            Kind = ModpackResourcePaths.InferKind(relativePath)
        };
    }

    private static async Task<List<ModpackPlannedDownload>> _ResolveCurseForgeFilesAsync(
        List<ModpackCurseForgeFile> pending,
        string instanceDirectory,
        ModpackInstallOptions options,
        List<string> warnings,
        List<string> unresolved,
        CancellationToken cancellationToken)
    {
        var planned = new List<ModpackPlannedDownload>(pending.Count);

        if (options.CurseForgeResolver is null)
        {
            warnings.Add($"未配置 CurseForge 文件解析器，{pending.Count} 个模组无法下载");
            unresolved.AddRange(pending.Select(file => file.DisplayName));
            return planned;
        }

        var keys = pending.Select(file => new CurseForgeFileKey(file.ProjectId, file.FileId)).ToArray();

        IReadOnlyList<CurseForgeFileDescriptor> descriptors;
        try
        {
            descriptors = await options.CurseForgeResolver
                .ResolveAsync(keys, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Modpack", "解析 CurseForge 文件信息失败");
            throw new ModpackException("获取整合包的模组下载信息失败，请检查网络连接后重试。", ex);
        }

        var descriptorByKey = descriptors
            .GroupBy(descriptor => descriptor.Key)
            .ToDictionary(group => group.Key, group => group.First());

        foreach (var file in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!descriptorByKey.TryGetValue(new CurseForgeFileKey(file.ProjectId, file.FileId), out var descriptor)
                || string.IsNullOrWhiteSpace(descriptor.FileName))
            {
                // 作者删除文件后 API 不再返回该条目，这是整合包本身的问题
                unresolved.Add(file.DisplayName);
                continue;
            }

            var urls = CurseForgeResourceClassifier.BuildDownloadUrls(descriptor, file.Url);
            if (urls.Count == 0)
            {
                unresolved.Add(descriptor.FileName);
                continue;
            }

            var relativePath = CurseForgeResourceClassifier.ResolveTargetPath(descriptor, file.TargetPath);

            planned.Add(new ModpackPlannedDownload
            {
                TargetPath = ModpackPathPolicy.ResolveWithin(instanceDirectory, relativePath),
                Urls = urls,
                DisplayName = descriptor.DisplayName ?? descriptor.FileName,
                Sha1 = descriptor.Sha1,
                FileSize = descriptor.FileSize,
                Requirement = file.Requirement,
                Kind = CurseForgeResourceClassifier.Classify(descriptor)
            });
        }

        if (unresolved.Count > 0)
            warnings.Add($"有 {unresolved.Count} 个文件无法获取下载信息，可能已被作者删除");

        return planned;
    }

    /// <summary>
    /// 按目标路径去重，保留首个 —— 同一路径重复下载会互相覆盖。
    /// </summary>
    private static List<ModpackPlannedDownload> _Deduplicate(List<ModpackPlannedDownload> downloads)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<ModpackPlannedDownload>(downloads.Count);

        foreach (var download in downloads)
        {
            if (seen.Add(download.TargetPath)) result.Add(download);
        }

        return result;
    }
}

/// <summary>
/// 安装规划选项。
/// </summary>
public sealed record ModpackInstallOptions
{
    /// <summary>实例目录，可为相对路径，规划时会解析为绝对路径。</summary>
    public required string InstanceDirectory { get; init; }

    /// <summary>
    /// CurseForge 文件解析器。为 <c>null</c> 时，需要 API 解析的文件会被记入
    /// <see cref="ModpackInstallPlan.UnresolvedFiles"/>。
    /// </summary>
    public ICurseForgeFileResolver? CurseForgeResolver { get; init; }
}
