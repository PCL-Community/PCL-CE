// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Net.Sockets;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.VisualTree;
using PCL.Application.Instances;
using PCL.Desktop.Controls.Legacy;
using PCL.Desktop.Features.Launching.Views;

namespace PCL.Desktop.Features.Instances.Views;

public partial class PageInstanceServerRight : MyPageRight
{
    private readonly IMinecraftServerStatusService _statusService;
    private LaunchInstanceInfo? _instance;

    public PageInstanceServerRight()
        : this(new MinecraftServerStatusService())
    {
    }

    public PageInstanceServerRight(IMinecraftServerStatusService statusService)
    {
        _statusService = statusService ?? throw new ArgumentNullException(nameof(statusService));
        AvaloniaXamlLoader.Load(this);
        PanScroll = this.FindControl<MyScrollViewer>("PanBack");
    }

    public event EventHandler<LaunchInstanceInfo>? RefreshRequested;

    public event EventHandler<LaunchInstanceInfo>? AddServerRequested;

    public event EventHandler<MinecraftServerEntry>? ConnectServerRequested;

    public event EventHandler<MinecraftServerEntry>? RefreshServerRequested;

    public event EventHandler<MinecraftServerEntry>? EditServerRequested;

    public event EventHandler<MinecraftServerEntry>? RemoveServerRequested;

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
            ServerCard serverCard = new();
            serverCard.UpdateServerInfo(server);
            serverCard.RefreshRequested += (_, entry) => _ = RefreshServerCardAsync(serverCard, entry);
            serverCard.ConnectRequested += (_, entry) => ConnectServerRequested?.Invoke(this, entry);
            serverCard.EditRequested += (_, entry) => EditServerRequested?.Invoke(this, entry);
            serverCard.RemoveRequested += (_, entry) => RemoveServerRequested?.Invoke(this, entry);
            stack.Children.Add(serverCard);
        }
        card.Children.Add(stack);
        panel.Children.Add(card);
    }

    private void BtnRefresh_Click(object? sender, EventArgs e)
    {
        if (_instance is not null)
            RefreshRequested?.Invoke(this, _instance);
        Reload();
        foreach (ServerCard card in this.GetVisualDescendants().OfType<ServerCard>())
        {
            if (card.Server is { } server)
                _ = RefreshServerCardAsync(card, server);
        }
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

    private async Task RefreshServerCardAsync(ServerCard card, MinecraftServerEntry server)
    {
        card.SetRefreshing();
        RefreshServerRequested?.Invoke(this, server);
        try
        {
            MinecraftServerStatus status = await _statusService.QueryAsync(server.Address).ConfigureAwait(true);
            if (card.IsAttachedToVisualTree() && Equals(card.Server, server))
                card.UpdateStatus(status);
        }
        catch (Exception ex) when (ex is SocketException or IOException or TimeoutException or
                                   OperationCanceledException or FormatException or JsonException or InvalidDataException)
        {
            if (card.IsAttachedToVisualTree() && Equals(card.Server, server))
                card.UpdateStatusError(ex is OperationCanceledException ? "连接超时" : ex.Message);
        }
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
