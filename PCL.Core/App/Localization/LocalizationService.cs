using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using PCL.Core.App.Configuration;
using PCL.Core.App.IoC;

namespace PCL.Core.App.Localization;

/// <summary>
///     UI 本地化服务。
/// </summary>
[LifecycleScope("localization", "本地化", false)]
[LifecycleService(LifecycleState.Loaded, Priority = 114514)]
public sealed partial class LocalizationService
{
    /// <summary>
    ///     跟随系统设置的配置值。
    /// </summary>
    public const string Auto = "auto";

    /// <summary>
    ///     展示格式同步 UI 语言的配置值。
    /// </summary>
    public const string FormatCultureFollowLanguage = "ui-language";

    /// <summary>
    ///     默认语言，也是语言资源的完整兜底。
    /// </summary>
    public const string DefaultLanguageCode = "zh-CN";

    private const string AssemblyResourcePrefix = "/PCL.Core;component/App/Localization/Languages/";

    private static readonly LocalizationLanguage _DefaultLanguage = new(
        DefaultLanguageCode,
        "简体中文（中国大陆）",
        "zh-CN",
        LocalizationFontProfile.SimplifiedChinese);

    private static ResourceDictionary? _baseLanguageDictionary;
    private static ResourceDictionary? _currentLanguageDictionary;
    private static CultureInfo _systemFormatCulture = CultureInfo.CurrentCulture;
    private static CultureInfo _systemUiCulture = CultureInfo.CurrentUICulture;

    /// <summary>
    ///     当前 UI 语言。
    /// </summary>
    public static LocalizationLanguage CurrentLanguage { get; private set; } = _DefaultLanguage;

    /// <summary>
    ///     当前 UI 展示格式所使用的区域性。
    /// </summary>
    public static CultureInfo CurrentFormatCulture { get; private set; } = CultureInfo.CurrentCulture;

    /// <summary>
    ///     受支持的 UI 语言。
    /// </summary>
    public static IReadOnlyList<LocalizationLanguage> SupportedLanguages { get; } =
    [
        _DefaultLanguage,
        new("zh-TW", "繁體中文（台灣）", "zh-TW", LocalizationFontProfile.TraditionalChinese),
        new("en-US", "English (US)", "en-US", LocalizationFontProfile.English),
        new("en-GB", "English (United Kingdom)", "en-GB", LocalizationFontProfile.English),
        new("ja-JP", "日本語（日本）", "ja-JP", LocalizationFontProfile.Japanese),
        new("fr-FR", "Français (France)", "fr-FR", LocalizationFontProfile.Other),
        new("es-ES", "Español (España)", "es-ES", LocalizationFontProfile.Other)
    ];

    [RegisterConfigEvent]
    public static ConfigEventRegistry OnLanguageConfigChanged => new(
        [
            Config.Preference.Localization.LanguageConfig,
            Config.Preference.Localization.FormatCultureConfig
        ],
        trigger: ConfigEvent.Update,
        handler: _ => ApplyFromConfig()
    );

    /// <summary>
    ///     语言或展示格式更改后触发。
    /// </summary>
    public static event Action? LanguageChanged;

    [LifecycleStart]
    private static void _Start()
    {
        _systemFormatCulture = CultureInfo.CurrentCulture;
        _systemUiCulture = CultureInfo.CurrentUICulture;
        ApplyFromConfig();
    }

    /// <summary>
    ///     按当前配置应用 UI 语言与展示格式。
    /// </summary>
    public static void ApplyFromConfig(bool save = false)
    {
        if (!ConfigService.IsInitialized)
        {
            Apply(Auto, Auto, false);
            return;
        }

        Apply(
            Config.Preference.Localization.Language,
            Config.Preference.Localization.FormatCulture,
            save);
    }

    /// <summary>
    ///     应用 UI 语言与展示格式。
    /// </summary>
    /// <param name="languageCode">UI 语言代码，auto 表示跟随系统语言。</param>
    /// <param name="formatCultureCode">展示格式区域性，auto 表示跟随系统区域格式。</param>
    /// <param name="save">是否写回配置。</param>
    public static void Apply(string languageCode, string formatCultureCode = Auto, bool save = true)
    {
        var normalizedLanguageCode = _NormalizeConfigValue(languageCode);
        var language = ResolveLanguage(normalizedLanguageCode);
        var uiCulture = CultureInfo.GetCultureInfo(language.CultureName);
        var formatCulture = _ResolveFormatCulture(formatCultureCode, uiCulture, out var normalizedFormatCultureCode);

        var isLanguageChanged = !string.Equals(CurrentLanguage.Code, language.Code, StringComparison.OrdinalIgnoreCase);
        var isFormatCultureChanged = !string.Equals(CurrentFormatCulture.Name, formatCulture.Name,
            StringComparison.OrdinalIgnoreCase);
        if (_baseLanguageDictionary is not null && !isLanguageChanged && !isFormatCultureChanged)
        {
            _SaveConfigIfNeeded(save, normalizedLanguageCode, language, normalizedFormatCultureCode);
            return;
        }

        _ApplyCultures(uiCulture, formatCulture);
        _ApplyLanguageResources(language.Code, uiCulture, formatCulture);

        CurrentLanguage = language;
        LocalizationFontService.ApplyLaunchFont(
            ConfigService.IsInitialized ? Config.Preference.Font : null,
            language);
        CurrentFormatCulture = formatCulture;

        _SaveConfigIfNeeded(save, normalizedLanguageCode, language, normalizedFormatCultureCode);

        _LogInfo($"当前 UI 语言: {language.Code}, 展示格式: {formatCulture.Name}");
        LanguageChanged?.Invoke();
    }

    /// <summary>
    ///     判断语言代码是否受支持。
    /// </summary>
    public static bool IsLanguageSupported(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode)) return true;
        if (string.Equals(languageCode, Auto, StringComparison.OrdinalIgnoreCase)) return true;
        var normalizedCode = _NormalizeCultureCode(languageCode);
        return SupportedLanguages.Any(language =>
            string.Equals(language.Code, normalizedCode, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     按配置值解析语言。
    /// </summary>
    public static LocalizationLanguage ResolveLanguage(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode) ||
            string.Equals(languageCode, Auto, StringComparison.OrdinalIgnoreCase)) return _ResolveSystemLanguage();

        var normalizedCode = _NormalizeCultureCode(languageCode);
        return SupportedLanguages.FirstOrDefault(language =>
                   string.Equals(language.Code, normalizedCode, StringComparison.OrdinalIgnoreCase))
               ?? _DefaultLanguage;
    }

    private static void _SaveConfigIfNeeded(bool save, string normalizedLanguageCode, LocalizationLanguage language,
        string normalizedFormatCultureCode)
    {
        if (!save || !ConfigService.IsInitialized) return;

        var configLanguageCode = string.Equals(normalizedLanguageCode, Auto, StringComparison.OrdinalIgnoreCase)
            ? Auto
            : language.Code;
        if (Config.Preference.Localization.Language != configLanguageCode)
            Config.Preference.Localization.Language = configLanguageCode;
        if (Config.Preference.Localization.FormatCulture != normalizedFormatCultureCode)
            Config.Preference.Localization.FormatCulture = normalizedFormatCultureCode;
    }

    private static LocalizationLanguage _ResolveSystemLanguage()
    {
        var systemLanguage = _NormalizeCultureCode(_systemUiCulture.Name);
        var exact = SupportedLanguages.FirstOrDefault(language =>
            string.Equals(language.Code, systemLanguage, StringComparison.OrdinalIgnoreCase));
        if (exact is not null) return exact;

        var neutral = _systemUiCulture.TwoLetterISOLanguageName;
        return SupportedLanguages.FirstOrDefault(language =>
                   language.Code.StartsWith(neutral + "-", StringComparison.OrdinalIgnoreCase))
               ?? _DefaultLanguage;
    }

    private static CultureInfo _ResolveFormatCulture(string? formatCultureCode, CultureInfo uiCulture,
        out string normalizedCode)
    {
        if (string.IsNullOrWhiteSpace(formatCultureCode) ||
            string.Equals(formatCultureCode, Auto, StringComparison.OrdinalIgnoreCase))
        {
            normalizedCode = Auto;
            return _systemFormatCulture;
        }

        if (string.Equals(formatCultureCode, FormatCultureFollowLanguage, StringComparison.OrdinalIgnoreCase))
        {
            normalizedCode = FormatCultureFollowLanguage;
            return uiCulture;
        }

        try
        {
            var culture = CultureInfo.GetCultureInfo(formatCultureCode);
            normalizedCode = culture.Name;
            return culture;
        }
        catch (CultureNotFoundException)
        {
            _LogWarn($"无法识别展示格式区域性: {formatCultureCode}，已回退系统区域格式");
            normalizedCode = Auto;
            return _systemFormatCulture;
        }
    }

    private static void _ApplyCultures(CultureInfo uiCulture, CultureInfo formatCulture)
    {
        CultureInfo.CurrentUICulture = uiCulture;
        CultureInfo.DefaultThreadCurrentUICulture = uiCulture;
        Thread.CurrentThread.CurrentUICulture = uiCulture;

        CultureInfo.CurrentCulture = formatCulture;
        CultureInfo.DefaultThreadCurrentCulture = formatCulture;
        Thread.CurrentThread.CurrentCulture = formatCulture;
        Lang.SyncCulture(formatCulture);
    }

    private static void _ApplyLanguageResources(string languageCode, CultureInfo uiCulture, CultureInfo formatCulture)
    {
        var app = Application.Current ?? Lifecycle.CurrentApplication;

        if (!app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.Invoke(() =>
            {
                _ApplyCultures(uiCulture, formatCulture);
                _ApplyLanguageResourcesCore(app, languageCode);
            });
            return;
        }

        _ApplyLanguageResourcesCore(app, languageCode);
    }

    private static void _ApplyLanguageResourcesCore(Application app, string languageCode)
    {
        var dictionaries = app.Resources.MergedDictionaries;

        if (_baseLanguageDictionary is not null) dictionaries.Remove(_baseLanguageDictionary);
        if (_currentLanguageDictionary is not null) dictionaries.Remove(_currentLanguageDictionary);

        _baseLanguageDictionary = _LoadLanguageDictionary(DefaultLanguageCode);
        dictionaries.Add(_baseLanguageDictionary);

        if (string.Equals(languageCode, DefaultLanguageCode, StringComparison.OrdinalIgnoreCase))
        {
            _currentLanguageDictionary = null;
        }
        else
        {
            _currentLanguageDictionary = _LoadLanguageDictionary(languageCode);
            dictionaries.Add(_currentLanguageDictionary);
        }
    }

    private static ResourceDictionary _LoadLanguageDictionary(string languageCode)
    {
        return new ResourceDictionary
        {
            Source = new Uri($"{AssemblyResourcePrefix}{languageCode}.xaml", UriKind.Relative)
        };
    }

    private static string _NormalizeConfigValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? Auto : _NormalizeCultureCode(value);
    }

    private static string _NormalizeCultureCode(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? Auto : value.Replace('_', '-').Trim();
    }

    private static void _LogInfo(string message)
    {
        try
        {
            Context.Info(message);
        }
        catch (InvalidOperationException)
        {
            // 静态 API 可在生命周期服务创建前被测试调用，此时无需写入生命周期日志。
        }
    }

    private static void _LogWarn(string message)
    {
        try
        {
            Context.Warn(message);
        }
        catch (InvalidOperationException)
        {
            // 静态 API 可在生命周期服务创建前被测试调用，此时无需写入生命周期日志。
        }
    }
}