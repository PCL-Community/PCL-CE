using System;
using System.Collections.Generic;

namespace PCL;

/// <summary>
///     判断某版本是否满足一条依赖版本约束。按加载器方言分派：
///     Forge/NeoForge = Maven 区间（<c>[a,b] [a,b) (a,b) [a,) (,b] [a]</c> 或裸 a=软下限）；
///     Fabric/Quilt = SemVer 谓词（<c>||</c> OR、空格 AND、<c>&gt;= &gt; &lt;= &lt; =</c>、<c>x</c>/<c>*</c> 通配、尾 <c>-</c>）。
///     哲学：宁可不标不可错标——任何拿不准的边界一律判不满足（fail-closed），比较统一走
///     <see cref="McVersionComparer" /> 以正确处理快照/预发布/年份版本。
/// </summary>
public static class McConstraintMatcher
{
    public static bool IsForgeLike(string loaderType) =>
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

    // 版本串是否可比较（1.20.1 / 26.1 / 23w13a 均数字开头；剥掉 Fabric 尾 - 后判断）
    private static bool IsKnown(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        if (s.EndsWith("-")) s = s.Substring(0, s.Length - 1);
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
                if (IsKnown(s) && McVersionComparer.CompareVersion(mc, s) >= 0) return true; // 裸版本=软下限
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
                if (IsKnown(only) && McVersionComparer.CompareVersion(mc, only) == 0) return true;
                continue;
            }

            var loStr = body.Substring(0, comma).Trim();
            var hiStr = body.Substring(comma + 1).Trim();
            if ((loStr.Length > 0 && !IsKnown(loStr)) || (hiStr.Length > 0 && !IsKnown(hiStr)))
                continue; // 边界不可解析：整段判不满足（fail-closed）
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

    // 空格 = AND，|| = OR，运算符 >= > <= < =，x/* 通配，尾 - 预发布标记
    private static bool SatisfiesSemVer(string constraint, string mc)
    {
        foreach (var alt in constraint.Split(new[] { "||" }, StringSplitOptions.None)) // OR
        {
            var a = alt.Trim();
            if (a.Length == 0) continue;
            var all = true;
            foreach (var term in a.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)) // AND
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
            if (McVersionComparer.CompareVersion(mc, baseVer) < 0) return false;
            var nums = new List<int>();
            var cur = -1;
            foreach (var ch in baseVer)
                if (char.IsDigit(ch)) cur = (cur < 0 ? 0 : cur * 10) + (ch - '0');
                else if (cur >= 0)
                {
                    nums.Add(cur);
                    cur = -1;
                }

            if (cur >= 0) nums.Add(cur);
            if (nums.Count == 0) return false;
            var upper = (tilde || nums[0] == 0) && nums.Count >= 2
                ? nums[0] + "." + (nums[1] + 1)
                : (nums[0] + 1).ToString();
            return McVersionComparer.CompareVersion(mc, upper) < 0;
        }

        // 通配 1.20.x / 1.20.* 仅在无运算符时有意义
        if (op.Length == 0 && (ver.EndsWith(".x") || ver.EndsWith(".X") || ver.EndsWith(".*")))
        {
            var prefix = ver.Substring(0, ver.Length - 2);
            return mc == prefix || mc.StartsWith(prefix + ".", StringComparison.Ordinal);
        }

        if (!IsKnown(ver)) return false; // 不可解析边界 fail-closed
        if (prereleaseFloor && (op is "" or ">=" or ">") &&
            string.Equals(mc.Split('-')[0], ver, StringComparison.OrdinalIgnoreCase))
            return true;
        var c = McVersionComparer.CompareVersion(mc, ver);
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
