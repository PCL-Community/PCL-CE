using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.UI.Game.Log;

namespace PCL.Core.Test.Minecraft.Watcher;

[TestClass]
public sealed class GameLogViewModelTest
{
    [DataTestMethod]
    [DataRow(0, 50UL)]
    [DataRow(1, 60UL)]
    [DataRow(5, 100UL)]
    [DataRow(6, 150UL)]
    [DataRow(13, 500UL)]
    [DataRow(14, 600UL)]
    [DataRow(28, 2000UL)]
    [DataRow(29, ulong.MaxValue)]
    public void GetMaxLogCount_ReturnsConfiguredLimit(int configured, ulong expected)
    {
        Assert.AreEqual(expected, GameLogListViewModel.GetMaxLogCount(configured));
    }

    [TestMethod]
    public void FormatMaxLogHint_FormatsConfiguredValues()
    {
        Assert.AreEqual("100", GameLogContentViewModel.FormatMaxLogHint(5));
        Assert.AreEqual("500", GameLogContentViewModel.FormatMaxLogHint(13));
        Assert.AreEqual("2000", GameLogContentViewModel.FormatMaxLogHint(28));
    }
}