// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Application.Minecraft.Launch.Arguments;

namespace PCL.Application.Test;

[TestClass]
public sealed class MinecraftLaunchArgumentServiceTests
{
    [TestMethod]
    public void LegacyGameArguments_ShouldAppendResolutionAndNormalizeOptiFineTweaker()
    {
        MinecraftGameArgumentResult result = MinecraftLaunchArgumentService.BuildLegacyGameArguments(
            new MinecraftLegacyGameArgumentRequest
            {
                MinecraftArguments = "--username ${auth_player_name} --tweakClass optifine.OptiFineTweaker",
                HasForge = true,
                HasOptiFine = true
            });

        Assert.AreEqual(OptiFineTweakerAdjustment.ReplacedPlainTweaker, result.OptiFineTweakerAdjustment);
        StringAssert.Contains(result.Arguments, "--height ${resolution_height} --width ${resolution_width}");
        StringAssert.EndsWith(result.Arguments, "--tweakClass optifine.OptiFineForgeTweaker");
    }

    [TestMethod]
    public void ModernGameArguments_ShouldApplyOsRulesAndMergeSwitchValues()
    {
        JsonObject versionJson = JsonNode.Parse(
            """
            {
              "arguments": {
                "game": [
                  "--username",
                  "${auth_player_name}",
                  {
                    "rules": [{ "action": "allow", "os": { "name": "windows" } }],
                    "value": ["--winOnly", "enabled"]
                  },
                  {
                    "rules": [{ "action": "allow", "features": { "is_quick_play_multiplayer": true } }],
                    "value": "--blockedQuickPlay"
                  }
                ]
              }
            }
            """)!.AsObject();

        MinecraftGameArgumentResult result = MinecraftLaunchArgumentService.BuildModernGameArguments(
            new MinecraftModernGameArgumentRequest
            {
                VersionJson = versionJson,
                RuleContext = new MinecraftArgumentRuleContext
                {
                    OperatingSystem = MinecraftArgumentOperatingSystem.Win32,
                    OperatingSystemVersion = "10.0.19045",
                    Is32BitArchitecture = false
                }
            });

        Assert.AreEqual("--username ${auth_player_name} --winOnly enabled", result.Arguments);
    }

    [TestMethod]
    public void FinalArguments_ShouldReplaceTokensAndRemoveEmptyVersionType()
    {
        MinecraftFinalArgumentResult result = MinecraftLaunchArgumentService.BuildFinalArguments(
            new MinecraftFinalArgumentRequest
            {
                Arguments = "${auth_player_name} --versionType ${version_type} --gameDir ${game_directory}",
                JavaMajorVersion = 17,
                Replacements = new Dictionary<string, string>
                {
                    ["${auth_player_name}"] = "Steve",
                    ["${version_type}"] = "",
                    ["${game_directory}"] = @"D:\Games\PCL Test"
                }
            });

        Assert.AreEqual(
            @"-Dstderr.encoding=UTF-8 -Dstdout.encoding=UTF-8 Steve --gameDir ""D:\Games\PCL Test""",
            result.Arguments);
    }

    [TestMethod]
    public void FinalArguments_ShouldUseQuickPlayForModernServerJoin()
    {
        MinecraftFinalArgumentResult result = MinecraftLaunchArgumentService.BuildFinalArguments(
            new MinecraftFinalArgumentRequest
            {
                Arguments = "--demo",
                JavaMajorVersion = 8,
                Replacements = new Dictionary<string, string>(),
                Server = "play.example.com",
                ReleaseTime = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)
            });

        Assert.AreEqual("--demo --quickPlayMultiplayer \"play.example.com\"", result.Arguments);
        Assert.IsFalse(result.ShouldWarnOptiFineAutoJoin);
    }

    [TestMethod]
    public void FinalArguments_ShouldUseLegacyServerArgumentsForOldVersions()
    {
        MinecraftFinalArgumentResult result = MinecraftLaunchArgumentService.BuildFinalArguments(
            new MinecraftFinalArgumentRequest
            {
                Arguments = "--demo",
                JavaMajorVersion = 8,
                Replacements = new Dictionary<string, string>(),
                Server = "play.example.com:25566",
                ReleaseTime = new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero),
                HasOptiFine = true
            });

        Assert.AreEqual("--demo --server play.example.com --port 25566", result.Arguments);
        Assert.IsTrue(result.ShouldWarnOptiFineAutoJoin);
    }
}
