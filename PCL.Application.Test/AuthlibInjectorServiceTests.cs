// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Net;
using System.Security.Cryptography;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Application.Launching;

namespace PCL.Application.Test;

[TestClass]
public sealed class AuthlibInjectorServiceTests
{
    [TestMethod]
    public async Task EnsureAsync_DownloadsAndVerifiesLatestArtifact()
    {
        byte[] jarContent = "authlib"u8.ToArray();
        string sha256 = Convert.ToHexString(SHA256.HashData(jarContent));
        using HttpClient client = new(new DelegateHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("latest.json", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        $$"""
                          {
                            "download_url": "https://authlib-injector.yushi.moe/artifact/authlib-injector.jar",
                            "checksums": {
                              "sha256": "{{sha256}}"
                            }
                          }
                          """)
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(jarContent)
            };
        }));
        AuthlibInjectorService service = new(client, ["https://authlib-injector.yushi.moe/artifact/latest.json"]);
        string root = Path.Combine(Path.GetTempPath(), "pcl-authlib-" + Guid.NewGuid().ToString("N"));
        string target = Path.Combine(root, "authlib-injector.jar");

        try
        {
            string path = await service.EnsureAsync(target);

            Assert.AreEqual(target, path);
            CollectionAssert.AreEqual(jarContent, await File.ReadAllBytesAsync(path));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task GetServerMetadataAsync_NormalizesAuthserverSuffix()
    {
        using HttpClient client = new(new DelegateHandler(request =>
        {
            Assert.AreEqual("https://example.com/api/yggdrasil", request.RequestUri!.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"skinDomains":["example.com"]}""")
            };
        }));
        AuthlibInjectorService service = new(client, ["https://unused.invalid/latest.json"]);

        string metadata = await service.GetServerMetadataAsync("https://example.com/api/yggdrasil/authserver");

        Assert.AreEqual("""{"skinDomains":["example.com"]}""", metadata);
    }

    private sealed class DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(handle(request));
    }
}
