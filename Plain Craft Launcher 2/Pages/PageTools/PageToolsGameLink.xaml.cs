using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PCL.Core.App;
using PCL.Core.Link;
using PCL.Core.Link.EasyTier;
using PCL.Core.Link.Lobby;
using PCL.Core.Link.McPing;
using PCL.Core.Link.Natayark;
using PCL.Core.Link.Scaffolding.Client.Models;
using PCL.Core.Link.Scaffolding.EasyTier;
using PCL.Core.Logging;
using PCL.Core.UI.Dialogs;
using PCL.Core.Utils.Validate;
using PCL.Network;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.Core.Utils;

namespace PCL;

public partial class PageToolsGameLink : IPageView
{
    private readonly PageToolsGameLinkViewModel _vm = new();

    private bool _refreshed;

    static PageToolsGameLink()
    {
        initLoader = new Loading.LoaderCombo<int>(Lang.Text("Link.Mod.Task.InitLobby"),
            new[] { new Loading.LoaderTask<int, int>(Lang.Text("Common.Action.Initialize"), InitTask) { ProgressWeight = 0.5d } });
    }

    public PageToolsGameLink()
    {
        InitializeComponent();
        DataContext = _vm;
        DialogServiceInstaller.Install(_vm);
        LoaderInit();
        Loaded += (_, _) => RefreshOnNavigate();
        Unloaded += (_, _) => _refreshed = false;
        PageEnter += PageLinkLobby_OnPageEnter;
    }

    public PageToolsGameLinkViewModel ViewModel => _vm;

    public void OnNavigatedTo(object? parameter)
    {
        RefreshOnNavigate();
    }

    public void OnNavigatedFrom()
    {
    }

    private void RefreshOnNavigate()
    {
        if (_refreshed) return;
        _refreshed = true;
        Reload();
    }

    #region 初始化

    // 加载器初始化
    private void LoaderInit()
    {
        PageLoaderInit(Load, PanLoad, PanContent, null, initLoader, autoRun: false);
        // 注册自定义的 OnStateChanged
        initLoader.OnStateChangedUi += OnLoadStateChanged;

        LobbyService.OnNeedDownloadEasyTier += () => GameLink.DownloadEasyTier();
        LobbyService.DiscoveredWorlds.CollectionChanged += OnDiscoveredWorldsChanged;
        LobbyService.Players.CollectionChanged += OnPlayersChanged;
        LobbyService.OnUserStopGame += OnUserStopGame;
        LobbyService.OnClientPing += OnClientPingHandler;
        LobbyService.OnServerShutDown += OnServerShuttedDownHandler;
        LobbyService.OnServerStarted += OnServerStartedHandler;
        LobbyService.OnServerException += OnServerExceptionHandler;

        if (lobbyAnnouncementLoader is null)
        {
            var loaders = new List<Loading.LoaderBase>();
            loaders.Add(new Loading.LoaderTask<int, int>(Lang.Text("Link.Mod.Task.InitLobbyUi"), _ => Common.RunInUi(() =>
            {
                _vm.HintAnnounceVisibility = Visibility.Visible;
                HintAnnounce.Theme = MyHint.Themes.Blue;
                _vm.HintAnnounceText = Lang.Text("Tools.GameLink.Loading.ConnectingServer");
            })));
            loaders.Add(new Loading.LoaderTask<int, int>(Lang.Text("Link.Mod.Task.FetchAnnouncement"), _ => GetAnnouncement()) { ProgressWeight = 0.5d });
            lobbyAnnouncementLoader = new Loading.LoaderCombo<int>("Lobby Announcement", loaders) { show = false };
        }
    }

    private async void OnServerExceptionHandler(Exception ex)
    {
        Common.RunInUi(() => HintService.Show(Hints.Error(Lang.Text("Tools.GameLink.Error.ServerMessage", ex.Message))));

        try
        {
            await LobbyService.LeaveLobbyAsync();

            Common.RunInUi(() =>
            {
                CardPlayerList.Title = Lang.Text("Tools.GameLink.Member.ListLoading");
                _vm.PlayerRows.Clear();
                CurrentSubpage = Subpages.PanSelect;
            });
        }
        catch (Exception secEx)
        {
            Common.Log(secEx, "Occurred an exception when exit server.");
            HintService.Show(Hints.Error(Lang.Text("Tools.GameLink.Error.ServerExit")));
        }
    }

    public async void Reload()
    {
        _vm.HintAnnounceVisibility = Visibility.Visible;
        _vm.HintAnnounceText = Lang.Text("Tools.GameLink.Loading.ConnectingServer");
        HintAnnounce.Theme = MyHint.Themes.Blue;

        if (_linkAnnounceUpdateCancelSource is not null)
            _linkAnnounceUpdateCancelSource.Cancel();
        _linkAnnounceUpdateCancelSource = new CancellationTokenSource();
        BeginAnnouncementRequest();
        _linkAnnounces.Clear();

        // 加载公告
        _announcementLoadState = AnnouncementLoadState.Loading;
        lobbyAnnouncementLoader.Start(isForceRestart: true);
        await Dispatcher.BeginInvoke(new Action(async () =>
            await _LinkAnnounceUpdateAsync())); // 我实在不理解为啥 BeginInvoke 这个委托要 MustBeInherit

        await LobbyService.InitializeAsync().ConfigureAwait(false);
    }

    private void BtnAgreeEula_Click(object sender, MouseButtonEventArgs e)
    {
        States.Link.LinkEula = true;
        CurrentSubpage = Subpages.PanSelect;
    }

    private async void BtnEulaStop_Click(object sender, EventArgs eventArgs)
    {
        _vm.EulaStopEnabled = false;
        try
        {
            if ((await _vm.Dialog.ShowAsync(new TextDialogRequest
            {
                Title = Lang.Text("Tools.GameLink.Eula.RevokeTitle"),
                Content = Lang.Text("Tools.GameLink.Eula.RevokeConfirm"),
                Buttons = DialogButtons.OkCancel,
                IsWarn = true
            })).IsConfirmed)
            {
                States.Link.NaidRefreshTokenConfig.Reset();
                States.Link.LinkEulaConfig.Reset();
                HintService.Show(Hints.Info(Lang.Text("Tools.GameLink.Eula.Disabled")));
                CurrentSubpage = Subpages.PanEula;
            }
        }
        catch (Exception ex)
        {
            Common.Log(ex, "[Link] 撤回联机用户协议失败");
        }
        finally
        {
            _vm.EulaStopEnabled = true;
        }
    }

    private static readonly Loading.LoaderCombo<int> initLoader;

    private static async void InitTask(Loading.LoaderTask<int, int> task)
    {
        await LobbyService.InitializeAsync();
    }

    #region Subscribser

    private void OnServerStartedHandler()
    {
        Common.Log("Received server started event.");
        Common.RunInUi(() =>
        {
            _vm.FinishIdText = LobbyService.CurrentLobbyCode;
            _vm.PlayerRows.Clear();
            foreach (var player in LobbyService.Players)
                _vm.PlayerRows.Add(_MakePlayerRow(player));
        });
    }

    private async void OnServerShuttedDownHandler()
    {
        try
        {
            await LobbyService.LeaveLobbyAsync();

            Common.RunInUi(() =>
            {
                CardPlayerList.Title = Lang.Text("Tools.GameLink.Member.ListLoading");
                _vm.PlayerRows.Clear();
                CurrentSubpage = Subpages.PanSelect;
            });
        }
        catch (Exception ex)
        {
            Common.Log(ex, "Occurred an exception when exit server.");
            HintService.Show(Hints.Error(Lang.Text("Tools.GameLink.Error.ServerExit")));
        }
    }

    private void OnClientPingHandler(long latency)
    {
        Common.RunInUi(() =>
        {
            _vm.FinishQualityText = Lang.Text("Tools.GameLink.Finish.Connected");
            _vm.FinishPingText = Lang.Text("Tools.GameLink.Finish.PingMs", latency);
            _vm.ConnectTypeText = Lang.Text("Tools.GameLink.Finish.Unavailable");
        });
    }

    private void OnUserStopGame()
    {
        Common.RunInUi(() =>
        {
            CardPlayerList.Title = Lang.Text("Tools.GameLink.Member.ListLoading");
            _vm.PlayerRows.Clear();
            CurrentSubpage = Subpages.PanSelect;
        });
        _vm.Dialog.Show(new TextDialogRequest
        {
            Title = Lang.Text("Tools.GameLink.Exit.DisbandedTitle"),
            Content = Lang.Text("Tools.GameLink.Exit.Disbanded"),
            Buttons = DialogButtons.Ok,
            ForceWait = true
        });
    }


    private void OnPlayersChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        Common.Log("接收到玩家列表改变事件");
        Common.RunInUi(() =>
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    if (e.NewItems is not null)
                        foreach (PlayerProfile player in e.NewItems)
                            _vm.PlayerRows.Add(_MakePlayerRow(player));
                    break;
                case NotifyCollectionChangedAction.Remove:
                    if (e.OldItems is not null)
                        foreach (PlayerProfile player in e.OldItems)
                        {
                            var itemToRemove = _vm.PlayerRows
                                .FirstOrDefault(item => item.MachineId == player.MachineId);
                            if (itemToRemove is not null) _vm.PlayerRows.Remove(itemToRemove);
                        }

                    break;
                default:
                    _vm.PlayerRows.Clear();
                    foreach (var player in LobbyService.Players)
                        _vm.PlayerRows.Add(_MakePlayerRow(player));
                    break;
            }

            _vm.FinishQualityText = Lang.Text("Tools.GameLink.Finish.Connected");
            CardPlayerList.Title = Lang.Text("Tools.GameLink.Member.ListCount", LobbyService.Players.Count);
        });
    }


    private void OnDiscoveredWorldsChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        LogWrapper.Info("[Lobby] Found new world changes");

        Common.RunInUi(() =>
        {
            #region 处理集合变更

            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Reset:
                    ComboWorldList.Items.Clear();
                    foreach (var world in LobbyService.DiscoveredWorlds)
                        ComboWorldList.Items.Add(new MyComboBoxItem
                        {
                            Tag = world.Port,
                            Content = world.Name
                        });
                    break;

                case NotifyCollectionChangedAction.Add:
                    if (e.NewItems is not null)
                        foreach (FoundWorld world in e.NewItems)
                            ComboWorldList.Items.Add(new MyComboBoxItem
                            {
                                Tag = world.Port,
                                Content = world.Name
                            });

                    break;

                case NotifyCollectionChangedAction.Remove:
                    if (e.OldItems is not null)
                    {
                        // 使用 HashSet 提高查询效率
                        var portsToRemove = e.OldItems.Cast<FoundWorld>().Select(w => w.Port).ToHashSet();
                        var itemsToRemove = ComboWorldList.Items
                            .Cast<MyComboBoxItem>()
                            .Where(item => portsToRemove.Contains((int)item.Tag))
                            .ToList();

                        foreach (var item in itemsToRemove) ComboWorldList.Items.Remove(item);
                    }

                    break;
            }

            #endregion

            #region 更新 UI 状态

            var hasItems = ComboWorldList.Items.Count > 0;
            _vm.WorldListEnabled = hasItems;
            _vm.CreateEnabled = hasItems;

            if (hasItems && ComboWorldList.SelectedIndex == -1) ComboWorldList.SelectedIndex = 0;

            #endregion
        });
    }

    #endregion

    #endregion

    #region 公告

    public static Loading.LoaderCombo<int> lobbyAnnouncementLoader;
    private readonly ObservableCollection<LinkAnnounceInfo> _linkAnnounces = new();

    private CancellationTokenSource _linkAnnounceUpdateCancelSource;
    private readonly object _announcementRequestLock = new();
    private int _announcementRequestId;
    private AnnouncementLoadState _announcementLoadState;

    private enum AnnouncementLoadState
    {
        Loading,
        Loaded,
        Failed
    }

    // 公告轮播实现
    private async Task _LinkAnnounceUpdateAsync()
    {
        var currentIndex = 0;
        var globalCancelToken = _linkAnnounceUpdateCancelSource.Token;
        CancellationTokenSource waiterCts = null;

        _linkAnnounces.CollectionChanged += (sender, e) =>
        {
            if (waiterCts is not null) waiterCts.Cancel();
        };

        while (!globalCancelToken.IsCancellationRequested)
        {
            waiterCts = CancellationTokenSource.CreateLinkedTokenSource(globalCancelToken);
            var waiterCancelToken = waiterCts.Token;

            if (_announcementLoadState == AnnouncementLoadState.Failed)
            {
                // 错误提示由请求失败处理设置，轮播不应覆盖它。
            }
            else if (_linkAnnounces.Count > 0)
            {
                _vm.HintAnnounceVisibility = Visibility.Visible;
                var info = _linkAnnounces[currentIndex];
                string prefix;
                if (info.Type == LinkAnnounceType.Important)
                {
                    HintAnnounce.Theme = MyHint.Themes.Red;
                    prefix = Lang.Text("Tools.GameLink.Announcement.Important");
                }
                else if (info.Type == LinkAnnounceType.Warning)
                {
                    HintAnnounce.Theme = MyHint.Themes.Yellow;
                    prefix = Lang.Text("Tools.GameLink.Announcement.Warning");
                }
                else
                {
                    HintAnnounce.Theme = MyHint.Themes.Blue;
                    prefix = Lang.Text("Tools.GameLink.Announcement.Notice");
                }

                _vm.HintAnnounceText = Lang.Text("Tools.GameLink.Announcement.Format", prefix,
                    info.Content.Replace("\n", "\r\n"));
            }
            else if (_announcementLoadState == AnnouncementLoadState.Loaded)
            {
                _vm.HintAnnounceVisibility = Visibility.Collapsed;
            }

            try
            {
                await Task.Delay(10000, waiterCancelToken);
            }
            catch (TaskCanceledException)
            {
                // 忽略取消任务的异常
            }

            if (!waiterCancelToken.IsCancellationRequested)
                currentIndex += 1;
            if (currentIndex >= _linkAnnounces.Count)
                currentIndex = 0;
            waiterCts = null;
        }
    }

    // 获取公告信息
    private void GetAnnouncement()
    {
        var requestId = Volatile.Read(ref _announcementRequestId);
        Common.RunInNewThread(() =>
        {
            try
            {
                var serverNumber = 0;
                JsonObject jObj = null;

                #region 多服务器轮询获取公告

                while (serverNumber < Secrets.LinkServers.Length)
                    try
                    {
                        // 获取缓存版本号
                        var cacheRes = Requester.Fetch($"{Secrets.LinkServers[serverNumber]}/api/link/v2/cache.ini",
                            new FetchParam
                            {
                                Method = "GET",
                                ContentType = "application/json",
                                Timeout = 7000
                            }).Trim();
                        var cacheVer = int.Parse(cacheRes);

                        if (cacheVer == States.Link.AnnounceCacheVer)
                        {
                            LogWrapper.Info("[Link] Using cached announcement data");
                            jObj = (JsonObject)Common.GetJson(States.Link.AnnounceCache);
                        }
                        else
                        {
                            LogWrapper.Info("[Link] Fetching new announcement data");
                            var received = Requester.Fetch(
                                $"{Secrets.LinkServers[serverNumber]}/api/link/v2/announce.json",
                                new FetchParam
                                {
                                    Method = "GET",
                                    ContentType = "application/json",
                                    Timeout = 7000
                                });
                            jObj = (JsonObject)Common.GetJson(received);

                            // 更新缓存
                            if (!TryApplyCurrentAnnouncementRequest(requestId, () =>
                                {
                                    States.Link.AnnounceCache = received;
                                    States.Link.AnnounceCacheVer = cacheVer;
                                })) return;
                        }

                        break; // 成功获取，跳出轮询
                    }
                    catch (Exception ex)
                    {
                        LogWrapper.Error(ex, $"[Link] Failed to get announcement from server {serverNumber}");
                        if (!TryApplyCurrentAnnouncementRequest(requestId, () =>
                            {
                                States.Link.AnnounceCacheConfig.Reset();
                                States.Link.AnnounceCacheVerConfig.Reset();
                            })) return;
                        serverNumber++;
                    }

                #endregion

                if (jObj is null) throw new Exception("Failed to fetch lobby data");
                if (!IsCurrentAnnouncementRequest(requestId)) return;

                #region 解析基础状态与版本限制

                LobbyInfoProvider.IsLobbyAvailable = (bool)jObj["available"];
                LobbyInfoProvider.AllowCustomName = (bool)jObj["allowCustomName"];
                LobbyInfoProvider.RequiresLogin = (bool)jObj["requireLogin"];
                LobbyInfoProvider.RequiresRealName = (bool)jObj["requireRealname"];

                if (jObj["version"].ToObject<double>() > LobbyInfoProvider.ProtocolVersion)
                {
                    Common.RunInUi(() =>
                    {
                        if (!IsCurrentAnnouncementRequest(requestId)) return;
                        _announcementLoadState = AnnouncementLoadState.Failed;
                        _vm.HintAnnounceVisibility = Visibility.Visible;
                        HintAnnounce.Theme = MyHint.Themes.Red;
                        _vm.HintAnnounceText = Lang.Text("Tools.GameLink.Error.UpdateRequired");
                        LobbyInfoProvider.IsLobbyAvailable = false;
                    });
                    return;
                }

                #endregion

                #region 解析公告列表 (Notices)

                var notices = (JsonArray)jObj["notices"];
                var announcements = new List<LinkAnnounceInfo>();
                foreach (JsonObject notice in notices)
                {
                    if (!IsCurrentAnnouncementRequest(requestId)) return;
                    var content = notice["content"]?.ToString();
                    if (string.IsNullOrWhiteSpace(content)) continue;

                    // 版本过滤
                    var minVer = notice["minVer"].ToObject<double>();
                    var maxVer = notice["maxVer"].ToObject<double>();
                    if (Common.VersionCode < minVer || Common.VersionCode > maxVer) continue;

                    // 类型映射
                    var type = LinkAnnounceType.Notice;
                    var typeStr = notice["type"]?.ToString().ToLower();
                    if (typeStr == "important" || typeStr == "red") type = LinkAnnounceType.Important;
                    else if (typeStr == "warning" || typeStr == "yellow") type = LinkAnnounceType.Warning;

                    // 按行拆分公告
                    foreach (var announce in content.Split('\n'))
                    {
                        if (string.IsNullOrWhiteSpace(announce)) continue;
                        announcements.Add(new LinkAnnounceInfo(type, announce));
                    }
                }

                #endregion

                #region 解析中继服务器 (Relays)

                var relays = (JsonArray)jObj["relays"];
                ETRelay.RelayList = new List<ETRelay>();
                foreach (var relay in relays)
                    ETRelay.RelayList.Add(new ETRelay
                    {
                        Name = relay["name"]?.ToString(),
                        Url = relay["url"]?.ToString(),
                        Type = relay["type"]?.ToString() == "official" ? ETRelayType.Selfhosted : ETRelayType.Community
                    });

                #endregion

                #region 处理账户登录状态显示

                if (string.IsNullOrWhiteSpace(States.Link.NaidRefreshToken))
                {
                    Common.RunInUi(() => _vm.NatayarkUserNameText = Lang.Text("Tools.GameLink.Natayark.Login"));
                }
                else
                {
                    Common.RunInUi(() => _vm.NatayarkUserNameText = Lang.Text("Tools.GameLink.Natayark.Loading"));
                    if (string.IsNullOrEmpty(NatayarkProfileManager.NaidProfile.Username))
                        ReloadNaidData();
                    else
                        Common.RunInUi(() =>
                        {
                            if (NatayarkProfileManager.NaidProfile.Status == 0)
                            {
                                _vm.NatayarkUserNameText = NatayarkProfileManager.NaidProfile.Username;
                                _vm.NatayarkUserNameOpacity = 1;
                            }
                            else
                            {
                                _vm.NatayarkUserNameText = $"{NatayarkProfileManager.NaidProfile.Username} {Lang.Text("Tools.GameLink.Natayark.Abnormal")}";
                                _vm.NatayarkUserNameOpacity = 0.6;
                            }
                        });
                }

                #endregion

                Common.RunInUi(() =>
                {
                    if (!IsCurrentAnnouncementRequest(requestId)) return;
                    _linkAnnounces.Clear();
                    foreach (var announcement in announcements)
                        _linkAnnounces.Add(announcement);
                    _announcementLoadState = AnnouncementLoadState.Loaded;
                    if (_linkAnnounces.Count == 0)
                        _vm.HintAnnounceVisibility = Visibility.Collapsed;
                });
            }
            catch (Exception ex)
            {
                if (!IsCurrentAnnouncementRequest(requestId)) return;
                LobbyInfoProvider.IsLobbyAvailable = false;
                Common.RunInUi(() =>
                {
                    if (!IsCurrentAnnouncementRequest(requestId)) return;
                    _announcementLoadState = AnnouncementLoadState.Failed;
                    _vm.HintAnnounceVisibility = Visibility.Visible;
                    HintAnnounce.Theme = MyHint.Themes.Red;
                    _vm.HintAnnounceText = Lang.Text("Tools.GameLink.Error.ConnectFailed");
                });
                LogWrapper.Error(ex, "[Link] Failed to get lobby announcement");
            }
        });
    }

    private void BeginAnnouncementRequest()
    {
        lock (_announcementRequestLock)
            _announcementRequestId++;
    }

    private bool IsCurrentAnnouncementRequest(int requestId)
    {
        lock (_announcementRequestLock)
            return requestId == _announcementRequestId;
    }

    private bool TryApplyCurrentAnnouncementRequest(int requestId, Action action)
    {
        lock (_announcementRequestLock)
        {
            if (requestId != _announcementRequestId) return false;
            action();
            return true;
        }
    }

    #endregion

    #region 信息获取与展示

    #region UI 元素

    private static GameLinkPlayerRow _MakePlayerRow(PlayerProfile info)
    {
        var details = info.Kind == PlayerKind.HOST
            ? Lang.Text("Tools.GameLink.Player.Details", Lang.Text("Tools.GameLink.Player.Host"), info.Vendor)
            : info.Vendor;

        return new GameLinkPlayerRow
        {
            Name = info.Name,
            Info = details,
            Vendor = info.Vendor,
            MachineId = info.MachineId
        };
    }

    private async void PlayerInfoClick(object sender, MouseButtonEventArgs e)
    {
        try
        {
            var info = (GameLinkPlayerRow)((MyListItem)sender).Tag;
            await _vm.Dialog.ShowAsync(new TextDialogRequest
            {
                Title = Lang.Text("Tools.GameLink.Player.InfoTitle", info.Name),
                Content = Lang.Text("Tools.GameLink.Player.InfoMessage", info.Name, info.Vendor),
                Buttons = DialogButtons.Ok
            });
        }
        catch (Exception ex)
        {
            Common.Log(ex, "显示玩家信息失败", Common.LogLevel.Msgbox,
                userSummary: Lang.Text("Tools.GameLink.Player.InfoTitle", ""));
        }
    }

    #endregion

    #region Natayark 账户相关功能

    private void ReloadNaidData()
    {
        Common.RunInNewThread(() =>
        {
            try
            {
                #region 1. 登录令牌有效期检查

                // 检查 Token 是否过期
                var expireTime = Convert.ToDateTime(States.Link.NaidRefreshExpireTime);
                if (expireTime.CompareTo(DateTime.Now) < 0)
                {
                    States.Link.NaidRefreshToken = "";
                    HintService.Show(Hints.Error(Lang.Text("Tools.GameLink.Natayark.TokenExpired")));
                    return;
                }

                #endregion

                #region 2. 异步获取数据并同步等待

                // 调用异步方法并阻塞获取结果
                NatayarkProfileManager.GetNaidDataAsync(States.Link.NaidRefreshToken, true).GetAwaiter().GetResult();

                // 等待用户名加载，设置 10 秒超时防止线程卡死
                var retryCount = 0;
                while (string.IsNullOrWhiteSpace(NatayarkProfileManager.NaidProfile.Username) && retryCount < 10)
                {
                    Thread.Sleep(1000);
                    retryCount++;
                }

                if (string.IsNullOrWhiteSpace(NatayarkProfileManager.NaidProfile.Username))
                    throw new Exception("Timeout waiting for username");

                #endregion

                #region 3. UI 状态更新

                Common.RunInUi(() =>
                {
                    var profile = NatayarkProfileManager.NaidProfile;

                    // 状态 0 为正常
                    if (profile.Status == 0)
                    {
                        _vm.NatayarkUserNameText = profile.Username;
                        _vm.NatayarkUserNameOpacity = 1.0;
                    }
                    else
                    {
                        _vm.NatayarkUserNameText = $"{profile.Username} {Lang.Text("Tools.GameLink.Natayark.Abnormal")}";
                        _vm.NatayarkUserNameOpacity = 0.6;
                    }
                });

                #endregion
            }
            catch (Exception ex)
            {
                #region 错误处理

                Common.Log(ex, "Failed to refresh Natayark ID info, re-login required");

                Common.RunInUi(() =>
                {
                    _vm.NatayarkUserNameText = Lang.Text("Tools.GameLink.Natayark.FetchFailed");
                    _vm.NatayarkUserNameOpacity = 0.6;
                });

                #endregion
            }
        }, "Natayark Profile Refresh");
    }

    private async void LabNatayarkUserName_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // If Not IsLobbyAvailable Then
        // Hint("大厅功能暂不可用，请稍后再试", HintType.Critical)
        // Exit Sub
        // End If

        try
        {
            if (string.IsNullOrWhiteSpace(States.Link.NaidRefreshToken))
            {
                // 当前未登录，显示登录选项
                if ((await _vm.Dialog.ShowAsync(new TextDialogRequest
                {
                    Title = Lang.Text("Tools.GameLink.Natayark.LoginTitle"),
                    Content = Lang.Text("Tools.GameLink.Natayark.LoginPrompt"),
                    Buttons = DialogButtons.From(
                        DialogButton.Create(Lang.Text("Tools.GameLink.Natayark.Continue")),
                        DialogButton.Create(Lang.Text("Common.Action.Cancel")))
                })).IsConfirmed)
                {
                    _vm.NatayarkUserNameText = Lang.Text("Tools.GameLink.Natayark.BrowserContinue");
                    _vm.NatayarkUserNameOpacity = 0.6d;
                    _vm.NatayarkUserEnabled = false;
                    WebServer.StartNaidAuthorize(() =>
                    {
                        Common.RunInUi(() => _vm.NatayarkUserEnabled = true);
                        HintService.Show(Hints.Success(Lang.Text("Tools.GameLink.Natayark.LoginComplete")));
                        ReloadNaidData();
                    });
                }
            }
            // 当前已登录，显示登出选项
            else if ((await _vm.Dialog.ShowAsync(new TextDialogRequest
            {
                Title = Lang.Text("Tools.GameLink.Natayark.LogoutTitle"),
                Content = Lang.Text("Tools.GameLink.Natayark.LogoutConfirm"),
                Buttons = DialogButtons.OkCancel
            })).IsConfirmed)
            {
                States.Link.NaidRefreshTokenConfig.Reset();
                States.Link.NaidRefreshToken = "";
                _vm.NatayarkUserNameText = Lang.Text("Tools.GameLink.Natayark.Login");
                Common.Log("[Link] 已退出登录 Natayark Network");
                HintService.Show(Hints.Success(Lang.Text("Tools.GameLink.Natayark.LogoutComplete"), log: false));
            }
        }
        catch (Exception ex)
        {
            Common.Log(ex, "[Link] 切换 Natayark Network 登录状态失败");
        }
    }

    #endregion

    // 网络测试功能
    private async void BtnNetTest_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            _vm.NatTestEnabled = false;
            _vm.NatTypeText = Lang.Text("Tools.GameLink.Nat.Testing");
            var status = await CliNetTest.GetNetStatusAsync();
            Common.RunInUi(() => _vm.NatTypeText = Lang.Text("Tools.GameLink.Nat.Result",
                CliNetTest.GetNatTypeString(status.UdpNatType),
                CliNetTest.GetNatTypeString(status.TcpNatType)));
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "[Link] 获取网络测试结果失败",
                Common.LogLevel.Hint,
                userSummary: Lang.Text("Tools.GameLink.Error.NetworkTestFailed"));
            _vm.NatTestEnabled = true;
            _vm.NatTypeText = Lang.Text("Tools.GameLink.Nat.Failed");
        }
        finally
        {
            _vm.NatTestEnabled = true;
        }
    }

    #endregion

    #region PanSelect | 种类选择页面

    // 刷新按钮
    private void BtnRefresh_Click(object sender, MouseButtonEventArgs e)
    {
        var lobby = LobbyService.DiscoverWorldAsync();
    }

    private async void BtnInputPort_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            _vm.InputPortEnabled = false;
            if (!GameLink.LobbyPrecheck()) return;
            var inputDialog = await _vm.Dialog.ShowAsync(new InputDialogRequest
            {
                Title = Lang.Text("Tools.GameLink.Create.EnterPort"),
                Content = "",
                ValidateRules = [new IntValidator(65535, 1024)],
                HighLight = false
            });
            var input = inputDialog.IsConfirmed ? inputDialog.InputText : null;
            int port;
            if (int.TryParse(input, out port))
                using (var ping = McPingServiceFactory.CreateService("127.0.0.1", port, 5000))
                {
                    var res = await ping.PingAsync();
                    if (res is not null && res.Version.Protocol != 0)
                        await CreateLobbyAsync(port);
                    else
                        HintService.Show(Hints.Error(Lang.Text("Tools.GameLink.Create.NotMcPort")));
                }
        }
        catch (Exception ex)
        {
            Common.Log(ex, "[Link] 通过端口创建大厅失败");
        }
        finally
        {
            _vm.InputPortEnabled = true;
        }
    }

    // 创建大厅
    private async void BtnCreate_Click(object sender, MouseButtonEventArgs e)
    {
        if (ComboWorldList.SelectedItem is null)
        {
            HintService.Show(Hints.Info(Lang.Text("Tools.GameLink.Create.NoWorld")));
            return;
        }

        _vm.CreateEnabled = false;

        if (!GameLink.LobbyPrecheck())
        {
            _vm.CreateEnabled = true;
            return;
        }

        var port = (int)((MyComboBoxItem)ComboWorldList.SelectedItem).Tag;
        await CreateLobbyAsync(port);
    }

    private async Task CreateLobbyAsync(int port)
    {
        Common.Log("[Link] 创建大厅，端口：" + port);


        var username = LobbyInfoProvider.GetUsername();

        Common.RunInUi(() =>
        {
            _vm.FinishPingVisibility = Visibility.Collapsed;
            _vm.FinishPingText = "-ms";
            _vm.ConnectTypeVisibility = Visibility.Collapsed;
            _vm.ConnectTypeText = Lang.Text("Tools.GameLink.Finish.Connecting");
            CardPlayerList.Title = Lang.Text("Tools.GameLink.Member.ListLoading");
            _vm.PlayerRows.Clear();
            _vm.ConnectUserNameText = username;
            _vm.ConnectUserTypeText = Lang.Text("Tools.GameLink.Finish.Host");
            _vm.FinishIdText = LobbyService.CurrentLobbyCode;
            _vm.FinishCopyIpVisibility = Visibility.Collapsed;
            _vm.CreateEnabled = true;
            _vm.FinishExitText = Lang.Text("Tools.GameLink.Finish.CloseLobby");
            CurrentSubpage = Subpages.PanFinish;
        });

        var res = await LobbyService.CreateLobbyAsync(port, username).ConfigureAwait(true);

        if (!res)
            Common.RunInUi(() =>
            {
                CardPlayerList.Title = Lang.Text("Tools.GameLink.Member.ListLoading");
                _vm.PlayerRows.Clear();
                CurrentSubpage = Subpages.PanSelect;
            });
    }

    // 加入大厅
    private async void BtnJoin_Click(object sender, MouseButtonEventArgs e)
    {
        if (!GameLink.LobbyPrecheck())
            return;

        Common.Log("Start to join lobby.");

        var id = _vm.JoinLobbyId;
        var username = LobbyInfoProvider.GetUsername();

        Common.RunInUi(() =>
        {
            _vm.FinishPingVisibility = Visibility.Visible;
            _vm.FinishPingText = "-ms";
            _vm.ConnectTypeVisibility = Visibility.Visible;
            _vm.ConnectTypeText = Lang.Text("Tools.GameLink.Finish.Connecting");
            CardPlayerList.Title = Lang.Text("Tools.GameLink.Member.ListLoading");
            _vm.PlayerRows.Clear();
            _vm.ConnectUserNameText = username;
            _vm.ConnectUserTypeText = Lang.Text("Tools.GameLink.Finish.Guest");
            _vm.FinishIdText = id;
            _vm.FinishCopyIpVisibility = Visibility.Visible;
            CurrentSubpage = Subpages.PanFinish;
        });

        var res = await LobbyService.JoinLobbyAsync(id, username).ConfigureAwait(true);

        if (!res)
            Common.RunInUi(() =>
            {
                CardPlayerList.Title = Lang.Text("Tools.GameLink.Member.ListLoading");
                _vm.PlayerRows.Clear();
                CurrentSubpage = Subpages.PanSelect;
            });
    }

    private void TextJoinLobbyId_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            BtnJoin_Click(sender, null);
    }

    #endregion

    #region PanLoad | 加载中页面

    // 承接状态切换的 UI 改变
    private void OnLoadStateChanged(Loading.LoaderBase loader, LoadState newState, LoadState oldState)
    {
    }

    private static string _loadStep = "准备初始化";

    private static void SetLoadDesc(string intro, string step)
    {
        Common.Log("连接步骤：" + intro);
        _loadStep = step;
        Common.RunInUiWait(() =>
        {
            if (Launcher.frmToolsGameLink is null || !Launcher.frmToolsGameLink.LabLoadDesc.IsLoaded)
                return;
            Launcher.frmToolsGameLink.ViewModel.LoadDescText = intro;
            Launcher.frmToolsGameLink.UpdateProgress();
        });
    }

    // 承接重试
    private void CardLoad_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (initLoader.State != LoadState.Failed)
            return;
        initLoader.Start(isForceRestart: true);
    }

    // 取消加载
    private void CancelLoad(object sender, EventArgs eventArgs)
    {
        if (initLoader.State == LoadState.Loading)
        {
            CurrentSubpage = Subpages.PanSelect;
            initLoader.Abort();
        }
        else
        {
            initLoader.State = LoadState.Waiting;
        }
    }

    // 进度改变
    private void UpdateProgress(double value = -1)
    {
        if (value == -1)
            value = initLoader.Progress;
        var displayingProgress = ColumnProgressA.Width.Value;
        if (Math.Round(value - displayingProgress, 3) == 0d)
            return;
        if (displayingProgress > value)
        {
            ColumnProgressA.Width = new GridLength(value, GridUnitType.Star);
            ColumnProgressB.Width = new GridLength(1d - value, GridUnitType.Star);
            Animations.AniStop("LobbyController Progress");
        }
        else
        {
            var newProgress = value == 1d ? 1d : (value - displayingProgress) * 0.2d + displayingProgress;
            Animations.AniStart(
                new[]
                {
                    Animations.AaGridLengthWidth(ColumnProgressA, newProgress - ColumnProgressA.Width.Value, 300,
                        ease: new Animations.AniEaseOutFluent()),
                    Animations.AaGridLengthWidth(ColumnProgressB, 1d - newProgress - ColumnProgressB.Width.Value, 300,
                        ease: new Animations.AniEaseOutFluent())
                }, "LobbyController Progress");
        }
    }

    private void CardResized(object sender, SizeChangedEventArgs sizeChangedEventArgs)
    {
        RectProgressClip.Rect = new Rect(0d, 0d, CardLoad.ActualWidth, 12d);
    }

    #endregion

    #region PanFinish | 加载完成页面

    // 退出
    private async void BtnFinishExit_Click(object sender, Common.RouteEventArgs routeEventArgs)
    {
        _vm.FinishExitEnabled = false;
        try
        {
            if ((await _vm.Dialog.ShowAsync(new TextDialogRequest
            {
                Title = Lang.Text("Tools.GameLink.Exit.ConfirmTitle"),
                Content = Lang.Text(LobbyService.IsHost
                    ? "Tools.GameLink.Exit.ConfirmMessageWithHost"
                    : "Tools.GameLink.Exit.ConfirmMessage"),
                Buttons = DialogButtons.OkCancel,
                IsWarn = true
            })).IsConfirmed)
            {
                CurrentSubpage = Subpages.PanSelect;
                _vm.FinishExitText = Lang.Text("Tools.GameLink.Finish.Exit");
                await LobbyService.LeaveLobbyAsync().ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            Common.Log(ex, "[Link] 退出联机大厅失败");
        }
        finally
        {
            _vm.FinishExitEnabled = true;
        }
    }

    // 复制大厅编号
    private void BtnFinishCopy_Click(object sender, Common.RouteEventArgs routeEventArgs)
    {
        Common.ClipboardSet(LabFinishId.Text);
    }

    // 复制 IP
    private void BtnFinishCopyIp_Click(object sender, Common.RouteEventArgs routeEventArgs)
    {
        var port = LobbyInfoProvider.McForward?.LocalEndPoint?.Port;
        if (port == null) return;
        var ip = $"127.0.0.1:{port}";
        _ = _vm.Dialog.ShowAsync(new TextDialogRequest
        {
            Title = Lang.Text("Tools.GameLink.CopyIp.Title"),
            Content = Lang.Text("Tools.GameLink.CopyIp.Message", ip),
            Buttons = DialogButtons.From(
                DialogButton.Create(Lang.Text("Common.Action.Copy"), () => Common.ClipboardSet(ip)),
                DialogButton.Create(Lang.Text("Tools.GameLink.CopyIp.Back"))),
            ForceWait = true
        });
    }

    #endregion

    #region 子页面管理

    public enum Subpages
    {
        PanEula,
        PanSelect,
        PanFinish
    }

    public Subpages CurrentSubpage
    {
        get => field;
        set
        {
            if (field == value)
                return;
            field = value;
            Common.Log("[Link] 子页面更改为 " + Common.GetStringFromEnum(value));
            PageOnContentExit();
        }
    } = States.Link.LinkEula ? Subpages.PanSelect : Subpages.PanEula;

    private void PageLinkLobby_OnPageEnter()
    {
        _vm.ApplySubpage(CurrentSubpage);
    }

    #endregion
}
