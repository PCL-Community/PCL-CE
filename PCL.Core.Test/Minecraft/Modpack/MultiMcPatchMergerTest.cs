using System.Linq;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Modpack.MultiMc;
using PCL.Core.Utils;

namespace PCL.Core.Test.Minecraft.Modpack;

/// <summary>
/// MultiMC JSON Patch 合并测试。
/// </summary>
[TestClass]
public class MultiMcPatchMergerTest
{
    [TestMethod]
    public void MergesLibrariesAndMainClass()
    {
        var result = MultiMcPatchMerger.Merge(
        [
            _Patch("""
                {
                  "uid": "a.b",
                  "version": "1.0",
                  "mainClass": "com.example.Main",
                  "libraries": [{ "name": "com.example:lib:1.0" }]
                }
                """),
            _Patch("""
                {
                  "uid": "c.d",
                  "version": "2.0",
                  "+libraries": [{ "name": "com.example:other:1.0" }]
                }
                """)
        ], selfContained: false);

        Assert.IsNotNull(result);
        Assert.AreEqual("com.example.Main", result.VersionJson["mainClass"]!.GetValue<string>());
        Assert.AreEqual(2, result.VersionJson["libraries"]!.AsArray().Count);
        CollectionAssert.AreEqual(new[] { "a.b", "c.d" }, result.AppliedComponentUids.ToArray());
    }

    /// <summary>
    /// 同一坐标的库以后应用者为准，版本号不参与去重标识。
    /// </summary>
    [TestMethod]
    public void LaterLibraryReplacesEarlierWithSameCoordinate()
    {
        var result = MultiMcPatchMerger.Merge(
        [
            _Patch("""{ "uid": "a", "libraries": [{ "name": "org.ow2.asm:asm:9.0" }] }"""),
            _Patch("""{ "uid": "b", "libraries": [{ "name": "org.ow2.asm:asm:9.6" }] }""")
        ], selfContained: false);

        Assert.IsNotNull(result);
        var libraries = result.VersionJson["libraries"]!.AsArray();
        Assert.AreEqual(1, libraries.Count);
        Assert.AreEqual("org.ow2.asm:asm:9.6", libraries[0]!["name"]!.GetValue<string>());
    }

    [TestMethod]
    public void RemovesLibrariesWithMinusPrefix()
    {
        var result = MultiMcPatchMerger.Merge(
        [
            _Patch("""{ "uid": "a", "libraries": [{ "name": "com.example:removeme:1.0" }] }"""),
            _Patch("""{ "uid": "b", "-libraries": [{ "name": "com.example:removeme:1.0" }] }""")
        ], selfContained: false);

        Assert.IsNull(result, "移除唯一的库后补丁应为空");
    }

    /// <summary>
    /// tweakClass 需要以游戏参数的形式传入。
    /// </summary>
    [TestMethod]
    public void ConvertsTweakersToGameArguments()
    {
        var result = MultiMcPatchMerger.Merge(
        [
            _Patch("""{ "uid": "a", "+tweakers": ["com.example.Tweaker"] }""")
        ], selfContained: false);

        Assert.IsNotNull(result);
        var game = result.VersionJson["arguments"]!["game"]!.AsArray()
            .Select(node => node!.GetValue<string>()).ToArray();

        CollectionAssert.AreEqual(new[] { "--tweakClass", "com.example.Tweaker" }, game);
        Assert.IsTrue(result.OverridesGameArguments);
    }

    /// <summary>
    /// 增量模式不得写入官方固定 JVM 参数，否则叠加后会出现两份 -cp。
    /// </summary>
    [TestMethod]
    public void IncrementalModeOmitsStandardJvmArguments()
    {
        var result = MultiMcPatchMerger.Merge(
        [
            _Patch("""{ "uid": "a", "+jvmArgs": ["-XX:+UseG1GC"] }""")
        ], selfContained: false);

        Assert.IsNotNull(result);
        var jvm = result.VersionJson["arguments"]!["jvm"]!.AsArray()
            .Select(node => node!.GetValue<string>()).ToArray();

        CollectionAssert.AreEqual(new[] { "-XX:+UseG1GC" }, jvm);
        Assert.IsFalse(result.ReplacesGameJson);
    }

    /// <summary>
    /// 自包含模式必须补齐官方固定 JVM 参数，否则实例缺少 classpath 与 natives 路径。
    /// </summary>
    [TestMethod]
    public void SelfContainedModeAddsStandardJvmArguments()
    {
        var result = MultiMcPatchMerger.Merge(
        [
            _Patch("""{ "uid": "a", "+jvmArgs": ["-XX:+UseG1GC"] }""")
        ], selfContained: true);

        Assert.IsNotNull(result);
        var jvm = result.VersionJson["arguments"]!["jvm"]!.AsArray()
            .Select(node => node!.GetValue<string>()).ToArray();

        CollectionAssert.Contains(jvm, "-cp");
        CollectionAssert.Contains(jvm, "${classpath}");
        Assert.AreEqual("-XX:+UseG1GC", jvm[^1]);
        Assert.IsTrue(result.ReplacesGameJson);
    }

    [TestMethod]
    public void MapsCompatibleJavaMajorsToJavaVersion()
    {
        var result = MultiMcPatchMerger.Merge(
        [
            _Patch("""{ "uid": "a", "compatibleJavaMajors": [8, 17, 21] }""")
        ], selfContained: false);

        Assert.IsNotNull(result);
        Assert.AreEqual(21, result.VersionJson["javaVersion"]!["majorVersion"]!.GetValue<int>());
        Assert.AreEqual("java-runtime-delta", result.VersionJson["javaVersion"]!["component"]!.GetValue<string>());
    }

    /// <summary>
    /// MultiMC 专有的库字段需翻译为官方 JSON 的等价写法。
    /// </summary>
    [TestMethod]
    public void TranslatesMultiMcLibraryFields()
    {
        var result = MultiMcPatchMerger.Merge(
        [
            _Patch("""
                {
                  "uid": "a",
                  "libraries": [{
                    "name": "com.example:lib:1.0",
                    "MMC-hint": "local",
                    "MMC-absoluteUrl": "https://example.com/lib.jar"
                  }]
                }
                """)
        ], selfContained: false);

        Assert.IsNotNull(result);
        var library = result.VersionJson["libraries"]!.AsArray()[0]!.AsObject();

        Assert.IsFalse(library.ContainsKey("MMC-hint"));
        Assert.IsFalse(library.ContainsKey("MMC-absoluteUrl"));
        Assert.AreEqual("local", library["hint"]!.GetValue<string>());
        Assert.AreEqual("https://example.com/lib.jar",
            library["downloads"]!["artifact"]!["url"]!.GetValue<string>());
    }

    [TestMethod]
    public void AppliesCustomPatchAfterLoaderWithoutLosingItsPosition()
    {
        var output = new JsonObject
        {
            ["mainClass"] = "net.minecraft.client.Main",
            ["libraries"] = new JsonArray
            {
                new JsonObject { ["name"] = "com.example:remove-me:1.0" }
            }
        };

        MultiMcPatchMerger.ApplyTo(output, JsonNode.Parse("""
            {
              "uid": "net.minecraftforge",
              "mainClass": "cpw.mods.bootstraplauncher.BootstrapLauncher",
              "+libraries": [{ "name": "com.example:forge:1.0" }]
            }
            """)!.AsObject());
        MultiMcPatchMerger.ApplyTo(output, JsonNode.Parse("""
            {
              "uid": "com.example.custom",
              "mainClass": "com.example.CustomMain",
              "-libraries": [{ "name": "com.example:remove-me:1.0" }]
            }
            """)!.AsObject());

        Assert.AreEqual("com.example.CustomMain", output["mainClass"]!.GetValue<string>());
        var libraries = output["libraries"]!.AsArray()
            .Select(node => node!["name"]!.GetValue<string>()).ToArray();
        CollectionAssert.AreEqual(new[] { "com.example:forge:1.0" }, libraries);
    }

    [TestMethod]
    public void PatchWithoutLibraryOperationsLeavesExistingLibrariesUntouched()
    {
        var first = new JsonObject
        {
            ["name"] = "com.example:shared:1.0",
            ["rules"] = new JsonArray
            {
                new JsonObject { ["action"] = "allow", ["os"] = new JsonObject { ["name"] = "windows" } }
            }
        };
        var second = new JsonObject
        {
            ["name"] = "com.example:shared:2.0",
            ["rules"] = new JsonArray
            {
                new JsonObject { ["action"] = "allow", ["os"] = new JsonObject { ["name"] = "linux" } }
            }
        };
        var output = new JsonObject
        {
            ["libraries"] = new JsonArray(first.DeepClone(), second.DeepClone())
        };
        var expected = output["libraries"]!.DeepClone();

        MultiMcPatchMerger.ApplyTo(output, JsonNode.Parse("""
            { "uid": "com.example.custom", "mainClass": "com.example.CustomMain" }
            """)!.AsObject());

        Assert.IsTrue(JsonNode.DeepEquals(expected, output["libraries"]));
    }

    [TestMethod]
    public void KeepsBaseReleaseMetadataButPreservesUnknownExtensionFields()
    {
        var output = new JsonObject
        {
            ["type"] = "release",
            ["releaseTime"] = "2020-01-01T00:00:00Z"
        };

        MultiMcPatchMerger.ApplyTo(output, JsonNode.Parse("""
            {
              "uid": "com.example.custom",
              "type": "snapshot",
              "releaseTime": "2030-01-01T00:00:00Z",
              "customExtension": { "enabled": true }
            }
            """)!.AsObject());

        Assert.AreEqual("release", output["type"]!.GetValue<string>());
        Assert.AreEqual("2020-01-01T00:00:00Z", output["releaseTime"]!.GetValue<string>());
        Assert.IsTrue(output["customExtension"]!["enabled"]!.GetValue<bool>());
    }

    [TestMethod]
    public void ReturnsNullForEmptyInput()
    {
        Assert.IsNull(MultiMcPatchMerger.Merge([], selfContained: false));
        Assert.IsNull(MultiMcPatchMerger.Merge([_Patch("""{ "uid": "a", "version": "1.0" }""")], false));
    }

    private static MultiMcPatch _Patch(string json)
        => MultiMcPatch.TryCreate(JsonCompat.ParseNode(json), MultiMcPatchSource.Local)!;
}
