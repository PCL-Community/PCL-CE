using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PCL.Core.Test;

[TestClass]
public class AssetUsageTest
{
    private static readonly Regex SvgRegex = new(
        "SvgIcon\\s*=\\s*\"lucide/([^\"]+)\"",
        RegexOptions.Compiled);

    private static readonly Regex BlockRegex = new(
        "\"Blocks/([^\"]+)\"",
        RegexOptions.Compiled);

    [TestMethod]
    public void ReferencedSvgAndBlockAssetsShouldExist()
    {
        var root = GetRepositoryRoot();
        var launcherRoot = Path.Combine(root, "PCL.Launcher");
        var iconRoot = Path.Combine(launcherRoot, "Assets", "IconPacks", "lucide");
        var imageRoot = Path.Combine(launcherRoot, "Assets", "Images");
        var missing = new System.Collections.Generic.List<string>();

        foreach (var file in Directory.EnumerateFiles(launcherRoot, "*.*", SearchOption.AllDirectories))
        {
            var extension = Path.GetExtension(file);
            if (extension is not (".cs" or ".axaml")) continue;
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                continue;

            var text = File.ReadAllText(file);
            foreach (Match match in SvgRegex.Matches(text))
            {
                var name = match.Groups[1].Value;
                if (!File.Exists(Path.Combine(iconRoot, name + ".svg")))
                    missing.Add($"lucide/{name} ({Path.GetFileName(file)})");
            }

            foreach (Match match in BlockRegex.Matches(text))
            {
                var name = match.Groups[1].Value;
                if (!File.Exists(Path.Combine(imageRoot, "Blocks", name)))
                    missing.Add($"Blocks/{name} ({Path.GetFileName(file)})");
            }
        }

        Assert.IsEmpty(
            missing.Distinct().OrderBy(item => item).ToArray(),
            $"引用的图标/方块资源不存在：{string.Join(", ", missing.Distinct())}");
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
