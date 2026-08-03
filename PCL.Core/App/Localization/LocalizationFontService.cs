using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using PCL.Core.App.IoC;

namespace PCL.Core.App.Localization;

/// <summary>
///     启动器 UI 字体服务。
/// </summary>
public static class LocalizationFontService
{
    private const string PclEnglishFont = "./Resources/#PCL English";
    private static readonly Uri _ApplicationPackUri = new("pack://application:,,,/");

    private static readonly IReadOnlyDictionary<string, LocalizationFontProfile> _ExactCultureProfiles =
        new Dictionary<string, LocalizationFontProfile>(StringComparer.OrdinalIgnoreCase)
        {
            ["zh-CN"] = LocalizationFontProfile.SimplifiedChinese,
            ["zh-SG"] = LocalizationFontProfile.SimplifiedChinese,
            ["zh-TW"] = LocalizationFontProfile.TraditionalChinese,
            ["zh-HK"] = LocalizationFontProfile.TraditionalChinese,
            ["zh-MO"] = LocalizationFontProfile.TraditionalChinese
        };

    private static readonly IReadOnlyDictionary<string, LocalizationFontProfile> _ScriptProfiles =
        new Dictionary<string, LocalizationFontProfile>(StringComparer.OrdinalIgnoreCase)
        {
            ["Hans"] = LocalizationFontProfile.SimplifiedChinese,
            ["Hant"] = LocalizationFontProfile.TraditionalChinese
        };

    private static readonly IReadOnlyDictionary<string, LocalizationFontProfile> _LanguageProfiles =
        new Dictionary<string, LocalizationFontProfile>(StringComparer.OrdinalIgnoreCase)
        {
            ["zh"] = LocalizationFontProfile.SimplifiedChinese,
            ["ja"] = LocalizationFontProfile.Japanese,
            ["ko"] = LocalizationFontProfile.Korean,
            ["en"] = LocalizationFontProfile.English
        };

    /// <summary>
    ///     生成启动器 UI 使用的 FontFamily。
    /// </summary>
    /// <param name="customFontName">用户自定义字体。为空时使用当前语言默认字体链。</param>
    /// <param name="language">目标语言。为空时使用当前 UI 语言。</param>
    public static FontFamily BuildLaunchFontFamily(string? customFontName = null, LocalizationLanguage? language = null)
    {
        language ??= LocalizationService.CurrentLanguage;

        var familyNames = string.IsNullOrWhiteSpace(customFontName)
            ? _GetDefaultFamilyNames(language.FontProfile)
            : _GetCustomFamilyNames(customFontName, language.FontProfile);

        return new FontFamily(_ApplicationPackUri, string.Join(", ", familyNames));
    }

    /// <summary>
    ///     生成用于代表某种语言的 FontFamily。
    /// </summary>
    public static FontFamily BuildRepresentativeFontFamily(LocalizationLanguage language)
    {
        return BuildRepresentativeFontFamily(language.FontProfile);
    }

    /// <summary>
    ///     生成用于代表某种字体策略的 FontFamily。
    /// </summary>
    public static FontFamily BuildRepresentativeFontFamily(LocalizationFontProfile profile)
    {
        return new FontFamily(_ApplicationPackUri, string.Join(", ", _GetDefaultFamilyNames(profile)));
    }

    /// <summary>
    ///     应用启动器 UI 字体。
    /// </summary>
    public static void ApplyLaunchFont(string? customFontName = null, LocalizationLanguage? language = null)
    {
        var app = Application.Current ?? Lifecycle.CurrentApplication;

        var fontFamily = BuildLaunchFontFamily(customFontName, language);
        if (!app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.Invoke(() => app.Resources["LaunchFontFamily"] = fontFamily);
            return;
        }

        app.Resources["LaunchFontFamily"] = fontFamily;
    }

    /// <summary>
    ///     根据区域性名称解析字体策略。
    /// </summary>
    public static LocalizationFontProfile ResolveProfileFromCultureName(string? cultureName)
    {
        var code = _NormalizeCultureCode(cultureName);
        if (string.IsNullOrEmpty(code)) return LocalizationFontProfile.Other;

        if (_ExactCultureProfiles.TryGetValue(code, out var exactProfile)) return exactProfile;

        var cultureParts = code.Split('-', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in cultureParts.Skip(1))
            if (_ScriptProfiles.TryGetValue(part, out var scriptProfile))
                return scriptProfile;

        var languageCode = cultureParts.FirstOrDefault();
        return languageCode is not null && _LanguageProfiles.TryGetValue(languageCode, out var languageProfile)
            ? languageProfile
            : LocalizationFontProfile.Other;
    }

    private static string _NormalizeCultureCode(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace('_', '-').Trim();
    }

    private static IReadOnlyList<string> _GetDefaultFamilyNames(LocalizationFontProfile profile)
    {
        return profile switch
        {
            LocalizationFontProfile.English =>
                [PclEnglishFont, "Segoe UI", "Microsoft YaHei UI"],

            LocalizationFontProfile.SimplifiedChinese =>
                [PclEnglishFont, "Microsoft YaHei UI", "Segoe UI"],

            LocalizationFontProfile.TraditionalChinese =>
                [PclEnglishFont, "Microsoft JhengHei UI", "Microsoft YaHei UI", "Segoe UI"],

            LocalizationFontProfile.Japanese =>
                [PclEnglishFont, "Yu Gothic UI", "Microsoft YaHei UI", "Segoe UI"],

            LocalizationFontProfile.Korean =>
                [PclEnglishFont, "Malgun Gothic", "Microsoft YaHei UI", "Segoe UI"],

            _ =>
                ["Segoe UI", "Microsoft YaHei UI"]
        };
    }

    private static IReadOnlyList<string> _GetCustomFamilyNames(string customFontName, LocalizationFontProfile profile)
    {
        var familyNames = _GetDefaultFamilyNames(profile)
            .Where(name => !string.Equals(name, PclEnglishFont, StringComparison.OrdinalIgnoreCase))
            .ToList();
        familyNames.Insert(0, _EscapeFontFamilyName(customFontName));
        return familyNames.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string _EscapeFontFamilyName(string name)
    {
        return name.Replace(",", ",,");
    }
}