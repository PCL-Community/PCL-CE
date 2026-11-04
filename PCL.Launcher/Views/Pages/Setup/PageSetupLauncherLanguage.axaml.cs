using System.Globalization;
using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Setup;

/// <summary>
///     语言设置页：界面语言、区域格式与多语言横幅。
///     语言写入 <c>Config.Preference.Localization.Language</c> 后由 <c>LocalizationService</c> 重新加载语言资源，
///     XAML 中的 <c>{loc:Loc}</c> 绑定与代码侧 <c>Lang.Text</c> 文本随即刷新。
/// </summary>
public partial class PageSetupLauncherLanguage : MyPageRight, ISetupResetPage
{
    private bool _loaded;
    private bool _reloading;

    public PageSetupLauncherLanguage()
    {
        InitializeComponent();
        PanScroll = PanBack;

        ComboUiLanguage.SelectionChanged += (_, _) => OnLanguageChanged();
        ComboUiFormatCulture.SelectionChanged += (_, _) => OnFormatCultureChanged();
        BtnTranslateWebsite.Click += (_, _) =>
            Basics.OpenPath("https://translate.pclc.cc/projects/pcl-ce/");
        BtnSubmitPullRequest.Click += (_, _) =>
            Basics.OpenPath("https://github.com/PCL-Community/PCL-CE/pulls");

        LocalizationService.LanguageChanged += OnLanguageChangedExternally;

        Loaded += (_, _) =>
        {
            if (_loaded) return;
            _loaded = true;
            Reload();
        };
    }

    public void Reset()
    {
        try
        {
            Config.Preference.Localization.LanguageConfig.SetDefaultValue();
            Config.Preference.Localization.FormatCultureConfig.SetDefaultValue();
            LocalizationService.ApplyFromConfig();
            HintService.Show(Hints.Success(Lang.Text("Setup.Language.Reset.Success"), false));
            Reload();
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "重置语言设置失败",
                LauncherLogLevel.Msgbox,
                userSummary: Lang.Text("Setup.Error.OperationFailed"));
        }
    }

    public override void OnNavigatedTo(object? parameter)
    {
        Reload();
    }

    public override void OnNavigatedFrom()
    {
    }

    public void Reload()
    {
        _reloading = true;
        try
        {
            ReloadLanguageCombo();
            ReloadFormatCultureCombo();
        }
        finally
        {
            _reloading = false;
        }
    }

    protected override void RefreshLocalization()
    {
        if (!_loaded) return;
        Reload();
    }

    private void ReloadLanguageCombo()
    {
        ComboUiLanguage.Items.Clear();
        var autoLanguage = LocalizationService.ResolveLanguage(LocalizationService.Auto);
        ComboUiLanguage.Items.Add(
            new MyComboBoxItem
            {
                Content = Lang.Text("Setup.Language.UiLanguage.Auto", autoLanguage.NativeName),
                Tag = LocalizationService.Auto,
                FontFamily = LaunchFonts.BuildRepresentativeFontFamily(autoLanguage)
            });
        foreach (var language in LocalizationService.SupportedLanguages)
            ComboUiLanguage.Items.Add(
                new MyComboBoxItem
                {
                    Content = language.NativeName,
                    Tag = language.Code,
                    FontFamily = LaunchFonts.BuildRepresentativeFontFamily(language)
                });

        var configValue = NormalizeConfigValue(Config.Preference.Localization.Language);
        ComboUiLanguage.SelectedIndex = ResolveLanguageIndex(configValue);
    }

    private void ReloadFormatCultureCombo()
    {
        ComboUiFormatCulture.Items.Clear();
        ComboUiFormatCulture.Items.Add(
            new MyComboBoxItem
            {
                Content = Lang.Text("Setup.Language.FormatCulture.Auto"),
                Tag = LocalizationService.Auto
            });
        ComboUiFormatCulture.Items.Add(
            new MyComboBoxItem
            {
                Content = Lang.Text("Setup.Language.FormatCulture.FollowLanguage"),
                Tag = LocalizationService.FormatCultureFollowLanguage
            });

        var index = 2;
        foreach (var culture in GetBuiltInFormatCultures())
        {
            ComboUiFormatCulture.Items.Add(
                new MyComboBoxItem
                {
                    Content = culture.NativeName,
                    Tag = culture.Name
                });
            index++;
        }

        var configValue = NormalizeConfigValue(Config.Preference.Localization.FormatCulture);
        var target = -1;
        for (var i = 0; i < ComboUiFormatCulture.ItemCount; i++)
            if (ComboUiFormatCulture.Items[i] is MyComboBoxItem item
                && string.Equals(
                    item.Tag?.ToString(),
                    configValue,
                    StringComparison.OrdinalIgnoreCase))
            {
                target = i;
                break;
            }

        if (target < 0 && TryGetCulture(configValue, out var customCulture))
        {
            ComboUiFormatCulture.Items.Add(
                new MyComboBoxItem
                {
                    Content = customCulture.NativeName,
                    Tag = customCulture.Name
                });
            target = ComboUiFormatCulture.ItemCount - 1;
        }

        ComboUiFormatCulture.SelectedIndex = target < 0 ? 0 : target;
    }

    private void OnLanguageChanged()
    {
        if (_reloading || ComboUiLanguage.SelectedItem is not MyComboBoxItem item)
            return;
        var value = item.Tag?.ToString() ?? LocalizationService.Auto;
        if (string.Equals(
                Config.Preference.Localization.Language,
                value,
                StringComparison.OrdinalIgnoreCase))
            return;
        Config.Preference.Localization.Language = value;
        HintService.Show(Hints.Success(Lang.Text("Setup.Language.Changed"), false));
    }

    private void OnFormatCultureChanged()
    {
        if (_reloading || ComboUiFormatCulture.SelectedItem is not MyComboBoxItem item)
            return;
        var value = item.Tag?.ToString() ?? LocalizationService.Auto;
        if (string.Equals(
                Config.Preference.Localization.FormatCulture,
                value,
                StringComparison.OrdinalIgnoreCase))
            return;
        Config.Preference.Localization.FormatCulture = value;
        HintService.Show(Hints.Success(Lang.Text("Setup.Language.Changed"), false));
    }

    private void OnLanguageChangedExternally()
    {
        if (_reloading) return;
        Dispatcher.UIThread.Post(Reload);
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

    private static int ResolveLanguageIndex(string configValue)
    {
        if (!LocalizationService.IsLanguageSupported(configValue)) return 0;
        if (string.Equals(
                configValue,
                LocalizationService.Auto,
                StringComparison.OrdinalIgnoreCase)) return 0;
        var code = LocalizationService.ResolveLanguage(configValue).Code;
        var index = 1;
        foreach (var language in LocalizationService.SupportedLanguages)
        {
            if (string.Equals(language.Code, code, StringComparison.OrdinalIgnoreCase))
                return index;
            index++;
        }

        return 0;
    }
}