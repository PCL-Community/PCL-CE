using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Dialogs;
using PCL.Core.Utils;
using PCL.Core.Utils.OS;
using PCL.UI.Controls;
using PCL.UI.Seams;

namespace PCL.Views.Pages.Setup;

/// <summary>
///     更新设置页：更新通道、自动更新行为、MirrorChyan CDK 与检查更新入口。
///     检查更新与下载走 <c>UpdateManager</c>；非 Windows 平台不提供安装按钮（更新包为 Windows 可执行文件）。
/// </summary>
public partial class PageSetupUpdate : MyPageRight, ISeamSetupUpdatePage
{
    private string? _changelog;
    private bool _checking;
    private VersionDataModel? _latestVersion;
    private bool _loaded;
    private bool _reloading;

    public PageSetupUpdate()
    {
        InitializeComponent();
        PanScroll = PanBack;

        ComboSystemUpdateMode.SelectionChanged += (_, _) => WriteMode();
        ComboSystemUpdateChannel.SelectionChanged += async (_, _) => await OnChannelChangedAsync();
        TextMirrorCDK.TextChanged += (_, _) =>
        {
            if (_reloading) return;
            Config.Update.MirrorChyanKey = TextMirrorCDK.Text ?? string.Empty;
        };
        BtnGetMirrorCDK.Click += (_, _) => Basics.OpenPath("https://mirrorchyan.com/");
        BtnCheckAgain.Click += async (_, _) => await CheckUpdateAsync();
        BtnChangelog.Click += (_, _) => OpenReleasesPage();
        BtnChangelogDetail.Click += async (_, _) => await ShowChangelogAsync();
        BtnUpdate.Click += (_, _) => UpdateManager.UpdateStart(UpdateEnums.UpdateType.UpdateNow);

        TextCurrentVersion.Text = "PCL CE " + FormatVersionName(LauncherCommon.VersionBaseName);
        TextCurrentDesc.Text = Lang.Text("Setup.Update.Checking");
        TextChangelog.Text = Lang.Text("Setup.Update.Changelog.Placeholder");
        // TODO(FIX-P3-14): WPF 更新通道含 Dev 项（默认 Collapsed）与 MirrorChyan
        // 专用下载流程（PageSetupUpdate.xaml.cs 未迁移），
        // 当前仅保留 Release/Beta 通道切换与 CDK 输入。
        BtnUpdate.IsVisible = OperatingSystem.IsWindows();

        SeamHosts.SetupUpdatePage = this;

        Loaded += async (_, _) =>
        {
            if (_loaded) return;
            _loaded = true;
            Init();
            await CheckUpdateAsync();
        };
    }

    public void SetUpdateButton(string text, bool enabled)
    {
        BtnUpdate.Text = text;
        BtnUpdate.IsEnabled = enabled;
    }

    public override void OnNavigatedTo(object? parameter)
    {
        PanBack.ScrollToHome();
        if (!_loaded) return;
        Reload();
    }

    public override void OnNavigatedFrom()
    {
    }

    private void Init()
    {
        Reload();
    }

    private void Reload()
    {
        _reloading = true;
        try
        {
            TextMirrorCDK.Text = Config.Update.MirrorChyanKey;
            ComboSystemUpdateChannel.SelectedIndex = Math.Clamp(
                (int)Config.Update.UpdateChannel,
                0,
                1);
            ComboSystemUpdateMode.SelectedIndex = (int)Config.Update.UpdateMode;
        }
        finally
        {
            _reloading = false;
        }
    }

    private void WriteMode()
    {
        if (_reloading || ComboSystemUpdateMode.SelectedIndex < 0) return;
        Config.Update.UpdateMode = (LauncherAutoUpdateBehavior)ComboSystemUpdateMode.SelectedIndex;
    }

    private async Task OnChannelChangedAsync()
    {
        if (_reloading || ComboSystemUpdateChannel.SelectedIndex < 0) return;
        var index = ComboSystemUpdateChannel.SelectedIndex;
        if (index == 1)
        {
            var result = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Setup.Update.Channel.Common.Warning.Title"),
                    Content = Lang.Text("Setup.Update.Channel.Beta.Warning.Message"),
                    Buttons = DialogButtons.Create(
                        "Setup.Update.Channel.Common.Warning.Confirm",
                        "Common.Action.Cancel"),
                    IsWarn = true
                });
            if (!result.IsConfirmed)
            {
                _reloading = true;
                ComboSystemUpdateChannel.SelectedIndex = Math.Clamp(
                    (int)Config.Update.UpdateChannel,
                    0,
                    1);
                _reloading = false;
                return;
            }
        }

        Config.Update.UpdateChannel = (Core.App.UpdateChannel)index;
        await CheckUpdateAsync();
    }

    private async Task CheckUpdateAsync()
    {
        if (_checking) return;
        _checking = true;
        CardCheck.IsVisible = true;
        CardUpdate.IsVisible = false;
        TextCurrentDesc.Text = Lang.Text("Setup.Update.Checking");
        BtnCheckAgain.IsEnabled = false;
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
                TextCurrentDesc.Text = Lang.Text("Setup.Update.Latest");
                return;
            }

            var version = await Task.Run(() =>
                UpdateManager.remoteServer.GetLatestVersion(channel, arch));
            _latestVersion = version;
            _changelog = version.Changelog;
            TextUpdateName.Text = "PCL CE " + FormatVersionName(version.VersionName);
            var summary = ExtractSummary(version.Changelog);
            TextChangelog.Text = string.IsNullOrWhiteSpace(summary)
                ? Lang.Text("Setup.Update.Changelog.Empty")
                : summary;
            TextUpdateDesc.Text = $"by PCL-Community · {version.Source}";
            CardCheck.IsVisible = false;
            CardUpdate.IsVisible = true;
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Setup.Update.Error.NetworkFailed"),
                LauncherLogLevel.Hint,
                userSummary: Lang.Text("Setup.Update.Error.NetworkFailed"));
            TextCurrentDesc.Text = Lang.Text("Setup.Update.CheckFailed");
        }
        finally
        {
            _checking = false;
            BtnCheckAgain.IsEnabled = true;
        }
    }

    private async Task ShowChangelogAsync()
    {
        var content = _latestVersion?.Changelog ?? _changelog;
        if (string.IsNullOrWhiteSpace(content))
        {
            await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Setup.Update.Changelog.Title"),
                    Content = Lang.Text("Setup.Update.Changelog.Unavailable"),
                    Buttons = DialogButtons.Ok
                });
            return;
        }

        await DialogService.Instance.ShowAsync(
            new MarkdownDialogRequest
            {
                Title = Lang.Text("Setup.Update.Changelog.Title"),
                Content = content,
                Buttons = DialogButtons.Ok
            });
    }

    private static void OpenReleasesPage()
    {
        Basics.OpenPath("https://github.com/PCL-Community/PCL2-CE/releases");
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

    public static string FormatVersionName(string value)
    {
        value = value.Replace("v", "");
        if (!value.Contains('-')) return value;
        var suffix = value[(value.LastIndexOf('-') + 1)..];
        var prefix = value[..value.LastIndexOf('-')];
        return $"{prefix} {suffix.Replace(".", " ").Replace("beta", "Beta").Replace("rc", "RC")}";
    }
}