using System;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PCL.Core.App.Localization;
using PCL.Core.Utils;

namespace PCL;

public enum McVersionCategory
{
    Release,
    Snapshot,
    BeforeRelease,
    AprilFools,
    Unknown
}

public static class McVersionClassifier
{
    private static readonly Regex _WeekSnapshot = new(
        @"[0-9]{2}w[0-9]{2}[a-z]",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string GetCategoryDisplayName(McVersionCategory cat)
    {
        return cat switch
        {
            McVersionCategory.Release => Lang.Text("Download.Version.Type.Release"),
            McVersionCategory.Snapshot => Lang.Text("Download.Version.Type.Development"),
            McVersionCategory.BeforeRelease => Lang.Text("Download.Version.Type.BeforeRelease"),
            McVersionCategory.AprilFools => Lang.Text("Download.Version.Type.AprilFools"),
            McVersionCategory.Unknown => Lang.Text("Download.Version.Type.Unknown"),
            _ => Lang.Text("Download.Version.Type.Unknown")
        };
    }

    public static McVersionCategory Classify(string? id, int? line, int? anchorLine, DateTime? releaseTime,
        string? versionType = null)
    {
        if (string.IsNullOrEmpty(id)) return McVersionCategory.Unknown;
        if (_IsAprilFoolsSnapshot(releaseTime, versionType) || id.StartsWith("2point0_", StringComparison.Ordinal))
            return McVersionCategory.AprilFools;

        var lower = id.ToLowerInvariant();
        if (line is int current && anchorLine is int anchor && current < anchor)
            return McVersionCategory.BeforeRelease;
        if (lower.Contains("snapshot") || lower.Contains("rc") || lower.Contains("pre") || lower.Contains("combat") || _WeekSnapshot.IsMatch(lower) || lower.Contains("13w12~"))
            return McVersionCategory.Snapshot;
        if (lower.Contains('.')) return McVersionCategory.Release;
        return McVersionCategory.Unknown;
    }

    private static bool _IsAprilFoolsSnapshot(DateTime? releaseTime, string? versionType)
    {
        if (!string.Equals(versionType, "snapshot", StringComparison.OrdinalIgnoreCase))
            return false;
        if (releaseTime is not { } time || time == DateTime.MinValue)
            return false;
        var shifted = time.ToUniversalTime().AddHours(2d);
        return shifted is { Month: 4, Day: 1 };
    }

    public static McVersionCategory CategoryOf(JsonObject version)
    {
        var type = _GetString(version, "type");
        if (string.Equals(type, "special", StringComparison.OrdinalIgnoreCase))
            return McVersionCategory.AprilFools;
        var id = _GetString(version, "id");
        var index = VanillaVersionIndex.Capture();
        int? line = null;
        if (index is not null && index.MatchLine(id, out var found))
            line = found;
        var releaseTime = GetReleaseTime(version);
        return Classify(id, line, index?.AnchorLine, releaseTime == DateTime.MinValue ? null : releaseTime, type);
    }

    public static McVersionCategory ClassifyVersion(JsonObject version)
    {
        var category = CategoryOf(version);
        if (category == McVersionCategory.AprilFools)
            _MarkAsAprilFools(version);
        return category;
    }

    public static int ListedLine(JsonObject version)
    {
        var index = VanillaVersionIndex.Capture();
        if (index is null) return int.MinValue;
        return index.MatchLine(_GetString(version, "id"), out var line) ? line : int.MinValue;
    }

    /// <summary>
    /// 只对已经分成 Release 的 id 做一次分段。1.21.5 → 1.21，26.1.2 → 26.1。
    /// </summary>
    public static string? ReleaseFamily(string id)
    {
        var parts = id.Split('.');
        if (parts.Length < 2) return null;
        if (parts[0] == "1") return "1." + parts[1];
        if (parts[0].Length == 2 && char.IsDigit(parts[0][0]) && char.IsDigit(parts[0][1]))
            return parts[0] + "." + parts[1];
        return null;
    }

    public static DateTime GetReleaseTime(JsonObject version)
    {
        return _GetDateTime(version, "releaseTime");
    }

    private static void _MarkAsAprilFools(JsonObject version)
    {
        version["type"] = "special";
        var lore = GetMcFoolName(_GetString(version, "id"));
        if (lore.Length > 0)
            version["lore"] = lore;
    }

    public static string GetMcFoolName(string name)
    {
        name = name.ToLowerInvariant();

        return name switch
        {
            _ when name.StartsWith("2.0") || name.StartsWith("2point0")
                => Lang.Text("Minecraft.Fool.Description.2013") + name switch
                {
                    _ when name.EndsWith("red")
                        => Lang.Text("Minecraft.Fool.Tag.Red"),

                    _ when name.EndsWith("blue")
                        => Lang.Text("Minecraft.Fool.Tag.Blue"),

                    _ when name.EndsWith("purple")
                        => Lang.Text("Minecraft.Fool.Tag.Purple"),

                    _ => ""
                },

            "15w14a" => Lang.Text("Minecraft.Fool.Description.2015"),

            "1.rv-pre1" => Lang.Text("Minecraft.Fool.Description.2016"),

            "3d shareware v1.34" => Lang.Text("Minecraft.Fool.Description.2019"),

            _ when name.StartsWith("20w14inf") || name == "20w14∞"
                => Lang.Text("Minecraft.Fool.Description.2020"),

            "22w13oneblockatatime" => Lang.Text("Minecraft.Fool.Description.2022"),

            "23w13a_or_b" => Lang.Text("Minecraft.Fool.Description.2023"),

            "24w14potato" => Lang.Text("Minecraft.Fool.Description.2024"),

            "25w14craftmine" => Lang.Text("Minecraft.Fool.Description.2025"),

            "26w14a" => Lang.Text("Minecraft.Fool.Description.2026"),

            _ => ""
        };
    }

    private static DateTime _GetDateTime(JsonObject obj, string key)
    {
        return JsonCompat.TryGetDateTime(obj[key], out var dateTime)
            ? dateTime
            : DateTime.MinValue;
    }

    private static string _GetString(JsonObject obj, string key)
    {
        var node = obj[key];
        if (node is null) return "";

        return node is JsonValue value && value.TryGetValue<string>(out var result)
            ? result
            : node.ToString();
    }
}
