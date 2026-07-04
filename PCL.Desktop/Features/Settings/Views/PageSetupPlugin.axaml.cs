// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia.Markup.Xaml;
using PCL.Desktop.Controls.Legacy;
using PCL.Desktop.Hosting;

namespace PCL.Desktop.Features.Settings.Views;

public partial class PageSetupPlugin : MyPageRight
{
    public PageSetupPlugin()
    {
        AvaloniaXamlLoader.Load(this);
        PanScroll = PanBack;
        RefreshHostModuleState();
    }

    private void RefreshHostModuleState()
    {
        DesktopHost.Initialize();
        if (LabPluginState is null)
            return;

        int moduleCount = DesktopHost.Current.ModuleIds.Count;
        int navigationCount = DesktopHost.Current.Navigation.Pages.Count;
        LabPluginState.Text =
            $"已启用 {moduleCount} 个 Host Module，注册 {navigationCount} 个导航入口。Online 后续将作为外部 Host Module 接入。";
    }
}
