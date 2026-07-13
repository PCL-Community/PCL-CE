// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Application.Downloads;

public enum MinecraftVersionCategory
{
    Release,
    Snapshot,
    BeforeRelease,
    AprilFools
}

public readonly record struct MinecraftVersionClassification(
    string Id,
    string Type,
    MinecraftVersionCategory Category,
    MinecraftAprilFoolsDescriptor? AprilFoolsDescriptor);

public readonly record struct MinecraftAprilFoolsDescriptor(
    string DescriptionResourceKey,
    string? TagResourceKey);

public static class MinecraftVersionCatalogClassifier
{
    public static MinecraftVersionClassification Classify(MinecraftVersionManifestEntry version)
    {
        ArgumentNullException.ThrowIfNull(version);

        string id = version.Id;
        string type = version.Type;
        string idLower = id.ToLowerInvariant();
        MinecraftVersionCategory category = type switch
        {
            "release" => MinecraftVersionCategory.Release,
            "special" => MinecraftVersionCategory.AprilFools,
            "snapshot" or "pending" => ClassifySnapshotOrPending(idLower, ref type),
            _ => MinecraftVersionCategory.BeforeRelease
        };

        if (TryMarkAprilFools(version, idLower, ref id, ref type, out MinecraftAprilFoolsDescriptor? descriptor))
            category = MinecraftVersionCategory.AprilFools;

        return new MinecraftVersionClassification(id, type, category, descriptor);
    }

    public static string FormatVersion(string gameVersion)
    {
        string id = gameVersion.ToLowerInvariant();

        switch (id)
        {
            case "0.30-1":
            case "0.30-2":
            case "c0.30_01c":
                return "Classic_0.30";
            case "in-20100206-2103":
                return "Indev_20100206";
            case "inf-20100630-1":
                return "Infdev_20100630";
            case "inf-20100630-2":
                return "Alpha_v1.0.0";
            case "1.19_deep_dark_experimental_snapshot-1":
                return "1.19-exp1";
            case "in-20100130":
                return "Indev_0.31_20100130";
            case "b1.6-tb3":
                return "Beta_1.6_Test_Build_3";
            case "1_14_combat-212796":
                return "1.14.3_-_Combat_Test";
            case "1_14_combat-0":
                return "Combat_Test_2";
            case "1_14_combat-3":
                return "Combat_Test_3";
            case "1_15_combat-1":
                return "Combat_Test_4";
            case "1_15_combat-6":
                return "Combat_Test_5";
            case "1_16_combat-0":
                return "Combat_Test_6";
            case "1_16_combat-1":
                return "Combat_Test_7";
            case "1_16_combat-2":
                return "Combat_Test_7b";
            case "1_16_combat-3":
                return "Combat_Test_7c";
            case "1_16_combat-4":
                return "Combat_Test_8";
            case "1_16_combat-5":
                return "Combat_Test_8b";
            case "1_16_combat-6":
                return "Combat_Test_8c";
        }

        if (id.StartsWith("1.0.0-rc2", StringComparison.Ordinal)) return "RC2";
        if (id.StartsWith("2.0", StringComparison.Ordinal) || id.StartsWith("2point0", StringComparison.Ordinal)) return "2.0";
        if (id.StartsWith("b1.8-pre1", StringComparison.Ordinal)) return "Beta_1.8-pre1";
        if (id.StartsWith("b1.1-", StringComparison.Ordinal)) return "Beta_1.1";
        if (id.StartsWith("a1.1.0", StringComparison.Ordinal)) return "Alpha_v1.1.0";
        if (id.StartsWith("a1.0.14", StringComparison.Ordinal)) return "Alpha_v1.0.14";
        if (id.StartsWith("a1.0.13_01", StringComparison.Ordinal)) return "Alpha_v1.0.13_01";
        if (id.StartsWith("in-20100214", StringComparison.Ordinal)) return "Indev_20100214";

        if (id.Contains("experimental-snapshot", StringComparison.Ordinal)) return id.Replace("_experimental-snapshot-", "-exp", StringComparison.Ordinal);
        if (id.StartsWith("inf-", StringComparison.Ordinal)) return "Infdev_" + id[4..];
        if (id.StartsWith("in-", StringComparison.Ordinal)) return "Indev_" + id[3..];
        if (id.StartsWith("rd-", StringComparison.Ordinal)) return "pre-Classic_" + id;
        if (id.StartsWith('b')) return "Beta_" + id[1..];
        if (id.StartsWith('a')) return "Alpha_v" + id[1..];

        return id.StartsWith('c') ? ("Classic_" + id[1..]).Replace("st", "SURVIVAL_TEST", StringComparison.Ordinal) : id;
    }

    private static MinecraftVersionCategory ClassifySnapshotOrPending(string idLower, ref string type)
    {
        if (idLower.StartsWith("1.", StringComparison.Ordinal) &&
            !idLower.Contains("combat", StringComparison.Ordinal) &&
            !idLower.Contains("rc", StringComparison.Ordinal) &&
            !idLower.Contains("experimental", StringComparison.Ordinal) &&
            idLower != "1.2" &&
            !idLower.Contains("pre", StringComparison.Ordinal))
        {
            type = "release";
            return MinecraftVersionCategory.Release;
        }

        return MinecraftVersionCategory.Snapshot;
    }

    private static bool TryMarkAprilFools(
        MinecraftVersionManifestEntry version,
        string idLower,
        ref string id,
        ref string type,
        out MinecraftAprilFoolsDescriptor? descriptor)
    {
        descriptor = idLower switch
        {
            "2point0_blue" or "2point0_red" or "2point0_purple" or "2.0_blue" or "2.0_red" or "2.0_purple" or "2.0"
                => new MinecraftAprilFoolsDescriptor("Minecraft.Fool.Description.2013", GetAprilFoolsColorTag(idLower)),
            "15w14a" => new MinecraftAprilFoolsDescriptor("Minecraft.Fool.Description.2015", null),
            "1.rv-pre1" => new MinecraftAprilFoolsDescriptor("Minecraft.Fool.Description.2016", null),
            "3d shareware v1.34" => new MinecraftAprilFoolsDescriptor("Minecraft.Fool.Description.2019", null),
            "20w14infinite" or "20w14∞" => new MinecraftAprilFoolsDescriptor("Minecraft.Fool.Description.2020", null),
            "22w13oneblockatatime" => new MinecraftAprilFoolsDescriptor("Minecraft.Fool.Description.2022", null),
            "23w13a_or_b" => new MinecraftAprilFoolsDescriptor("Minecraft.Fool.Description.2023", null),
            "24w14potato" => new MinecraftAprilFoolsDescriptor("Minecraft.Fool.Description.2024", null),
            "25w14craftmine" => new MinecraftAprilFoolsDescriptor("Minecraft.Fool.Description.2025", null),
            "26w14a" => new MinecraftAprilFoolsDescriptor("Minecraft.Fool.Description.2026", null),
            _ => null
        };

        // Mojang also ships ordinary releases on April 1st (for example 26.1.1).
        // Keep the date heuristic for snapshot-like entries, but never let it
        // override an explicit release type unless the version id is known.
        bool isAprilFools = descriptor is not null ||
                            (!type.Equals("release", StringComparison.OrdinalIgnoreCase) &&
                             IsReleasedOnAprilFoolsDay(version.ReleaseTime));
        if (!isAprilFools)
            return false;

        if (idLower is "2point0_blue" or "2point0_red" or "2point0_purple")
            id = id.Replace("point", ".", StringComparison.OrdinalIgnoreCase);
        else if (idLower is "20w14infinite" or "20w14∞")
            id = "20w14∞";

        type = "special";
        return true;
    }

    private static string? GetAprilFoolsColorTag(string idLower)
    {
        if (idLower.EndsWith("red", StringComparison.Ordinal))
            return "Minecraft.Fool.Tag.Red";
        if (idLower.EndsWith("blue", StringComparison.Ordinal))
            return "Minecraft.Fool.Tag.Blue";
        if (idLower.EndsWith("purple", StringComparison.Ordinal))
            return "Minecraft.Fool.Tag.Purple";
        return null;
    }

    private static bool IsReleasedOnAprilFoolsDay(DateTimeOffset? releaseTime)
    {
        if (releaseTime is not DateTimeOffset value)
            return false;

        DateTimeOffset adjusted = value.ToUniversalTime().AddHours(2d);
        return adjusted.Month == 4 && adjusted.Day == 1;
    }
}
