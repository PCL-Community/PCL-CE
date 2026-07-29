using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Modpack;
using PCL.Core.Minecraft.Modpack.CurseForge;
using PCL.Core.Minecraft.Modpack.Installation;
using PCL.Core.Minecraft.Modpack.Model;

namespace PCL.Core.Test.Minecraft.Modpack;

/// <summary>
/// 在线整合包文件的安装规划测试。下载器只消费这里生成的 Downloads，
/// 因此需要覆盖不同平台与资源目录，避免清单被静默规划为空。
/// </summary>
[TestClass]
public class ModpackInstallPlannerTest
{
    [TestMethod]
    public async Task PlansAllModrinthResourceFiles()
    {
        var instanceDirectory = _CreateInstancePath();
        var descriptor = new ModpackDescriptor
        {
            Format = ModpackFormat.Modrinth,
            Components = new ModpackComponents("1.20.1"),
            Files =
            [
                _DirectFile("mods/example.jar", "https://cdn.modrinth.com/data/mod/example.jar"),
                _DirectFile("resourcepacks/example.zip", "https://cdn.modrinth.com/data/resource/example.zip"),
                _DirectFile("shaderpacks/example.zip", "https://cdn.modrinth.com/data/shader/example.zip")
            ]
        };

        var plan = await ModpackInstallPlanner.Shared.CreateAsync(descriptor, new ModpackInstallOptions
        {
            InstanceDirectory = instanceDirectory
        });

        Assert.AreEqual(3, plan.Downloads.Count);
        _AssertDownload(plan, "mods/example.jar", "https://cdn.modrinth.com/data/mod/example.jar");
        _AssertDownload(plan, "resourcepacks/example.zip", "https://cdn.modrinth.com/data/resource/example.zip");
        _AssertDownload(plan, "shaderpacks/example.zip", "https://cdn.modrinth.com/data/shader/example.zip");
    }

    [TestMethod]
    public async Task ResolvesCurseForgeFilesToTheirResourceDirectories()
    {
        var instanceDirectory = _CreateInstancePath();
        var resolver = new StubCurseForgeResolver(
        [
            _CurseDescriptor(11, 101, "example-mod.jar", 6),
            _CurseDescriptor(12, 102, "example-resources.zip", 12),
            _CurseDescriptor(13, 103, "example-shaders.zip", 6552)
        ]);
        var descriptor = new ModpackDescriptor
        {
            Format = ModpackFormat.CurseForge,
            Components = new ModpackComponents("1.20.1"),
            Files =
            [
                new ModpackCurseForgeFile { ProjectId = 11, FileId = 101 },
                new ModpackCurseForgeFile { ProjectId = 12, FileId = 102 },
                new ModpackCurseForgeFile { ProjectId = 13, FileId = 103 }
            ]
        };

        var plan = await ModpackInstallPlanner.Shared.CreateAsync(descriptor, new ModpackInstallOptions
        {
            InstanceDirectory = instanceDirectory,
            CurseForgeResolver = resolver
        });

        Assert.AreEqual(3, resolver.RequestedKeys.Count);
        Assert.AreEqual(3, plan.Downloads.Count);
        _AssertDownload(plan, "mods/example-mod.jar", "https://example.test/example-mod.jar");
        _AssertDownload(plan, "resourcepacks/example-resources.zip", "https://example.test/example-resources.zip");
        _AssertDownload(plan, "shaderpacks/example-shaders.zip", "https://example.test/example-shaders.zip");
    }

    /// <summary>
    /// fileName 只能确定 CDN 地址，不能确定资源种类。没有 targetPath 时仍必须调用 API，
    /// 否则资源包、世界和光影都会被错误放进 mods。
    /// </summary>
    [TestMethod]
    public async Task ResolvesFilenameOnlyCurseForgeEntriesThroughApi()
    {
        var instanceDirectory = _CreateInstancePath();
        var resolver = new StubCurseForgeResolver(
        [
            new CurseForgeFileDescriptor
            {
                ProjectId = 12,
                FileId = 102,
                FileName = "example-resources.zip",
                DownloadUrl = "https://example.test/example-resources.zip",
                ClassId = 12,
                Sha1 = "0123456789abcdef",
                FileSize = 12345
            }
        ]);
        var descriptor = new ModpackDescriptor
        {
            Format = ModpackFormat.CurseForge,
            Components = new ModpackComponents("1.20.1"),
            Files =
            [
                new ModpackCurseForgeFile
                {
                    ProjectId = 12,
                    FileId = 102,
                    FileName = "example-resources.zip"
                }
            ]
        };

        var plan = await ModpackInstallPlanner.Shared.CreateAsync(descriptor, new ModpackInstallOptions
        {
            InstanceDirectory = instanceDirectory,
            CurseForgeResolver = resolver
        });

        Assert.AreEqual(1, resolver.RequestedKeys.Count);
        var download = plan.Downloads.Single();
        Assert.AreEqual(ModpackResourceKind.ResourcePack, download.Kind);
        Assert.AreEqual("0123456789abcdef", download.Sha1);
        Assert.AreEqual(12345, download.FileSize);
        _AssertDownload(plan, "resourcepacks/example-resources.zip", "https://example.test/example-resources.zip");
    }

    private static ModpackDirectFile _DirectFile(string targetPath, string url) => new()
    {
        TargetPath = targetPath,
        Urls = [url]
    };

    private static CurseForgeFileDescriptor _CurseDescriptor(
        int projectId, int fileId, string fileName, int classId) => new()
    {
        ProjectId = projectId,
        FileId = fileId,
        FileName = fileName,
        DownloadUrl = $"https://example.test/{fileName}",
        ClassId = classId
    };

    private static string _CreateInstancePath()
        => Path.Combine(Path.GetTempPath(), "pclce-modpack-plan", Guid.NewGuid().ToString("N"));

    private static void _AssertDownload(ModpackInstallPlan plan, string relativePath, string expectedUrl)
    {
        var expectedPath = Path.GetFullPath(Path.Combine(plan.InstanceDirectory, relativePath));
        var download = plan.Downloads.Single(item => item.TargetPath == expectedPath);

        Assert.IsTrue(download.Urls.Contains(expectedUrl));
    }

    private sealed class StubCurseForgeResolver(IReadOnlyList<CurseForgeFileDescriptor> descriptors)
        : ICurseForgeFileResolver
    {
        public IReadOnlyList<CurseForgeFileKey> RequestedKeys { get; private set; } = [];

        public Task<IReadOnlyList<CurseForgeFileDescriptor>> ResolveAsync(
            IReadOnlyList<CurseForgeFileKey> keys, CancellationToken cancellationToken = default)
        {
            RequestedKeys = keys.ToArray();
            return Task.FromResult(descriptors);
        }
    }
}
