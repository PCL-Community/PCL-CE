using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Logging;
using PCL.Core.Minecraft.Modpack.Model;
using PCL.Core.Minecraft.Modpack.Persistence;

namespace PCL.Core.Minecraft.Modpack.Installation;

/// <summary>
/// 一次整合包安装的会话。
/// <para>
/// 压缩包需要在识别、规划、释放三个阶段之间保持打开，本类负责这段生命周期。
/// 三个阶段以独立方法暴露：识别与解析是纯本地操作，可以立即完成以取得实例名与游戏版本；
/// 规划涉及 CurseForge API 调用，通常放在带进度显示的任务中执行。
/// 宿主据此编排自己的任务模型，不必接受某种固定流程。
/// </para>
/// </summary>
public sealed class ModpackInstallSession : IDisposable
{
    /// <summary>
    /// 最多向内拆解的层数。整合包被套两层已属罕见，再深则更可能是误判，
    /// 继续深入只会白白解压大文件。
    /// </summary>
    private const int MaxNestingDepth = 2;

    private readonly ModpackArchive _archive;

    /// <summary>从外层压缩包中取出内层整合包时产生的临时文件，随会话一同删除。</summary>
    private readonly string? _extractedFile;

    private bool _disposed;

    /// <summary>整合包的归一化描述，在 <see cref="OpenAsync"/> 完成时即可用。</summary>
    public ModpackDescriptor Descriptor { get; }

    /// <summary>安装方案，调用 <see cref="CreatePlanAsync"/> 后可用。</summary>
    public ModpackInstallPlan? Plan { get; private set; }

    /// <summary>
    /// 目标实例目录中已有的安装记录，调用 <see cref="CreatePlanAsync"/> 后可用。
    /// 非 <c>null</c> 时释放覆写文件按更新语义执行。
    /// </summary>
    public ModpackConfiguration? Previous { get; private set; }

    private ModpackInstallSession(ModpackArchive archive, ModpackDescriptor descriptor, string? extractedFile)
    {
        _archive = archive;
        Descriptor = descriptor;
        _extractedFile = extractedFile;
    }

    /// <summary>
    /// 打开整合包并完成识别与解析。仅访问本地文件，不产生网络请求。
    /// <para>
    /// 外层压缩包本身不是整合包时，会自动查找并取出其中被再次打包的整合包
    /// （见 <see cref="NestedModpackLocator"/>），使「附带启动器的整合包」等
    /// 嵌套分发形式也能正常安装。
    /// </para>
    /// </summary>
    /// <param name="filePath">整合包文件的绝对路径。</param>
    /// <param name="readContext">解析期可用的外部依赖。</param>
    /// <exception cref="ModpackArchiveException">压缩包无法读取。</exception>
    /// <exception cref="ModpackFormatNotRecognizedException">未能识别整合包格式，且其中不含可识别的内层整合包。</exception>
    /// <exception cref="ModpackManifestInvalidException">清单不合法。</exception>
    /// <exception cref="ModpackUnsupportedContentException">整合包要求当前启动器不支持的内容。</exception>
    public static async Task<ModpackInstallSession> OpenAsync(
        string filePath,
        ModpackReadContext? readContext = null,
        CancellationToken cancellationToken = default)
    {
        var identifier = ModpackIdentifier.Shared;
        var archive = ModpackArchive.Open(filePath);
        string? extractedFile = null;

        try
        {
            for (var depth = 0; depth < MaxNestingDepth && identifier.Identify(archive) is null; depth++)
            {
                var nested = await NestedModpackLocator
                    .TryExtractAsync(archive, identifier, cancellationToken)
                    .ConfigureAwait(false);

                // 没有可用的内层整合包，交由下面的解析步骤抛出「无法识别」
                if (nested is null) break;

                archive.Dispose();
                NestedModpackLocator.DeleteTemporaryFile(extractedFile);

                extractedFile = nested;
                archive = ModpackArchive.Open(nested);
            }

            var descriptor = await identifier
                .ReadAsync(archive, readContext, cancellationToken)
                .ConfigureAwait(false);

            return new ModpackInstallSession(archive, descriptor, extractedFile);
        }
        catch
        {
            archive.Dispose();
            NestedModpackLocator.DeleteTemporaryFile(extractedFile);
            throw;
        }
    }

    /// <summary>
    /// 生成安装方案。需要解析 CurseForge 文件时会产生网络请求。
    /// </summary>
    /// <exception cref="ModpackException">获取文件下载信息失败。</exception>
    /// <exception cref="ModpackUnsafePathException">整合包中存在越出实例目录的路径。</exception>
    public async Task<ModpackInstallPlan> CreatePlanAsync(
        ModpackInstallOptions options, CancellationToken cancellationToken = default)
    {
        _ThrowIfDisposed();

        var plan = await ModpackInstallPlanner.Shared
            .CreateAsync(Descriptor, options, cancellationToken)
            .ConfigureAwait(false);

        Previous = ModpackConfigurationStore.TryRead(plan.InstanceDirectory);
        if (Previous is not null)
            LogWrapper.Info("Modpack",
                $"检测到实例已有整合包安装记录（{Previous.Type} {Previous.Version}），本次按更新处理");

        Plan = plan;
        return plan;
    }

    /// <summary>
    /// 释放覆写目录到实例目录。
    /// </summary>
    /// <param name="progress">进度回调，取值 0 到 1。</param>
    /// <returns>已释放文件的快照，供 <see cref="WriteConfigurationAsync"/> 记录。</returns>
    public Task<IReadOnlyList<ModpackFileSnapshot>> ExtractOverridesAsync(
        IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        var plan = _RequirePlan();

        return ModpackOverrideExtractor.ExtractAsync(
            _archive, plan.Overrides, plan.InstanceDirectory, progress, Previous, cancellationToken);
    }

    /// <summary>
    /// 释放内嵌载荷（库文件、JAR Mod），保持其原有目录结构。
    /// </summary>
    public async Task<IReadOnlyList<ModpackFileSnapshot>> ExtractPayloadsAsync(
        IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        var plan = _RequirePlan();
        if (plan.EmbeddedPayloads.Count == 0) return [];

        var directives = new List<ModpackOverride>(plan.EmbeddedPayloads.Count);
        foreach (var payload in plan.EmbeddedPayloads)
            directives.Add(new ModpackOverride(payload.ArchiveDirectory, payload.ArchiveDirectory));

        return await ModpackOverrideExtractor
            .ExtractAsync(_archive, directives, plan.InstanceDirectory, progress, previous: null, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// 把压缩包内的某个文件复制到指定位置，用于取出实例图标等零散资源。
    /// </summary>
    /// <returns>压缩包内不存在该条目时返回 <c>false</c>。</returns>
    public async Task<bool> TryExtractFileAsync(
        string archivePath, string destinationPath, CancellationToken cancellationToken = default)
    {
        _ThrowIfDisposed();

        var entry = _archive.TryGetEntry(archivePath);
        if (entry is null) return false;

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

        await using var source = entry.Open();
        await using var destination = File.Create(destinationPath);
        await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// 写入 <c>modpack.json</c> 安装记录。
    /// </summary>
    /// <param name="overrides"><see cref="ExtractOverridesAsync"/> 返回的快照。</param>
    public Task WriteConfigurationAsync(
        IReadOnlyList<ModpackFileSnapshot> overrides, CancellationToken cancellationToken = default)
    {
        var plan = _RequirePlan();

        var configuration = ModpackConfigurationStore.Create(plan, overrides, Descriptor.RawManifest);
        return ModpackConfigurationStore.WriteAsync(plan.InstanceDirectory, configuration, cancellationToken);
    }

    private ModpackInstallPlan _RequirePlan()
    {
        _ThrowIfDisposed();
        return Plan ?? throw new InvalidOperationException(
            $"请先调用 {nameof(CreatePlanAsync)} 生成安装方案。");
    }

    private void _ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _archive.Dispose();
        NestedModpackLocator.DeleteTemporaryFile(_extractedFile);
    }
}
