// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using PCL.Application.Settings;
using PCL.Core.App;
using PCL.Platform.Paths;

namespace PCL.Desktop.Theme;

public static class AvaloniaThemeManager
{
    private const string SettingsPathOverrideEnvironmentVariable = "PCLN_LAUNCHER_SETTINGS_PATH";

    public static LauncherSettings CurrentSettings { get; private set; } = new();

    public static bool IsDarkMode { get; private set; }

    public static void InitializeFromSettings()
    {
        CurrentSettings = LoadSettings();
        Apply(CurrentSettings);
    }

    public static void Apply(LauncherSettings settings)
    {
        CurrentSettings = LauncherSettingsPolicy.Normalize(
            settings,
            supportsSystemAccentTheme: false,
            allowsDomesticMirror: true);
        IsDarkMode = ResolveDarkMode(CurrentSettings.ColorMode);

        if (Avalonia.Application.Current is { } application)
        {
            application.RequestedThemeVariant = IsDarkMode ? ThemeVariant.Dark : ThemeVariant.Light;
            ApplyResources(application.Resources, ThemeColorPalette.Create(IsDarkMode, ResolveTheme(IsDarkMode)));
            application.Resources["LaunchFontFamily"] = ResolveLaunchFontFamily(CurrentSettings);
        }
    }

    private static LauncherSettings LoadSettings()
    {
        try
        {
            using LauncherSettingsStore store = new(CreateSettingsPath());
            LauncherSettingsLoadResult result = store.LoadAsync().AsTask().GetAwaiter().GetResult();
            return result.Settings;
        }
        catch
        {
            return new LauncherSettings();
        }
    }

    private static string CreateSettingsPath()
    {
        string? overridePath = Environment.GetEnvironmentVariable(SettingsPathOverrideEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overridePath))
            return Path.GetFullPath(overridePath);

        DefaultPlatformPathProvider paths = new();
        return Path.Combine(paths.ApplicationDataDirectory, "PCL-N", "launcher-settings.json");
    }

    private static bool ResolveDarkMode(ColorMode mode) =>
        mode switch
        {
            ColorMode.Light => false,
            ColorMode.Dark => true,
            _ => Avalonia.Application.Current?.ActualThemeVariant == ThemeVariant.Dark
        };

    private static ColorTheme ResolveTheme(bool isDarkMode)
    {
        ColorTheme theme = isDarkMode ? CurrentSettings.DarkColor : CurrentSettings.LightColor;
        return ThemeColorPalette.NormalizeTheme(theme);
    }

    private static void ApplyResources(IResourceDictionary resources, IReadOnlyDictionary<string, Color> palette)
    {
        foreach (KeyValuePair<string, Color> entry in palette)
        {
            if (entry.Key.StartsWith("ColorBrush", StringComparison.Ordinal))
                resources[entry.Key] = new SolidColorBrush(entry.Value);
            else
                resources[entry.Key] = entry.Value;
        }
    }

    private static FontFamily ResolveLaunchFontFamily(LauncherSettings settings)
    {
        string fontName = settings.GetTextOption("UiFont").Trim();
        if (string.IsNullOrEmpty(fontName))
            return new FontFamily("Microsoft YaHei UI, Segoe UI, Arial");

        try
        {
            return new FontFamily(fontName);
        }
        catch (ArgumentException)
        {
            return new FontFamily("Microsoft YaHei UI, Segoe UI, Arial");
        }
    }
}
