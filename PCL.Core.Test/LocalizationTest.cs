using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PCL.Core.Test;

[TestClass]
public class LocalizationTest
{
    private static readonly string[] LanguageFiles = ["zh-CN", "en-US", "zh-TW"];

    [TestMethod]
    public void AllLanguageDictionariesShouldContainBaseKeys()
    {
        var baseKeys = LoadKeys("zh-CN");

        foreach (var language in LanguageFiles.Where(language => language != "zh-CN"))
        {
            var keys = LoadKeys(language);
            var missing = baseKeys.Except(keys).ToArray();

            Assert.AreEqual(0, missing.Length, $"{language} 缺少语言键：{string.Join(", ", missing)}");
        }
    }


    [TestMethod]
    public void LanguageDictionariesShouldNotContainDuplicateKeys()
    {
        foreach (var language in LanguageFiles)
        {
            var keys = LoadKeyList(language);
            var duplicated = keys
                .GroupBy(key => key)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();

            Assert.AreEqual(0, duplicated.Length, $"{language} 存在重复语言键：{string.Join(", ", duplicated)}");
        }
    }

    [TestMethod]
    public void LanguageKeysShouldUseDotNaming()
    {
        foreach (var language in LanguageFiles)
        foreach (var key in LoadKeys(language))
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(key), $"{language} 存在空语言键");
            Assert.IsTrue(key.Contains('.'), $"{language} 的语言键缺少分组分隔符：{key}");
            Assert.IsFalse(key.Contains(' '), $"{language} 的语言键不应包含空格：{key}");
        }
    }

    private static HashSet<string> LoadKeys(string language)
    {
        return LoadKeyList(language).ToHashSet();
    }

    private static string[] LoadKeyList(string language)
    {
        var filePath = Path.Combine(GetRepositoryRoot(), "PCL.Core", "App", "Localization", "Languages",
            language + ".xaml");
        var document = XDocument.Load(filePath);
        var keyAttributeName = XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml");
        return document.Descendants()
            .Select(element => element.Attribute(keyAttributeName)?.Value)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key!)
            .ToArray();
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "PCL.Core"))) return directory.FullName;
            directory = directory.Parent;
        }

        Assert.Fail("无法定位仓库根目录");
        return string.Empty;
    }
}