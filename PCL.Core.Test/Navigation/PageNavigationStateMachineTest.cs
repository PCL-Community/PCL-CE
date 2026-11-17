using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Navigation;

namespace PCL.Core.Test.Navigation;

[TestClass]
public sealed class PageNavigationStateMachineTest
{
    [TestMethod]
    public void StartsAtLaunchWithoutBackStack()
    {
        var state = new PageNavigationStateMachine();

        Assert.AreEqual(PageIds.Launch, state.Current.Page);
        Assert.IsFalse(state.CanGoBack);
        Assert.IsEmpty(state.BackStack);
    }

    [TestMethod]
    public void SecondaryNavigationPushesCurrentPage()
    {
        var state = new PageNavigationStateMachine();

        state.Navigate(
            new PageRoute(PageIds.Setup, SubPageIds.SetupLog),
            true,
            MainTab.Setup);

        Assert.AreEqual(PageIds.Setup, state.Current.Page);
        Assert.AreEqual(SubPageIds.SetupLog, state.Current.SubPage);
        Assert.IsTrue(state.CanGoBack);
        Assert.HasCount(1, state.BackStack);
        Assert.AreEqual(PageIds.Launch, state.BackStack[0].Page);
    }

    [TestMethod]
    public void PrimaryNavigationClearsBackStack()
    {
        var state = new PageNavigationStateMachine();
        state.Navigate(
            new PageRoute(PageIds.Setup, SubPageIds.SetupLog),
            true,
            MainTab.Setup);

        state.Navigate(new PageRoute(PageIds.Download), false, MainTab.Download);

        Assert.AreEqual(PageIds.Download, state.Current.Page);
        Assert.IsFalse(state.CanGoBack);
        Assert.IsEmpty(state.BackStack);
    }

    [TestMethod]
    public void BackReturnsFirstSecondaryPage()
    {
        var state = new PageNavigationStateMachine();
        state.Navigate(
            new PageRoute(PageIds.Setup, SubPageIds.SetupLog),
            true,
            MainTab.Setup);

        var canGoBack = state.Back(out var target);

        Assert.IsTrue(canGoBack);
        Assert.AreEqual(PageIds.Launch, target.Page);
    }

    [TestMethod]
    public void TabStateIsPreservedUntilReplaced()
    {
        var state = new PageNavigationStateMachine();
        state.Navigate(
            new PageRoute(PageIds.Setup, SubPageIds.SetupUi),
            true,
            MainTab.Setup);
        var setup = state.GetTabState(MainTab.Setup);

        state.Navigate(new PageRoute(PageIds.Download), false, MainTab.Download);

        Assert.IsNotNull(setup);
        Assert.AreEqual(SubPageIds.SetupUi, setup.SubPage);
        Assert.AreEqual(SubPageIds.SetupUi, state.GetTabState(MainTab.Setup)?.SubPage);
        Assert.AreEqual(PageIds.Download, state.GetTabState(MainTab.Download)?.Page);
    }
}