using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App.Localization;
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

    [ObservableProperty] private string _launchingStageText = Lang.Text("Launch.Status.DownloadLibs");

    [ObservableProperty] private string _launchingTitleText = Lang.Text("Launch.Status.Title.Launching");

    [ObservableProperty] private string _launchingNameText = "Forge 1.12.2-15.8.0.1560";

    [ObservableProperty] private string _launchingMethodText = Lang.Text("Launch.Status.MicrosoftLogin");

    [ObservableProperty] private string _launchingProgressText = "69.28 %";

    [ObservableProperty] private string _launchingDownloadText = "5.2 M/s";

    [ObservableProperty] private string _launchingHintText = Lang.Text("Launch.Status.Trivia.Placeholder");

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
        LaunchingDownloadText = ModBase.GetString(speed) + "/s";
    }
}
