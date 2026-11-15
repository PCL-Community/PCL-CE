using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>设置页面中「启动器」语言页的视图模型。</summary>
public partial class PageSetupLauncherLanguageViewModel : ViewModelBase
{
    [ObservableProperty] private int _formatCultureIndex = -1;

    [ObservableProperty] private int _uiLanguageIndex = -1;

    public static IEnumerable<CultureInfo> GetBuiltInFormatCultures()
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var language in LocalizationService.SupportedLanguages)
        {
            var culture = CultureInfo.GetCultureInfo(language.CultureName);
            if (used.Add(culture.Name)) yield return culture;
        }
    }

    public static string NormalizeConfigValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? LocalizationService.Auto : value;
    }

    public static bool TryGetCulture(string value, out CultureInfo culture)
    {
        try
        {
            culture = CultureInfo.GetCultureInfo(value);
            return true;
        }
        catch (CultureNotFoundException)
        {
            culture = CultureInfo.InvariantCulture;
            return false;
        }
    }

    public void Reload()
    {
        UiLanguageIndex = -1;
        FormatCultureIndex = -1;
        UiLanguageIndex = ResolveLanguageIndex();
        FormatCultureIndex = ResolveFormatCultureIndex();
    }

    private static int ResolveFormatCultureIndex()
    {
        var configValue = NormalizeConfigValue(Config.Preference.Localization.FormatCulture);
        if (string.Equals(configValue, LocalizationService.FormatCultureFollowLanguage,
                StringComparison.OrdinalIgnoreCase))
            return 1;
        var index = 2;
        foreach (var culture in GetBuiltInFormatCultures())
        {
            if (string.Equals(culture.Name, configValue, StringComparison.OrdinalIgnoreCase)) return index;
            index++;
        }

        return TryGetCulture(configValue, out _) ? index : 0;
    }

    private static int ResolveLanguageIndex()
    {
        var configValue = NormalizeConfigValue(Config.Preference.Localization.Language);
        if (!LocalizationService.IsLanguageSupported(configValue)) return 0;
        if (string.Equals(configValue, LocalizationService.Auto, StringComparison.OrdinalIgnoreCase)) return 0;
        var code = LocalizationService.ResolveLanguage(configValue).Code;
        var index = 1;
        foreach (var language in LocalizationService.SupportedLanguages)
        {
            if (string.Equals(language.Code, code, StringComparison.OrdinalIgnoreCase)) return index;
            index++;
        }

        return 0;
    }
}
