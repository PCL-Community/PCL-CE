using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PCL.Core.App;
using PCL.Core.Utils;
using PCL.Core.App.Localization;
using PCL.Core.Utils.OS;

namespace PCL;

public partial class PageSetupUpdate : IPageView
{
    private readonly PageSetupUpdateViewModel _vm = new();
    private bool _refreshed;

    public VersionDataModel updateInfo;

    public PageSetupUpdate()
    {
        InitializeComponent();
        DataContext = _vm;
        Loaded += (_, _) => Refresh();
        Unloaded += PageSetupUpdate_Unloaded;
    }

    public PageSetupUpdateViewModel ViewModel => _vm;

    public void OnNavigatedTo(object? parameter) => Refresh();

    public void OnNavigatedFrom()
    {
    }

    private void PageSetupUpdate_Unloaded(object sender, RoutedEventArgs e) => _refreshed = false;

    private void Refresh()
    {
        if (_refreshed)
            return;
        _refreshed = true;
        Init();
    }

    private void Init()
    {
        ModAnimation.AniBeginControl();
        TextMirrorCDK.Password = Config.Update.MirrorChyanKey;

        ComboSystemUpdateChannel.SelectedIndex = (int)Config.Update.UpdateChannel;
        ComboSystemUpdateMode.SelectedIndex = (int)Config.Update.UpdateMode;

        _vm.Reload();
        ModAnimation.AniEndControl();
        CheckUpdate();
    }

    private async Task<UpdateStatus> IsLatestAsync()
    {
        try
        {
            // 修复：使用 dynamic 绕过命名空间重名导致的编译期类型冲突，
            // 或者你可以尝试替换为 PCL.Core.App.SemVer.Parse(ModBase.versionBaseName)
            if (await UpdateManager.remoteServer.IsLatestAsync(
                    UpdateManager.IsCurrentVersionBeta ? UpdateChannel.beta : UpdateChannel.stable,
                    SystemInfo.IsArm64System ? UpdateArch.arm64 : UpdateArch.x64,
                    SemVer.Parse(ModBase.versionBaseName),
                    ModBase.versionCode))
            {
                ModBase.Log("[Update] 已是最新版本");
                return UpdateStatus.Latest;
            }

            ModBase.Log("[Update] 有可用的新版本");
            return UpdateStatus.Available;
        }
        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                Lang.Text("Setup.Update.Error.NetworkFailed"),
                ModBase.LogLevel.Hint,
                userSummary: Lang.Text("Setup.Update.Error.NetworkFailed"));
            return UpdateStatus.Error;
        }
    }

    public async void CheckUpdate()
    {
        ModBase.Log("[Update] 开始检查更新");
        _vm.ShowUpdateCard = false;
        _vm.ShowCheckCard = true;
        _vm.CheckAgainEnabled = false;
        TextCurrentDesc.TrySetLocalizedValue("Text", "Setup.Update.Checking");
        switch (await IsLatestAsync())
        {
            case UpdateStatus.Available:
            {
                Exception checkUpdateEx = null;
                try
                {
                    updateInfo = UpdateManager.remoteServer.GetLatestVersion(
                        UpdateManager.IsCurrentVersionBeta
                            ? UpdateChannel.beta
                            : UpdateChannel.stable, SystemInfo.IsArm64System ? UpdateArch.arm64 : UpdateArch.x64);
                    _vm.UpdateNameText = "PCL CE " + VersionNameFormat(updateInfo.VersionName);
                    var summary = updateInfo.Changelog.Between("<summary>", "</summary>");
                    if (!updateInfo.Changelog.Contains("<summary>") || string.IsNullOrWhiteSpace(summary.Trim()))
                        TextChangelog.TrySetLocalizedValue("Text", "Setup.Update.Changelog.Empty");
                    else
                        TextChangelog.Text = summary;
                }
                catch (Exception ex)
                {
                    checkUpdateEx = ex;
                }

                _vm.CheckAgainEnabled = true;
                if (updateInfo is null)
                {
                    _vm.CurrentDescText = Lang.Text("Setup.Update.CheckFailed");
                    TextCurrentDesc.TrySetLocalizedValue("Text", "Setup.Update.CheckFailed");
                    if (checkUpdateEx is not null)
                        ModBase.Log(
                            checkUpdateEx,
                            "[Update] 检查更新失败",
                            ModBase.LogLevel.Msgbox,
                            userSummary: Lang.Text("Update.Check.Failed"));
                    else
                        ModBase.Log(
                            "[Update] 检查更新失败",
                            ModBase.LogLevel.Msgbox,
                            userSummary: Lang.Text("Update.Check.Failed"));
                    return;
                }

                if (UpdateManager.updateLoader is not null && UpdateManager.updateLoader.State == ModBase.LoadState.Loading)
                {
                    BtnUpdate_Timer();
                    BtnUpdate.IsEnabled = false;
                }
                else if (UpdateManager.isUpdateWaitingRestart)
                {
                    BtnUpdate.TrySetLocalizedValue("Text", "Setup.Update.RestartInstall");
                    BtnUpdate.IsEnabled = true;
                }
                else
                {
                    BtnUpdate.TrySetLocalizedValue("Text", "Setup.Update.Install");
                    BtnUpdate.IsEnabled = true;
                }

                _vm.ShowUpdateCard = true;
                _vm.ShowCheckCard = false;
                break;
            }
            case UpdateStatus.Latest:
            {
                _vm.ShowUpdateCard = false;
                _vm.ShowCheckCard = true;
                _vm.CheckAgainEnabled = true;
                _vm.CurrentDescText = Lang.Text("Setup.Update.Latest");
                TextCurrentDesc.TrySetLocalizedValue("Text", "Setup.Update.Latest");
                break;
            }
            case UpdateStatus.Error:
            {
                _vm.ShowUpdateCard = false;
                _vm.ShowCheckCard = true;
                _vm.CheckAgainEnabled = true;
                _vm.CurrentDescText = Lang.Text("Setup.Update.CheckFailed");
                TextCurrentDesc.TrySetLocalizedValue("Text", "Setup.Update.CheckFailed");
                break;
            }
        }
    }

    public void BtnUpdate_Timer()
    {
        while (UpdateManager.updateLoader is not null && UpdateManager.updateLoader.State == ModBase.LoadState.Loading)
        {
            ModBase.RunInUi(() => BtnUpdate.Text = Lang.Number(UpdateManager.updateLoader.Progress, "P2"));
            Thread.Sleep(200);
        }
    }

    private void BtnUpdate_Click(object sender, MouseButtonEventArgs e)
    {
        if (UpdateManager.isUpdateWaitingRestart) UpdateManager.UpdateRestart(true);
        // 开始更新流程
        UpdateManager.UpdateStart(UpdateEnums.UpdateType.UpdateNow);
    }

    private async void BtnChangelogDetail_Click(object sender, EventArgs e)
    {
        var ctl = sender as Control;
        if (ctl is not null) ctl.IsEnabled = false;
        try
        {
            if (updateInfo is null)
                await ModMain.MyMsgBoxAsync(Lang.Text("Setup.Update.Changelog.Unavailable"), Lang.Text("Setup.Update.Changelog.Title"));
            else
                ModMain.MyMsgBoxMarkdown(updateInfo.Changelog, Lang.Text("Setup.Update.Changelog.Title"));
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "查看更新日志失败", ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Setup.Error.OperationFailed"));
        }
        finally
        {
            if (ctl is not null) ctl.IsEnabled = true;
        }
    }

    private void ComboSystemUpdateMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ModAnimation.AniExitsAnyControl())
            Config.Update.UpdateMode = (LauncherAutoUpdateBehavior)ComboSystemUpdateMode.SelectedIndex;
    }

    private async void ComboSystemUpdateBranch_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var ctl = sender as Control;
        if (ctl is not null) ctl.IsEnabled = false;
        try
        {
            if (ModAnimation.AniExitsAnyControl())
                return;

            var isCancelled = false;
            switch (ComboSystemUpdateChannel.SelectedIndex)
            {
                case 0:
                {
                    break;
                }
                case 1:
                {
                    if (await ModMain.MyMsgBoxAsync(Lang.Text("Setup.Update.Channel.Beta.Warning.Message"),
                            Lang.Text("Setup.Update.Channel.Common.Warning.Title"),
                            Lang.Text("Setup.Update.Channel.Common.Warning.Confirm"),
                            Lang.Text("Common.Action.Cancel"), isWarn: true) == 2)
                        isCancelled = true;
                    else
                        CheckUpdate();
                    break;
                }
                case 2:
                {
                    if (await ModMain.MyMsgBoxAsync(Lang.Text("Setup.Update.Channel.Dev.Warning.Message"),
                            Lang.Text("Setup.Update.Channel.Common.Warning.Title"),
                            Lang.Text("Setup.Update.Channel.Common.Warning.Confirm"),
                            Lang.Text("Common.Action.Cancel"), isWarn: true) == 2)
                    {
                        isCancelled = true;
                        break;
                    }

                    var confirmText = Lang.Text("Setup.Update.Channel.Dev.FinalConfirm.ExpectedInput");
                    var ret = await ModMain.MyMsgBoxInputAsync(
                        Lang.Text("Setup.Update.Channel.Dev.FinalConfirm.Title"),
                        Lang.Text("Setup.Update.Channel.Dev.FinalConfirm.Message", confirmText),
                        button1: Lang.Text("Setup.Update.Channel.Dev.FinalConfirm.Submit"),
                        button2: Lang.Text("Common.Action.Cancel"), isWarn: true);

                    if (ret == confirmText)
                    {
                        CheckUpdate();
                    }
                    else
                    {
                        HintService.Hint(Lang.Text("Setup.Update.Channel.Dev.FinalConfirm.WrongInput"));
                        isCancelled = true;
                    }
                    break;
                }
            }

            if (isCancelled)
            {
                ModAnimation.AniBeginControl();
                ComboSystemUpdateChannel.SelectedItem = e.RemovedItems[0];
                ModAnimation.AniEndControl();
            }
            else
            {
                Config.Update.UpdateChannel = (Core.App.UpdateChannel)ComboSystemUpdateChannel.SelectedIndex;
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "切换更新通道失败", ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Setup.Error.OperationFailed"));
        }
        finally
        {
            if (ctl is not null) ctl.IsEnabled = true;
        }
    }

    private void TextMirrorCDK_PasswordChanged(object sender, EventArgs e)
    {
        Config.Update.MirrorChyanKey = TextMirrorCDK.Password;
    }

    private void BtnGetMirrorCDK_Click(object sender, MouseButtonEventArgs e)
    {
        ModBase.OpenWebsite("https://mirrorchyan.com/");
    }

    private void BtnChangelog_Click(object sender, MouseButtonEventArgs e)
    {
        ModBase.OpenWebsite("https://github.com/PCL-Community/PCL2-CE/releases/v" + ModBase.versionBaseName);
    }

    public string VersionNameFormat(string str) => PageSetupUpdateViewModel.VersionNameFormat(str);

    private void BtnCheckAgain_OnClick(object sender, MouseButtonEventArgs e)
    {
        CheckUpdate();
    }

    private enum UpdateStatus
    {
        Checking = 0,
        Available = 1,
        Error = 2,
        Latest = 3
    }
}
