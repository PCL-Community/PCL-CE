namespace PCL.Core.Minecraft;

public static class McFormatter {
    public static string GetWikiUrlSuffix(string gameVersion) {
        var formattedVersion = FormatVersion(gameVersion);

        if (gameVersion.Contains('w')) return formattedVersion;

        return "Java版" + formattedVersion;
    }

    public static string FormatVersion(string gameVersion) {
        var id = gameVersion.ToLowerInvariant();

        switch (id) {
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

        if (id.StartsWith("1.0.0-rc2")) return "RC2";
        if (id.StartsWith("2.0") || id.StartsWith("2point0")) return "2.0";
        if (id.StartsWith("b1.8-pre1")) return "Beta_1.8-pre1";
        if (id.StartsWith("b1.1-")) return "Beta_1.1";
        if (id.StartsWith("a1.1.0")) return "Alpha_v1.1.0";
        if (id.StartsWith("a1.0.14")) return "Alpha_v1.0.14";
        if (id.StartsWith("a1.0.13_01")) return "Alpha_v1.0.13_01";
        if (id.StartsWith("in-20100214")) return "Indev_20100214";

        if (id.Contains("experimental-snapshot")) {
            return id.Replace("_experimental-snapshot-", "-exp");
        }

        if (id.StartsWith("inf-")) return id.Replace("inf-", "Infdev_");
        if (id.StartsWith("in-")) return id.Replace("in-", "Indev_");
        if (id.StartsWith("rd-")) return "pre-Classic_" + id;
        if (id.StartsWith('b')) return id.Replace("b", "Beta_");
        if (id.StartsWith('a')) return id.Replace("a", "Alpha_v");

        return id.StartsWith('c') ? id.Replace("c", "Classic_").Replace("st", "SURVIVAL_TEST") : id;
    }
}
