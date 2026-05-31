using System;
using System.Text.Json.Nodes;
using PCL.Core.App.Localization;
using PCL.Core.Utils;

namespace PCL;

public enum McVersionCategory
{
    Release,
    Snapshot,
    BeforeRelease,
    AprilFools
}

public static class McVersionClassifier
{
    public static string GetCategoryDisplayName(McVersionCategory cat)
    {
        return cat switch
        {
            McVersionCategory.Release => Lang.Text("Download.Version.Type.Release"),
            McVersionCategory.Snapshot => Lang.Text("Download.Version.Type.Development"),
            McVersionCategory.BeforeRelease => Lang.Text("Download.Version.Type.BeforeRelease"),
            McVersionCategory.AprilFools => Lang.Text("Download.Version.Type.AprilFools"),
            _ => throw new ArgumentOutOfRangeException(nameof(cat))
        };
    }

    public static McVersionCategory ClassifyVersion(JsonObject version)
    {
        var type = _GetString(version, "type");
        var idLower = _GetString(version, "id").ToLowerInvariant();

        return type switch
        {
            "release" => McVersionCategory.Release,
            "special" => McVersionCategory.AprilFools,
            "snapshot" or "pending" => _ClassifySnapshotOrPending(version, idLower),
            _ => McVersionCategory.BeforeRelease
        };
    }

    public static DateTime GetReleaseTime(JsonObject version)
    {
        return _GetDateTime(version, "releaseTime");
    }

    private static McVersionCategory _ClassifySnapshotOrPending(JsonObject version, string idLower)
    {
        var category = McVersionCategory.Snapshot;

        if (
            idLower.StartsWith("1.") &&
            !idLower.Contains("combat") &&
            !idLower.Contains("rc") &&
            !idLower.Contains("experimental") &&
            idLower != "1.2" &&
            !idLower.Contains("pre")
        )
        {
            category = McVersionCategory.Release;
            version["type"] = "release";
        }

        return _TryMarkAprilFoolsVersion(version, idLower)
            ? McVersionCategory.AprilFools
            : category;
    }

    private static bool _TryMarkAprilFoolsVersion(JsonObject version, string idLower)
    {
        switch (idLower)
        {
            case "2point0_blue":
            case "2point0_red":
            case "2point0_purple":
            case "2.0_blue":
            case "2.0_red":
            case "2.0_purple":
            case "2.0":
                version["id"] = _GetString(version, "id").Replace("point", ".");
                _MarkAsAprilFools(version, true);
                return true;

            case "20w14infinite":
            case "20w14∞":
                version["id"] = "20w14∞";
                _MarkAsAprilFools(version, true);
                return true;

            case "3d shareware v1.34":
            case "1.rv-pre1":
            case "15w14a":
            case "22w13oneblockatatime":
            case "23w13a_or_b":
            case "24w14potato":
            case "25w14craftmine":
            case "26w14a":
                _MarkAsAprilFools(version, true);
                return true;

            default:
                var releaseDate = GetReleaseTime(version).ToUniversalTime().AddHours(2d);
                if (releaseDate is not { Month: 4, Day: 1 }) return false;
                _MarkAsAprilFools(version, false);
                return true;
        }
    }

    private static void _MarkAsAprilFools(JsonObject version, bool addLore)
    {
        version["type"] = "special";

        if (addLore)
            version["lore"] = GetMcFoolName(_GetString(version, "id"));
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