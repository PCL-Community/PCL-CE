// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Application.Minecraft.Launch.Libraries;

namespace PCL.Application.Test;

[TestClass]
public sealed class MinecraftLibraryResolverTests
{
    [TestMethod]
    public void Resolve_ShouldUseArtifactDownloadMetadata()
    {
        string root = Path.Combine(Path.GetTempPath(), "pcl-libs");
        JsonObject versionJson = Parse(
            """
            {
              "libraries": [
                {
                  "name": "org.lwjgl:lwjgl:3.3.3",
                  "downloads": {
                    "artifact": {
                      "path": "org/lwjgl/lwjgl/3.3.3/lwjgl-3.3.3.jar",
                      "url": "https://libraries.minecraft.net/org/lwjgl/lwjgl/3.3.3/lwjgl-3.3.3.jar",
                      "sha1": "abcdef",
                      "size": 123
                    }
                  }
                }
              ]
            }
            """);

        MinecraftLibraryToken token = MinecraftLibraryResolver.Resolve(CreateRequest(versionJson, root))[0];

        Assert.AreEqual("org.lwjgl:lwjgl:3.3.3", token.OriginalName);
        Assert.AreEqual("org.lwjgl:lwjgl", token.NameWithoutVersion);
        Assert.AreEqual("https://libraries.minecraft.net/org/lwjgl/lwjgl/3.3.3/lwjgl-3.3.3.jar", token.Url);
        Assert.AreEqual(Path.Combine(root, "libraries", "org", "lwjgl", "lwjgl", "3.3.3", "lwjgl-3.3.3.jar"), token.LocalPath);
        Assert.AreEqual("abcdef", token.Sha1);
        Assert.AreEqual(123L, token.Size);
        Assert.IsFalse(token.IsNatives);
    }

    [TestMethod]
    public void Resolve_ShouldUseLocalInstanceLibraryPath_WhenHintIsLocal()
    {
        string root = Path.Combine(Path.GetTempPath(), "pcl-libs");
        string instance = Path.Combine(root, "versions", "Custom");
        JsonObject versionJson = Parse(
            """
            {
              "libraries": [
                {
                  "name": "custom.loader:bootstrap:1.0",
                  "hint": "local"
                }
              ]
            }
            """);

        MinecraftLibraryToken token = MinecraftLibraryResolver.Resolve(CreateRequest(versionJson, root) with
        {
            TargetInstanceDirectory = instance
        })[0];

        Assert.AreEqual(Path.Combine(instance, "libraries", "bootstrap-1.0.jar"), token.LocalPath);
        Assert.IsTrue(token.IsLocal);
    }

    [TestMethod]
    public void Resolve_ShouldSelectLinuxNativeClassifier()
    {
        string root = Path.Combine(Path.GetTempPath(), "pcl-libs");
        JsonObject versionJson = Parse(
            """
            {
              "libraries": [
                {
                  "name": "org.lwjgl:lwjgl:3.3.3",
                  "natives": {
                    "windows": "natives-windows",
                    "linux": "natives-linux",
                    "osx": "natives-macos"
                  },
                  "downloads": {
                    "classifiers": {
                      "natives-linux": {
                        "path": "org/lwjgl/lwjgl/3.3.3/lwjgl-3.3.3-natives-linux.jar",
                        "url": "https://example.test/linux.jar",
                        "sha1": "linux",
                        "size": 456
                      }
                    }
                  }
                }
              ]
            }
            """);

        MinecraftLibraryToken token = MinecraftLibraryResolver.Resolve(CreateRequest(
            versionJson,
            root,
            MinecraftLibraryOperatingSystem.Linux))[0];

        Assert.IsTrue(token.IsNatives);
        Assert.AreEqual("https://example.test/linux.jar", token.Url);
        Assert.AreEqual(Path.Combine(root, "libraries", "org", "lwjgl", "lwjgl", "3.3.3", "lwjgl-3.3.3-natives-linux.jar"), token.LocalPath);
        Assert.AreEqual("linux", token.Sha1);
        Assert.AreEqual(456L, token.Size);
    }

    [TestMethod]
    public void Resolve_ShouldReplaceNativeArchitecturePlaceholder()
    {
        string root = Path.Combine(Path.GetTempPath(), "pcl-libs");
        JsonObject versionJson = Parse(
            """
            {
              "libraries": [
                {
                  "name": "org.lwjgl:lwjgl:2.9.4",
                  "natives": { "windows": "natives-windows-${arch}" },
                  "downloads": {
                    "classifiers": {
                      "natives-windows-64": {
                        "path": "org/lwjgl/lwjgl/2.9.4/lwjgl-2.9.4-natives-windows-64.jar"
                      }
                    }
                  }
                }
              ]
            }
            """);

        MinecraftLibraryToken token = MinecraftLibraryResolver.Resolve(CreateRequest(
            versionJson,
            root,
            MinecraftLibraryOperatingSystem.Win32) with
        {
            Is64BitArchitecture = true
        })[0];

        Assert.AreEqual(Path.Combine(root, "libraries", "org", "lwjgl", "lwjgl", "2.9.4", "lwjgl-2.9.4-natives-windows-64.jar"), token.LocalPath);
    }

    [TestMethod]
    public void Resolve_ShouldRespectOperatingSystemRules()
    {
        string root = Path.Combine(Path.GetTempPath(), "pcl-libs");
        JsonObject versionJson = Parse(
            """
            {
              "libraries": [
                {
                  "name": "win.only:lib:1.0",
                  "rules": [{ "action": "allow", "os": { "name": "windows" } }]
                },
                {
                  "name": "linux.only:lib:1.0",
                  "rules": [{ "action": "allow", "os": { "name": "linux" } }]
                }
              ]
            }
            """);

        IReadOnlyList<MinecraftLibraryToken> tokens = MinecraftLibraryResolver.Resolve(CreateRequest(
            versionJson,
            root,
            MinecraftLibraryOperatingSystem.Linux));

        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual("linux.only:lib:1.0", tokens[0].OriginalName);
    }

    private static MinecraftLibraryResolutionRequest CreateRequest(
        JsonObject versionJson,
        string root,
        MinecraftLibraryOperatingSystem operatingSystem = MinecraftLibraryOperatingSystem.Win32) =>
        new()
        {
            VersionJson = versionJson,
            MinecraftRootDirectory = root,
            OperatingSystem = operatingSystem,
            OperatingSystemVersion = "10.0.19045",
            Is64BitArchitecture = true
        };

    private static JsonObject Parse(string json) => JsonNode.Parse(json)!.AsObject();
}
