// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using PCL.Application.Hosting.PluginPlatform;
using PCL.Application.Settings;
using PCL.Desktop.Controls.Legacy;

namespace PCL.Desktop.Features.Settings.Views;

internal static class HostSettingsPageFactory
{
    public static MyPageRight Create(HostSettingsPageDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return descriptor.Id switch
        {
            PluginSettingsPageIds.Installed => new PageSetupPluginInstalled(descriptor),
            PluginSettingsPageIds.Market => new PageSetupPluginMarket(descriptor),
            PluginSettingsPageIds.Safety => new PageSetupPluginSafety(descriptor),
            PluginSettingsPageIds.UiPatches => new PageSetupPluginUiPatches(descriptor),
            PluginSettingsPageIds.Compatibility => new PageSetupPluginCompatibility(descriptor),
            PluginSettingsPageIds.LegacySettings => new PageSetupPluginInstalled(descriptor),
            _ => new PageSetupHostModule(descriptor)
        };
    }
}
