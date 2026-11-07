using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.IO.Net.Http;
using PCL.Core.Link;
using PCL.Core.Link.EasyTier;
using PCL.Core.Link.Lobby;
using PCL.Core.Link.McPing;
using PCL.Core.Link.Natayark;
using PCL.Core.Link.Scaffolding.Client.Models;
using PCL.Core.Link.Scaffolding.EasyTier;
using PCL.Core.Logging;
using PCL.Core.Minecraft.Profile;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.Core.Utils;
using PCL.Core.Utils.Validate;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>
///     联机大厅页的视图模型：公告轮询与轮播、账号状态、NAT/EasyTier 预检、世界发现、
///     大厅创建/加入以及完成态的生命周期。
///     所有静态服务事件在这里汇总，页面只负责控件绑定与剪贴板等界面交互。
/// </summary>
public partial class PageToolsGameLinkViewModel : ViewModelBase
{
    private const int AnnouncementIntervalMilliseconds = 10_000;
    private const int AnnouncementRequestTimeoutMilliseconds = 7_000;

    private readonly Lock _announcementLock = new();
    private readonly SemaphoreSlim _easyTierDownloadLock = new(1, 1);

    private int _accountActionBusy;
    private bool _accountBusy;
    private bool _accountLoginPending;
    private bool _accountRefreshFailed;
    private int _accountRefreshVersion;

    private CancellationTokenSource? _announcementCarouselCts;
    private int _announcementFailureKind;
    private int _announcementIndex;
    private LinkAnnounceInfo[] _announcementItems = [];
    private int _announcementRequestId;
    private int _announcementState;
    private int _easyTierDownloading;
    private int _eulaBusy;
    private bool _eulaReadErrorShown;
    private bool _forceEulaPage;
    private volatile bool _initCancelRequested;
    private string? _initErrorText;
    private bool _initFailed;
    private bool _initialized;
    private bool _initializing;
    private long _lastPing;
    private int _loginAttemptVersion;
    private CliNetTest.NetStatus? _natStatus;
    private int _natTestBusy;
    private bool _natTestFailed;
    private int _operationBusy;
    private bool _refreshingWorlds;
    private int _serverLifecycleBusy;
    private bool _tokenExpiredHintShown;

    public PageToolsGameLinkViewModel()
    {
        LobbyService.StateChanged += OnLobbyStateChanged;
        LobbyService.OnClientPing += OnClientPing;
        LobbyService.OnNeedDownloadEasyTier += OnNeedDownloadEasyTier;
        LobbyService.OnUserStopGame += OnUserStopGame;
        LobbyService.OnServerShutDown += OnServerShutDown;
        LobbyService.OnServerStarted += OnServerStarted;
        LobbyService.OnServerException += OnServerException;
        LobbyService.DiscoveredWorlds.CollectionChanged += OnDiscoveredWorldsChanged;
        LobbyService.Players.CollectionChanged += OnPlayersChanged;

        UpdateAccountDisplay();
        UpdateNatText();
    }

    #region 预检

    public async Task<bool> PrecheckAsync()
    {
        if (!LobbyInfoProvider.IsLobbyAvailable)
        {
            HintService.Show(
                CurrentAnnouncementState == AnnouncementLoadState.Loading
                    ? Hints.Info(Lang.Text("Tools.GameLink.Loading.ConnectingServer"))
                    : Hints.Error(Lang.Text("Link.Mod.LobbyUnavailable")));
            return false;
        }

        var currentProfile = ProfileService.Current;
        if (currentProfile is not null && currentProfile.UserName.Contains('|'))
        {
            HintService.Show(Hints.Info(Lang.Text("Link.Mod.InvalidPlayerId")));
            return false;
        }

        if (LobbyInfoProvider.RequiresLogin)
        {
            var token = States.Link.NaidRefreshToken;
            if (string.IsNullOrWhiteSpace(token))
            {
                HintService.Show(Hints.Error(Lang.Text("Link.Mod.LoginFirst")));
                return false;
            }

            try
            {
                await NatayarkProfileManager.GetNaidDataAsync(token, true).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogWrapper.Error(ex, "Link", "刷新 Natayark 登录信息失败");
                HintService.Show(Hints.Error(Lang.Text("Link.Mod.ReLoginRequired")));
                RunOnUi(UpdateAccountDisplay);
                return false;
            }

            RunOnUi(UpdateAccountDisplay);
            var profile = NatayarkProfileManager.NaidProfile;
            if (string.IsNullOrWhiteSpace(profile.Username))
            {
                HintService.Show(Hints.Error(Lang.Text("Link.Mod.NaidFetchFailed")));
                return false;
            }

            if (LobbyInfoProvider.RequiresRealName && !profile.IsRealNamed)
            {
                HintService.Show(Hints.Error(Lang.Text("Link.Mod.RealNameRequired")));
                return false;
            }

            if (profile.Status != 0)
            {
                HintService.Show(Hints.Error(Lang.Text("Link.Mod.AccountBanned")));
                return false;
            }
        }

        var username = GetUsername();
        if (string.IsNullOrWhiteSpace(username))
        {
            HintService.Show(Hints.Error(Lang.Text("Link.Mod.UsernameOrLogin")));
            return false;
        }

        if (username.Contains('|'))
        {
            HintService.Show(Hints.Info(Lang.Text("Link.Mod.InvalidPlayerId")));
            return false;
        }

        if (Volatile.Read(ref _easyTierDownloading) == 1)
        {
            HintService.Show(Hints.Info(Lang.Text("Link.Mod.EasyTierNotReady")));
            return false;
        }

        int easyTierStatus;
        try
        {
            easyTierStatus = await Task.Run(ETController.Precheck).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Link", "检测 EasyTier 依赖失败");
            HintService.Show(Hints.Error(ex.Message));
            return false;
        }

        if (easyTierStatus == 1)
        {
            HintService.Show(Hints.Info(Lang.Text("Link.Mod.DownloadingDeps")));
            _ = DownloadEasyTierAsync();
            return false;
        }

        return true;
    }

    #endregion

    private static void RunOnUi(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.Post(action);
    }

    private enum AnnouncementLoadState
    {
        Loading,
        Loaded,
        Failed
    }

    private enum AnnouncementFailureKind
    {
        None,
        Connect,
        UpdateRequired
    }

    #region 绑定状态

    [ObservableProperty]
    public partial string AnnouncementText { get; set; } =
        Lang.Text("Tools.GameLink.Loading.ConnectingServer");

    [ObservableProperty] public partial int AnnouncementThemeIndex { get; set; }

    [ObservableProperty] public partial bool AnnouncementVisible { get; set; } = true;

    [ObservableProperty] public partial bool AccountEnabled { get; set; } = true;

    [ObservableProperty] public partial double AccountOpacity { get; set; } = 1d;

    [ObservableProperty]
    public partial string AccountText { get; set; } =
        Lang.Text("Tools.GameLink.Natayark.Login");

    [ObservableProperty] public partial bool CancelVisible { get; set; }

    [ObservableProperty] public partial bool ConnectTypeVisible { get; set; }

    [ObservableProperty]
    public partial string ConnectTypeText { get; set; } =
        Lang.Text("Tools.GameLink.Finish.Unavailable");

    [ObservableProperty] public partial string ConnectUserNameText { get; set; } = string.Empty;

    [ObservableProperty] public partial string ConnectUserTypeText { get; set; } = string.Empty;

    [ObservableProperty] public partial bool CreateEnabled { get; set; }

    [ObservableProperty] public partial bool EasyTierVersionWarningVisible { get; set; }

    [ObservableProperty] public partial bool EulaStopEnabled { get; set; } = true;

    [ObservableProperty] public partial bool FinishCopyIpVisible { get; set; }

    [ObservableProperty] public partial bool FinishCopyEnabled { get; set; }

    [ObservableProperty] public partial bool FinishExitEnabled { get; set; }

    [ObservableProperty]
    public partial string FinishExitText { get; set; } =
        Lang.Text("Tools.GameLink.Finish.Exit");

    [ObservableProperty] public partial string FinishIdText { get; set; } = "—";

    [ObservableProperty]
    public partial string FinishPingText { get; set; } =
        Lang.Text("Tools.GameLink.Finish.Checking");

    [ObservableProperty] public partial bool FinishPingVisible { get; set; }

    [ObservableProperty]
    public partial string FinishQualityText { get; set; } =
        Lang.Text("Tools.GameLink.Finish.Checking");

    [ObservableProperty]
    public partial string FinishQualityToolTip { get; set; } =
        Lang.Text("Tools.GameLink.Finish.ConnectionStatus");

    [ObservableProperty] public partial bool InputPortEnabled { get; set; }

    [ObservableProperty] public partial bool IsLoadError { get; set; }

    [ObservableProperty] public partial bool JoinEnabled { get; set; }

    [ObservableProperty] public partial string JoinCode { get; set; } = string.Empty;

    [ObservableProperty] public partial string LoadDescription { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LoadTitle { get; set; } =
        Lang.Text("Tools.GameLink.Loading.ConnectingServer");

    [ObservableProperty] public partial bool NatTestEnabled { get; set; } = true;

    [ObservableProperty]
    public partial string NatTypeText { get; set; } =
        Lang.Text("Tools.GameLink.Nat.Test");

    [ObservableProperty]
    public partial string PlayerListTitle { get; set; } =
        Lang.Text("Tools.GameLink.Member.ListLoading");

    [ObservableProperty] public partial GameLinkWorldOption? SelectedWorld { get; set; }

    [ObservableProperty] public partial bool ShowContent { get; set; }

    [ObservableProperty] public partial bool ShowEula { get; set; }

    [ObservableProperty] public partial bool ShowFinish { get; set; }

    [ObservableProperty] public partial bool ShowLoader { get; set; } = true;

    [ObservableProperty] public partial bool ShowSelect { get; set; }

    [ObservableProperty] public partial bool WorldListEnabled { get; set; }

    public ObservableCollection<GameLinkPlayerRow> PlayerRows { get; } = [];

    public ObservableCollection<GameLinkWorldOption> Worlds { get; } = [];

    partial void OnSelectedWorldChanged(GameLinkWorldOption? value)
    {
        RefreshLayout();
    }

    #endregion

    #region 页面入口

    /// <summary>页面重新进入或左栏刷新时调用。</summary>
    public void Reload()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(Reload);
            return;
        }

        BeginAnnouncementReload();
        SyncWorldsFromService();
        EnsureAccountState();
        _ = EnsureInitializedAsync();
    }

    private void SyncWorldsFromService()
    {
        var serviceWorlds = LobbyService.DiscoveredWorlds.ToList();
        if (serviceWorlds.Count == Worlds.Count
            && serviceWorlds.All(world => Worlds.Any(option =>
                option.Port == world.Port && option.Name == world.Name)))
            return;

        var selectedPort = SelectedWorld?.Port;
        Worlds.Clear();
        foreach (var world in serviceWorlds)
            AddWorld(world);

        SelectedWorld = selectedPort is null
            ? Worlds.FirstOrDefault()
            : Worlds.FirstOrDefault(world => world.Port == selectedPort) ?? Worlds.FirstOrDefault();
        RefreshLayout();
    }

    /// <summary>页面离开视觉树后停止公告轮播，返回时由 <see cref="Reload" /> 重新启动。</summary>
    public void Suspend()
    {
        _announcementCarouselCts?.Cancel();
        _announcementCarouselCts?.Dispose();
        _announcementCarouselCts = null;
    }

    /// <summary>初始化失败后点击加载卡片重试。</summary>
    public async Task RetryInitializeAsync()
    {
        if (_initializing)
            return;

        BeginAnnouncementReload();
        _initFailed = false;
        _initErrorText = null;
        _initialized = false;
        SyncWorldsFromService();
        await EnsureInitializedAsync().ConfigureAwait(true);
    }

    public void RefreshLocalization()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(RefreshLocalization);
            return;
        }

        RefreshLayout();
        RefreshAnnouncementText();
        ApplyNatText();
        RebuildPlayerRows();
        UpdateAccountDisplay();
    }

    #endregion

    #region 状态刷新

    private void OnLobbyStateChanged(LobbyState oldState, LobbyState newState)
    {
        RunOnUi(() =>
        {
            switch (newState)
            {
                case LobbyState.Connected:
                    _initialized = true;
                    _initFailed = false;
                    _initErrorText = null;
                    UpdateConnectedInfo();
                    break;
                case LobbyState.Error:
                    _initialized = false;
                    _initFailed = true;
                    _initErrorText = Lang.Text("Tools.GameLink.Error.ConnectFailed");
                    break;
                case LobbyState.Idle:
                case LobbyState.Initialized:
                    ClearFinishState();
                    EnsureAccountState();
                    break;
            }

            RefreshLayout();
        });
    }

    private void OnClientPing(long latency)
    {
        _lastPing = latency;
        RunOnUi(UpdateConnectedInfo);
    }

    private void OnServerStarted()
    {
        RunOnUi(() =>
        {
            FinishIdText = string.IsNullOrWhiteSpace(LobbyService.CurrentLobbyCode)
                ? "—"
                : LobbyService.CurrentLobbyCode!;
            RefreshLayout();
        });
    }

    private void OnPlayersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RunOnUi(RebuildPlayerRows);
    }

    private void OnDiscoveredWorldsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RunOnUi(() =>
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Reset:
                    Worlds.Clear();
                    foreach (var world in LobbyService.DiscoveredWorlds)
                        AddWorld(world);
                    break;
                case NotifyCollectionChangedAction.Add when e.NewItems is not null:
                    foreach (var item in e.NewItems.OfType<FoundWorld>())
                        AddWorld(item);
                    break;
                case NotifyCollectionChangedAction.Remove when e.OldItems is not null:
                {
                    var ports =
                        e.OldItems.OfType<FoundWorld>().Select(world => world.Port).ToHashSet();
                    for (var i = Worlds.Count - 1; i >= 0; i--)
                        if (ports.Contains(Worlds[i].Port))
                            Worlds.RemoveAt(i);
                    break;
                }
                case NotifyCollectionChangedAction.Replace:
                case NotifyCollectionChangedAction.Move:
                    Worlds.Clear();
                    foreach (var world in LobbyService.DiscoveredWorlds)
                        AddWorld(world);
                    break;
            }

            SyncWorldSelection();
            RefreshLayout();
        });
    }

    private void AddWorld(FoundWorld world)
    {
        var existingIndex = -1;
        for (var i = 0; i < Worlds.Count; i++)
        {
            if (Worlds[i].Port != world.Port)
                continue;
            existingIndex = i;
            break;
        }

        if (existingIndex >= 0)
        {
            if (Worlds[existingIndex].Name != world.Name)
                Worlds[existingIndex] = new GameLinkWorldOption(world.Name, world.Port);
            return;
        }

        Worlds.Add(new GameLinkWorldOption(world.Name, world.Port));
    }

    private void SyncWorldSelection()
    {
        if (Worlds.Count == 0)
        {
            SelectedWorld = null;
            return;
        }

        if (SelectedWorld is null || !Worlds.Contains(SelectedWorld))
            SelectedWorld = Worlds[0];
    }

    private void RefreshLayout()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(RefreshLayout);
            return;
        }

        var state = LobbyService.CurrentState;
        var busy = state is LobbyState.Initializing
            or LobbyState.Creating
            or LobbyState.Joining
            or LobbyState.Leaving;
        var loading = busy || _initializing || _initFailed;

        ShowLoader = loading;
        ShowContent = !loading;
        IsLoadError = _initFailed;
        CancelVisible = _initializing || state is LobbyState.Initializing;

        if (_initFailed)
        {
            LoadTitle = Lang.Text("Tools.GameLink.Error.ConnectFailed");
            LoadDescription = _initErrorText ?? string.Empty;
            ShowEula = false;
            ShowSelect = false;
            ShowFinish = false;
            CancelVisible = false;
            WorldListEnabled = false;
            CreateEnabled = false;
            JoinEnabled = false;
            InputPortEnabled = false;
            FinishExitEnabled = false;
            FinishCopyEnabled = false;
            PlayerListTitle = Lang.Text("Tools.GameLink.Member.ListLoading");
            PlayerRows.Clear();
            EulaStopEnabled = Volatile.Read(ref _eulaBusy) == 0
                && Volatile.Read(ref _operationBusy) == 0;
            AccountEnabled = !_accountBusy && Volatile.Read(ref _accountActionBusy) == 0;
            NatTestEnabled = Volatile.Read(ref _natTestBusy) == 0;
            UpdateAccountDisplay();
            return;
        }

        LoadTitle = state switch
        {
            LobbyState.Creating => Lang.Text("Tools.GameLink.Loading.Creating"),
            _ => Lang.Text("Tools.GameLink.Loading.ConnectingServer")
        };
        LoadDescription = state switch
        {
            LobbyState.Creating => LobbyService.CurrentLobbyCode ?? string.Empty,
            LobbyState.Initializing => Lang.Text("Tools.GameLink.Loading.DownloadingModule"),
            _ => _initializing
                ? Lang.Text("Tools.GameLink.Loading.DownloadingModule")
                : string.Empty
        };

        var eulaAccepted = !_forceEulaPage && TryGetEulaAccepted();
        var connected = state == LobbyState.Connected;
        var operationBusy = Volatile.Read(ref _operationBusy) == 1;
        var canSelect = eulaAccepted && !loading && !connected;

        ShowEula = !eulaAccepted && !loading;
        ShowSelect = canSelect;
        ShowFinish = eulaAccepted && connected;

        if (connected)
        {
            UpdateConnectedInfo();
            RebuildPlayerRows();
        }
        else
        {
            ClearFinishState();
        }

        if (canSelect)
        {
            WorldListEnabled = Worlds.Count > 0 && !operationBusy;
            CreateEnabled = SelectedWorld is not null && !operationBusy;
            JoinEnabled = !operationBusy;
            InputPortEnabled = !operationBusy;
        }
        else
        {
            WorldListEnabled = false;
            CreateEnabled = false;
            JoinEnabled = false;
            InputPortEnabled = false;
        }

        EulaStopEnabled =
            Volatile.Read(ref _eulaBusy) == 0 && Volatile.Read(ref _operationBusy) == 0;
        AccountEnabled = !_accountBusy && Volatile.Read(ref _accountActionBusy) == 0;
        NatTestEnabled = Volatile.Read(ref _natTestBusy) == 0;
        UpdateAccountDisplay();
    }

    private bool TryGetEulaAccepted()
    {
        try
        {
            return States.Link.LinkEula;
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Link", "读取联机大厅协议状态失败");
            if (!_eulaReadErrorShown)
            {
                _eulaReadErrorShown = true;
                HintService.Show(Hints.Error(ex.Message));
            }

            return false;
        }
    }

    private void ClearFinishState()
    {
        _lastPing = 0;
        FinishIdText = "—";
        FinishPingText = Lang.Text("Tools.GameLink.Finish.Checking");
        FinishPingVisible = false;
        FinishQualityText = Lang.Text("Tools.GameLink.Finish.Checking");
        FinishQualityToolTip = Lang.Text("Tools.GameLink.Finish.ConnectionStatus");
        ConnectTypeVisible = false;
        ConnectTypeText = Lang.Text("Tools.GameLink.Finish.Unavailable");
        ConnectUserNameText = string.Empty;
        ConnectUserTypeText = string.Empty;
        FinishCopyIpVisible = false;
        FinishCopyEnabled = false;
        FinishExitEnabled = false;
        FinishExitText = Lang.Text("Tools.GameLink.Finish.Exit");
        EasyTierVersionWarningVisible = false;
        PlayerRows.Clear();
        PlayerListTitle = Lang.Text("Tools.GameLink.Member.ListLoading");
    }

    private void UpdateConnectedInfo()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(UpdateConnectedInfo);
            return;
        }

        if (LobbyService.CurrentState != LobbyState.Connected)
            return;

        var isHost = LobbyService.IsHost;
        var code = LobbyService.CurrentLobbyCode;
        FinishIdText = string.IsNullOrWhiteSpace(code) ? "—" : code!;
        FinishCopyEnabled = !string.IsNullOrWhiteSpace(code);
        ConnectUserNameText = string.IsNullOrWhiteSpace(LobbyService.CurrentUserName)
            ? Lang.Text("Tools.GameLink.Finish.Guest")
            : LobbyService.CurrentUserName!;
        ConnectUserTypeText = isHost
            ? Lang.Text("Tools.GameLink.Finish.Host")
            : Lang.Text("Tools.GameLink.Finish.Guest");
        FinishExitText = isHost
            ? Lang.Text("Tools.GameLink.Finish.CloseLobby")
            : Lang.Text("Tools.GameLink.Finish.Exit");
        FinishExitEnabled =
            Volatile.Read(ref _eulaBusy) == 0 && Volatile.Read(ref _operationBusy) == 0;
        FinishCopyIpVisible = !isHost && LobbyInfoProvider.McForward?.LocalEndPoint is not null;
        ConnectTypeVisible = !isHost;
        FinishPingVisible = !isHost;

        if (isHost)
        {
            var qualityIndex = _natStatus?.UdpNatType switch
            {
                CliNetTest.NatType.OpenInternet
                    or CliNetTest.NatType.NoPat
                    or CliNetTest.NatType.FullCone => 3,
                CliNetTest.NatType.Symmetric
                    or CliNetTest.NatType.SymmetricEasy
                    or CliNetTest.NatType.SymmetricFirewall
                    or CliNetTest.NatType.UdpBlocked => 1,
                _ => _natStatus is null ? 3 : 2
            };
            var quality = LobbyTextHandler.GetQualityDesc(qualityIndex);
            ConnectTypeText = Lang.Text("Link.Connection.Local");
            FinishQualityText = quality.Keyword;
            FinishQualityToolTip = quality.Desc;
            FinishPingText = _lastPing > 0
                ? Lang.Text("Tools.GameLink.Finish.PingMs", _lastPing)
                : Lang.Text("Tools.GameLink.Finish.Unavailable");
        }
        else
        {
            var hostInfo = LobbyService.CurrentHostInfo;
            if (hostInfo is null)
            {
                ConnectTypeText = Lang.Text("Tools.GameLink.Finish.Unavailable");
                FinishQualityText = Lang.Text("Tools.GameLink.Finish.Connected");
                FinishQualityToolTip = Lang.Text("Tools.GameLink.Finish.ConnectionStatus");
                FinishPingText = _lastPing > 0
                    ? Lang.Text("Tools.GameLink.Finish.PingMs", _lastPing)
                    : Lang.Text("Tools.GameLink.Finish.Checking");
            }
            else
            {
                var quality = hostInfo.Way switch
                {
                    ConnectionWay.Local or ConnectionWay.P2P => 3,
                    ConnectionWay.Relay => 2,
                    _ => 1
                };
                if (hostInfo.Ping > 300 || hostInfo.Loss > 5 || _lastPing > 300)
                    quality = 1;
                else if (hostInfo.Loss > 2 || _lastPing > 150)
                    quality = Math.Min(quality, 2);

                var qualityDesc = LobbyTextHandler.GetQualityDesc(quality);
                ConnectTypeText = hostInfo.Way switch
                {
                    ConnectionWay.Local => Lang.Text("Link.Connection.Local"),
                    ConnectionWay.P2P => Lang.Text("Link.Connection.P2P"),
                    ConnectionWay.Relay => Lang.Text("Link.Connection.Relay"),
                    _ => Lang.Text("Link.Connection.Unknown")
                };
                FinishQualityText = qualityDesc.Keyword;
                FinishQualityToolTip = qualityDesc.Desc;
                FinishPingText = _lastPing > 0
                    ? Lang.Text("Tools.GameLink.Finish.PingMs", _lastPing)
                    : hostInfo.Ping > 0
                        ? Lang.Text("Tools.GameLink.Finish.PingMs", (long)hostInfo.Ping)
                        : Lang.Text("Tools.GameLink.Finish.Checking");
            }
        }

        EasyTierVersionWarningVisible = HasEasyTierVersionMismatch(isHost);
    }

    private static bool HasEasyTierVersionMismatch(bool isHost)
    {
        var current = EasyTierMetadata.CurrentEasyTierVer;
        if (!isHost
            && LobbyService.CurrentHostInfo is { EasyTierVer: { Length: > 0 } hostVersion }
            && !VersionMatches(hostVersion, current))
            return true;

        foreach (var player in LobbyService.Players)
        {
            if (string.IsNullOrWhiteSpace(player.Vendor))
                continue;
            if (!player.Vendor.Contains("EasyTier", StringComparison.OrdinalIgnoreCase)
                || !player.Vendor.Contains(current, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool VersionMatches(string actual, string expected)
    {
        return string.Equals(
            actual.Trim().TrimStart('v', 'V'),
            expected.Trim().TrimStart('v', 'V'),
            StringComparison.OrdinalIgnoreCase);
    }

    private void RebuildPlayerRows()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(RebuildPlayerRows);
            return;
        }

        PlayerRows.Clear();
        foreach (var player in LobbyService.Players.ToList())
        {
            var isHost = player.Kind == PlayerKind.HOST;
            var info = isHost
                ? Lang.Text(
                    "Tools.GameLink.Player.Details",
                    Lang.Text("Tools.GameLink.Player.Host"),
                    player.Vendor)
                : player.Vendor;
            var row = new GameLinkPlayerRow
            {
                Name = player.Name,
                Info = info,
                Vendor = player.Vendor,
                MachineId = player.MachineId
            };
            row.ShowInfoCommand = new AsyncRelayCommand(() => ShowPlayerInfoAsync(row));
            PlayerRows.Add(row);
        }

        PlayerListTitle = Lang.Text("Tools.GameLink.Member.ListCount", PlayerRows.Count);
        if (LobbyService.CurrentState == LobbyState.Connected)
            EasyTierVersionWarningVisible = HasEasyTierVersionMismatch(LobbyService.IsHost);
    }

    #endregion

    #region 公告

    private void BeginAnnouncementReload()
    {
        var requestId = BeginAnnouncementRequest();
        Suspend();

        _announcementCarouselCts = new CancellationTokenSource();
        _announcementIndex = 0;
        Volatile.Write(ref _announcementItems, Array.Empty<LinkAnnounceInfo>());
        SetAnnouncementState(AnnouncementLoadState.Loading, AnnouncementFailureKind.None);
        AnnouncementThemeIndex = 0;
        AnnouncementText = Lang.Text("Tools.GameLink.Loading.ConnectingServer");
        AnnouncementVisible = true;

        _ = FetchAnnouncementAsync(requestId);
        _ = RunAnnouncementCarouselAsync(_announcementCarouselCts.Token);
    }

    private int BeginAnnouncementRequest()
    {
        lock (_announcementLock)
        {
            return ++_announcementRequestId;
        }
    }

    private bool IsCurrentAnnouncementRequest(int requestId)
    {
        lock (_announcementLock)
        {
            return requestId == _announcementRequestId;
        }
    }

    private bool TryApplyCurrentAnnouncementRequest(int requestId, Action action)
    {
        lock (_announcementLock)
        {
            if (requestId != _announcementRequestId)
                return false;
            action();
            return true;
        }
    }

    private AnnouncementLoadState CurrentAnnouncementState =>
        (AnnouncementLoadState)Volatile.Read(ref _announcementState);

    private AnnouncementFailureKind CurrentAnnouncementFailure =>
        (AnnouncementFailureKind)Volatile.Read(ref _announcementFailureKind);

    private void SetAnnouncementState(
        AnnouncementLoadState state,
        AnnouncementFailureKind failureKind)
    {
        Volatile.Write(ref _announcementState, (int)state);
        Volatile.Write(ref _announcementFailureKind, (int)failureKind);
    }

    private async Task RunAnnouncementCarouselAsync(CancellationToken token)
    {
        var index = 0;
        try
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(AnnouncementIntervalMilliseconds, token).ConfigureAwait(false);
                if (token.IsCancellationRequested)
                    break;

                if (CurrentAnnouncementState != AnnouncementLoadState.Loaded)
                    continue;

                var items = Volatile.Read(ref _announcementItems);
                if (items.Length == 0)
                {
                    RunOnUi(() =>
                    {
                        if (CurrentAnnouncementState == AnnouncementLoadState.Loaded)
                            AnnouncementVisible = false;
                    });
                    continue;
                }

                index = (index + 1) % items.Length;
                var next = index;
                RunOnUi(() =>
                {
                    if (CurrentAnnouncementState != AnnouncementLoadState.Loaded)
                        return;
                    ShowAnnouncement(next);
                });
            }
        }
        catch (OperationCanceledException)
        {
            // 页面刷新或离开时正常结束轮播。
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Link", "联机大厅公告轮播失败");
        }
    }

    private void ShowAnnouncement(int index)
    {
        var items = Volatile.Read(ref _announcementItems);
        if (items.Length == 0)
        {
            AnnouncementVisible = false;
            return;
        }

        _announcementIndex = Math.Abs(index) % items.Length;
        var info = items[_announcementIndex];
        var (prefix, theme) = info.Type switch
        {
            LinkAnnounceType.Important => (
                Lang.Text("Tools.GameLink.Announcement.Important"), 1),
            LinkAnnounceType.Warning => (
                Lang.Text("Tools.GameLink.Announcement.Warning"), 2),
            _ => (
                Lang.Text("Tools.GameLink.Announcement.Notice"), 0)
        };

        AnnouncementThemeIndex = theme;
        AnnouncementText = Lang.Text(
            "Tools.GameLink.Announcement.Format",
            prefix,
            info.Content.Replace("\r\n", "\n").Replace("\r", "\n"));
        AnnouncementVisible = true;
    }

    private void RefreshAnnouncementText()
    {
        switch (CurrentAnnouncementState)
        {
            case AnnouncementLoadState.Loading:
                AnnouncementThemeIndex = 0;
                AnnouncementText = Lang.Text("Tools.GameLink.Loading.ConnectingServer");
                AnnouncementVisible = true;
                break;
            case AnnouncementLoadState.Loaded:
                if (Volatile.Read(ref _announcementItems).Length == 0)
                    AnnouncementVisible = false;
                else
                    ShowAnnouncement(_announcementIndex);
                break;
            case AnnouncementLoadState.Failed:
                AnnouncementThemeIndex = 1;
                AnnouncementText =
                    CurrentAnnouncementFailure == AnnouncementFailureKind.UpdateRequired
                        ? Lang.Text("Tools.GameLink.Error.UpdateRequired")
                        : Lang.Text("Tools.GameLink.Error.ConnectFailed");
                AnnouncementVisible = true;
                break;
        }
    }

    private async Task FetchAnnouncementAsync(int requestId)
    {
        try
        {
            var root = await RequestAnnouncementAsync(requestId).ConfigureAwait(false);
            if (!IsCurrentAnnouncementRequest(requestId))
                return;

            if (root["available"] is null)
                throw new InvalidDataException("联机大厅公告缺少可用状态");

            var available = ReadBool(root["available"], false);
            var allowCustomName = ReadBool(root["allowCustomName"], false);
            var requiresLogin = ReadBool(root["requireLogin"], true);
            var requiresRealName = ReadBool(root["requireRealname"], true);
            var protocolVersion = ReadDouble(root["version"], double.NaN);
            if (double.IsNaN(protocolVersion))
                throw new InvalidDataException("联机大厅公告缺少协议版本号");

            if (protocolVersion > LobbyInfoProvider.ProtocolVersion)
            {
                LobbyInfoProvider.IsLobbyAvailable = false;
                SetAnnouncementState(
                    AnnouncementLoadState.Failed,
                    AnnouncementFailureKind.UpdateRequired);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (!IsCurrentAnnouncementRequest(requestId))
                        return;
                    AnnouncementThemeIndex = 1;
                    AnnouncementText = Lang.Text("Tools.GameLink.Error.UpdateRequired");
                    AnnouncementVisible = true;
                });
                return;
            }

            var announcements = ParseAnnouncements(root);
            var relays = ParseRelays(root);

            if (!IsCurrentAnnouncementRequest(requestId))
                return;

            LobbyInfoProvider.IsLobbyAvailable = available;
            LobbyInfoProvider.AllowCustomName = allowCustomName;
            LobbyInfoProvider.RequiresLogin = requiresLogin;
            LobbyInfoProvider.RequiresRealName = requiresRealName;
            ETRelay.RelayList = relays;

            Volatile.Write(ref _announcementItems, announcements.ToArray());
            SetAnnouncementState(AnnouncementLoadState.Loaded, AnnouncementFailureKind.None);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!IsCurrentAnnouncementRequest(requestId))
                    return;
                _announcementIndex = 0;
                if (announcements.Count == 0)
                    AnnouncementVisible = false;
                else
                    ShowAnnouncement(0);
                EnsureAccountState();
            });
        }
        catch (OperationCanceledException)
        {
            // 请求已被新的 Reload 作废，不再覆盖当前界面。
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Link", "获取联机大厅公告失败");
            if (!IsCurrentAnnouncementRequest(requestId))
                return;

            LobbyInfoProvider.IsLobbyAvailable = false;
            SetAnnouncementState(AnnouncementLoadState.Failed, AnnouncementFailureKind.Connect);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!IsCurrentAnnouncementRequest(requestId))
                    return;
                AnnouncementThemeIndex = 1;
                AnnouncementText = Lang.Text("Tools.GameLink.Error.ConnectFailed");
                AnnouncementVisible = true;
            });
        }
    }

    private async Task<JsonObject> RequestAnnouncementAsync(int requestId)
    {
        JsonObject? result = null;
        Exception? lastError = null;

        foreach (var rawServer in Secrets.LinkServers)
        {
            var server = rawServer.Trim().TrimEnd('/');
            if (string.IsNullOrWhiteSpace(server))
                continue;

            try
            {
                var cacheVersionText = await FetchStringAsync($"{server}/api/link/v2/cache.ini")
                    .ConfigureAwait(false);
                if (!int.TryParse(
                        cacheVersionText.Trim(),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var cacheVersion))
                    throw new InvalidDataException("联机大厅缓存版本号无效");

                if (cacheVersion == States.Link.AnnounceCacheVer
                    && !string.IsNullOrWhiteSpace(States.Link.AnnounceCache))
                {
                    result = ParseAnnouncementRoot(States.Link.AnnounceCache);
                }
                else
                {
                    var received = await FetchStringAsync($"{server}/api/link/v2/announce.json")
                        .ConfigureAwait(false);
                    var parsed = ParseAnnouncementRoot(received);
                    if (!TryApplyCurrentAnnouncementRequest(
                            requestId,
                            () =>
                            {
                                States.Link.AnnounceCache = received;
                                States.Link.AnnounceCacheVer = cacheVersion;
                            }))
                        throw new OperationCanceledException();

                    result = parsed;
                }

                break;
            }
            catch (Exception ex)
            {
                lastError = ex;
                LogWrapper.Error(ex, "Link", $"从服务器 {server} 获取联机大厅公告失败");
                if (!TryApplyCurrentAnnouncementRequest(
                        requestId,
                        () =>
                        {
                            States.Link.AnnounceCache = string.Empty;
                            States.Link.AnnounceCacheVer = 0;
                        }))
                    throw new OperationCanceledException();
            }
        }

        if (result is not null)
            return result;

        throw new InvalidOperationException(
            "无法从任何联机大厅服务器获取公告",
            lastError);
    }

    private static async Task<string> FetchStringAsync(string url)
    {
        using var cts = new CancellationTokenSource(AnnouncementRequestTimeoutMilliseconds);
        using var response = await HttpRequest
            .Create(url)
            .SendAsync(cancellationToken: cts.Token)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.AsStringAsync(cts.Token).ConfigureAwait(false);
    }

    private static JsonObject ParseAnnouncementRoot(string text)
    {
        return JsonCompat.ParseNode(text) as JsonObject
            ?? throw new InvalidDataException("联机大厅公告格式无效");
    }

    private static List<LinkAnnounceInfo> ParseAnnouncements(JsonObject root)
    {
        if (root["notices"] is not JsonArray notices)
            throw new InvalidDataException("联机大厅公告列表格式无效");

        var result = new List<LinkAnnounceInfo>();
        foreach (var noticeNode in notices)
        {
            if (noticeNode is not JsonObject notice)
                continue;

            var content = notice["content"]?.ToString();
            if (string.IsNullOrWhiteSpace(content))
                continue;

            var minVersion = ReadDouble(notice["minVer"], 0d);
            var maxVersion = ReadDouble(notice["maxVer"], double.MaxValue);
            if (Basics.VersionCode < minVersion || Basics.VersionCode > maxVersion)
                continue;

            var typeText = notice["type"]?.ToString().ToLowerInvariant();
            var type = typeText switch
            {
                "important" or "red" => LinkAnnounceType.Important,
                "warning" or "yellow" => LinkAnnounceType.Warning,
                _ => LinkAnnounceType.Notice
            };

            foreach (var line in content.Split(
                         '\n',
                         StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                result.Add(new LinkAnnounceInfo(type, line));
        }

        return result;
    }

    private static List<ETRelay> ParseRelays(JsonObject root)
    {
        if (root["relays"] is not JsonArray relays)
            throw new InvalidDataException("联机大厅中继列表格式无效");

        var result = new List<ETRelay>();
        foreach (var relayNode in relays)
        {
            if (relayNode is not JsonObject relay)
                continue;

            var name = relay["name"]?.ToString();
            var url = relay["url"]?.ToString();
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url))
                continue;

            result.Add(
                new ETRelay
                {
                    Name = name,
                    Url = url,
                    Type = string.Equals(
                        relay["type"]?.ToString(),
                        "official",
                        StringComparison.OrdinalIgnoreCase)
                        ? ETRelayType.Selfhosted
                        : ETRelayType.Community
                });
        }

        return result;
    }

    private static bool ReadBool(JsonNode? node, bool fallback)
    {
        if (node is null)
            return fallback;

        try
        {
            return node.GetValue<bool>();
        }
        catch (Exception)
        {
            return bool.TryParse(node.ToString(), out var value) ? value : fallback;
        }
    }

    private static double ReadDouble(JsonNode? node, double fallback)
    {
        if (node is null)
            return fallback;

        return double.TryParse(
            node.ToString(),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var value)
            ? value
            : fallback;
    }

    #endregion

    #region 初始化 / 世界发现

    private async Task EnsureInitializedAsync()
    {
        if (_initializing)
            return;

        if (_initialized && LobbyService.CurrentState is not (LobbyState.Idle or LobbyState.Error))
        {
            RefreshLayout();
            return;
        }

        _initializing = true;
        _initFailed = false;
        _initErrorText = null;
        RefreshLayout();
        try
        {
            await LobbyService.InitializeAsync().ConfigureAwait(true);
            if (_initCancelRequested)
            {
                _initCancelRequested = false;
                _initialized = false;
                await LobbyService.LeaveLobbyAsync().ConfigureAwait(true);
                return;
            }

            if (LobbyService.CurrentState == LobbyState.Error)
                throw new InvalidOperationException(
                    Lang.Text("Tools.GameLink.Error.ConnectFailed"));

            _initialized = true;
            await RefreshWorldsAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Link", "初始化联机大厅模块失败");
            _initialized = false;
            _initFailed = true;
            _initErrorText = ex.Message;
            HintService.Show(Hints.Error(Lang.Text("Tools.GameLink.Error.ConnectFailed")));
        }
        finally
        {
            _initializing = false;
            _initCancelRequested = false;
            RefreshLayout();
        }
    }

    public async Task RefreshWorldsAsync()
    {
        if (_refreshingWorlds)
            return;

        if (!_initialized)
        {
            await EnsureInitializedAsync().ConfigureAwait(true);
            return;
        }

        _refreshingWorlds = true;
        try
        {
            if (LobbyService.CurrentState is LobbyState.Initialized or LobbyState.Idle)
                await LobbyService.DiscoverWorldAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Link", "探测局域网世界失败");
            HintService.Show(Hints.Warning(ex.Message));
        }
        finally
        {
            _refreshingWorlds = false;
        }
    }

    #endregion

    #region 创建 / 加入 / 退出

    public async Task CreateLobbyAsync()
    {
        var world = SelectedWorld;
        if (world is null)
        {
            HintService.Show(Hints.Info(Lang.Text("Tools.GameLink.Create.NoWorld")));
            return;
        }

        if (!TryBeginOperation())
            return;

        try
        {
            if (!await PrecheckAsync().ConfigureAwait(true))
                return;

            await CreateLobbyCoreAsync(world.Port).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Link", "创建联机大厅失败");
            HintService.Show(Hints.Error(Lang.Text("Tools.GameLink.Error.ConnectFailed")));
            await CleanupLobbyAsync().ConfigureAwait(true);
        }
        finally
        {
            EndOperation();
        }
    }

    public async Task JoinLobbyAsync()
    {
        var code = JoinCode.Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            HintService.Show(Hints.Info(Lang.Text("Tools.GameLink.Join.InvalidText")));
            return;
        }

        if (LobbyInfoProvider.ParseCode(code) is null)
        {
            HintService.Show(Hints.Info(Lang.Text("Tools.GameLink.Join.InvalidText")));
            return;
        }

        if (!TryBeginOperation())
            return;

        try
        {
            if (!await PrecheckAsync().ConfigureAwait(true))
                return;

            var username = GetUsername();
            if (string.IsNullOrWhiteSpace(username))
            {
                HintService.Show(Hints.Warning(Lang.Text("Link.Mod.UsernameOrLogin")));
                return;
            }

            PrepareFinish(false, code, username);
            var result = await LobbyService.JoinLobbyAsync(code, username).ConfigureAwait(true);
            if (!result)
            {
                await CleanupLobbyAsync().ConfigureAwait(true);
                return;
            }

            UpdateConnectedInfo();
            RefreshLayout();
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Link", "加入联机大厅失败");
            HintService.Show(Hints.Error(Lang.Text("Tools.GameLink.Error.ConnectFailed")));
            await CleanupLobbyAsync().ConfigureAwait(true);
        }
        finally
        {
            EndOperation();
        }
    }

    public async Task InputManualPortAsync()
    {
        if (!TryBeginOperation())
            return;

        try
        {
            if (!await PrecheckAsync().ConfigureAwait(true))
                return;

            var result = await Dialog
                .ShowAsync(
                    new InputDialogRequest
                    {
                        Title = Lang.Text("Tools.GameLink.Create.EnterPort"),
                        HintText = Lang.Text("Tools.GameLink.Create.EnterPort"),
                        Content = string.Empty,
                        ValidateRules = [new IntValidator(65535, 1024)],
                        HighLight = false,
                        Buttons = DialogButtons.OkCancel
                    })
                .ConfigureAwait(true);
            if (!result.IsConfirmed)
                return;

            var text = (result.InputText ?? string.Empty).Trim();
            if (!int.TryParse(
                    text,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var port)
                || port is < 1024 or > 65535)
            {
                HintService.Show(Hints.Error(Lang.Text("Tools.GameLink.Create.NotMcPort")));
                return;
            }

            var isMinecraftPort = false;
            try
            {
                using var ping = McPingServiceFactory.CreateService("127.0.0.1", port, 5000);
                var pingResult = await ping.PingAsync().ConfigureAwait(true);
                isMinecraftPort = pingResult is not null && pingResult.Version.Protocol != 0;
            }
            catch (Exception ex)
            {
                LogWrapper.Error(ex, "Link", $"探测本地端口 {port} 失败");
            }

            if (!isMinecraftPort)
            {
                HintService.Show(Hints.Error(Lang.Text("Tools.GameLink.Create.NotMcPort")));
                return;
            }

            await CreateLobbyCoreAsync(port).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Link", "通过端口创建联机大厅失败");
            HintService.Show(Hints.Error(Lang.Text("Tools.GameLink.Error.ConnectFailed")));
            await CleanupLobbyAsync().ConfigureAwait(true);
        }
        finally
        {
            EndOperation();
        }
    }

    private async Task CreateLobbyCoreAsync(int port)
    {
        if (port is < 1024 or > 65535)
        {
            HintService.Show(Hints.Error(Lang.Text("Tools.GameLink.Create.NotMcPort")));
            return;
        }

        var username = GetUsername();
        if (string.IsNullOrWhiteSpace(username))
        {
            HintService.Show(Hints.Warning(Lang.Text("Link.Mod.UsernameOrLogin")));
            return;
        }

        PrepareFinish(true, null, username);
        var result = await LobbyService.CreateLobbyAsync(port, username).ConfigureAwait(true);
        if (!result)
        {
            await CleanupLobbyAsync().ConfigureAwait(true);
            return;
        }

        UpdateConnectedInfo();
        RefreshLayout();
    }

    private void PrepareFinish(bool isHost, string? lobbyCode, string username)
    {
        _lastPing = 0;
        FinishIdText = string.IsNullOrWhiteSpace(lobbyCode) ? "—" : lobbyCode;
        FinishPingVisible = !isHost;
        FinishPingText = Lang.Text("Tools.GameLink.Finish.Checking");
        FinishQualityText = Lang.Text("Tools.GameLink.Finish.Checking");
        ConnectTypeVisible = !isHost;
        ConnectTypeText = Lang.Text("Tools.GameLink.Finish.Connecting");
        ConnectUserNameText = username;
        ConnectUserTypeText = isHost
            ? Lang.Text("Tools.GameLink.Finish.Host")
            : Lang.Text("Tools.GameLink.Finish.Guest");
        FinishCopyIpVisible = false;
        FinishCopyEnabled = false;
        FinishExitEnabled = false;
        FinishExitText = isHost
            ? Lang.Text("Tools.GameLink.Finish.CloseLobby")
            : Lang.Text("Tools.GameLink.Finish.Exit");
        EasyTierVersionWarningVisible = false;
        PlayerRows.Clear();
        PlayerListTitle = Lang.Text("Tools.GameLink.Member.ListLoading");
    }

    public async Task LeaveLobbyAsync()
    {
        if (Interlocked.Exchange(ref _operationBusy, 1) == 1)
            return;

        RefreshLayout();
        var left = false;
        try
        {
            var result = await Dialog
                .ShowAsync(
                    new TextDialogRequest
                    {
                        Title = Lang.Text("Tools.GameLink.Exit.ConfirmTitle"),
                        Content = LobbyService.IsHost
                            ? Lang.Text("Tools.GameLink.Exit.ConfirmMessageWithHost")
                            : Lang.Text("Tools.GameLink.Exit.ConfirmMessage"),
                        Buttons = DialogButtons.OkCancel,
                        IsWarn = true
                    })
                .ConfigureAwait(true);
            if (!result.IsConfirmed)
                return;

            await LobbyService.LeaveLobbyAsync().ConfigureAwait(true);
            left = true;
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Link", "退出联机大厅失败");
            HintService.Show(Hints.Error(Lang.Text("Tools.GameLink.Error.ServerExit")));
        }
        finally
        {
            if (left)
                ClearFinishState();
            EndOperation();
        }
    }

    public Task CancelLoadAsync()
    {
        if (!_initializing && LobbyService.CurrentState is not LobbyState.Initializing)
            return Task.CompletedTask;

        _initCancelRequested = true;
        RefreshLayout();
        return Task.CompletedTask;
    }

    private async Task CleanupLobbyAsync()
    {
        try
        {
            if (LobbyService.CurrentState is not (LobbyState.Idle or LobbyState.Initialized))
                await LobbyService.LeaveLobbyAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Link", "清理联机大厅失败");
            HintService.Show(Hints.Error(Lang.Text("Tools.GameLink.Error.ServerExit")));
        }

        RunOnUi(() =>
        {
            ClearFinishState();
            RefreshLayout();
        });
    }

    private bool TryBeginOperation()
    {
        if (Interlocked.CompareExchange(ref _operationBusy, 1, 0) != 0)
            return false;

        RefreshLayout();
        return true;
    }

    private void EndOperation()
    {
        Interlocked.Exchange(ref _operationBusy, 0);
        RunOnUi(RefreshLayout);
    }

    private static string GetUsername()
    {
        return LobbyInfoProvider.GetUsername()?.Trim() ?? string.Empty;
    }

    public string? GetCopyIpAddress()
    {
        var port = LobbyInfoProvider.McForward?.LocalEndPoint?.Port;
        return port is null ? null : $"127.0.0.1:{port.Value}";
    }

    public async Task ShowPlayerInfoAsync(GameLinkPlayerRow row)
    {
        try
        {
            await Dialog
                .ShowAsync(
                    new TextDialogRequest
                    {
                        Title = Lang.Text("Tools.GameLink.Player.InfoTitle", row.Name),
                        Content = Lang.Text(
                            "Tools.GameLink.Player.InfoMessage",
                            row.Name,
                            row.Vendor),
                        Buttons = DialogButtons.Ok
                    })
                .ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Link", "显示玩家信息失败");
            HintService.Show(Hints.Error(ex.Message));
        }
    }

    #endregion

    #region 账号

    private void EnsureAccountState()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(EnsureAccountState);
            return;
        }

        if (_accountBusy)
            return;

        if (string.IsNullOrWhiteSpace(States.Link.NaidRefreshToken))
        {
            _accountRefreshFailed = false;
            UpdateAccountDisplay();
            return;
        }

        if (IsNaidTokenExpired())
        {
            ClearNaidSession();
            if (!_tokenExpiredHintShown)
            {
                _tokenExpiredHintShown = true;
                HintService.Show(Hints.Error(Lang.Text("Tools.GameLink.Natayark.TokenExpired")));
            }

            UpdateAccountDisplay();
            return;
        }

        if (string.IsNullOrWhiteSpace(NatayarkProfileManager.NaidProfile.Username))
        {
            _ = RefreshNaidProfileAsync();
            return;
        }

        _accountRefreshFailed = false;
        UpdateAccountDisplay();
    }

    private static bool IsNaidTokenExpired()
    {
        var expireText = States.Link.NaidRefreshExpireTime;
        return !string.IsNullOrWhiteSpace(expireText)
            && DateTime.TryParse(expireText, out var expireTime)
            && expireTime < DateTime.Now;
    }

    private async Task RefreshNaidProfileAsync()
    {
        if (_accountBusy)
            return;

        var version = Interlocked.Increment(ref _accountRefreshVersion);
        _accountBusy = true;
        _accountRefreshFailed = false;
        UpdateAccountDisplay();
        RefreshLayout();
        try
        {
            var token = States.Link.NaidRefreshToken;
            if (string.IsNullOrWhiteSpace(token))
                return;

            if (IsNaidTokenExpired())
            {
                ClearNaidSession();
                if (!_tokenExpiredHintShown)
                {
                    _tokenExpiredHintShown = true;
                    HintService.Show(
                        Hints.Error(Lang.Text("Tools.GameLink.Natayark.TokenExpired")));
                }

                return;
            }

            await NatayarkProfileManager.GetNaidDataAsync(token, true).ConfigureAwait(false);
            if (version != Volatile.Read(ref _accountRefreshVersion))
                return;

            _accountRefreshFailed = false;
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Link", "刷新 Natayark 账号信息失败");
            if (version == Volatile.Read(ref _accountRefreshVersion))
                _accountRefreshFailed = true;
        }
        finally
        {
            _accountBusy = false;
            RunOnUi(() =>
            {
                UpdateAccountDisplay();
                RefreshLayout();
            });
        }
    }

    private void UpdateAccountDisplay()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(UpdateAccountDisplay);
            return;
        }

        if (_accountLoginPending)
        {
            AccountText = Lang.Text("Tools.GameLink.Natayark.BrowserContinue");
            AccountOpacity = 0.6d;
            return;
        }

        if (string.IsNullOrWhiteSpace(States.Link.NaidRefreshToken))
        {
            AccountText = Lang.Text("Tools.GameLink.Natayark.Login");
            AccountOpacity = 1d;
            return;
        }

        var profile = NatayarkProfileManager.NaidProfile;
        if (_accountBusy)
        {
            AccountText = string.IsNullOrWhiteSpace(profile.Username)
                ? Lang.Text("Tools.GameLink.Natayark.Loading")
                : profile.Username!;
            AccountOpacity = 0.6d;
            return;
        }

        if (_accountRefreshFailed)
        {
            AccountText = Lang.Text("Tools.GameLink.Natayark.FetchFailed");
            AccountOpacity = 0.6d;
            return;
        }

        if (string.IsNullOrWhiteSpace(profile.Username))
        {
            AccountText = Lang.Text("Tools.GameLink.Natayark.Loading");
            AccountOpacity = 0.6d;
            return;
        }

        if (profile.Status == 0)
        {
            AccountText = profile.Username ?? string.Empty;
            AccountOpacity = 1d;
            return;
        }

        AccountText = $"{profile.Username} {Lang.Text("Tools.GameLink.Natayark.Abnormal")}";
        AccountOpacity = 0.6d;
    }

    public async Task ToggleNatayarkAccountAsync()
    {
        if (Interlocked.CompareExchange(ref _accountActionBusy, 1, 0) != 0)
            return;

        try
        {
            RefreshLayout();
            if (_accountBusy)
                return;

            var token = States.Link.NaidRefreshToken;
            if (!string.IsNullOrWhiteSpace(token) && IsNaidTokenExpired())
            {
                ClearNaidSession();
                token = string.Empty;
                UpdateAccountDisplay();
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                var loginResult = await Dialog
                    .ShowAsync(
                        new TextDialogRequest
                        {
                            Title = Lang.Text("Tools.GameLink.Natayark.LoginTitle"),
                            Content = Lang.Text("Tools.GameLink.Natayark.LoginPrompt"),
                            Buttons = DialogButtons.From(
                                DialogButton.Create(Lang.Text("Tools.GameLink.Natayark.Continue")),
                                DialogButton.Create(Lang.Text("Common.Action.Cancel")))
                        })
                    .ConfigureAwait(true);
                if (!loginResult.IsConfirmed)
                    return;

                _accountBusy = true;
                _accountLoginPending = true;
                _accountRefreshFailed = false;
                var loginVersion = Interlocked.Increment(ref _loginAttemptVersion);
                _ = ResetLoginPendingAfterTimeoutAsync(loginVersion);
                UpdateAccountDisplay();
                RefreshLayout();
                try
                {
                    if (WebServer.IsWebServerRunning("NatayarkID"))
                    {
                        _accountLoginPending = false;
                        _accountBusy = false;
                        HintService.Show(
                            Hints.Error(Lang.Text("Tools.GameLink.Natayark.LoginFailed")));
                        UpdateAccountDisplay();
                        RefreshLayout();
                        return;
                    }

                    WebServer.StartNaidAuthorize(() => RunOnUi(() =>
                    {
                        Interlocked.Increment(ref _loginAttemptVersion);
                        _accountLoginPending = false;
                        _accountBusy = false;
                        if (string.IsNullOrWhiteSpace(States.Link.NaidRefreshToken))
                            HintService.Show(
                                Hints.Error(Lang.Text("Tools.GameLink.Natayark.LoginFailed")));
                        else
                            HintService.Show(
                                Hints.Success(Lang.Text("Tools.GameLink.Natayark.LoginComplete")));

                        _accountRefreshFailed = false;
                        UpdateAccountDisplay();
                        RefreshLayout();
                    }));
                }
                catch (Exception ex)
                {
                    _accountLoginPending = false;
                    _accountBusy = false;
                    LogWrapper.Error(ex, "Link", "启动 Natayark 登录失败");
                    HintService.Show(Hints.Error(Lang.Text("Tools.GameLink.Natayark.LoginFailed")));
                    UpdateAccountDisplay();
                    RefreshLayout();
                }

                return;
            }

            var logoutResult = await Dialog
                .ShowAsync(
                    new TextDialogRequest
                    {
                        Title = Lang.Text("Tools.GameLink.Natayark.LogoutTitle"),
                        Content = Lang.Text("Tools.GameLink.Natayark.LogoutConfirm"),
                        Buttons = DialogButtons.OkCancel
                    })
                .ConfigureAwait(true);
            if (!logoutResult.IsConfirmed)
                return;

            ClearNaidSession();
            HintService.Show(Hints.Success(Lang.Text("Tools.GameLink.Natayark.LogoutComplete")));
            UpdateAccountDisplay();
            RefreshLayout();
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Link", "切换 Natayark Network 登录状态失败");
            HintService.Show(Hints.Error(Lang.Text("Tools.GameLink.Natayark.LoginFailed")));
        }
        finally
        {
            Interlocked.Exchange(ref _accountActionBusy, 0);
            RunOnUi(RefreshLayout);
        }
    }

    private async Task ResetLoginPendingAfterTimeoutAsync(int version)
    {
        await Task.Delay(TimeSpan.FromMinutes(3)).ConfigureAwait(false);
        if (version != Volatile.Read(ref _loginAttemptVersion))
            return;

        RunOnUi(() =>
        {
            if (version != Volatile.Read(ref _loginAttemptVersion) || !_accountLoginPending)
                return;

            _accountLoginPending = false;
            _accountBusy = false;
            HintService.Show(Hints.Error(Lang.Text("Tools.GameLink.Natayark.LoginFailed")));
            UpdateAccountDisplay();
            RefreshLayout();
        });
    }

    private void ClearNaidSession()
    {
        Interlocked.Increment(ref _accountRefreshVersion);
        try
        {
            States.Link.NaidRefreshToken = string.Empty;
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Link", "清除 Natayark 登录令牌失败");
        }

        try
        {
            States.Link.NaidRefreshExpireTime = string.Empty;
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Link", "清除 Natayark 令牌有效期失败");
        }

        NatayarkProfileManager.ResetProfile();
        _accountRefreshFailed = false;
        _accountLoginPending = false;
        _tokenExpiredHintShown = false;
    }

    #endregion

    #region NAT / EasyTier

    public async Task RunNatTestAsync()
    {
        if (Interlocked.CompareExchange(ref _natTestBusy, 1, 0) != 0)
            return;

        _natStatus = null;
        _natTestFailed = false;
        NatTestEnabled = false;
        NatTypeText = Lang.Text("Tools.GameLink.Nat.Testing");
        RefreshLayout();
        try
        {
            if (!EasyTierMetadata.IsInstalled)
            {
                await DownloadEasyTierAsync().ConfigureAwait(true);
                if (!EasyTierMetadata.IsInstalled)
                {
                    _natTestFailed = true;
                    return;
                }
            }

            var status = await Task
                .Run(() => CliNetTest.GetNetStatusAsync())
                .WaitAsync(TimeSpan.FromSeconds(20))
                .ConfigureAwait(true);
            if (status is null)
                throw new InvalidOperationException(Lang.Text("Tools.GameLink.Nat.Failed"));

            _natStatus = status;
            _natTestFailed = false;
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Link", "获取网络测试结果失败");
            _natStatus = null;
            _natTestFailed = true;
            HintService.Show(Hints.Error(Lang.Text("Tools.GameLink.Error.NetworkTestFailed")));
        }
        finally
        {
            Interlocked.Exchange(ref _natTestBusy, 0);
            NatTestEnabled = true;
            ApplyNatText();
            RefreshLayout();
        }
    }

    private void ApplyNatText()
    {
        if (Volatile.Read(ref _natTestBusy) == 1)
        {
            NatTypeText = Lang.Text("Tools.GameLink.Nat.Testing");
            return;
        }

        if (_natStatus is not null)
        {
            NatTypeText = Lang.Text(
                "Tools.GameLink.Nat.Result",
                CliNetTest.GetNatTypeString(_natStatus.UdpNatType),
                CliNetTest.GetNatTypeString(_natStatus.TcpNatType));
            return;
        }

        NatTypeText = Lang.Text(
            _natTestFailed
                ? "Tools.GameLink.Nat.Failed"
                : "Tools.GameLink.Nat.Test");
    }

    private void UpdateNatText()
    {
        ApplyNatText();
    }

    private void OnNeedDownloadEasyTier()
    {
        _ = DownloadEasyTierAsync();
    }

    private async Task DownloadEasyTierAsync()
    {
        if (!await _easyTierDownloadLock.WaitAsync(0).ConfigureAwait(false))
        {
            HintService.Show(Hints.Info(Lang.Text("Link.Mod.EasyTierNotReady")));
            return;
        }

        Interlocked.Exchange(ref _easyTierDownloading, 1);
        try
        {
            HintService.Show(Hints.Info(Lang.Text("Link.Mod.DownloadingDeps")));

            var installed = await EasyTierInstaller
                .EnsureInstalledAsync()
                .ConfigureAwait(false);

            HintService.Show(
                installed
                    ? Hints.Success(Lang.Text("Link.Mod.DownloadComplete"))
                    : Hints.Error(Lang.Text("Link.Mod.DownloadEasyTierFailed")));
        }
        catch (PlatformNotSupportedException ex)
        {
            LogWrapper.Error(ex, "Link", "当前架构没有可用的 EasyTier 依赖包");
            HintService.Show(Hints.Error(ex.Message));
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Link", "下载 EasyTier 依赖文件失败");
            HintService.Show(Hints.Error(Lang.Text("Link.Mod.DownloadEasyTierFailed")));
        }
        finally
        {
            Interlocked.Exchange(ref _easyTierDownloading, 0);
            _easyTierDownloadLock.Release();
        }
    }

    #endregion

    #region 生命周期

    private void OnServerShutDown()
    {
        _ = HandleServerShutDownAsync();
    }

    private async Task HandleServerShutDownAsync()
    {
        if (Interlocked.Exchange(ref _serverLifecycleBusy, 1) == 1)
            return;

        try
        {
            await LobbyService.LeaveLobbyAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Link", "服务器关闭后退出大厅失败");
            HintService.Show(Hints.Error(Lang.Text("Tools.GameLink.Error.ServerExit")));
        }
        finally
        {
            RunOnUi(() =>
            {
                ClearFinishState();
                RefreshLayout();
            });
            Interlocked.Exchange(ref _serverLifecycleBusy, 0);
        }
    }

    private void OnServerException(Exception ex)
    {
        HintService.Show(Hints.Error(Lang.Text("Tools.GameLink.Error.ServerMessage", ex.Message)));
        _ = HandleServerExceptionAsync();
    }

    private async Task HandleServerExceptionAsync()
    {
        if (Interlocked.Exchange(ref _serverLifecycleBusy, 1) == 1)
            return;

        try
        {
            await LobbyService.LeaveLobbyAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Link", "服务器异常后退出大厅失败");
            HintService.Show(Hints.Error(Lang.Text("Tools.GameLink.Error.ServerExit")));
        }
        finally
        {
            RunOnUi(() =>
            {
                ClearFinishState();
                RefreshLayout();
            });
            Interlocked.Exchange(ref _serverLifecycleBusy, 0);
        }
    }

    private void OnUserStopGame()
    {
        _ = HandleUserStopGameAsync();
    }

    private async Task HandleUserStopGameAsync()
    {
        if (Interlocked.Exchange(ref _serverLifecycleBusy, 1) == 1)
            return;

        try
        {
            await Dialog
                .ShowAsync(
                    new TextDialogRequest
                    {
                        Title = Lang.Text("Tools.GameLink.Exit.DisbandedTitle"),
                        Content = Lang.Text("Tools.GameLink.Exit.Disbanded"),
                        Buttons = DialogButtons.Ok,
                        ForceWait = true
                    })
                .ConfigureAwait(true);
            await LobbyService.LeaveLobbyAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Link", "游戏停止后清理大厅失败");
            HintService.Show(Hints.Error(Lang.Text("Tools.GameLink.Error.ServerExit")));
        }
        finally
        {
            RunOnUi(() =>
            {
                ClearFinishState();
                RefreshLayout();
            });
            Interlocked.Exchange(ref _serverLifecycleBusy, 0);
        }
    }

    #endregion

    #region 协议

    public void AgreeEula()
    {
        try
        {
            States.Link.LinkEula = true;
            _forceEulaPage = false;
        }
        catch (Exception ex)
        {
            _forceEulaPage = true;
            LogWrapper.Error(ex, "Link", "写入大厅协议同意状态失败");
            HintService.Show(Hints.Error(ex.Message));
        }

        RefreshLayout();
    }

    public async Task RevokeEulaAsync()
    {
        if (Interlocked.Exchange(ref _eulaBusy, 1) == 1)
            return;

        try
        {
            RefreshLayout();
            var result = await Dialog
                .ShowAsync(
                    new TextDialogRequest
                    {
                        Title = Lang.Text("Tools.GameLink.Eula.RevokeTitle"),
                        Content = Lang.Text("Tools.GameLink.Eula.RevokeConfirm"),
                        Buttons = DialogButtons.OkCancel,
                        IsWarn = true
                    })
                .ConfigureAwait(true);
            if (!result.IsConfirmed)
                return;

            _forceEulaPage = true;
            try
            {
                if (LobbyService.CurrentState == LobbyState.Connected)
                    await LobbyService.LeaveLobbyAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                LogWrapper.Error(ex, "Link", "撤销协议时退出大厅失败");
                HintService.Show(Hints.Error(Lang.Text("Tools.GameLink.Error.ServerExit")));
            }

            ClearNaidSession();
            try
            {
                States.Link.LinkEula = false;
            }
            catch (Exception ex)
            {
                LogWrapper.Error(ex, "Link", "写入大厅协议撤销状态失败");
                HintService.Show(Hints.Error(ex.Message));
            }

            HintService.Show(Hints.Info(Lang.Text("Tools.GameLink.Eula.Disabled")));
            RunOnUi(() =>
            {
                ClearFinishState();
                UpdateAccountDisplay();
                RefreshLayout();
            });
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Link", "撤销大厅协议失败");
            HintService.Show(Hints.Error(ex.Message));
        }
        finally
        {
            Interlocked.Exchange(ref _eulaBusy, 0);
            RunOnUi(RefreshLayout);
        }
    }

    #endregion
}

/// <summary>世界下拉框中的一项，保留原始端口以便创建大厅。</summary>
public sealed record GameLinkWorldOption(string Name, int Port);

/// <summary>大厅成员列表行；主机与客户端标识直接来自玩家档案，不依赖列表下标。</summary>
public sealed class GameLinkPlayerRow
{
    public required string Name { get; init; }

    public required string Info { get; init; }

    public required string Vendor { get; init; }

    public required string MachineId { get; init; }

    public ICommand ShowInfoCommand { get; set; } = null!;
}