using System.Linq;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Modpack.Model;
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
    /// 同一坐标只有严格更高的版本才能替换，版本号不参与去重标识。
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
    public void LowerOrEqualLibraryDoesNotReplaceExistingEntry()
    {
        var result = MultiMcPatchMerger.Merge(
        [
            _Patch("""{ "uid": "a", "libraries": [{ "name": "org.ow2.asm:asm:9.6", "origin": "first" }] }"""),
            _Patch("""{ "uid": "b", "libraries": [{ "name": "org.ow2.asm:asm:9.0", "origin": "lower" }] }"""),
            _Patch("""{ "uid": "c", "libraries": [{ "name": "org.ow2.asm:asm:9.6", "origin": "equal" }] }""")
        ], selfContained: false);

        var library = result!.VersionJson["libraries"]!.AsArray().Single()!.AsObject();
        Assert.AreEqual("org.ow2.asm:asm:9.6", library["name"]!.GetValue<string>());
        Assert.AreEqual("first", library["origin"]!.GetValue<string>());
    }

    [TestMethod]
    public void KeepsNativeAndRegularLibrariesWithSameCoordinateSeparate()
    {
        var result = MultiMcPatchMerger.Merge(
        [
            _Patch("""
                {
                  "uid": "a",
                  "libraries": [
                    { "name": "com.example:shared:1.0" },
                    {
                      "name": "com.example:shared:1.0",
                      "natives": { "windows": "natives-windows-${arch}" },
                      "downloads": {
                        "classifiers": {
                          "natives-windows": { "path": "com/example/shared/1.0/shared-1.0-natives-windows.jar" }
                        }
                      }
                    }
                  ]
                }
                """)
        ], selfContained: false);

        Assert.AreEqual(2, result!.VersionJson["libraries"]!.AsArray().Count);
    }

    [TestMethod]
    public void InactiveHigherLibraryDoesNotReplaceActiveVersion()
    {
        var result = MultiMcPatchMerger.Merge(
        [
            _Patch("""{ "uid": "a", "libraries": [{ "name": "com.example:conditional:1.0" }] }"""),
            _Patch("""
                {
                  "uid": "b",
                  "libraries": [{
                    "name": "com.example:conditional:9.0",
                    "rules": [{ "action": "allow", "os": { "name": "linux" } }]
                  }]
                }
                """),
            _Patch("""{ "uid": "c", "libraries": [{ "name": "com.example:conditional:2.0" }] }""")
        ], selfContained: false);

        var active = result!.VersionJson["libraries"]!.AsArray()
            .Select(node => node!.AsObject())
            .Where(MultiMcPatchMerger.IsLibraryActiveOnCurrentSystem)
            .Single();
        Assert.AreEqual("com.example:conditional:2.0", active["name"]!.GetValue<string>());
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

    [TestMethod]
    public void ReappliedTweakerMovesToLaterComponentPosition()
    {
        var result = MultiMcPatchMerger.Merge(
        [
            _Patch("""{ "uid": "a", "+tweakers": ["first", "moved", "third"] }"""),
            _Patch("""{ "uid": "b", "+tweakers": ["moved"] }""")
        ], selfContained: false);

        CollectionAssert.AreEqual(
            new[]
            {
                "--tweakClass", "first",
                "--tweakClass", "third",
                "--tweakClass", "moved"
            },
            result!.VersionJson["arguments"]!["game"]!.AsArray()
                .Select(node => node!.GetValue<string>()).ToArray());
    }

    [TestMethod]
    public void DirectModernGameArgumentsSuppressLegacyArguments()
    {
        var patch = new ModpackVersionPatch(new JsonObject(), false, [])
        {
            OrderedComponents =
            [
                new ModpackVersionComponent(
                    "com.example",
                    ModpackVersionComponentKind.CustomPatch,
                    Patch: JsonNode.Parse("""
                        { "arguments": { "game": [] } }
                        """)!.AsObject())
            ]
        };

        Assert.IsTrue(patch.OverridesGameArguments);
    }

    [TestMethod]
    public void PreservesLegacyArgumentsWhenAppendingModernGameArguments()
    {
        var output = JsonNode.Parse("""
            {
              "minecraftArguments": "--username ${auth_player_name} --assetIndex \"legacy assets\""
            }
            """)!.AsObject();

        MultiMcPatchMerger.ApplyTo(output, JsonNode.Parse("""
            { "uid": "com.example", "+gameArgs": ["--demo"] }
            """)!.AsObject());

        Assert.IsFalse(output.ContainsKey("minecraftArguments"));
        CollectionAssert.AreEqual(
            new[] { "--username", "${auth_player_name}", "--assetIndex", "legacy assets", "--demo" },
            output["arguments"]!["game"]!.AsArray()
                .Select(node => node!.GetValue<string>()).ToArray());
    }

    [TestMethod]
    public void RemovingGameArgumentsMarksPatchAsOverridingThem()
    {
        var result = MultiMcPatchMerger.Merge(
        [
            _Patch("""{ "uid": "a", "minecraftArguments": "--demo --width 800" }"""),
            _Patch("""{ "uid": "b", "-gameArgs": ["--demo"] }""")
        ], selfContained: false);

        Assert.IsTrue(result!.OverridesGameArguments);
        CollectionAssert.AreEqual(
            new[] { "--width", "800" },
            result.VersionJson["arguments"]!["game"]!.AsArray()
                .Select(node => node!.GetValue<string>()).ToArray());
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

    [TestMethod]
    public void PreservesTraitsInFirstSeenOrder()
    {
        var result = MultiMcPatchMerger.Merge(
        [
            _Patch("""{ "uid": "a", "+traits": ["legacyFML", "texturepacks"] }"""),
            _Patch("""{ "uid": "b", "+traits": ["legacyFML", "XR:Initial"] }""")
        ], selfContained: false);

        CollectionAssert.AreEqual(
            new[] { "legacyFML", "texturepacks", "XR:Initial" },
            result!.Traits.ToArray());
        Assert.IsFalse(result.IsEmpty);
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
    public void PreservesExplicitArtifactPathAndBuildsClassifierPathForCustomFileName()
    {
        var explicitPath = MultiMcPatchMerger.NormalizeLibrary(JsonNode.Parse("""
            {
              "name": "com.example:custom:1.0:client@zip",
              "downloads": { "artifact": { "path": "custom/location/client.zip" } }
            }
            """)!.AsObject());
        Assert.AreEqual(
            "custom/location/client.zip",
            explicitPath["downloads"]!["artifact"]!["path"]!.GetValue<string>());

        var renamed = MultiMcPatchMerger.NormalizeLibrary(JsonNode.Parse("""
            {
              "name": "com.example:native:2.0",
              "MMC-filename": "renamed.bin",
              "natives": { "windows": "natives-windows" },
              "downloads": {
                "classifiers": {
                  "natives-windows": { "path": "old/path/native.jar" }
                }
              }
            }
            """)!.AsObject());

        Assert.AreEqual(
            "com/example/native/2.0/renamed.bin",
            renamed["downloads"]!["artifact"]!["path"]!.GetValue<string>());
        Assert.AreEqual(
            "com/example/native/2.0/renamed.bin",
            renamed["downloads"]!["classifiers"]!["natives-windows"]!["path"]!.GetValue<string>());
    }

    [TestMethod]
    public void RemoteMainJarFallsBackToMinecraftLibraryRepository()
    {
        var result = MultiMcPatchMerger.Merge(
        [
            _Patch("""
                {
                  "uid": "com.example.customjar",
                  "mainJar": { "name": "com.example:custom-client:1.0:client" }
                }
                """)
        ], selfContained: false);

        var client = result!.VersionJson["downloads"]!["client"]!;
        Assert.AreEqual(
            "https://libraries.minecraft.net/com/example/custom-client/1.0/custom-client-1.0-client.jar",
            client["url"]!.GetValue<string>());
        Assert.AreEqual(
            "com/example/custom-client/1.0/custom-client-1.0-client.jar",
            client["path"]!.GetValue<string>());
    }

    [TestMethod]
    public void MainJarTakesPrecedenceOverDownloadsInSamePatch()
    {
        var output = new JsonObject();

        MultiMcPatchMerger.ApplyTo(output, JsonNode.Parse("""
            {
              "uid": "com.example.customjar",
              "downloads": { "client": { "url": "https://example.com/wrong.jar" } },
              "mainJar": {
                "name": "com.example:custom-client:1.0",
                "MMC-absoluteUrl": "https://example.com/right.jar"
              }
            }
            """)!.AsObject());

        Assert.AreEqual(
            "https://example.com/right.jar",
            output["downloads"]!["client"]!["url"]!.GetValue<string>());
    }

    [TestMethod]
    public void CustomComponentCannotOverrideMinecraftAssetIndex()
    {
        var output = JsonNode.Parse("""
            { "assetIndex": { "id": "base" }, "assets": "base" }
            """)!.AsObject();

        MultiMcPatchMerger.ApplyTo(output, JsonNode.Parse("""
            {
              "uid": "com.example.custom",
              "assetIndex": { "id": "wrong" },
              "assets": "wrong"
            }
            """)!.AsObject());

        Assert.AreEqual("base", output["assetIndex"]!["id"]!.GetValue<string>());
        Assert.AreEqual("base", output["assets"]!.GetValue<string>());
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
