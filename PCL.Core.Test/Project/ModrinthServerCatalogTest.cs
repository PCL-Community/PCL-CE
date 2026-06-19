using System.Linq;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Online;

namespace PCL.Core.Test.Project;

[TestClass]
public class ModrinthServerCatalogTest
{
    [TestMethod]
    public void ParseServerEntryShouldPreferDetailPayload()
    {
        var hit = new JsonObject
        {
            ["project_id"] = "search-id",
            ["slug"] = "search-slug",
            ["title"] = "Search title",
            ["description"] = "Search description",
            ["versions"] = new JsonArray("1.20.1")
        };
        var detail = new JsonObject
        {
            ["id"] = "detail-id",
            ["slug"] = "detail-slug",
            ["name"] = "Detail title",
            ["summary"] = "Detail summary",
            ["icon_url"] = "https://example.invalid/icon.png",
            ["game_versions"] = new JsonArray("1.21.6", "1.21.6", "1.21.5"),
            ["minecraft_java_server"] = new JsonObject
            {
                ["address"] = "play.example.invalid",
                ["ping"] = new JsonObject
                {
                    ["data"] = new JsonObject
                    {
                        ["players_online"] = 42,
                        ["players_max"] = 100
                    }
                }
            }
        };

        var entry = ModrinthServerCatalog.ParseServerEntry(hit, detail);

        Assert.IsNotNull(entry);
        Assert.AreEqual("detail-id", entry.ProjectId);
        Assert.AreEqual("detail-slug", entry.Slug);
        Assert.AreEqual("Detail title", entry.Title);
        Assert.AreEqual("Detail summary", entry.Description);
        Assert.AreEqual("play.example.invalid", entry.Address);
        CollectionAssert.AreEqual(new[] { "1.21.6", "1.21.5" }, entry.Versions.ToArray());
        Assert.AreEqual(42, entry.PlayersOnline);
        Assert.AreEqual(100, entry.PlayersMax);
    }
}
