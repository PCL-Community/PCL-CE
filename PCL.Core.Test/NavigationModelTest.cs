using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Navigation;

namespace PCL.Core.Test;

[TestClass]
public class NavigationModelTest
{
    [TestMethod]
    public void PageRouteEqualityShouldComparePageAndParameter()
    {
        const string parameterA = "instance-a";
        const string parameterB = "instance-b";

        Assert.AreEqual(new PageRoute(PageIds.Launch), new PageRoute(PageIds.Launch));
        Assert.AreNotEqual(
            new PageRoute(PageIds.Launch),
            new PageRoute(PageIds.Download));

        var withParamA = new PageRoute(
            PageIds.InstanceSetup,
            SubPageIds.VersionOverall,
            parameterA);
        var withParamASame = new PageRoute(
            PageIds.InstanceSetup,
            SubPageIds.VersionOverall,
            parameterA);
        var withParamB = new PageRoute(
            PageIds.InstanceSetup,
            SubPageIds.VersionOverall,
            parameterB);
        Assert.AreEqual(withParamA, withParamASame);
        Assert.AreNotEqual(withParamA, withParamB);
    }

    [TestMethod]
    public void IsSameLocationShouldCompareSubPage()
    {
        var a = new PageRoute(PageIds.Download, SubPageIds.DownloadInstall);
        var b = new PageRoute(PageIds.Download, SubPageIds.DownloadInstall);
        var c = new PageRoute(PageIds.Download, SubPageIds.DownloadClient);

        Assert.IsTrue(a.IsSameLocation(b));
        Assert.IsFalse(a.IsSameLocation(c));
        Assert.IsFalse(a.IsSameLocation(null));
    }

    [TestMethod]
    public void TabPageMappingShouldMatchWpfBehaviour()
    {
        Assert.AreEqual(PageIds.Launch, PageRegistry.PageOfTab(MainTab.Launch));
        Assert.AreEqual(PageIds.Download, PageRegistry.PageOfTab(MainTab.Download));
        Assert.AreEqual(PageIds.Setup, PageRegistry.PageOfTab(MainTab.Setup));
        Assert.AreEqual(PageIds.Tools, PageRegistry.PageOfTab(MainTab.Tools));

        Assert.AreEqual(MainTab.Launch, PageRegistry.TabOfPage(PageIds.Launch));
        Assert.AreEqual(MainTab.Download, PageRegistry.TabOfPage(PageIds.Download));
        Assert.AreEqual(MainTab.Setup, PageRegistry.TabOfPage(PageIds.Setup));
        Assert.AreEqual(MainTab.Tools, PageRegistry.TabOfPage(PageIds.Tools));

        Assert.IsNull(PageRegistry.TabOfPage(PageIds.TaskManager));
        Assert.IsNull(PageRegistry.TabOfPage(PageIds.InstanceSelect));
        Assert.IsNull(PageRegistry.TabOfPage(PageIds.GameLog));
    }
}