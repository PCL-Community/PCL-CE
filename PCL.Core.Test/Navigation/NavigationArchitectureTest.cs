using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PCL.Core.Test.Navigation;

[TestClass]
public sealed class NavigationArchitectureTest
{
    [TestMethod]
    public void HostsShouldNotRediscoverPagesByReflectionOrSwitch()
    {
        var root = GetRepositoryRoot();
        AssertFileDoesNotContain(
            Path.Combine(root, "PCL.Launcher", "Navigation", "PageRegistry.cs"),
            "GetTypes(");
        AssertFileDoesNotContain(
            Path.Combine(root, "PCL.Launcher", "Navigation", "PageRegistry.cs"),
            "Activator.CreateInstance");

        foreach (
            var file in new[]
            {
                "PageSetupHost.cs",
                "PageDownloadHost.cs",
                "ToolsPageHost.cs",
                "InstanceSetupHost.cs",
                "VersionSavesHost.cs"
            })
        {
            var path = Directory.GetFiles(
                Path.Combine(root, "PCL.Launcher", "Views"),
                file,
                SearchOption.AllDirectories);
            Assert.HasCount(1, path);
            AssertFileDoesNotContain(path[0], "switch (sub");
            AssertFileDoesNotContain(path[0], "switch (subType");
        }
    }

    [TestMethod]
    public void PageCatalogShouldOnlyAggregateFeatureModules()
    {
        var root = GetRepositoryRoot();
        var catalogPath = Path.Combine(root, "PCL.Launcher", "Navigation", "PageCatalog.cs");
        var catalog = File.ReadAllText(catalogPath);
        Assert.IsFalse(catalog.Contains("new PageDescriptor(", StringComparison.Ordinal));
        Assert.IsTrue(catalog.Contains("PageCatalogBuilder.Build", StringComparison.Ordinal));

        var legacyNames = new[]
        {
            "PageStackData",
            "PageSubType",
            "PageType",
            "RouteKey"
        };
        foreach (var file in Directory.EnumerateFiles(
                     Path.Combine(root, "PCL.Launcher"),
                     "*.cs",
                     SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                continue;

            var text = File.ReadAllText(file);
            foreach (var legacyName in legacyNames)
                Assert.IsFalse(
                    text.Contains(legacyName, StringComparison.Ordinal),
                    $"{Path.GetRelativePath(root, file)} 仍包含旧页面类型 {legacyName}");
        }
    }

    [TestMethod]
    public void NavigationListsShouldBeDescriptorDriven()
    {
        var root = GetRepositoryRoot();
        AssertFileDoesNotContain(
            Path.Combine(root, "PCL.Launcher", "Views", "Shell", "TitleBar.axaml"),
            "BtnTitleSelect0");
        AssertFileDoesNotContain(
            Path.Combine(root, "PCL.Launcher", "Views", "Pages", "Setup", "PageSetupLeft.axaml"),
            "ItemLaunch");
        AssertFileDoesNotContain(
            Path.Combine(
                root,
                "PCL.Launcher",
                "Views",
                "Pages",
                "Download",
                "PageDownloadLeft.axaml"),
            "ItemInstall");
        AssertFileDoesNotContain(
            Path.Combine(root, "PCL.Launcher", "Views", "Pages", "Tools", "PageToolsLeft.axaml"),
            "ItemGameLink");
    }

    private static void AssertFileDoesNotContain(string path, string text)
    {
        Assert.IsTrue(File.Exists(path), $"文件不存在：{path}");
        Assert.IsFalse(
            File.ReadAllText(path).Contains(text, StringComparison.Ordinal),
            $"{Path.GetFileName(path)} 仍包含 {text}");
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "PCL.Core")))
                return directory.FullName;
            directory = directory.Parent;
        }

        Assert.Fail("无法定位仓库根目录");
        return string.Empty;
    }
}