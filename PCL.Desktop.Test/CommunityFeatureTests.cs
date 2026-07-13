// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Net;
using System.Text;
using PCL.Desktop.Features.Community;
using PCL.Desktop.Features.Launching.Views;

namespace PCL.Desktop.Test;

[TestClass]
public sealed class CommunityFeatureTests
{
    [TestMethod]
    public async Task VersionInspector_ShouldPreferClientVersionForStandaloneLoaderProfile()
    {
        string root = Path.Combine(Path.GetTempPath(), "pcln-version-test-" + Guid.NewGuid().ToString("N"));
        string jsonPath = Path.Combine(root, "26.2-Fabric_0.19.3.json");
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(
                jsonPath,
                """
                {
                  "id": "26.2-Fabric_0.19.3",
                  "clientVersion": "26.2",
                  "libraries": [{ "name": "net.fabricmc:fabric-loader:0.19.3" }]
                }
                """);
            LaunchInstanceInfo instance = new("26.2-Fabric_0.19.3", jsonPath, root);

            CommunitySearchOptions options = CommunityInstanceCompatibility.Apply(
                new CommunitySearchOptions(),
                CommunityResourceCategory.Mod,
                instance);
            HttpRequestMessage? captured = null;
            using HttpClient client = new(new DelegateHandler(request =>
            {
                captured = request;
                return JsonResponse(
                    """
                    [{
                      "id": "fabric-api-26.2",
                      "name": "[26.2] Fabric API 0.154.2+26.2",
                      "files": [{
                        "filename": "fabric-api-0.154.2+26.2.jar",
                        "url": "https://cdn.modrinth.com/fabric-api-26.2.jar",
                        "size": 123,
                        "primary": true
                      }]
                    }]
                    """);
            }));
            using ModrinthCommunityResourceCatalog catalog = new(client);
            CommunityResourceEntry entry = new(
                "P7dR8mSH",
                "fabric-api",
                "Fabric API",
                string.Empty,
                "mod",
                null,
                0,
                null);

            CommunityResourceDownloadFile? file = await catalog.ResolveDownloadAsync(entry, options);

            Assert.AreEqual("26.2", options.GameVersion);
            Assert.AreEqual("fabric", options.Loader);
            Assert.AreEqual("fabric-api-0.154.2+26.2.jar", file?.FileName);
            string query = Uri.UnescapeDataString(captured!.RequestUri!.Query);
            StringAssert.Contains(query, "game_versions=[\"26.2\"]");
            StringAssert.Contains(query, "loaders=[\"fabric\"]");
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task CurseForgeCatalog_ShouldAuthenticateFilterAndParseProjects()
    {
        HttpRequestMessage? captured = null;
        using HttpClient client = new(new DelegateHandler(request =>
        {
            captured = request;
            return JsonResponse(
                """
                {
                  "data": [{
                    "id": 238222,
                    "name": "Just Enough Items",
                    "slug": "jei",
                    "summary": "Item and recipe viewing mod",
                    "downloadCount": 456789,
                    "dateModified": "2026-06-01T00:00:00Z",
                    "logo": { "thumbnailUrl": "https://media.forgecdn.net/jei.png" },
                    "links": { "websiteUrl": "https://www.curseforge.com/minecraft/mc-mods/jei" }
                  }]
                }
                """);
        }));
        using CurseForgeCommunityResourceCatalog catalog = new(client, "test-key");

        IReadOnlyList<CommunityResourceEntry> entries = await catalog.SearchAsync(
            CommunityResourceCategory.Mod,
            "jei",
            new CommunitySearchOptions(
                CommunityResourceSort.Downloads,
                GameVersion: "1.20.1",
                Loader: "fabric",
                Source: CommunityResourceSource.CurseForge));

        CommunityResourceEntry entry = entries.Single();
        Assert.AreEqual(CommunityResourceSource.CurseForge, entry.Source);
        Assert.AreEqual("238222", entry.ProjectId);
        Assert.AreEqual("https://www.curseforge.com/minecraft/mc-mods/jei", entry.WebsiteUrl);
        Assert.AreEqual("test-key", captured!.Headers.GetValues("x-api-key").Single());
        string query = Uri.UnescapeDataString(captured.RequestUri!.Query);
        StringAssert.Contains(query, "classId=6");
        StringAssert.Contains(query, "modLoaderType=4");
        StringAssert.Contains(query, "gameVersion=1.20.1");
        StringAssert.Contains(query, "sortField=6");
    }

    [TestMethod]
    public async Task CurseForgeCatalog_ShouldBuildCdnUrlWhenDownloadUrlIsMissing()
    {
        using HttpClient client = new(new DelegateHandler(_ => JsonResponse(
            """
            {
              "data": [{
                "id": 5678123,
                "displayName": "Example 1.0",
                "fileName": "Example Mod.jar",
                "downloadUrl": null,
                "fileLength": 1234,
                "fileDate": "2026-06-02T00:00:00Z",
                "gameVersions": ["1.20.1", "Fabric"]
              }]
            }
            """)));
        using CurseForgeCommunityResourceCatalog catalog = new(client, "test-key");
        CommunityResourceEntry entry = new("42", "example", "Example", string.Empty, "mod", null, 0L, null)
        {
            Source = CommunityResourceSource.CurseForge
        };

        IReadOnlyList<CommunityResourceVersion> versions = await catalog.GetVersionsAsync(entry);

        CommunityResourceVersion version = versions.Single();
        Assert.AreEqual("https://edge.forgecdn.net/files/5678/123/Example%20Mod.jar", version.Files.Single().Url);
        CollectionAssert.Contains(version.GameVersions.ToArray(), "1.20.1");
        CollectionAssert.Contains(version.Loaders.ToArray(), "fabric");
    }

    [TestMethod]
    public void FavoritesStore_ShouldPersistAndToggleBySourceAndProjectId()
    {
        string root = Path.Combine(Path.GetTempPath(), "pcln-favorites-test-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(root, "favorites.json");
        try
        {
            CommunityResourceEntry entry = new("AANobbMI", "sodium", "Sodium", "Fast", "mod", null, 10L, null);
            CommunityFavoritesStore store = new(path);

            Assert.IsTrue(store.Toggle(entry, CommunityResourceCategory.Mod));
            Assert.IsTrue(store.Contains(entry));

            CommunityFavoritesStore reloaded = new(path);
            Assert.IsTrue(reloaded.Contains(entry));
            Assert.AreEqual(CommunityResourceCategory.Mod, reloaded.Items.Single().Category);
            Assert.IsFalse(reloaded.Toggle(entry, CommunityResourceCategory.Mod));
            Assert.IsFalse(new CommunityFavoritesStore(path).Contains(entry));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(handler(request));
    }
}
