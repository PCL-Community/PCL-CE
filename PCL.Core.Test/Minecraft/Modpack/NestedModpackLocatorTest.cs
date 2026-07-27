using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Modpack;
using PCL.Core.Minecraft.Modpack.Installation;
using PCL.Core.Minecraft.Modpack.Model;

namespace PCL.Core.Test.Minecraft.Modpack;

/// <summary>
/// 嵌套整合包的取出与安装测试 —— 覆盖「附带启动器的整合包」等被再套一层压缩包的分发形式。
/// </summary>
[TestClass]
public class NestedModpackLocatorTest
{
    private readonly List<string> _tempFiles = [];

    [TestCleanup]
    public void Cleanup()
    {
        foreach (var file in _tempFiles)
        {
            try { File.Delete(file); } catch (IOException) { /* 清理失败不影响测试结果 */ }
        }
    }

    private const string ModrinthIndex = """
        {
          "formatVersion": 1,
          "game": "minecraft",
          "versionId": "1.0.0",
          "name": "Inner Pack",
          "dependencies": { "minecraft": "1.20.1", "fabric-loader": "0.15.11" },
          "files": []
        }
        """;

    private const string CurseForgeManifest = """
        {
          "manifestType": "minecraftModpack",
          "name": "Inner Curse Pack",
          "overrides": "overrides",
          "minecraft": { "version": "1.19.2", "modLoaders": [{ "id": "forge-43.2.0" }] },
          "files": []
        }
        """;

    /// <summary>
    /// 外层压缩包里放着 modpack.mrpack，应当取出内层整合包并按其格式安装。
    /// </summary>
    [TestMethod]
    public async Task InstallsNestedMrpack()
    {
        var inner = _CreateArchive(new Dictionary<string, string> { ["modrinth.index.json"] = ModrinthIndex });
        var outer = _CreateArchiveWithNested("modpack.mrpack", inner, extras: new Dictionary<string, string>
        {
            ["readme.txt"] = "put the launcher here"
        });

        using var session = await ModpackInstallSession.OpenAsync(outer);

        Assert.AreEqual(ModpackFormat.Modrinth, session.Descriptor.Format);
        Assert.AreEqual("Inner Pack", session.Descriptor.Metadata.Name);
        Assert.AreEqual("1.20.1", session.Descriptor.Components.GameVersion);
        Assert.AreEqual("0.15.11", session.Descriptor.Components.GetLoaderVersion(ModLoaderKind.Fabric));
    }

    [TestMethod]
    public async Task InstallsNestedModpackZip()
    {
        var inner = _CreateArchive(new Dictionary<string, string>
        {
            ["manifest.json"] = CurseForgeManifest,
            ["overrides/config/a.cfg"] = "a"
        });
        var outer = _CreateArchiveWithNested("modpack.zip", inner);

        using var session = await ModpackInstallSession.OpenAsync(outer);

        Assert.AreEqual(ModpackFormat.CurseForge, session.Descriptor.Format);
        Assert.AreEqual("1.19.2", session.Descriptor.Components.GameVersion);
    }

    /// <summary>
    /// 附带启动器时，可执行文件应被完全忽略，只取出真正的整合包文件。
    /// </summary>
    [TestMethod]
    public async Task IgnoresBundledLauncherExecutable()
    {
        var inner = _CreateArchive(new Dictionary<string, string> { ["modrinth.index.json"] = ModrinthIndex });
        var outer = _CreateArchiveWithNested("modpack.mrpack", inner, extras: new Dictionary<string, string>
        {
            ["PCL.exe"] = "MZ fake executable",
            ["启动器.exe"] = "MZ fake executable",
            [".minecraft/versions/1.20.1/1.20.1.json"] = "{}"
        });

        using var session = await ModpackInstallSession.OpenAsync(outer);

        Assert.AreEqual(ModpackFormat.Modrinth, session.Descriptor.Format);
        Assert.AreEqual("Inner Pack", session.Descriptor.Metadata.Name);
    }

    /// <summary>
    /// 内层整合包位于子目录时同样应被找到。
    /// </summary>
    [TestMethod]
    public async Task FindsNestedModpackInSubdirectory()
    {
        var inner = _CreateArchive(new Dictionary<string, string> { ["modrinth.index.json"] = ModrinthIndex });
        var outer = _CreateArchiveWithNested("data/packs/modpack.mrpack", inner, extras: new Dictionary<string, string>
        {
            ["run.bat"] = "echo hi"
        });

        using var session = await ModpackInstallSession.OpenAsync(outer);

        Assert.AreEqual(ModpackFormat.Modrinth, session.Descriptor.Format);
    }

    /// <summary>
    /// 文件名不是约定名称、但确实是整合包的 zip 也应被识别出来。
    /// </summary>
    [TestMethod]
    public async Task FindsNestedModpackWithArbitraryName()
    {
        var inner = _CreateArchive(new Dictionary<string, string> { ["manifest.json"] = CurseForgeManifest });
        var outer = _CreateArchiveWithNested("SomeCoolPack-1.0.zip", inner, extras: new Dictionary<string, string>
        {
            ["notes.txt"] = "hello"
        });

        using var session = await ModpackInstallSession.OpenAsync(outer);

        Assert.AreEqual(ModpackFormat.CurseForge, session.Descriptor.Format);
    }

    /// <summary>
    /// 内层不是整合包时不应误判，而是照常报「无法识别」。
    /// </summary>
    [TestMethod]
    public async Task ThrowsWhenNestedArchiveIsNotAModpack()
    {
        var inner = _CreateArchive(new Dictionary<string, string> { ["readme.txt"] = "not a modpack" });
        var outer = _CreateArchiveWithNested("bundle.zip", inner, extras: new Dictionary<string, string>
        {
            ["launcher.exe"] = "MZ",
            [".minecraft/versions/1.20.1/1.20.1.json"] = "{}"
        });

        await Assert.ThrowsExactlyAsync<ModpackFormatNotRecognizedException>(
            () => ModpackInstallSession.OpenAsync(outer));
    }

    /// <summary>
    /// 会话释放后，取出内层整合包产生的临时文件必须被删除。
    /// </summary>
    [TestMethod]
    public async Task DeletesExtractedTemporaryFileOnDispose()
    {
        var inner = _CreateArchive(new Dictionary<string, string> { ["modrinth.index.json"] = ModrinthIndex });
        var outer = _CreateArchiveWithNested("modpack.mrpack", inner);

        var before = _CountTemporaryFiles();

        using (var session = await ModpackInstallSession.OpenAsync(outer))
        {
            Assert.AreEqual(ModpackFormat.Modrinth, session.Descriptor.Format);
            Assert.AreEqual(before + 1, _CountTemporaryFiles(), "会话存续期间临时文件应当存在");
        }

        Assert.AreEqual(before, _CountTemporaryFiles(), "会话释放后临时文件应被删除");
    }

    private static int _CountTemporaryFiles()
    {
        var directory = NestedModpackLocator.TemporaryDirectory;
        return Directory.Exists(directory) ? Directory.GetFiles(directory, "nested-*").Length : 0;
    }

    private string _CreateArchive(Dictionary<string, string> entries)
    {
        var path = Path.Combine(Path.GetTempPath(), $"pclce-nested-test-{Guid.NewGuid():N}.zip");
        _tempFiles.Add(path);

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);

        foreach (var (name, content) in entries)
        {
            using var writer = new StreamWriter(archive.CreateEntry(name).Open());
            writer.Write(content);
        }

        return path;
    }

    /// <summary>
    /// 构造一个把 <paramref name="innerArchivePath" /> 作为条目嵌入的外层压缩包。
    /// </summary>
    private string _CreateArchiveWithNested(
        string nestedEntryName, string innerArchivePath, Dictionary<string, string>? extras = null)
    {
        var path = Path.Combine(Path.GetTempPath(), $"pclce-nested-outer-{Guid.NewGuid():N}.zip");
        _tempFiles.Add(path);

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);

        using (var entryStream = archive.CreateEntry(nestedEntryName).Open())
        using (var innerStream = File.OpenRead(innerArchivePath))
        {
            innerStream.CopyTo(entryStream);
        }

        foreach (var (name, content) in extras ?? [])
        {
            using var writer = new StreamWriter(archive.CreateEntry(name).Open());
            writer.Write(content);
        }

        return path;
    }
}
