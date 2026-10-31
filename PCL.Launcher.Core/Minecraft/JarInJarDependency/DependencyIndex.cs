using CompFile = PCL.LocalComp.LocalCompFile;

namespace PCL;

/// <summary>按实例版本索引可加载的内嵌模组、依赖、提供者与冲突。</summary>
public partial class JarInJarIndex
{
    private readonly List<CompFile> _allMods;
    private readonly Dictionary<(CompFile Owner, string Id), CompFile> _declaredSources = new();
    private readonly Dictionary<CompFile, List<DepRow>> _deps = new();
    private readonly Dictionary<CompFile, List<CompFile>> _loadableNodes = new();
    private readonly Dictionary<CompFile, CompFile> _nodeHosts = new();

    private readonly Dictionary<string, List<Provider>> _providers =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<CompFile, List<(string Id, string? Version)>> _selfBundled = new();

    public JarInJarIndex(IEnumerable<CompFile> allMods, string mc)
    {
        _allMods =
        [
            .. allMods.Where(mod =>
                !mod.IsFolder && mod.State != CompFile.LocalFileStatus.Unavailable)
        ];

        _BuildIndex(mc);
    }

    private void _BuildIndex(string mc)
    {
        var activeMods = _allMods
            .Where(mod => mod.State == CompFile.LocalFileStatus.Fine)
            .ToList();

        var preliminaryNodes = activeMods.ToDictionary(
            mod => mod,
            mod => _CollectLoadableNodes(mod.EmbeddedMods, mc));

        var forgeSelections = _SelectForgeJarJarVersions(
            preliminaryNodes.Values.SelectMany(nodes => nodes));

        var nodesAfterForge = activeMods.ToDictionary(
            mod => mod,
            mod => _CollectLoadableNodes(mod.EmbeddedMods, mc, forgeSelections));

        var fabricSelections = _SelectFabricVersions(nodesAfterForge, mc, forgeSelections);

        foreach (var mod in _allMods)
        {
            var nodes = mod.State == CompFile.LocalFileStatus.Fine
                ? _CollectLoadableNodes(mod.EmbeddedMods, mc, forgeSelections, fabricSelections)
                : _CollectLoadableNodes(mod.EmbeddedMods, mc);

            _loadableNodes[mod] = nodes;

            foreach (var node in nodes)
                _nodeHosts[node] = mod;

            _IndexMod(mod, nodes);
        }
    }

    private void _IndexMod(CompFile mod, List<CompFile> nodes)
    {
        var bundled = new List<(string Id, string? Version)>();
        _AddSelfBundledEntries(mod, bundled);

        foreach (var node in nodes)
            _AddSelfBundledEntries(node, bundled);

        _selfBundled[mod] = bundled;

        _RegisterProviders(mod, nodes);
        _deps[mod] = _BuildDependencies(mod, nodes);
    }

    private static void _AddSelfBundledEntries(
        CompFile source,
        List<(string Id, string? Version)> bundled)
    {
        if (!string.IsNullOrWhiteSpace(source.ModId))
            bundled.Add((source.ModId, source.Version));

        foreach (var id in source.ProvidedIds.Where(id => !string.IsNullOrWhiteSpace(id)))
        {
            var version = source.ProvidedVersions.TryGetValue(id, out var providedVersion)
                ? providedVersion
                : source.Version;

            bundled.Add((id, version));
        }
    }

    private void _RegisterProviders(CompFile mod, List<CompFile> nodes)
    {
        if (!string.IsNullOrEmpty(mod.ModId))
        {
            _RegisterSource(mod, mod.ModId, mod);
            _AddProvider(mod.ModId, mod, mod, mod.Version);
        }

        foreach (var id in mod.ProvidedIds)
        {
            var hasOwnVersion = mod.ProvidedVersions.TryGetValue(id, out var providedVersion);
            var source = hasOwnVersion
                ? _CreateSiblingSource(mod, id, providedVersion)
                : mod;

            _RegisterSource(mod, id, source);
            _AddProvider(id, mod, source, hasOwnVersion ? providedVersion : mod.Version);
        }

        foreach (var node in nodes.Where(node => !string.IsNullOrEmpty(node.ModId)))
        {
            _RegisterSource(node, node.ModId, node);
            _AddProvider(node.ModId, mod, node, node.Version);
        }

        foreach (var node in nodes)
        foreach (var id in node.ProvidedIds)
        {
            var hasOwnVersion = node.ProvidedVersions.TryGetValue(id, out var providedVersion);
            var source = hasOwnVersion
                ? _CreateSiblingSource(node, id, providedVersion)
                : node;

            _RegisterSource(node, id, source);
            _AddProvider(id, mod, source, hasOwnVersion ? providedVersion : node.Version);
        }
    }

    private static List<DepRow> _BuildDependencies(CompFile mod, List<CompFile> nodes)
    {
        var rows = mod
            .DependencyDeclarations
            .Select(declaration => new DepRow
            {
                DepId = declaration.Id,
                Raw = declaration.Raw,
                Optional = declaration.Optional,
                Loader = mod.DetectedLoader
            })
            .ToList();

        rows.AddRange(
            from node in nodes
            from declaration in node.DependencyDeclarations
            select new DepRow
            {
                DepId = declaration.Id,
                Raw = declaration.Raw,
                Optional = declaration.Optional,
                Loader = node.JijLoader
            });

        return
        [
            .. rows
                .GroupBy(row => (row.DepId, row.Raw, row.Loader, row.Optional))
                .Select(group => group.First())
        ];
    }

    /// <summary>某 Mod 的有效依赖（含其内嵌 mod 上浮的依赖）。用于缺失警告与级联。</summary>
    public IReadOnlyList<DepRow> GetDependencies(CompFile mod)
    {
        return _deps.TryGetValue(mod, out var list)
            ? list
            : [];
    }

    /// <summary>宿主的可加载内嵌节点（关系页遍历，把有依赖的内嵌 mod 单独成卡）。</summary>
    public IReadOnlyList<CompFile> GetLoadableEmbedded(CompFile host)
    {
        return _loadableNodes.TryGetValue(host, out var list)
            ? list
            : [];
    }

    public IReadOnlyList<string> GetProviderVersions(CompFile dependent, string depId)
    {
        if (!_providers.TryGetValue(_Norm(depId), out var providers))
            return [];

        return
        [
            .. providers
                .Where(provider => provider.Source != dependent)
                .Select(provider => provider.Version ?? "")
                .Where(version => !string.IsNullOrWhiteSpace(version))
                .Distinct(StringComparer.OrdinalIgnoreCase)
        ];
    }

    /// <summary>构造某 mod 自身声明的依赖行（不含内嵌上浮），供关系页按 mod 分卡展示。</summary>
    public static List<DepRow> BuildOwnDependencies(CompFile mod, string loader)
    {
        return
        [
            .. mod.DependencyDeclarations
                .Select(declaration => new DepRow
                {
                    DepId = declaration.Id,
                    Raw = declaration.Raw,
                    Optional = declaration.Optional,
                    Loader = loader
                })
        ];
    }

    private static string _Norm(string? id)
    {
        return id?.ToLowerInvariant() ?? "";
    }

    private static bool _VersionSatisfies(DepRow dep, string? providerVersion)
    {
        if (dep.Raw is null) return true;

        if (string.IsNullOrWhiteSpace(providerVersion))
            return true;

        var ver = McConstraintMatcher.StripV(providerVersion.Trim());
        if (ver.Length == 0 || ver.Contains('$'))
            return true;

        if (McConstraintMatcher.Satisfies(dep.Raw, dep.Loader ?? "", ver))
            return true;

        if (!string.IsNullOrWhiteSpace(dep.Loader))
            return false;

        return !McConstraintMatcher.HasComparableBound(dep.Raw);
    }

    private bool _SelfBundleSatisfies(CompFile mod, DepRow dep)
    {
        return _selfBundled.TryGetValue(mod, out var self)
            && self.Any(entry =>
                string.Equals(
                    _Norm(entry.Id),
                    _Norm(dep.DepId),
                    StringComparison.OrdinalIgnoreCase)
                && _VersionSatisfies(dep, entry.Version));
    }

    /// <summary>返回某条依赖在当前实例中的状态。</summary>
    public JijDepStatus Analyze(CompFile mod, DepRow dep)
    {
        if (_SelfBundleSatisfies(mod, dep)
            || (_nodeHosts.TryGetValue(mod, out var host) && _SelfBundleSatisfies(host, dep)))
            return JijDepStatus.Bundled;

        _providers.TryGetValue(_Norm(dep.DepId), out var providers);
        var otherProviders = providers
                ?
                .Where(provider => provider.Source != mod)
                .ToList()
            ?? [];

        var satisfyingProviders = otherProviders
            .Where(provider => _VersionSatisfies(dep, provider.Version))
            .ToList();

        if (satisfyingProviders.Any(provider =>
                provider.Host.State == CompFile.LocalFileStatus.Fine))
            return JijDepStatus.Installed;

        if (satisfyingProviders.Count > 0)
            return JijDepStatus.Disabled;

        if (otherProviders.Count > 0)
            return JijDepStatus.VersionMismatch;

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
            foreach (var node in _loadableNodes[host]
                         .Where(node => !order.ContainsKey(node)))
                order[node] = order.Count;
        }

        foreach (var source in _providers
                     .Values
                     .SelectMany(providers => providers)
                     .Select(provider => provider.Source)
                     .Distinct())
            if (!order.ContainsKey(source))
                order[source] = order.Count;

        var pairs = new Dictionary<(CompFile, CompFile), bool>();
        foreach (var m in _allMods
                     .Where(m => m.State == CompFile.LocalFileStatus.Fine))
        {
            _CollectConflicts(m, m.ConflictDeclarations, m.DetectedLoader, order, pairs);
            foreach (var n in _loadableNodes[m])
                _CollectConflicts(n, n.ConflictDeclarations, n.JijLoader, order, pairs);
        }

        return [.. pairs.Select(kv => (kv.Key.Item1, kv.Key.Item2, kv.Value))];
    }

    private void _CollectConflicts(
        CompFile declarerSource,
        IEnumerable<EmbeddedConflict> conflicts,
        string? loader,
        Dictionary<CompFile, int> order,
        Dictionary<(CompFile, CompFile), bool> pairs)
    {
        foreach (var conflict in conflicts)
        {
            var actualDeclarer = _ResolveConflictSource(declarerSource, conflict.DeclarerId);

            if (!_providers.TryGetValue(_Norm(conflict.Target), out var providers))
                continue;

            var range = conflict.Raw;
            var hard = conflict.Hard;

            foreach (var provider in providers.Where(provider =>
                         provider.Source != actualDeclarer
                         && provider.Host.State == CompFile.LocalFileStatus.Fine))
            {
                if (range is not null)
                {
                    var version = McConstraintMatcher.StripV(provider.Version?.Trim() ?? "");
                    if (version.Length == 0 || version.Contains('$'))
                        continue;

                    if (!McConstraintMatcher.Satisfies(range, loader ?? "", version))
                        continue;
                }

                var pair = order[actualDeclarer] < order[provider.Source]
                    ? (actualDeclarer, provider.Source)
                    : (provider.Source, actualDeclarer);

                pairs[pair] = pairs.TryGetValue(pair, out var existingHard)
                    ? existingHard || hard
                    : hard;
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

            foreach (var mod in _allMods)
            {
                if (mod.State != CompFile.LocalFileStatus.Fine || removal.Contains(mod))
                    continue;

                foreach (var dependency in GetDependencies(mod))
                {
                    if (DependencyIds.IsPlatform(dependency.DepId)
                        || dependency.Optional
                        || _SelfBundleSatisfies(mod, dependency))
                        continue;

                    if (!_providers.TryGetValue(_Norm(dependency.DepId), out var providers))
                        continue;

                    var activeProviders = providers
                        .Where(provider =>
                            provider.Host.State == CompFile.LocalFileStatus.Fine
                            && _VersionSatisfies(dependency, provider.Version))
                        .ToList();

                    if (activeProviders.Count == 0)
                        continue;

                    if (!activeProviders.All(provider => removal.Contains(provider.Host)))
                        continue;

                    removal.Add(mod);
                    changed = true;
                }
            }
        }
        while (changed);

        return [.. removal.Where(mod => !targetSet.Contains(mod))];
    }

    private void _AddProvider(
        string id,
        CompFile host,
        CompFile source,
        string? version)
    {
        id = _Norm(id);
        if (!_providers.TryGetValue(id, out var list))
        {
            list = [];
            _providers[id] = list;
        }

        if (!list.Any(p =>
                p.Host == host && p.Source == source && p.Version == version))
            list.Add(
                new Provider
                {
                    Host = host,
                    Source = source,
                    Version = version
                });
    }

    private void _RegisterSource(CompFile owner, string id, CompFile source)
    {
        if (!string.IsNullOrWhiteSpace(id))
            _declaredSources[(owner, _Norm(id))] = source;
    }

    private CompFile _ResolveConflictSource(CompFile owner, string? declarerId)
    {
        return !string.IsNullOrWhiteSpace(declarerId)
            && _declaredSources.TryGetValue((owner, _Norm(declarerId)), out var source)
                ? source
                : owner;
    }

    private static CompFile _CreateSiblingSource(CompFile owner, string id, string? version)
    {
        var source = new CompFile(owner.path + "!/@" + id);
        source.SetJijMetadata(id, id, version);
        source.JijLoader = owner.JijLoader ?? owner.DetectedLoader;
        return source;
    }

    public sealed class DepRow
    {
        public string? DepId;
        public string? Loader;
        public bool Optional;
        public string? Raw;
    }

    private sealed class Provider
    {
        public required CompFile Host;
        public required CompFile Source;
        public string? Version;
    }
}