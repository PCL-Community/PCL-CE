// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace PCL.Desktop.Controls.Legacy;

public partial class MinecraftServerQuery : Grid
{
    public MinecraftServerQuery()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public async Task ServerQueryAsync(CancellationToken cancellationToken = default)
    {
        string address = (LabServerIp.Text ?? string.Empty).Trim().Replace('：', ':');
        ServerInfo.IsVisible = true;

        if (string.IsNullOrWhiteSpace(address))
        {
            PanMcServer.ShowClientMessage("请输入服务器地址。");
            return;
        }

        if (address.Contains('/', StringComparison.Ordinal))
        {
            PanMcServer.ShowClientMessage("服务器地址中不应包含 /。", "请填写服务器域名或 IP，端口可写作 example.com:25565。");
            return;
        }

        await PanMcServer.UpdateServerInfoAsync(address, cancellationToken).ConfigureAwait(true);
    }

    private async void BtnServerQueryClick(object? sender, EventArgs e)
    {
        try
        {
            await ServerQueryAsync().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
        }
    }

}
