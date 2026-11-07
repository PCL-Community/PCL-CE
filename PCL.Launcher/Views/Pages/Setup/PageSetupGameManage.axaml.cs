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
        DataContext = ViewModel;

        SliderDownloadThread.getHintText = new Func<object, object>(value => (int)value + 1);
        SliderDownloadFileConnection.getHintText =
            new Func<object, object>(value => (int)value + 1);
        SliderDownloadSpeed.getHintText = new Func<object, object>(FormatSpeedLimit);

        SliderDownloadThread.Change += (_, _) =>
            WriteIfReady(() => Config.Download.ThreadLimit = SliderDownloadThread.Value);
        SliderDownloadFileConnection.Change += (_, _) => WriteIfReady(() =>
            Config.Download.FileConnectionLimit = SliderDownloadFileConnection.Value);
        SliderDownloadSpeed.Change += (_, _) =>
            WriteIfReady(() => Config.Download.SpeedLimit = SliderDownloadSpeed.Value);
        SliderDownloadThread.PreviewChange += (_, e) =>
        {
            if (_reloading) return;
            if (SliderDownloadThread.Value < 64) return;
            _ = ViewModel.WarnLargeThreadOnceAsync();
        };

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

    public PageSetupGameManageViewModel ViewModel { get; } = new();

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

    protected override void RefreshLocalization()
    {
        if (_loaded) Reload();
    }

    public void Reload()
    {
        _reloading = true;
        try
        {
            ViewModel.Reload();
            SliderDownloadThread.Value = Config.Download.ThreadLimit;
            SliderDownloadFileConnection.Value = Config.Download.FileConnectionLimit;
            SliderDownloadSpeed.Value = Config.Download.SpeedLimit;
            CheckDownloadAutoSelectVersion.Checked = Config.Download.AutoSelectInstance;
            CheckFixAuthlib.Checked = Config.Download.FixAuthLib;

            CheckDownloadIgnoreQuilt.Checked = Config.Download.Comp.IgnoreQuilt;
            CheckDownloadAutoInstallDependencies.Checked =
                Config.Download.Comp.AutoInstallDependencies;
            CheckDownloadClipboard.Checked = Config.Download.Comp.ReadClipboard;

            CheckUpdateRelease.Checked = Config.Tool.ReleaseNotification;
            CheckUpdateSnapshot.Checked = Config.Tool.SnapshotNotification;
            CheckHelpLauncherLanguage.Checked = Config.Tool.AutoChangeLanguage;

            // WPF 仅按语言隐藏文件名格式与 Mod 管理风格两行；快速下载行始终显示。
            var show = ViewModel.FileFormatSettingsVisible;
            for (var row = 2; row <= 5; row++)
                PanCommunityRows.RowDefinitions[row].Height = new GridLength(
                    show ? row % 2 == 0 ? 28d : 7d : 0d);
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