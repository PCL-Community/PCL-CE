// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using PCL.Application.Settings;
using PCL.Core.App;
using PCL.Platform.Paths;

namespace PCL.Desktop.Theme;

public static class AvaloniaThemeManager
{
    private const string SettingsPathOverrideEnvironmentVariable = "PCLN_LAUNCHER_SETTINGS_PATH";
    private const string WindowsDefaultFontFamily = "Microsoft YaHei UI, Segoe UI, Arial";
    private const string MacOsDefaultFontFamily = "PingFang SC, Hiragino Sans GB, Helvetica Neue, Arial";
    private const string LinuxDefaultFontFamily = "Noto Sans CJK SC, Noto Sans SC, WenQuanYi Micro Hei, DejaVu Sans";
    private static bool _platformThemeHooked;

    public static LauncherSettings CurrentSettings { get; private set; } = new();

    public static bool IsDarkMode { get; private set; }

    /// <summary>Raised after palette resources are updated (UI thread).</summary>
    public static event Action? ThemeChanged;

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

        if (Avalonia.Application.Current is { } application)
        {
            EnsurePlatformThemeHook(application);
            ApplyRequestedVariant(application, CurrentSettings.ColorMode);
            IsDarkMode = ResolveDarkMode(CurrentSettings.ColorMode, application);
            ApplyResources(application.Resources, ThemeColorPalette.Create(IsDarkMode, ResolveTheme(IsDarkMode)));
            application.Resources["LaunchFontFamily"] = ResolveLaunchFontFamily(CurrentSettings);
        }
        else
        {
            IsDarkMode = CurrentSettings.ColorMode == ColorMode.Dark;
        }

        ThemeChanged?.Invoke();
    }

    private static void ApplyRequestedVariant(Avalonia.Application application, ColorMode mode)
    {
        // ThemeVariant.Default follows OS light/dark preference.
        application.RequestedThemeVariant = mode switch
        {
            ColorMode.Light => ThemeVariant.Light,
            ColorMode.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }

    private static void EnsurePlatformThemeHook(Avalonia.Application application)
    {
        if (_platformThemeHooked)
            return;

        IPlatformSettings? platformSettings = application.PlatformSettings;
        if (platformSettings is null)
            return;

        _platformThemeHooked = true;
        platformSettings.ColorValuesChanged += (_, _) =>
        {
            if (CurrentSettings.ColorMode != ColorMode.System)
                return;

            // Re-resolve dark/light + palette when OS appearance changes.
            Dispatcher.UIThread.Post(
                () => Apply(CurrentSettings),
                DispatcherPriority.Background);
        };
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

    private static bool ResolveDarkMode(ColorMode mode, Avalonia.Application application)
    {
        if (mode == ColorMode.Light)
            return false;
        if (mode == ColorMode.Dark)
            return true;

        // ColorMode.System — prefer live OS values over stale ActualThemeVariant.
        try
        {
            IPlatformSettings? platformSettings = application.PlatformSettings;
            if (platformSettings is not null)
            {
                PlatformColorValues colors = platformSettings.GetColorValues();
                return colors.ThemeVariant == PlatformThemeVariant.Dark;
            }
        }
        catch
        {
            // fall through
        }

        return application.ActualThemeVariant == ThemeVariant.Dark;
    }

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
            {
                // Mutate in place so controls that hold the brush reference (MyButton etc.) update live.
                object? existing = null;
                if (resources.TryGetResource(entry.Key, theme: null, out existing) ||
                    resources.TryGetValue(entry.Key, out existing))
                {
                    if (existing is SolidColorBrush solidBrush)
                    {
                        if (solidBrush.Color != entry.Value)
                            solidBrush.Color = entry.Value;
                        continue;
                    }
                }

                resources[entry.Key] = new SolidColorBrush(entry.Value);
            }
            else
            {
                resources[entry.Key] = entry.Value;
            }
        }
    }

    private static FontFamily ResolveLaunchFontFamily(LauncherSettings settings)
    {
        string fontName = settings.GetTextOption("UiFont").Trim();
        if (string.IsNullOrEmpty(fontName))
            return new FontFamily(GetDefaultLaunchFontFamilyName());

        // FontFamily construction does not fail when the named family is absent.
        // Settings copied from Windows therefore used to leave Linux with a
        // non-rendering font. Fall back to the platform chain when no installed
        // family matches the configured single-family name.
        if (!fontName.Contains(',') && !IsInstalledFont(fontName))
            return new FontFamily(GetDefaultLaunchFontFamilyName());

        try
        {
            return new FontFamily(fontName);
        }
        catch (ArgumentException)
        {
            return new FontFamily(GetDefaultLaunchFontFamilyName());
        }
    }

    internal static string GetDefaultLaunchFontFamilyName()
    {
        if (OperatingSystem.IsLinux())
            return LinuxDefaultFontFamily;
        if (OperatingSystem.IsMacOS())
            return MacOsDefaultFontFamily;
        return WindowsDefaultFontFamily;
    }

    private static bool IsInstalledFont(string fontName)
    {
        try
        {
            return FontManager.Current.SystemFonts.Any(font =>
                string.Equals(font.Name, fontName, StringComparison.OrdinalIgnoreCase));
        }
        catch (InvalidOperationException)
        {
            // Theme initialization can run before the platform font manager in
            // tooling/headless contexts; keep the user's value in that case.
            return true;
        }
    }
}
