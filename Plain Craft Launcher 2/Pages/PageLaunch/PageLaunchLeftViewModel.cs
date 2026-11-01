using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>启动页左侧栏的视图模型。</summary>
public partial class PageLaunchLeftViewModel : ViewModelBase
{
    [ObservableProperty] private string _launchButtonText = Lang.Text("Launch.Home.Button.Loading");

    [ObservableProperty] private string _versionNameText = Lang.Text("Launch.Home.VersionList.Loading");

    [ObservableProperty] private bool _isInstanceButtonEnabled;

    [ObservableProperty] private Visibility _instanceButtonVisibility = Visibility.Visible;

    [ObservableProperty] private Visibility _moreButtonVisibility = Visibility.Collapsed;

    [ObservableProperty] private bool _isLaunchButtonEnabled;

    [ObservableProperty] private string _launchingStageText = Lang.Text("Launch.Status.DownloadLibs");

    [ObservableProperty] private string _launchingTitleText = Lang.Text("Launch.Status.Title.Launching");

    [ObservableProperty] private string _launchingNameText = "Forge 1.12.2-15.8.0.1560";

    [ObservableProperty] private string _launchingMethodText = Lang.Text("Launch.Status.MicrosoftLogin");

    [ObservableProperty] private string _launchingProgressText = "69.28 %";

    [ObservableProperty] private string _launchingDownloadText = "5.2 M/s";

    [ObservableProperty] private string _launchingHintText = Lang.Text("Launch.Status.Trivia.Placeholder");

    [ObservableProperty] private Visibility _launchingDownloadVisibility = Visibility.Collapsed;

    [ObservableProperty] private Visibility _launchingProgressVisibility = Visibility.Visible;

    [ObservableProperty] private Visibility _launchingHintVisibility = Visibility.Collapsed;

    public void ApplyLaunchingHint(bool showHint)
    {
        LaunchingHintText = showHint ? PageLaunchRight.GetRandomHint(true, true) : "";
    }

    public void ApplyLaunchingProgress(double progress)
    {
        LaunchingProgressText = Lang.Number(progress, "P2");
    }

    public void ApplyLaunchingDownload(long speed)
    {
        LaunchingDownloadText = Common.GetString(speed) + "/s";
    }

    // 实例选择按钮
    [RelayCommand]
    private void SelectInstance()
    {
        if (GameLaunch.mcLaunchLoader.State == Common.LoadState.Loading)
            return;
        Launcher.frmMain.PageChange(FormMain.PageType.InstanceSelect);
    }

    // 取消按钮
    [RelayCommand]
    private void CancelLaunch()
    {
        if (GameLaunch.mcLaunchLoaderReal is not null)
        {
            GameLaunch.mcLaunchLoaderReal.Abort();
            GameLaunch.McLaunchLog("已取消启动");
            try
            {
                if (GameLaunch.mcLaunchWatcher is not null)
                    GameLaunch.mcLaunchWatcher.Kill();
                else if (GameLaunch.mcLaunchProcess is not null)
                    if (!GameLaunch.mcLaunchProcess.HasExited)
                        GameLaunch.mcLaunchProcess.Kill();
            }
            catch (Exception ex)
            {
                Common.Log(
                    ex,
                    Lang.Text("Minecraft.Launch.Error.CancelProcess"),
                    Common.LogLevel.Hint,
                    userSummary: Lang.Text("Minecraft.Launch.Error.CancelProcess"));
            }
        }
    }

    // 实例设置按钮
    [RelayCommand]
    private void OpenInstanceSettings()
    {
        if (GameLaunch.mcLaunchLoader.State == Common.LoadState.Loading)
            return;
        InstanceList.McMcInstanceSelected.Load();
        PageInstanceLeft.McInstance = InstanceList.McMcInstanceSelected;
        if (File.Exists(InstanceList.McMcInstanceSelected.PathInstance + ".pclignore"))
        {
            HintService.Show(Hints.Error(Lang.Text("Launch.Home.Instance.InstallingCannotSetup")));
            return;
        }

        Launcher.frmMain.PageChange(FormMain.PageType.InstanceSetup);
    }
}
