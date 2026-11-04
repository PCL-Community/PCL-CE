using Avalonia.Controls;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Setup;

public partial class PageSetupGameManage : MyPageRight, ISetupResetPage
{
    private bool _loaded;
    private bool _reloading;

    public PageSetupGameManage()
    {
        InitializeComponent();
        PanScroll = PanBack;

        SliderDownloadThread.getHintText = new Func<object, object>(value => (int)value + 1);
        SliderDownloadFileConnection.getHintText =
            new Func<object, object>(value => (int)value + 1);
        SliderDownloadSpeed.getHintText = new Func<object, object>(FormatSpeedLimit);

        ComboDownloadSource.SelectionChanged += (_, _) =>
            WriteIfReady(() => Config.Download.FileSource = ComboDownloadSource.SelectedIndex);
        ComboDownloadVersion.SelectionChanged += (_, _) => WriteIfReady(() =>
            Config.Download.VersionListSource = ComboDownloadVersion.SelectedIndex);
        ComboDownloadHttpMode.SelectionChanged += (_, _) => WriteIfReady(() =>
            Config.Download.HttpMode = (DownloadHttpMode)ComboDownloadHttpMode.SelectedIndex);
        ComboDownloadMod.SelectionChanged += (_, _) => WriteIfReady(() =>
            Config.Download.Comp.CompSourceSolution = ComboDownloadMod.SelectedIndex);
        ComboDownloadTranslateV2.SelectionChanged += (_, _) => WriteIfReady(() =>
            Config.Download.Comp.NameFormatV2 = ComboDownloadTranslateV2.SelectedIndex);
        ComboModLocalNameStyle.SelectionChanged += (_, _) => WriteIfReady(() =>
            Config.Download.Comp.UiCompNameSolution = ComboModLocalNameStyle.SelectedIndex);
        ComboDownloadQuickBehavior.SelectionChanged += (_, _) => WriteIfReady(() =>
            Config.Download.Comp.QuickDownloadBehavior = ComboDownloadQuickBehavior.SelectedIndex);

        SliderDownloadThread.Change += (_, _) =>
            WriteIfReady(() => Config.Download.ThreadLimit = SliderDownloadThread.Value);
        SliderDownloadFileConnection.Change += (_, _) => WriteIfReady(() =>
            Config.Download.FileConnectionLimit = SliderDownloadFileConnection.Value);
        SliderDownloadSpeed.Change += (_, _) =>
            WriteIfReady(() => Config.Download.SpeedLimit = SliderDownloadSpeed.Value);

        CheckDownloadAutoSelectVersion.Change += (_, _) => WriteIfReady(() =>
            Config.Download.AutoSelectInstance = CheckDownloadAutoSelectVersion.Checked == true);
        CheckFixAuthlib.Change += (_, _) =>
            WriteIfReady(() => Config.Download.FixAuthLib = CheckFixAuthlib.Checked == true);
        CheckDownloadIgnoreQuilt.Change += (_, _) => WriteIfReady(() =>
            Config.Download.Comp.IgnoreQuilt = CheckDownloadIgnoreQuilt.Checked == true);
        CheckDownloadAutoInstallDependencies.Change += (_, _) => WriteIfReady(() =>
            Config.Download.Comp.AutoInstallDependencies =
                CheckDownloadAutoInstallDependencies.Checked == true);
        CheckDownloadClipboard.Change += (_, _) => WriteIfReady(() =>
            Config.Download.Comp.ReadClipboard = CheckDownloadClipboard.Checked == true);
        CheckUpdateRelease.Change += (_, _) => WriteIfReady(() =>
            Config.Tool.ReleaseNotification = CheckUpdateRelease.Checked == true);
        CheckUpdateSnapshot.Change += (_, _) => WriteIfReady(() =>
            Config.Tool.SnapshotNotification = CheckUpdateSnapshot.Checked == true);
        CheckHelpLauncherLanguage.Change += (_, _) => WriteIfReady(() =>
            Config.Tool.AutoChangeLanguage = CheckHelpLauncherLanguage.Checked == true);

        Loaded += (_, _) =>
        {
            if (_loaded) return;
            _loaded = true;
            Reload();
        };
    }

    public void Reset()
    {
        try
        {
            Config.Download.Reset();
            Config.Tool.Reset();
            HintService.Show(Hints.Success(Lang.Text("Setup.GameManage.Initialized"), false));
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Setup.GameManage.Error.InitFailed"),
                LauncherLogLevel.Msgbox,
                userSummary: Lang.Text("Setup.GameManage.Error.InitFailed"));
        }

        Reload();
    }

    public override void OnNavigatedTo(object? parameter)
    {
        PanBack.ScrollToHome();
        if (_loaded) Reload();
    }

    public override void OnNavigatedFrom()
    {
    }

    public void Reload()
    {
        _reloading = true;
        try
        {
            ComboDownloadSource.SelectedIndex = Math.Clamp(Config.Download.FileSource, 0, 2);
            ComboDownloadVersion.SelectedIndex = Math.Clamp(
                Config.Download.VersionListSource,
                0,
                2);
            ComboDownloadHttpMode.SelectedIndex = Math.Clamp((int)Config.Download.HttpMode, 0, 2);
            SliderDownloadThread.Value = Config.Download.ThreadLimit;
            SliderDownloadFileConnection.Value = Config.Download.FileConnectionLimit;
            SliderDownloadSpeed.Value = Config.Download.SpeedLimit;
            CheckDownloadAutoSelectVersion.Checked = Config.Download.AutoSelectInstance;
            CheckFixAuthlib.Checked = Config.Download.FixAuthLib;

            ComboDownloadMod.SelectedIndex = Math.Clamp(
                Config.Download.Comp.CompSourceSolution,
                0,
                2);
            ComboDownloadTranslateV2.SelectedIndex = Math.Clamp(
                Config.Download.Comp.NameFormatV2,
                0,
                4);
            ComboModLocalNameStyle.SelectedIndex = Math.Clamp(
                Config.Download.Comp.UiCompNameSolution,
                0,
                1);
            ComboDownloadQuickBehavior.SelectedIndex = Math.Clamp(
                Config.Download.Comp.QuickDownloadBehavior,
                0,
                3);
            CheckDownloadIgnoreQuilt.Checked = Config.Download.Comp.IgnoreQuilt;
            CheckDownloadAutoInstallDependencies.Checked =
                Config.Download.Comp.AutoInstallDependencies;
            CheckDownloadClipboard.Checked = Config.Download.Comp.ReadClipboard;

            CheckUpdateRelease.Checked = Config.Tool.ReleaseNotification;
            CheckUpdateSnapshot.Checked = Config.Tool.SnapshotNotification;
            CheckHelpLauncherLanguage.Checked = Config.Tool.AutoChangeLanguage;

            var isChineseMainland = Lang.IsChineseMainland;
            PanFilenameFormat.IsVisible = isChineseMainland;
            PanModManageStyle.IsVisible = isChineseMainland;
            PanQuickDownload.IsVisible = isChineseMainland;
            for (var row = 2; row <= 7; row++)
                PanCommunityRows.RowDefinitions[row].Height = new GridLength(
                    isChineseMainland ? row % 2 == 0 ? 28d : 7d : 0d);
        }
        finally
        {
            _reloading = false;
        }
    }

    private void WriteIfReady(Action action)
    {
        if (_reloading) return;
        try
        {
            action();
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "写入下载设置失败");
        }
    }

    private static object FormatSpeedLimit(object value)
    {
        var tick = Convert.ToInt32(value);
        return tick switch
        {
            <= 14 => Lang.Number((tick + 1) * 0.1d, "N1") + " MiB/s",
            <= 31 => Lang.Number((tick - 11) * 0.5d, "N1") + " MiB/s",
            <= 41 => Lang.Number(tick - 21, "N0") + " MiB/s",
            _ => Lang.Text("Setup.GameManage.Download.Unlimited")
        };
    }
}