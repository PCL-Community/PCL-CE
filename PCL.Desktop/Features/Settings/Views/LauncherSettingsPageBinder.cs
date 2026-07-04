// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using PCL.Application.Settings;
using PCL.Core.App;
using PCL.Desktop.Controls.Legacy;
using PCL.Desktop.Theme;
using PCL.Platform.Paths;

namespace PCL.Desktop.Features.Settings.Views;

internal static class LauncherSettingsPageBinder
{
    private const string SettingsPathOverrideEnvironmentVariable = "PCLN_LAUNCHER_SETTINGS_PATH";
    private static readonly object LatestSavedSettingsLock = new();
    private static LatestSavedSettings? _latestSavedSettings;

    private static readonly ColorTheme[] ThemeOrder =
    [
        ColorTheme.SkyBlue,
        ColorTheme.CatBlue,
        ColorTheme.DeathBlue,
        ColorTheme.HmclBlue
    ];

    public static readonly string[] ThemeColorNames =
    [
        "天空蓝",
        "龙猫蓝",
        "死亡蓝",
        "HMCL 蓝"
    ];

    public static void Attach(MyPageRight page, Action<LauncherSettings>? settingsApplied = null)
    {
        ArgumentNullException.ThrowIfNull(page);

        LauncherSettings settings = LoadSettings();
        bool isApplying = true;
        ApplySettings(page, settings);
        settingsApplied?.Invoke(settings);
        isApplying = false;
        Window? ownerWindow = null;
        page.AttachedToVisualTree += (_, _) =>
        {
            isApplying = false;
            if (TopLevel.GetTopLevel(page) is not Window window || ReferenceEquals(ownerWindow, window))
                return;

            if (ownerWindow is not null)
            {
                UnwireOwnerWindow(ownerWindow);
            }

            ownerWindow = window;
            ownerWindow.Activated += OwnerWindow_Activated;
            ownerWindow.Deactivated += OwnerWindow_Deactivated;
            ownerWindow.Closing += OwnerWindow_Closing;
            ownerWindow.Closed += OwnerWindow_Closed;
        };
        page.DetachedFromVisualTree += (_, _) => isApplying = true;
        page.DetachedFromLogicalTree += (_, _) => isApplying = true;

        void OwnerWindow_Closing(object? sender, WindowClosingEventArgs e)
        {
            isApplying = true;
            if (ownerWindow is not null)
            {
                UnwireOwnerWindow(ownerWindow);
            }
            ownerWindow = null;
        }

        void OwnerWindow_Activated(object? sender, EventArgs e) => isApplying = false;

        void OwnerWindow_Deactivated(object? sender, EventArgs e) => isApplying = true;

        void OwnerWindow_Closed(object? sender, EventArgs e)
        {
            isApplying = true;
            FlushLatestSettings();
            if (ownerWindow is not null)
            {
                UnwireOwnerWindow(ownerWindow);
                ownerWindow = null;
            }
        }

        void UnwireOwnerWindow(Window window)
        {
            window.Activated -= OwnerWindow_Activated;
            window.Deactivated -= OwnerWindow_Deactivated;
            window.Closing -= OwnerWindow_Closing;
            window.Closed -= OwnerWindow_Closed;
        }

        foreach (MyCheckBox checkBox in page.GetVisualDescendants().OfType<MyCheckBox>())
        {
            checkBox.Change += (_, _) =>
            {
                if (isApplying || !IsInteractive(page))
                    return;

                string? tag = GetTag(checkBox);
                if (string.IsNullOrWhiteSpace(tag))
                    return;

                settings = LoadSettings();
                bool value = checkBox.Checked == true;
                settings.BooleanOptions[tag] = value;
                if (tag == "LaunchAutoRepairGame")
                    settings = settings with { AutomaticallyRepairGameIssues = value };

                SaveSettings(settings);
            };
        }

        foreach (MyComboBox comboBox in page.GetVisualDescendants().OfType<MyComboBox>())
        {
            void PersistComboBox()
            {
                if (isApplying || !IsInteractive(page) || comboBox.SelectedIndex < 0)
                    return;

                string? tag = GetTag(comboBox);
                if (string.IsNullOrWhiteSpace(tag))
                    return;

                settings = LoadSettings();
                settings.IntegerOptions[tag] = comboBox.SelectedIndex;
                bool shouldApplyTheme = false;
                settings = tag switch
                {
                    "UiDarkMode" => settings with
                    {
                        ColorMode = (ColorMode)Math.Clamp(comboBox.SelectedIndex, 0, 2)
                    },
                    "UiLightColor" => settings with
                    {
                        LightColor = GetTheme(comboBox.SelectedIndex)
                    },
                    "UiDarkColor" => settings with
                    {
                        DarkColor = GetTheme(comboBox.SelectedIndex)
                    },
                    "ToolDownloadSource" or "ToolDownloadVersion" or "ToolDownloadMod" => settings with
                    {
                        DownloadSource = (DownloadSourcePreference)Math.Clamp(comboBox.SelectedIndex, 0, 2)
                    },
                    _ => settings
                };
                shouldApplyTheme = tag is "UiDarkMode" or "UiLightColor" or "UiDarkColor";

                if (comboBox.IsEditable)
                    settings.TextOptions[tag] = comboBox.Text ?? string.Empty;

                SaveSettings(settings);
                if (shouldApplyTheme)
                    AvaloniaThemeManager.Apply(settings);
            }

            comboBox.SelectionChanged += (_, _) => PersistComboBox();
            comboBox.GetObservable(ComboBox.SelectedIndexProperty).Subscribe(_ => PersistComboBox());

            if (comboBox.IsEditable)
            {
                comboBox.TextChanged += (_, _) =>
                {
                    if (isApplying || !IsInteractive(page))
                        return;

                    string? tag = GetTag(comboBox);
                    if (string.IsNullOrWhiteSpace(tag))
                        return;

                    settings = LoadSettings();
                    settings.TextOptions[tag] = comboBox.Text ?? string.Empty;
                    SaveSettings(settings);
                };
            }
        }

        foreach (MySlider slider in page.GetVisualDescendants().OfType<MySlider>())
        {
            slider.Change += (_, _) =>
            {
                if (isApplying || !IsInteractive(page))
                    return;

                string? tag = GetTag(slider);
                if (string.IsNullOrWhiteSpace(tag))
                    return;

                settings = LoadSettings();
                settings.IntegerOptions[tag] = slider.Value;
                SaveSettings(settings);
            };
        }

        foreach (MyTextBox textBox in page.GetVisualDescendants().OfType<MyTextBox>())
        {
            textBox.TextChanged += (_, _) =>
            {
                if (isApplying || !IsInteractive(page))
                    return;

                string? tag = GetTag(textBox);
                if (string.IsNullOrWhiteSpace(tag))
                    return;

                settings = LoadSettings();
                settings.TextOptions[tag] = textBox.Text ?? string.Empty;
                SaveSettings(settings);
            };
        }

        foreach (MyRadioBox radioBox in page.GetVisualDescendants().OfType<MyRadioBox>())
        {
            radioBox.Check += (_, _) =>
            {
                if (isApplying || !IsInteractive(page) || !radioBox.Checked)
                    return;

                if (!TryParseRadioTag(GetTag(radioBox), out string? key, out int value))
                    return;

                settings = LoadSettings();
                settings.IntegerOptions[key] = value;
                SaveSettings(settings);
            };
        }
    }

    private static void ApplySettings(MyPageRight page, LauncherSettings settings)
    {
        foreach (MyComboBox comboBox in page.GetVisualDescendants().OfType<MyComboBox>())
        {
            string? tag = GetTag(comboBox);
            if (string.IsNullOrWhiteSpace(tag))
                continue;

            if (tag is "UiLightColor" or "UiDarkColor")
                comboBox.ItemsSource = ThemeColorNames;

            if (tag == "UiDarkMode")
                SetComboIndex(comboBox, (int)settings.ColorMode);
            else if (tag == "UiLightColor")
                SetComboIndex(comboBox, GetThemeIndex(settings.LightColor));
            else if (tag == "UiDarkColor")
                SetComboIndex(comboBox, GetThemeIndex(settings.DarkColor));
            else if (tag is "ToolDownloadSource" or "ToolDownloadVersion" or "ToolDownloadMod")
                SetComboIndex(comboBox, (int)settings.DownloadSource);
            else if (settings.IntegerOptions.TryGetValue(tag, out int index))
                SetComboIndex(comboBox, index);

            if (comboBox.IsEditable && settings.TextOptions.TryGetValue(tag, out string? text))
                comboBox.Text = text;
        }

        foreach (MyCheckBox checkBox in page.GetVisualDescendants().OfType<MyCheckBox>())
        {
            string? tag = GetTag(checkBox);
            if (string.IsNullOrWhiteSpace(tag))
                continue;

            if (tag == "LaunchAutoRepairGame")
                checkBox.Checked = settings.AutomaticallyRepairGameIssues;
            else if (settings.BooleanOptions.TryGetValue(tag, out bool value))
                checkBox.Checked = value;
        }

        foreach (MySlider slider in page.GetVisualDescendants().OfType<MySlider>())
        {
            string? tag = GetTag(slider);
            if (!string.IsNullOrWhiteSpace(tag) && settings.IntegerOptions.TryGetValue(tag, out int value))
                slider.Value = Math.Clamp(value, 0, slider.MaxValue);
        }

        foreach (MyTextBox textBox in page.GetVisualDescendants().OfType<MyTextBox>())
        {
            string? tag = GetTag(textBox);
            if (!string.IsNullOrWhiteSpace(tag) && settings.TextOptions.TryGetValue(tag, out string? value))
                textBox.Text = value;
        }

        foreach (IGrouping<string, MyRadioBox> group in page.GetVisualDescendants()
                     .OfType<MyRadioBox>()
                     .Select(static radio => (Radio: radio, Parsed: TryParseRadioTag(GetTag(radio), out string? key, out int value)
                         ? (Key: key, Value: value)
                         : ((string Key, int Value)?)null))
                     .Where(static item => item.Parsed is not null)
                     .GroupBy(static item => item.Parsed!.Value.Key, static item => item.Radio))
        {
            if (!settings.IntegerOptions.TryGetValue(group.Key, out int selectedValue))
                continue;

            foreach (MyRadioBox radioBox in group)
            {
                if (TryParseRadioTag(GetTag(radioBox), out _, out int value))
                    radioBox.Checked = value == selectedValue;
            }
        }
    }

    internal static LauncherSettings LoadSettings()
    {
        using LauncherSettingsStore store = new(CreateSettingsPath());
        LauncherSettings settings = store.LoadAsync().AsTask().GetAwaiter().GetResult().Settings;
        return settings with
        {
            BooleanOptions = settings.BooleanOptions is null ? [] : new Dictionary<string, bool>(settings.BooleanOptions),
            IntegerOptions = settings.IntegerOptions is null ? [] : new Dictionary<string, int>(settings.IntegerOptions),
            TextOptions = settings.TextOptions is null ? [] : new Dictionary<string, string>(settings.TextOptions)
        };
    }

    internal static void SaveSettings(LauncherSettings settings)
    {
        string settingsPath = CreateSettingsPath();
        using LauncherSettingsStore store = new(settingsPath);
        store.SaveAsync(settings).AsTask().GetAwaiter().GetResult();
        lock (LatestSavedSettingsLock)
            _latestSavedSettings = new LatestSavedSettings(settingsPath, settings);
    }

    private static void FlushLatestSettings()
    {
        LatestSavedSettings? latest;
        lock (LatestSavedSettingsLock)
            latest = _latestSavedSettings;

        if (latest is null)
            return;

        using LauncherSettingsStore store = new(latest.SettingsPath);
        store.SaveAsync(latest.Settings).AsTask().GetAwaiter().GetResult();
    }

    internal static string CreateSettingsPath()
    {
        string? overridePath = Environment.GetEnvironmentVariable(SettingsPathOverrideEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overridePath))
            return Path.GetFullPath(overridePath);

        DefaultPlatformPathProvider paths = new();
        return Path.Combine(paths.ApplicationDataDirectory, "PCL-N", "launcher-settings.json");
    }

    internal static string CreateDataDirectory()
    {
        string settingsDirectory = Path.GetDirectoryName(CreateSettingsPath()) ?? AppContext.BaseDirectory;
        Directory.CreateDirectory(settingsDirectory);
        return settingsDirectory;
    }

    private static string? GetTag(Control control) => control.Tag?.ToString();

    private static bool IsInteractive(Control page)
    {
        if (!page.IsAttachedToVisualTree())
            return false;

        return TopLevel.GetTopLevel(page) is not Window { IsVisible: false };
    }

    private static bool TryParseRadioTag(string? tag, out string key, out int value)
    {
        key = string.Empty;
        value = 0;
        if (string.IsNullOrWhiteSpace(tag))
            return false;

        int separator = tag.LastIndexOf('/');
        if (separator <= 0 || separator == tag.Length - 1)
            return false;

        key = tag[..separator];
        return int.TryParse(tag[(separator + 1)..], out value);
    }

    private static void SetComboIndex(MyComboBox comboBox, int index)
    {
        if (comboBox.ItemCount > 0)
            comboBox.SelectedIndex = Math.Clamp(index, 0, comboBox.ItemCount - 1);
    }

    private static ColorTheme GetTheme(int index) =>
        ThemeOrder[Math.Clamp(index, 0, ThemeOrder.Length - 1)];

    private static int GetThemeIndex(ColorTheme theme)
    {
        int index = Array.IndexOf(ThemeOrder, ThemeColorPalette.NormalizeTheme(theme));
        return index < 0 ? Array.IndexOf(ThemeOrder, ColorTheme.CatBlue) : index;
    }

    private sealed record LatestSavedSettings(string SettingsPath, LauncherSettings Settings);
}
