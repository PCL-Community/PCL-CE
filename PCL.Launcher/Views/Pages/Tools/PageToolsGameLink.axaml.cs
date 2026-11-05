using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Link.Lobby;
using PCL.Core.Link.Natayark;
using PCL.Core.Link.Scaffolding.Client.Models;
using PCL.Core.Logging;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Tools;

public partial class PageToolsGameLink : MyPageRight
{
    private const string PrivacyUrl = "https://www.pclc.cc/privacy/personal-info-brief.html";
    private const string TermsUrl = "https://account.naids.com/policy";
    private const string ReportUrl = "https://qm.qq.com/q/zfml1KaPWS";
    private const string EasyTierUrl = "https://easytier.cn/";
    private const string PysioUrl = "https://pysio.online/";
    private readonly MyButton _btnClearCode;
    private readonly MyButton _btnCreate;
    private readonly MyButton _btnEulaAgree;
    private readonly MyTextButton _btnEulaStop;
    private readonly MyIconTextButton _btnFinishCopy;
    private readonly MyIconTextButton _btnFinishCopyIp;
    private readonly MyIconTextButton _btnFinishExit;
    private readonly MyButton _btnInputPort;
    private readonly MyButton _btnJoin;
    private readonly MyIconButton _btnLoadCancel;
    private readonly MyButton _btnPasteCode;
    private readonly MyButton _btnRefreshWorlds;
    private readonly MyCard _cardPlayerList;
    private readonly MyComboBox _comboWorld;
    private readonly TextBlock _labConnectUserName;
    private readonly TextBlock _labConnectUserType;
    private readonly TextBlock _labFinishId;
    private readonly TextBlock _labFinishPing;
    private readonly TextBlock _labFinishQuality;
    private readonly TextBlock _labLoadDesc;
    private readonly TextBlock _labLoadTitle;
    private readonly TextBlock _labNatayarkUserName;
    private readonly MyLoading _load;

    private readonly MyLoadingStateSimulator _loadState = new();
    private readonly StackPanel _panEula;
    private readonly Grid _panFinish;
    private readonly StackPanel _panSelect;
    private readonly StackPanel _stackPlayerList;
    private readonly MyTextBox _txtJoinCode;

    private readonly List<FoundWorld> _worlds = [];
    private Exception? _initError;
    private bool _initFailed;

    private bool _initialized;
    private bool _initializing;
    private long _lastPing;
    private LobbyState _lastState = LobbyState.Idle;
    private int? _manualPort;

    private int _naidRefreshVersion;

    public PageToolsGameLink()
    {
        InitializeComponent();
        PanScroll = PanContent;

        _load = this.FindControl<MyLoading>("Load")!;
        _labLoadTitle = this.FindControl<TextBlock>("LabLoadTitle")!;
        _labLoadDesc = this.FindControl<TextBlock>("LabLoadDesc")!;
        _btnLoadCancel = this.FindControl<MyIconButton>("BtnLoadCancel")!;
        _panEula = this.FindControl<StackPanel>("PanEula")!;
        _panSelect = this.FindControl<StackPanel>("PanSelect")!;
        _panFinish = this.FindControl<Grid>("PanFinish")!;
        _cardPlayerList = this.FindControl<MyCard>("CardPlayerList")!;
        _stackPlayerList = this.FindControl<StackPanel>("StackPlayerList")!;
        _labFinishPing = this.FindControl<TextBlock>("LabFinishPing")!;
        _labFinishId = this.FindControl<TextBlock>("LabFinishId")!;
        _labConnectUserName = this.FindControl<TextBlock>("LabConnectUserName")!;
        _labConnectUserType = this.FindControl<TextBlock>("LabConnectUserType")!;
        _labFinishQuality = this.FindControl<TextBlock>("LabFinishQuality")!;
        _btnFinishCopyIp = this.FindControl<MyIconTextButton>("BtnFinishCopyIp")!;
        _labNatayarkUserName = this.FindControl<TextBlock>("LabNatayarkUserName")!;
        _comboWorld = this.FindControl<MyComboBox>("ComboWorld")!;
        _txtJoinCode = this.FindControl<MyTextBox>("TxtJoinCode")!;
        _btnCreate = this.FindControl<MyButton>("BtnCreate")!;
        _btnJoin = this.FindControl<MyButton>("BtnJoin")!;
        _btnInputPort = this.FindControl<MyButton>("BtnInputPort")!;
        _btnRefreshWorlds = this.FindControl<MyButton>("BtnRefreshWorlds")!;
        _btnClearCode = this.FindControl<MyButton>("BtnClearCode")!;
        _btnPasteCode = this.FindControl<MyButton>("BtnPasteCode")!;
        _btnEulaAgree = this.FindControl<MyButton>("BtnEulaAgree")!;
        _btnEulaStop = this.FindControl<MyTextButton>("BtnEulaStop")!;
        _btnFinishCopy = this.FindControl<MyIconTextButton>("BtnFinishCopy")!;
        _btnFinishExit = this.FindControl<MyIconTextButton>("BtnFinishExit")!;

        Current = this;
        _load.AutoRun = true;
        _load.State = _loadState;
        CardLoad.PointerReleased += (_, _) =>
        {
            if (!_initFailed)
                return;
            _initFailed = false;
            _initError = null;
            _ = _EnsureInitializedAsync();
        };

        _btnCreate.Click += (_, _) => _ = _CreateLobbyAsync(_manualPort);
        _btnJoin.Click += (_, _) => _ = _JoinLobbyAsync();
        _btnInputPort.Click += (_, _) => _ = _InputManualPortAsync();
        _btnRefreshWorlds.Click += (_, _) => _ = _RefreshWorldsAsync();
        _btnClearCode.Click += (_, _) => _txtJoinCode.Text = string.Empty;
        _btnPasteCode.Click += (_, _) => _ = _PasteLobbyCodeAsync();
        _btnEulaAgree.Click += (_, _) => _AgreeEula();
        _btnEulaStop.Click += (_, _) => _ = _RevokeEulaAsync();
        _btnLoadCancel.Click += (_, _) => _ = _CancelLoadAsync();
        _btnFinishCopy.Click += (_, _) => _ = _CopyLobbyCodeAsync();
        _btnFinishCopyIp.Click += (_, _) => _ = _ShowCopyIpDialogAsync();
        _btnFinishExit.Click += (_, _) => _ = _LeaveLobbyAsync();

        this.FindControl<MyListItem>("ItemEulaPrivacy")!.Click +=
            (_, _) => Basics.OpenPath(PrivacyUrl);
        this.FindControl<MyListItem>("ItemEulaTerms")!.Click += (_, _) => Basics.OpenPath(TermsUrl);
        this.FindControl<MyTextButton>("BtnLinkPrivacy")!.Click +=
            (_, _) => Basics.OpenPath(PrivacyUrl);
        this.FindControl<MyTextButton>("BtnLinkTerms")!.Click +=
            (_, _) => Basics.OpenPath(TermsUrl);
        this.FindControl<MyTextButton>("BtnLinkReport")!.Click +=
            (_, _) => Basics.OpenPath(ReportUrl);
        this.FindControl<MyTextButton>("BtnLinkEasyTier")!.Click +=
            (_, _) => Basics.OpenPath(EasyTierUrl);
        this.FindControl<MyTextButton>("BtnLinkPysio")!.Click +=
            (_, _) => Basics.OpenPath(PysioUrl);

        _labNatayarkUserName.Text = Lang.Text("Tools.GameLink.Natayark.Loading");
        _labNatayarkUserName.PointerPressed += (_, _) => _OnNatayarkAccountClicked();
        CardAccount.PointerPressed += (_, _) => _OnNatayarkAccountClicked();
        UpdateNatayarkUser();

        LobbyService.StateChanged += _OnLobbyStateChanged;
        LobbyService.OnClientPing += _OnClientPing;
        LobbyService.Players.CollectionChanged += (_, _) => RebuildPlayerList();
        Loaded += (_, _) => _OnLoaded();
    }

    // TODO(FIX-P3-46): NAT 类型检测、Natayark 账户界面、大厅公告与 EasyTier 连接质量/虚拟 IP 依赖未迁移接口，
    // 界面按禁用态或隐藏处理；接口补齐后在 _UpdateLayout 中接回对应控件。

    public static PageToolsGameLink? Current { get; private set; }

    public void Reload()
    {
        UpdateNatayarkUser();
        _ = _EnsureInitializedAsync();
        _ = _RefreshWorldsAsync();
        _UpdateLayout();
    }

    protected override void RefreshLocalization()
    {
        _UpdateLayout();
        RebuildPlayerList();
        UpdateNatayarkUser();
    }

    private void _OnLoaded()
    {
        Reload();
    }

    #region 状态刷新

    private void _OnLobbyStateChanged(LobbyState oldState, LobbyState newState)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(_UpdateLayout);
            return;
        }

        _UpdateLayout();
    }

    private void _OnClientPing(long latency)
    {
        _lastPing = latency;
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(_UpdateLayout);
            return;
        }

        _UpdateLayout();
    }

    private void _UpdateLayout()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(_UpdateLayout);
            return;
        }

        var state = LobbyService.CurrentState;
        _lastState = state;

        var isLoading = state is LobbyState.Initializing
            or LobbyState.Creating
            or LobbyState.Joining
            or LobbyState.Leaving;
        var showLoader = isLoading || _initializing || _initFailed;
        var panLoad = this.FindControl<Grid>("PanLoad")!;
        panLoad.IsVisible = showLoader;
        PanContent.IsVisible = !showLoader;

        if (_initFailed)
        {
            _labLoadTitle.Text = Lang.Text("Tools.GameLink.Error.ConnectFailed");
            _labLoadDesc.Text = _initError?.Message ?? "";
            _labLoadDesc.IsVisible = !string.IsNullOrWhiteSpace(_labLoadDesc.Text);
            _btnLoadCancel.IsVisible = false;
            _loadState.LoadingState = LoadingState.Error;
            _panEula.IsVisible = false;
            _panSelect.IsVisible = false;
            _panFinish.IsVisible = false;
            _cardPlayerList.IsVisible = false;
            _stackPlayerList.Children.Clear();
            return;
        }

        _loadState.LoadingState = showLoader ? LoadingState.Run : LoadingState.Stop;
        _labLoadTitle.Text = state switch
        {
            LobbyState.Creating => Lang.Text("Tools.GameLink.Loading.Creating"),
            _ => Lang.Text("Tools.GameLink.Loading.ConnectingServer")
        };
        _labLoadDesc.Text = state switch
        {
            LobbyState.Creating => LobbyService.CurrentLobbyCode ?? "",
            _ => ""
        };
        _labLoadDesc.IsVisible = !string.IsNullOrEmpty(_labLoadDesc.Text);
        _btnLoadCancel.IsVisible = state is LobbyState.Creating or LobbyState.Joining;

        var eulaAccepted = IsEulaAccepted();
        var connected = state == LobbyState.Connected;
        var canSelect = eulaAccepted && !showLoader && !connected;
        _panEula.IsVisible = !eulaAccepted && !showLoader;
        _panSelect.IsVisible = canSelect;
        _panFinish.IsVisible = connected;

        if (connected)
        {
            _labFinishQuality.Text = Lang.Text("Tools.GameLink.Finish.Connected");
            _labFinishPing.Text = _lastPing > 0
                ? Lang.Text("Tools.GameLink.Finish.PingMs", _lastPing)
                : Lang.Text("Tools.GameLink.Finish.Checking");
            _btnFinishCopyIp.IsVisible = !LobbyService.IsHost
                && LobbyInfoProvider.McForward?.LocalEndPoint is not null;
            _labFinishId.Text = string.IsNullOrWhiteSpace(LobbyService.CurrentLobbyCode)
                ? "—"
                : LobbyService.CurrentLobbyCode;
            _labConnectUserName.Text = string.IsNullOrWhiteSpace(LobbyService.CurrentUserName)
                ? Lang.Text("Tools.GameLink.Finish.Guest")
                : LobbyService.CurrentUserName;
            _labConnectUserType.Text = LobbyService.IsHost
                ? Lang.Text("Tools.GameLink.Finish.Host")
                : Lang.Text("Tools.GameLink.Finish.Guest");
            _btnFinishCopy.IsEnabled = !string.IsNullOrWhiteSpace(LobbyService.CurrentLobbyCode);
            _cardPlayerList.IsVisible = true;
            RebuildPlayerList();
        }
        else
        {
            _cardPlayerList.IsVisible = false;
            _stackPlayerList.Children.Clear();
        }

        if (canSelect)
        {
            _btnCreate.IsEnabled = _manualPort is not null || _comboWorld.SelectedIndex >= 0;
            _btnJoin.IsEnabled = true;
        }
    }

    private static bool IsEulaAccepted()
    {
        try
        {
            return States.Link.LinkEula;
        }
        catch (Exception)
        {
            return true;
        }
    }

    private void UpdateNatayarkUser()
    {
        if (string.IsNullOrWhiteSpace(States.Link.NaidRefreshToken))
        {
            _labNatayarkUserName.Text = Lang.Text("Tools.GameLink.Natayark.Login");
            return;
        }

        var profile = NatayarkProfileManager.NaidProfile;
        if (!string.IsNullOrWhiteSpace(profile.Username))
        {
            _labNatayarkUserName.Text = profile.Status == 0
                ? profile.Username
                : $"{profile.Username} {Lang.Text("Tools.GameLink.Natayark.Abnormal")}";
            _labNatayarkUserName.Opacity = profile.Status == 0 ? 1d : 0.6d;
            return;
        }

        _labNatayarkUserName.Text = Lang.Text("Tools.GameLink.Natayark.Loading");
        _ = RefreshNaidProfileAsync();
    }

    private async Task RefreshNaidProfileAsync()
    {
        var version = Interlocked.Increment(ref _naidRefreshVersion);
        try
        {
            var token = States.Link.NaidRefreshToken;
            if (string.IsNullOrWhiteSpace(token))
                return;
            await Task.Run(() =>
            {
                var expireText = States.Link.NaidRefreshExpireTime;
                if (DateTime.TryParse(expireText, out var expireAt) && expireAt < DateTime.Now)
                    throw new InvalidOperationException("Natayark 刷新令牌已过期");
                NatayarkProfileManager.GetNaidDataAsync(token, true).GetAwaiter().GetResult();
            });
            if (version != Volatile.Read(ref _naidRefreshVersion))
                return;
            await Dispatcher.UIThread.InvokeAsync(UpdateNatayarkUser);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Tools", "刷新 Natayark 账号信息失败");
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (version != Volatile.Read(ref _naidRefreshVersion))
                    return;
                _labNatayarkUserName.Text = Lang.Text("Tools.GameLink.Natayark.FetchFailed");
                _labNatayarkUserName.Opacity = 0.6d;
            });
        }
    }

    private void _OnNatayarkAccountClicked()
    {
        _ = string.IsNullOrWhiteSpace(States.Link.NaidRefreshToken)
            ? LoginNaidAsync()
            : LogoutNaidAsync();
    }

    private async Task LoginNaidAsync()
    {
        try
        {
            var result = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Tools.GameLink.Natayark.LoginTitle"),
                    Content = Lang.Text("Tools.GameLink.Natayark.LoginPrompt"),
                    Buttons = DialogButtons.From(
                        DialogButton.Create(Lang.Text("Tools.GameLink.Natayark.Continue")),
                        DialogButton.Create(Lang.Text("Common.Action.Cancel")))
                });
            if (!result.IsConfirmed)
                return;

            _labNatayarkUserName.Text = Lang.Text("Tools.GameLink.Natayark.BrowserContinue");
            _labNatayarkUserName.Opacity = 0.6d;
            WebServer.StartNaidAuthorize(() => LauncherCommon.RunInUi(() =>
            {
                HintService.Show(Hints.Success(Lang.Text("Tools.GameLink.Natayark.LoginComplete")));
                UpdateNatayarkUser();
            }));
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Tools", "启动 Natayark 登录失败");
            HintService.Show(Hints.Error(Lang.Text("Tools.GameLink.Natayark.LoginFailed")));
        }
    }

    private async Task LogoutNaidAsync()
    {
        try
        {
            var result = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Tools.GameLink.Natayark.LogoutTitle"),
                    Content = Lang.Text("Tools.GameLink.Natayark.LogoutConfirm"),
                    Buttons = DialogButtons.OkCancel
                });
            if (!result.IsConfirmed)
                return;

            States.Link.NaidRefreshToken = "";
            Interlocked.Increment(ref _naidRefreshVersion);
            UpdateNatayarkUser();
            HintService.Show(Hints.Success(Lang.Text("Tools.GameLink.Natayark.LogoutComplete")));
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Tools", "退出 Natayark 账号失败");
        }
    }

    private void RebuildPlayerList()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(RebuildPlayerList);
            return;
        }

        var players = LobbyService.Players;
        _cardPlayerList.Title = Lang.Text("Tools.GameLink.Member.ListCount", players.Count);
        _stackPlayerList.Children.Clear();
        foreach (var player in players)
        {
            var info = player.Kind == PlayerKind.HOST
                ? Lang.Text(
                    "Tools.GameLink.Player.Details",
                    Lang.Text("Tools.GameLink.Player.Host"),
                    player.Vendor)
                : player.Vendor;
            var item = new MyListItem
            {
                Title = player.Name,
                Info = info,
                SvgIcon = "lucide/user",
                Type = MyListItem.CheckType.Clickable
            };
            var profile = player;
            item.Click += (_, _) => _ShowPlayerInfo(profile);
            _stackPlayerList.Children.Add(item);
        }
    }

    private async void _ShowPlayerInfo(PlayerProfile profile)
    {
        try
        {
            await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Tools.GameLink.Player.InfoTitle", profile.Name),
                    Content = Lang.Text(
                        "Tools.GameLink.Player.InfoMessage",
                        profile.Name,
                        profile.Vendor),
                    Buttons = DialogButtons.Ok
                });
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "显示玩家信息失败",
                LauncherLogLevel.Msgbox,
                userSummary: Lang.Text("Tools.GameLink.Player.InfoTitle", ""));
        }
    }

    #endregion

    #region 大厅操作

    private async Task _EnsureInitializedAsync()
    {
        if (_initialized)
        {
            _loadState.LoadingState = LoadingState.Stop;
            _UpdateLayout();
            return;
        }

        if (_initializing)
            return;

        _initializing = true;
        _initFailed = false;
        _initError = null;
        _loadState.LoadingState = LoadingState.Run;
        _UpdateLayout();
        try
        {
            await LobbyService.InitializeAsync();
            _initialized = true;
            _loadState.LoadingState = LoadingState.Stop;
            _ = _RefreshWorldsAsync();
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Tools", "初始化联机大厅模块失败");
            _initFailed = true;
            _initError = ex;
            _loadState.LoadingState = LoadingState.Error;
            HintService.Show(Hints.Error(Lang.Text("Tools.GameLink.Error.NetworkTestFailed")));
        }
        finally
        {
            _initializing = false;
            _UpdateLayout();
        }
    }

    private async Task _RefreshWorldsAsync()
    {
        try
        {
            if (_initialized)
                await LobbyService.DiscoverWorldAsync();
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Tools", "探测局域网世界失败");
        }

        _worlds.Clear();
        _worlds.AddRange(LobbyService.DiscoveredWorlds);
        _comboWorld.ItemsSource = _worlds.Select(world => world.Name).ToList();
        if (_worlds.Count > 0)
            _comboWorld.SelectedIndex = 0;
        _btnCreate.IsEnabled = _manualPort is not null || _worlds.Count > 0;
    }

    private async Task _InputManualPortAsync()
    {
        var result = await DialogService.Instance.ShowAsync(
            new InputDialogRequest
            {
                Title = Lang.Text("Tools.GameLink.Create.ManualInput"),
                HintText = Lang.Text("Tools.GameLink.Create.EnterPort"),
                Content = Lang.Text("Tools.GameLink.Create.Description"),
                DefaultValue = _manualPort?.ToString() ?? "",
                Buttons = DialogButtons.OkCancel,
                HighLight = false
            });
        if (!result.IsConfirmed)
            return;

        var text = (result.InputText ?? "").Trim();
        if (!int.TryParse(text, out var port) || port is <= 0 or > 65535)
        {
            HintService.Show(Hints.Error(Lang.Text("Tools.GameLink.Create.NotMcPort")));
            return;
        }

        _manualPort = port;
        await _CreateLobbyAsync(port);
    }

    private async Task _CreateLobbyAsync(int? port)
    {
        var targetPort = port;
        if (targetPort is null)
        {
            var index = _comboWorld.SelectedIndex;
            if (index < 0 || index >= _worlds.Count)
            {
                HintService.Show(Hints.Warning(Lang.Text("Tools.GameLink.Create.NoWorld")));
                return;
            }

            targetPort = _worlds[index].Port;
        }

        var userName = ResolveUserName();
        if (string.IsNullOrWhiteSpace(userName))
        {
            HintService.Show(Hints.Warning(Lang.Text("Link.Mod.UsernameOrLogin")));
            return;
        }

        _btnCreate.IsEnabled = false;
        try
        {
            if (!await LobbyService.CreateLobbyAsync(targetPort.Value, userName))
                HintService.Show(Hints.Error(Lang.Text("Tools.GameLink.Error.ConnectFailed")));
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Tools", "创建联机大厅失败");
            HintService.Show(Hints.Error(Lang.Text("Tools.GameLink.Error.ConnectFailed")));
        }
        finally
        {
            _btnCreate.IsEnabled = true;
            _UpdateLayout();
        }
    }

    private async Task _JoinLobbyAsync()
    {
        var lobbyCode = _txtJoinCode.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(lobbyCode))
        {
            HintService.Show(Hints.Warning(Lang.Text("Tools.GameLink.Join.InvalidText")));
            return;
        }

        var userName = ResolveUserName();
        if (string.IsNullOrWhiteSpace(userName))
        {
            HintService.Show(Hints.Warning(Lang.Text("Link.Mod.UsernameOrLogin")));
            return;
        }

        _btnJoin.IsEnabled = false;
        try
        {
            if (!await LobbyService.JoinLobbyAsync(lobbyCode, userName))
                HintService.Show(Hints.Error(Lang.Text("Tools.GameLink.Error.ConnectFailed")));
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Tools", "加入联机大厅失败");
            HintService.Show(Hints.Error(Lang.Text("Tools.GameLink.Error.ConnectFailed")));
        }
        finally
        {
            _btnJoin.IsEnabled = true;
            _UpdateLayout();
        }
    }

    private async Task _LeaveLobbyAsync()
    {
        try
        {
            var result = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Tools.GameLink.Exit.ConfirmTitle"),
                    Content = LobbyService.IsHost
                        ? Lang.Text("Tools.GameLink.Exit.ConfirmMessageWithHost")
                        : Lang.Text("Tools.GameLink.Exit.ConfirmMessage"),
                    Buttons = DialogButtons.OkCancel
                });
            if (!result.IsConfirmed)
                return;
            await LobbyService.LeaveLobbyAsync();
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Tools", "退出联机大厅失败");
        }
        finally
        {
            _UpdateLayout();
        }
    }

    private async Task _CancelLoadAsync()
    {
        try
        {
            await LobbyService.LeaveLobbyAsync();
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Tools", "取消联机操作失败");
        }
        finally
        {
            _UpdateLayout();
        }
    }

    private async Task _CopyLobbyCodeAsync()
    {
        var lobbyCode = LobbyService.CurrentLobbyCode;
        if (string.IsNullOrWhiteSpace(lobbyCode))
            return;
        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(lobbyCode);
            HintService.Show(Hints.Success(Lang.Text("Tools.GameLink.Finish.CopyLobbyId")));
        }
    }

    /// <summary>显示本机大厅转发地址，与 WPF 的复制 IP 弹窗一致。</summary>
    private async Task _ShowCopyIpDialogAsync()
    {
        var port = LobbyInfoProvider.McForward?.LocalEndPoint?.Port;
        if (port is null)
            return;

        var ip = $"127.0.0.1:{port.Value}";
        await DialogService.Instance.ShowAsync(
            new TextDialogRequest
            {
                Title = Lang.Text("Tools.GameLink.CopyIp.Title"),
                Content = Lang.Text("Tools.GameLink.CopyIp.Message", ip),
                Buttons = DialogButtons.From(
                    DialogButton.Create(
                        Lang.Text("Common.Action.Copy"),
                        () => _ = _CopyTextAsync(ip)),
                    DialogButton.Create(Lang.Text("Tools.GameLink.CopyIp.Back"))),
                ForceWait = true
            });
    }

    private async Task _CopyTextAsync(string text)
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
            await clipboard.SetTextAsync(text);
    }

    private async Task _PasteLobbyCodeAsync()
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard) return;
        try
        {
            var text = await clipboard.TryGetTextAsync();
            if (!string.IsNullOrWhiteSpace(text))
                _txtJoinCode.Text = text.Trim();
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Tools", "读取剪贴板失败");
        }
    }

    private static string ResolveUserName()
    {
        return LobbyInfoProvider.GetUsername()?.Trim() ?? string.Empty;
    }

    #endregion

    #region 协议

    private void _AgreeEula()
    {
        try
        {
            States.Link.LinkEula = true;
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Tools", "写入大厅协议同意状态失败");
        }

        _UpdateLayout();
    }

    private async Task _RevokeEulaAsync()
    {
        try
        {
            var result = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Tools.GameLink.Eula.RevokeTitle"),
                    Content = Lang.Text("Tools.GameLink.Eula.RevokeConfirm"),
                    Buttons = DialogButtons.OkCancel,
                    IsWarn = true
                });
            if (!result.IsConfirmed)
                return;

            if (LobbyService.CurrentState == LobbyState.Connected)
                await LobbyService.LeaveLobbyAsync();
            States.Link.LinkEula = false;
            HintService.Show(Hints.Info(Lang.Text("Tools.GameLink.Eula.Disabled")));
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Tools", "撤销大厅协议失败");
        }

        _UpdateLayout();
    }

    #endregion
}