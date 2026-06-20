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

bool valid =
    result.Arguments.Contains("-Xmx4096m", StringComparison.Ordinal) &&
    result.Arguments.Contains("-Dlinux=true", StringComparison.Ordinal) &&
    result.Arguments.Contains("net.minecraft.client.main.Main", StringComparison.Ordinal) &&
    result.Arguments.Contains("--username Steve", StringComparison.Ordinal) &&
    result.Arguments.Contains("-Dfile.encoding=COMPAT", StringComparison.Ordinal);

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
    valid &= expectedSettings == loadedSettings.Settings;
}
finally
{
    if (Directory.Exists(settingsDirectory))
        Directory.Delete(settingsDirectory, recursive: true);
}

return valid ? 0 : 1;
