using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Navigation;

namespace PCL.Core.Test.Navigation;

[TestClass]
[DoNotParallelize]
public sealed class PageFactoryDependencyTest
{
    [TestMethod]
    public void PageHostFactoryResolvesConstructorDependencies()
    {
        var services = new ServiceCollection();
        services.AddSingleton<TestDependency>();
        using var provider = services.BuildServiceProvider();

        var descriptor = new PageDescriptor(
            PageIds.Launch,
            "Title",
            null,
            MainTab.Launch,
            300d,
            null,
            [],
            [],
            (sp, page) => ActivatorUtilities.CreateInstance<DependencyHost>(sp, page));

        var host = new PageHostFactory(provider).Create(descriptor);

        Assert.IsInstanceOfType<DependencyHost>(host);
        Assert.IsNotNull(((DependencyHost)host).Dependency);
    }

    private sealed class TestDependency;

    private sealed class DependencyHost(
        TestDependency dependency,
        PageDescriptor descriptor) : IPageHost
    {
        public TestDependency Dependency { get; } = dependency;

        public PageId Page => descriptor.Id;

        public MainTab? Tab => descriptor.Tab;

        public string GetTitle(PageRoute route)
        {
            return string.Empty;
        }

        public Control? GetLeft(PageRoute route)
        {
            return null;
        }

        public Control GetRight(PageRoute route)
        {
            return null!;
        }
    }
}