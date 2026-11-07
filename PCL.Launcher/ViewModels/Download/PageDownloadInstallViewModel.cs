using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App.Localization;
using PCL.Core.ViewModel;
using PCL.Views.Pages.Download;

namespace PCL;

/// <summary>下载安装向导页的视图模型。</summary>
public partial class PageDownloadInstallViewModel : ViewModelBase
{
    [ObservableProperty] public partial bool CanStartInstall { get; set; }

    [ObservableProperty] public partial bool HasLoaderWarning { get; set; }

    [ObservableProperty] public partial bool HasNoFolder { get; set; }

    [ObservableProperty] public partial bool ShowHintFabricApi { get; set; }

    [ObservableProperty] public partial bool ShowHintLegacyFabricApi { get; set; }

    [ObservableProperty] public partial bool ShowHintLegacyOptiFabric { get; set; }

    [ObservableProperty] public partial bool ShowHintModOptiFine { get; set; }

    [ObservableProperty] public partial bool ShowHintOptiFabric { get; set; }

    [ObservableProperty] public partial bool ShowHintOptiFabricOld { get; set; }

    [ObservableProperty] public partial string InstanceName { get; set; } = "";

    [ObservableProperty] public partial bool IsConfigStageVisible { get; set; }

    [ObservableProperty] public partial bool IsInstalling { get; set; }

    [ObservableProperty] public partial bool IsProgressVisible { get; set; }

    [ObservableProperty] public partial string LoaderWarning { get; set; } = "";

    [ObservableProperty] public partial string ProgressText { get; set; } = "";

    [ObservableProperty]
    public partial string StartButtonText { get; set; } =
        Lang.Text("Download.Install.StartDownload");

    [ObservableProperty]
    public partial string VersionIcon { get; set; } =
        $"{LauncherPaths.ImageRoot}Icons/NoIcon.png";

    [ObservableProperty] public partial string VersionName { get; set; } = "";

    [ObservableProperty] public partial string? VersionUrl { get; set; }

    internal InstallLoaderSelection Selection { get; } = new();

    public void ApplySelection(McInstallRequest request)
    {
        Selection.ApplyTo(request);
    }

    public string GetLoaderSuffix()
    {
        return Selection.GetSuffix();
    }

    public string GetSelectionInfo()
    {
        return Selection.GetInfo(VersionName);
    }
}