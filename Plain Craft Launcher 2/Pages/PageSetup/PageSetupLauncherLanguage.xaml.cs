using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;

namespace PCL;

public partial class PageSetupLauncherLanguage : IPageView
{
    private readonly PageSetupLauncherLanguageViewModel _vm = new();

    private bool _isLoaded;

    public PageSetupLauncherLanguage()
    {
        InitializeComponent();
        DataContext = _vm;
        Loaded += PageSetupLauncherLanguage_Loaded;
    }

    public void OnNavigatedTo(object? parameter)
    {
    }

    public void OnNavigatedFrom()
    {
    }

    public PageSetupLauncherLanguageViewModel ViewModel => _vm;

    private void PageSetupLauncherLanguage_Loaded(object sender, RoutedEventArgs e)
    {
        // 重复加载部分
        PanBack.ScrollToHome();

        // 非重复加载部分
        if (_isLoaded)
            return;
        _isLoaded = true;

        Animations.AniBeginControl();
        Reload();
        Animations.AniEndControl();
    }

    public void Reload()
    {
        Animations.AniBeginControl();
        try
        {
            ReloadLanguageCombo();
            ReloadFormatCultureCombo();
            _vm.Reload();
        }
        finally
        {
            Animations.AniEndControl();
        }
    }

    public void Reset()
    {
        try
        {
            Config.Preference.Localization.LanguageConfig.SetDefaultValue();
            Config.Preference.Localization.FormatCultureConfig.SetDefaultValue();
            LocalizationService.ApplyFromConfig();
            Common.Log("[Setup] 已初始化启动器-语言页设置");
            HintService.Show(Hints.Success(Lang.Text("Setup.Language.Reset.Success"), log: false));
            Reload();
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "初始化启动器-语言页设置失败",
                Common.LogLevel.Msgbox,
                userSummary: Lang.Text("Setup.Error.OperationFailed"));
        }
    }

    private void ReloadLanguageCombo()
    {
        ComboUiLanguage.Items.Clear();
        var autoLanguage = LocalizationService.ResolveLanguage(LocalizationService.Auto);
        var autoLanguageItem = CreateLanguageComboItem(
            string.Empty,
            LocalizationService.Auto,
            autoLanguage);
        LocalizedText.TryBind(
            autoLanguageItem,
            "Content",
            () => Lang.Text("Setup.Language.UiLanguage.Auto", GetLanguageDisplay(autoLanguage)));
        ComboUiLanguage.Items.Add(autoLanguageItem);

        foreach (var language in LocalizationService.SupportedLanguages)
            ComboUiLanguage.Items.Add(CreateLanguageComboItem(
                GetLanguageDisplay(language),
                language.Code,
                language));
    }

    private void ReloadFormatCultureCombo()
    {
        ComboUiFormatCulture.Items.Clear();
        var autoFormatItem = new MyComboBoxItem
        {
            Tag = LocalizationService.Auto
        };
        autoFormatItem.TrySetLocalizedValue(
            "Content",
            "Setup.Language.FormatCulture.Auto");
        ComboUiFormatCulture.Items.Add(autoFormatItem);
        var followLanguageFormatItem = new MyComboBoxItem
        {
            Tag = LocalizationService.FormatCultureFollowLanguage
        };
        followLanguageFormatItem.TrySetLocalizedValue(
            "Content",
            "Setup.Language.FormatCulture.FollowLanguage");
        ComboUiFormatCulture.Items.Add(followLanguageFormatItem);

        foreach (var culture in PageSetupLauncherLanguageViewModel.GetBuiltInFormatCultures())
            ComboUiFormatCulture.Items.Add(new MyComboBoxItem
            {
                Content = GetCultureDisplay(culture),
                Tag = culture.Name
            });

        var configValue = PageSetupLauncherLanguageViewModel.NormalizeConfigValue(
            Config.Preference.Localization.FormatCulture);
        if (!IsFormatCultureItemExisting(configValue) &&
            PageSetupLauncherLanguageViewModel.TryGetCulture(configValue, out var customCulture))
            ComboUiFormatCulture.Items.Add(new MyComboBoxItem
            {
                Content = GetCultureDisplay(customCulture),
                Tag = customCulture.Name
            });
    }

    private void ComboUiLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Animations.AniExitsAnyControl())
            return;
        if (ComboUiLanguage.SelectedItem is not MyComboBoxItem item)
            return;

        var value = item.Tag?.ToString() ?? LocalizationService.Auto;
        if (Config.Preference.Localization.Language == value)
            return;

        Config.Preference.Localization.Language = value;
        HintService.Show(Hints.Success(Lang.Text("Setup.Language.Changed"), log: false));
        Reload();
    }

    private void ComboUiFormatCulture_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Animations.AniExitsAnyControl())
            return;
        if (ComboUiFormatCulture.SelectedItem is not MyComboBoxItem item)
            return;

        var value = item.Tag?.ToString() ?? LocalizationService.Auto;
        if (Config.Preference.Localization.FormatCulture == value)
            return;

        Config.Preference.Localization.FormatCulture = value;
        HintService.Show(Hints.Success(Lang.Text("Setup.Language.Changed"), log: false));
        Reload();
    }

    private static MyComboBoxItem CreateLanguageComboItem(string content, string tag, LocalizationLanguage language)
    {
        return new MyComboBoxItem
        {
            Content = content,
            Tag = tag,
            FontFamily = LocalizationFontService.BuildRepresentativeFontFamily(language)
        };
    }

    private static string GetLanguageDisplay(LocalizationLanguage language)
    {
        return $"{language.NativeName}";
    }

    private static string GetCultureDisplay(CultureInfo culture)
    {
        return $"{culture.NativeName}";
    }

    private bool IsFormatCultureItemExisting(string tag)
    {
        return ComboUiFormatCulture.Items.OfType<MyComboBoxItem>()
            .Any(item => string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase));
    }
}