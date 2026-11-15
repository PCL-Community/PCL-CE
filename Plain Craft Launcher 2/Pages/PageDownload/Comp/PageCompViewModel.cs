using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Core.App.Localization;
using PCL.Core.ViewModel;
using System.Collections.ObjectModel;

namespace PCL;

/// <summary>资源下载页组件卡片的视图模型。</summary>
public partial class PageCompViewModel : ViewModelBase
{
    /// <summary>当前页展示的资源条目。整体替换以只触发一次集合变更通知。</summary>
    [ObservableProperty] private ObservableCollection<MyCompItemViewModel> _projects = [];

    [ObservableProperty] private string _pageText = "1";

    [ObservableProperty] private bool _canGoFirst;

    [ObservableProperty] private double _firstOpacity = 0.2d;

    [ObservableProperty] private bool _canGoLeft;

    [ObservableProperty] private double _leftOpacity = 0.2d;

    [ObservableProperty] private bool _canGoRight = true;

    [ObservableProperty] private double _rightOpacity = 1d;

    [ObservableProperty] private bool _isPagerVisible = true;

    [ObservableProperty] private bool _isErrorVisible = true;

    [ObservableProperty] private string _errorMessage = Lang.Text("Download.Comp.Hint.ModrinthUnreachable");

    [ObservableProperty] private bool _isInstallModPackVisible;

    [ObservableProperty] private bool _supportCurseForge = true;

    [ObservableProperty] private bool _supportModrinth = true;

    [ObservableProperty] private bool _isLoaderLabelVisible = true;

    [ObservableProperty] private bool _isLoaderComboVisible = true;

    [ObservableProperty] private bool _isShaderLoaderComboVisible;

    [ObservableProperty] private string _loadingText = Lang.Text("Application.Control.Loading.Default");

    [RelayCommand]
    public void InstallModPack()
    {
        Modpack.ModpackInstall();
    }

    public void ApplyPagerState(int currentPage, Comp.CompProjectStorage storage)
    {
        PageText = Lang.Number(currentPage + 1, "N0");
        IsPagerVisible = storage.results.Count > PageComp.PageSize ||
                         storage.curseForgeOffset < storage.curseForgeTotal ||
                         storage.modrinthOffset < storage.modrinthTotal;
        CanGoFirst = currentPage > 1;
        FirstOpacity = currentPage > 1 ? 1d : 0.2d;
        CanGoLeft = currentPage > 0;
        LeftOpacity = currentPage > 0 ? 1d : 0.2d;
        CanGoRight = storage.results.Count > PageComp.PageSize * (currentPage + 1) ||
                     storage.curseForgeOffset < storage.curseForgeTotal ||
                     storage.modrinthOffset < storage.modrinthTotal;
        RightOpacity = CanGoRight ? 1d : 0.2d;
    }

    public void ApplyErrorState(Comp.CompProjectStorage storage)
    {
        IsErrorVisible = storage.errorMessage is not null;
        ErrorMessage = storage.errorMessage ?? string.Empty;
    }

    /// <summary>整体替换当前页条目。仅在数据到达时调用一次，避免逐项增删引起的多次布局传递。</summary>
    public void ReplaceProjects(IReadOnlyList<MyCompItemViewModel> projects)
    {
        Projects = new ObservableCollection<MyCompItemViewModel>(projects);
    }

    /// <summary>刷新所有条目的收藏图标。</summary>
    public void RefreshAllFavoriteStatus()
    {
        foreach (var project in Projects)
            project.RefreshFavoriteStatus();
    }
}
