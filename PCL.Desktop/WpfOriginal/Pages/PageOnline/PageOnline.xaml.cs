// Copyright (c) MUXUE1230. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using PCL.Core.App;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageOnline
{
    public PageOnline()
    {
        InitializeComponent();
        BtnWindowsLogin.Visibility = OperatingSystem.IsWindows() ? Visibility.Visible : Visibility.Collapsed;
        PageEnter += RefreshAccountCard;
    }

    private void RefreshAccountCard()
    {
        if (PCL.Online.OnlineAccountService.IsLoggedIn)
        {
            PanNotLoggedIn.Visibility = Visibility.Collapsed;
            PanLoggedIn.Visibility = Visibility.Visible;
            CardSync.Visibility = Visibility.Visible;
            LabUserName.Text = PCL.Online.OnlineAccountService.UserName ?? Lang.Text("Common.State.Unknown");
            LabAccountType.Text = PCL.Online.OnlineAccountService.OwnsMinecraft
                ? Lang.Text("Online.Account.OwnsMinecraft")
                : Lang.Text("Online.Account.DoesNotOwnMinecraft");
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
            SetCloudSyncUnavailable(!PCL.Online.CloudSyncService.IsAvailable);
        }
        else
        {
            PanNotLoggedIn.Visibility = Visibility.Visible;
            PanLoggedIn.Visibility = Visibility.Collapsed;
            CardSync.Visibility = Visibility.Collapsed;
            ImgAvatar.Source = null;
        }
    }

    public void SetCloudSyncRetrying()
    {
        BtnRetrySync.IsEnabled = false;
    }

    public void SetCloudSyncUnavailable(bool unavailable)
    {
        SyncBlurEffect.Radius = unavailable ? 6d : 0d;
        PanSyncContent.IsHitTestVisible = !unavailable;
        PanSyncUnavailable.Visibility = unavailable ? Visibility.Visible : Visibility.Collapsed;
        BtnRetrySync.IsEnabled = unavailable;
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
        StartMicrosoftLogin();
    }

    private async void BtnWindowsLogin_Click(object sender, ModBase.RouteEventArgs e)
    {
        if (!MicrosoftLoginPolicyGate.EnsureAccepted())
            return;

        SetLoginButtonsEnabled(false);
        try
        {
            var handle = new WindowInteropHelper(ModMain.frmMain).Handle;
            var result = await PCL.Online.Windows.WindowsOnlineAccountService.LoginWithWindowsAccountAsync(handle);
            if (!result.Success)
            {
                ModMain.Hint(Lang.Text("Online.Login.WindowsFallback", result.Message));
                SetLoginButtonsEnabled(true);
                StartMicrosoftLogin();
                return;
            }

            HandleLoginResult(result);
        }
        finally
        {
            if (!BtnLogin.IsEnabled)
                SetLoginButtonsEnabled(true);
        }
    }

    private void StartMicrosoftLogin()
    {
        if (!MicrosoftLoginPolicyGate.EnsureAccepted())
            return;

        SetLoginButtonsEnabled(false);
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
                HandleLoginResult(result);
                SetLoginButtonsEnabled(true);
            });
        }, "OnlineLogin");
    }

    private void HandleLoginResult(PCL.Online.OnlineLoginResult result)
    {
        if (result.Success && result.OwnsMinecraft && result.HasMinecraftProfile)
            ModProfile.AddProfileFromOnline(result);
        else if (result.Success)
            ModProfile.AddOfflineProfileFromOnline(result);
        if (result.Success)
            PCL.Online.CloudSyncService.TrySyncInBackground("login",
                PCL.Online.CloudSyncService.SyncMode.RemoteOverwrite);
        ModMain.Hint(result.Message,
            result.Success ? ModMain.HintType.Finish : ModMain.HintType.Critical);
        if (result.Success && result.OwnsMinecraft && result.MinecraftProfileMissing)
            MicrosoftLoginPolicyGate.ShowOnlineCreateProfilePromptOnce(result.MsId ?? result.DisplayName);
        RefreshAccountCard();
    }

    private void SetLoginButtonsEnabled(bool enabled)
    {
        BtnLogin.IsEnabled = enabled;
        BtnWindowsLogin.IsEnabled = enabled;
    }

    private void BtnLogout_Click(object sender, ModBase.RouteEventArgs e)
    {
        PCL.Online.OnlineAccountService.Logout();
        RefreshAccountCard();
        ModMain.Hint(Lang.Text("Online.Account.LoggedOut"), ModMain.HintType.Finish);
    }

    private void BtnDeleteCloudProfile_Click(object sender, ModBase.RouteEventArgs e)
    {
        if (!PCL.Online.OnlineAccountService.IsLoggedIn)
            return;

        if (ModMain.MyMsgBox(Lang.Text("Online.Account.DeleteCloudAndLogout.Warning"),
                Lang.Text("Online.Account.DeleteCloudAndLogout.Title"),
                Lang.Text("Common.Action.Continue"), Lang.Text("Common.Action.Cancel"),
                isWarn: true) != 1)
            return;

        var confirmation = ModMain.MyMsgBoxInput(
            Lang.Text("Online.Account.DeleteCloudAndLogout.Title"),
            Lang.Text("Online.Account.DeleteCloudAndLogout.ConfirmInput"),
            hintText: "DELETE",
            button1: Lang.Text("Online.Account.DeleteCloudAndLogout"),
            isWarn: true);
        if (!string.Equals(confirmation, "DELETE", StringComparison.Ordinal))
            return;

        BtnDeleteCloudProfile.IsEnabled = false;
        ModBase.RunInNewThread(() =>
        {
            try
            {
                PCL.Online.CloudSyncService.DeleteCloudProfileAsync().GetAwaiter().GetResult();
                PCL.Online.OnlineAccountService.Logout();
                ModBase.RunInUi(() =>
                {
                    RefreshAccountCard();
                    ModMain.Hint(Lang.Text("Online.Account.DeleteCloudAndLogout.Success"),
                        ModMain.HintType.Finish);
                });
            }
            catch (Exception ex)
            {
                ModBase.RunInUi(() =>
                    ModMain.Hint(Lang.Text("Online.Account.DeleteCloudAndLogout.Failed", ex.Message),
                        ModMain.HintType.Critical));
            }
            finally
            {
                ModBase.RunInUi(() => BtnDeleteCloudProfile.IsEnabled = true);
            }
        }, "DeleteCloudProfile");
    }

    private void BtnSyncDisable_Click(object sender, ModBase.RouteEventArgs e)
    {
        if (!States.Online.CloudSyncEnabled)
            return;

        States.Online.CloudSyncEnabled = false;
        ReloadSyncSettings();
        ModMain.Hint(Lang.Text("Online.CloudSync.Disabled"), ModMain.HintType.Finish);
    }

    private void BtnRetrySync_Click(object sender, ModBase.RouteEventArgs e)
    {
        BtnRetrySync.IsEnabled = false;
        if (!PCL.Online.CloudSyncService.RetryLastFailed())
            BtnRetrySync.IsEnabled = true;
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
            PCL.Online.CloudSyncService.TrySyncInBackground("settings",
                PCL.Online.CloudSyncService.SyncMode.LocalOverwrite);
    }
}
