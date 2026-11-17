using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PCL.Core.Test.Boundaries;

[TestClass]
public sealed class PlatformArchitectureTest
{
    [TestMethod]
    public void WindowsPathCapabilityShouldNotLeakIntoBusinessCode()
    {
        var root = GetRepositoryRoot();
        var violations = (
            from directory in new[]
            {
                "PCL.Core",
                "PCL.Core.UI",
                "PCL.Launcher.Core",
                "PCL.Launcher"
            }
            select Path.Combine(root, directory)
            into projectRoot
            where Directory.Exists(projectRoot)
            from file in Directory.EnumerateFiles(projectRoot, "*.cs", SearchOption.AllDirectories)
            where !IsBuildOutput(file)
            where File.ReadAllText(file).Contains("UsesWindowsPaths", StringComparison.Ordinal)
            select Path.GetRelativePath(root, file)).ToList();

        Assert.IsEmpty(
            violations,
            "UsesWindowsPaths 仍然泄漏到业务代码："
            + Environment.NewLine
            + string.Join(Environment.NewLine, violations));
    }

    [TestMethod]
    public void PlatformKindShouldStayAtCompositionAndPlatformResolutionBoundaries()
    {
        var root = GetRepositoryRoot();
        var projectRoot = Path.Combine(root, "PCL.Launcher.Core");

        var violations = (
            from file in Directory.EnumerateFiles(
                projectRoot,
                "*.cs",
                SearchOption.AllDirectories)
            where !IsBuildOutput(file)
            where !file.Contains(
                $"{Path.DirectorySeparatorChar}"
                + $"Link{Path.DirectorySeparatorChar}"
                + $"Scaffolding{Path.DirectorySeparatorChar}"
                + $"EasyTier{Path.DirectorySeparatorChar}")
            where File.ReadAllText(file).Contains("PlatformKind", StringComparison.Ordinal)
            select Path.GetRelativePath(root, file)).ToList();

        Assert.IsEmpty(
            violations,
            "PlatformKind 仍然出现在业务层："
            + Environment.NewLine
            + string.Join(Environment.NewLine, violations));
    }

    private static bool IsBuildOutput(string file)
    {
        return file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
            || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}");
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