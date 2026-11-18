using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App.Localization;
using PCL.Core.ViewModel;
using PCL.Views.Pages.Download;

namespace PCL;

/// <summary>下载安装向导页的视图模型。</summary>
public partial class PageDownloadInstallViewModel : ViewModelBase
{
    [ObservableProperty] public partial bool CanStartInstall { get; set; }

    [ObservableProperty] public partial bool HasNoFolder { get; set; }

    [ObservableProperty] public partial string InstanceName { get; set; } = "";

    [ObservableProperty] public partial bool IsConfigStageVisible { get; set; }

    [LocalizedProperty("Download.Install.StartDownload")]
    public partial string StartButtonText { get; }

    [ObservableProperty]
    public partial string VersionIcon { get; set; } = $"{LauncherPaths.ImageRoot}Icons/NoIcon.png";

    [ObservableProperty] public partial string VersionName { get; set; } = "";

    [ObservableProperty] public partial string? VersionUrl { get; set; }

    internal InstallLoaderSelection Selection { get; } = new();
}