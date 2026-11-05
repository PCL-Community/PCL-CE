using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Core.App;
using PCL.Core.ViewModel;
using PCL.Navigation;
using PCL.Views.Shell;

namespace PCL;

/// <summary>实例 Mod 不可用提示页的视图模型。</summary>
public partial class PageInstanceModDisabledViewModel : ViewModelBase
{
    [ObservableProperty] public partial bool IsDownloadVisible { get; set; } = true;

    /// <summary>按“功能隐藏”设置刷新下载按钮的可见性；返回状态是否发生变化。</summary>
    public bool Refresh()
    {
        var visible = HiddenFeatureState.ForceShow || !Config.Preference.Hide.PageDownload;
        if (IsDownloadVisible == visible)
            return false;

        IsDownloadVisible = visible;
        return true;
    }

    [RelayCommand]
    public static void GoDownload()
    {
        PageRouter.Navigate(PageType.Download, PageSubType.DownloadInstall);
    }

    [RelayCommand]
    public static void SelectInstance()
    {
        PageRouter.Navigate(PageType.Launch);
        PageRouter.Navigate(PageType.InstanceSelect);
    }
}