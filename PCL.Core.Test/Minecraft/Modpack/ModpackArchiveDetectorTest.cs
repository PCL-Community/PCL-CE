using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Modpack;

namespace PCL.Core.Test.Minecraft.Modpack;

[TestClass]
public class ModpackArchiveDetectorTest
{
    private const string ManifestWithoutAddons =
        """{"manifestType":"minecraftModpack","name":"Test","minecraft":{"version":"1.20.1"}}""";

    private const string ManifestWithAddons =
        """{"manifestType":"minecraftModpack","name":"Test","addons":[{"id":"game","version":"1.20.1"}]}""";

    [TestMethod]
    public void DetectsCurseForgeAtRoot()
    {
        var archive = new FakeModpackArchive(
            new KeyValuePair<string, string>("manifest.json", ManifestWithoutAddons),
            new KeyValuePair<string, string>("overrides/mods/Test.jar", ""));
        var result = ModpackArchiveDetector.Detect(archive);
        Assert.AreEqual(ModpackFormat.CurseForge, result.Format);
        Assert.AreEqual("", result.ArchiveBaseFolder);
    }

    [TestMethod]
    public void DetectsCurseForgeInSubfolder()
    {
        var archive = new FakeModpackArchive(
            new KeyValuePair<string, string>("MyPack/manifest.json", ManifestWithoutAddons),
            new KeyValuePair<string, string>("MyPack/overrides/mods/Test.jar", ""));
        var result = ModpackArchiveDetector.Detect(archive);
        Assert.AreEqual(ModpackFormat.CurseForge, result.Format);
        Assert.AreEqual("MyPack/", result.ArchiveBaseFolder);
    }

    [TestMethod]
    public void DetectsModrinthAtRoot()
    {
        var archive = new FakeModpackArchive("modrinth.index.json", "overrides/mods/Test.jar");
        var result = ModpackArchiveDetector.Detect(archive);
        Assert.AreEqual(ModpackFormat.Modrinth, result.Format);
        Assert.AreEqual("", result.ArchiveBaseFolder);
    }

    [TestMethod]
    public void DetectsMultiMcAtRoot()
    {
        var archive = new FakeModpackArchive("mmc-pack.json", "instance.cfg", "patches/net.minecraft.json",
            ".minecraft/mods/Test.jar");
        var result = ModpackArchiveDetector.Detect(archive);
        Assert.AreEqual(ModpackFormat.MultiMc, result.Format);
        Assert.AreEqual("", result.ArchiveBaseFolder);
    }

    [TestMethod]
    public void DetectsMcbbsByPackmeta()
    {
        var archive = new FakeModpackArchive("mcbbs.packmeta", "manifest.json");
        var result = ModpackArchiveDetector.Detect(archive);
        Assert.AreEqual(ModpackFormat.Mcbbs, result.Format);
    }

    [TestMethod]
    public void DetectsMcbbsByManifestWithAddons()
    {
        var archive = new FakeModpackArchive(
            new KeyValuePair<string, string>("manifest.json", ManifestWithAddons));
        var result = ModpackArchiveDetector.Detect(archive);
        Assert.AreEqual(ModpackFormat.Mcbbs, result.Format);
    }

    [TestMethod]
    public void DetectsHmclAtRoot()
    {
        var archive = new FakeModpackArchive("modpack.json", "minecraft/mods/Test.jar");
        var result = ModpackArchiveDetector.Detect(archive);
        Assert.AreEqual(ModpackFormat.Hmcl, result.Format);
    }

    [TestMethod]
    public void DetectsLauncherPackByNestedZipAtRoot()
    {
        var archive = new FakeModpackArchive("modpack.zip");
        var result = ModpackArchiveDetector.Detect(archive);
        Assert.AreEqual(ModpackFormat.LauncherPack, result.Format);
    }

    [TestMethod]
    public void DetectsLauncherPackByNestedMrpackInSubfolder()
    {
        var archive = new FakeModpackArchive("SomeFolder/modpack.mrpack", "SomeFolder/README.txt");
        var result = ModpackArchiveDetector.Detect(archive);
        Assert.AreEqual(ModpackFormat.LauncherPack, result.Format);
        Assert.AreEqual("SomeFolder/", result.ArchiveBaseFolder);
    }

    [TestMethod]
    public void DetectsLazyPackByMinecraftVersions()
    {
        var archive = new FakeModpackArchive(".minecraft/versions/1.20.1/1.20.1.json",
            ".minecraft/mods/Test.jar", "README.txt");
        var result = ModpackArchiveDetector.Detect(archive);
        Assert.AreEqual(ModpackFormat.LazyPack, result.Format);
    }

    [TestMethod]
    public void DetectsRootLayoutLazyPack()
    {
        var archive = new FakeModpackArchive("versions/1.20.1/1.20.1.json", "mods/Test.jar", "README.txt");
        var result = ModpackArchiveDetector.Detect(archive);
        Assert.AreEqual(ModpackFormat.LazyPack, result.Format);
    }

    [TestMethod]
    public void ReturnsUnknownForUnrecognizedArchive()
    {
        var archive = new FakeModpackArchive("README.txt", "data/something.dat");
        var result = ModpackArchiveDetector.Detect(archive);
        Assert.AreEqual(ModpackFormat.Unknown, result.Format);
    }

    [TestMethod]
    public void DetectsDeepNestedModrinthAsLauncherPack()
    {
        var archive = new FakeModpackArchive("外层/整合包/modrinth.index.json", "外层/说明.txt");
        var result = ModpackArchiveDetector.Detect(archive);
        Assert.AreEqual(ModpackFormat.LauncherPack, result.Format);
    }

    [TestMethod]
    public void DetectsDeepNestedMultiMcAsLauncherPack()
    {
        var archive = new FakeModpackArchive("外层/子目录/mmc-pack.json", "外层/启动器.exe");
        var result = ModpackArchiveDetector.Detect(archive);
        Assert.AreEqual(ModpackFormat.LauncherPack, result.Format);
    }

    [TestMethod]
    public void DetectsDeepNestedCurseForgeAsLauncherPack()
    {
        var archive = new FakeModpackArchive(
            new KeyValuePair<string, string>("外层/整合包/manifest.json", ManifestWithoutAddons),
            new KeyValuePair<string, string>("外层/启动器.exe", ""));
        var result = ModpackArchiveDetector.Detect(archive);
        Assert.AreEqual(ModpackFormat.LauncherPack, result.Format);
    }

    [TestMethod]
    public void DeepModManifestDoesNotCountAsModpack()
    {
        // Fabric 模组的 manifest.json 不含 minecraft 字段，不应被误判为整合包
        const string modManifest =
            """{"schemaVersion":1,"id":"example_mod","version":"1.0.0","depends":{"minecraft":">=1.20"}}""";
        var archive = new FakeModpackArchive(
            new KeyValuePair<string, string>("mods/ExampleMod/manifest.json", modManifest),
            new KeyValuePair<string, string>("README.txt", ""));
        var result = ModpackArchiveDetector.Detect(archive);
        Assert.AreEqual(ModpackFormat.Unknown, result.Format);
    }

    [TestMethod]
    public void DeepNestedModpackZipAsLauncherPack()
    {
        var archive = new FakeModpackArchive("外层/子目录/modpack.mrpack", "外层/说明.txt");
        var result = ModpackArchiveDetector.Detect(archive);
        Assert.AreEqual(ModpackFormat.LauncherPack, result.Format);
    }

    [TestMethod]
    public void RootManifestTakesPriorityOverSubfolderFiles()
    {
        var archive = new FakeModpackArchive(
            new KeyValuePair<string, string>("manifest.json", ManifestWithoutAddons),
            new KeyValuePair<string, string>("SubFolder/modrinth.index.json", ""));
        var result = ModpackArchiveDetector.Detect(archive);
        Assert.AreEqual(ModpackFormat.CurseForge, result.Format);
    }
}
