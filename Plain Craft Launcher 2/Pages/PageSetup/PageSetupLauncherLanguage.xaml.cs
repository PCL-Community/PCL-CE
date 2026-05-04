using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using PCL.Core.App;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageSetupLauncherLanguage
{
    private new bool IsLoaded;

    public PageSetupLauncherLanguage()
    {
        InitializeComponent();
        Loaded += PageSetupLauncherLanguage_Loaded;
        Loaded += (_, _) => Reload();
    }

    private void PageSetupLauncherLanguage_Loaded(object sender, RoutedEventArgs e)
    {
        // 重复加载部分
        PanBack.ScrollToHome();

        // 非重复加载部分
        if (IsLoaded)
            return;
        IsLoaded = true;

        ModAnimation.AniControlEnabled += 1;
        Reload();
        ModAnimation.AniControlEnabled -= 1;
    }

    public void Reload()
    {
        ModAnimation.AniControlEnabled += 1;
        try
        {
            ReloadLanguageCombo();
            ReloadFormatCultureCombo();
        }
        finally
        {
            ModAnimation.AniControlEnabled -= 1;
        }
    }

    public void Reset()
    {
        try
        {
            Config.Preference.Localization.LanguageConfig.SetDefaultValue();
            Config.Preference.Localization.FormatCultureConfig.SetDefaultValue();
            LocalizationService.ApplyFromConfig();
            ModBase.Log("[Setup] 已初始化启动器-语言页设置");
            ModMain.Hint("已初始化语言页设置！", ModMain.HintType.Finish, false);
            Reload();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "初始化启动器-语言页设置失败", ModBase.LogLevel.Msgbox);
        }
    }

    private void ReloadLanguageCombo()
    {
        ComboUiLanguage.Items.Clear();
        ComboUiLanguage.Items.Add(new MyComboBoxItem
        {
            Content = Lang.Text("Localization.Language.Auto",
                GetLanguageDisplay(LocalizationService.ResolveLanguage(LocalizationService.Auto))),
            Tag = LocalizationService.Auto
        });

        foreach (var language in LocalizationService.SupportedLanguages)
            ComboUiLanguage.Items.Add(new MyComboBoxItem
            {
                Content = GetLanguageDisplay(language),
                Tag = language.Code
            });

        var configValue = Config.Preference.Localization.Language;
        SelectComboItem(ComboUiLanguage,
            LocalizationService.IsLanguageSupported(configValue)
                ? NormalizeConfigValue(configValue)
                : LocalizationService.Auto);
    }

    private void ReloadFormatCultureCombo()
    {
        ComboUiFormatCulture.Items.Clear();
        ComboUiFormatCulture.Items.Add(new MyComboBoxItem
        {
            Content = Lang.Text("Localization.FormatCulture.Auto"),
            Tag = LocalizationService.Auto
        });
        ComboUiFormatCulture.Items.Add(new MyComboBoxItem
        {
            Content = Lang.Text("Localization.FormatCulture.FollowLanguage"),
            Tag = LocalizationService.FormatCultureFollowLanguage
        });

        foreach (var culture in GetBuiltInFormatCultures())
            ComboUiFormatCulture.Items.Add(new MyComboBoxItem
            {
                Content = GetCultureDisplay(culture),
                Tag = culture.Name
            });

        var configValue = NormalizeConfigValue(Config.Preference.Localization.FormatCulture);
        if (!IsFormatCultureItemExisting(configValue) && TryGetCulture(configValue, out var customCulture))
            ComboUiFormatCulture.Items.Add(new MyComboBoxItem
            {
                Content = GetCultureDisplay(customCulture),
                Tag = customCulture.Name
            });

        SelectComboItem(ComboUiFormatCulture,
            IsFormatCultureItemExisting(configValue) ? configValue : LocalizationService.Auto);
    }

    private void ComboUiLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModAnimation.AniControlEnabled != 0)
            return;
        if (ComboUiLanguage.SelectedItem is not MyComboBoxItem item)
            return;

        var value = item.Tag?.ToString() ?? LocalizationService.Auto;
        if (Config.Preference.Localization.Language == value)
            return;

        Config.Preference.Localization.Language = value;
        ModMain.Hint(Lang.Text("Setup.LauncherLanguage.Changed"), ModMain.HintType.Finish, false);
        Reload();
    }

    private void ComboUiFormatCulture_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModAnimation.AniControlEnabled != 0)
            return;
        if (ComboUiFormatCulture.SelectedItem is not MyComboBoxItem item)
            return;

        var value = item.Tag?.ToString() ?? LocalizationService.Auto;
        if (Config.Preference.Localization.FormatCulture == value)
            return;

        Config.Preference.Localization.FormatCulture = value;
        ModMain.Hint(Lang.Text("Setup.LauncherLanguage.Changed"), ModMain.HintType.Finish, false);
        Reload();
    }

    private static IEnumerable<CultureInfo> GetBuiltInFormatCultures()
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var language in LocalizationService.SupportedLanguages)
        {
            var culture = CultureInfo.GetCultureInfo(language.CultureName);
            if (used.Add(culture.Name)) yield return culture;
        }
    }

    private static string GetLanguageDisplay(LocalizationLanguage language)
    {
        return $"{language.NativeName}";
    }

    private static string GetCultureDisplay(CultureInfo culture)
    {
        return $"{culture.NativeName}";
    }

    private static string NormalizeConfigValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? LocalizationService.Auto : value;
    }

    private static bool TryGetCulture(string value, out CultureInfo culture)
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

    private static void SelectComboItem(MyComboBox comboBox, string tag)
    {
        foreach (var item in comboBox.Items.OfType<MyComboBoxItem>())
        {
            if (!string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase)) continue;
            comboBox.SelectedItem = item;
            return;
        }

        comboBox.SelectedIndex = comboBox.Items.Count > 0 ? 0 : -1;
    }

    private bool IsFormatCultureItemExisting(string tag)
    {
        return ComboUiFormatCulture.Items.OfType<MyComboBoxItem>()
            .Any(item => string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase));
    }
}