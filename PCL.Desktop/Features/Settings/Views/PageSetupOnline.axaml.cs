// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using PCL.Desktop.Controls.Legacy;

#pragma warning disable CA1822, CS0067

namespace PCL.Desktop.Features.Settings.Views;

public partial class PageSetupOnline : MyPageRight, ISettingsPageInteractionSource
{
    public PageSetupOnline()
    {
        AvaloniaXamlLoader.Load(this);
        PanScroll = PanBack;
        LauncherSettingsPageBinder.Attach(this);
        AttachedToVisualTree += (_, _) => RefreshOnlineState();
    }

    public event EventHandler<SettingsPathRequestedEventArgs>? OpenPathRequested;

    public event EventHandler<SettingsUrlRequestedEventArgs>? OpenUrlRequested;

    public event EventHandler<SettingsMessageRequestedEventArgs>? MessageRequested;

    public event EventHandler<SettingsConfirmRequestedEventArgs>? ConfirmRequested;

    private void BtnDeleteCloudProfile_Click(object sender, IconTextButtonClickEventArgs e)
    {
        ShowOnlinePluginUnavailable();
    }

    private void BtnLogin_Click(object sender, IconTextButtonClickEventArgs e)
    {
        ShowOnlinePluginUnavailable();
    }

    private void BtnLogout_Click(object sender, IconTextButtonClickEventArgs e)
    {
        ShowOnlinePluginUnavailable();
    }

    private void BtnRetrySync_Click(object sender, IconTextButtonClickEventArgs e)
    {
        ShowOnlinePluginUnavailable();
    }

    private void BtnSyncDisable_Click(object sender, IconTextButtonClickEventArgs e)
    {
        ShowOnlinePluginUnavailable();
    }

    private void BtnWindowsLogin_Click(object sender, IconTextButtonClickEventArgs e)
    {
        ShowOnlinePluginUnavailable();
    }

    private void SyncCheckBoxChange(object sender, bool user)
    {
        RefreshSyncState();
    }

    private void RefreshSyncState()
    {
        if (LabSyncDisabledHint is not null)
            LabSyncDisabledHint.IsVisible = true;
        if (PanSyncSections is not null)
            PanSyncSections.IsEnabled = false;
        if (PanSyncUnavailable is not null)
            PanSyncUnavailable.IsVisible = true;
    }

    public void RefreshOnlineState()
    {
        if (PanNotLoggedIn is not null)
            PanNotLoggedIn.IsVisible = true;
        if (PanLoggedIn is not null)
            PanLoggedIn.IsVisible = false;
        if (CardSync is not null)
            CardSync.IsVisible = true;

        if (CheckCloudSyncEnabled is not null)
            CheckCloudSyncEnabled.Checked = false;

        RefreshSyncState();
    }

    private void ShowOnlinePluginUnavailable() =>
        MessageRequested?.Invoke(
            this,
            new SettingsMessageRequestedEventArgs(
                "在线功能暂不可用",
                "Microsoft 登录、N Cloud 同步和在线好友将由后续 Online 内置插件提供。当前版本不会加载在线服务。",
                "知道了"));
}
