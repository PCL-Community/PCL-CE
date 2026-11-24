using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PCL.Core.Test.Boundaries;

[TestClass]
public sealed class WindowCompositionArchitectureTest
{
    private static readonly string[] ProjectDirectories =
    [
        "PCL.Core",
        "PCL.Core.UI",
        "PCL.Launcher.Core",
        "PCL.Launcher"
    ];

    private static readonly string[] ForbiddenFragments =
    [
        "MainWindow.Current",
        "ApplicationHost.Current",
        "UiBridgeRegistration",
        "DialogPresenter.Instance",
        "HintPresenter.Instance"
    ];

    [TestMethod]
    public void WindowAndBridgeStaticsShouldNotReturn()
    {
        var root = GetRepositoryRoot();
        var violations = (
            from directory in ProjectDirectories
            select Path.Combine(root, directory)
            into projectRoot
            where Directory.Exists(projectRoot)
            from file in Directory.EnumerateFiles(
                projectRoot,
                "*.cs",
                SearchOption.AllDirectories)
            where !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                && !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
            let text = File.ReadAllText(file)
            from fragment in ForbiddenFragments
            where text.Contains(fragment, StringComparison.Ordinal)
            select $"{Path.GetRelativePath(root, file)}: {fragment}").ToList();

        Assert.IsEmpty(
            violations,
            $"窗口/桥接静态入口又出现了：{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
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
