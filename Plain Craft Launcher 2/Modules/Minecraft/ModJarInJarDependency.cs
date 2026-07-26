using System;
using System.Collections.Generic;
using System.Linq;
using CompFile = PCL.ModLocalComp.LocalCompFile;

namespace PCL;

/// <summary>内嵌模组（Jar-in-Jar）依赖状态四态。</summary>
public enum JijDepStatus
{
    Installed, // 有启用的独立/其它内嵌提供者
    Disabled, // 有提供者但都被禁用
    Bundled, // 无独立提供，但本 Mod 内嵌了它
    Missing // 无任何提供者
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
    private readonly List<CompFile> _allMods;
    private readonly Dictionary<string, List<(CompFile Mod, string Version)>> _providers =
        new(StringComparer.OrdinalIgnoreCase);
    // 每个 Mod 内嵌提供的 (id, 版本) 列表（同一 id 的多版本 wrapper 保留全部副本版本）
    private readonly Dictionary<CompFile, List<(string Id, string Version)>> _selfBundled = new();

    public ModJarInJarIndex(IEnumerable<CompFile> allMods, string mc)
    {
        _allMods = allMods.Where(m => !m.IsFolder).ToList();
        foreach (var m in _allMods)
        {
            var loadable = _CollectLoadable(m.EmbeddedMods, mc);
            _selfBundled[m] = loadable;

            if (!string.IsNullOrEmpty(m.ModId)) _AddProvider(m.ModId, m, m.Version);
            foreach (var (id, ver) in loadable) _AddProvider(id, m, ver);
        }
    }

    private static bool _VersionSatisfies(CompFile dependent, string depId, string providerVersion)
    {
        var req = dependent.DependencyRaw.TryGetValue(depId, out var r) ? r : null;
        if (req is null) return true;
        // provider 版本未知或不可比较（占位符未解析、纯库无版本、"MC1.21-xx" 等字母开头）：
        // 无法可靠判断时视为满足——错标"缺失"比漏一次版本警告更糟
        if (string.IsNullOrWhiteSpace(providerVersion)) return true;
        var ver = providerVersion.Split('+')[0].Trim(); // 剥 semver 构建元数据（1.0.82+mc1.21.1）
        if (ver.Length == 0 || !char.IsDigit(ver[0])) return true;
        return McConstraintMatcher.Satisfies(req, dependent.DetectedLoader, ver);
    }

    // 本 Mod 自己内嵌的副本是否满足其对该依赖的版本要求（内嵌了但版本不够时不算满足）
    private bool _SelfBundleSatisfies(CompFile mod, string depId)
    {
        return _selfBundled.TryGetValue(mod, out var self) &&
               self.Any(x => string.Equals(x.Id, depId, StringComparison.OrdinalIgnoreCase) &&
                             _VersionSatisfies(mod, depId, x.Version));
    }

    public static bool IsPlatform(string id) => ModDependencyIds.IsPlatform(id);

    /// <summary>某 Mod 的某条依赖当前处于四态中的哪一态。</summary>
    public JijDepStatus Analyze(CompFile mod, string depId)
    {
        if (_SelfBundleSatisfies(mod, depId)) return JijDepStatus.Bundled;
        _providers.TryGetValue(depId, out var provs);
        var satisfying = provs?.Where(p => p.Mod != mod && _VersionSatisfies(mod, depId, p.Version)).ToList()
                         ?? new List<(CompFile Mod, string Version)>();
        if (satisfying.Any(p => p.Mod.State == CompFile.LocalFileStatus.Fine)) return JijDepStatus.Installed;
        if (satisfying.Count > 0) return JijDepStatus.Disabled;
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
                foreach (var dep in c.Dependencies.Keys)
                {
                    if (ModDependencyIds.IsPlatform(dep)) continue;
                    if (c.OptionalDependencies.Contains(dep)) continue; // 可选依赖不参与级联
                    if (_SelfBundleSatisfies(c, dep)) continue;
                    if (!_providers.TryGetValue(dep, out var provs)) continue;
                    var active = provs
                        .Where(p => p.Mod.State == CompFile.LocalFileStatus.Fine && _VersionSatisfies(c, dep, p.Version))
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
        if (!_providers.TryGetValue(id, out var list))
        {
            list = new List<(CompFile, string)>();
            _providers[id] = list;
        }

        // 去重键含版本：多版本 wrapper 的每份副本版本都保留，供依赖版本区间校验逐一尝试
        if (!list.Any(p => p.Mod == top && p.Version == version)) list.Add((top, version));
    }

    #region MC 版本匹配

    // 递归收集"会加载"的内嵌 (ModId, 版本)：某副本 MC 约束不匹配当前实例则整支剪掉
    private static List<(string Id, string Version)> _CollectLoadable(List<CompFile> embedded, string mc)
    {
        var into = new List<(string, string)>();
        _Collect(embedded, mc, into);
        return into;
    }

    private static void _Collect(List<CompFile> embedded, string mc, List<(string, string)> into)
    {
        if (embedded is null) return;
        foreach (var e in embedded)
        {
            if (!_NodeLoads(e, mc)) continue;
            if (!string.IsNullOrEmpty(e.ModId)) into.Add((e.ModId, e.Version));
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
