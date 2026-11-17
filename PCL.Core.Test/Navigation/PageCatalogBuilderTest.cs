using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Navigation;

namespace PCL.Core.Test.Navigation;

[TestClass]
public sealed class PageCatalogBuilderTest
{
    [TestMethod]
    public void BuildAcceptsValidModules()
    {
        var snapshot = PageCatalogBuilder.Build(
        [
            new TestPageModule(
                PageIds.Launch,
                new MainTabDescriptor(
                    MainTab.Launch,
                    PageIds.Launch,
                    "Title",
                    "lucide/play",
                    0))
        ]);

        Assert.HasCount(1, snapshot.Pages);
        Assert.HasCount(1, snapshot.MainTabs);
    }

    [TestMethod]
    public void DuplicatePageIdIsRejected()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            PageCatalogBuilder.Build(
            [
                new TestPageModule(PageIds.Launch),
                new TestPageModule(PageIds.Launch)
            ]));
    }

    [TestMethod]
    public void SubPageFromAnotherParentIsRejected()
    {
        var page = CreatePage(
            PageIds.Download,
            [CreateSubPage(SubPageIds.SetupLaunch)],
            SubPageIds.SetupLaunch);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            PageCatalogBuilder.Build([new TestPageModule(page)]));
    }

    [TestMethod]
    public void MissingDefaultSubPageIsRejected()
    {
        var page = CreatePage(
            PageIds.Setup,
            [CreateSubPage(SubPageIds.SetupLaunch)],
            null);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            PageCatalogBuilder.Build([new TestPageModule(page)]));
    }

    [TestMethod]
    public void TabMustPointToExistingPage()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            PageCatalogBuilder.Build(
            [
                new TestPageModule(
                    PageIds.Launch,
                    new MainTabDescriptor(
                        MainTab.Launch,
                        PageIds.Download,
                        "Title",
                        "lucide/play",
                        0))
            ]));
    }

    private static PageDescriptor CreatePage(
        PageId pageId,
        IReadOnlyList<SubPageDescriptor> subs,
        SubPageId? defaultSubPage)
    {
        return new PageDescriptor(
            pageId,
            "Title",
            null,
            null,
            210d,
            defaultSubPage,
            [],
            subs,
            (_, _) => null!);
    }

    private static SubPageDescriptor CreateSubPage(SubPageId id)
    {
        return new SubPageDescriptor(
            id,
            "Title",
            null,
            null,
            0,
            (_, _) => new Control());
    }

    private sealed class TestPageModule(
        PageDescriptor page,
        IReadOnlyList<MainTabDescriptor>? tabs = null) : IPageModule
    {
        public TestPageModule(
            PageId pageId,
            MainTabDescriptor? tab = null)
            : this(CreatePage(pageId, [], null), tab is null ? [] : [tab])
        {
        }

        public IReadOnlyList<PageDescriptor> Pages { get; } = [page];

        public IReadOnlyList<MainTabDescriptor> MainTabs { get; } = tabs ?? [];
    }
}