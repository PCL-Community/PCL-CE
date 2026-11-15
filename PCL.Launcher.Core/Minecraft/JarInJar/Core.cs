using System.IO.Compression;

namespace PCL;

/// <summary>Jar-in-Jar（内嵌模组）解析。</summary>
public static partial class JarInJar
{
    internal const int MaxDepth = 5;
    internal const int MaxNodes = 512;
    private const long MaxEntryBytes = 256L * 1024 * 1024;
    private const long MaxTotalBytes = 512L * 1024 * 1024;
    private const long MaxScanBytes = 2L * 1024 * 1024 * 1024;

    /// <summary>
    ///     带持久化缓存的解析：按文件指纹命中缓存则直接重建，否则解析并写入缓存（批量结束后需调用
    ///     <see cref="JarInJarCache.Flush" /> 落盘）。
    /// </summary>
    internal static List<LocalComp.LocalCompFile>? ResolveCached(
        string modFilePath,
        ZipArchive jar,
        out bool complete,
        bool deferOnMiss = false,
        long? expectedLastModified = null,
        long? expectedSize = null,
        ScanContext? scan = null,
        string? preferredLoader = null)
    {
        complete = true;
        long lastModified, size;
        try
        {
            var fi = new FileInfo(modFilePath);
            lastModified = expectedLastModified ?? fi.LastWriteTimeUtc.Ticks;
            size = expectedSize ?? fi.Length;
            if (!_FingerprintMatches(fi, lastModified, size))
            {
                complete = false;
                return null;
            }
        }
        catch
        {
            if (deferOnMiss)
            {
                complete = false;
                return null;
            }

            var fallbackBudget = new ResolveBudget
            {
                Scan = scan,
                PreferredLoader = preferredLoader
            };
            var fallback = _Resolve(modFilePath, jar, 0, fallbackBudget);
            complete = !fallbackBudget.Incomplete;
            return fallback;
        }

        var cached = JarInJarCache.TryGet(modFilePath, lastModified, size);
        if (cached is not null)
            return _FromNodes(cached, modFilePath);

        if (deferOnMiss)
        {
            complete = false;
            return null;
        }

        var budget = new ResolveBudget
        {
            Scan = scan,
            PreferredLoader = preferredLoader
        };
        var tree = _Resolve(modFilePath, jar, 0, budget);
        complete = !budget.Incomplete;
        try
        {
            if (!_FingerprintMatches(new FileInfo(modFilePath), lastModified, size))
                complete = false;
        }
        catch
        {
            complete = false;
        }

        if (complete)
            try
            {
                JarInJarCache.Set(
                    modFilePath,
                    lastModified,
                    size,
                    _ToNodes(tree));
            }
            catch (Exception ex)
            {
                complete = false;
                LauncherCommon.Log(ex, "写入 Jar-in-Jar 缓存条目失败", LauncherLogLevel.Developer);
            }

        return tree;
    }

    private static bool _FingerprintMatches(FileInfo file, long lastModified, long size)
    {
        file.Refresh();
        return file.Exists && file.LastWriteTimeUtc.Ticks == lastModified && file.Length == size;
    }

    private static List<EmbeddedModNode> _ToNodes(List<LocalComp.LocalCompFile> mods)
    {
        return
        [
            .. mods.Select(m => new EmbeddedModNode
            {
                FileName = m.FileName,
                Name = m.Name,
                ModId = m.ModId,
                Version = m.Version,
                Loader = m.JijLoader,
                TargetMcVersion = m.JijTargetMcVersion,
                DependencyRows =
                [
                    .. m.DependencyDeclarations
                        .Select(d => new EmbeddedDependency
                        {
                            Id = d.Id,
                            Raw = d.Raw,
                            Optional = d.Optional
                        })
                ],
                Conflicts =
                [
                    .. m.ConflictDeclarations.Select(c => new EmbeddedConflict
                    {
                        DeclarerId = c.DeclarerId,
                        Target = c.Target,
                        Raw = c.Raw,
                        Hard = c.Hard
                    })
                ],
                ProvidedIds = [.. m.ProvidedIds],
                ProvidedVersions = new Dictionary<string, string>(m.ProvidedVersions),
                JijIdentifier = m.JijIdentifier,
                JijVersionRange = m.JijVersionRange,
                JijArtifactVersion = m.JijArtifactVersion,
                Children = _ToNodes(m.EmbeddedMods)
            })
        ];
    }

    private static List<LocalComp.LocalCompFile> _FromNodes(
        List<EmbeddedModNode>? nodes,
        string parentPath)
    {
        var result = new List<LocalComp.LocalCompFile>();
        if (nodes is null)
            return result;

        foreach (var node in nodes)
        {
            var childPath = $"{parentPath}!/{node.FileName}";
            var child = new LocalComp.LocalCompFile(childPath);
            child.SetJijMetadata(node.Name, node.ModId, node.Version);
            child.JijLoader = node.Loader;
            child.JijTargetMcVersion = node.TargetMcVersion;
            child.JijIdentifier = node.JijIdentifier;
            child.JijVersionRange = node.JijVersionRange;
            child.JijArtifactVersion = node.JijArtifactVersion;
            child.SetJijDependencies(node.DependencyRows);
            child.SetJijConflicts(node.Conflicts, node.ProvidedIds, node.ProvidedVersions);
            child.EmbeddedMods = _FromNodes(node.Children, childPath);
            result.Add(child);
        }

        return result;
    }

    private static List<LocalComp.LocalCompFile> _Resolve(
        string parentPath,
        ZipArchive jar,
        int depth,
        ResolveBudget budget)
    {
        var result = new List<LocalComp.LocalCompFile>();
        if (budget.Scan?.IsCancelled == true)
        {
            budget.Incomplete = true;
            return result;
        }

        var nested = new List<NestedJarInfo>();
        if (!_CollectFabricNestedJars(jar, nested))
            budget.Incomplete = true;
        if (!_CollectQuiltNestedJars(jar, nested))
            budget.Incomplete = true;
        if (!_CollectForgeNestedJars(jar, nested))
            budget.Incomplete = true;
        if (!_CollectManifestEmbeddedJars(jar, nested))
            budget.Incomplete = true;
        nested =
        [
            .. nested
                .Where(n => !string.IsNullOrWhiteSpace(n.Path))
                .GroupBy(n => n.Path, StringComparer.Ordinal)
                .Select(g => g
                    .OrderByDescending(n => n.Identifier is not null)
                    .First())
        ];
        if (depth >= MaxDepth)
        {
            if (nested.Count > 0) budget.Incomplete = true;
            return result;
        }

        foreach (var info in nested)
        {
            if (budget.Scan?.IsCancelled == true || budget.NodesRemaining <= 0)
            {
                budget.Incomplete = true;
                break;
            }

            if (info.Path is null) continue;
            var entry = jar.GetEntry(info.Path);
            if (entry is null) continue;
            try
            {
                _ = LauncherCommon.GetFileNameFromPath(info.Path);
            }
            catch (Exception ex)
            {
                budget.Incomplete = true;
                LauncherCommon.Log(
                    ex,
                    $"跳过路径无效的内嵌 Mod（{parentPath} -> {info.Path}）",
                    LauncherLogLevel.Developer);
                continue;
            }

            if (entry.Length > MaxEntryBytes || entry.Length > budget.BytesRemaining)
            {
                budget.Incomplete = true;
                LauncherCommon.Log(
                    $"跳过过大的内嵌 Mod（{parentPath} -> {info.Path}，{entry.Length} bytes）",
                    LauncherLogLevel.Developer);
                continue;
            }

            budget.NodesRemaining--;

            var childPath = $"{parentPath}!/{info.Path}";
            var child = new LocalComp.LocalCompFile(childPath);
            child.MarkLoaded();
            child.JijIdentifier = info.Identifier;
            child.JijVersionRange = info.VersionRange;
            child.JijArtifactVersion = info.ArtifactVersion;
            result.Add(child);

            string? tmp = null;
            try
            {
                tmp = Path.GetTempFileName();
                using (var es = entry.Open())
                using (var fs = File.Create(tmp))
                {
                    _CopyWithBudget(es, fs, budget);
                }

                using var nestedJar = ZipFile.OpenRead(tmp);
                child.JijLoader = DetectLoader(nestedJar, budget.PreferredLoader);
                child.PreferredLoader = budget.PreferredLoader;
                child.LookupMetadata(nestedJar, child.JijLoader);
                child.JijTargetMcVersion = child.DependencyRaw.TryGetValue("minecraft", out var mc)
                    ? mc
                    : null;
                child.EmbeddedMods = _Resolve(childPath, nestedJar, depth + 1, budget);
            }
            catch (OperationCanceledException)
            {
                budget.Incomplete = true;
                throw;
            }
            catch (Exception ex)
            {
                budget.Incomplete = true;
                LauncherCommon.Log(
                    ex,
                    $"解析内嵌 Mod 失败（{parentPath} -> {info.Path}）",
                    LauncherLogLevel.Developer);
            }
            finally
            {
                if (tmp is not null)
                    try
                    {
                        File.Delete(tmp);
                    }
                    catch
                    {
                        // 临时文件清理失败无妨
                    }
            }
        }

        return result;
    }

    private static void _CopyWithBudget(Stream input, Stream output, ResolveBudget budget)
    {
        var buffer = new byte[81920];
        long entryBytes = 0;
        while (true)
        {
            var read = input.Read(buffer, 0, buffer.Length);
            if (read <= 0) return;
            entryBytes += read;
            budget.BytesRemaining -= read;
            if (budget.Scan?.IsCancelled == true)
            {
                budget.Incomplete = true;
                throw new OperationCanceledException("Jar-in-Jar scan cancelled");
            }

            if (entryBytes > MaxEntryBytes
                || budget.BytesRemaining < 0
                || (budget.Scan is not null && !budget.Scan.TryConsume(read)))
            {
                budget.Incomplete = true;
                throw new InvalidDataException("内嵌 Mod 解压体积超过安全上限");
            }

            output.Write(buffer, 0, read);
        }
    }

    internal sealed class ScanContext(Func<bool>? isCancelled = null)
    {
        private long _bytesRemaining = MaxScanBytes;

        public bool IsCancelled => isCancelled?.Invoke() == true;

        public bool TryConsume(int count)
        {
            return !IsCancelled && Interlocked.Add(ref _bytesRemaining, -count) >= 0;
        }
    }

    private sealed class ResolveBudget
    {
        public long BytesRemaining = MaxTotalBytes;
        public bool Incomplete;
        public int NodesRemaining = MaxNodes;
        public string? PreferredLoader;
        public ScanContext? Scan;
    }

    private sealed class NestedJarInfo
    {
        public string? ArtifactVersion;
        public string? Identifier;
        public string? Path;
        public string? VersionRange;
    }
}