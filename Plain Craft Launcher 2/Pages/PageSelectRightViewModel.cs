using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>选择 Minecraft 实例页面右侧的视图模型。</summary>
public partial class PageSelectRightViewModel : ViewModelBase
{
    [ObservableProperty] private Visibility _backVisibility = Visibility.Visible;

    [ObservableProperty] private string _emptyContent = Lang.Text("Select.Instance.Empty.Message");

    [ObservableProperty] private Visibility _emptyDownloadVisibility = Visibility.Visible;

    [ObservableProperty] private Visibility _emptyPanelVisibility = Visibility.Visible;

    [ObservableProperty] private string _emptySearchContent = string.Empty;

    [ObservableProperty] private Visibility _emptySearchPanelVisibility = Visibility.Visible;

    [ObservableProperty] private string _emptySearchTitle = Lang.Text("Select.Instance.EmptySearch.Title");

    [ObservableProperty] private string _emptyTitle = Lang.Text("Select.Instance.Empty.Title");

    [ObservableProperty] private Visibility _searchBoxVisibility = Visibility.Visible;

    public bool MatchesSearch(McInstance instance, string searchText)
    {
        if (string.IsNullOrEmpty(searchText))
            return true;
        return instance.Name.ToLower().Contains(searchText) ||
               (instance.Desc is not null && instance.Desc.ToLower().Contains(searchText)) ||
               instance.GetDefaultDescription().Replace(",", "").ToLower().Trim().Contains(searchText);
    }

    public void RefreshEmptyDownloadVisibility(bool showHidden)
    {
        EmptyDownloadVisibility = (Config.Preference.Hide.PageDownload && !PageSetupUI.HiddenForceShow) || showHidden
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    [RelayCommand]
    private void Download()
    {
        Launcher.frmMain.PageChange(FormMain.PageType.Download, FormMain.PageSubType.DownloadInstall);
    }

    [RelayCommand]
    private void RetryLoad()
    {
        if (InstanceList.mcInstanceListLoader.State == LoadState.Failed)
            Loading.LoaderFolderRun(InstanceList.mcInstanceListLoader, GameFolders.mcFolderSelected,
                Loading.LoaderFolderRunType.ForceRun, 1, @"versions\");
    }
}
