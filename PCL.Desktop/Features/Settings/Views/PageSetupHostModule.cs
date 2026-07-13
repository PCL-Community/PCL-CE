// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia.Controls;
using PCL.Application.Settings;
using PCL.Desktop.Controls.Legacy;

namespace PCL.Desktop.Features.Settings.Views;

/// <summary>
/// Empty routing target for a HostModule page without a Desktop implementation.
/// The host retains the page ID but never fabricates plugin-owned page chrome or content.
/// </summary>
public sealed class PageSetupHostModule : MyPageRight
{
    public PageSetupHostModule(HostSettingsPageDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        PageId = descriptor.Id;

        Panel panel = new Grid { Name = "PanHostModuleContent" };
        MyScrollViewer scroll = new()
        {
            Name = "PanBack",
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = panel
        };
        PanScroll = scroll;
        Content = scroll;
    }

    public string PageId { get; }
}
