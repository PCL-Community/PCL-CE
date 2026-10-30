using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App.Localization;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>游戏联机页的视图模型，承载联机状态对应的显示文本与可见性。</summary>
public partial class PageToolsGameLinkViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _connectTypeText = Lang.Text("Tools.GameLink.Finish.Connecting");

    [ObservableProperty]
    private string _connectUserNameText = "UserName";

    [ObservableProperty]
    private string _connectUserTypeText = "UserType";

    [ObservableProperty]
    private Visibility _finishCopyIpVisibility = Visibility.Visible;

    [ObservableProperty]
    private string _finishExitText = Lang.Text("Tools.GameLink.Finish.Exit");

    [ObservableProperty]
    private string _finishIdText = "";

    [ObservableProperty]
    private string _finishPingText = "-ms";

    [ObservableProperty]
    private Visibility _finishPingVisibility = Visibility.Collapsed;

    [ObservableProperty]
    private string _finishQualityText = Lang.Text("Tools.GameLink.Finish.Checking");

    [ObservableProperty]
    private string _hintAnnounceText = Lang.Text("Tools.GameLink.Loading.ConnectingServer");

    [ObservableProperty]
    private Visibility _hintAnnounceVisibility = Visibility.Visible;

    [ObservableProperty]
    private string _loadDescText = Lang.Text("Tools.GameLink.Loading.DownloadingModule");

    [ObservableProperty]
    private string _natTypeText = Lang.Text("Tools.GameLink.Nat.Test");

    [ObservableProperty]
    private double _natayarkUserNameOpacity = 1d;

    [ObservableProperty]
    private string _natayarkUserNameText = Lang.Text("Tools.GameLink.Natayark.Login");

    [ObservableProperty]
    private Visibility _panEulaVisibility = Visibility.Visible;

    [ObservableProperty]
    private Visibility _panFinishVisibility = Visibility.Collapsed;

    [ObservableProperty]
    private Visibility _panSelectVisibility = Visibility.Visible;

    /// <summary>按当前子页面更新三个子页面的可见性。</summary>
    public void ApplySubpage(PageToolsGameLink.Subpages subpage)
    {
        PanEulaVisibility = subpage == PageToolsGameLink.Subpages.PanEula ? Visibility.Visible : Visibility.Collapsed;
        PanSelectVisibility = subpage == PageToolsGameLink.Subpages.PanSelect ? Visibility.Visible : Visibility.Collapsed;
        PanFinishVisibility = subpage == PageToolsGameLink.Subpages.PanFinish ? Visibility.Visible : Visibility.Collapsed;
    }
}
