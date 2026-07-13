// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PCL.Application.Settings;
using PCL.Desktop.Controls.Legacy;

namespace PCL.Desktop.Features.Settings.Views;

/// <summary>Generic HostModule settings descriptor page for pages without a Desktop-specific implementation.</summary>
public sealed class PageSetupHostModule : MyPageRight
{
    public PageSetupHostModule(HostSettingsPageDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        StackPanel panel = new() { Margin = new Thickness(25d, 25d, 25d, 10d) };
        MyCard card = new() { Title = descriptor.Title };
        StackPanel content = new() { Margin = new Thickness(25d, 40d, 25d, 20d), Spacing = 10 };
        content.Children.Add(new TextBlock
        {
            Name = "LabHostHeading",
            Text = descriptor.Heading,
            FontSize = 20d,
            FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(new TextBlock
        {
            Name = "LabHostDescription",
            Text = descriptor.Description,
            FontSize = 13d,
            TextWrapping = TextWrapping.Wrap
        });
        foreach (HostSettingsHintDescriptor hint in descriptor.Hints)
        {
            content.Children.Add(new MyHint
            {
                Text = hint.Text,
                Theme = hint.Kind switch
                {
                    HostSettingsHintKind.Warning => MyHint.Themes.Yellow,
                    HostSettingsHintKind.Error => MyHint.Themes.Red,
                    _ => MyHint.Themes.Blue
                },
                Margin = new Thickness(0d, 4d, 0d, 0d)
            });
        }

        card.Children.Add(content);
        panel.Children.Add(card);
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
}
