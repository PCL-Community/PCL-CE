// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Application.Launching;

namespace PCL.Application.Test;

[TestClass]
public sealed class MinecraftProcessLaunchServiceTests
{
    [TestMethod]
    public async Task CreatePlanAsync_AppliesInstanceLaunchOverrides()
    {
        string root = Path.Combine(Path.GetTempPath(), "pcl-launch-plan-" + Guid.NewGuid().ToString("N"));
        string instanceDirectory = Path.Combine(root, "versions", "CustomPack");
        string versionJsonPath = Path.Combine(instanceDirectory, "CustomPack.json");
        string versionJarPath = Path.Combine(instanceDirectory, "CustomPack.jar");
        string classpathHead = Path.Combine(root, "custom-head.jar");

        try
        {
            Directory.CreateDirectory(instanceDirectory);
            await File.WriteAllTextAsync(
                versionJsonPath,
                """
                {
                  "mainClass": "net.minecraft.client.main.Main",
                  "arguments": {
                    "jvm": [
                      "-cp",
                      "${classpath}"
                    ],
                    "game": [
                      "--username",
                      "${auth_player_name}",
                      "--gameDir",
                      "${game_directory}"
                    ]
                  }
                }
                """);
            await File.WriteAllTextAsync(versionJarPath, string.Empty);

            MinecraftProcessLaunchPlan plan = await MinecraftProcessLaunchService.CreatePlanAsync(
                new MinecraftProcessLaunchRequest
                {
                    VersionId = "CustomPack",
                    VersionJsonPath = versionJsonPath,
                    InstanceDirectory = instanceDirectory,
                    MinecraftRootDirectory = root,
                    PlayerName = "Steve",
                    PlayerUuid = "00000000000000000000000000000000",
                    JavaExecutablePath = "java",
                    MemoryMegabytes = 3072,
                    IsolatedGameDirectory = true,
                    CustomJvmArguments = "-XX:+UseZGC",
                    CustomGameArguments = "--demo",
                    ClasspathHeadEntries = [classpathHead],
                    AuthlibInjectorPath = Path.Combine(root, "authlib-injector.jar"),
                    AuthlibServer = "https://example.com/api/yggdrasil",
                    AuthlibPrefetchedMetadata = "{}",
                    Server = "play.example.com",
                    ReleaseTime = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)
                });

            Assert.AreEqual(instanceDirectory, plan.StartInfo.WorkingDirectory);
            StringAssert.Contains(plan.StartInfo.Arguments, "-Xmx3072m");
            StringAssert.Contains(plan.StartInfo.Arguments, "-XX:+UseZGC");
            StringAssert.Contains(plan.StartInfo.Arguments, "-javaagent:");
            StringAssert.Contains(plan.StartInfo.Arguments, "authlib-injector.jar=https://example.com/api/yggdrasil");
            StringAssert.Contains(plan.StartInfo.Arguments, "-Dauthlibinjector.yggdrasil.prefetched=e30=");
            StringAssert.Contains(plan.StartInfo.Arguments, "--demo");
            StringAssert.Contains(plan.StartInfo.Arguments, "--quickPlayMultiplayer \"play.example.com\"");
            CollectionAssert.AreEqual(new[] { classpathHead, versionJarPath }, plan.ClasspathEntries.ToArray());
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
