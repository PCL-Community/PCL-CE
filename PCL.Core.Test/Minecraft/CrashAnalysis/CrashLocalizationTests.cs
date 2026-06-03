using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft.CrashAnalysis;

namespace PCL.Core.Test.Minecraft.CrashAnalysis;

[TestClass]
public sealed class CrashLocalizationTests
{
    private static readonly string[] LanguageCodes = LocalizationService.SupportedLanguages
        .Select(static language => language.Code)
        .ToArray();

    [TestMethod]
    public void AllCrashDiagnosisCodesHaveLocalizationKey()
    {
        var baseKeys = _LoadResources("zh-CN");
        foreach (var code in Enum.GetValues<CrashDiagnosisCode>())
        {
            Assert.IsTrue(baseKeys.ContainsKey($"Crash.Diagnosis.Title.{code}"), $"缺少 Crash.Diagnosis.Title.{code}");
            Assert.IsTrue(baseKeys.ContainsKey($"Crash.Diagnosis.Description.{code}"),
                $"缺少 Crash.Diagnosis.Description.{code}");
        }
    }

    [TestMethod]
    public void AllCrashLocalizationKeysExistInEveryLanguage()
    {
        var baseKeys = _LoadResources("zh-CN").Keys
            .Where(static key => key.StartsWith("Crash.", StringComparison.Ordinal))
            .ToArray();

        foreach (var languageCode in LanguageCodes.Where(static code => code != "zh-CN"))
        {
            var keys = _LoadResources(languageCode).Keys.ToHashSet(StringComparer.Ordinal);
            var missing = baseKeys.Where(key => !keys.Contains(key)).ToArray();
            Assert.IsEmpty(missing, $"{languageCode} 缺少 Crash 语言键：{string.Join(", ", missing)}");
        }
    }

    [TestMethod]
    public void CrashLocalizationPlaceholdersMatchAcrossLanguages()
    {
        var baseResources = _LoadResources("zh-CN");
        foreach (var languageCode in LanguageCodes.Where(static code => code != "zh-CN"))
        {
            var resources = _LoadResources(languageCode);
            foreach (var (key, value) in baseResources.Where(static pair =>
                         pair.Key.StartsWith("Crash.", StringComparison.Ordinal)))
            {
                if (!resources.TryGetValue(key, out var localizedValue)) continue;
                CollectionAssert.AreEquivalent(
                    _GetPlaceholders(value),
                    _GetPlaceholders(localizedValue),
                    $"{languageCode} 的 {key} 占位符不一致");
            }
        }
    }

    private static Dictionary<string, string> _LoadResources(string languageCode)
    {
        var filePath = Path.Combine(_GetRepositoryRoot(), "PCL.Core", "App", "Localization", "Languages",
            languageCode + ".xaml");
        var document = XDocument.Load(filePath);
        var keyAttributeName = XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml");
        return document.Descendants()
            .Select(element => new
            {
                Key = element.Attribute(keyAttributeName)?.Value, element.Value
            })
            .Where(static item => !string.IsNullOrWhiteSpace(item.Key))
            .ToDictionary(static item => item.Key!, static item => item.Value);
    }

    private static string[] _GetPlaceholders(string value)
    {
        return Regex.Matches(value, @"\{[A-Za-z][A-Za-z0-9_]*\}|\{\d+(?::[^}]*)?\}")
            .Select(static match => match.Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static placeholder => placeholder, StringComparer.Ordinal)
            .ToArray();
    }

    private static string _GetRepositoryRoot()
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