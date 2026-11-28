using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Views.Pages.Homepage;

namespace PCL.Test;

[TestClass]
public sealed class HomepagePresetTableTest
{
    [TestMethod]
    public void KeepsSupportedPresetIndexes()
    {
        Assert.AreEqual(HomepageSourceKind.Trivia, HomepagePresetTable.Find(0)?.Kind);
        Assert.AreEqual(HomepageSourceKind.CustomAxaml, HomepagePresetTable.Find(7)?.Kind);
        Assert.AreEqual(HomepageSourceKind.NewsFeed, HomepagePresetTable.Find(15)?.Kind);
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(6)]
    [DataRow(8)]
    [DataRow(14)]
    [DataRow(16)]
    public void UnknownPresetIndexReturnsNull(int index)
    {
        Assert.IsNull(HomepagePresetTable.Find(index));
    }
}