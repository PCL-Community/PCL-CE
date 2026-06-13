// Copyright (c) MUXUE1230. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using PCL.Core.App;

namespace PCL;

public partial class PageOnline
{
    public PageOnline()
    {
        InitializeComponent();
        PageEnter += RefreshAccountCard;
    }

    private void RefreshAccountCard()
    {
        if (PCL.Online.OnlineAccountService.IsLoggedIn)
        {
            PanNotLoggedIn.Visibility = Visibility.Collapsed;
            PanLoggedIn.Visibility = Visibility.Visible;
            CardSync.Visibility = Visibility.Visible;
            LabUserName.Text = PCL.Online.OnlineAccountService.UserName ?? "未知";
            LabAccountType.Text = PCL.Online.OnlineAccountService.OwnsMinecraft
                ? "已拥有 Minecraft 正版" : "未拥有 Minecraft";
            var url = PCL.Online.OnlineAccountService.AvatarUrl;
            ImgAvatar.Source = null;
            if (!string.IsNullOrEmpty(url))
                try
                {
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.UriSource = new Uri(Path.GetFullPath(url), UriKind.Absolute);
                    image.EndInit();
                    image.Freeze();
                    ImgAvatar.Source = image;
                }
                catch
                {
                    ImgAvatar.Source = null;
                }

            ReloadSyncSettings();
        }
        else
        {
            PanNotLoggedIn.Visibility = Visibility.Visible;
            PanLoggedIn.Visibility = Visibility.Collapsed;
            CardSync.Visibility = Visibility.Collapsed;
            ImgAvatar.Source = null;
        }
    }

    private void ReloadSyncSettings()
    {
        ModAnimation.AniControlEnabled += 1;
        CheckCloudSyncEnabled.Checked = States.Online.CloudSyncEnabled;
        CheckSyncAccount.Checked = States.Online.CloudSyncAccount;
        CheckSyncFavorites.Checked = States.Online.CloudSyncFavorites;
        CheckSyncUiPreferences.Checked = States.Online.CloudSyncUiPreferences;
        CheckSyncHintPreferences.Checked = States.Online.CloudSyncHintPreferences;
        CheckSyncDownloadPreferences.Checked = States.Online.CloudSyncDownloadPreferences;
        CheckSyncLaunchPreferences.Checked = States.Online.CloudSyncLaunchPreferences;
        CheckSyncHomepagePreferences.Checked = States.Online.CloudSyncHomepagePreferences;
        CheckSyncMusicPreferences.Checked = States.Online.CloudSyncMusicPreferences;
        CheckSyncUpdatePreferences.Checked = States.Online.CloudSyncUpdatePreferences;
        CheckSyncCustomVariables.Checked = States.Online.CloudSyncCustomVariables;
        ModAnimation.AniControlEnabled -= 1;
        UpdateSyncSettingsState();
    }

    private void UpdateSyncSettingsState()
    {
        var enabled = States.Online.CloudSyncEnabled;
        PanSyncSections.IsEnabled = enabled;
        PanSyncSections.Opacity = enabled ? 1d : 0.55d;
        LabSyncDisabledHint.Visibility = enabled ? Visibility.Collapsed : Visibility.Visible;
        BtnSyncDisable.IsEnabled = enabled;
    }

    private void BtnLogin_Click(object sender, ModBase.RouteEventArgs e)
    {
        BtnLogin.IsEnabled = false;
        ModBase.RunInNewThread(() =>
        {
            var result = PCL.Online.OnlineAccountService.Login(prepareJson =>
            {
                var converter = new ModMain.MyMsgBoxConverter
                    { Content = prepareJson, ForceWait = true, Type = ModMain.MyMsgBoxType.Login };
                ModBase.RunInUi(() => ModMain.WaitingMyMsgBox.Add(converter));
                while (converter.Result is null) Thread.Sleep(100);
                return converter.Result;
            });

            ModBase.RunInUi(() =>
            {
                if (result.Success && result.OwnsMinecraft)
                    ModProfile.AddProfileFromOnline(result);
                if (result.Success)
                    PCL.Online.CloudSyncService.TrySyncInBackground("login");
                ModMain.Hint(result.Message,
                    result.Success ? ModMain.HintType.Finish : ModMain.HintType.Critical);
                RefreshAccountCard();
                BtnLogin.IsEnabled = true;
            });
        }, "OnlineLogin");
    }

    private void BtnLogout_Click(object sender, ModBase.RouteEventArgs e)
    {
        PCL.Online.OnlineAccountService.Logout();
        RefreshAccountCard();
        ModMain.Hint("已退出登录", ModMain.HintType.Finish);
    }

    private void BtnSyncDisable_Click(object sender, ModBase.RouteEventArgs e)
    {
        if (!States.Online.CloudSyncEnabled)
            return;

        States.Online.CloudSyncEnabled = false;
        ReloadSyncSettings();
        ModMain.Hint("已关闭 N Cloud 同步", ModMain.HintType.Finish);
    }

    private void SyncCheckBoxChange(object senderRaw, bool user)
    {
        if (ModAnimation.AniControlEnabled != 0)
            return;

        var sender = (MyCheckBox)senderRaw;
        var value = sender.Checked == true;
        switch (sender.Tag?.ToString())
        {
            case "CloudSyncEnabled":
                States.Online.CloudSyncEnabled = value;
                break;
            case "CloudSyncAccount":
                States.Online.CloudSyncAccount = value;
                break;
            case "CloudSyncFavorites":
                States.Online.CloudSyncFavorites = value;
                break;
            case "CloudSyncUiPreferences":
                States.Online.CloudSyncUiPreferences = value;
                break;
            case "CloudSyncHintPreferences":
                States.Online.CloudSyncHintPreferences = value;
                break;
            case "CloudSyncDownloadPreferences":
                States.Online.CloudSyncDownloadPreferences = value;
                break;
            case "CloudSyncLaunchPreferences":
                States.Online.CloudSyncLaunchPreferences = value;
                break;
            case "CloudSyncHomepagePreferences":
                States.Online.CloudSyncHomepagePreferences = value;
                break;
            case "CloudSyncMusicPreferences":
                States.Online.CloudSyncMusicPreferences = value;
                break;
            case "CloudSyncUpdatePreferences":
                States.Online.CloudSyncUpdatePreferences = value;
                break;
            case "CloudSyncCustomVariables":
                States.Online.CloudSyncCustomVariables = value;
                break;
        }

        UpdateSyncSettingsState();
        if (user && States.Online.CloudSyncEnabled)
            PCL.Online.CloudSyncService.TrySyncInBackground("settings");
    }
}
