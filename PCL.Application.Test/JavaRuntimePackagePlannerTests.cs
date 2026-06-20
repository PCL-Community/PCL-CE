// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Application.Minecraft.Java;
using PCL.Platform.Abstractions.Java;
using PCL.Platform.Abstractions.Paths;

namespace PCL.Application.Test;

[TestClass]
public sealed class JavaRuntimePackagePlannerTests
{
    [TestMethod]
    public void Platform_ShouldMapToMojangRuntimeKeys()
    {
        Assert.AreEqual("windows-x64",
            new JavaRuntimePlatform(JavaRuntimeOperatingSystem.Win32, JavaRuntimeArchitecture.X64).ToMojangKey());
        Assert.AreEqual("windows-x86",
            new JavaRuntimePlatform(JavaRuntimeOperatingSystem.Win32, JavaRuntimeArchitecture.X86).ToMojangKey());
        Assert.AreEqual("linux",
            new JavaRuntimePlatform(JavaRuntimeOperatingSystem.Linux, JavaRuntimeArchitecture.X64).ToMojangKey());
        Assert.AreEqual("mac-os-arm64",
            new JavaRuntimePlatform(JavaRuntimeOperatingSystem.MacOs, JavaRuntimeArchitecture.Arm64).ToMojangKey());
    }

    [TestMethod]
    public void SelectPackage_ShouldPreferExactComponentName()
    {
        JavaRuntimePackageDescriptor descriptor = JavaRuntimePackagePlanner.SelectPackage(
            RuntimeIndexJson,
            new JavaRuntimePlatform(JavaRuntimeOperatingSystem.Win32, JavaRuntimeArchitecture.X64),
            "java-runtime-alpha");

        Assert.AreEqual("java-runtime-alpha", descriptor.ComponentName);
        Assert.AreEqual("17.0.8", descriptor.VersionName);
        Assert.AreEqual("https://piston-meta.mojang.com/alpha.json", descriptor.ManifestUrl);
    }

    [TestMethod]
    public void SelectPackage_ShouldFallbackToVersionPrefix()
    {
        JavaRuntimePackageDescriptor descriptor = JavaRuntimePackagePlanner.SelectPackage(
            RuntimeIndexJson,
            new JavaRuntimePlatform(JavaRuntimeOperatingSystem.Win32, JavaRuntimeArchitecture.X64),
            "21");

        Assert.AreEqual("java-runtime-gamma", descriptor.ComponentName);
        Assert.AreEqual("21.0.2", descriptor.VersionName);
    }

    [TestMethod]
    public void CreateDownloadPlan_ShouldParseRawDownloadsAndIgnoreKnownNoiseHashes()
    {
        JavaRuntimePackageDescriptor descriptor = new(
            "java-runtime-alpha",
            "17.0.8",
            "https://piston-meta.mojang.com/alpha.json");
        string runtimeRoot = Path.Combine(Path.GetTempPath(), "pcl-runtime-plan");

        JavaRuntimeDownloadPlan plan = JavaRuntimePackagePlanner.CreateDownloadPlan(
            descriptor,
            RuntimeManifestJson,
            runtimeRoot);

        Assert.AreEqual("java-runtime-alpha", plan.ComponentName);
        Assert.AreEqual(Path.Combine(runtimeRoot, "java-runtime-alpha"), plan.TargetDirectory);
        Assert.AreEqual(1, plan.Files.Count);
        Assert.AreEqual("bin/java", plan.Files[0].RelativePath);
        Assert.AreEqual("0123456789abcdef0123456789abcdef01234567", plan.Files[0].Sha1);
        Assert.AreEqual(1234L, plan.Files[0].Size);
    }

    [TestMethod]
    public void CreateDownloadPlan_ShouldRejectPathTraversal()
    {
        JavaRuntimePackageDescriptor descriptor = new(
            "java-runtime-alpha",
            "17.0.8",
            "https://piston-meta.mojang.com/alpha.json");

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            JavaRuntimePackagePlanner.CreateDownloadPlan(
                descriptor,
                TraversalManifestJson,
                Path.Combine(Path.GetTempPath(), "pcl-runtime-plan")));
    }

    [TestMethod]
    public async Task DownloadPlanService_ShouldFetchIndexAndManifestThroughProvider()
    {
        string runtimeRoot = Path.Combine(Path.GetTempPath(), "pcl-runtime-plan");
        JavaRuntimeDownloadPlanService service = new(new FakeRuntimeMetadataProvider());

        JavaRuntimeDownloadPlan plan = await service.CreatePlanAsync(
            "21",
            new JavaRuntimePlatform(JavaRuntimeOperatingSystem.Win32, JavaRuntimeArchitecture.X64),
            runtimeRoot);

        Assert.AreEqual("java-runtime-gamma", plan.ComponentName);
        Assert.AreEqual("21.0.2", plan.VersionName);
        Assert.AreEqual(1, plan.Files.Count);
        Assert.AreEqual(Path.Combine(runtimeRoot, "java-runtime-gamma", "bin", "java"), plan.Files[0].TargetPath);
    }

    [TestMethod]
    public async Task DownloadPlanService_ShouldUsePlatformApplicationDataDirectory()
    {
        string appData = Path.Combine(Path.GetTempPath(), "pcl-app-data");
        JavaRuntimeDownloadPlanService service = new(new FakeRuntimeMetadataProvider());

        JavaRuntimeDownloadPlan plan = await service.CreatePlanAsync(
            "java-runtime-alpha",
            new JavaRuntimePlatform(JavaRuntimeOperatingSystem.Win32, JavaRuntimeArchitecture.X64),
            new FakePlatformPathProvider(appData));

        Assert.AreEqual(
            Path.Combine(appData, ".minecraft", "runtime", "java-runtime-alpha"),
            plan.TargetDirectory);
    }

    private const string RuntimeIndexJson = """
        {
          "windows-x64": {
            "java-runtime-alpha": [
              {
                "version": { "name": "17.0.8" },
                "manifest": { "url": "https://piston-meta.mojang.com/alpha.json" }
              }
            ],
            "java-runtime-gamma": [
              {
                "version": { "name": "21.0.2" },
                "manifest": { "url": "https://piston-meta.mojang.com/gamma.json" }
              }
            ]
          }
        }
        """;

    private const string RuntimeManifestJson = """
        {
          "files": {
            "bin/java": {
              "downloads": {
                "raw": {
                  "url": "https://piston-data.mojang.com/bin/java",
                  "sha1": "0123456789abcdef0123456789abcdef01234567",
                  "size": 1234
                }
              }
            },
            "noise-file": {
              "downloads": {
                "raw": {
                  "url": "https://piston-data.mojang.com/noise",
                  "sha1": "12976a6c2b227cbac58969c1455444596c894656",
                  "size": 1
                }
              }
            },
            "directory-only": {
              "type": "directory"
            }
          }
        }
        """;

    private const string TraversalManifestJson = """
        {
          "files": {
            "../escape": {
              "downloads": {
                "raw": {
                  "url": "https://piston-data.mojang.com/escape",
                  "sha1": "abcdefabcdefabcdefabcdefabcdefabcdefabcd",
                  "size": 12
                }
              }
            }
          }
        }
        """;

    private sealed class FakeRuntimeMetadataProvider : IJavaRuntimeMetadataProvider
    {
        public ValueTask<string> GetRuntimeIndexAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(RuntimeIndexJson);

        public ValueTask<string> GetManifestAsync(string manifestUrl, CancellationToken cancellationToken)
        {
            Assert.IsTrue(
                manifestUrl is "https://piston-meta.mojang.com/gamma.json" or "https://piston-meta.mojang.com/alpha.json",
                manifestUrl);
            return ValueTask.FromResult(RuntimeManifestJson);
        }
    }

    private sealed class FakePlatformPathProvider : IPlatformPathProvider
    {
        public FakePlatformPathProvider(string applicationDataDirectory)
        {
            ApplicationDataDirectory = applicationDataDirectory;
        }

        public string ApplicationDataDirectory { get; }
        public string CacheDirectory => Path.Combine(ApplicationDataDirectory, "cache");
        public string TemporaryDirectory => Path.GetTempPath();
    }
}
