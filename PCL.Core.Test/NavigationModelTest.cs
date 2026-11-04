using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Navigation;

namespace PCL.Core.Test;

[TestClass]
public class NavigationModelTest
{
    [TestMethod]
    public void PageStackDataEqualityShouldComparePageAndParameter()
    {
        var instanceA = new object();
        var instanceB = new object();

        Assert.AreEqual(new PageStackData(PageType.Launch), new PageStackData(PageType.Launch));
        Assert.AreNotEqual(new PageStackData(PageType.Launch), new PageStackData(PageType.Download));

        var withParamA = new PageStackData(PageType.InstanceSetup, PageSubType.VersionOverall, instanceA);
        var withParamASame = new PageStackData(PageType.InstanceSetup, PageSubType.VersionOverall, instanceA);
        var withParamB = new PageStackData(PageType.InstanceSetup, PageSubType.VersionOverall, instanceB);
        Assert.AreEqual(withParamA, withParamASame);
        Assert.AreNotEqual(withParamA, withParamB);
    }

    [TestMethod]
    public void IsSameLocationShouldCompareSubType()
    {
        var a = new PageStackData(PageType.Download, PageSubType.DownloadInstall);
        var b = new PageStackData(PageType.Download, PageSubType.DownloadInstall);
        var c = new PageStackData(PageType.Download, PageSubType.DownloadClient);

        Assert.IsTrue(a.IsSameLocation(b));
        Assert.IsFalse(a.IsSameLocation(c));
        Assert.IsFalse(a.IsSameLocation(null));
    }

    [TestMethod]
    public void TabPageMappingShouldMatchWpfBehaviour()
    {
        Assert.AreEqual(PageType.Launch, PageRegistry.PageOfTab(MainTab.Launch));
        Assert.AreEqual(PageType.Download, PageRegistry.PageOfTab(MainTab.Download));
        Assert.AreEqual(PageType.Setup, PageRegistry.PageOfTab(MainTab.Setup));
        Assert.AreEqual(PageType.Tools, PageRegistry.PageOfTab(MainTab.Tools));

        Assert.AreEqual(MainTab.Launch, PageRegistry.TabOfPage(PageType.Launch));
        Assert.AreEqual(MainTab.Download, PageRegistry.TabOfPage(PageType.Download));
        Assert.AreEqual(MainTab.Setup, PageRegistry.TabOfPage(PageType.Setup));
        Assert.AreEqual(MainTab.Tools, PageRegistry.TabOfPage(PageType.Tools));

        Assert.IsNull(PageRegistry.TabOfPage(PageType.TaskManager));
        Assert.IsNull(PageRegistry.TabOfPage(PageType.InstanceSelect));
        Assert.IsNull(PageRegistry.TabOfPage(PageType.GameLog));
    }

    [TestMethod]
    public void PageTypeValuesShouldStayStable()
    {
        Assert.AreEqual(0, (int)PageType.Launch);
        Assert.AreEqual(1, (int)PageType.Download);
        Assert.AreEqual(2, (int)PageType.Setup);
        Assert.AreEqual(3, (int)PageType.Tools);
        Assert.AreEqual(5, (int)PageType.InstanceSelect);
        Assert.AreEqual(6, (int)PageType.TaskManager);
        Assert.AreEqual(7, (int)PageType.InstanceSetup);
        Assert.AreEqual(8, (int)PageType.CompDetail);
        Assert.AreEqual(9, (int)PageType.HelpDetail);
        Assert.AreEqual(10, (int)PageType.GameLog);
        Assert.AreEqual(12, (int)PageType.VersionSaves);
    }
}
