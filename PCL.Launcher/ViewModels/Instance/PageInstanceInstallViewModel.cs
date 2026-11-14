using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App.Localization;
using PCL.Core.ViewModel;
using PCL.Views.Pages.Download;

namespace PCL;

/// <summary>实例安装/修改页的视图模型。</summary>
public partial class PageInstanceInstallViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string ActionText { get; set; } =
        Lang.Text("Instance.Install.Action.StartModify");

    [ObservableProperty] public partial bool CanStartInstall { get; set; }

    [ObservableProperty] public partial string CurrentInfo { get; set; } = "";

    [ObservableProperty] public partial bool HasWarning { get; set; }

    [ObservableProperty] public partial string InstanceName { get; set; } = "";

    [ObservableProperty] public partial bool IsInstalling { get; set; }

    [ObservableProperty] public partial bool IsProgressVisible { get; set; }

    [ObservableProperty] public partial bool IsSelecting { get; set; }

    [ObservableProperty] public partial string ProgressText { get; set; } = "";

    [ObservableProperty] public partial string SelectedInfo { get; set; } = "";

    [ObservableProperty]
    public partial string VersionIcon { get; set; } =
        $"{LauncherPaths.ImageRoot}Icons/NoIcon.png";

    [ObservableProperty] public partial string VersionName { get; set; } = "";

    [ObservableProperty] public partial string? VersionUrl { get; set; }

    [ObservableProperty] public partial string WarningText { get; set; } = "";

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