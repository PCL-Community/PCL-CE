// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO.Compression;
using System.Text;
using PCL.Application.Downloads;

namespace PCL.Application.Test;

[TestClass]
public sealed class MinecraftModArchiveInstallerTests
{
    [TestMethod]
    public void Install_DisablesOlderFabricModWithSameId()
    {
        string root = CreateRoot();
        try
        {
            string mods = Path.Combine(root, "mods");
            Directory.CreateDirectory(mods);
            string oldPath = Path.Combine(mods, "fabric-api-0.154.3+26.3.jar");
            string incoming = Path.Combine(root, "download.jar");
            CreateFabricMod(oldPath, "fabric-api", "0.154.3+26.3");
            CreateFabricMod(incoming, "fabric-api", "0.154.2+26.2");

            string installed = MinecraftModArchiveInstaller.Install(
                incoming,
                mods,
                "fabric-api-0.154.2+26.2.jar");

            Assert.IsTrue(File.Exists(installed));
            Assert.IsFalse(File.Exists(oldPath));
            Assert.IsTrue(File.Exists(oldPath + ".disabled"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Install_LeavesDifferentFabricModEnabled()
    {
        string root = CreateRoot();
        try
        {
            string mods = Path.Combine(root, "mods");
            Directory.CreateDirectory(mods);
            string otherPath = Path.Combine(mods, "sodium.jar");
            string incoming = Path.Combine(root, "download.jar");
            CreateFabricMod(otherPath, "sodium", "1.0");
            CreateFabricMod(incoming, "fabric-api", "0.154.2+26.2");

            MinecraftModArchiveInstaller.Install(incoming, mods, "fabric-api.jar");

            Assert.IsTrue(File.Exists(otherPath));
            Assert.IsFalse(File.Exists(otherPath + ".disabled"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "pcln-mod-install-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void CreateFabricMod(string path, string id, string version)
    {
        using ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create);
        ZipArchiveEntry entry = archive.CreateEntry("fabric.mod.json");
        using Stream stream = entry.Open();
        using StreamWriter writer = new(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write($$"""{"id":"{{id}}","version":"{{version}}"}""");
    }
}
