using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PCL.Core.Test;

[TestClass]
public partial class LocalizationUsageTest
{
    [TestMethod]
    public void StaticLocalizationKeysShouldExistInBaseLanguage()
    {
        var root = GetRepositoryRoot();
        var baseKeys = LoadKeys(Path.Combine(root, "PCL.Localization", "Languages", "zh-CN.axaml"));
        var used = new HashSet<string>(StringComparer.Ordinal);

        foreach (var directory in new[]
                 {
                     Path.Combine(root, "PCL.Launcher"),
                     Path.Combine(root, "PCL.Core"),
                     Path.Combine(root, "PCL.Core.UI"),
                     Path.Combine(root, "PCL.Launcher.Core")
                 })
        {
            if (!Directory.Exists(directory)) continue;
            foreach (var file in Directory.EnumerateFiles(
                         directory,
                         "*.*",
                         SearchOption.AllDirectories))
            {
                var extension = Path.GetExtension(file);
                if (extension is not (".cs" or ".axaml")) continue;
                if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                    || file.Contains(
                        $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                    continue;

                var text = File.ReadAllText(file);
                foreach (Match match in LangTextRegex().Matches(text))
                    used.Add(match.Groups[1].Value);
                foreach (Match match in LocRegex().Matches(text))
                    used.Add(match.Groups[1].Value);
            }
        }

        var missing = used.Except(baseKeys).OrderBy(key => key, StringComparer.Ordinal).ToArray();
        Assert.IsEmpty(
            missing,
            $"以下代码引用的本地化键在 zh-CN 中不存在：{string.Join(", ", missing)}");
    }

    private static HashSet<string> LoadKeys(string path)
    {
        var document = XDocument.Load(path);
        return document
            .Descendants()
            .Where(element => element.Name.LocalName == "String")
            .Select(element =>
                element.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml"))
                    ?.Value)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key!)
            .ToHashSet(StringComparer.Ordinal);
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

    [GeneratedRegex("Lang\\.Text\\(\\s*\"([^\"]+)\"", RegexOptions.Compiled)]
    private static partial Regex LangTextRegex();

    [GeneratedRegex(@"\{loc:Loc\s+([A-Za-z0-9_.]+)\s*\}", RegexOptions.Compiled)]
    private static partial Regex LocRegex();
}