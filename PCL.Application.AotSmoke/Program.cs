// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json.Nodes;
using PCL.Application.Minecraft.Launch.Arguments;
using PCL.Application.Settings;

JsonObject versionJson = JsonNode.Parse(
    """
    {
      "mainClass": "net.minecraft.client.main.Main",
      "arguments": {
        "jvm": [
          "-Djava.library.path=${natives_directory}",
          "-cp",
          "${classpath}",
          {
            "rules": [{ "action": "allow", "os": { "name": "linux" } }],
            "value": "-Dlinux=true"
          }
        ],
        "game": [
          "--username",
          "${auth_player_name}",
          "--gameDir",
          "${game_directory}"
        ]
      }
    }
    """)!.AsObject();
var ruleContext = new MinecraftArgumentRuleContext
{
    OperatingSystem = MinecraftArgumentOperatingSystem.Linux,
    OperatingSystemVersion = "6.8",
    Is32BitArchitecture = false
};

MinecraftLaunchPlanResult result = MinecraftLaunchPlanService.CreatePlan(
    new MinecraftLaunchPlanRequest
    {
        Jvm = new MinecraftJvmArgumentRequest
        {
            VersionJson = versionJson,
            RuleContext = ruleContext,
            MainClass = "net.minecraft.client.main.Main",
            UseModernArguments = true,
            MemoryMegabytes = 4096,
            PreferredIpStack = MinecraftJvmIpPreference.PreferV6,
            CustomJvmArguments = "-XX:+UseG1GC"
        },
        ModernGame = new MinecraftModernGameArgumentRequest
        {
            VersionJson = versionJson,
            RuleContext = ruleContext
        },
        Replacements = new Dictionary<string, string>
        {
            ["${natives_directory}"] = "/home/pcl/.minecraft/natives",
            ["${classpath}"] = "/home/pcl/.minecraft/libraries/client.jar",
            ["${auth_player_name}"] = "Steve",
            ["${game_directory}"] = "/home/pcl/.minecraft"
        },
        JavaMajorVersion = 21
    });

List<string> failures = [];
Require(result.Arguments.Contains("-Xmx4096m", StringComparison.Ordinal), "missing -Xmx4096m");
Require(result.Arguments.Contains("-Dlinux=true", StringComparison.Ordinal), "missing linux rule argument");
Require(result.Arguments.Contains("net.minecraft.client.main.Main", StringComparison.Ordinal), "missing main class");
Require(result.Arguments.Contains("--username Steve", StringComparison.Ordinal), "missing username game argument");
Require(result.Arguments.Contains("-Dfile.encoding=COMPAT", StringComparison.Ordinal), "missing Java 18+ encoding argument");

string settingsDirectory = Path.Combine(
    Path.GetTempPath(),
    "pcl-application-aot-" + Guid.NewGuid().ToString("N"));
try
{
    using LauncherSettingsStore settingsStore = new(
        Path.Combine(settingsDirectory, "settings.json"));
    LauncherSettings expectedSettings = new()
    {
        AutomaticallyRepairGameIssues = false,
        DownloadSource = DownloadSourcePreference.OfficialOnly
    };
    await settingsStore.SaveAsync(expectedSettings);
    LauncherSettingsLoadResult loadedSettings = await settingsStore.LoadAsync();
    Require(LauncherSettingsMatch(expectedSettings, loadedSettings.Settings), "launcher settings round-trip mismatch");
}
finally
{
    if (Directory.Exists(settingsDirectory))
        Directory.Delete(settingsDirectory, recursive: true);
}

if (failures.Count == 0)
    return 0;

Console.Error.WriteLine("PCL.Application.AotSmoke failed:");
foreach (string failure in failures)
    Console.Error.WriteLine("- " + failure);
Console.Error.WriteLine("Arguments: " + result.Arguments);
return 1;

void Require(bool condition, string message)
{
    if (!condition)
        failures.Add(message);
}

static bool LauncherSettingsMatch(LauncherSettings expected, LauncherSettings actual) =>
    expected.SchemaVersion == actual.SchemaVersion &&
    expected.AutomaticallyRepairGameIssues == actual.AutomaticallyRepairGameIssues &&
    expected.ColorMode == actual.ColorMode &&
    expected.LightColor == actual.LightColor &&
    expected.DarkColor == actual.DarkColor &&
    expected.DownloadSource == actual.DownloadSource &&
    DictionaryEquals(expected.BooleanOptions, actual.BooleanOptions) &&
    DictionaryEquals(expected.IntegerOptions, actual.IntegerOptions) &&
    DictionaryEquals(expected.TextOptions, actual.TextOptions);

static bool DictionaryEquals<TKey, TValue>(
    IReadOnlyDictionary<TKey, TValue> expected,
    IReadOnlyDictionary<TKey, TValue> actual)
    where TKey : notnull
{
    if (expected.Count != actual.Count)
        return false;

    foreach ((TKey key, TValue value) in expected)
    {
        if (!actual.TryGetValue(key, out TValue? actualValue) ||
            !EqualityComparer<TValue>.Default.Equals(value, actualValue))
        {
            return false;
        }
    }

    return true;
}
