// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Net;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Application.Downloads;

namespace PCL.Application.Test;

[TestClass]
public sealed class MinecraftVanillaInstallServiceTests
{
    [TestMethod]
    public async Task InstallAsync_RewritesVersionJsonIdWhenInstallNameIsCustomized()
    {
        string root = Path.Combine(Path.GetTempPath(), "pcl-install-" + Guid.NewGuid().ToString("N"));
        using HttpClient client = new(new DelegateHandler(request => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                request.RequestUri!.AbsolutePath.Contains("/assets/", StringComparison.Ordinal)
                    ? """{"objects":{}}"""
                    : """
                      {
                        "id": "1.20.1",
                        "type": "release",
                        "assetIndex": {
                          "id": "empty",
                          "url": "https://example.invalid/assets/empty.json"
                        }
                      }
                      """)
        }));
        MinecraftVanillaInstallService service = new(client);

        try
        {
            MinecraftInstallResult result = await service.InstallAsync(
                new MinecraftInstallRequest
                {
                    VersionId = "自定义 1.20.1",
                    VersionJsonUrl = "https://example.invalid/versions/1.20.1.json",
                    MinecraftRootDirectory = root
                });

            Assert.AreEqual("自定义 1.20.1", result.VersionId);
            Assert.IsTrue(File.Exists(result.VersionJsonPath));
            JsonObject json = JsonNode.Parse(await File.ReadAllTextAsync(result.VersionJsonPath))!.AsObject();
            Assert.AreEqual("自定义 1.20.1", json["id"]?.GetValue<string>());
            Assert.IsFalse(File.Exists(result.VersionJsonPath + ".tmp"));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private sealed class DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(handle(request));
    }
}
