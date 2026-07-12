// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using System.Runtime.CompilerServices;
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
    private static readonly ConditionalWeakTable<MyPageRight, BindingState> BindingStates = new();
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

    internal static event Action<LauncherSettings>? SettingsChanged;

    public static void Attach(MyPageRight page, Action<LauncherSettings>? settingsApplied = null)
    {
        ArgumentNullException.ThrowIfNull(page);

        BindingState state = new(page, settingsApplied);
        BindingStates.Remove(page);
        BindingStates.Add(page, state);
        LauncherSettings settings = LoadSettings();
        ApplySettings(page, settings);
        settingsApplied?.Invoke(settings);
        state.IsApplying = false;
        Window? ownerWindow = null;
        page.AttachedToVisualTree += (_, _) =>
        {
            state.IsApplying = false;
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
        page.DetachedFromVisualTree += (_, _) => state.IsApplying = true;
        page.DetachedFromLogicalTree += (_, _) => state.IsApplying = true;

        void OwnerWindow_Closing(object? sender, WindowClosingEventArgs e)
        {
            state.IsApplying = true;
            if (ownerWindow is not null)
            {
                UnwireOwnerWindow(ownerWindow);
            }
            ownerWindow = null;
        }

        void OwnerWindow_Activated(object? sender, EventArgs e) => state.IsApplying = false;

        void OwnerWindow_Deactivated(object? sender, EventArgs e) => state.IsApplying = true;

        void OwnerWindow_Closed(object? sender, EventArgs e)
        {
            state.IsApplying = true;
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

        foreach (MyCheckBox checkBox in GetControlDescendants(page).OfType<MyCheckBox>())
        {
            checkBox.Change += (_, _) =>
            {
                if (state.IsApplying || !IsInteractive(page))
                    return;

                string? tag = GetTag(checkBox);
                if (string.IsNullOrWhiteSpace(tag))
                    return;

                settings = LoadSettings();
                bool value = checkBox.Checked == true;
                settings.SetBooleanOption(tag, value);
                if (tag == "LaunchAutoRepairGame")
                    settings = settings with { AutomaticallyRepairGameIssues = value };

                SaveSettings(settings);
            };
        }

        foreach (MyComboBox comboBox in GetControlDescendants(page).OfType<MyComboBox>())
        {
            void PersistComboBox()
            {
                if (state.IsApplying || !IsInteractive(page) || comboBox.SelectedIndex < 0)
                    return;

                string? tag = GetTag(comboBox);
                if (string.IsNullOrWhiteSpace(tag))
                    return;

                settings = LoadSettings();
                int comboValue = GetComboValue(comboBox);
                settings.SetIntegerOption(tag, comboValue);
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
                    settings.SetTextOption(tag, comboBox.Text ?? string.Empty);

                // Apply theme palette first so SettingsChanged / form chrome see new colors.
                if (shouldApplyTheme)
                    AvaloniaThemeManager.Apply(settings);
                // Always flush immediately so defaults/persist survive page switches.
                SaveSettings(settings);
            }

            comboBox.SelectionChanged += (_, _) => PersistComboBox();
            comboBox.GetObservable(ComboBox.SelectedIndexProperty).Subscribe(_ => PersistComboBox());

            if (comboBox.IsEditable)
            {
                comboBox.TextChanged += (_, _) =>
                {
                    if (state.IsApplying || !IsInteractive(page))
                        return;

                    string? tag = GetTag(comboBox);
                    if (string.IsNullOrWhiteSpace(tag))
                        return;

                    settings = LoadSettings();
                    settings.SetTextOption(tag, comboBox.Text ?? string.Empty);
                    SaveSettings(settings);
                };
            }
        }

        foreach (MySlider slider in GetControlDescendants(page).OfType<MySlider>())
        {
            slider.Change += (_, _) =>
            {
                if (state.IsApplying || !IsInteractive(page))
                    return;

                string? tag = GetTag(slider);
                if (string.IsNullOrWhiteSpace(tag))
                    return;

                settings = LoadSettings();
                settings.SetIntegerOption(tag, slider.Value);
                SaveSettings(settings);
            };
        }

        foreach (MyTextBox textBox in GetControlDescendants(page).OfType<MyTextBox>())
        {
            textBox.TextChanged += (_, _) =>
            {
                if (state.IsApplying || !IsInteractive(page))
                    return;

                string? tag = GetTag(textBox);
                if (string.IsNullOrWhiteSpace(tag))
                    return;

                settings = LoadSettings();
                settings.SetTextOption(tag, textBox.Text ?? string.Empty);
                SaveSettings(settings);
            };
        }

        foreach (MyRadioBox radioBox in GetControlDescendants(page).OfType<MyRadioBox>())
        {
            radioBox.Check += (_, _) =>
            {
                if (state.IsApplying || !IsInteractive(page) || !radioBox.Checked)
                    return;

                if (!TryParseRadioTag(GetTag(radioBox), out string? key, out int value))
                    return;

                settings = LoadSettings();
                settings.SetIntegerOption(key, value);
                SaveSettings(settings);
            };
        }
    }

    internal static bool ResetPage(MyPageRight page)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (!BindingStates.TryGetValue(page, out BindingState? state))
            return false;

        state.IsApplying = true;
        try
        {
            LauncherSettings settings = LoadSettings();
            LauncherSettings defaults = new();
            bool resetAutoRepair = page is PageSetupLaunch;
            bool resetColorMode = page is PageSetupUI;
            bool resetLightColor = page is PageSetupUI;
            bool resetDarkColor = page is PageSetupUI;
            bool resetDownloadSource = page is PageSetupGameManage;

            foreach (Control control in state.TaggedControls)
            {
                string? tag = GetTag(control);
                if (string.IsNullOrWhiteSpace(tag))
                    continue;

                string settingKey = control is MyRadioBox && TryParseRadioTag(tag, out string key, out _)
                    ? key
                    : tag;
                settings.BooleanOptions.Remove(settingKey);
                settings.IntegerOptions.Remove(settingKey);
                settings.TextOptions.Remove(settingKey);

                resetAutoRepair |= tag == "LaunchAutoRepairGame";
                resetColorMode |= tag == "UiDarkMode";
                resetLightColor |= tag == "UiLightColor";
                resetDarkColor |= tag == "UiDarkColor";
                resetDownloadSource |= tag is "ToolDownloadSource" or "ToolDownloadVersion" or "ToolDownloadMod";
            }

            settings = settings with
            {
                AutomaticallyRepairGameIssues = resetAutoRepair
                    ? defaults.AutomaticallyRepairGameIssues
                    : settings.AutomaticallyRepairGameIssues,
                ColorMode = resetColorMode ? defaults.ColorMode : settings.ColorMode,
                LightColor = resetLightColor ? defaults.LightColor : settings.LightColor,
                DarkColor = resetDarkColor ? defaults.DarkColor : settings.DarkColor,
                DownloadSource = resetDownloadSource ? defaults.DownloadSource : settings.DownloadSource
            };

            SaveSettings(settings);
            state.RestoreControlDefaults();
            ApplySettings(page, settings);
            state.SettingsApplied?.Invoke(settings);
            if (page is IRefreshableSettingsPage refreshable)
                refreshable.RefreshPage();
            return true;
        }
        finally
        {
            state.IsApplying = !page.IsAttachedToVisualTree();
        }
    }

    internal static bool ReloadPage(MyPageRight page)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (!BindingStates.TryGetValue(page, out BindingState? state))
            return false;

        state.IsApplying = true;
        try
        {
            LauncherSettings settings = LoadSettings();
            state.RestoreControlDefaults();
            ApplySettings(page, settings);
            state.SettingsApplied?.Invoke(settings);
            if (page is IRefreshableSettingsPage refreshable)
                refreshable.RefreshPage();
            return true;
        }
        finally
        {
            state.IsApplying = !page.IsAttachedToVisualTree();
        }
    }

    private static void ApplySettings(MyPageRight page, LauncherSettings settings)
    {
        foreach (MyComboBox comboBox in GetControlDescendants(page).OfType<MyComboBox>())
        {
            string? tag = GetTag(comboBox);
            if (string.IsNullOrWhiteSpace(tag))
                continue;

            if (tag is "UiLightColor" or "UiDarkColor")
                EnsureThemeColorItems(comboBox);

            if (tag == "UiDarkMode")
                SetComboIndex(comboBox, (int)settings.ColorMode);
            else if (tag == "UiLightColor")
                SetComboIndex(comboBox, GetThemeIndex(settings.LightColor));
            else if (tag == "UiDarkColor")
                SetComboIndex(comboBox, GetThemeIndex(settings.DarkColor));
            else if (tag is "ToolDownloadSource" or "ToolDownloadVersion" or "ToolDownloadMod")
                SetComboIndex(comboBox, (int)settings.DownloadSource);
            else if (settings.TryGetIntegerOption(tag, out int index))
                SetComboValue(comboBox, index);
            else
            {
                int defaultIndex = LauncherSettingDefaults.GetInteger(
                    tag,
                    comboBox.SelectedIndex >= 0 ? comboBox.SelectedIndex : 0);
                SetComboValue(comboBox, defaultIndex);
            }

            if (comboBox.IsEditable && settings.TryGetTextOption(tag, out string? text))
                comboBox.Text = text ?? string.Empty;
            else if (comboBox.IsEditable)
                comboBox.Text = LauncherSettingDefaults.GetText(tag, comboBox.Text ?? string.Empty);

            // Force visible selection text (string ItemsSource / DynamicResource could leave blank).
            comboBox.RefreshSelectionDisplay();
        }

        foreach (MyCheckBox checkBox in GetControlDescendants(page).OfType<MyCheckBox>())
        {
            string? tag = GetTag(checkBox);
            if (string.IsNullOrWhiteSpace(tag))
                continue;

            if (tag == "LaunchAutoRepairGame")
                checkBox.Checked = settings.AutomaticallyRepairGameIssues;
            else if (settings.TryGetBooleanOption(tag, out bool value))
                checkBox.Checked = value;
            else
            {
                // Use LauncherSettingDefaults when present; otherwise keep XAML Checked.
                bool xamlFallback = checkBox.Checked == true;
                checkBox.Checked = LauncherSettingDefaults.GetBoolean(tag, xamlFallback);
            }
        }

        foreach (MySlider slider in GetControlDescendants(page).OfType<MySlider>())
        {
            string? tag = GetTag(slider);
            if (!string.IsNullOrWhiteSpace(tag))
            {
                int value = settings.TryGetIntegerOption(tag, out int configured)
                    ? configured
                    : LauncherSettingDefaults.GetInteger(tag, slider.Value);
                slider.Value = Math.Clamp(value, 0, slider.MaxValue);
            }
        }

        foreach (MyTextBox textBox in GetControlDescendants(page).OfType<MyTextBox>())
        {
            string? tag = GetTag(textBox);
            if (!string.IsNullOrWhiteSpace(tag))
            {
                textBox.Text = settings.TryGetTextOption(tag, out string? value)
                    ? value ?? string.Empty
                    : LauncherSettingDefaults.GetText(tag, textBox.Text ?? string.Empty);
            }
        }

        foreach (IGrouping<string, MyRadioBox> group in GetControlDescendants(page)
                     .OfType<MyRadioBox>()
                     .Select(static radio => (Radio: radio, Parsed: TryParseRadioTag(GetTag(radio), out string? key, out int value)
                         ? (Key: key, Value: value)
                         : ((string Key, int Value)?)null))
                     .Where(static item => item.Parsed is not null)
                     .GroupBy(static item => item.Parsed!.Value.Key, static item => item.Radio))
        {
            int selectedValue = settings.TryGetIntegerOption(group.Key, out int configuredValue)
                ? configuredValue
                : LauncherSettingDefaults.GetInteger(group.Key);

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
        return settings.NormalizeOptionDictionaries();
    }

    internal static void SaveSettings(LauncherSettings settings)
    {
        string settingsPath = CreateSettingsPath();
        using LauncherSettingsStore store = new(settingsPath);
        store.SaveAsync(settings).AsTask().GetAwaiter().GetResult();
        lock (LatestSavedSettingsLock)
            _latestSavedSettings = new LatestSavedSettings(settingsPath, settings);
        SettingsChanged?.Invoke(settings);
    }

    internal static void NotifySettingsChanged() => SettingsChanged?.Invoke(LoadSettings());

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

    private static IEnumerable<Control> GetControlDescendants(Control page) =>
        page.GetVisualDescendants()
            .OfType<Control>()
            .Concat(page.GetLogicalDescendants().OfType<Control>())
            .Distinct();

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

    /// <summary>
    /// Populate light/dark theme color combos with real <see cref="MyComboBoxItem"/>s
    /// so the closed selection always shows text (string ItemsSource was blank on enter).
    /// </summary>
    private static void EnsureThemeColorItems(MyComboBox comboBox)
    {
        bool needsRebuild = comboBox.ItemCount != ThemeColorNames.Length;
        if (!needsRebuild)
        {
            for (int i = 0; i < ThemeColorNames.Length; i++)
            {
                string shown = comboBox.Items[i] switch
                {
                    MyComboBoxItem item => item.Content?.ToString() ?? string.Empty,
                    string s => s,
                    _ => comboBox.Items[i]?.ToString() ?? string.Empty
                };
                if (!string.Equals(shown, ThemeColorNames[i], StringComparison.Ordinal))
                {
                    needsRebuild = true;
                    break;
                }
            }
        }

        if (!needsRebuild)
            return;

        int preserve = comboBox.SelectedIndex;
        comboBox.ItemsSource = null;
        comboBox.Items.Clear();
        foreach (string name in ThemeColorNames)
            comboBox.Items.Add(new MyComboBoxItem { Content = name });
        if (preserve >= 0)
            comboBox.SelectedIndex = Math.Clamp(preserve, 0, ThemeColorNames.Length - 1);
    }

    private static ColorTheme GetTheme(int index) =>
        ThemeOrder[Math.Clamp(index, 0, ThemeOrder.Length - 1)];

    private static int GetThemeIndex(ColorTheme theme)
    {
        int index = Array.IndexOf(ThemeOrder, ThemeColorPalette.NormalizeTheme(theme));
        return index < 0 ? Array.IndexOf(ThemeOrder, ColorTheme.CatBlue) : index;
    }

    private sealed record LatestSavedSettings(string SettingsPath, LauncherSettings Settings);

    private sealed class BindingState
    {
        private readonly Dictionary<MyComboBox, (int Index, string Text)> _comboDefaults;
        private readonly Dictionary<MyCheckBox, bool?> _checkDefaults;
        private readonly Dictionary<MySlider, int> _sliderDefaults;
        private readonly Dictionary<MyTextBox, string> _textDefaults;
        private readonly Dictionary<MyRadioBox, bool> _radioDefaults;

        public BindingState(MyPageRight page, Action<LauncherSettings>? settingsApplied)
        {
            SettingsApplied = settingsApplied;
            TaggedControls = GetControlDescendants(page)
                .Where(static control => !string.IsNullOrWhiteSpace(GetTag(control)))
                .ToArray();
            _comboDefaults = TaggedControls.OfType<MyComboBox>()
                .ToDictionary(static combo => combo, static combo => (combo.SelectedIndex, combo.Text ?? string.Empty));
            _checkDefaults = TaggedControls.OfType<MyCheckBox>()
                .ToDictionary(static check => check, static check => check.Checked);
            _sliderDefaults = TaggedControls.OfType<MySlider>()
                .ToDictionary(static slider => slider, static slider => slider.Value);
            _textDefaults = TaggedControls.OfType<MyTextBox>()
                .ToDictionary(static text => text, static text => text.Text ?? string.Empty);
            _radioDefaults = TaggedControls.OfType<MyRadioBox>()
                .ToDictionary(static radio => radio, static radio => radio.Checked);
        }

        public bool IsApplying { get; set; } = true;

        public Control[] TaggedControls { get; }

        public Action<LauncherSettings>? SettingsApplied { get; }

        public void RestoreControlDefaults()
        {
            foreach ((MyComboBox combo, (int index, string text)) in _comboDefaults)
            {
                if (combo.ItemCount > 0)
                    combo.SelectedIndex = Math.Clamp(index, 0, combo.ItemCount - 1);
                if (combo.IsEditable)
                    combo.Text = text;
            }

            foreach ((MyCheckBox check, bool? value) in _checkDefaults)
                check.Checked = value;
            foreach ((MySlider slider, int value) in _sliderDefaults)
                slider.Value = value;
            foreach ((MyTextBox text, string value) in _textDefaults)
                text.Text = value;
            foreach ((MyRadioBox radio, bool value) in _radioDefaults)
                radio.Checked = value;
        }
    }

    private static int GetComboValue(MyComboBox comboBox)
    {
        if (comboBox.SelectedItem is MyComboBoxItem { Tag: { } tag } &&
            int.TryParse(tag.ToString(), out int taggedValue))
        {
            return taggedValue;
        }

        return comboBox.SelectedIndex;
    }

    private static void SetComboValue(MyComboBox comboBox, int value)
    {
        MyComboBoxItem? taggedItem = comboBox.Items
            .OfType<MyComboBoxItem>()
            .FirstOrDefault(item => item.Tag is not null &&
                                    int.TryParse(item.Tag.ToString(), out int taggedValue) &&
                                    taggedValue == value);
        if (taggedItem is not null)
        {
            comboBox.SelectedItem = taggedItem;
            return;
        }

        SetComboIndex(comboBox, value);
    }
}
