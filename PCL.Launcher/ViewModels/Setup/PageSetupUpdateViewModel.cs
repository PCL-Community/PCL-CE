using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.Core.Utils;
using PCL.Core.Utils.OS;
using PCL.Core.ViewModel;
using PCL.Text;

namespace PCL;

/// <summary>设置页面中「更新」页的视图模型。</summary>
public partial class PageSetupUpdateViewModel : ViewModelBase
{
    private bool _checking;

    private object?[] _descArgs = [];
    private string _descKey = "Setup.Update.Checking";

    private VersionDataModel? _latestVersion;

    [ObservableProperty] public partial bool ChangelogDetailEnabled { get; set; } = true;

    [ObservableProperty] public partial string ChangelogText { get; set; } = string.Empty;

    [ObservableProperty] public partial bool ChannelEnabled { get; set; } = true;

    [ObservableProperty] public partial bool CheckAgainEnabled { get; set; } = true;

    [ObservableProperty] public partial string CurrentDescText { get; set; } = string.Empty;

    [ObservableProperty] public partial string CurrentVersionText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MirrorChyanKey { get; set; } = Config.Update.MirrorChyanKey;

    [ObservableProperty] public partial bool ShowCheckCard { get; set; }

    [ObservableProperty] public partial bool ShowUpdateCard { get; set; }

    [ObservableProperty] public partial bool UpdateButtonEnabled { get; set; } = true;

    [ObservableProperty]
    public partial string UpdateButtonText { get; set; } = Lang.Text("Setup.Update.Install");

    [ObservableProperty]
    public partial int UpdateModeIndex { get; set; } = Math.Clamp(
        (int)Config.Update.UpdateMode,
        0,
        3);

    [ObservableProperty] public partial string UpdateNameText { get; set; } = string.Empty;

    public string? LatestChangelog => _latestVersion?.Changelog;

    /// <summary>Flatpak 包由软件中心更新，不能展示假的安装入口。</summary>
    public static bool IsFlatpak =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FLATPAK_ID"));

    private static string InstallButtonText =>
        IsFlatpak
            ? Lang.Text("Setup.Update.Flatpak")
            : UpdateManager.SupportsInAppUpdate
                ? Lang.Text("Setup.Update.Install")
                : Lang.Text("Setup.Update.OpenDownload");

    partial void OnMirrorChyanKeyChanged(string value)
    {
        Config.Update.MirrorChyanKey = value ?? string.Empty;
    }

    partial void OnUpdateModeIndexChanged(int value)
    {
        if (value >= 0) Config.Update.UpdateMode = (LauncherAutoUpdateBehavior)value;
    }

    public void Reload()
    {
        CurrentVersionText = "PCL CE " + VersionNameFormat(LauncherCommon.VersionBaseName);
        if (!ShowUpdateCard)
            ChangelogText = Lang.Text("Setup.Update.Changelog.Placeholder");
        MirrorChyanKey = Config.Update.MirrorChyanKey;
        UpdateModeIndex = Math.Clamp((int)Config.Update.UpdateMode, 0, 3);
        UpdateButtonText = InstallButtonText;
        UpdateButtonEnabled = !IsFlatpak;
    }

    public async Task CheckUpdateAsync()
    {
        if (_checking) return;
        _checking = true;
        ShowUpdateCard = false;
        ShowCheckCard = true;
        SetStatusText("Setup.Update.Checking");
        CheckAgainEnabled = false;
        try
        {
            var channel = UpdateManager.IsCurrentVersionBeta
                ? UpdateChannel.Beta
                : UpdateChannel.Stable;
            var arch = SystemInfo.IsArm64System ? UpdateArch.Arm64 : UpdateArch.X64;
            var isLatest = await UpdateManager.remoteServer.IsLatestAsync(
                channel,
                arch,
                SemVer.Parse(LauncherCommon.VersionBaseName),
                LauncherCommon.VersionCode);
            if (isLatest)
            {
                SetStatusText("Setup.Update.Latest");
                return;
            }

            var version = await Task.Run(() =>
                UpdateManager.remoteServer.GetLatestVersion(channel, arch));
            _latestVersion = version;
            UpdateNameText = "PCL CE " + VersionNameFormat(version.VersionName);
            var summary = ExtractSummary(version.Changelog);
            ChangelogText = string.IsNullOrWhiteSpace(summary)
                ? Lang.Text("Setup.Update.Changelog.Empty")
                : summary;

            if (UpdateManager.updateLoader is { State: LoadState.Loading })
            {
                UpdateButtonEnabled = false;
                _ = MonitorAndRestoreAsync();
            }
            else if (UpdateManager.isUpdateWaitingRestart)
            {
                UpdateButtonText = Lang.Text("Setup.Update.RestartInstall");
                UpdateButtonEnabled = !IsFlatpak;
            }
            else
            {
                UpdateButtonText = InstallButtonText;
                UpdateButtonEnabled = !IsFlatpak;
            }

            ShowUpdateCard = true;
            ShowCheckCard = false;
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Setup.Update.Error.NetworkFailed"),
                LauncherLogLevel.Hint,
                userSummary: Lang.Text("Setup.Update.Error.NetworkFailed"));
            SetStatusText("Setup.Update.CheckFailed");
        }
        finally
        {
            _checking = false;
            CheckAgainEnabled = true;
        }
    }

    /// <summary>点击安装：等待重启时直接重启安装，否则启动更新并轮询下载进度。</summary>
    public async Task StartUpdateAsync()
    {
        if (IsFlatpak)
            return;

        if (!UpdatePolicies.Current.SupportsInAppUpdate)
        {
            OpenReleasesPage();
            return;
        }

        if (UpdateManager.isUpdateWaitingRestart)
        {
            UpdateManager.UpdateRestart(true);
            return;
        }

        UpdateButtonEnabled = false;
        UpdateManager.UpdateStart(UpdateEnums.UpdateType.UpdateNow);
        await MonitorAndRestoreAsync();
    }

    private async Task MonitorAndRestoreAsync()
    {
        await MonitorUpdateProgressAsync();
        UpdateButtonText = UpdateManager.isUpdateWaitingRestart
            ? Lang.Text("Setup.Update.RestartInstall")
            : InstallButtonText;
        UpdateButtonEnabled = !IsFlatpak;
    }

    /// <summary>页面重新进入时同步更新按钮状态，若下载仍在进行则恢复进度显示。</summary>
    public async Task SyncUpdateButtonStateAsync()
    {
        if (UpdateManager.updateLoader is { State: LoadState.Loading })
        {
            UpdateButtonEnabled = false;
            UpdateButtonText = InstallButtonText;
            await MonitorAndRestoreAsync();
        }
        else if (UpdateManager.isUpdateWaitingRestart)
        {
            UpdateButtonText = Lang.Text("Setup.Update.RestartInstall");
            UpdateButtonEnabled = !IsFlatpak;
        }
        else
        {
            UpdateButtonText = InstallButtonText;
            UpdateButtonEnabled = !IsFlatpak;
        }
    }

    private async Task MonitorUpdateProgressAsync()
    {
        // UpdateStart 在后台线程准备任务，先等待 updateLoader 出现，避免错过进度。
        for (var i = 0; i < 150; i++)
        {
            if (UpdateManager.updateLoader is not null || UpdateManager.isUpdateWaitingRestart)
                break;
            await Task.Delay(200);
        }

        while (UpdateManager.updateLoader is { State: LoadState.Loading } loader)
        {
            var progress = loader.Progress;
            LauncherCommon.RunInUi(() => UpdateButtonText = Lang.Number(progress, "P2"));
            await Task.Delay(200);
        }
    }

    public async Task ShowChangelogAsync()
    {
        ChangelogDetailEnabled = false;
        try
        {
            var content = _latestVersion?.Changelog;
            if (string.IsNullOrWhiteSpace(content))
            {
                await Dialog.ShowAsync(
                    new TextDialogRequest
                    {
                        Title = Lang.Text("Setup.Update.Changelog.Title"),
                        Content = Lang.Text("Setup.Update.Changelog.Unavailable"),
                        Buttons = DialogButtons.Ok
                    });
                return;
            }

            await Dialog.ShowAsync(
                new MarkdownDialogRequest
                {
                    Title = Lang.Text("Setup.Update.Changelog.Title"),
                    Content = content,
                    Buttons = DialogButtons.Ok
                });
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "查看更新日志失败",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Setup.Error.OperationFailed"));
        }
        finally
        {
            ChangelogDetailEnabled = true;
        }
    }

    public static void OpenReleasesPage()
    {
        Basics.OpenPath(
            "https://github.com/PCL-Community/PCL2-CE/releases/v"
            + LauncherCommon.VersionBaseName);
    }

    /// <summary>切换更新通道；返回 false 表示用户取消，调用方需要把下拉恢复原值。</summary>
    public async Task<bool> ChangeChannelAsync(int index)
    {
        ChannelEnabled = false;
        try
        {
            switch (index)
            {
                case 1:
                {
                    var result = await Dialog.ShowAsync(
                        new TextDialogRequest
                        {
                            Title = Lang.Text("Setup.Update.Channel.Common.Warning.Title"),
                            Content = Lang.Text("Setup.Update.Channel.Beta.Warning.Message"),
                            Buttons = DialogButtons.From(
                                DialogButton.Create(
                                    Lang.Text("Setup.Update.Channel.Common.Warning.Confirm")),
                                DialogButton.Create(Lang.Text("Common.Action.Cancel"))),
                            IsWarn = true
                        });
                    if (result.Button != 1) return false;
                    break;
                }
                case 2:
                {
                    var warn = await Dialog.ShowAsync(
                        new TextDialogRequest
                        {
                            Title = Lang.Text("Setup.Update.Channel.Common.Warning.Title"),
                            Content = Lang.Text("Setup.Update.Channel.Dev.Warning.Message"),
                            Buttons = DialogButtons.From(
                                DialogButton.Create(
                                    Lang.Text("Setup.Update.Channel.Common.Warning.Confirm")),
                                DialogButton.Create(Lang.Text("Common.Action.Cancel"))),
                            IsWarn = true
                        });
                    if (warn.Button != 1) return false;

                    var confirmText = Lang.Text(
                        "Setup.Update.Channel.Dev.FinalConfirm.ExpectedInput");
                    var input = await Dialog.ShowAsync(
                        new InputDialogRequest
                        {
                            Title = Lang.Text("Setup.Update.Channel.Dev.FinalConfirm.Title"),
                            Content = Lang.Text(
                                "Setup.Update.Channel.Dev.FinalConfirm.Message",
                                confirmText),
                            Buttons = DialogButtons.From(
                                DialogButton.Create(
                                    Lang.Text("Setup.Update.Channel.Dev.FinalConfirm.Submit")),
                                DialogButton.Create(Lang.Text("Common.Action.Cancel"))),
                            IsWarn = true,
                            HighLight = false
                        });
                    if (!input.IsConfirmed || input.InputText != confirmText)
                    {
                        if (input.IsConfirmed)
                            HintService.Show(
                                Hints.Info(
                                    Lang.Text("Setup.Update.Channel.Dev.FinalConfirm.WrongInput")));
                        return false;
                    }

                    break;
                }
            }

            Config.Update.UpdateChannel = (Core.App.UpdateChannel)index;
            return true;
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "切换更新通道失败",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Setup.Error.OperationFailed"));
            return false;
        }
        finally
        {
            ChannelEnabled = true;
        }
    }

    private void SetStatusText(string key, params object?[] args)
    {
        _descKey = key;
        _descArgs = args;
        CurrentDescText = Lang.Text(key, args);
    }

    public void RefreshStatusText()
    {
        CurrentDescText = Lang.Text(_descKey, _descArgs);
    }

    public static string VersionNameFormat(string str)
    {
        str = str.Replace("v", "");
        if (!str.Contains('-'))
            return str;
        var add = str.AfterLast("-");
        str = str.BeforeLast("-");
        return $"{str} {add.Replace(".", " ").Replace("beta", "Beta").Replace("rc", "RC")}";
    }

    private static string ExtractSummary(string? changelog)
    {
        if (string.IsNullOrEmpty(changelog)) return string.Empty;
        const string startTag = "<summary>";
        const string endTag = "</summary>";
        var start = changelog.IndexOf(startTag, StringComparison.OrdinalIgnoreCase);
        if (start < 0) return string.Empty;
        start += startTag.Length;
        var end = changelog.IndexOf(endTag, start, StringComparison.OrdinalIgnoreCase);
        return end < 0 ? string.Empty : changelog[start..end].Trim();
    }
}