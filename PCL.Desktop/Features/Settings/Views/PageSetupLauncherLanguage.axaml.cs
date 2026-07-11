// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Application.Settings;
using PCL.Desktop.Controls.Legacy;

namespace PCL.Desktop.Features.Settings.Views;

public partial class PageSetupLauncherLanguage : MyPageRight
{
    private const string Auto = "auto";
    private const string FormatCultureFollowLanguage = "follow-language";
    private string _language = Auto;
    private string _formatCulture = Auto;
    private bool _isLoaded;
    private bool _reloadQueued;

    public PageSetupLauncherLanguage()
    {
        AvaloniaXamlLoader.Load(this);
        PanScroll = LanguageScroll;
        LauncherSettings settings = LauncherSettingsPageBinder.LoadSettings();
        _language = NormalizeConfigValue(settings.GetTextOption("UiLanguage", Auto));
        _formatCulture = NormalizeConfigValue(settings.GetTextOption("UiFormatCulture", Auto));
        AttachedToVisualTree += PageSetupLauncherLanguage_Loaded;
    }

    private void PageSetupLauncherLanguage_Loaded(object? sender, VisualTreeAttachmentEventArgs e)
    {
        LanguageScroll.Offset = Vector.Zero;
        if (_isLoaded)
            return;

        _isLoaded = true;
        ModAnimation.AniControlEnabled += 1;
        try
        {
            Reload();
        }
        finally
        {
            ModAnimation.AniControlEnabled -= 1;
        }
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
        _language = Auto;
        _formatCulture = Auto;
        LauncherSettings settings = LauncherSettingsPageBinder.LoadSettings();
        settings.TextOptions.Remove("UiLanguage");
        settings.TextOptions.Remove("UiFormatCulture");
        LauncherSettingsPageBinder.SaveSettings(settings);
        Reload();
    }

    private void ReloadLanguageCombo()
    {
        LanguageCombo.Items.Clear();
        SupportedLanguage autoLanguage = ResolveLanguage(Auto);
        LanguageCombo.Items.Add(CreateLanguageComboItem(
            string.Format(
                CultureInfo.CurrentCulture,
                FindString("Setup.LauncherLanguage.UiLanguage.Auto", "跟随系统（{0}）"),
                GetLanguageDisplay(autoLanguage)),
            Auto,
            autoLanguage));

        foreach (SupportedLanguage language in SupportedLanguages)
            LanguageCombo.Items.Add(CreateLanguageComboItem(GetLanguageDisplay(language), language.Code, language));

        string selectedLanguageTag = IsLanguageSupported(_language)
            ? string.Equals(_language, Auto, StringComparison.OrdinalIgnoreCase) ? Auto : ResolveLanguage(_language).Code
            : Auto;
        SelectComboItem(LanguageCombo, selectedLanguageTag);
    }

    private void ReloadFormatCultureCombo()
    {
        FormatCultureCombo.Items.Clear();
        FormatCultureCombo.Items.Add(new MyComboBoxItem
        {
            Content = FindString("Setup.LauncherLanguage.FormatCulture.Auto", "跟随系统区域格式"),
            Tag = Auto
        });
        FormatCultureCombo.Items.Add(new MyComboBoxItem
        {
            Content = FindString("Setup.LauncherLanguage.FormatCulture.FollowLanguage", "同步界面语言"),
            Tag = FormatCultureFollowLanguage
        });

        foreach (CultureInfo culture in GetBuiltInFormatCultures())
            FormatCultureCombo.Items.Add(new MyComboBoxItem
            {
                Content = GetCultureDisplay(culture),
                Tag = culture.Name
            });

        string configValue = NormalizeConfigValue(_formatCulture);
        if (!IsFormatCultureItemExisting(configValue) && TryGetCulture(configValue, out CultureInfo? customCulture))
            FormatCultureCombo.Items.Add(new MyComboBoxItem
            {
                Content = GetCultureDisplay(customCulture),
                Tag = customCulture.Name
            });

        SelectComboItem(FormatCultureCombo, IsFormatCultureItemExisting(configValue) ? configValue : Auto);
    }

    private void ComboUiLanguage_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ModAnimation.AniControlEnabled != 0 ||
            LanguageCombo.SelectedItem is not MyComboBoxItem item)
        {
            return;
        }

        string value = item.Tag?.ToString() ?? Auto;
        if (string.Equals(_language, value, StringComparison.OrdinalIgnoreCase))
            return;

        _language = value;
        SaveLocalizationSetting("UiLanguage", value);
        QueueReload();
    }

    private void ComboUiFormatCulture_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ModAnimation.AniControlEnabled != 0 ||
            FormatCultureCombo.SelectedItem is not MyComboBoxItem item)
        {
            return;
        }

        string value = item.Tag?.ToString() ?? Auto;
        if (string.Equals(_formatCulture, value, StringComparison.OrdinalIgnoreCase))
            return;

        _formatCulture = value;
        SaveLocalizationSetting("UiFormatCulture", value);
        QueueReload();
    }

    private static IEnumerable<CultureInfo> GetBuiltInFormatCultures()
    {
        HashSet<string> used = new(StringComparer.OrdinalIgnoreCase);
        foreach (SupportedLanguage language in SupportedLanguages)
        {
            CultureInfo culture = CultureInfo.GetCultureInfo(language.CultureName);
            if (used.Add(culture.Name))
                yield return culture;
        }
    }

    private static MyComboBoxItem CreateLanguageComboItem(string content, string tag, SupportedLanguage language) =>
        new()
        {
            Content = content,
            Tag = tag,
            FontFamily = new FontFamily(language.RepresentativeFontFamily)
        };

    private static string GetLanguageDisplay(SupportedLanguage language) => language.NativeName;

    private static string GetCultureDisplay(CultureInfo culture) => culture.NativeName;

    private static string NormalizeConfigValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? Auto : value;

    private static void SaveLocalizationSetting(string key, string value)
    {
        LauncherSettings settings = LauncherSettingsPageBinder.LoadSettings();
        if (string.Equals(value, Auto, StringComparison.OrdinalIgnoreCase))
            settings.TextOptions.Remove(key);
        else
            settings.SetTextOption(key, value);
        LauncherSettingsPageBinder.SaveSettings(settings);
    }

    private void QueueReload()
    {
        if (_reloadQueued)
            return;

        _reloadQueued = true;
        Dispatcher.UIThread.Post(
            () =>
            {
                _reloadQueued = false;
                Reload();
            },
            DispatcherPriority.Background);
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
        foreach (MyComboBoxItem item in comboBox.Items.OfType<MyComboBoxItem>())
        {
            if (!string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
                continue;

            comboBox.SelectedItem = item;
            return;
        }

        comboBox.SelectedIndex = comboBox.Items.Count > 0 ? 0 : -1;
    }

    private bool IsFormatCultureItemExisting(string tag) =>
        FormatCultureCombo.Items.OfType<MyComboBoxItem>()
            .Any(item => string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase));

    private static bool IsLanguageSupported(string value) =>
        string.Equals(value, Auto, StringComparison.OrdinalIgnoreCase) ||
        SupportedLanguages.Any(language => string.Equals(language.Code, value, StringComparison.OrdinalIgnoreCase));

    private static SupportedLanguage ResolveLanguage(string value)
    {
        if (string.Equals(value, Auto, StringComparison.OrdinalIgnoreCase))
        {
            CultureInfo current = CultureInfo.CurrentUICulture;
            return SupportedLanguages.FirstOrDefault(language =>
                    string.Equals(language.CultureName, current.Name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(language.Code, current.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase))
                ?? SupportedLanguages[0];
        }

        return SupportedLanguages.FirstOrDefault(language =>
                string.Equals(language.Code, value, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(language.CultureName, value, StringComparison.OrdinalIgnoreCase))
            ?? SupportedLanguages[0];
    }

    private string FindString(string key, string fallback)
    {
        if (this.TryGetResource(key, null, out object? resource) && resource is string text)
            return text;

        return fallback;
    }

    private MyScrollViewer LanguageScroll => this.FindControl<MyScrollViewer>("PanBack")
        ?? throw new InvalidOperationException("PageSetupLauncherLanguage 缺少 PanBack。");

    private MyComboBox LanguageCombo => this.FindControl<MyComboBox>("ComboUiLanguage")
        ?? throw new InvalidOperationException("PageSetupLauncherLanguage 缺少 ComboUiLanguage。");

    private MyComboBox FormatCultureCombo => this.FindControl<MyComboBox>("ComboUiFormatCulture")
        ?? throw new InvalidOperationException("PageSetupLauncherLanguage 缺少 ComboUiFormatCulture。");

    private static readonly SupportedLanguage[] SupportedLanguages =
    [
        new("zh-CN", "zh-CN", "简体中文（中国大陆）", "Microsoft YaHei UI, PingFang SC, Noto Sans CJK SC, Segoe UI"),
        new("en-US", "en-US", "English (US)", "Segoe UI, Arial")
    ];

    private sealed record SupportedLanguage(
        string Code,
        string CultureName,
        string NativeName,
        string RepresentativeFontFamily);
}
