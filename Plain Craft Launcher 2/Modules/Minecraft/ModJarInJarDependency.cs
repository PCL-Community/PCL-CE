using System;
using System.Collections.Generic;
using System.Linq;
using CompFile = PCL.ModLocalComp.LocalCompFile;

namespace PCL;

/// <summary>内嵌模组（Jar-in-Jar）依赖状态四态。</summary>
public enum JijDepStatus
{
    Installed, // 有启用的独立/其它内嵌提供者
    Disabled, // 有满足版本的提供者但都被禁用
    Bundled, // 无独立提供，但本 Mod 内嵌了它
    VersionMismatch, // 有提供者但版本都不满足约束（装了，但装错版本）
    Missing // 无任何提供者（根本没装）
}

/// <summary>
///     加载器/平台伪依赖 id 的统一判定，供依赖解析（<see cref="ModLocalComp" />.AddDependency）与
///     依赖四态/级联分析共用，避免两处 id 集漂移。
/// </summary>
public static class ModDependencyIds
{
    // 加载器/运行时平台伪 id：不作为真实 Mod 依赖收录。不含 minecraft（其版本要求另有用途）
    private static readonly HashSet<string> _loaderIds = new(StringComparer.OrdinalIgnoreCase)
        { "forge", "neoforge", "fabric", "fabricloader", "quilt", "quilt_loader", "java", "mcp" };

    /// <summary>是否加载器/平台伪 id（AddDependency 收录依赖时用；不含 minecraft）。</summary>
    public static bool IsLoaderId(string id) => _loaderIds.Contains(id);

    /// <summary>是否平台伪依赖（依赖四态/级联判定时用；含 minecraft，不参与"缺失"判定）。</summary>
    public static bool IsPlatform(string id) =>
        string.Equals(id, "minecraft", StringComparison.OrdinalIgnoreCase) || _loaderIds.Contains(id);
}

/// <summary>
///     内嵌模组依赖分析。构建时按当前实例 MC 版本过滤出"真正会加载"的内嵌副本，
///     供依赖四态判定与禁用/删除的级联反查复用（模组管理页与内嵌模组二级页共用）。
/// </summary>
public class ModJarInJarIndex
{
    /// <summary>一条依赖要求（可能来自宿主自身或其某个内嵌 mod），带来源加载器方言与可选标记。</summary>
    public sealed class DepRow
    {
        public string DepId;
        public string Raw; // 原始版本约束，null=无版本要求
        public bool Optional;
        public string Loader; // 声明方的加载器（决定版本方言），null=未知
    }

    private readonly List<CompFile> _allMods;
    private readonly Dictionary<string, List<(CompFile Mod, string Version)>> _providers =
        new(StringComparer.OrdinalIgnoreCase);
    // 每个 Mod 内嵌提供的 (id, 版本) 列表（同一 id 的多版本 wrapper 保留全部副本版本）
    private readonly Dictionary<CompFile, List<(string Id, string Version)>> _selfBundled = new();
    // 用于缺失警告与级联反查（宿主是启用/禁用单位，故内嵌依赖归到宿主承担）
    private readonly Dictionary<CompFile, List<DepRow>> _deps = new();
    // 每个宿主的可加载内嵌节点（关系页把有依赖的内嵌 mod 单独成卡时遍历）
    private readonly Dictionary<CompFile, List<CompFile>> _loadableNodes = new();

    public ModJarInJarIndex(IEnumerable<CompFile> allMods, string mc)
    {
        _allMods = allMods.Where(m => !m.IsFolder).ToList();
        foreach (var m in _allMods)
        {
            var nodes = _CollectLoadableNodes(m.EmbeddedMods, mc);
            _loadableNodes[m] = nodes;
            _selfBundled[m] = nodes.Where(n => !string.IsNullOrEmpty(n.ModId))
                .Select(n => (n.ModId, n.Version)).ToList();

            if (!string.IsNullOrEmpty(m.ModId)) _AddProvider(m.ModId, m, m.Version);
            foreach (var pid in m.ProvidedIds)
                _AddProvider(pid, m, m.Version);
            foreach (var n in nodes.Where(n => !string.IsNullOrEmpty(n.ModId)))
                _AddProvider(n.ModId, m, n.Version);

            var rows = new List<DepRow>();
            foreach (var kv in m.DependencyRaw)
                rows.Add(new DepRow
                {
                    DepId = kv.Key, Raw = kv.Value,
                    Optional = m.OptionalDependencies.Contains(kv.Key), Loader = m.DetectedLoader
                });
            foreach (var n in nodes)
            foreach (var kv in n.DependencyRaw)
                rows.Add(new DepRow
                {
                    DepId = kv.Key, Raw = kv.Value,
                    Optional = n.OptionalDependencies.Contains(kv.Key), Loader = n.JijLoader
                });
            _deps[m] = rows
                .GroupBy(r => r.DepId, StringComparer.OrdinalIgnoreCase)
                .Select(g => new DepRow
                {
                    DepId = g.Key,
                    Raw = g.Select(r => r.Raw).FirstOrDefault(v => v is not null),
                    Optional = g.All(r => r.Optional),
                    Loader = g.Select(r => r.Loader).FirstOrDefault(l => l is not null)
                }).ToList();
        }
    }

    /// <summary>某 Mod 的有效依赖（含其内嵌 mod 上浮的依赖）。用于缺失警告与级联。</summary>
    public IReadOnlyList<DepRow> GetDependencies(CompFile mod) =>
        _deps.TryGetValue(mod, out var list) ? list : new List<DepRow>();

    /// <summary>宿主的可加载内嵌节点（关系页遍历，把有依赖的内嵌 mod 单独成卡）。</summary>
    public IReadOnlyList<CompFile> GetLoadableEmbedded(CompFile host) =>
        _loadableNodes.TryGetValue(host, out var list) ? list : new List<CompFile>();

    /// <summary>构造某 mod 自身声明的依赖行（不含内嵌上浮），供关系页按 mod 分卡展示。</summary>
    public static List<DepRow> BuildOwnDependencies(CompFile mod, string loader) =>
        mod.DependencyRaw.Select(kv => new DepRow
        {
            DepId = kv.Key, Raw = kv.Value,
            Optional = mod.OptionalDependencies.Contains(kv.Key), Loader = loader
        }).ToList();

    private static string _Norm(string id) => id?.Replace('-', '_');

    private static bool _VersionSatisfies(DepRow dep, string providerVersion)
    {
        if (dep.Raw is null) return true;
        // provider 版本未知或不可比较（占位符未解析、纯库无版本、"MC1.21-xx" 等字母开头）：
        // 无法可靠判断时视为满足——错标"缺失"比漏一次版本警告更糟
        if (string.IsNullOrWhiteSpace(providerVersion)) return true;
        var ver = McConstraintMatcher.StripV(providerVersion.Trim());
        if (ver.Length == 0 || !char.IsDigit(ver[0])) return true;
        if (McConstraintMatcher.Satisfies(dep.Raw, dep.Loader, ver)) return true;
        return !McConstraintMatcher.HasComparableLowerBound(dep.Raw);
    }

    // 本 Mod 自己内嵌的副本是否满足该依赖的版本要求（内嵌了但版本不够时不算满足）
    private bool _SelfBundleSatisfies(CompFile mod, DepRow dep)
    {
        return _selfBundled.TryGetValue(mod, out var self) &&
               self.Any(x => string.Equals(_Norm(x.Id), _Norm(dep.DepId), StringComparison.OrdinalIgnoreCase) &&
                             _VersionSatisfies(dep, x.Version));
    }

    public static bool IsPlatform(string id) => ModDependencyIds.IsPlatform(id);

    /// <summary>某 Mod 的某条有效依赖当前处于四态中的哪一态。</summary>
    public JijDepStatus Analyze(CompFile mod, DepRow dep)
    {
        if (_SelfBundleSatisfies(mod, dep)) return JijDepStatus.Bundled;
        _providers.TryGetValue(_Norm(dep.DepId), out var provs);
        var others = provs?.Where(p => p.Mod != mod).ToList() ?? new List<(CompFile Mod, string Version)>();
        var satisfying = others.Where(p => _VersionSatisfies(dep, p.Version)).ToList();
        if (satisfying.Any(p => p.Mod.State == CompFile.LocalFileStatus.Fine)) return JijDepStatus.Installed;
        if (satisfying.Count > 0) return JijDepStatus.Disabled;
        // 有提供者却无一满足版本：装了但版本不对，区别于根本没装
        if (others.Count > 0) return JijDepStatus.VersionMismatch;
        return JijDepStatus.Missing;
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
                        .Where(p => p.Mod.State == CompFile.LocalFileStatus.Fine && _VersionSatisfies(dep, p.Version))
                        .ToList();
                    if (active.Count == 0) continue; // 本就未满足，忽略
                    if (active.All(p => removal.Contains(p.Mod)))
                    {
                        removal.Add(c);
                        changed = true;
                    }
                }
            }
        } while (changed);

        return removal.Where(m => !targetSet.Contains(m)).ToList();
    }

    private void _AddProvider(string id, CompFile top, string version)
    {
        id = _Norm(id); // 归一化连字符/下划线，使依赖 yumi-commons-core 能命中 id=yumi_commons_core
        if (!_providers.TryGetValue(id, out var list))
        {
            list = new List<(CompFile, string)>();
            _providers[id] = list;
        }

        // 去重键含版本：多版本 wrapper 的每份副本版本都保留，供依赖版本区间校验逐一尝试
        if (!list.Any(p => p.Mod == top && p.Version == version)) list.Add((top, version));
    }

    #region MC 版本匹配

    // 递归收集"会加载"的内嵌节点：某副本 MC 约束不匹配当前实例则整支剪掉
    private static List<CompFile> _CollectLoadableNodes(List<CompFile> embedded, string mc)
    {
        var into = new List<CompFile>();
        _Collect(embedded, mc, into);
        return into;
    }

    private static void _Collect(List<CompFile> embedded, string mc, List<CompFile> into)
    {
        if (embedded is null) return;
        foreach (var e in embedded)
        {
            if (!_NodeLoads(e, mc)) continue;
            into.Add(e);
            _Collect(e.EmbeddedMods, mc, into);
        }
    }

    private static bool _NodeLoads(CompFile node, string mc)
    {
        if (string.IsNullOrEmpty(mc)) return true; // 拿不到实例版本则不过滤
        var constraint = node.JijTargetMcVersion;
        if (string.IsNullOrWhiteSpace(constraint)) return true; // 无 MC 约束：任意版本均加载
        if (McConstraintMatcher.Satisfies(constraint, node.JijLoader, mc)) return true;
        return McConstraintMatcher.ContainsVersionToken(node.FileName, mc) ||
               McConstraintMatcher.ContainsVersionToken(node.Version, mc);
    }

    #endregion
}
