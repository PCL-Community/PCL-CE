using System;
using System.Collections.Generic;

namespace PCL;

/// <summary>按 Forge/NeoForge Maven 区间或 Fabric/Quilt SemVer 谓词求值版本约束。</summary>
public static class McConstraintMatcher
{
    private static bool IsForgeLike(string loaderType) =>
        string.Equals(loaderType, "Forge", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(loaderType, "NeoForge", StringComparison.OrdinalIgnoreCase);

    /// <summary>某内嵌副本相对当前实例 MC 版本的匹配类型（用于多版本 wrapper 折叠时标记当前副本）。
    ///     数值顺序即代表副本选择的优先级。</summary>
    public enum MatchKind
    {
        None, // 无从判断（拿不到实例版本）
        Incompatible, // 有实例版本，此副本声明的范围不含它
        NoConstraint, // 副本无 MC 约束：任意版本均会加载，但无从"精确匹配"（优先于不兼容、低于范围命中）
        Range, // 声明的版本范围覆盖当前实例
        Exact // 文件名/版本号里精确出现当前实例版本
    }

    /// <summary>
    ///     判断内嵌副本在 <paramref name="instanceMc" /> 下的匹配类型：精确命中(文件名/版本含该版本词) &gt;
    ///     范围覆盖(约束满足) &gt; 无约束(恒加载) &gt; 不兼容。用于多版本 wrapper 选出当前实例会加载的那份并标记。
    /// </summary>
    public static MatchKind Match(string fileName, string version, string constraint, string loaderType,
        string instanceMc)
    {
        if (string.IsNullOrWhiteSpace(instanceMc)) return MatchKind.None;
        if (ContainsVersionToken(fileName, instanceMc) || ContainsVersionToken(version, instanceMc))
            return MatchKind.Exact;
        if (string.IsNullOrWhiteSpace(constraint)) return MatchKind.NoConstraint;
        return Satisfies(constraint, loaderType, instanceMc) ? MatchKind.Range : MatchKind.Incompatible;
    }

    /// <summary>
    ///     <paramref name="version" /> 是否满足 <paramref name="constraint" />。约束为空视为无限制（满足）。
    ///     <paramref name="loaderType" /> 决定方言；为 null/未知时先按 semver 再按 Maven 兜底。
    /// </summary>
    public static bool Satisfies(string constraint, string loaderType, string version)
    {
        if (string.IsNullOrWhiteSpace(constraint)) return true;
        if (string.IsNullOrWhiteSpace(version)) return false;
        try
        {
            // 语法特征优先于 loader 声明：混合元数据 jar（fabric.mod.json + mods.toml 并存）可能把
            // Maven 区间挂在 Fabric loader 名下，按语法自识别可避免方言错配恒判不满足
            var t = constraint.TrimStart();
            var looksMaven = t.Length > 0 && (t[0] == '[' || t[0] == '(');
            var looksSemver = constraint.IndexOfAny(new[] { '>', '<', '~', '^', '*' }) >= 0 ||
                              constraint.Contains("||");
            if (looksMaven && !looksSemver) return SatisfiesMaven(constraint, version);
            if (looksSemver && !looksMaven) return SatisfiesSemVer(constraint, version);

            // 语法无法区分（如裸版本：Maven=软下限，semver=精确）：按 loader 方言
            if (IsForgeLike(loaderType)) return SatisfiesMaven(constraint, version);
            if (loaderType is null) return SatisfiesSemVer(constraint, version) || SatisfiesMaven(constraint, version);
            return SatisfiesSemVer(constraint, version);
        }
        catch
        {
            return false;
        }
    }

    private static string StripBuild(string s)
    {
        var i = s.IndexOf('+');
        return i < 0 ? s : s.Substring(0, i);
    }

    public static string StripV(string s) =>
        s is { Length: > 1 } && (s[0] == 'v' || s[0] == 'V') && char.IsDigit(s[1]) ? s.Substring(1) : s;

    private static int Cmp(string a, string b) => McVersionComparer.CompareVersion(StripV(a), StripV(b));

    /// <summary>按 Maven ComparableVersion 规则比较版本。</summary>
    public static int CompareMavenVersions(string left, string right)
    {
        left = StripV(left?.Trim() ?? "");
        right = StripV(right?.Trim() ?? "");
        try
        {
            return MavenComparableVersion.Compare(left, right);
        }
        catch (ArgumentException)
        {
            return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>Fabric 扩展 SemVer 排序；不符合 SemVer 的版本按规范回退为字典序。</summary>
    public static int CompareFabricVersions(string left, string right)
    {
        left = StripBuild(left?.Trim() ?? "");
        right = StripBuild(right?.Trim() ?? "");
        return IsKnown(left) && IsKnown(right)
            ? CompareSemVer(left, right)
            : string.CompareOrdinal(left, right);
    }

    /// <summary>
    ///     约束是否至少有一个可解析的版本边界，供 provider 检查时判断能否可靠地报告版本不符。
    ///     Maven 区间的上界同样是有效边界，例如 <c>(,2.0)</c> 必须拒绝 3.0，不能因缺少下界而 fail-open。
    /// </summary>
    public static bool HasComparableBound(string constraint)
    {
        if (string.IsNullOrWhiteSpace(constraint)) return false;
        foreach (var interval in SplitTopLevel(constraint))
        {
            var s = interval.Trim();
            if (s.Length == 0) continue;
            if (s[0] != '[' && s[0] != '(')
            {
                var bare = s.TrimStart('>', '<', '=', '~', '^', ' ', '"');
                if (IsKnown(bare)) return true;
                continue;
            }

            if (s.Length < 2 || (s[^1] != ']' && s[^1] != ')')) continue;
            var body = s.Substring(1, s.Length - 2);
            var comma = body.IndexOf(',');
            if (comma < 0)
            {
                if (IsKnown(body.Trim())) return true; // [a] 精确
                continue;
            }

            var lo = body.Substring(0, comma).Trim();
            var hi = body.Substring(comma + 1).Trim();
            if (IsKnown(lo) || IsKnown(hi)) return true;
        }

        return false;
    }

    private static bool IsKnown(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        if (s.EndsWith("-")) s = s.Substring(0, s.Length - 1);
        s = StripV(s);
        return s.Length > 0 && char.IsDigit(s[0]);
    }

    #region Maven 区间（Forge / NeoForge）

    private static bool SatisfiesMaven(string constraint, string mc)
    {
        foreach (var interval in SplitTopLevel(constraint))
        {
            var s = interval.Trim();
            if (s.Length == 0) continue;

            if (s[0] != '[' && s[0] != '(')
            {
                if (s.Length > 0 && CompareMavenVersions(mc, s) >= 0) return true; // 裸版本=软下限
                continue;
            }

            if (s.Length < 2 || (s[^1] != ']' && s[^1] != ')')) continue; // 畸形区间（未闭合）：不猜
            var incLo = s[0] == '[';
            var incHi = s[^1] == ']';
            var body = s.Substring(1, s.Length - 2);
            var comma = body.IndexOf(',');
            if (comma < 0)
            {
                var only = body.Trim(); // [a] 精确
                if (only.Length > 0 && CompareMavenVersions(mc, only) == 0) return true;
                continue;
            }

            var loStr = body.Substring(0, comma).Trim();
            var hiStr = body.Substring(comma + 1).Trim();
            var ok = true;
            if (loStr.Length > 0)
            {
                var c = CompareMavenVersions(mc, loStr);
                ok = incLo ? c >= 0 : c > 0;
            }

            if (ok && hiStr.Length > 0)
            {
                var c = CompareMavenVersions(mc, hiStr);
                ok = incHi ? c <= 0 : c < 0;
            }

            if (ok) return true;
        }

        return false;
    }

    // 按括号深度为 0 的逗号切分（区间内部的逗号不切）
    private static List<string> SplitTopLevel(string s)
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

    #endregion

    #region SemVer 谓词（Fabric / Quilt）

    private static int CompareSemVer(string a, string b)
    {
        a = StripBuild(a);
        b = StripBuild(b);
        var aPre = a.IndexOf('-');
        var bPre = b.IndexOf('-');
        var aBase = aPre < 0 ? a : a.Substring(0, aPre);
        var bBase = bPre < 0 ? b : b.Substring(0, bPre);
        var c = _CompareSemVerCore(aBase, bBase);
        if (c != 0) return c;
        if (aPre < 0 && bPre < 0) return 0;
        if (aPre < 0) return 1; // a 无预发布 > b（有预发布）
        if (bPre < 0) return -1; // a 有预发布 < b（正式版）
        return ComparePrerelease(a.Substring(aPre + 1), b.Substring(bPre + 1));
    }

    private static int _CompareSemVerCore(string left, string right)
    {
        var a = left.Split('.');
        var b = right.Split('.');
        if (a.All(IsAllDigits) && b.All(IsAllDigits))
        {
            var count = Math.Max(a.Length, b.Length);
            for (var i = 0; i < count; i++)
            {
                var c = CompareNumericId(i < a.Length ? a[i] : "0", i < b.Length ? b[i] : "0");
                if (c != 0) return c;
            }
            return 0;
        }

        return Cmp(left, right);
    }

    private static int ComparePrerelease(string a, string b)
    {
        var pa = a.Split('.');
        var pb = b.Split('.');
        var n = Math.Min(pa.Length, pb.Length);
        for (var i = 0; i < n; i++)
        {
            bool xn = IsAllDigits(pa[i]), yn = IsAllDigits(pb[i]);
            int c;
            if (xn && yn) c = CompareNumericId(pa[i], pb[i]);
            else if (xn) c = -1; // 数字标识符 < 字母标识符
            else if (yn) c = 1;
            else c = string.CompareOrdinal(pa[i], pb[i]);
            if (c != 0) return c;
        }

        return pa.Length.CompareTo(pb.Length);
    }

    private static bool IsAllDigits(string s)
    {
        if (s.Length == 0) return false;
        foreach (var ch in s)
            if (!char.IsDigit(ch))
                return false;
        return true;
    }

    // 纯数字标识符按数值比：去前导零后先比长度再字典序，避免长串溢出
    private static int CompareNumericId(string x, string y)
    {
        x = x.TrimStart('0');
        y = y.TrimStart('0');
        return x.Length != y.Length ? x.Length - y.Length : string.CompareOrdinal(x, y);
    }

    private static bool SatisfiesSemVer(string constraint, string mc)
    {
        foreach (var alt in constraint.Split(new[] { "||" }, StringSplitOptions.None))
        {
            var a = alt.Trim();
            if (a.Length == 0) continue;
            var all = true;
            foreach (var term in a.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                if (!TermMatches(term, mc))
                {
                    all = false;
                    break;
                }

            if (all) return true;
        }

        return false;
    }

    private static bool TermMatches(string term, string mc)
    {
        if (term == "*" || string.Equals(term, "any", StringComparison.OrdinalIgnoreCase)) return true;

        var i = 0;
        while (i < term.Length && "><=".IndexOf(term[i]) >= 0) i++;
        var op = term.Substring(0, i);
        var ver = term.Substring(i).Trim();
        // Fabric 预发布下限语法 ">=26.1-"：下界纳入 26.1 的预发布（26.1-pre1 等），而非稳定版 26.1
        var prereleaseFloor = ver.EndsWith("-");
        if (prereleaseFloor) ver = ver.Substring(0, ver.Length - 1);
        if (ver.Length == 0) return false;

        // ~（同 minor 内可升）与 ^（同 major 内可升；major=0 时按 semver 规则同 minor）
        if (op.Length == 0 && (ver[0] == '~' || ver[0] == '^'))
        {
            var tilde = ver[0] == '~';
            var baseVer = ver.Substring(1).Trim();
            if (!IsKnown(baseVer)) return false;
            if (CompareSemVer(mc, baseVer) < 0) return false;
            var nums = new List<int>();
            var cur = -1;
            foreach (var ch in baseVer)
                if (char.IsDigit(ch))
                {
                    var digit = ch - '0';
                    if (cur > (int.MaxValue - digit) / 10) return false;
                    cur = (cur < 0 ? 0 : cur * 10) + digit;
                }
                else if (cur >= 0)
                {
                    nums.Add(cur);
                    cur = -1;
                }

            if (cur >= 0) nums.Add(cur);
            if (nums.Count == 0) return false;
            string upper;
            if (tilde)
            {
                // ~：允许 patch 级更新，上界为 minor+1（只有 major 时为 major+1）
                upper = nums.Count >= 2 ? nums[0] + "." + (nums[1] + 1) : (nums[0] + 1).ToString();
            }
            else
            {
                // ^：上界为第一个非零组件 +1（其后组件归零）；^0.0.3 → 0.0.4，^0.2.3 → 0.3，^1.2.3 → 2
                var idx = 0;
                while (idx < nums.Count && nums[idx] == 0) idx++;
                if (idx >= nums.Count) idx = nums.Count - 1; // 全零 ^0.0.0：上界为末位 +1（仅匹配自身）
                upper = string.Join(".", nums.Take(idx).Concat(new[] { nums[idx] + 1 }));
            }

            return CompareSemVer(mc, upper) < 0;
        }

        // 通配 1.20.x / 1.20.* 仅在无运算符时有意义
        if (op.Length == 0 && (ver.EndsWith(".x") || ver.EndsWith(".X") || ver.EndsWith(".*")))
        {
            var prefix = ver.Substring(0, ver.Length - 2);
            return mc == prefix || mc.StartsWith(prefix + ".", StringComparison.Ordinal);
        }

        if (!IsKnown(ver))
            return (op is "" or "=" or "==") && string.Equals(mc, ver, StringComparison.Ordinal);
        if (prereleaseFloor && (op is "" or ">=" or ">") &&
            string.Equals(mc.Split('-')[0], ver, StringComparison.OrdinalIgnoreCase))
            return true;
        var c = CompareSemVer(mc, ver);
        return op switch
        {
            "" or "=" or "==" => c == 0, // Fabric 裸版本是精确匹配
            ">=" => c >= 0,
            ">" => c > 0,
            "<=" => c <= 0,
            "<" => c < 0,
            _ => false // 异常运算符：不猜
        };
    }

    #endregion

    #region 版本 token（文件名/版本号精确命中兜底）

    // version 是否作为完整版本词出现在 haystack 中（"1.21" 不命中 "1.21.2" 或 "9.1.20"）
    public static bool ContainsVersionToken(string haystack, string version)
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
