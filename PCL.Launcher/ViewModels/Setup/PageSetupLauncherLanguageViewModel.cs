using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.Core.ViewModel;

namespace PCL;

public partial class PageSetupLauncherLanguageViewModel : ViewModelBase
{
    [ObservableProperty] private int _formatCultureIndex = -1;

    [ObservableProperty] private int _uiLanguageIndex = -1;

    public bool IsSynchronizing { get; set; }

    public string AutoLanguageText =>
        Lang.Text(
            "Setup.Language.UiLanguage.Auto",
            LocalizationService.ResolveLanguage(LocalizationService.Auto).NativeName);

    public event Action? ReloadRequested;

    partial void OnUiLanguageIndexChanged(int value)
    {
        if (IsSynchronizing || value < 0) return;

        var code = ResolveLanguageCode(value);
        if (string.Equals(
                Config.Preference.Localization.Language,
                code,
                StringComparison.OrdinalIgnoreCase))
            return;

        Config.Preference.Localization.Language = code;
        HintService.Show(Hints.Success(Lang.Text("Setup.Language.Changed"), false));
        ReloadRequested?.Invoke();
    }

    partial void OnFormatCultureIndexChanged(int value)
    {
        if (IsSynchronizing || value < 0) return;

        var code = ResolveFormatCultureValue(value);
        if (string.Equals(
                Config.Preference.Localization.FormatCulture,
                code,
                StringComparison.OrdinalIgnoreCase))
            return;

        Config.Preference.Localization.FormatCulture = code;
        HintService.Show(Hints.Success(Lang.Text("Setup.Language.Changed"), false));
        ReloadRequested?.Invoke();
    }

    protected override void OnLanguageChanged()
    {
        Reload();
        base.OnLanguageChanged();
    }

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

    public static int ResolveLanguageIndex(string? configValue)
    {
        var value = NormalizeConfigValue(configValue);
        if (!LocalizationService.IsLanguageSupported(value)) return 0;
        if (string.Equals(
                value,
                LocalizationService.Auto,
                StringComparison.OrdinalIgnoreCase)) return 0;
        var code = LocalizationService.ResolveLanguage(value).Code;
        var index = 1;
        foreach (var language in LocalizationService.SupportedLanguages)
        {
            if (string.Equals(language.Code, code, StringComparison.OrdinalIgnoreCase))
                return index;
            index++;
        }

        return 0;
    }

    public static int ResolveFormatCultureIndex(string? configValue)
    {
        var value = NormalizeConfigValue(configValue);
        if (string.Equals(
                value,
                LocalizationService.FormatCultureFollowLanguage,
                StringComparison.OrdinalIgnoreCase))
            return 1;
        var index = 2;
        foreach (var culture in GetBuiltInFormatCultures())
        {
            if (string.Equals(culture.Name, value, StringComparison.OrdinalIgnoreCase))
                return index;
            index++;
        }

        return TryGetCulture(value, out _) ? index : 0;
    }

    public void Reload()
    {
        var wasSynchronizing = IsSynchronizing;
        IsSynchronizing = true;
        try
        {
            UiLanguageIndex = ResolveLanguageIndex(
                Config.Preference.Localization.Language);
            FormatCultureIndex = ResolveFormatCultureIndex(
                Config.Preference.Localization.FormatCulture);
        }
        finally
        {
            IsSynchronizing = wasSynchronizing;
        }
    }

    private static string ResolveLanguageCode(int index)
    {
        if (index <= 0 || index > LocalizationService.SupportedLanguages.Count)
            return LocalizationService.Auto;
        return LocalizationService.SupportedLanguages[index - 1].Code;
    }

    private static string ResolveFormatCultureValue(int index)
    {
        switch (index)
        {
            case <= 0:
                return LocalizationService.Auto;
            case 1:
                return LocalizationService.FormatCultureFollowLanguage;
        }

        var builtInIndex = index - 2;
        foreach (var culture in GetBuiltInFormatCultures())
            if (builtInIndex-- == 0)
                return culture.Name;

        var current = NormalizeConfigValue(Config.Preference.Localization.FormatCulture);
        return TryGetCulture(current, out _) ? current : LocalizationService.Auto;
    }
}