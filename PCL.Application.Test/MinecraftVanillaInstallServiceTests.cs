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

    [TestMethod]
    public async Task InstallAsync_DownloadsClientJarIntoInstanceDirectory()
    {
        string root = Path.Combine(Path.GetTempPath(), "pcl-install-client-" + Guid.NewGuid().ToString("N"));
        byte[] clientJar = [0x50, 0x4B, 0x03, 0x04];
        List<MinecraftInstallProgress> progress = [];
        using HttpClient client = new(new DelegateHandler(request =>
        {
            string path = request.RequestUri!.AbsolutePath;
            if (path.Contains("/assets/", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"objects":{}}""")
                };
            }

            if (path.Contains("/client.jar", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(clientJar)
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $$"""
                    {
                      "id": "1.20.1",
                      "type": "release",
                      "downloads": {
                        "client": {
                          "url": "https://example.invalid/client.jar",
                          "size": {{clientJar.Length}}
                        }
                      },
                      "assetIndex": {
                        "id": "empty",
                        "url": "https://example.invalid/assets/empty.json"
                      }
                    }
                    """)
            };
        }));
        MinecraftVanillaInstallService service = new(client);

        try
        {
            MinecraftInstallResult result = await service.InstallAsync(
                new MinecraftInstallRequest
                {
                    VersionId = "1.20.1",
                    VersionJsonUrl = "https://example.invalid/versions/1.20.1.json",
                    MinecraftRootDirectory = root
                },
                new CaptureProgress<MinecraftInstallProgress>(progress));

            string jarPath = Path.Combine(result.InstanceDirectory, "1.20.1.jar");
            Assert.IsTrue(File.Exists(jarPath));
            CollectionAssert.AreEqual(clientJar, await File.ReadAllBytesAsync(jarPath));
            Assert.IsTrue(progress.Any(item => item.Stage == "下载客户端"));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private sealed class CaptureProgress<T>(List<T> items) : IProgress<T>
    {
        public void Report(T value) => items.Add(value);
    }

    private sealed class DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(handle(request));
    }
}
