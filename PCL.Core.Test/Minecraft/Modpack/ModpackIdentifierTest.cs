using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Modpack;
using PCL.Core.Minecraft.Modpack.Model;
using PCL.Core.Minecraft.Modpack.MultiMc;

namespace PCL.Core.Test.Minecraft.Modpack;

/// <summary>
/// 五种整合包格式的识别与解析测试。全部使用内存中构造的压缩包，不依赖网络。
/// </summary>
[TestClass]
public class ModpackIdentifierTest
{
    private readonly List<string> _tempFiles = [];

    /// <summary>
    /// 注册代码页编码提供程序 —— 正式运行时由启动流程完成，测试宿主中需自行注册，
    /// 否则 GB18030 不可用，测不到条目名编码回退的真实行为。
    /// </summary>
    [AssemblyInitialize]
    public static void RegisterEncodings(TestContext context)
        => System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

    [TestCleanup]
    public void Cleanup()
    {
        foreach (var file in _tempFiles)
        {
            try { File.Delete(file); } catch (IOException) { /* 清理失败不影响测试结果 */ }
        }
    }

    [TestMethod]
    public async Task IdentifiesCurseForge()
    {
        var path = _CreateArchive(new Dictionary<string, string>
        {
            ["manifest.json"] = """
                {
                  "manifestType": "minecraftModpack",
                  "manifestVersion": 1,
                  "name": "Example Pack",
                  "version": "1.0.0",
                  "author": "someone",
                  "overrides": "overrides",
                  "minecraft": {
                    "version": "1.20.1",
                    "modLoaders": [{ "id": "forge-47.2.0", "primary": true }]
                  },
                  "files": [{ "projectID": 238222, "fileID": 5246076, "required": true }]
                }
                """,
            ["overrides/config/example.cfg"] = "example"
        });

        var descriptor = await ModpackIdentifier.Shared.ReadAsync(path);

        Assert.AreEqual(ModpackFormat.CurseForge, descriptor.Format);
        Assert.AreEqual("Example Pack", descriptor.Metadata.Name);
        Assert.AreEqual("1.20.1", descriptor.Components.GameVersion);
        Assert.AreEqual("47.2.0", descriptor.Components.GetLoaderVersion(ModLoaderKind.Forge));
        Assert.AreEqual("overrides", descriptor.Overrides.Single().ArchiveDirectory);

        var file = (ModpackCurseForgeFile)descriptor.Files.Single();
        Assert.AreEqual(238222, file.ProjectId);
        Assert.AreEqual(5246076, file.FileId);
    }

    /// <summary>
    /// MCBBS 与 CurseForge 共用 manifest.json，仅以 addons 字段区分。
    /// </summary>
    [TestMethod]
    public async Task DistinguishesMcbbsFromCurseForgeByAddons()
    {
        var path = _CreateArchive(new Dictionary<string, string>
        {
            ["manifest.json"] = """
                {
                  "manifestType": "minecraftModpack",
                  "name": "MCBBS Pack",
                  "version": "2.0",
                  "addons": [
                    { "id": "game", "version": "1.19.2" },
                    { "id": "fabric", "version": "0.14.14" }
                  ],
                  "launchInfo": { "minMemory": 2048, "javaArgument": ["-XX:+UseG1GC"] }
                }
                """
        });

        var descriptor = await ModpackIdentifier.Shared.ReadAsync(path);

        Assert.AreEqual(ModpackFormat.Mcbbs, descriptor.Format);
        Assert.AreEqual("1.19.2", descriptor.Components.GameVersion);
        Assert.AreEqual("0.14.14", descriptor.Components.GetLoaderVersion(ModLoaderKind.Fabric));
        Assert.AreEqual(2048, descriptor.LaunchOptions.MinMemoryMegabytes);
        CollectionAssert.AreEqual(new[] { "-XX:+UseG1GC" }, descriptor.LaunchOptions.JvmArguments.ToArray());
    }

    [TestMethod]
    public async Task IdentifiesModrinthAndFiltersUnsupportedFiles()
    {
        var path = _CreateArchive(new Dictionary<string, string>
        {
            ["modrinth.index.json"] = """
                {
                  "formatVersion": 1,
                  "game": "minecraft",
                  "versionId": "1.2.3",
                  "name": "Modrinth Pack",
                  "dependencies": { "minecraft": "1.21.1", "fabric-loader": "0.16.5" },
                  "files": [
                    {
                      "path": "mods/sodium.jar",
                      "hashes": { "sha1": "cc297357ff0031f805a744ca3a1378a112c2ddf4", "sha512": "d0" },
                      "downloads": ["https://cdn.modrinth.com/data/x/sodium.jar"],
                      "fileSize": 1234567
                    },
                    {
                      "path": "mods/server-only.jar",
                      "hashes": { "sha1": "aa" },
                      "env": { "client": "unsupported", "server": "required" },
                      "downloads": ["https://cdn.modrinth.com/data/y/server.jar"]
                    }
                  ]
                }
                """,
            ["overrides/options.txt"] = "fov:80"
        });

        var descriptor = await ModpackIdentifier.Shared.ReadAsync(path);

        Assert.AreEqual(ModpackFormat.Modrinth, descriptor.Format);
        Assert.AreEqual("1.21.1", descriptor.Components.GameVersion);
        Assert.AreEqual("0.16.5", descriptor.Components.GetLoaderVersion(ModLoaderKind.Fabric));

        // env.client 为 unsupported 的文件应被剔除
        var file = (ModpackDirectFile)descriptor.Files.Single();
        Assert.AreEqual(Path.Combine("mods", "sodium.jar"), file.TargetPath);
        Assert.AreEqual(1234567L, file.FileSize);
        Assert.AreEqual("cc297357ff0031f805a744ca3a1378a112c2ddf4", file.Sha1);
    }

    /// <summary>
    /// 大量 .mrpack 直接引用 CurseForge 的 CDN，这属于正常情况，不应产生任何告警。
    /// </summary>
    [TestMethod]
    public async Task AcceptsThirdPartyCdnUrlsWithoutWarnings()
    {
        var path = _CreateArchive(new Dictionary<string, string>
        {
            ["modrinth.index.json"] = """
                {
                  "formatVersion": 1,
                  "game": "minecraft",
                  "name": "CDN Pack",
                  "dependencies": { "minecraft": "1.21.1", "neoforge": "21.1.72" },
                  "files": [
                    {
                      "path": "mods/geckolib-neoforge-1.21.1-4.9.2.jar",
                      "hashes": { "sha1": "aa" },
                      "downloads": ["https://edge.forgecdn.net/files/8350/73/geckolib-neoforge-1.21.1-4.9.2.jar"]
                    },
                    {
                      "path": "mods/custom.jar",
                      "hashes": { "sha1": "bb" },
                      "downloads": ["https://cdn.example.com/custom.jar"]
                    }
                  ]
                }
                """
        });

        var descriptor = await ModpackIdentifier.Shared.ReadAsync(path);

        Assert.AreEqual(2, descriptor.Files.Count);
        Assert.AreEqual(0, descriptor.Warnings.Count,
            "第三方 CDN 是正常情况，不应产生告警：" + string.Join(" / ", descriptor.Warnings));
    }

    /// <summary>
    /// 明文 HTTP 无法防篡改，应被丢弃；地址全部不可用时该文件被跳过。
    /// </summary>
    [TestMethod]
    public async Task DropsPlainHttpDownloadUrls()
    {
        var path = _CreateArchive(new Dictionary<string, string>
        {
            ["modrinth.index.json"] = """
                {
                  "formatVersion": 1,
                  "game": "minecraft",
                  "name": "Insecure Pack",
                  "dependencies": { "minecraft": "1.20.1" },
                  "files": [
                    {
                      "path": "mods/insecure.jar",
                      "hashes": { "sha1": "aa" },
                      "downloads": ["http://example.com/insecure.jar"]
                    }
                  ]
                }
                """
        });

        var descriptor = await ModpackIdentifier.Shared.ReadAsync(path);

        Assert.AreEqual(0, descriptor.Files.Count);
        Assert.IsTrue(descriptor.Warnings.Any(w => w.Contains("非 HTTPS")));
    }

    /// <summary>
    /// 越出实例目录的路径必须被拒绝，这是 Zip Slip 的主要入口。
    /// </summary>
    [TestMethod]
    public async Task RejectsPathTraversalInModrinthFiles()
    {
        var path = _CreateArchive(new Dictionary<string, string>
        {
            ["modrinth.index.json"] = """
                {
                  "formatVersion": 1,
                  "game": "minecraft",
                  "name": "Evil Pack",
                  "dependencies": { "minecraft": "1.20.1" },
                  "files": [
                    {
                      "path": "../../../Windows/System32/evil.dll",
                      "hashes": { "sha1": "aa" },
                      "downloads": ["https://cdn.modrinth.com/data/x/evil.dll"]
                    }
                  ]
                }
                """
        });

        var descriptor = await ModpackIdentifier.Shared.ReadAsync(path);

        Assert.AreEqual(0, descriptor.Files.Count, "含 .. 的路径必须被丢弃");
        Assert.IsTrue(descriptor.Warnings.Any(w => w.Contains("路径不合法")));
    }

    [TestMethod]
    public async Task IdentifiesMultiMcAndReadsInstanceConfig()
    {
        var path = _CreateArchive(new Dictionary<string, string>
        {
            ["mmc-pack.json"] = """
                {
                  "formatVersion": 1,
                  "components": [
                    { "uid": "org.lwjgl3", "version": "3.3.1", "dependencyOnly": true },
                    { "uid": "net.minecraft", "version": "1.20.1", "important": true },
                    { "uid": "net.minecraftforge", "version": "47.2.20" }
                  ]
                }
                """,
            // 值中带冒号，用于验证解析器不会像通用 INI 那样在冒号处截断
            ["instance.cfg"] = """
                InstanceType=OneSix
                name=My Instance
                notes=Hello
                OverrideJavaArgs=true
                JvmArgs=-Dhttp.proxy=http://127.0.0.1:8080 -Xss1M
                OverrideMemory=true
                MaxMemAlloc=4096
                MinMemAlloc=512
                iconKey=custom
                """,
            [".minecraft/mods/example.jar"] = "jar",
            ["custom.png"] = "icon"
        });

        var descriptor = await ModpackIdentifier.Shared.ReadAsync(path);

        Assert.AreEqual(ModpackFormat.MultiMc, descriptor.Format);
        Assert.AreEqual("1.20.1", descriptor.Components.GameVersion);
        Assert.AreEqual("47.2.20", descriptor.Components.GetLoaderVersion(ModLoaderKind.Forge));
        Assert.AreEqual("My Instance", descriptor.Metadata.Name);
        Assert.AreEqual(".minecraft", descriptor.Overrides.Single().ArchiveDirectory);

        var options = descriptor.LaunchOptions;
        Assert.AreEqual("-Dhttp.proxy=http://127.0.0.1:8080 -Xss1M", options.JvmArguments.Single());
        Assert.AreEqual(4096, options.MaxMemoryMegabytes);
        Assert.AreEqual(512, options.MinMemoryMegabytes);
        Assert.AreEqual("custom.png", options.IconArchivePath);
    }

    /// <summary>
    /// Override 开关为 false 时，实例级取值不应生效。
    /// </summary>
    [TestMethod]
    public async Task IgnoresMultiMcSettingsWithoutOverrideFlag()
    {
        var path = _CreateArchive(new Dictionary<string, string>
        {
            ["mmc-pack.json"] = """
                { "formatVersion": 1, "components": [{ "uid": "net.minecraft", "version": "1.20.1" }] }
                """,
            ["instance.cfg"] = """
                name=Plain
                OverrideJavaArgs=false
                JvmArgs=-Xmx99G
                OverrideMemory=false
                MaxMemAlloc=99999
                """
        });

        var descriptor = await ModpackIdentifier.Shared.ReadAsync(path);

        Assert.AreEqual(0, descriptor.LaunchOptions.JvmArguments.Count);
        Assert.IsNull(descriptor.LaunchOptions.MaxMemoryMegabytes);
    }

    [TestMethod]
    public async Task PreservesMultiMcComponentAndJarModOrder()
    {
        var path = _CreateArchive(new Dictionary<string, string>
        {
            ["mmc-pack.json"] = """
                {
                  "formatVersion": 1,
                  "components": [
                    { "uid": "net.minecraft", "version": "1.12.2" },
                    { "uid": "net.minecraftforge", "version": "14.23.5.2860" },
                    { "uid": "com.example.custom", "version": "1.0" }
                  ]
                }
                """,
            ["instance.cfg"] = "name=Ordered Pack",
            ["patches/com.example.custom.json"] = """
                {
                  "formatVersion": 1,
                  "uid": "com.example.custom",
                  "version": "1.0",
                  "mainClass": "com.example.CustomMain",
                  "jarMods": [
                    {
                      "name": "org.multimc.jarmods:first:1",
                      "MMC-hint": "local",
                      "MMC-filename": "first.jar"
                    },
                    {
                      "name": "org.multimc.jarmods:second:1",
                      "MMC-hint": "local",
                      "MMC-filename": "second.jar"
                    }
                  ],
                  "+jarMods": [{ "name": "ignored-legacy.jar" }]
                }
                """,
            ["jarmods/first.jar"] = "first",
            ["jarmods/second.jar"] = "second"
        });

        var descriptor = await ModpackIdentifier.Shared.ReadAsync(path);
        var patch = descriptor.VersionPatch;

        Assert.IsNotNull(patch);
        CollectionAssert.AreEqual(
            new[] { "net.minecraft", "net.minecraftforge", "com.example.custom" },
            patch.OrderedComponents.Select(component => component.Uid).ToArray());
        Assert.AreEqual(ModpackVersionComponentKind.Loader, patch.OrderedComponents[1].Kind);
        Assert.AreEqual(ModLoaderKind.Forge, patch.OrderedComponents[1].LoaderKind);
        Assert.AreEqual(ModpackVersionComponentKind.CustomPatch, patch.OrderedComponents[2].Kind);

        var jarMods = descriptor.EmbeddedPayloads.Single(payload => payload.Kind == ModpackPayloadKind.JarMods);
        CollectionAssert.AreEqual(new[] { "first.jar", "second.jar" }, jarMods.OrderedFiles!.ToArray());
    }

    [TestMethod]
    public async Task InstallsPre16ForgeAsMultiMcJarModComponent()
    {
        var path = _CreateArchive(new Dictionary<string, string>
        {
            ["mmc-pack.json"] = """
                {
                  "formatVersion": 1,
                  "components": [
                    { "uid": "net.minecraft", "version": "1.5.2" },
                    { "uid": "net.minecraftforge", "version": "7.8.1.738" }
                  ]
                }
                """,
            ["instance.cfg"] = "name=Legacy Forge Pack",
            ["patches/net.minecraftforge.json"] = """
                {
                  "formatVersion": 1,
                  "uid": "net.minecraftforge",
                  "version": "7.8.1.738",
                  "+traits": ["legacyFML"],
                  "jarMods": [{
                    "name": "net.minecraftforge:forge:1.5.2-7.8.1.738:universal",
                    "downloads": { "artifact": {
                      "sha1": "76223709288287a6a8d22ab16b43a6ab2a284a0d",
                      "size": 2033732,
                      "url": "https://maven.minecraftforge.net/net/minecraftforge/forge/1.5.2-7.8.1.738/forge-1.5.2-7.8.1.738-universal.zip"
                    }}
                  }],
                  "requires": [{ "equals": "1.5.2", "uid": "net.minecraft" }]
                }
                """
        });

        var descriptor = await ModpackIdentifier.Shared.ReadAsync(path);
        var patch = descriptor.VersionPatch!;

        Assert.IsNull(descriptor.Components.GetLoaderVersion(ModLoaderKind.Forge));
        Assert.AreEqual(ModpackVersionComponentKind.CustomPatch, patch.OrderedComponents[1].Kind);
        Assert.IsNull(patch.OrderedComponents[1].LoaderKind);
        CollectionAssert.Contains(patch.Traits.ToArray(), "legacyFML");

        var jarMod = patch.JarMods.Single();
        Assert.IsFalse(jarMod.IsLocal);
        // Prism 按 Maven 坐标将该 ZIP 缓存为 .jar；两者都是 ZIP 容器，扩展名不影响合并。
        Assert.AreEqual("forge-1.5.2-7.8.1.738-universal.jar", jarMod.FileName);
        Assert.AreEqual("76223709288287a6a8d22ab16b43a6ab2a284a0d", jarMod.Sha1);
    }

    [TestMethod]
    public async Task KeepsForge16AsPclInstallableLoader()
    {
        var path = _CreateArchive(new Dictionary<string, string>
        {
            ["mmc-pack.json"] = """
                {
                  "formatVersion": 1,
                  "components": [
                    { "uid": "net.minecraft", "version": "1.6.4" },
                    { "uid": "net.minecraftforge", "version": "9.11.1.1345" }
                  ]
                }
                """,
            ["instance.cfg"] = "name=Forge 1.6.4 Pack",
            ["patches/net.minecraftforge.json"] = """
                {
                  "formatVersion": 1,
                  "uid": "net.minecraftforge",
                  "version": "9.11.1.1345",
                  "+tweakers": ["cpw.mods.fml.common.launcher.FMLTweaker"],
                  "mainClass": "net.minecraft.launchwrapper.Launch",
                  "requires": [{ "equals": "1.6.4", "uid": "net.minecraft" }]
                }
                """
        });

        var descriptor = await ModpackIdentifier.Shared.ReadAsync(path);

        Assert.AreEqual("9.11.1.1345", descriptor.Components.GetLoaderVersion(ModLoaderKind.Forge));
        Assert.AreEqual(
            ModpackVersionComponentKind.Loader,
            descriptor.VersionPatch!.OrderedComponents[1].Kind);
    }

    [TestMethod]
    public async Task ReadsLegacyPlusJarModsByName()
    {
        var path = _CreateArchive(new Dictionary<string, string>
        {
            ["mmc-pack.json"] = """
                {
                  "formatVersion": 1,
                  "components": [
                    { "uid": "net.minecraft", "version": "1.12.2" },
                    { "uid": "com.example.legacy", "version": "1.0" }
                  ]
                }
                """,
            ["instance.cfg"] = "name=Legacy JAR Mod",
            ["patches/com.example.legacy.json"] = """
                {
                  "formatVersion": 1,
                  "uid": "com.example.legacy",
                  "version": "1.0",
                  "+jarMods": [{ "name": "legacy.jar", "originalName": "Legacy" }]
                }
                """,
            ["jarmods/legacy.jar"] = "legacy"
        });

        var descriptor = await ModpackIdentifier.Shared.ReadAsync(path);

        var jarMod = descriptor.VersionPatch!.JarMods.Single();
        Assert.AreEqual("legacy.jar", jarMod.FileName);
        Assert.IsTrue(jarMod.IsLocal);
    }

    [TestMethod]
    public async Task ReadsRemoteModernJarModMetadata()
    {
        var path = _CreateArchive(new Dictionary<string, string>
        {
            ["mmc-pack.json"] = """
                {
                  "formatVersion": 1,
                  "components": [
                    { "uid": "net.minecraft", "version": "1.12.2" },
                    { "uid": "com.example.remote", "version": "1.0" }
                  ]
                }
                """,
            ["instance.cfg"] = "name=Remote JAR Mod",
            ["patches/com.example.remote.json"] = """
                {
                  "formatVersion": 1,
                  "uid": "com.example.remote",
                  "version": "1.0",
                  "jarMods": [{
                    "name": "com.example:remote-jarmod:1.2:client@zip",
                    "MMC-filename": "remote.jar",
                    "MMC-absoluteUrl": "https://example.com/files/remote.jar",
                    "downloads": { "artifact": { "sha1": "abcdef", "size": 12345 } }
                  }]
                }
                """
        });

        var descriptor = await ModpackIdentifier.Shared.ReadAsync(path);

        var jarMod = descriptor.VersionPatch!.JarMods.Single();
        Assert.AreEqual("remote.jar", jarMod.FileName);
        Assert.IsFalse(jarMod.IsLocal);
        Assert.AreEqual("https://example.com/files/remote.jar", jarMod.DownloadUrls.Single());
        Assert.AreEqual("abcdef", jarMod.Sha1);
        Assert.AreEqual(12345L, jarMod.FileSize);
        Assert.IsFalse(descriptor.EmbeddedPayloads.Any(payload => payload.Kind == ModpackPayloadKind.JarMods));
    }

    [TestMethod]
    public async Task RejectsMissingLocalJarMod()
    {
        var path = _CreateArchive(new Dictionary<string, string>
        {
            ["mmc-pack.json"] = """
                {
                  "formatVersion": 1,
                  "components": [
                    { "uid": "net.minecraft", "version": "1.12.2" },
                    { "uid": "com.example.missing", "version": "1.0" }
                  ]
                }
                """,
            ["instance.cfg"] = "name=Missing JAR Mod",
            ["patches/com.example.missing.json"] = """
                {
                  "formatVersion": 1,
                  "uid": "com.example.missing",
                  "version": "1.0",
                  "jarMods": [{
                    "name": "org.multimc.jarmods:missing:1",
                    "MMC-hint": "local",
                    "MMC-filename": "missing.jar"
                  }]
                }
                """
        });

        await Assert.ThrowsExactlyAsync<ModpackManifestInvalidException>(
            () => ModpackIdentifier.Shared.ReadAsync(path));
    }

    [TestMethod]
    public async Task RejectsMalformedMainJarDefinition()
    {
        var path = _CreateArchive(new Dictionary<string, string>
        {
            ["mmc-pack.json"] = """
                {
                  "formatVersion": 1,
                  "components": [
                    { "uid": "net.minecraft", "version": "1.12.2" },
                    { "uid": "com.example.bad", "version": "1.0" }
                  ]
                }
                """,
            ["instance.cfg"] = "name=Bad Main JAR",
            ["patches/com.example.bad.json"] = """
                {
                  "formatVersion": 1,
                  "uid": "com.example.bad",
                  "version": "1.0",
                  "mainJar": "not-a-library"
                }
                """
        });

        await Assert.ThrowsExactlyAsync<ModpackManifestInvalidException>(
            () => ModpackIdentifier.Shared.ReadAsync(path));
    }

    [TestMethod]
    public async Task ParsesPrismIniCommentsEscapesAndMemorySettings()
    {
        var path = _CreateArchive(new Dictionary<string, string>
        {
            ["mmc-pack.json"] = """
                { "formatVersion": 1, "components": [{ "uid": "net.minecraft", "version": "1.7.10" }] }
                """,
            ["instance.cfg"] = """
                name=INI Pack # discarded
                notes=Keep \# hash # discarded
                OverrideMemory=true
                MinMemAlloc=4096 # swapped
                MaxMemAlloc=1024
                PermGen=192
                """
        });

        var descriptor = await ModpackIdentifier.Shared.ReadAsync(path);

        Assert.AreEqual("INI Pack", descriptor.Metadata.Name);
        Assert.AreEqual("Keep # hash", descriptor.LaunchOptions.Notes);
        Assert.AreEqual(1024, descriptor.LaunchOptions.MinMemoryMegabytes);
        Assert.AreEqual(4096, descriptor.LaunchOptions.MaxMemoryMegabytes);
        Assert.AreEqual(192, descriptor.LaunchOptions.PermGenMegabytes);
        Assert.IsTrue(descriptor.Warnings.Any(warning => warning.Contains("MinMemAlloc")));
    }

    [TestMethod]
    public async Task ServerManifestIsNoLongerRecognized()
    {
        var path = _CreateArchive(new Dictionary<string, string>
        {
            ["server-manifest.json"] = """
                {
                  "name": "Server Pack",
                  "version": "3.0",
                  "fileApi": "https://example.com/api",
                  "addons": [{ "id": "game", "version": "1.18.2" }, { "id": "forge", "version": "40.2.0" }],
                  "files": [{ "path": "mods/a.jar", "hash": "abc" }]
                }
                """
        });

        await Assert.ThrowsExactlyAsync<ModpackFormatNotRecognizedException>(
            () => ModpackIdentifier.Shared.ReadAsync(path));
    }

    [TestMethod]
    public async Task IdentifiesHmclModpack()
    {
        var path = _CreateArchive(new Dictionary<string, string>
        {
            ["modpack.json"] = """{ "name": "HMCL Pack", "version": "1.0", "author": "me" }""",
            ["minecraft/pack.json"] = """
                {
                  "jar": "1.16.5",
                  "patches": [
                    { "id": "game", "version": "1.16.5" },
                    { "id": "forge", "version": "36.2.39" }
                  ]
                }
                """,
            ["minecraft/mods/a.jar"] = "jar"
        });

        var descriptor = await ModpackIdentifier.Shared.ReadAsync(path);

        Assert.AreEqual(ModpackFormat.Hmcl, descriptor.Format);
        Assert.AreEqual("1.16.5", descriptor.Components.GameVersion);
        Assert.AreEqual("36.2.39", descriptor.Components.GetLoaderVersion(ModLoaderKind.Forge));
        Assert.AreEqual("minecraft", descriptor.Overrides.Single().ArchiveDirectory);
    }

    /// <summary>
    /// 内容整体位于单个一级子目录下的整合包，应把该目录抹平为逻辑根。
    /// </summary>
    [TestMethod]
    public async Task FlattensSingleTopLevelDirectory()
    {
        var path = _CreateArchive(new Dictionary<string, string>
        {
            ["MyPack/modrinth.index.json"] = """
                {
                  "formatVersion": 1,
                  "game": "minecraft",
                  "name": "Nested Pack",
                  "dependencies": { "minecraft": "1.20.4" },
                  "files": []
                }
                """,
            ["MyPack/overrides/config/a.cfg"] = "a"
        });

        var descriptor = await ModpackIdentifier.Shared.ReadAsync(path);

        Assert.AreEqual(ModpackFormat.Modrinth, descriptor.Format);
        Assert.AreEqual("1.20.4", descriptor.Components.GameVersion);
        Assert.AreEqual("overrides", descriptor.Overrides.Single().ArchiveDirectory);
    }

    [TestMethod]
    public async Task ThrowsWhenFormatIsNotRecognized()
    {
        var path = _CreateArchive(new Dictionary<string, string>
        {
            ["readme.txt"] = "nothing to see here"
        });

        await Assert.ThrowsExactlyAsync<ModpackFormatNotRecognizedException>(
            () => ModpackIdentifier.Shared.ReadAsync(path));
    }

    [TestMethod]
    public async Task ThrowsWhenGameVersionIsMissing()
    {
        var path = _CreateArchive(new Dictionary<string, string>
        {
            ["manifest.json"] = """{ "manifestType": "minecraftModpack", "name": "Broken", "minecraft": {} }"""
        });

        var exception = await Assert.ThrowsExactlyAsync<ModpackManifestInvalidException>(
            () => ModpackIdentifier.Shared.ReadAsync(path));

        Assert.AreEqual(ModpackFormat.CurseForge, exception.Format);
    }

    /// <summary>
    /// Quilt 已不受支持，应给出明确原因而非笼统的解析失败。
    /// </summary>
    [TestMethod]
    public async Task ReportsUnsupportedLoader()
    {
        var path = _CreateArchive(new Dictionary<string, string>
        {
            ["modrinth.index.json"] = """
                {
                  "formatVersion": 1,
                  "game": "minecraft",
                  "name": "Quilt Pack",
                  "dependencies": { "minecraft": "1.20.1", "quilt-loader": "0.26.0" },
                  "files": []
                }
                """
        });

        await Assert.ThrowsExactlyAsync<ModpackUnsupportedContentException>(
            () => ModpackIdentifier.Shared.ReadAsync(path));
    }

    private string _CreateArchive(Dictionary<string, string> entries)
    {
        var path = Path.Combine(Path.GetTempPath(), $"pclce-modpack-test-{Guid.NewGuid():N}.zip");
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
}
