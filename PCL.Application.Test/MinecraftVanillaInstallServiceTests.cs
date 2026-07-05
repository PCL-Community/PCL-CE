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

    [TestMethod]
    public async Task InstallAsync_CreatesLoaderVersionThatInheritsVanillaBase()
    {
        string root = Path.Combine(Path.GetTempPath(), "pcl-install-loader-" + Guid.NewGuid().ToString("N"));
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

            if (path.EndsWith(".jar", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent([0x50, 0x4B, 0x03, 0x04])
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "id": "1.20.1",
                      "type": "release",
                      "assetIndex": {
                        "id": "empty",
                        "url": "https://example.invalid/assets/empty.json"
                      }
                    }
                    """)
            };
        }));
        MinecraftVanillaInstallService service = new(client, new FakeMinecraftLoaderMetadataService());

        try
        {
            MinecraftInstallResult result = await service.InstallAsync(
                new MinecraftInstallRequest
                {
                    VersionId = "fabric-loader-0.16.14-1.20.1",
                    BaseVersionId = "1.20.1",
                    VersionJsonUrl = "https://example.invalid/versions/1.20.1.json",
                    MinecraftRootDirectory = root,
                    Loader = new MinecraftLoaderInstallRequest(MinecraftLoaderKind.Fabric, "0.16.14")
                });

            string baseJsonPath = Path.Combine(root, "versions", "1.20.1", "1.20.1.json");
            Assert.IsTrue(File.Exists(baseJsonPath));
            JsonObject baseJson = JsonNode.Parse(await File.ReadAllTextAsync(baseJsonPath))!.AsObject();
            Assert.AreEqual("1.20.1", baseJson["id"]?.GetValue<string>());

            JsonObject loaderJson = JsonNode.Parse(await File.ReadAllTextAsync(result.VersionJsonPath))!.AsObject();
            Assert.AreEqual("fabric-loader-0.16.14-1.20.1", loaderJson["id"]?.GetValue<string>());
            Assert.AreEqual("1.20.1", loaderJson["inheritsFrom"]?.GetValue<string>());
            Assert.AreEqual("net.fabricmc.loader.impl.launch.knot.KnotClient", loaderJson["mainClass"]?.GetValue<string>());
            string libraries = loaderJson["libraries"]!.ToJsonString();
            StringAssert.Contains(libraries, "net.fabricmc:fabric-loader:0.16.14");
            StringAssert.Contains(libraries, "net.fabricmc:intermediary:1.20.1");
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task InstallAsync_DownloadsVersionFilesConcurrently()
    {
        string root = Path.Combine(Path.GetTempPath(), "pcl-install-parallel-" + Guid.NewGuid().ToString("N"));
        byte[] jar = [0x50, 0x4B, 0x03, 0x04];
        List<MinecraftInstallProgress> progress = [];
        object sync = new();
        int activeRequests = 0;
        int maxActiveRequests = 0;
        using HttpClient client = new(new AsyncDelegateHandler(async (request, cancellationToken) =>
        {
            string path = request.RequestUri!.AbsolutePath;
            if (path.Contains("/assets/", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"objects":{}}""")
                };
            }

            if (path.EndsWith(".jar", StringComparison.Ordinal))
            {
                int active = Interlocked.Increment(ref activeRequests);
                lock (sync)
                    maxActiveRequests = Math.Max(maxActiveRequests, active);
                try
                {
                    await Task.Delay(80, cancellationToken).ConfigureAwait(false);
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(jar)
                    };
                }
                finally
                {
                    Interlocked.Decrement(ref activeRequests);
                }
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
                          "size": {{jar.Length}}
                        }
                      },
                      "libraries": [
                        {
                          "name": "org.example:lib-a:1.0.0",
                          "downloads": {
                            "artifact": {
                              "path": "org/example/lib-a/1.0.0/lib-a-1.0.0.jar",
                              "url": "https://example.invalid/libraries/lib-a.jar",
                              "size": {{jar.Length}}
                            }
                          }
                        },
                        {
                          "name": "org.example:lib-b:1.0.0",
                          "downloads": {
                            "artifact": {
                              "path": "org/example/lib-b/1.0.0/lib-b-1.0.0.jar",
                              "url": "https://example.invalid/libraries/lib-b.jar",
                              "size": {{jar.Length}}
                            }
                          }
                        },
                        {
                          "name": "org.example:lib-c:1.0.0",
                          "downloads": {
                            "artifact": {
                              "path": "org/example/lib-c/1.0.0/lib-c-1.0.0.jar",
                              "url": "https://example.invalid/libraries/lib-c.jar",
                              "size": {{jar.Length}}
                            }
                          }
                        }
                      ],
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
            await service.InstallAsync(
                new MinecraftInstallRequest
                {
                    VersionId = "1.20.1",
                    VersionJsonUrl = "https://example.invalid/versions/1.20.1.json",
                    MinecraftRootDirectory = root,
                    DownloadThreadLimit = 4
                },
                new CaptureProgress<MinecraftInstallProgress>(progress));

            Assert.IsTrue(maxActiveRequests > 1, "安装文件下载应并发执行。");
            Assert.IsTrue(progress.Any(item => item.ActiveThreads > 1), "安装进度应上报真实活动线程数。");
            Assert.IsTrue(progress.Any(item => item.ThreadLimit == 4), "安装进度应保留请求的线程上限。");
            Assert.IsTrue(progress.Any(item => item.Steps.Any(step => step.Name == "下载客户端")), "安装进度应展示客户端下载子任务。");
            Assert.IsTrue(progress.Any(item => item.Steps.Any(step => step.Name == "下载运行库")), "安装进度应展示运行库下载子任务。");
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

    private sealed class FakeMinecraftLoaderMetadataService : IMinecraftLoaderMetadataService
    {
        public Task<IReadOnlyList<MinecraftLoaderVersionEntry>> GetLoaderVersionsAsync(
            MinecraftLoaderKind kind,
            string gameVersion,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MinecraftLoaderVersionEntry>>(
            [
                new MinecraftLoaderVersionEntry(kind, "0.16.14", true)
            ]);

        public Task<MinecraftLoaderInstallMetadata> GetLoaderInstallMetadataAsync(
            MinecraftLoaderInstallRequest request,
            string gameVersion,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new MinecraftLoaderInstallMetadata(
                request.Kind,
                request.LoaderVersion,
                "net.fabricmc:fabric-loader:0.16.14",
                "net.fabricmc:intermediary:1.20.1",
                "https://maven.fabricmc.net/",
                "net.fabricmc.loader.impl.launch.knot.KnotClient",
                [
                    new MinecraftLoaderLibrary("net.fabricmc:intermediary:1.20.1", "https://maven.fabricmc.net/"),
                    new MinecraftLoaderLibrary("net.fabricmc:fabric-loader:0.16.14", "https://maven.fabricmc.net/")
                ],
                17));
    }

    private sealed class DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(handle(request));
    }

    private sealed class AsyncDelegateHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            handle(request, cancellationToken);
    }
}
