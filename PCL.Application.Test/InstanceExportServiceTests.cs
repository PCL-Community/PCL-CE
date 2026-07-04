// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO.Compression;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Application.Instances;

namespace PCL.Application.Test;

[TestClass]
public sealed class InstanceExportServiceTests
{
    [TestMethod]
    public async Task ExportAsync_AppliesIncludeExcludeRulesAndKeepsInstanceCore()
    {
        string root = Path.Combine(Path.GetTempPath(), "pcl-instance-export-" + Guid.NewGuid().ToString("N"));
        string versionDirectory = Path.Combine(root, "versions", "1.20.1");
        string archivePath = Path.Combine(root, "export.zip");

        try
        {
            Directory.CreateDirectory(versionDirectory);
            Directory.CreateDirectory(Path.Combine(root, "mods"));
            await File.WriteAllTextAsync(Path.Combine(root, "options.txt"), "settings");
            await File.WriteAllTextAsync(Path.Combine(root, "mods", "enabled.jar"), "enabled");
            await File.WriteAllTextAsync(Path.Combine(root, "mods", "disabled.disabled"), "disabled");
            await File.WriteAllTextAsync(Path.Combine(versionDirectory, "1.20.1.json"), "{}");

            await InstanceExportService.ExportAsync(
                new InstanceExportRequest
                {
                    InstanceDirectory = versionDirectory,
                    GameDirectory = root,
                    TargetArchivePath = archivePath,
                    Rules =
                    [
                        "options.txt",
                        "mods/",
                        "!mods/*.disabled"
                    ]
                });

            using ZipArchive archive = ZipFile.OpenRead(archivePath);
            Assert.IsNotNull(archive.GetEntry("options.txt"));
            Assert.IsNotNull(archive.GetEntry("mods/enabled.jar"));
            Assert.IsNull(archive.GetEntry("mods/disabled.disabled"));
            Assert.IsNotNull(archive.GetEntry("1.20.1/1.20.1.json"));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
