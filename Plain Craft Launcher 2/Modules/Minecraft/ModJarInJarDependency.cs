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
///     内嵌模组依赖分析。构建时按当前实例 MC 版本过滤出"真正会加载"的内嵌副本，
///     供依赖四态判定与禁用/删除的级联反查复用（模组管理页与内嵌模组二级页共用）。
/// </summary>
public class ModJarInJarIndex
{
    // 加载器/平台伪依赖，不参与"缺失"判定
    private static readonly HashSet<string> _platformIds = new(StringComparer.OrdinalIgnoreCase)
        { "minecraft", "forge", "neoforge", "fabric", "fabricloader", "quilt", "quilt_loader", "java", "mcp" };

    private readonly List<CompFile> _allMods;
    private readonly Dictionary<string, List<CompFile>> _providers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<CompFile, HashSet<string>> _selfBundled = new();

    public ModJarInJarIndex(IEnumerable<CompFile> allMods, string mc)
    {
        _allMods = allMods.Where(m => !m.IsFolder).ToList();
        foreach (var m in _allMods)
        {
            // 仅收录当前实例 MC 版本真正会加载的内嵌副本（多版本 wrapper 只留匹配的那份）
            var bundled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _CollectLoadableIds(m.EmbeddedMods, mc, bundled);
            _selfBundled[m] = bundled;

            if (!string.IsNullOrEmpty(m.ModId)) _AddProvider(m.ModId, m);
            foreach (var id in bundled) _AddProvider(id, m);
        }
    }

    public static bool IsPlatform(string id) => _platformIds.Contains(id);

    /// <summary>某 Mod 的某条依赖当前处于四态中的哪一态。</summary>
    public JijDepStatus Analyze(CompFile mod, string depId)
    {
        _providers.TryGetValue(depId, out var provs);
        var external = provs?.Where(p => p != mod).ToList() ?? new List<CompFile>();
        if (external.Any(p => p.State == CompFile.LocalFileStatus.Fine)) return JijDepStatus.Installed;
        if (external.Count > 0) return JijDepStatus.Disabled;
        if (_selfBundled.TryGetValue(mod, out var ids) && ids.Contains(depId)) return JijDepStatus.Bundled;
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
                    if (_platformIds.Contains(dep)) continue;
                    if (_selfBundled.TryGetValue(c, out var ids) && ids.Contains(dep)) continue;
                    if (!_providers.TryGetValue(dep, out var provs)) continue;
                    var active = provs.Where(p => p.State == CompFile.LocalFileStatus.Fine).ToList();
                    if (active.Count == 0) continue; // 本就未满足，忽略
                    if (active.All(removal.Contains))
                    {
                        removal.Add(c);
                        changed = true;
                    }
                }
            }
        } while (changed);

        return removal.Where(m => !targetSet.Contains(m)).ToList();
    }

    private void _AddProvider(string id, CompFile top)
    {
        if (!_providers.TryGetValue(id, out var list))
        {
            list = new List<CompFile>();
            _providers[id] = list;
        }

        if (!list.Contains(top)) list.Add(top);
    }

    #region MC 版本匹配

    // 递归收集"会加载"的内嵌 ModId：某副本 MC 约束不匹配当前实例则整支剪掉
    private static void _CollectLoadableIds(List<CompFile> embedded, string mc, HashSet<string> into)
    {
        if (embedded is null) return;
        foreach (var e in embedded)
        {
            if (!_NodeLoads(e, mc)) continue;
            if (!string.IsNullOrEmpty(e.ModId)) into.Add(e.ModId);
            _CollectLoadableIds(e.EmbeddedMods, mc, into);
        }
    }

    // 该内嵌副本是否会在当前实例 MC 版本下加载
    private static bool _NodeLoads(CompFile node, string mc)
    {
        if (string.IsNullOrEmpty(mc)) return true; // 拿不到实例版本则不过滤
        var constraint = node.JijTargetMcVersion;
        if (string.IsNullOrWhiteSpace(constraint)) return true; // 无 MC 约束：任意版本均加载
        if (_McSatisfiesRange(constraint, mc)) return true;
        // 兜底：文件名/版本号里恰好整词出现该实例版本，视作精确命中
        return _ContainsVersionToken(node.FileName, mc) || _ContainsVersionToken(node.Version, mc);
    }

    // Maven 风格区间：[a,b] [a,b) (a,b) [a,) (,b] [a]（精确）或裸 a（软下限 >=a），逗号分隔的多区间取或
    private static bool _McSatisfiesRange(string constraint, string mc)
    {
        foreach (var interval in _SplitTopLevel(constraint))
        {
            var s = interval.Trim();
            if (s.Length == 0) continue;

            if (s[0] != '[' && s[0] != '(')
            {
                if (McVersionComparer.CompareVersion(mc, s) >= 0) return true; // 裸版本 = 软下限
                continue;
            }

            var incLo = s[0] == '[';
            var incHi = s[^1] == ']';
            var body = s.Substring(1, s.Length - 2);
            var comma = body.IndexOf(',');
            if (comma < 0)
            {
                var only = body.Trim(); // [a] 精确
                if (only.Length > 0 && McVersionComparer.CompareVersion(mc, only) == 0) return true;
                continue;
            }

            var loStr = body.Substring(0, comma).Trim();
            var hiStr = body.Substring(comma + 1).Trim();
            var ok = true;
            if (loStr.Length > 0)
            {
                var c = McVersionComparer.CompareVersion(mc, loStr);
                ok = incLo ? c >= 0 : c > 0;
            }

            if (ok && hiStr.Length > 0)
            {
                var c = McVersionComparer.CompareVersion(mc, hiStr);
                ok = incHi ? c <= 0 : c < 0;
            }

            if (ok) return true;
        }

        return false;
    }

    // 按括号深度为 0 的逗号切分（区间内部的逗号不切）
    private static List<string> _SplitTopLevel(string s)
    {
        var outList = new List<string>();
        int depth = 0, start = 0;
        for (var i = 0; i < s.Length; i++)
        {
            var ch = s[i];
            if (ch == '[' || ch == '(') depth++;
            else if (ch == ']' || ch == ')') depth--;
            else if (ch == ',' && depth == 0)
            {
                outList.Add(s.Substring(start, i - start));
                start = i + 1;
            }
        }

        outList.Add(s.Substring(start));
        return outList;
    }

    // version 是否作为完整版本词出现在 haystack 中（"1.21" 不命中 "1.21.2" 或 "9.1.20"）
    private static bool _ContainsVersionToken(string haystack, string version)
    {
        if (string.IsNullOrEmpty(haystack) || string.IsNullOrEmpty(version)) return false;
        var i = haystack.IndexOf(version, StringComparison.Ordinal);
        while (i >= 0)
        {
            var before = i > 0 ? haystack[i - 1] : ' ';
            var end = i + version.Length;
            var after = end < haystack.Length ? haystack[end] : ' ';
            var leadingOk = before != '.' && !char.IsDigit(before);
            bool trailingOk;
            if (char.IsDigit(after))
                trailingOk = false;
            else if (after == '.')
            {
                var next = end + 1 < haystack.Length ? haystack[end + 1] : ' ';
                trailingOk = !char.IsDigit(next);
            }
            else
            {
                trailingOk = true;
            }

            if (leadingOk && trailingOk) return true;
            i = haystack.IndexOf(version, i + 1, StringComparison.Ordinal);
        }

        return false;
    }

    #endregion
}
