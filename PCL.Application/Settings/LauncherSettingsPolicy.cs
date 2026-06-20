// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using PCL.Core.App;

namespace PCL.Application.Settings;

public static class LauncherSettingsPolicy
{
    public static LauncherSettings Normalize(
        LauncherSettings settings,
        bool supportsSystemAccentTheme,
        bool allowsDomesticMirror)
    {
        ArgumentNullException.ThrowIfNull(settings);

        ColorTheme lightColor = NormalizeColor(
            settings.LightColor,
            supportsSystemAccentTheme);
        ColorTheme darkColor = NormalizeColor(
            settings.DarkColor,
            supportsSystemAccentTheme);
        DownloadSourcePreference downloadSource =
            !allowsDomesticMirror &&
            settings.DownloadSource != DownloadSourcePreference.OfficialOnly
                ? DownloadSourcePreference.OfficialOnly
                : settings.DownloadSource;

        return settings with
        {
            SchemaVersion = LauncherSettings.CurrentSchemaVersion,
            LightColor = lightColor,
            DarkColor = darkColor,
            DownloadSource = downloadSource
        };
    }

    private static ColorTheme NormalizeColor(
        ColorTheme color,
        bool supportsSystemAccentTheme) =>
        !supportsSystemAccentTheme && color == ColorTheme.SystemAccent
            ? ColorTheme.CatBlue
            : color;
}
