// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia.Markup.Xaml;
using PCL.Desktop.Controls.Legacy;
using PCL.Desktop.Plugins;

namespace PCL.Desktop.Features.Settings.Views;

public partial class PageSetupPlugin : MyPageRight
{
    public PageSetupPlugin()
    {
        AvaloniaXamlLoader.Load(this);
        PanScroll = PanBack;
        RefreshPluginState();
    }

    private void RefreshPluginState()
    {
        DesktopPluginHost.Initialize();
        if (LabPluginState is null)
            return;

        int pluginCount = DesktopPluginHost.Plugins.Count;
        int featureCount = DesktopPluginHost.Features.Count;
        LabPluginState.Text = pluginCount == 0
            ? "尚未检测到已注入的插件运行时。Online 将由后续 PluginSDK 内置插件提供。"
            : $"已加载 {pluginCount} 个插件模块，注册 {featureCount} 个插件功能。";
    }
}
