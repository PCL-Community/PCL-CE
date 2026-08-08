using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Modpack;
using PCL.Core.Utils;

namespace PCL.Core.Test.Minecraft.Modpack;

[TestClass]
public class CurseForgeManifestTest
{
    [TestMethod]
    public void ParsesCompleteManifest()
    {
        const string json = """
            {
              "manifestType": "minecraftModpack",
              "manifestVersion": 1,
              "name": "示例整合包",
              "version": "1.0.0",
              "recommendedRam": 8192,
              "author": "作者名",
              "minecraft": {
                "version": "1.20.1",
                "modLoaders": [ { "id": "forge-47.2.0", "primary": true } ]
              },
              "files": [
                { "projectID": 238222, "fileID": 5246076, "required": true },
                { "projectID": 123456, "fileID": 789012, "required": false }
              ],
              "overrides": "overrides"
            }
            """;
        var manifest = CurseForgeManifest.Parse(JsonCompat.ParseNode(json));
        Assert.IsNotNull(manifest);
        Assert.AreEqual("minecraftModpack", manifest.ManifestType);
        Assert.AreEqual(1, manifest.ManifestVersion);
        Assert.AreEqual("示例整合包", manifest.Name);
        Assert.AreEqual("1.0.0", manifest.Version);
        Assert.AreEqual(8192, manifest.RecommendedRam);
        Assert.AreEqual("作者名", manifest.Author);
        Assert.AreEqual("1.20.1", manifest.Minecraft?.Version);
        Assert.IsNotNull(manifest.Minecraft?.ModLoaders);
        Assert.AreEqual(1, manifest.Minecraft.ModLoaders.Count);
        Assert.AreEqual("forge-47.2.0", manifest.Minecraft.ModLoaders[0].Id);
        Assert.IsTrue(manifest.Minecraft.ModLoaders[0].Primary);
        Assert.IsNotNull(manifest.Files);
        Assert.AreEqual(2, manifest.Files.Count);
        Assert.AreEqual(238222, manifest.Files[0].ProjectId);
        Assert.AreEqual(5246076, manifest.Files[0].FileId);
        Assert.IsFalse(manifest.Files[0].IsOptional);
        Assert.IsTrue(manifest.Files[1].IsOptional);
        Assert.AreEqual("overrides", manifest.Overrides);
    }

    [TestMethod]
    public void RecommendedRamPrefersRootOverMinecraft()
    {
        const string json = """
            {
              "recommendedRam": 4096,
              "minecraft": {
                "version": "1.20.1",
                "recommendedRam": 8192
              }
            }
            """;
        var manifest = CurseForgeManifest.Parse(JsonCompat.ParseNode(json));
        Assert.IsNotNull(manifest);
        Assert.AreEqual(4096, manifest.RecommendedRam);
        Assert.AreEqual(8192, manifest.Minecraft?.RecommendedRam);
        Assert.AreEqual(4096, manifest.RecommendedRamEffective);
    }

    [TestMethod]
    public void RecommendedRamFromMinecraftWhenRootMissing()
    {
        const string json = """{"minecraft": { "version": "1.20.1", "recommendedRam": 8192 }}""";
        var manifest = CurseForgeManifest.Parse(JsonCompat.ParseNode(json));
        Assert.IsNotNull(manifest);
        Assert.IsNull(manifest.RecommendedRam);
        Assert.AreEqual(8192, manifest.Minecraft?.RecommendedRam);
        Assert.AreEqual(8192, manifest.RecommendedRamEffective);
    }

    [TestMethod]
    public void MissingRequiredDefaultsToRequired()
    {
        const string json = """{"files":[{"projectID":1,"fileID":2}]}""";
        var manifest = CurseForgeManifest.Parse(JsonCompat.ParseNode(json));
        Assert.IsNotNull(manifest);
        Assert.IsNotNull(manifest.Files);
        Assert.IsFalse(manifest.Files[0].IsOptional);
    }

    [TestMethod]
    public void MissingOptionalFieldsStayNull()
    {
        const string json = """{"name":"Test"}""";
        var manifest = CurseForgeManifest.Parse(JsonCompat.ParseNode(json));
        Assert.IsNotNull(manifest);
        Assert.AreEqual("Test", manifest.Name);
        Assert.IsNull(manifest.Minecraft);
        Assert.IsNull(manifest.Files);
        Assert.IsNull(manifest.Overrides);
        Assert.IsNull(manifest.Version);
        Assert.IsNull(manifest.RecommendedRam);
        Assert.IsNull(manifest.RecommendedRamEffective);
    }

    [TestMethod]
    public void ParseNullReturnsNull()
    {
        Assert.IsNull(CurseForgeManifest.Parse(null));
    }
}
