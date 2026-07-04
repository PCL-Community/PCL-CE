// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using PCL.Application.Instances;
using PCL.Desktop.Controls.Legacy;
using PCL.Desktop.Features.Launching.Views;

namespace PCL.Desktop.Features.Instances.Views;

public partial class PageInstanceServerRight : MyPageRight
{
    private LaunchInstanceInfo? _instance;

    public PageInstanceServerRight()
    {
        AvaloniaXamlLoader.Load(this);
        PanScroll = this.FindControl<MyScrollViewer>("PanBack");
    }

    public event EventHandler<LaunchInstanceInfo>? RefreshRequested;

    public event EventHandler<LaunchInstanceInfo>? AddServerRequested;

    public void SetInstance(LaunchInstanceInfo instance)
    {
        _instance = instance;
        Reload();
    }

    public void Reload()
    {
        if (this.FindControl<StackPanel>("PanServers") is not { } panel)
            return;

        panel.Children.Clear();
        if (_instance is null)
        {
            SetEmptyVisible(true);
            return;
        }

        string minecraftRoot = GetMinecraftRootFromInstance(_instance);
        IReadOnlyList<MinecraftServerEntry> servers;
        try
        {
            servers = MinecraftServerListService.LoadAsync(minecraftRoot).GetAwaiter().GetResult();
        }
        catch (Exception)
        {
            servers = [];
        }

        if (servers.Count == 0)
        {
            SetEmptyVisible(true);
            return;
        }

        SetEmptyVisible(false);
        MyCard card = new()
        {
            Title = "服务器列表",
            Margin = new Thickness(0d, 0d, 0d, 15d)
        };
        StackPanel stack = new()
        {
            Margin = new Thickness(20d, 40d, 18d, 15d)
        };
        foreach (MinecraftServerEntry server in servers)
        {
            stack.Children.Add(new MyListItem
            {
                Title = server.Name,
                Info = server.Address,
                SvgIcon = "lucide/server",
                Height = 42d,
                Margin = new Thickness(0d, 0d, 0d, 2d),
                IsHitTestVisible = false
            });
        }
        card.Children.Add(stack);
        panel.Children.Add(card);
    }

    private void BtnRefresh_Click(object? sender, EventArgs e)
    {
        if (_instance is not null)
            RefreshRequested?.Invoke(this, _instance);
        Reload();
    }

    private void BtnAddServer_Click(object? sender, EventArgs e)
    {
        if (_instance is not null)
            AddServerRequested?.Invoke(this, _instance);
    }

    private void SetEmptyVisible(bool isVisible)
    {
        if (this.FindControl<Control>("PanNoServer") is { } empty)
            empty.IsVisible = isVisible;
        if (this.FindControl<Control>("PanContent") is { } content)
            content.IsVisible = !isVisible;
        if (this.FindControl<Control>("PanServers") is { } servers)
            servers.IsVisible = !isVisible;
    }

    private static string GetMinecraftRootFromInstance(LaunchInstanceInfo instance)
    {
        DirectoryInfo versionDirectory = new(instance.InstanceDirectory);
        DirectoryInfo? versionsDirectory = versionDirectory.Parent;
        if (versionsDirectory?.Parent is not null &&
            string.Equals(versionsDirectory.Name, "versions", StringComparison.OrdinalIgnoreCase))
        {
            return versionsDirectory.Parent.FullName;
        }

        return instance.InstanceDirectory;
    }
}
