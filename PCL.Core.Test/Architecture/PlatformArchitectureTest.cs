using System;
using System.Collections.Generic;
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
    public void CorePlatformBranchesShouldStayInLowLevelAllowList()
    {
        var root = GetRepositoryRoot();
        var projectRoot = Path.Combine(root, "PCL.Core");
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "App/Essentials/PromoteService.cs",
            "IO/Files.cs",
            "IO/PipeComm.cs",
            "Minecraft/Java/Scanner/DefaultPathsScanner.cs",
            "Platform/PlatformDetector.cs",
            "Utils/OS/EnvironmentInterop.cs",
            "Utils/OS/KernelInterop.cs",
            "Utils/OS/ProcessInterop.cs",
            "Utils/OS/SystemInfo.cs",
            "Utils/OS/SystemPaths.cs",
            "Utils/Secret/EncryptHelper.cs",
            "Utils/Secret/Identify.cs",
            "Utils/Secret/IdentifyOld.cs"
        };

        var violations = (
            from file in Directory.EnumerateFiles(projectRoot, "*.cs", SearchOption.AllDirectories)
            where !IsBuildOutput(file)
            let relative = Path.GetRelativePath(projectRoot, file).Replace('\\', '/')
            where !allowed.Contains(relative)
            where File.ReadAllText(file).Contains("OperatingSystem.Is", StringComparison.Ordinal)
                || File
                    .ReadAllText(file)
                    .Contains(
                        "RuntimeInformation.IsOSPlatform",
                        StringComparison.Ordinal)
            select relative).ToList();

        Assert.IsEmpty(
            violations,
            "PCL.Core 出现了未登记的平台分支；请改用平台服务或将实现加入底层白名单："
            + Environment.NewLine
            + string.Join(Environment.NewLine, violations));
    }

    [TestMethod]
    public void CoreUiShouldNotStartExternalProcessesDirectly()
    {
        var root = GetRepositoryRoot();
        var projectRoot = Path.Combine(root, "PCL.Core.UI");

        var violations = (
            from file in Directory.EnumerateFiles(projectRoot, "*.cs", SearchOption.AllDirectories)
            where !IsBuildOutput(file)
            let text = File.ReadAllText(file)
            where text.Contains("Process.Start(", StringComparison.Ordinal)
                || text.Contains("ProcessStartInfo", StringComparison.Ordinal)
                || text.Contains("new Process(", StringComparison.Ordinal)
            select ToRepositoryPath(root, file)).ToList();

        Assert.IsEmpty(
            violations,
            "PCL.Core.UI 仍在直接创建外部进程，请使用 IExternalCommandRunner："
            + Environment.NewLine
            + string.Join(Environment.NewLine, violations));
    }


    [TestMethod]
    public void LegacyPlatformFacadesShouldBeDeleted()
    {
        var root = GetRepositoryRoot();
        Assert.IsFalse(
            File.Exists(Path.Combine(root, "PCL.Core", "Platform", "PlatformRuntime.cs")),
            "PlatformRuntime 应已删除");
        Assert.IsFalse(
            File.Exists(Path.Combine(root, "PCL.Core", "Platform", "PlatformServices.cs")),
            "PlatformServices 应已删除");

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
            let text = File.ReadAllText(file)
            where text.Contains("PlatformRuntime", StringComparison.Ordinal)
                || text.Contains("PlatformServices.", StringComparison.Ordinal)
            select ToRepositoryPath(root, file)).ToList();

        Assert.IsEmpty(
            violations,
            "仍存在旧平台门面引用："
            + Environment.NewLine
            + string.Join(Environment.NewLine, violations));
    }

    [TestMethod]
    public void CorePlatformKindShouldStayInPlatformResolutionAllowList()
    {
        var root = GetRepositoryRoot();
        var projectRoot = Path.Combine(root, "PCL.Core");
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Link/Scaffolding/EasyTier/EasyTierPlatform.cs",
            "Platform/PlatformDetector.cs",
            "Platform/PlatformKind.cs",
            "Platform/PlatformModule.cs"
        };

        var violations = (
            from file in Directory.EnumerateFiles(projectRoot, "*.cs", SearchOption.AllDirectories)
            where !IsBuildOutput(file)
            let relative = Path.GetRelativePath(projectRoot, file).Replace('\\', '/')
            where !allowed.Contains(relative)
            where File.ReadAllText(file).Contains("PlatformKind", StringComparison.Ordinal)
            select relative).ToList();

        Assert.IsEmpty(
            violations,
            "PlatformKind 出现在了未登记的核心业务文件中："
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

    private static string ToRepositoryPath(string root, string file)
    {
        return Path.GetRelativePath(root, file).Replace('\\', '/');
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