// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using PCL.Application.Hosting.RuntimeExtensions;
using PCL.Application.Settings;
using PCL.Desktop.Features.Settings.Views;

namespace PCL.Desktop.Hosting;

/// <summary>Bridges the launcher diagnostics setting without exposing Desktop persistence internals.</summary>
internal sealed class DesktopHostDeveloperDiagnostics : IHostDeveloperDiagnostics
{
    public static DesktopHostDeveloperDiagnostics Instance { get; } = new();

    public bool IsEnabled
    {
        get
        {
            LauncherSettings settings = LauncherSettingsPageBinder.LoadSettings();
            return settings.GetBooleanOption(
                "SystemDebugMode",
                LauncherSettingDefaults.GetBoolean("SystemDebugMode"));
        }
    }

    public void SetEnabled(bool enabled)
    {
        LauncherSettings settings = LauncherSettingsPageBinder.LoadSettings();
        settings.SetBooleanOption("SystemDebugMode", enabled);
        LauncherSettingsPageBinder.SaveSettings(settings);
    }
}
