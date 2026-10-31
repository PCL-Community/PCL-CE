using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.Core.Link.Lobby;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>游戏联机页的视图模型，承载联机状态对应的显示文本与可见性。</summary>
public partial class PageToolsGameLinkViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _connectTypeText = Lang.Text("Tools.GameLink.Finish.Connecting");

    [ObservableProperty]
    private Visibility _connectTypeVisibility = Visibility.Collapsed;

    [ObservableProperty]
    private string _connectUserNameText = "UserName";

    [ObservableProperty]
    private string _connectUserTypeText = "UserType";

    [ObservableProperty]
    private bool _createEnabled = true;

    [ObservableProperty]
    private bool _eulaStopEnabled = true;

    [ObservableProperty]
    private Visibility _finishCopyIpVisibility = Visibility.Visible;

    [ObservableProperty]
    private bool _finishExitEnabled = true;

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
    private bool _inputPortEnabled = true;

    [ObservableProperty]
    private string _joinLobbyId = "";

    [ObservableProperty]
    private string _loadDescText = Lang.Text("Tools.GameLink.Loading.DownloadingModule");

    [ObservableProperty]
    private bool _natTestEnabled = true;

    [ObservableProperty]
    private string _natTypeText = Lang.Text("Tools.GameLink.Nat.Test");

    [ObservableProperty]
    private bool _natayarkUserEnabled = true;

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

    [ObservableProperty]
    private bool _worldListEnabled = true;

    public ObservableCollection<GameLinkPlayerRow> PlayerRows { get; } = [];

    /// <summary>按当前子页面更新三个子页面的可见性。</summary>
    public void ApplySubpage(PageToolsGameLink.Subpages subpage)
    {
        PanEulaVisibility = subpage == PageToolsGameLink.Subpages.PanEula ? Visibility.Visible : Visibility.Collapsed;
        PanSelectVisibility = subpage == PageToolsGameLink.Subpages.PanSelect ? Visibility.Visible : Visibility.Collapsed;
        PanFinishVisibility = subpage == PageToolsGameLink.Subpages.PanFinish ? Visibility.Visible : Visibility.Collapsed;
    }

    [RelayCommand]
    private void PasteLobbyId()
    {
        string lobbyId;
        try
        {
            lobbyId = Clipboard.GetText(TextDataFormat.Text);
        }
        catch (Exception ex)
        {
            Common.Log(ex, "从剪贴板识别大厅编号出错");
            return;
        }

        if (!string.IsNullOrEmpty(lobbyId))
            JoinLobbyId = lobbyId;
        else
            HintService.Show(Hints.Info(Lang.Text("Tools.GameLink.Join.InvalidText")));
    }

    [RelayCommand]
    private void ClearLobbyId()
    {
        JoinLobbyId = string.Empty;
    }
}

/// <summary>游戏联机页的玩家列表行。</summary>
public sealed class GameLinkPlayerRow
{
    public required string Name { get; init; }

    public required string Info { get; init; }

    public required string Vendor { get; init; }

    public required string MachineId { get; init; }
}
