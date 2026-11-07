using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Navigation;

namespace PCL.Core.Test;

[TestClass]
public sealed class NavigationServiceTest
{
    [TestMethod]
    public void RouteKeyComparesPageAndSubPage()
    {
        Assert.AreEqual(
            new RouteKey(PageType.Setup, PageSubType.SetupLog),
            new RouteKey(PageType.Setup, PageSubType.SetupLog));
        Assert.AreNotEqual(
            new RouteKey(PageType.Setup, PageSubType.SetupLog),
            new RouteKey(PageType.Setup, PageSubType.SetupUi));
    }

    [TestMethod]
    public void NavigationServiceCurrentMatchesRouterDefault()
    {
        Assert.AreEqual(
            new RouteKey(PageType.Launch),
            NavigationService.Instance.Current);
    }
}