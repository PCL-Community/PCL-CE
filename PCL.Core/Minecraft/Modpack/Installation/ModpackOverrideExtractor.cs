using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Logging;
using PCL.Core.Minecraft.Modpack.Model;
using PCL.Core.Minecraft.Modpack.Persistence;

namespace PCL.Core.Minecraft.Modpack.Installation;

/// <summary>
/// 把整合包的覆写目录释放到实例目录。
/// <para>
/// 直接从压缩包流式写入目标位置，不经过临时目录中转 ——
/// 整合包动辄数百 MB，中转会带来一倍的磁盘读写与占用。
/// 写入的同时增量计算 SHA-1，供 <c>modpack.json</c> 记录与后续更新比对，
/// 避免安装结束后再整体扫描一遍。
/// </para>
/// </summary>
public static class ModpackOverrideExtractor
{
    private const int BufferSize = 128 * 1024;

    /// <summary>
    /// 释放覆写目录。
    /// </summary>
    /// <param name="archive">整合包压缩包。</param>
    /// <param name="overrides">覆写指令，按顺序应用 —— 后者覆盖前者。</param>
    /// <param name="instanceDirectory">实例目录的绝对路径。</param>
    /// <param name="progress">进度回调，取值 0 到 1。</param>
    /// <param name="previous">
    /// 上一次安装记录。提供时执行更新语义：用户改动过的文件不被覆盖，
    /// 旧版本有而新版本没有的文件被删除。
    /// </param>
    /// <returns>已释放文件的路径与 SHA-1 快照。</returns>
    /// <exception cref="ModpackUnsafePathException">压缩包中存在越出实例目录的路径。</exception>
    public static async Task<IReadOnlyList<ModpackFileSnapshot>> ExtractAsync(
        ModpackArchive archive,
        IReadOnlyList<ModpackOverride> overrides,
        string instanceDirectory,
        IProgress<double>? progress = null,
        ModpackConfiguration? previous = null,
        CancellationToken cancellationToken = default)
    {
        var root = Path.GetFullPath(instanceDirectory);
        Directory.CreateDirectory(root);

        var items = _CollectItems(archive, overrides, root);
        var snapshots = new List<ModpackFileSnapshot>(items.Count);

        var previousHashes = previous?.BuildOverrideIndex();
        var totalBytes = Math.Max(1L, items.Sum(item => Math.Max(item.Entry.Length, 1L)));
        var writtenBytes = 0L;

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var keepUserEdit = previousHashes is not null &&
                               _ShouldPreserveUserEdit(item, previousHashes);

            // 用户改动过的文件保留现状，但仍要记录新包中的 hash 以便下次比对
            var hash = keepUserEdit
                ? await _ComputeEntryHashAsync(item, cancellationToken).ConfigureAwait(false)
                : await _ExtractEntryAsync(item, cancellationToken).ConfigureAwait(false);

            snapshots.Add(new ModpackFileSnapshot(item.RelativePath, hash));

            writtenBytes += Math.Max(item.Entry.Length, 1L);
            progress?.Report(Math.Min(1d, (double)writtenBytes / totalBytes));
        }

        if (previous is not null) _RemoveStaleFiles(previous, snapshots, root);

        progress?.Report(1d);
        return snapshots;
    }

    /// <summary>
    /// 展开全部覆写指令，得到「压缩包条目 → 实例内相对路径」的列表。
    /// 后出现的指令覆盖先前同路径的条目。
    /// </summary>
    private static List<_ExtractItem> _CollectItems(
        ModpackArchive archive, IReadOnlyList<ModpackOverride> overrides, string root)
    {
        var byTarget = new Dictionary<string, _ExtractItem>(StringComparer.OrdinalIgnoreCase);

        foreach (var directive in overrides)
        {
            foreach (var item in archive.EnumerateFiles(directive.ArchiveDirectory))
            {
                var relative = directive.TargetSubPath.Length > 0
                    ? $"{directive.TargetSubPath}/{item.RelativePath}"
                    : item.RelativePath;

                if (!ModpackPathPolicy.TryNormalizeRelativePath(relative, out var normalized))
                    throw new ModpackUnsafePathException(relative);

                var fullPath = Path.GetFullPath(Path.Combine(root, normalized));
                if (!ModpackPathPolicy.IsWithin(root, fullPath))
                    throw new ModpackUnsafePathException(relative);

                byTarget[normalized] = new _ExtractItem(normalized, fullPath, item.Entry);
            }
        }

        return [.. byTarget.Values];
    }

    /// <summary>
    /// 判断目标文件是否被用户改动过 —— 改动过则不应被整合包更新覆盖。
    /// </summary>
    private static bool _ShouldPreserveUserEdit(
        _ExtractItem item, IReadOnlyDictionary<string, string> previousHashes)
    {
        if (!File.Exists(item.FullPath)) return false;
        if (!previousHashes.TryGetValue(item.RelativePath, out var recordedHash)) return false;

        try
        {
            using var stream = File.OpenRead(item.FullPath);
            var currentHash = Convert.ToHexStringLower(SHA1.HashData(stream));
            return !string.Equals(currentHash, recordedHash, StringComparison.OrdinalIgnoreCase);
        }
        catch (IOException)
        {
            // 读不到就当作未改动，交由后续写入步骤报告真正的错误
            return false;
        }
    }

    /// <summary>
    /// 释放单个条目并返回其 SHA-1。
    /// </summary>
    private static async Task<string> _ExtractEntryAsync(_ExtractItem item, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(item.FullPath)!);

        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);

        try
        {
            await using var source = item.Entry.Open();
            await using var destination = new FileStream(
                item.FullPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);

            while (true)
            {
                var read = await source.ReadAsync(buffer.AsMemory(0, BufferSize), cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0) break;

                hasher.AppendData(buffer, 0, read);
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return Convert.ToHexStringLower(hasher.GetHashAndReset());
    }

    /// <summary>
    /// 只计算条目的 SHA-1，不写出文件。
    /// </summary>
    private static async Task<string> _ComputeEntryHashAsync(_ExtractItem item, CancellationToken cancellationToken)
    {
        await using var source = item.Entry.Open();
        var hash = await SHA1.HashDataAsync(source, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// 删除上一版本存在、当前版本已移除的覆写文件。
    /// 用户新增的文件不在记录中，因此不受影响。
    /// </summary>
    private static void _RemoveStaleFiles(
        ModpackConfiguration previous, List<ModpackFileSnapshot> current, string root)
    {
        var currentPaths = new HashSet<string>(
            current.Select(snapshot => snapshot.Path), StringComparer.OrdinalIgnoreCase);

        foreach (var stale in previous.Overrides)
        {
            if (currentPaths.Contains(stale.Path)) continue;
            if (!ModpackPathPolicy.TryNormalizeRelativePath(stale.Path, out var normalized)) continue;

            var fullPath = Path.Combine(root, normalized);
            if (!ModpackPathPolicy.IsWithin(root, Path.GetFullPath(fullPath)) || !File.Exists(fullPath)) continue;
            if (!_MatchesRecordedHash(fullPath, stale.Hash))
            {
                LogWrapper.Debug("Modpack", $"保留用户修改过的已移除文件：{normalized}");
                continue;
            }

            try
            {
                File.Delete(fullPath);
                LogWrapper.Debug("Modpack", $"已删除整合包更新中移除的文件：{normalized}");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // 逐个文件的问题只记日志：Warn 在调试构建中会弹提示条，按文件数量弹会刷屏
                LogWrapper.Info("Modpack", $"删除文件失败：{normalized}（{ex.Message}）");
            }
        }
    }

    /// <summary>
    /// 只有磁盘文件仍与上次安装记录一致时，更新流程才拥有删除它的权限。
    /// 无法读取或记录缺少校验值时按用户文件处理并保留。
    /// </summary>
    private static bool _MatchesRecordedHash(string filePath, string recordedHash)
    {
        if (string.IsNullOrWhiteSpace(recordedHash)) return false;

        try
        {
            using var stream = File.OpenRead(filePath);
            var currentHash = Convert.ToHexStringLower(SHA1.HashData(stream));
            return string.Equals(currentHash, recordedHash, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogWrapper.Info("Modpack", $"读取待删除文件失败，已保留：{filePath}（{ex.Message}）");
            return false;
        }
    }

    /// <param name="RelativePath">相对于实例目录的路径。</param>
    /// <param name="FullPath">目标文件的绝对路径。</param>
    /// <param name="Entry">压缩包条目。</param>
    private readonly record struct _ExtractItem(
        string RelativePath,
        string FullPath,
        System.IO.Compression.ZipArchiveEntry Entry);
}
