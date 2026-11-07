using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.ResourceProject.Comp.Clients;
using PCL.Core.Minecraft.ResourceProject.Comp.Models;
using PCL.Core.Minecraft.ResourceProject.Comp.Models.Enums;

namespace PCL.Core.Test.Minecraft.ResourceProject.Comp;

[TestClass]
public class CompClientTest
{
    private static CurseForgeClient? _cf;
    private static ModrinthClient? _mr;
    private static bool _hasCfKey;
    private static HttpClient _httpClient = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext _)
    {
        _httpClient = new HttpClient();
        var key = Environment.GetEnvironmentVariable("PCL_CURSEFORGE_API_KEY");
        _hasCfKey = !string.IsNullOrEmpty(key);
        if (_hasCfKey)
            _cf = new CurseForgeClient(key!, _httpClient);
        _mr = new ModrinthClient(httpClient: _httpClient);
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        _httpClient.Dispose();
    }

    // ===== CurseForge =====

    [TestMethod]
    public async Task CurseForge_SearchProjects_ReturnsResults()
    {
        if (!_hasCfKey) Assert.Inconclusive("PCL_CURSEFORGE_API_KEY not set");
        var filter = new CompSearchFilter { Query = "sodium", Limit = 5 };
        var result = await _cf!.SearchProjects(filter);
        Assert.IsTrue(result.Hits.Count > 0);
        Assert.IsTrue(result.TotalCount > 0);
        Assert.IsFalse(string.IsNullOrEmpty(result.Hits[0].Name));
    }

    [TestMethod]
    public async Task CurseForge_GetProject_ReturnsProject()
    {
        if (!_hasCfKey) Assert.Inconclusive("PCL_CURSEFORGE_API_KEY not set");
        var project = await _cf!.GetProject("394468");
        Assert.AreEqual("394468", project.Id);
        Assert.AreEqual("CurseForge", project.Provider);
        Assert.IsFalse(string.IsNullOrEmpty(project.Name));
        Assert.IsFalse(string.IsNullOrEmpty(project.Summary));
    }

    [TestMethod]
    public async Task CurseForge_GetProjectFiles_ReturnsFiles()
    {
        if (!_hasCfKey) Assert.Inconclusive("PCL_CURSEFORGE_API_KEY not set");
        var files = await _cf!.GetProjectFiles("394468");
        Assert.IsTrue(files.Count > 0);
        Assert.IsFalse(string.IsNullOrEmpty(files[0].Id));
        Assert.IsFalse(string.IsNullOrEmpty(files[0].DisplayName));
    }

    [TestMethod]
    public async Task CurseForge_GetGameVersions_ReturnsVersions()
    {
        if (!_hasCfKey) Assert.Inconclusive("PCL_CURSEFORGE_API_KEY not set");
        var versions = await _cf!.GetGameVersions();
        Assert.IsTrue(versions.Count > 0);
        Assert.IsTrue(versions.Any(v => v.Version == "1.20.1" || v.Version.Contains("1.20")));
    }

    [TestMethod]
    public async Task CurseForge_GetCategories_ReturnsCategories()
    {
        if (!_hasCfKey) Assert.Inconclusive("PCL_CURSEFORGE_API_KEY not set");
        var categories = await _cf!.GetCategories();
        Assert.IsTrue(categories.Count > 0);
        Assert.IsFalse(string.IsNullOrEmpty(categories[0].Name));
    }

    // ===== Modrinth =====

    [TestMethod]
    public async Task Modrinth_SearchProjects_ReturnsResults()
    {
        var filter = new CompSearchFilter { Query = "sodium", Limit = 5 };
        var result = await _mr!.SearchProjects(filter);
        Assert.IsTrue(result.Hits.Count > 0);
        Assert.IsTrue(result.TotalCount > 0);
        Assert.IsFalse(string.IsNullOrEmpty(result.Hits[0].Name));
    }

    [TestMethod]
    public async Task Modrinth_GetProject_ReturnsProject()
    {
        var project = await _mr!.GetProject("sodium");
        Assert.AreEqual("sodium", project.Slug);
        Assert.AreEqual("Modrinth", project.Provider);
        Assert.IsFalse(string.IsNullOrEmpty(project.Name));
    }

    [TestMethod]
    public async Task Modrinth_GetProjectFiles_ReturnsFiles()
    {
        var files = await _mr!.GetProjectFiles("sodium");
        Assert.IsTrue(files.Count > 0);
        Assert.IsFalse(string.IsNullOrEmpty(files[0].Id));
        Assert.IsFalse(string.IsNullOrEmpty(files[0].DisplayName));
    }

    [TestMethod]
    public async Task Modrinth_GetGameVersions_ReturnsVersions()
    {
        var versions = await _mr!.GetGameVersions();
        Assert.IsTrue(versions.Count > 0);
        Assert.IsTrue(versions.Any(v => v.Version == "1.20.1" || v.Version.Contains("1.20")));
    }

    [TestMethod]
    public async Task Modrinth_GetCategories_ReturnsCategories()
    {
        var categories = await _mr!.GetCategories();
        Assert.IsTrue(categories.Count > 0);
        Assert.IsFalse(string.IsNullOrEmpty(categories[0].Name));
    }

    [TestMethod]
    public async Task Modrinth_GetLoaders_ReturnsLoaders()
    {
        var loaders = await _mr!.GetLoaders();
        Assert.IsTrue(loaders.Count > 0);
        Assert.IsTrue(loaders.Any(l => l.LoaderType == ModLoaderType.Forge));
    }

    // ===== Aggregate =====

    [TestMethod]
    public async Task Aggregate_SearchProjects_MergesResults()
    {
        if (!_hasCfKey) Assert.Inconclusive("PCL_CURSEFORGE_API_KEY not set");
        var agg = new AggregateClient(_cf!, _mr!);
        var filter = new CompSearchFilter { Query = "sodium", Limit = 3 };
        var result = await agg.SearchProjects(filter);
        Assert.IsTrue(result.Hits.Count > 0);
        Assert.IsTrue(result.Hits.Any(p => p.Provider == "CurseForge"));
        Assert.IsTrue(result.Hits.Any(p => p.Provider == "Modrinth"));
    }
}
