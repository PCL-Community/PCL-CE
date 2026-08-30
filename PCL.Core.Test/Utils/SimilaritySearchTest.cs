using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Utils;

namespace PCL.Core.Test.Utils;

[TestClass]
public class SimilaritySearchTest
{
    [TestMethod]
    public void PreservesAliasGroupsWithoutDilutingSimilarity()
    {
        var entryWithAliases = new SearchEntry<string>(
            "JEI",
            [
                new SearchSource(["物品管理器", "Just Enough Items", "JEI"]),
                new SearchSource("mezz jei", 0.5d)
            ]);

        var results = SimilaritySearch.Search(
            [entryWithAliases],
            "物品管理器",
            10,
            0.2);
        Assert.HasCount(1, results);
        Assert.AreEqual("JEI", results[0].Item);
        Assert.IsGreaterThanOrEqualTo(0.2, results[0].Similarity);
    }
}