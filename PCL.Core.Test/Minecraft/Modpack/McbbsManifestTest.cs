using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Modpack;
using PCL.Core.Utils;

namespace PCL.Core.Test.Minecraft.Modpack;

[TestClass]
public class McbbsManifestTest
{
    [TestMethod]
    public void ParsesCompleteManifest()
    {
        const string json = """
            {
              "manifestType": "minecraftModpack",
              "name": "示例整合包",
              "version": "1.0.0",
              "addons": [
                { "id": "game", "version": "1.20.1" },
                { "id": "forge", "version": "47.2.0" }
              ],
              "launchInfo": {
                "javaArgument": ["-Xmx2G", "-XX:+UseG1GC"],
                "launchArgument": ["--username", "Player"]
              }
            }
            """;
        var manifest = McbbsManifest.Parse(JsonCompat.ParseNode(json));
        Assert.IsNotNull(manifest);
        Assert.AreEqual("示例整合包", manifest.Name);
        Assert.AreEqual("1.0.0", manifest.Version);
        Assert.IsNotNull(manifest.Addons);
        Assert.AreEqual(2, manifest.Addons.Count);
        Assert.AreEqual("game", manifest.Addons[0].Id);
        Assert.AreEqual("1.20.1", manifest.Addons[0].Version);
        Assert.AreEqual("forge", manifest.Addons[1].Id);
        Assert.AreEqual("47.2.0", manifest.Addons[1].Version);
        Assert.IsNotNull(manifest.LaunchInfo);
        Assert.AreEqual("-Xmx2G", manifest.LaunchInfo.JavaArgument?[0]?.ToString());
        Assert.AreEqual("-XX:+UseG1GC", manifest.LaunchInfo.JavaArgument?[1]?.ToString());
        Assert.AreEqual("--username", manifest.LaunchInfo.LaunchArgument?[0]?.ToString());
    }

    [TestMethod]
    public void MissingOptionalFieldsStayNull()
    {
        const string json = """{"name":"Test"}""";
        var manifest = McbbsManifest.Parse(JsonCompat.ParseNode(json));
        Assert.IsNotNull(manifest);
        Assert.AreEqual("Test", manifest.Name);
        Assert.IsNull(manifest.Addons);
        Assert.IsNull(manifest.LaunchInfo);
        Assert.IsNull(manifest.Version);
    }

    [TestMethod]
    public void SingleStringLaunchArgumentIsPreserved()
    {
        // 兼容个别包把 launchInfo 写成单个字符串而不是数组
        const string json = """{"launchInfo":{"javaArgument":"-Xmx2G"}}""";
        var manifest = McbbsManifest.Parse(JsonCompat.ParseNode(json));
        Assert.IsNotNull(manifest);
        Assert.IsNotNull(manifest.LaunchInfo);
        Assert.AreEqual("-Xmx2G", manifest.LaunchInfo.JavaArgument?.ToString());
    }

    [TestMethod]
    public void ParseNullReturnsNull()
    {
        Assert.IsNull(McbbsManifest.Parse(null));
    }
}
