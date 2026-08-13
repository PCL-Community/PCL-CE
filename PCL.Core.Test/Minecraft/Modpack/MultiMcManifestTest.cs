using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Modpack;
using PCL.Core.Utils;

namespace PCL.Core.Test.Minecraft.Modpack;

[TestClass]
public class MultiMcManifestTest
{
    [TestMethod]
    public void ParsesComponents()
    {
        const string json = """
            {
              "formatVersion": 1,
              "components": [
                { "cachedName": "Minecraft", "cachedVolatile": true, "uid": "net.minecraft", "version": "1.20.1" },
                { "uid": "net.minecraftforge", "version": "47.2.0" }
              ]
            }
            """;
        var manifest = MultiMcManifest.Parse(JsonCompat.ParseNode(json));
        Assert.IsNotNull(manifest);
        Assert.AreEqual(1, manifest.FormatVersion);
        Assert.IsNotNull(manifest.Components);
        Assert.AreEqual(2, manifest.Components.Count);
        Assert.AreEqual("net.minecraft", manifest.Components[0].Uid);
        Assert.AreEqual("1.20.1", manifest.Components[0].Version);
        Assert.AreEqual("Minecraft", manifest.Components[0].CachedName);
        Assert.IsTrue(manifest.Components[0].CachedVolatile);
        Assert.AreEqual("net.minecraftforge", manifest.Components[1].Uid);
        Assert.AreEqual("47.2.0", manifest.Components[1].Version);
        Assert.IsFalse(manifest.Components[1].CachedVolatile);
    }

    [TestMethod]
    public void MissingComponentsStayNull()
    {
        var manifest = MultiMcManifest.Parse(JsonCompat.ParseNode("""{"formatVersion":1}"""));
        Assert.IsNotNull(manifest);
        Assert.IsNull(manifest.Components);
    }

    [TestMethod]
    public void ParseInstanceNameHandlesCrLf()
    {
        const string cfg = "InstanceType=OneSix\r\nname=My Instance\r\niconKey=default\r\n";
        Assert.AreEqual("My Instance", MultiMcManifest.ParseInstanceName(cfg));
    }

    [TestMethod]
    public void ParseInstanceNameHandlesLfAndSpaces()
    {
        const string cfg = "InstanceType=OneSix\nname = Spaced Name\n";
        Assert.AreEqual("Spaced Name", MultiMcManifest.ParseInstanceName(cfg));
    }

    [TestMethod]
    public void ParseInstanceNameIgnoresCaseAndOtherKeys()
    {
        const string cfg = "Name=UpperCase\niconKey=default\n";
        Assert.AreEqual("UpperCase", MultiMcManifest.ParseInstanceName(cfg));
    }

    [TestMethod]
    public void ParseInstanceNameReturnsNullWhenMissing()
    {
        Assert.IsNull(MultiMcManifest.ParseInstanceName("InstanceType=OneSix\ntotalTimePlayed=100"));
        Assert.IsNull(MultiMcManifest.ParseInstanceName(null));
        Assert.IsNull(MultiMcManifest.ParseInstanceName(""));
    }

    [TestMethod]
    public void ParseNullReturnsNull()
    {
        Assert.IsNull(MultiMcManifest.Parse(null));
    }
}
