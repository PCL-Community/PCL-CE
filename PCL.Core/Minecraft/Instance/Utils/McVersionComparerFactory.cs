using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
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

// Comparer for snapshot versions like "24w14a"
public class SnapshotVersionComparer : IComparer<string> {
    public int Compare(string? x, string? y) {
        if (x == y) return 0;
        if (x == null) return -1;
        if (y == null) return 1;

        var xMatch = RegexPatterns.McSnapshotVersion.Match(x);
        var yMatch = RegexPatterns.McSnapshotVersion.Match(y);

        if (!xMatch.Success || !yMatch.Success) {
            // Fallback for non-standard snapshot formats
            return StringComparer.Ordinal.Compare(x, y);
        }

        var xYear = int.Parse(xMatch.Groups[1].Value);
        var yYear = int.Parse(yMatch.Groups[1].Value);
        if (xYear != yYear) return xYear.CompareTo(yYear);

        var xWeek = int.Parse(xMatch.Groups[2].Value);
        var yWeek = int.Parse(yMatch.Groups[2].Value);

        // Compare sub-version char, e.g., 'a' vs 'b'
        return xWeek != yWeek 
            ? xWeek.CompareTo(yWeek)
            : string.Compare(xMatch.Groups[3].Value, yMatch.Groups[3].Value, StringComparison.Ordinal);
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
            var priority = normalizedVersion.Contains('a') ? 0 : (normalizedVersion.Contains("st") ? 1 : 2);
            return (1, priority, raw);
        }

        var indevMatch = RegexPatterns.McIndevVersion.Match(normalizedVersion);
        if (indevMatch.Success) {
            var key = DateTime.TryParseExact(indevMatch.Groups[1].Value, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var date)
                ? (date, indevMatch.Groups[3].Success ? int.Parse(indevMatch.Groups[3].Value) : 0)
                : (DateTime.MinValue, 0);
            return (2, key, raw);
        }

        var infdevMatch = RegexPatterns.McInfdevVersion.Match(normalizedVersion);
        if (infdevMatch.Success) {
            var key = DateTime.TryParseExact(infdevMatch.Groups[1].Value, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var date)
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
/// Abstract base class for comparing semantic version strings that may have suffixes.
/// Handles the common logic of parsing and comparing numeric parts.
/// </summary>
public abstract class VersionComparerBase : IComparer<string> {
    public abstract int Compare(string? x, string? y);

    protected abstract (string VersionNum, string Suffix) SplitVersion(string version);

    protected virtual int CompareSuffix(string xSuffix, string ySuffix) => StringComparer.Ordinal.Compare(xSuffix, ySuffix);

    protected int CompareCore(string x, string y) {
        var (xVersionNum, xSuffix) = SplitVersion(x);
        var (yVersionNum, ySuffix) = SplitVersion(y);

        var xParts = xVersionNum.Split('.').Select(s => int.TryParse((string?)s, out var n) ? n : 0).ToArray();
        var yParts = yVersionNum.Split('.').Select(s => int.TryParse(s, out var n) ? n : 0).ToArray();

        for (var i = 0; i < Math.Min(xParts.Length, yParts.Length); i++) {
            if (xParts[i] != yParts[i])
                return xParts[i].CompareTo(yParts[i]);
        }

        if (xParts.Length != yParts.Length)
            return xParts.Length.CompareTo(yParts.Length);

        var xHasSuffix = !string.IsNullOrEmpty(xSuffix);
        var yHasSuffix = !string.IsNullOrEmpty(ySuffix);

        if (xHasSuffix != yHasSuffix)
            return xHasSuffix ? 1 : -1; // No suffix (stable) is "less than" (comes before) a suffix

        if (xHasSuffix)
            return CompareSuffix(xSuffix, ySuffix);

        return xHasSuffix 
            ? CompareSuffix(xSuffix, ySuffix) 
            : StringComparer.Ordinal.Compare(x, y);
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
        var parts = version.Split([ '-' ], 2);
        return (parts[0], parts.Length > 1 ? parts[1] : "");
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
        var parts = version.Split([ '+' ], 2);
        return (parts[0], parts.Length > 1 ? parts[1] : "");
    }

    protected override int CompareSuffix(string xSuffix, string ySuffix) {
        _ = int.TryParse(xSuffix.Replace("build.", ""), out var xBuildNum);
        _ = int.TryParse(ySuffix.Replace("build.", ""), out var yBuildNum);
        return xBuildNum.CompareTo(yBuildNum);
    }
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

public class QuiltVersionComparer : VersionComparerBase {
    public override int Compare(string? x, string? y) {
        if (x == y) return 0;
        if (x == null) return 1;
        if (y == null) return -1;
        return CompareCore(x.TrimEnd('/'), y.TrimEnd('/'));
    }

    protected override (string VersionNum, string Suffix) SplitVersion(string version) {
        var parts = version.Split([ "-beta." ], 2, StringSplitOptions.None);
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
        var parts = version.Split([ "-alpha" ], 2, StringSplitOptions.None);
        return (parts[0], parts.Length > 1 ? parts[1] : "");
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
        var preParts = version.Split([ "_pre" ], 2, StringSplitOptions.None);
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
        { McInstanceCardType.LabyMod, McVersionComparerFactory.ReleaseVersionComparer },
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
