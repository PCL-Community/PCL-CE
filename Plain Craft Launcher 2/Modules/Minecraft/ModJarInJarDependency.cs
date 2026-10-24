using System;
using System.Collections.Generic;
using System.Linq;
using CompFile = PCL.ModLocalComp.LocalCompFile;

namespace PCL;

/// <summary>内嵌模组（Jar-in-Jar）依赖状态。</summary>
public enum JijDepStatus
{
    Installed, // 有启用的独立/其它内嵌提供者
    Disabled, // 有满足版本的提供者但都被禁用
    Bundled, // 无独立提供，但本 Mod 内嵌了它
    VersionMismatch, // 有提供者但版本都不满足约束（装了，但装错版本）
    Missing // 无任何提供者（根本没装）
}

/// <summary>加载器与运行时平台伪依赖的统一判定。</summary>
public static class ModDependencyIds
{
    private static readonly HashSet<string> _loaderIds = new(StringComparer.OrdinalIgnoreCase)
        { "forge", "neoforge", "fabricloader", "quilt", "quilt_loader", "java", "mcp" };

    public static bool IsLoaderId(string id) => _loaderIds.Contains(id);

    public static bool IsPlatform(string id) =>
        string.Equals(id, "minecraft", StringComparison.OrdinalIgnoreCase) || _loaderIds.Contains(id);
}

/// <summary>按实例版本索引可加载的内嵌模组、依赖、提供者与冲突。</summary>
public class ModJarInJarIndex
{
    public sealed class DepRow
    {
        public string DepId;
        public string Raw; // 原始版本约束，null=无版本要求
        public bool Optional;
        public string Loader; // 声明方的加载器（决定版本方言），null=未知
    }

    private sealed class Provider
    {
        public CompFile Host;
        public CompFile Source;
        public string Version;
    }

    private readonly List<CompFile> _allMods;
    private readonly Dictionary<string, List<Provider>> _providers =
        new(StringComparer.OrdinalIgnoreCase);
    // 每个 Mod 内嵌提供的 (id, 版本) 列表（同一 id 的多版本 wrapper 保留全部副本版本）
    private readonly Dictionary<CompFile, List<(string Id, string Version)>> _selfBundled = new();
    // 用于缺失警告与级联反查（宿主是启用/禁用单位，故内嵌依赖归到宿主承担）
    private readonly Dictionary<CompFile, List<DepRow>> _deps = new();
    // 每个宿主的可加载内嵌节点（关系页把有依赖的内嵌 mod 单独成卡时遍历）
    private readonly Dictionary<CompFile, List<CompFile>> _loadableNodes = new();
    // 内嵌节点到物理宿主的反查，供单节点关系展示判断同 wrapper 的 provider。
    private readonly Dictionary<CompFile, CompFile> _nodeHosts = new();
    // 每个实际元数据节点所声明的 ModId/兄弟 ID 到展示来源的映射。
    private readonly Dictionary<(CompFile Owner, string Id), CompFile> _declaredSources = new();

    public ModJarInJarIndex(IEnumerable<CompFile> allMods, string mc)
    {
        _allMods = allMods
            .Where(m => !m.IsFolder && m.State != CompFile.LocalFileStatus.Unavailable)
            .ToList();
        var activeMods = _allMods.Where(m => m.State == CompFile.LocalFileStatus.Fine).ToList();
        var preliminary = activeMods.ToDictionary(m => m, m => _CollectLoadableNodes(m.EmbeddedMods, mc));
        var forgeSelections = _SelectForgeJarJarVersions(preliminary.Values.SelectMany(n => n));
        var afterForge = activeMods.ToDictionary(m => m,
            m => _CollectLoadableNodes(m.EmbeddedMods, mc, forgeSelections));
        var fabricSelections = _SelectFabricVersions(afterForge, mc, forgeSelections);
        foreach (var m in _allMods)
        {
            var nodes = m.State == CompFile.LocalFileStatus.Fine
                ? _CollectLoadableNodes(m.EmbeddedMods, mc, forgeSelections, fabricSelections)
                : _CollectLoadableNodes(m.EmbeddedMods, mc);
            _loadableNodes[m] = nodes;
            foreach (var node in nodes) _nodeHosts[node] = m;
            var bundled = new List<(string Id, string Version)>();
            if (!string.IsNullOrWhiteSpace(m.ModId)) bundled.Add((m.ModId, m.Version));
            foreach (var id in m.ProvidedIds.Where(id => !string.IsNullOrWhiteSpace(id)))
                bundled.Add((id, m.ProvidedVersions.TryGetValue(id, out var version) ? version : m.Version));
            foreach (var node in nodes)
            {
                if (!string.IsNullOrWhiteSpace(node.ModId)) bundled.Add((node.ModId, node.Version));
                foreach (var id in node.ProvidedIds.Where(id => !string.IsNullOrWhiteSpace(id)))
                    bundled.Add((id,
                        node.ProvidedVersions.TryGetValue(id, out var version) ? version : node.Version));
            }
            _selfBundled[m] = bundled;

            if (!string.IsNullOrEmpty(m.ModId))
            {
                _RegisterSource(m, m.ModId, m);
                _AddProvider(m.ModId, m, m, m.Version);
            }
            foreach (var pid in m.ProvidedIds)
            {
                var hasOwnVersion = m.ProvidedVersions.TryGetValue(pid, out var providedVersion);
                var source = hasOwnVersion ? _CreateSiblingSource(m, pid, providedVersion) : m;
                _RegisterSource(m, pid, source);
                _AddProvider(pid, m, source, hasOwnVersion ? providedVersion : m.Version);
            }
            foreach (var n in nodes.Where(n => !string.IsNullOrEmpty(n.ModId)))
            {
                _RegisterSource(n, n.ModId, n);
                _AddProvider(n.ModId, m, n, n.Version);
            }
            // 内嵌节点自身的别名(multi-mod 兄弟/provides)也由宿主提供
            foreach (var n in nodes)
            foreach (var pid in n.ProvidedIds)
            {
                var hasOwnVersion = n.ProvidedVersions.TryGetValue(pid, out var providedVersion);
                var source = hasOwnVersion ? _CreateSiblingSource(n, pid, providedVersion) : n;
                _RegisterSource(n, pid, source);
                _AddProvider(pid, m, source, hasOwnVersion ? providedVersion : n.Version);
            }

            var rows = new List<DepRow>();
            foreach (var declaration in m.DependencyDeclarations)
                rows.Add(new DepRow
                {
                    DepId = declaration.Id, Raw = declaration.Raw,
                    Optional = declaration.Optional, Loader = m.DetectedLoader
                });
            foreach (var n in nodes)
            foreach (var declaration in n.DependencyDeclarations)
                rows.Add(new DepRow
                {
                    DepId = declaration.Id, Raw = declaration.Raw,
                    Optional = declaration.Optional, Loader = n.JijLoader
                });
            _deps[m] = rows
                .GroupBy(r => (r.DepId, r.Raw, r.Loader, r.Optional))
                .Select(g => g.First())
                .ToList();
        }
    }

    /// <summary>某 Mod 的有效依赖（含其内嵌 mod 上浮的依赖）。用于缺失警告与级联。</summary>
    public IReadOnlyList<DepRow> GetDependencies(CompFile mod) =>
        _deps.TryGetValue(mod, out var list) ? list : new List<DepRow>();

    /// <summary>宿主的可加载内嵌节点（关系页遍历，把有依赖的内嵌 mod 单独成卡）。</summary>
    public IReadOnlyList<CompFile> GetLoadableEmbedded(CompFile host) =>
        _loadableNodes.TryGetValue(host, out var list) ? list : new List<CompFile>();

    public IReadOnlyList<string> GetProviderVersions(CompFile dependent, string depId)
    {
        if (!_providers.TryGetValue(_Norm(depId), out var providers)) return Array.Empty<string>();
        return providers.Where(provider => provider.Source != dependent)
            .Select(provider => provider.Version)
            .Where(version => !string.IsNullOrWhiteSpace(version))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>构造某 mod 自身声明的依赖行（不含内嵌上浮），供关系页按 mod 分卡展示。</summary>
    public static List<DepRow> BuildOwnDependencies(CompFile mod, string loader) =>
        mod.DependencyDeclarations.Select(declaration => new DepRow
        {
            DepId = declaration.Id, Raw = declaration.Raw,
            Optional = declaration.Optional, Loader = loader
        }).ToList();

    // ModId 比较只忽略大小写；连字符与下划线在 Fabric/Forge 元数据中均可能是不同合法 ID。
    private static string _Norm(string id) => id?.ToLowerInvariant();

    private static bool _VersionSatisfies(DepRow dep, string providerVersion)
    {
        if (dep.Raw is null) return true;
        // provider 版本缺失或含未替换占位符时无法可靠判断，保守视为满足。
        if (string.IsNullOrWhiteSpace(providerVersion)) return true;
        var ver = McConstraintMatcher.StripV(providerVersion.Trim());
        if (ver.Length == 0 || ver.Contains("$")) return true;
        if (McConstraintMatcher.Satisfies(dep.Raw, dep.Loader, ver)) return true;
        // 已知方言时，合法/非法约束均按该加载器的 fail-closed 结果处理；仅未知方言保留旧的 fail-open。
        if (!string.IsNullOrWhiteSpace(dep.Loader)) return false;
        return !McConstraintMatcher.HasComparableBound(dep.Raw);
    }

    private bool _SelfBundleSatisfies(CompFile mod, DepRow dep)
    {
        if (_selfBundled.TryGetValue(mod, out var self) &&
            self.Any(x => string.Equals(_Norm(x.Id), _Norm(dep.DepId), StringComparison.OrdinalIgnoreCase) &&
                          _VersionSatisfies(dep, x.Version)))
            return true;
        return false;
    }

    /// <summary>返回某条依赖在当前实例中的状态。</summary>
    public JijDepStatus Analyze(CompFile mod, DepRow dep)
    {
        if (_SelfBundleSatisfies(mod, dep) ||
            (_nodeHosts.TryGetValue(mod, out var host) && _SelfBundleSatisfies(host, dep)))
            return JijDepStatus.Bundled;
        _providers.TryGetValue(_Norm(dep.DepId), out var provs);
        var others = provs?.Where(p => p.Source != mod).ToList() ?? new List<Provider>();
        var satisfying = others.Where(p => _VersionSatisfies(dep, p.Version)).ToList();
        if (satisfying.Any(p => p.Host.State == CompFile.LocalFileStatus.Fine)) return JijDepStatus.Installed;
        if (satisfying.Count > 0) return JijDepStatus.Disabled;
        if (others.Count > 0) return JijDepStatus.VersionMismatch;
        return JijDepStatus.Missing;
    }

    /// <summary>
    ///     实际生效的冲突关系：声明方与对方均启用(Fine)、且对方版本落在冲突范围内（range 空=任意版本）。
    ///     无序对去重，同一对同时被硬/软声明时取硬；返回实际声明/提供该 ID 的顶层或内嵌节点。
    /// </summary>
    public List<(CompFile A, CompFile B, bool Hard)> FindActiveConflicts()
    {
        var order = new Dictionary<CompFile, int>();
        foreach (var host in _allMods)
        {
            order[host] = order.Count;
            foreach (var node in _loadableNodes[host])
                if (!order.ContainsKey(node)) order[node] = order.Count;
        }
        foreach (var source in _providers.Values.SelectMany(p => p).Select(p => p.Source).Distinct())
            if (!order.ContainsKey(source)) order[source] = order.Count;
        var pairs = new Dictionary<(CompFile, CompFile), bool>();
        foreach (var m in _allMods)
        {
            if (m.State != CompFile.LocalFileStatus.Fine) continue;
            // 宿主自身冲突 + 各可加载内嵌节点冲突；展示保留实际来源，启禁用状态仍由 Provider.Host 判断。
            _CollectConflicts(m, m.ConflictDeclarations, m.DetectedLoader, order, pairs);
            foreach (var n in _loadableNodes[m])
                _CollectConflicts(n, n.ConflictDeclarations, n.JijLoader, order, pairs);
        }

        return pairs.Select(kv => (kv.Key.Item1, kv.Key.Item2, kv.Value)).ToList();
    }

    private void _CollectConflicts(CompFile declarerSource,
        IEnumerable<EmbeddedConflict> conflicts, string loader,
        Dictionary<CompFile, int> order, Dictionary<(CompFile, CompFile), bool> pairs)
    {
        foreach (var conflict in conflicts)
        {
            var actualDeclarer = _ResolveConflictSource(declarerSource, conflict.DeclarerId);
            if (!_providers.TryGetValue(_Norm(conflict.Target), out var provs)) continue;
            var (range, hard) = (conflict.Raw, conflict.Hard);
            foreach (var p in provs)
            {
                if (p.Source == actualDeclarer || p.Host.State != CompFile.LocalFileStatus.Fine) continue;
                // 对方版本满足 range 才算撞上（按声明方 loader 方言）；range 空=任意版本都冲突
                if (range != null)
                {
                    var ver = McConstraintMatcher.StripV(p.Version?.Trim() ?? "");
                    // 未替换占位符无法可靠判定冲突范围；普通字符串版本交由加载器方言处理。
                    if (ver.Length == 0 || ver.Contains("$")) continue;
                    if (!McConstraintMatcher.Satisfies(range, loader, ver)) continue;
                }
                var key = order[actualDeclarer] < order[p.Source]
                    ? (actualDeclarer, p.Source)
                    : (p.Source, actualDeclarer);
                pairs[key] = pairs.TryGetValue(key, out var h) ? h || hard : hard;
            }
        }
    }

    /// <summary>
    ///     移除 <paramref name="targets" /> 后，哪些仍启用的 Mod 会因此丢失依赖（传递闭包，
    ///     "最后一个提供者"才算丢失）。返回不含 targets 自身。
    /// </summary>
    public List<CompFile> FindAffected(IEnumerable<CompFile> targets)
    {
        var targetSet = new HashSet<CompFile>(targets);
        var removal = new HashSet<CompFile>(targetSet);
        bool changed;
        do
        {
            changed = false;
            foreach (var c in _allMods)
            {
                if (c.State != CompFile.LocalFileStatus.Fine || removal.Contains(c)) continue;
                foreach (var dep in GetDependencies(c))
                {
                    if (ModDependencyIds.IsPlatform(dep.DepId)) continue;
                    if (dep.Optional) continue; // 可选依赖不参与级联
                    if (_SelfBundleSatisfies(c, dep)) continue;
                    if (!_providers.TryGetValue(_Norm(dep.DepId), out var provs)) continue;
                    var active = provs
                        .Where(p => p.Host.State == CompFile.LocalFileStatus.Fine && _VersionSatisfies(dep, p.Version))
                        .ToList();
                    if (active.Count == 0) continue; // 本就未满足，忽略
                    if (active.All(p => removal.Contains(p.Host)))
                    {
                        removal.Add(c);
                        changed = true;
                    }
                }
            }
        } while (changed);

        return removal.Where(m => !targetSet.Contains(m)).ToList();
    }

    private void _AddProvider(string id, CompFile host, CompFile source, string version)
    {
        id = _Norm(id);
        if (!_providers.TryGetValue(id, out var list))
        {
            list = new List<Provider>();
            _providers[id] = list;
        }

        // 去重键含版本：多版本 wrapper 的每份副本版本都保留，供依赖版本区间校验逐一尝试
        if (!list.Any(p => p.Host == host && p.Source == source && p.Version == version))
            list.Add(new Provider { Host = host, Source = source, Version = version });
    }

    private void _RegisterSource(CompFile owner, string id, CompFile source)
    {
        if (!string.IsNullOrWhiteSpace(id)) _declaredSources[(owner, _Norm(id))] = source;
    }

    private CompFile _ResolveConflictSource(CompFile owner, string declarerId) =>
        !string.IsNullOrWhiteSpace(declarerId) &&
        _declaredSources.TryGetValue((owner, _Norm(declarerId)), out var source)
            ? source
            : owner;

    private static CompFile _CreateSiblingSource(CompFile owner, string id, string version)
    {
        var source = new CompFile(owner.path + "!/@" + id);
        source.SetJijMetadata(id, id, version);
        source.JijLoader = owner.JijLoader ?? owner.DetectedLoader;
        return source;
    }

    #region MC 版本匹配

    private static List<CompFile> _CollectLoadableNodes(List<CompFile> embedded, string mc,
        IReadOnlyDictionary<string, string> forgeSelections = null,
        IReadOnlyDictionary<string, string> fabricSelections = null)
    {
        var into = new List<CompFile>();
        _Collect(embedded, mc, into, forgeSelections, fabricSelections);
        return into;
    }

    private static void _Collect(List<CompFile> embedded, string mc, List<CompFile> into,
        IReadOnlyDictionary<string, string> forgeSelections,
        IReadOnlyDictionary<string, string> fabricSelections)
    {
        if (embedded is null) return;
        foreach (var e in embedded)
        {
            if (!_NodeLoads(e, mc)) continue;
            if (!_ForgeNodeSelected(e, forgeSelections)) continue;
            if (!_FabricNodeSelected(e, fabricSelections)) continue;
            into.Add(e);
            _Collect(e.EmbeddedMods, mc, into, forgeSelections, fabricSelections);
        }
    }

    private static bool _FabricNodeSelected(CompFile node, IReadOnlyDictionary<string, string> selections)
    {
        if (selections is null || string.IsNullOrWhiteSpace(node.ModId) ||
            !string.Equals(node.JijLoader, "Fabric", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(node.JijLoader, "Quilt", StringComparison.OrdinalIgnoreCase))
            return true;
        if (!selections.TryGetValue(node.ModId, out var selected) || selected is null) return false;
        return McConstraintMatcher.CompareFabricVersions(node.Version, selected) == 0;
    }

    private Dictionary<string, string> _SelectFabricVersions(
        IReadOnlyDictionary<CompFile, List<CompFile>> nodesByHost, string mc,
        IReadOnlyDictionary<string, string> forgeSelections)
    {
        var candidates = nodesByHost.Values.SelectMany(nodes => nodes).ToList();
        var baseRequirements = new List<DepRow>();
        foreach (var mod in _allMods.Where(m => m.State == CompFile.LocalFileStatus.Fine))
        foreach (var declaration in mod.DependencyDeclarations.Where(d => !d.Optional))
            baseRequirements.Add(new DepRow
                { DepId = declaration.Id, Raw = declaration.Raw, Loader = mod.DetectedLoader });

        var result = _ChooseFabricVersions(candidates, baseRequirements);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var maxPasses = candidates.Count + 1;
        for (var pass = 0; pass < maxPasses; pass++)
        {
            if (!seen.Add(_FabricSelectionKey(result)))
            {
                ModBase.Log("[Mod] Fabric 内嵌版本选择出现循环，保留当前候选并交由依赖检查报告冲突",
                    ModBase.LogLevel.Developer);
                return result;
            }

            var requirements = new List<DepRow>(baseRequirements);
            foreach (var host in nodesByHost.Keys)
            foreach (var node in _CollectLoadableNodes(host.EmbeddedMods, mc, forgeSelections, result))
            foreach (var declaration in node.DependencyDeclarations.Where(d => !d.Optional))
                requirements.Add(new DepRow
                    { DepId = declaration.Id, Raw = declaration.Raw, Loader = node.JijLoader });

            var next = _ChooseFabricVersions(candidates, requirements);
            if (_SameFabricSelections(result, next)) return next;
            result = next;
        }

        ModBase.Log("[Mod] Fabric 内嵌版本选择未在限定轮次内收敛，保留当前候选并交由依赖检查报告冲突",
            ModBase.LogLevel.Developer);
        return result;
    }

    private static Dictionary<string, string> _ChooseFabricVersions(
        List<CompFile> candidates, List<DepRow> requirements)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in candidates.Where(n => !string.IsNullOrWhiteSpace(n.ModId) &&
                                                    (string.Equals(n.JijLoader, "Fabric", StringComparison.OrdinalIgnoreCase) ||
                                                     string.Equals(n.JijLoader, "Quilt", StringComparison.OrdinalIgnoreCase)))
                     .GroupBy(n => n.ModId, StringComparer.OrdinalIgnoreCase))
        {
            var versions = group.Select(n => n.Version).Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            versions.Sort((a, b) => McConstraintMatcher.CompareFabricVersions(b, a));
            var needed = requirements.Where(r => string.Equals(r.DepId, group.Key,
                StringComparison.OrdinalIgnoreCase)).ToList();
            result[group.Key] = versions.FirstOrDefault(candidate => needed.All(r =>
                _VersionSatisfies(r, candidate)));
        }
        return result;
    }

    private static bool _SameFabricSelections(IReadOnlyDictionary<string, string> left,
        IReadOnlyDictionary<string, string> right) =>
        left.Count == right.Count && left.All(pair => right.TryGetValue(pair.Key, out var value) &&
            string.Equals(pair.Value, value, StringComparison.OrdinalIgnoreCase));

    private static string _FabricSelectionKey(IReadOnlyDictionary<string, string> selections) =>
        string.Join("\n", selections.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => pair.Key + "\0" + pair.Value));

    private static bool _ForgeNodeSelected(CompFile node, IReadOnlyDictionary<string, string> selections)
    {
        if (selections is null || string.IsNullOrWhiteSpace(node.JijIdentifier)) return true;
        if (!selections.TryGetValue(node.JijIdentifier, out var selected) || selected is null) return false;
        var version = node.JijArtifactVersion ?? node.Version;
        return !string.IsNullOrWhiteSpace(version) &&
               McConstraintMatcher.CompareMavenVersions(version, selected) == 0;
    }

    private static Dictionary<string, string> _SelectForgeJarJarVersions(IEnumerable<CompFile> nodes)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in nodes.Where(n => !string.IsNullOrWhiteSpace(n.JijIdentifier))
                     .GroupBy(n => n.JijIdentifier, StringComparer.OrdinalIgnoreCase))
        {
            var candidates = group.Select(n => n.JijArtifactVersion ?? n.Version)
                .Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            candidates.Sort((a, b) => McConstraintMatcher.CompareMavenVersions(b, a));
            result[group.Key] = candidates.FirstOrDefault(candidate => group.All(n =>
                string.IsNullOrWhiteSpace(n.JijVersionRange) ||
                _JarJarRangeContains(n.JijVersionRange, candidate)));
        }
        return result;
    }

    private static bool _JarJarRangeContains(string range, string candidate)
    {
        var value = range.Trim();
        if (value.Length > 0 && value[0] is not '[' and not '(')
            return McConstraintMatcher.CompareMavenVersions(candidate, value) == 0;
        return McConstraintMatcher.Satisfies(value, "Forge", candidate);
    }

    private static bool _NodeLoads(CompFile node, string mc)
    {
        if (string.IsNullOrEmpty(mc)) return true;
        var declaredMc = node.DependencyDeclarations
            .Where(d => string.Equals(d.Id, "minecraft", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var requiredMc = declaredMc.Where(d => !d.Optional).ToList();
        if (requiredMc.Count > 0)
        {
            var constrained = requiredMc.Where(d => !string.IsNullOrWhiteSpace(d.Raw)).ToList();
            if (constrained.Count == 0 ||
                constrained.All(d => McConstraintMatcher.Satisfies(d.Raw, node.JijLoader, mc))) return true;
            return McConstraintMatcher.ContainsVersionToken(node.FileName, mc) ||
                   McConstraintMatcher.ContainsVersionToken(node.Version, mc);
        }
        if (declaredMc.Count > 0) return true;
        var constraint = node.JijTargetMcVersion;
        if (string.IsNullOrWhiteSpace(constraint)) return true;
        if (McConstraintMatcher.Satisfies(constraint, node.JijLoader, mc)) return true;
        return McConstraintMatcher.ContainsVersionToken(node.FileName, mc) ||
               McConstraintMatcher.ContainsVersionToken(node.Version, mc);
    }

    #endregion
}
