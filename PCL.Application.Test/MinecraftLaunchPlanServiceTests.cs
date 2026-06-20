// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Application.Minecraft.Launch.Arguments;

namespace PCL.Application.Test;

[TestClass]
public sealed class MinecraftLaunchPlanServiceTests
{
    [TestMethod]
    public void CreatePlan_ShouldBuildCompleteModernLaunchWithoutUiDependencies()
    {
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
                    ["${natives_directory}"] = "/home/user/.minecraft/natives",
                    ["${classpath}"] = "/home/user/.minecraft/libraries/client.jar",
                    ["${auth_player_name}"] = "Steve",
                    ["${game_directory}"] = "/home/user/.minecraft"
                },
                JavaMajorVersion = 21
            });

        StringAssert.Contains(result.Arguments, "-Xmx4096m");
        StringAssert.Contains(result.Arguments, "-Dlinux=true");
        StringAssert.Contains(result.Arguments, "net.minecraft.client.main.Main");
        StringAssert.Contains(result.Arguments, "--username Steve");
        StringAssert.Contains(result.Arguments, "--gameDir /home/user/.minecraft");
        StringAssert.Contains(result.Arguments, "-Dfile.encoding=COMPAT");
        Assert.AreEqual(OptiFineTweakerAdjustment.None, result.OptiFineTweakerAdjustment);
    }

    [TestMethod]
    public void BuildLegacyJvmArguments_ShouldPreservePlatformPrefixesAndSuffixes()
    {
        MinecraftJvmArgumentResult result = MinecraftJvmArgumentService.Build(
            new MinecraftJvmArgumentRequest
            {
                VersionJson = new JsonObject(),
                RuleContext = new MinecraftArgumentRuleContext
                {
                    OperatingSystem = MinecraftArgumentOperatingSystem.Win32,
                    OperatingSystemVersion = "10.0",
                    Is32BitArchitecture = false
                },
                MainClass = "net.minecraft.client.Minecraft",
                MemoryMegabytes = 1024,
                NativesDirectory = @"C:\Minecraft\natives",
                PrefixArguments = ["-javaagent:authlib.jar"],
                SuffixArguments = ["-Dproxy=true"]
            });

        StringAssert.StartsWith(result.Arguments, "-javaagent:authlib.jar");
        StringAssert.Contains(result.Arguments, "-Xmn153m");
        StringAssert.Contains(result.Arguments, "-Xmx1024m");
        StringAssert.Contains(result.Arguments, "-cp ${classpath}");
        StringAssert.EndsWith(result.Arguments, "-Dproxy=true net.minecraft.client.Minecraft");
    }
}
