using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Core.App.Localization;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>资源下载页组件卡片的视图模型。</summary>
public partial class PageCompViewModel : ViewModelBase
{
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

    [RelayCommand]
    public void InstallModPack()
    {
        Modpack.ModpackInstall();
    }

    public void ApplyPagerState(int currentPage, Comp.CompProjectStorage storage)
    {
        PageText = Lang.Number(currentPage + 1, "N0");
        IsPagerVisible = storage.results.Count > PageComp.pageSize ||
                         storage.curseForgeOffset < storage.curseForgeTotal ||
                         storage.modrinthOffset < storage.modrinthTotal;
        CanGoFirst = currentPage > 1;
        FirstOpacity = currentPage > 1 ? 1d : 0.2d;
        CanGoLeft = currentPage > 0;
        LeftOpacity = currentPage > 0 ? 1d : 0.2d;
        // 由于 WPF 的未知 bug，读取到的 IsEnabled 可能是错误的值（#3319）
        CanGoRight = storage.results.Count > PageComp.pageSize * (currentPage + 1) ||
                     storage.curseForgeOffset < storage.curseForgeTotal ||
                     storage.modrinthOffset < storage.modrinthTotal;
        RightOpacity = CanGoRight ? 1d : 0.2d;
    }

    public void ApplyErrorState(Comp.CompProjectStorage storage)
    {
        IsErrorVisible = storage.errorMessage is not null;
        ErrorMessage = storage.errorMessage ?? string.Empty;
    }
}
