using PCL.UI.Controls;

namespace PCL.Views.Pages.Instance;

/// <summary>实例不可安装 Mod 时的提示页：引导用户下载可安装 Mod 的实例或重新选择实例。</summary>
public partial class PageInstanceModDisabled : MyPageRight, IPageView
{
    public PageInstanceModDisabled()
    {
        InitializeComponent();
        DataContext = ViewModel;
        BtnDownload.Click += (_, _) => PageInstanceModDisabledViewModel.GoDownload();
        BtnVersion.Click += (_, _) => PageInstanceModDisabledViewModel.SelectInstance();
    }

    public PageInstanceModDisabledViewModel ViewModel { get; } = new();

    public override void OnNavigatedTo(object? parameter)
    {
        RefreshModDisabled();
    }

    public override void OnNavigatedFrom()
    {
    }

    public void RefreshModDisabled()
    {
        ViewModel.Refresh();
    }
}