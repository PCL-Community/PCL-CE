using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Setup;

/// <summary>
///     语言设置页：界面语言、区域格式与多语言横幅。
///     选择状态由 <see cref="PageSetupLauncherLanguageViewModel" /> 负责，页面构建带本地化名称的下拉项。
/// </summary>
public partial class PageSetupLauncherLanguage : MyPageRight, ISetupResetPage
{
    private bool _loaded;
    private bool _reloading;

    public PageSetupLauncherLanguage()
    {
        InitializeComponent();
        PanScroll = PanBack;
        DataContext = ViewModel;
        LabBannerTitle.FontFamily = LaunchFonts.BannerFont;

        ComboUiLanguage.SelectionChanged += (_, _) => OnLanguageChanged();
        ComboUiFormatCulture.SelectionChanged += (_, _) => OnFormatCultureChanged();
        BtnTranslateWebsite.Click += (_, _) =>
            Basics.OpenPath("https://translate.pclc.cc/projects/pcl-ce/");
        BtnSubmitPullRequest.Click += (_, _) =>
            Basics.OpenPath("https://github.com/PCL-Community/PCL-CE/pulls");

        Loaded += (_, _) =>
        {
            if (_loaded) return;
            _loaded = true;
            Reload();
        };
    }

    public PageSetupLauncherLanguageViewModel ViewModel { get; } = new();

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
        PanBack.ScrollToHome();
        if (_loaded) Reload();
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
            ViewModel.Reload();
        }
        finally
        {
            _reloading = false;
        }
    }

    protected override void RefreshLocalization()
    {
        if (_loaded) Reload();
    }

    private void ReloadLanguageCombo()
    {
        ComboUiLanguage.Items.Clear();
        var autoLanguage = LocalizationService.ResolveLanguage(LocalizationService.Auto);
        ComboUiLanguage.Items.Add(
            new MyComboBoxItem
            {
                Content = Lang.Text(
                    "Setup.Language.UiLanguage.Auto",
                    autoLanguage.NativeName),
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

        var configValue = PageSetupLauncherLanguageViewModel.NormalizeConfigValue(
            Config.Preference.Localization.Language);
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

        foreach (var culture in PageSetupLauncherLanguageViewModel.GetBuiltInFormatCultures())
            ComboUiFormatCulture.Items.Add(
                new MyComboBoxItem
                {
                    Content = culture.NativeName,
                    Tag = culture.Name
                });

        var configValue = PageSetupLauncherLanguageViewModel.NormalizeConfigValue(
            Config.Preference.Localization.FormatCulture);
        if (!IsFormatCultureItemExisting(configValue)
            && PageSetupLauncherLanguageViewModel.TryGetCulture(configValue, out var customCulture))
            ComboUiFormatCulture.Items.Add(
                new MyComboBoxItem
                {
                    Content = customCulture.NativeName,
                    Tag = customCulture.Name
                });

        ComboUiFormatCulture.SelectedIndex = ResolveFormatCultureIndex(configValue);
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
        Dispatcher.UIThread.Post(Reload);
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
        Dispatcher.UIThread.Post(Reload);
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

    private static int ResolveFormatCultureIndex(string configValue)
    {
        if (string.Equals(
                configValue,
                LocalizationService.FormatCultureFollowLanguage,
                StringComparison.OrdinalIgnoreCase))
            return 1;
        var index = 2;
        foreach (var culture in PageSetupLauncherLanguageViewModel.GetBuiltInFormatCultures())
        {
            if (string.Equals(culture.Name, configValue, StringComparison.OrdinalIgnoreCase))
                return index;
            index++;
        }

        return PageSetupLauncherLanguageViewModel.TryGetCulture(configValue, out _) ? index : 0;
    }

    private bool IsFormatCultureItemExisting(string tag)
    {
        return ComboUiFormatCulture
            .Items.OfType<MyComboBoxItem>()
            .Any(item => string.Equals(
                item.Tag?.ToString(),
                tag,
                StringComparison.OrdinalIgnoreCase));
    }
}