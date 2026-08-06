using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Modpack;
using PCL.Core.Utils;

namespace PCL.Core.Test.Minecraft.Modpack;

[TestClass]
public class ModrinthManifestTest
{
    [TestMethod]
    public void ParsesCompleteManifest()
    {
        const string json = """
            {
              "formatVersion": 1,
              "game": "minecraft",
              "versionId": "1.0.0",
              "name": "示例整合包",
              "summary": "简短描述",
              "dependencies": {
                "minecraft": "1.21.1",
                "fabric-loader": "0.16.5"
              },
              "files": [
                {
                  "path": "mods/示例模组.jar",
                  "hashes": { "sha1": "abcd", "sha512": "efgh" },
                  "env": { "client": "required", "server": "unsupported" },
                  "downloads": [ "https://cdn.modrinth.com/示例模组.jar" ],
                  "fileSize": 1234567
                }
              ]
            }
            """;
        var manifest = ModrinthManifest.Parse(JsonCompat.ParseNode(json));
        Assert.IsNotNull(manifest);
        Assert.AreEqual(1, manifest.FormatVersion);
        Assert.AreEqual("minecraft", manifest.Game);
        Assert.AreEqual("1.0.0", manifest.VersionId);
        Assert.AreEqual("示例整合包", manifest.Name);
        Assert.AreEqual("简短描述", manifest.Summary);
        Assert.IsNotNull(manifest.Dependencies);
        Assert.AreEqual("1.21.1", manifest.Dependencies["minecraft"]);
        Assert.AreEqual("0.16.5", manifest.Dependencies["fabric-loader"]);
        Assert.IsNotNull(manifest.Files);
        Assert.AreEqual(1, manifest.Files.Count);
        var file = manifest.Files[0];
        Assert.AreEqual("mods/示例模组.jar", file.Path);
        Assert.AreEqual("abcd", file.Hashes?.Sha1);
        Assert.AreEqual("efgh", file.Hashes?.Sha512);
        Assert.AreEqual("required", file.Env?.Client);
        Assert.AreEqual("unsupported", file.Env?.Server);
        Assert.IsNotNull(file.Downloads);
        Assert.AreEqual(1, file.Downloads.Count);
        Assert.AreEqual(1234567, file.FileSize);
    }

    [TestMethod]
    public void MissingOptionalFieldsStayNull()
    {
        const string json = """{"formatVersion":1,"name":"Test"}""";
        var manifest = ModrinthManifest.Parse(JsonCompat.ParseNode(json));
        Assert.IsNotNull(manifest);
        Assert.AreEqual("Test", manifest.Name);
        Assert.IsNull(manifest.Dependencies);
        Assert.IsNull(manifest.Files);
        Assert.IsNull(manifest.Summary);
    }

    [TestMethod]
    public void CaseInsensitiveDependencyKeysArePreserved()
    {
        const string json = """{"dependencies":{"Minecraft":"1.20.1","FORGE":"47.2.0"}}""";
        var manifest = ModrinthManifest.Parse(JsonCompat.ParseNode(json));
        Assert.IsNotNull(manifest);
        Assert.IsNotNull(manifest.Dependencies);
        Assert.AreEqual("1.20.1", manifest.Dependencies["Minecraft"]);
        Assert.AreEqual("47.2.0", manifest.Dependencies["FORGE"]);
    }

    [TestMethod]
    public void ParseNullReturnsNull()
    {
        Assert.IsNull(ModrinthManifest.Parse(null));
    }
}
