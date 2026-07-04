// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Application.Launching;
using PCL.Application.Minecraft.Launch.Natives;

namespace PCL.Application.Test;

[TestClass]
public sealed class MinecraftLaunchScriptServiceTests
{
    [TestMethod]
    public void CreateScript_WindowsScriptUsesWpfBatchShape()
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = "java",
            Arguments = "-Xmx2G -jar minecraft.jar",
            WorkingDirectory = "C:\\Games\\Minecraft"
        };

        string script = MinecraftLaunchScriptService.CreateScript(startInfo, windowsScript: true);

        StringAssert.StartsWith(script, "@echo off");
        StringAssert.Contains(script, "cd /d \"C:\\Games\\Minecraft\"");
        StringAssert.Contains(script, "\"java\" -Xmx2G -jar minecraft.jar");
        StringAssert.Contains(script, "pause");
    }

    [TestMethod]
    public async Task SaveAsync_WritesScriptFile()
    {
        string root = Path.Combine(Path.GetTempPath(), "pcl-launch-script-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string target = Path.Combine(root, "launch.sh");

        try
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = "java",
                Arguments = "-version",
                WorkingDirectory = root
            };
            MinecraftProcessLaunchPlan plan = new(
                startInfo,
                Path.Combine(root, "natives"),
                [],
                new MinecraftNativeExtractionResult([], [], [], []));

            await MinecraftLaunchScriptService.SaveAsync(
                new MinecraftLaunchScriptRequest
                {
                    LaunchPlan = plan,
                    TargetPath = target,
                    PauseOnExit = false
                });

            Assert.IsTrue(File.Exists(target));
            StringAssert.Contains(await File.ReadAllTextAsync(target), "exec 'java' -version");
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
