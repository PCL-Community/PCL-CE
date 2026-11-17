using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Navigation;

namespace PCL.Core.Test;

[TestClass]
public sealed class NavigationServiceTest
{
    [TestMethod]
    public void PageLocationComparesPageAndSubPage()
    {
        Assert.AreEqual(
            new PageLocation(PageIds.Setup, SubPageIds.SetupLog),
            new PageLocation(PageIds.Setup, SubPageIds.SetupLog));
        Assert.AreNotEqual(
            new PageLocation(PageIds.Setup, SubPageIds.SetupLog),
            new PageLocation(PageIds.Setup, SubPageIds.SetupUi));
    }

    [TestMethod]
    public void NavigationServiceCurrentMatchesRouterDefault()
    {
        Assert.AreEqual(
            new PageLocation(PageIds.Launch, null),
            NavigationService.Instance.Current);
    }
}