// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Application.Downloads;

namespace PCL.Application.Test;

[TestClass]
public sealed class MinecraftInstallAddonMetadataServiceTests
{
    [TestMethod]
    public async Task GetVersionsAsync_QueriesCompatibleModrinthFiles()
    {
        Uri? requested = null;
        using HttpClient client = new(new DelegateHandler(request =>
        {
            requested = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    [{"version_number":"0.100.0+1.20.1","version_type":"release","files":[
                      {"primary":true,"filename":"fabric-api.jar","url":"https://cdn.example/fabric-api.jar","size":1234,"hashes":{"sha1":"abc"}}
                    ]}]
                    """)
            };
        }));
        MinecraftInstallAddonMetadataService service = new(client);

        IReadOnlyList<MinecraftInstallAddonVersionEntry> versions = await service.GetVersionsAsync(
            MinecraftInstallAddonKind.FabricApi,
            "1.20.1");

        StringAssert.Contains(requested?.AbsolutePath, "/project/fabric-api/version");
        StringAssert.Contains(requested?.Query, "game_versions=");
        Assert.AreEqual("0.100.0+1.20.1", versions.Single().Version);
        Assert.AreEqual("fabric-api.jar", versions.Single().FileName);
        Assert.AreEqual("abc", versions.Single().Sha1);
        Assert.IsTrue(versions.Single().Stable);
    }

    private sealed class DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(handler(request));
    }
}
