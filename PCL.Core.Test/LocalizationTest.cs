using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Localization;

namespace PCL.Core.Test;

[TestClass]
public class LocalizationTest
{
    private static readonly string[] LanguageFiles =
    [
        .. LocalizationService
            .SupportedLanguages
            .Select(language => language.Code)
    ];

    [TestMethod]
    public void AllLanguageDictionariesShouldContainBaseKeys()
    {
        var baseKeys = LoadKeys("zh-CN");
        string[] partialLanguages = ["fr-FR", "ja-JP", "es-ES"];

        foreach (var language in LanguageFiles.Where(language => language != "zh-CN"))
        {
            var keys = LoadKeys(language);
            if (partialLanguages.Contains(language))
            {
                var unknown = keys.Except(baseKeys).ToArray();
                Assert.IsEmpty(unknown, $"{language} 存在基础语言没有的键：{string.Join(", ", unknown)}");
                continue;
            }

            var missing = baseKeys.Except(keys).ToArray();
            Assert.IsEmpty(missing, $"{language} 缺少语言键：{string.Join(", ", missing)}");
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

            Assert.IsEmpty(duplicated, $"{language} 存在重复语言键：{string.Join(", ", duplicated)}");
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

    [TestMethod]
    public void SupportedLanguagesShouldHaveValidCultureAndResourceDictionary()
    {
        foreach (var language in LocalizationService.SupportedLanguages)
        {
            _ = CultureInfo.GetCultureInfo(language.CultureName);
            Assert.IsTrue(
                File.Exists(GetLanguageFilePath(language.Code)),
                $"{language.Code} 缺少语言资源文件");
        }
    }

    [TestMethod]
    public void LanguageFontProfilesShouldMatchGlyphStandard()
    {
        Assert.AreEqual(LocalizationFontProfile.SimplifiedChinese, ProfileOf("zh-CN"));
        Assert.AreEqual(LocalizationFontProfile.TraditionalChinese, ProfileOf("zh-TW"));
        Assert.AreEqual(LocalizationFontProfile.Japanese, ProfileOf("ja-JP"));
        Assert.AreEqual(LocalizationFontProfile.English, ProfileOf("en-US"));
        Assert.AreEqual(LocalizationFontProfile.English, ProfileOf("en-GB"));
        Assert.AreEqual(LocalizationFontProfile.Other, ProfileOf("fr-FR"));
        Assert.AreEqual(LocalizationFontProfile.Other, ProfileOf("es-ES"));
        return;

        static LocalizationFontProfile ProfileOf(string code)
        {
            return LocalizationService.SupportedLanguages.Single(language => language.Code == code)
                .FontProfile;
        }
    }

    [TestMethod]
    public void FontProfileAliasesShouldNotMakeLanguageResourceSupported()
    {
        Assert.IsFalse(LocalizationService.IsLanguageSupported("zh-HK"));
        Assert.AreEqual(
            LocalizationService.DefaultLanguageCode,
            LocalizationService.ResolveLanguage("zh-HK").Code);
    }

    private static HashSet<string> LoadKeys(string language)
    {
        return [.. LoadKeyList(language)];
    }

    private static string[] LoadKeyList(string language)
    {
        var document = XDocument.Load(GetLanguageFilePath(language));
        var keyAttributeName = XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml");
        return
        [
            .. document
                .Descendants()
                .Select(element => element.Attribute(keyAttributeName)?.Value)
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Select(key => key!)
        ];
    }

    private static string GetLanguageFilePath(string language)
    {
        return Path.Combine(
            GetRepositoryRoot(),
            "PCL.Localization",
            "Languages",
            $"{language}.axaml");
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