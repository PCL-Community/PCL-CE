using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using PCL.Core.Minecraft.Instance.Impl;
using PCL.Core.Minecraft.Instance.Interface;
using PCL.Core.Utils;

namespace PCL.Core.Minecraft.Instance.Utils;

// Comparer for standard "1.12.2" style versions
public class ReleaseVersionComparer : IComparer<string> {
    public int Compare(string? x, string? y) {
        if (x == y) return 0;
        if (x == null) return -1;

        // Use Version.Parse for robust comparison of release versions
        return y == null
            ? 1
            : Version.Parse(x).CompareTo(Version.Parse(y));
    }
}

public class SnapshotVersionComparer : IComparer<string> {
    public int Compare(string? x, string? y) {
        if (x == y) return 0;
        if (x == null) return -1;
        if (y == null) return 1;

        var oldX = RegexPatterns.McSnapshotVersion.Match(x);
        var oldY = RegexPatterns.McSnapshotVersion.Match(y);

        var newX = RegexPatterns.McNewSnapshotVersion.Match(x);
        var newY = RegexPatterns.McNewSnapshotVersion.Match(y);

        // ===== 新 snapshot vs 新 snapshot =====
        if (newX.Success && newY.Success) {
            return CompareNewSnapshots(newX, newY);
        }

        // ===== 旧 snapshot vs 旧 snapshot =====
        if (oldX.Success && oldY.Success) {
            return CompareOldSnapshots(oldX, oldY);
        }

        // ===== 新 vs 旧 =====
        if (newX.Success && oldY.Success) return 1;
        if (oldX.Success && newY.Success) return -1;

        // ===== fallback =====
        return StringComparer.Ordinal.Compare(x, y);
    }

    private static int CompareOldSnapshots(Match x, Match y) {
        var xYear = int.Parse(x.Groups[1].Value);
        var yYear = int.Parse(y.Groups[1].Value);
        if (xYear != yYear) return xYear.CompareTo(yYear);

        var xWeek = int.Parse(x.Groups[2].Value);
        var yWeek = int.Parse(y.Groups[2].Value);
        if (xWeek != yWeek) return xWeek.CompareTo(yWeek);

        return string.Compare(
            x.Groups[3].Value,
            y.Groups[3].Value,
            StringComparison.Ordinal
            );
    }

    private static int CompareNewSnapshots(Match x, Match y) {
        // 比较基础版本号 26.1 / 26.1.1
        var baseCompare = McVersionComparerFactory.ReleaseVersionComparer.Compare(
            x.Groups[1].Value,
            y.Groups[1].Value
            );
        if (baseCompare != 0) return baseCompare;

        // 比较 snapshot 构建号
        var xBuild = int.Parse(x.Groups[2].Value);
        var yBuild = int.Parse(y.Groups[2].Value);
        return xBuild.CompareTo(yBuild);
    }
}

// Comparer for pre-release (Alpha, Beta, etc.) versions
public class OldVersionComparer : IComparer<string> {
    public int Compare(string? x, string? y) {
        if (x == y) return 0;
        if (x == null) return -1;
        if (y == null) return 1;

        var (xOrder, xKey, xRaw) = GetSortKey(x);
        var (yOrder, yKey, yRaw) = GetSortKey(y);

        if (xOrder != yOrder) return xOrder.CompareTo(yOrder);

        var keyCompare = CompareKeys(xKey, yKey);
        return keyCompare != 0 ? keyCompare : StringComparer.Ordinal.Compare(xRaw, yRaw);
    }

    private static (int order, object key, string raw) GetSortKey(string version) {
        var raw = version;
        var normalizedVersion = version.Trim().ToLowerInvariant();

        if (normalizedVersion.StartsWith("rd-")) {
            return (0, int.TryParse(normalizedVersion.AsSpan(3), out var num) ? num : 0, raw);
        }

        if (normalizedVersion.StartsWith('c')) {
            var priority = normalizedVersion.Contains('a') ? 0 : normalizedVersion.Contains("st") ? 1 : 2;
            return (1, priority, raw);
        }

        var indevMatch = RegexPatterns.McIndevVersion.Match(normalizedVersion);
        if (indevMatch.Success) {
            var key = DateTime.TryParseExact(indevMatch.Groups[1].Value, "yyyyMMdd", null, DateTimeStyles.None, out var date)
                ? (date, indevMatch.Groups[3].Success ? int.Parse(indevMatch.Groups[3].Value) : 0)
                : (DateTime.MinValue, 0);
            return (2, key, raw);
        }

        var infdevMatch = RegexPatterns.McInfdevVersion.Match(normalizedVersion);
        if (infdevMatch.Success) {
            var key = DateTime.TryParseExact(infdevMatch.Groups[1].Value, "yyyyMMdd", null, DateTimeStyles.None, out var date)
                ? (date, infdevMatch.Groups[3].Success ? int.Parse(infdevMatch.Groups[3].Value) : 0)
                : (DateTime.MinValue, 0);
            return (3, key, raw);
        }

        if (normalizedVersion.StartsWith('a')) {
            var alphaParts = normalizedVersion.Split('-')[0].Split('_')[0][1..];
            return (4, Version.TryParse(alphaParts, out var ver) ? ver : alphaParts, raw);
        }

        if (normalizedVersion.StartsWith('b')) {
            var betaParts = normalizedVersion.Split('-')[0].Split('_')[0][1..];
            return (5, Version.TryParse(betaParts, out var ver) ? ver : betaParts, raw);
        }

        return (99, normalizedVersion, raw);
    }

    private static int CompareKeys(object xKey, object yKey) {
        switch (xKey) {
            case int xInt when yKey is int yInt:
                return xInt.CompareTo(yInt);
            case ValueTuple<DateTime, int> xTuple when yKey is ValueTuple<DateTime, int> yTuple:
                var dateCompare = xTuple.Item1.CompareTo(yTuple.Item1);
                return dateCompare != 0 ? dateCompare : xTuple.Item2.CompareTo(yTuple.Item2);
            case Version xVer when yKey is Version yVer:
                return xVer.CompareTo(yVer);
            default:
                return StringComparer.Ordinal.Compare(xKey.ToString(), yKey.ToString());
        }
    }
}

// Comparer for April Fools' versions
public class FoolVersionComparer : IComparer<string> {
    // List is static and immutable for efficiency
    private static readonly ImmutableList<string> FoolVersions = ImmutableList.Create(
        "2point0_red", "2point0_blue", "2point0_purple", "2.0_red", "2.0_blue", "2.0_purple",
        "15w14a", "1.rv-pre1", "3d shareware v1.34", "20w14∞", "22w13oneblockatatime",
        "23w13a_or_b", "24w14potato", "25w14craftmine"
        );

    public int Compare(string? x, string? y) {
        if (x == y) return 0;
        if (x == null) return -1;

        // Order is determined by the hardcoded list index
        return y == null
            ? 1
            : FoolVersions.IndexOf(x).CompareTo(FoolVersions.IndexOf(y));
    }
}

/// <summary>
///     Abstract base class for comparing semantic version strings that may have suffixes.
///     Handles the common logic of parsing and comparing numeric parts.
/// </summary>
public abstract class VersionComparerBase : IComparer<string> {
    public abstract int Compare(string? x, string? y);

    protected abstract (string VersionNum, string Suffix) SplitVersion(string version);

    protected virtual int CompareSuffix(string xSuffix, string ySuffix)
        => StringComparer.Ordinal.Compare(xSuffix, ySuffix);

    // 修改：支持更多的分隔符，并且不再丢弃 + 号后的内容
    static protected int CompareSemVerSuffix(string xSuffix, string ySuffix) {
        // 使用 . - + 作为通用分隔符
        var delimiters = new[] { '.', '-', '+' };
        var xParts = xSuffix.Split(delimiters, StringSplitOptions.RemoveEmptyEntries);
        var yParts = ySuffix.Split(delimiters, StringSplitOptions.RemoveEmptyEntries);

        var len = Math.Min(xParts.Length, yParts.Length);
        for (var i = 0; i < len; i++) {
            var xPart = xParts[i];
            var yPart = yParts[i];

            var xIsNum = int.TryParse(xPart, out var xNum);
            var yIsNum = int.TryParse(yPart, out var yNum);

            if (xIsNum && yIsNum) {
                var comp = xNum.CompareTo(yNum);
                if (comp != 0) return comp;
            } else if (!xIsNum && !yIsNum) {
                var comp = StringComparer.Ordinal.Compare(xPart, yPart);
                if (comp != 0) return comp;
            } else {
                var comp = StringComparer.Ordinal.Compare(xPart, yPart);
                if (comp != 0) return comp;
            }
        }
        return xParts.Length.CompareTo(yParts.Length);
    }

    protected int CompareCore(string x, string y) {
        var (xVersionNum, xSuffix) = SplitVersion(x);
        var (yVersionNum, ySuffix) = SplitVersion(y);

        var xParts = xVersionNum.Split('.').Select(s => int.TryParse(s, out var n) ? n : 0).ToArray();
        var yParts = yVersionNum.Split('.').Select(s => int.TryParse(s, out var n) ? n : 0).ToArray();

        for (var i = 0; i < Math.Min(xParts.Length, yParts.Length); i++) {
            if (xParts[i] != yParts[i])
                return xParts[i].CompareTo(yParts[i]);
        }

        if (xParts.Length != yParts.Length) {
            // 26.1.1.15 > 26.1.1
            return xParts.Length.CompareTo(yParts.Length);
        }

        var xHasSuffix = !string.IsNullOrEmpty(xSuffix);
        var yHasSuffix = !string.IsNullOrEmpty(ySuffix);

        if (xHasSuffix != yHasSuffix)
            return xHasSuffix ? -1 : 1;

        return xHasSuffix ? CompareSuffix(xSuffix, ySuffix) : StringComparer.Ordinal.Compare(x, y);
    }
}

public class NeoForgeVersionComparer : VersionComparerBase {
    public override int Compare(string? x, string? y) {
        if (x == y) return 0;

        if (string.IsNullOrEmpty(x)) return 1;
        if (string.IsNullOrEmpty(y)) return -1;

        return CompareCore(x, y);
    }

    protected override (string VersionNum, string Suffix) SplitVersion(string version) {
        // NeoForge 新格式: 26.1.0.0-alpha.5
        var parts = version.Split(new[] { '-' }, 2);
        return (parts[0], parts.Length > 1 ? parts[1] : "");
    }

    protected override int CompareSuffix(string xSuffix, string ySuffix) {
        // 1. 优先尝试提取 Snapshot 版本号
        // 格式：alpha.X+snapshot.Y 或 alpha.X+snapshot-Y
        var xSnap = GetSnapshotVersion(xSuffix);
        var ySnap = GetSnapshotVersion(ySuffix);

        // 2. 如果两者都包含 snapshot 版本，且不相等，直接根据 snapshot 决定大小
        if (xSnap != -1 && ySnap != -1) {
            var snapResult = xSnap.CompareTo(ySnap);
            if (snapResult != 0) return snapResult;
        }

        // 3. 如果 snapshot 相同，或者其中一方没有 snapshot，
        // 则回退到按照 alpha/beta 版本号进行的通用 SemVer 比较
        return CompareSemVerSuffix(xSuffix, ySuffix);
    }

    private static int GetSnapshotVersion(string suffix) {
        // 寻找 "snapshot" 关键字
        var index = suffix.LastIndexOf("snapshot", StringComparison.OrdinalIgnoreCase);
        if (index == -1) return -1;

        // 截取 snapshot 后面的部分
        // "+snapshot.1" -> ".1"  或者  "+snapshot-2" -> "-2"
        var afterSnap = suffix.Substring(index + "snapshot".Length);

        // 寻找随后的数字
        if (afterSnap.Length > 1) {
            var separator = afterSnap[0];
            if (separator is '.' or '-') {
                var numberPart = afterSnap.Substring(1);
                // 考虑到可能还有其他后缀，只取数字部分
                var cleanNumber = new string(numberPart.TakeWhile(char.IsDigit).ToArray());
                if (int.TryParse(cleanNumber, out var parserNum)) {
                    return parserNum;
                }
            }
        }
        return -1;
    }
}

public class FabricVersionComparer : VersionComparerBase {
    public override int Compare(string? x, string? y) {
        if (x == y) return 0;
        if (x == null) return 1;
        if (y == null) return -1;
        return CompareCore(x, y);
    }

    protected override (string VersionNum, string Suffix) SplitVersion(string version) {
        var parts = version.Split(new[] { '+' }, 2);
        return (parts[0], parts.Length > 1 ? parts[1] : "");
    }

    protected override int CompareSuffix(string xSuffix, string ySuffix) {
        _ = int.TryParse(xSuffix.Replace("build.", ""), out var xBuildNum);
        _ = int.TryParse(ySuffix.Replace("build.", ""), out var yBuildNum);
        return xBuildNum.CompareTo(yBuildNum);
    }
}

public class QuiltVersionComparer : VersionComparerBase {
    public override int Compare(string? x, string? y) {
        if (x == y) return 0;
        if (x == null) return 1;
        if (y == null) return -1;
        return CompareCore(x.TrimEnd('/'), y.TrimEnd('/'));
    }

    protected override (string VersionNum, string Suffix) SplitVersion(string version) {
        var parts = version.Split(["-beta."], 2, StringSplitOptions.None);
        return (parts[0], parts.Length > 1 ? parts[1] : "");
    }

    protected override int CompareSuffix(string xSuffix, string ySuffix) {
        _ = int.TryParse(xSuffix, out var xBetaNum);
        _ = int.TryParse(ySuffix, out var yBetaNum);
        return xBetaNum.CompareTo(yBetaNum);
    }
}

public class CleanroomVersionComparer : VersionComparerBase {
    public override int Compare(string? x, string? y) {
        if (x == y) return 0;
        if (x == null) return 1;
        if (y == null) return -1;
        return CompareCore(x, y);
    }

    protected override (string VersionNum, string Suffix) SplitVersion(string version) {
        var parts = version.Split(["-alpha"], 2, StringSplitOptions.None);
        return (parts[0], parts.Length > 1 ? parts[1] : "");
    }

    protected override int CompareSuffix(string xSuffix, string ySuffix) => CompareSemVerSuffix(xSuffix, ySuffix);
}

public class ForgeVersionComparer : IComparer<string> {
    public int Compare(string? x, string? y) {
        if (x == y) return 0;
        if (x == null) return 1;
        if (y == null) return -1;

        var xParts = x.Split('.').Select(s => int.TryParse(s, out var n) ? n : 0).ToArray();
        var yParts = y.Split('.').Select(s => int.TryParse(s, out var n) ? n : 0).ToArray();

        for (var i = 0; i < Math.Min(Math.Max(xParts.Length, yParts.Length), 4); i++) {
            var xValue = i < xParts.Length ? xParts[i] : 0;
            var yValue = i < yParts.Length ? yParts[i] : 0;
            if (xValue != yValue)
                return xValue.CompareTo(yValue);
        }

        return StringComparer.Ordinal.Compare(x, y);
    }
}

public class LiteLoaderVersionComparer : IComparer<string> {
    public int Compare(string? x, string? y) {
        if (x == y) return 0;
        if (x == null) return 1;
        if (y == null) return -1;

        var (xTimestamp, xBuild) = Parse(x);
        var (yTimestamp, yBuild) = Parse(y);

        var timeCompare = xTimestamp.CompareTo(yTimestamp);
        return timeCompare != 0 ? timeCompare : xBuild.CompareTo(yBuild);
    }

    private static (long timestamp, int build) Parse(string version) {
        var parts = version.Split('-');
        if (parts.Length == 0) return (0, 0);

        var timeStr = parts[0].Replace(".", "");

        if (long.TryParse(timeStr, out var timestamp)) { } else {
            timestamp = 0;
        }

        int build;
        if (parts.Length > 1 && int.TryParse(parts[1], out var tempBuild)) {
            build = tempBuild;
        } else {
            build = 0;
        }
        return (timestamp, build);
    }
}

public class OptiFineVersionComparer : IComparer<string> {
    private const string Prefix = "HD_U_";

    public int Compare(string? x, string? y) {
        if (x == y) return 0;
        if (x == null) return 1;
        if (y == null) return -1;

        var (xMain, xSub, xPre) = Parse(x);
        var (yMain, ySub, yPre) = Parse(y);

        if (xMain != yMain) return xMain.CompareTo(yMain);
        if (xSub != ySub) return xSub.CompareTo(ySub);

        var xIsPre = xPre != -1;
        var yIsPre = yPre != -1;

        if (xIsPre != yIsPre) return xIsPre ? 1 : -1; // Stable versions (-1) come before pre-releases
        if (xPre != yPre) return xPre.CompareTo(yPre);

        return xPre != yPre
            ? xPre.CompareTo(yPre)
            : StringComparer.Ordinal.Compare(x, y);
    }

    private static (char main, int sub, int pre) Parse(string version) {
        var preParts = version.Split(["_pre"], 2, StringSplitOptions.None);
        var mainPart = preParts[0];

        if (!mainPart.StartsWith(Prefix)) return ('\0', 0, -1);

        mainPart = mainPart[Prefix.Length..];

        var main = mainPart.Length > 0 ? mainPart[0] : '\0';
        int sub;
        if (mainPart.Length > 1 && int.TryParse(mainPart.AsSpan(1), out var tempSub)) {
            sub = tempSub;
        } else {
            sub = 0;
        }

        var pre = -1; // -1 indicates a stable release
        if (preParts.Length > 1) {
            _ = int.TryParse(preParts[1], out pre);
        }

        return (main, sub, pre);
    }
}

// This class acts as a dispatcher to the correct comparer based on type.
public class PatcherVersionComparer : IComparer<(McInstanceCardType, PatchInfo)> {
    private static readonly Dictionary<McInstanceCardType, IComparer<string>> Comparers = new() {
        { McInstanceCardType.Release, McVersionComparerFactory.ReleaseVersionComparer },
        { McInstanceCardType.Snapshot, McVersionComparerFactory.SnapshotVersionComparer },
        { McInstanceCardType.Fool, McVersionComparerFactory.FoolVersionComparer },
        { McInstanceCardType.Old, McVersionComparerFactory.OldVersionComparer },
        { McInstanceCardType.NeoForge, McVersionComparerFactory.NeoForgeVersionComparer },
        { McInstanceCardType.Fabric, McVersionComparerFactory.FabricVersionComparer },
        { McInstanceCardType.Forge, McVersionComparerFactory.ForgeVersionComparer },
        { McInstanceCardType.Quilt, McVersionComparerFactory.QuiltVersionComparer },
        { McInstanceCardType.LegacyFabric, McVersionComparerFactory.FabricVersionComparer },
        { McInstanceCardType.Cleanroom, McVersionComparerFactory.CleanroomVersionComparer },
        { McInstanceCardType.LiteLoader, McVersionComparerFactory.LiteLoaderVersionComparer },
        { McInstanceCardType.OptiFine, McVersionComparerFactory.OptiFineVersionComparer },
        { McInstanceCardType.LabyMod, McVersionComparerFactory.ReleaseVersionComparer }
    };

    public int Compare((McInstanceCardType, PatchInfo) x, (McInstanceCardType, PatchInfo) y) {
        var (xType, xInfo) = x;
        var (_, yInfo) = y;

        if (xType is McInstanceCardType.Star or McInstanceCardType.Custom or McInstanceCardType.UnknownPatchers) {
            if (xInfo.ReleaseTime.HasValue && yInfo.ReleaseTime.HasValue) {
                return xInfo.ReleaseTime.Value.CompareTo(yInfo.ReleaseTime.Value);
            }
            return StringComparer.Ordinal.Compare(xInfo.Version, yInfo.Version);
        }

        if (xInfo.Version != null && yInfo.Version != null && Comparers.TryGetValue(xType, out var comparer)) {
            return comparer.Compare(xInfo.Version, yInfo.Version);
        }

        return 0;
    }
}

// This static factory provides singleton instances of each comparer.
public static class McVersionComparerFactory {
    public static IComparer<(McInstanceCardType, PatchInfo)> PatcherVersionComparer { get; } = new PatcherVersionComparer();

    public static IComparer<string> ReleaseVersionComparer { get; } = new ReleaseVersionComparer();
    public static IComparer<string> SnapshotVersionComparer { get; } = new SnapshotVersionComparer();
    public static IComparer<string> OldVersionComparer { get; } = new OldVersionComparer();
    public static IComparer<string> FoolVersionComparer { get; } = new FoolVersionComparer();

    public static IComparer<string> NeoForgeVersionComparer { get; } = new NeoForgeVersionComparer();
    public static IComparer<string> FabricVersionComparer { get; } = new FabricVersionComparer();
    public static IComparer<string> ForgeVersionComparer { get; } = new ForgeVersionComparer();
    public static IComparer<string> QuiltVersionComparer { get; } = new QuiltVersionComparer();
    public static IComparer<string> CleanroomVersionComparer { get; } = new CleanroomVersionComparer();
    public static IComparer<string> LiteLoaderVersionComparer { get; } = new LiteLoaderVersionComparer();

    public static IComparer<string> OptiFineVersionComparer { get; } = new OptiFineVersionComparer();
}
