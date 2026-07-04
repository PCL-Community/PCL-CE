// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using PCL.Desktop.Controls.Legacy;
using PCL.Online;

#pragma warning disable CA1822, CS0067

namespace PCL.Desktop.Features.Settings.Views;

public enum OnlineLoginKind
{
    Microsoft,
    Windows
}

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

    public event EventHandler<OnlineLoginKind>? LoginRequested;

    private void BtnDeleteCloudProfile_Click(object sender, IconTextButtonClickEventArgs e)
    {
        ConfirmRequested?.Invoke(
            this,
            new SettingsConfirmRequestedEventArgs(
                "删除云端档案并退出",
                "此操作会删除 PCL N 云端保存的账户档案并退出当前账户。删除后无法从云端恢复，请确认你已经保留了需要的本地数据。",
                confirmed =>
                {
                    if (confirmed)
                        _ = DeleteCloudProfileAndLogoutAsync();
                },
                primaryButton: "删除并退出",
                isWarn: true));
    }

    private void BtnLogin_Click(object sender, IconTextButtonClickEventArgs e)
    {
        LoginRequested?.Invoke(this, OnlineLoginKind.Microsoft);
    }

    private void BtnLogout_Click(object sender, IconTextButtonClickEventArgs e)
    {
        ConfirmRequested?.Invoke(
            this,
            new SettingsConfirmRequestedEventArgs(
                "退出账户",
                "确定要退出当前 PCL N 在线账户吗？",
                confirmed =>
                {
                    if (confirmed)
                        LogoutOnlineAccount();
                }));
    }

    private void BtnRetrySync_Click(object sender, IconTextButtonClickEventArgs e)
    {
        if (CloudSyncService.RetryLastFailed())
        {
            MessageRequested?.Invoke(
                this,
                new SettingsMessageRequestedEventArgs(
                    "正在重试云同步",
                    "启动器正在重新连接 N Cloud。同步结果会自动更新到此页面。",
                    "知道了"));
            RefreshOnlineState();
            return;
        }

        MessageRequested?.Invoke(
            this,
            new SettingsMessageRequestedEventArgs(
                "无需重试",
                OnlineAccountService.IsLoggedIn
                    ? "当前没有可重试的云同步任务。请确认已经开启云同步，并至少选择一个同步项目。"
                    : "请先登录 PCL N 在线账户，然后再使用云同步。",
                "知道了"));
    }

    private void BtnSyncDisable_Click(object sender, IconTextButtonClickEventArgs e)
    {
        ConfirmRequested?.Invoke(
            this,
            new SettingsConfirmRequestedEventArgs(
                "关闭云同步",
                "确定要关闭所有云同步项目吗？本地设置不会被删除。",
                confirmed =>
                {
                    if (!confirmed)
                        return;

                    if (CheckCloudSyncEnabled is not null)
                        CheckCloudSyncEnabled.Checked = false;
                    RefreshSyncState();
                }));
    }

    private void BtnWindowsLogin_Click(object sender, IconTextButtonClickEventArgs e)
    {
        LoginRequested?.Invoke(this, OnlineLoginKind.Windows);
    }

    private void SyncCheckBoxChange(object sender, bool user)
    {
        RefreshSyncState();
    }

    private void RefreshSyncState()
    {
        bool syncEnabled = CheckCloudSyncEnabled?.Checked == true;
        if (LabSyncDisabledHint is not null)
            LabSyncDisabledHint.IsVisible = !syncEnabled;
        if (PanSyncSections is not null)
            PanSyncSections.IsEnabled = syncEnabled;
        if (PanSyncUnavailable is not null)
            PanSyncUnavailable.IsVisible = !CloudSyncService.IsAvailable;
    }

    public void RefreshOnlineState()
    {
        bool isLoggedIn = OnlineAccountService.IsLoggedIn;
        if (PanNotLoggedIn is not null)
            PanNotLoggedIn.IsVisible = !isLoggedIn;
        if (PanLoggedIn is not null)
            PanLoggedIn.IsVisible = isLoggedIn;
        if (CardSync is not null)
            CardSync.IsVisible = isLoggedIn;

        if (isLoggedIn)
        {
            if (LabUserName is not null)
                LabUserName.Text = string.IsNullOrWhiteSpace(OnlineAccountService.UserName)
                    ? "Microsoft 账户"
                    : OnlineAccountService.UserName;
            if (LabAccountType is not null)
            {
                string minecraftProfileName = OnlineRuntime.Host.GetString("Online.MsMinecraftProfileName");
                LabAccountType.Text = (OnlineAccountService.OwnsMinecraft, string.IsNullOrWhiteSpace(minecraftProfileName)) switch
                {
                    (true, false) => "已连接 Microsoft 正版档案",
                    (true, true) => "已连接 Microsoft 账户，待创建 Minecraft 档案",
                    _ => "已连接 Microsoft 账户，当前使用离线档案"
                };
            }
            RefreshAvatar();
        }

        RefreshSyncState();
    }

    private async Task DeleteCloudProfileAndLogoutAsync()
    {
        if (!OnlineAccountService.IsLoggedIn)
        {
            MessageRequested?.Invoke(this, new SettingsMessageRequestedEventArgs("尚未登录", "当前没有可删除的云端档案。"));
            RefreshOnlineState();
            return;
        }

        try
        {
            await CloudSyncService.DeleteCloudProfileAsync().ConfigureAwait(true);
            OnlineAccountService.Logout();
            RefreshOnlineState();
            MessageRequested?.Invoke(
                this,
                new SettingsMessageRequestedEventArgs(
                    "已删除云端档案",
                    "云端档案已删除，并已退出当前账户。",
                    "知道了"));
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke(
                this,
                new SettingsMessageRequestedEventArgs(
                    "删除失败",
                    "未能删除云端档案，因此没有退出账户。\n\n详细信息：" + ex.Message,
                    "知道了"));
        }
    }

    private void LogoutOnlineAccount()
    {
        if (!OnlineAccountService.IsLoggedIn)
        {
            MessageRequested?.Invoke(this, new SettingsMessageRequestedEventArgs("尚未登录", "当前没有可退出的 PCL N 在线账户。"));
            RefreshOnlineState();
            return;
        }

        OnlineAccountService.Logout();
        RefreshOnlineState();
        MessageRequested?.Invoke(this, new SettingsMessageRequestedEventArgs("已退出账户", "已退出当前 PCL N 在线账户。", "知道了"));
    }

    private void RefreshAvatar()
    {
        if (ImgAvatar is null)
            return;

        string? avatar = OnlineAccountService.AvatarUrl;
        if (string.IsNullOrWhiteSpace(avatar) || !File.Exists(avatar))
        {
            ImgAvatar.Source = null;
            return;
        }

        try
        {
            ImgAvatar.Source = new Bitmap(avatar);
        }
        catch
        {
            ImgAvatar.Source = null;
        }
    }
}
