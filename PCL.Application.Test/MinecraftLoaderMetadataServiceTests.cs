// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Application.Downloads;

namespace PCL.Application.Test;

[TestClass]
public sealed class MinecraftLoaderMetadataServiceTests
{
    [TestMethod]
    public async Task GetLoaderVersionsAsync_FiltersForgeAndNeoForgeMavenMetadataByGameVersion()
    {
        using HttpClient client = new(new DelegateHandler(request => Ok(
            request.RequestUri!.Host.Contains("minecraftforge", StringComparison.Ordinal)
                ? MavenMetadata("1.20.1-47.2.0", "1.20.1-47.2.0-beta", "1.19.4-45.0.1")
                : MavenMetadata("20.2.10-beta", "20.4.220", "21.1.80"))));
        MinecraftLoaderMetadataService service = new(client);

        IReadOnlyList<MinecraftLoaderVersionEntry> forge = await service.GetLoaderVersionsAsync(
            MinecraftLoaderKind.Forge,
            "1.20.1");
        IReadOnlyList<MinecraftLoaderVersionEntry> neoForge = await service.GetLoaderVersionsAsync(
            MinecraftLoaderKind.NeoForge,
            "1.20.4");

        CollectionAssert.AreEqual(new[] { "47.2.0", "47.2.0-beta" }, forge.Select(entry => entry.Version).ToArray());
        Assert.IsTrue(forge[0].Stable);
        Assert.IsFalse(forge[1].Stable);
        CollectionAssert.AreEqual(new[] { "20.4.220" }, neoForge.Select(entry => entry.Version).ToArray());
    }

    [TestMethod]
    public async Task GetLoaderVersionsAsync_ReadsLegacyFabricProfileMetadata()
    {
        const string metadata = """
            [{
              "loader":{"version":"0.19.3","maven":"net.fabricmc:fabric-loader:0.19.3","stable":true},
              "intermediary":{"maven":"net.legacyfabric:intermediary:1.12.2"},
              "launcherMeta":{"mainClass":{"client":"net.fabricmc.loader.impl.launch.knot.KnotClient"},"libraries":{"common":[],"client":[]},"min_java_version":8}
            }]
            """;
        using HttpClient client = new(new DelegateHandler(_ => Ok(metadata)));
        MinecraftLoaderMetadataService service = new(client);

        IReadOnlyList<MinecraftLoaderVersionEntry> versions = await service.GetLoaderVersionsAsync(
            MinecraftLoaderKind.LegacyFabric,
            "1.12.2");
        MinecraftLoaderInstallMetadata install = await service.GetLoaderInstallMetadataAsync(
            new MinecraftLoaderInstallRequest(MinecraftLoaderKind.LegacyFabric, "0.19.3"),
            "1.12.2");

        Assert.AreEqual("0.19.3", versions.Single().Version);
        Assert.AreEqual("net.legacyfabric:intermediary:1.12.2", install.MappingMaven);
        Assert.AreEqual("https://maven.legacyfabric.net/", install.MappingMavenRepository);
    }

    [TestMethod]
    public async Task GetLoaderVersionsAsync_ParsesCleanroomOptiFineAndLiteLoaderSources()
    {
        using HttpClient client = new(new DelegateHandler(request =>
        {
            string host = request.RequestUri!.Host;
            if (host.Equals("api.github.com", StringComparison.OrdinalIgnoreCase))
                return Ok("""[{"tag_name":"0.5.15-alpha"},{"tag_name":"0.5.4"}]""");
            if (host.Equals("optifine.net", StringComparison.OrdinalIgnoreCase))
                return Ok("""<a href="OptiFine_1.20.1_HD_U_I6.jar">a</a><a href="OptiFine_1.19.4_HD_U_I4.jar">b</a>""");
            return Ok("""
                {"versions":{"1.12.2":{"snapshots":{"com.mumfrey:liteloader":{"latest":{"version":"1.12.2-SNAPSHOT","stream":"SNAPSHOT"}}}}}}
                """);
        }));
        MinecraftLoaderMetadataService service = new(client);

        IReadOnlyList<MinecraftLoaderVersionEntry> cleanroom = await service.GetLoaderVersionsAsync(MinecraftLoaderKind.Cleanroom, "1.12.2");
        IReadOnlyList<MinecraftLoaderVersionEntry> optiFine = await service.GetLoaderVersionsAsync(MinecraftLoaderKind.OptiFine, "1.20.1");
        IReadOnlyList<MinecraftLoaderVersionEntry> liteLoader = await service.GetLoaderVersionsAsync(MinecraftLoaderKind.LiteLoader, "1.12.2");

        CollectionAssert.AreEqual(new[] { "0.5.15-alpha", "0.5.4" }, cleanroom.Select(entry => entry.Version).ToArray());
        CollectionAssert.AreEqual(new[] { "1.20.1_HD_U_I6" }, optiFine.Select(entry => entry.Version).ToArray());
        Assert.AreEqual("1.12.2-SNAPSHOT", liteLoader.Single().Version);
        Assert.IsFalse(liteLoader.Single().Stable);
    }

    [TestMethod]
    public async Task GetLoaderVersionsAsync_FiltersLabyModChannelsBySupportedGame()
    {
        using HttpClient client = new(new DelegateHandler(request => Ok(
            request.RequestUri!.AbsolutePath.Contains("production", StringComparison.Ordinal)
                ? """{"labyModVersion":"4.5.14","commitReference":"prod123","minecraftVersions":[{"version":"1.21.8"}]}"""
                : """{"labyModVersion":"4.6.0","commitReference":"snap456","minecraftVersions":[{"tag":"1.21.8"}]}""")));
        MinecraftLoaderMetadataService service = new(client);

        IReadOnlyList<MinecraftLoaderVersionEntry> versions = await service.GetLoaderVersionsAsync(
            MinecraftLoaderKind.LabyMod,
            "1.21.8");

        CollectionAssert.AreEqual(
            new[] { "production:4.5.14:prod123", "snapshot:4.6.0:snap456" },
            versions.Select(entry => entry.Version).ToArray());
        Assert.IsTrue(versions[0].Stable);
        Assert.IsFalse(versions[1].Stable);
    }

    private static string MavenMetadata(params string[] versions) =>
        "<metadata><versioning><versions>" +
        string.Concat(versions.Select(version => "<version>" + version + "</version>")) +
        "</versions></versioning></metadata>";

    private static HttpResponseMessage Ok(string content) =>
        new(HttpStatusCode.OK) { Content = new StringContent(content) };

    private sealed class DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(handler(request));
        }
    }
}
