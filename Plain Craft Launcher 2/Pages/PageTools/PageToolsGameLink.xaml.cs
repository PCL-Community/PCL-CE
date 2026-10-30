using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Input;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Link;
using PCL.Core.Link.EasyTier;
using PCL.Core.Link.Lobby;
using PCL.Core.Link.McPing;
using PCL.Core.Link.Natayark;
using PCL.Core.Link.Scaffolding.Client.Models;
using PCL.Core.Link.Scaffolding.EasyTier;
using PCL.Core.Logging;
using PCL.Core.Utils;
using PCL.Core.Utils.Validate;
using PCL.Network;

namespace PCL;

public partial class PageToolsGameLink
{
    static PageToolsGameLink()
    {
        InitLoader = new Loading.LoaderCombo<int>(Lang.Text("Link.Mod.Task.InitLobby"),
            new[]
            {
                new Loading.LoaderTask<int, int>(Lang.Text("Common.Action.Initialize"), InitTask)
                    { ProgressWeight = 0.5d }
            });
    }

    public PageToolsGameLink()
    {
        InitializeComponent();
        LoaderInit();
        Loaded += (_, _) => Reload();
        PageEnter += PageLinkLobby_OnPageEnter;
    }

    #region 初始化

    // 加载器初始化
    private void LoaderInit()
    {
        PageLoaderInit(Load, PanLoad, PanContent, null, InitLoader, autoRun: false);
        // 注册自定义的 OnStateChanged
        InitLoader.OnStateChangedUi += OnLoadStateChanged;

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
            loaders.Add(new Loading.LoaderTask<int, int>(Lang.Text("Link.Mod.Task.InitLobbyUi"),
                _ => Common.RunInUi(() =>
            {
                HintAnnounce.Visibility = Visibility.Visible;
                HintAnnounce.Theme = MyHint.Themes.Blue;
                HintAnnounce.Text = Lang.Text("Tools.GameLink.Loading.ConnectingServer");
            })));
            loaders.Add(
                new Loading.LoaderTask<int, int>(Lang.Text("Link.Mod.Task.FetchAnnouncement"),
                    _ => GetAnnouncement()) { ProgressWeight = 0.5d });
            lobbyAnnouncementLoader = new Loading.LoaderCombo<int>("Lobby Announcement", loaders)
                { show = false };
        }
    }

    private async void OnServerExceptionHandler(Exception ex)
    {
        Common.RunInUi(() => HintService.Hint(
            Lang.Text("Tools.GameLink.Error.ServerMessage", ex.Message),
            HintType.Error));

        try
        {
            await LobbyService.LeaveLobbyAsync();

            Common.RunInUi(() =>
            {
                CardPlayerList.Title = Lang.Text("Tools.GameLink.Member.ListLoading");
                StackPlayerList.Children.Clear();
                CurrentSubpage = Subpages.PanSelect;
            });
        }
        catch (Exception secEx)
        {
            Common.Log(secEx, "Occurred an exception when exit server.");
            HintService.Hint(Lang.Text("Tools.GameLink.Error.ServerExit"), HintType.Error);
        }
    }

    public async void Reload()
    {
        HintAnnounce.Visibility = Visibility.Visible;
        HintAnnounce.Text = Lang.Text("Tools.GameLink.Loading.ConnectingServer");
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

    private void BtnEulaStop_Click(object sender, EventArgs eventArgs)
    {
        if (Launcher.MyMsgBox(Lang.Text("Tools.GameLink.Eula.RevokeConfirm"),
                Lang.Text("Tools.GameLink.Eula.RevokeTitle"),
                Lang.Text("Common.Action.Confirm"),
                Lang.Text("Common.Action.Cancel"),
                isWarn: true
            ) == 1)
        {
            States.Link.NaidRefreshTokenConfig.Reset();
            States.Link.LinkEulaConfig.Reset();
            HintService.Hint(Lang.Text("Tools.GameLink.Eula.Disabled"));
            CurrentSubpage = Subpages.PanEula;
        }
    }

    private static readonly Loading.LoaderCombo<int> InitLoader;

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
            LabFinishId.Text = LobbyService.CurrentLobbyCode;
            StackPlayerList.Children.Clear();
            foreach (var player in LobbyService.Players)
                StackPlayerList.Children.Add((UIElement)PlayerInfoItem(player, PlayerInfoClick));
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
                StackPlayerList.Children.Clear();
                CurrentSubpage = Subpages.PanSelect;
            });
        }
        catch (Exception ex)
        {
            Common.Log(ex, "Occurred an exception when exit server.");
            HintService.Hint(Lang.Text("Tools.GameLink.Error.ServerExit"), HintType.Error);
        }
    }

    private void OnClientPingHandler(long latency)
    {
        Common.RunInUi(() =>
        {
            LabFinishQuality.Text = Lang.Text("Tools.GameLink.Finish.Connected");
            LabFinishPing.Text = Lang.Text("Tools.GameLink.Finish.PingMs", latency);
            LabConnectType.Text = Lang.Text("Tools.GameLink.Finish.Unavailable");
        });
    }

    private void OnUserStopGame()
    {
        Common.RunInUi(() =>
        {
            CardPlayerList.Title = Lang.Text("Tools.GameLink.Member.ListLoading");
            StackPlayerList.Children.Clear();
            CurrentSubpage = Subpages.PanSelect;
        });
        Launcher.MyMsgBox(Lang.Text("Tools.GameLink.Exit.Disbanded"),
            Lang.Text("Tools.GameLink.Exit.DisbandedTitle"));
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
                            StackPlayerList.Children.Add((UIElement)PlayerInfoItem(player, PlayerInfoClick));
                    break;
                case NotifyCollectionChangedAction.Remove:
                    if (e.OldItems is not null)
                        foreach (PlayerProfile player in e.OldItems)
                        {
                            var itemToRemove = StackPlayerList.Children.OfType<MyListItem>()
                                .FirstOrDefault(item => ((PlayerProfile)item.Tag).MachineId == player.MachineId);
                            if (itemToRemove is not null) StackPlayerList.Children.Remove(itemToRemove);
                        }

                    break;
                default:
                    StackPlayerList.Children.Clear();
                    foreach (var player in LobbyService.Players)
                        StackPlayerList.Children.Add((UIElement)PlayerInfoItem(player, PlayerInfoClick));
                    break;
            }

            LabFinishQuality.Text = Lang.Text("Tools.GameLink.Finish.Connected");
            CardPlayerList.Title = Lang.Text("Tools.GameLink.Member.ListCount", LobbyService.Players.Count);
        });
    }


    private void OnDiscoveredWorldsChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        LogWrapper.Info("[Lobby] Found new world changes");

            #region 处理集合变更
        Common.RunInUi(() =>
        {

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

            #endregion
            #region 更新 UI 状态
                    break;
            }



            var hasItems = ComboWorldList.Items.Count > 0;
            #endregion
            ComboWorldList.IsEnabled = hasItems;
            BtnCreate.IsEnabled = hasItems;
    #endregion
    #region 公告
    #endregion

            if (hasItems && ComboWorldList.SelectedIndex == -1) ComboWorldList.SelectedIndex = 0;

        });
    }


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
                HintAnnounce.Visibility = Visibility.Visible;
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

                HintAnnounce.Text = Lang.Text("Tools.GameLink.Announcement.Format", prefix,
                    info.Content.Replace("\n", "\r\n"));
            }
            else if (_announcementLoadState == AnnouncementLoadState.Loaded)
            {
                HintAnnounce.Visibility = Visibility.Collapsed;
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
                #region 多服务器轮询获取公告
    {
        var requestId = Volatile.Read(ref _announcementRequestId);
        Common.RunInNewThread(() =>
        {
            try
            {
                var serverNumber = 0;
                JsonObject jObj = null;


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
                #endregion
                    {
                        LogWrapper.Error(ex, $"[Link] Failed to get announcement from server {serverNumber}");
                        if (!TryApplyCurrentAnnouncementRequest(requestId, () =>
                #region 解析基础状态与版本限制
                            {
                                States.Link.AnnounceCacheConfig.Reset();
                                States.Link.AnnounceCacheVerConfig.Reset();
                            })) return;
                        serverNumber++;
                    }


                if (jObj is null) throw new Exception("Failed to fetch lobby data");
                if (!IsCurrentAnnouncementRequest(requestId)) return;


                LobbyInfoProvider.IsLobbyAvailable = (bool)jObj["available"];
                LobbyInfoProvider.AllowCustomName = (bool)jObj["allowCustomName"];
                LobbyInfoProvider.RequiresLogin = (bool)jObj["requireLogin"];
                LobbyInfoProvider.RequiresRealName = (bool)jObj["requireRealname"];

                if (jObj["version"].ToObject<double>() > LobbyInfoProvider.ProtocolVersion)
                {
                #endregion
                #region 解析公告列表 (Notices)
                    Common.RunInUi(() =>
                    {
                        if (!IsCurrentAnnouncementRequest(requestId)) return;
                        _announcementLoadState = AnnouncementLoadState.Failed;
                        HintAnnounce.Visibility = Visibility.Visible;
                        HintAnnounce.Theme = MyHint.Themes.Red;
                        HintAnnounce.Text = Lang.Text("Tools.GameLink.Error.UpdateRequired");
                        LobbyInfoProvider.IsLobbyAvailable = false;
                    });
                    return;
                }



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

                #endregion
                #region 解析中继服务器 (Relays)
                    // 类型映射
                    var type = LinkAnnounceType.Notice;
                    var typeStr = notice["type"]?.ToString().ToLower();
                    if (typeStr == "important" || typeStr == "red") type = LinkAnnounceType.Important;
                    else if (typeStr == "warning" || typeStr == "yellow") type = LinkAnnounceType.Warning;

                    // 按行拆分公告
                    foreach (var announce in content.Split('\n'))
                    {
                        if (string.IsNullOrWhiteSpace(announce)) continue;
                #endregion
                #region 处理账户登录状态显示
                        announcements.Add(new LinkAnnounceInfo(type, announce));
                    }
                }



                var relays = (JsonArray)jObj["relays"];
                ETRelay.RelayList = new List<ETRelay>();
                foreach (var relay in relays)
                    ETRelay.RelayList.Add(new ETRelay
                    {
                        Name = relay["name"]?.ToString(),
                        Url = relay["url"]?.ToString(),
                        Type = relay["type"]?.ToString() == "official" ? ETRelayType.Selfhosted : ETRelayType.Community
                    });



                if (string.IsNullOrWhiteSpace(States.Link.NaidRefreshToken))
                {
                    Common.RunInUi(() =>
                        LabNatayarkUserName.Text = Lang.Text("Tools.GameLink.Natayark.Login"));
                }
                else
                {
                    Common.RunInUi(() =>
                        LabNatayarkUserName.Text = Lang.Text("Tools.GameLink.Natayark.Loading"));
                #endregion
                    if (string.IsNullOrEmpty(NatayarkProfileManager.NaidProfile.Username))
                        ReloadNaidData();
                    else
                        Common.RunInUi(() =>
                        {
                            if (NatayarkProfileManager.NaidProfile.Status == 0)
                            {
                                LabNatayarkUserName.Text = NatayarkProfileManager.NaidProfile.Username;
                                LabNatayarkUserName.Opacity = 1;
                            }
                            else
                            {
                                LabNatayarkUserName.Text = $"{NatayarkProfileManager.NaidProfile.Username} {Lang.Text("Tools.GameLink.Natayark.Abnormal")}";
                                LabNatayarkUserName.Opacity = 0.6;
                            }
                        });
                }


                Common.RunInUi(() =>
                {
                    if (!IsCurrentAnnouncementRequest(requestId)) return;
                    _linkAnnounces.Clear();
                    foreach (var announcement in announcements)
                        _linkAnnounces.Add(announcement);
                    _announcementLoadState = AnnouncementLoadState.Loaded;
                    if (_linkAnnounces.Count == 0)
                        HintAnnounce.Visibility = Visibility.Collapsed;
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
                    HintAnnounce.Visibility = Visibility.Visible;
                    HintAnnounce.Theme = MyHint.Themes.Red;
                    HintAnnounce.Text = Lang.Text("Tools.GameLink.Error.ConnectFailed");
                });
                LogWrapper.Error(ex, "[Link] Failed to get lobby announcement");
            }
        });
    }

    private void BeginAnnouncementRequest()
    {
        lock (_announcementRequestLock)
    #endregion
    #region 信息获取与展示
    #region UI 元素
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




    private object PlayerInfoItem(PlayerProfile info, MyListItem.ClickEventHandler onClick)
    {
        var details = info.Kind == PlayerKind.HOST
    #endregion
    #region Natayark 账户相关功能
            ? Lang.Text("Tools.GameLink.Player.Details", Lang.Text("Tools.GameLink.Player.Host"), info.Vendor)
            : info.Vendor;

        var newItem = new MyListItem
        {
            Title = info.Name,
                #region 1. 登录令牌有效期检查
            Info = details,
            Type = MyListItem.CheckType.Clickable,
            Tag = info
        };
        newItem.Click += onClick;

        return newItem;
    }

                #endregion
                #region 2. 异步获取数据并同步等待
    private void PlayerInfoClick(object sender, MouseButtonEventArgs e)
    {
        var info = (PlayerProfile)((MyListItem)sender).Tag;
        Launcher.MyMsgBox(Lang.Text("Tools.GameLink.Player.InfoMessage", info.Name, info.Vendor),
            Lang.Text("Tools.GameLink.Player.InfoTitle", info.Name));
    }



    private void ReloadNaidData()
    {
        Common.RunInNewThread(() =>
        {
            try
                #endregion
                #region 3. UI 状态更新
            {

                // 检查 Token 是否过期
                var expireTime = Convert.ToDateTime(States.Link.NaidRefreshExpireTime);
                if (expireTime.CompareTo(DateTime.Now) < 0)
                {
                    States.Link.NaidRefreshToken = "";
                    HintService.Hint(Lang.Text("Tools.GameLink.Natayark.TokenExpired"), HintType.Error);
                    return;
                }



                // 调用异步方法并阻塞获取结果
                NatayarkProfileManager.GetNaidDataAsync(States.Link.NaidRefreshToken, true).GetAwaiter().GetResult();

                #endregion
                // 等待用户名加载，设置 10 秒超时防止线程卡死
                var retryCount = 0;
                while (string.IsNullOrWhiteSpace(NatayarkProfileManager.NaidProfile.Username) && retryCount < 10)
                #region 错误处理
                {
                    Thread.Sleep(1000);
                    retryCount++;
                }

                if (string.IsNullOrWhiteSpace(NatayarkProfileManager.NaidProfile.Username))
                #endregion
                    throw new Exception("Timeout waiting for username");


                Common.RunInUi(() =>
                {
                    var profile = NatayarkProfileManager.NaidProfile;

                    // 状态 0 为正常
                    if (profile.Status == 0)
                    {
                        LabNatayarkUserName.Text = profile.Username;
                        LabNatayarkUserName.Opacity = 1.0;
                    }
                    else
                    {
                        LabNatayarkUserName.Text = $"{profile.Username} {Lang.Text("Tools.GameLink.Natayark.Abnormal")}";
                        LabNatayarkUserName.Opacity = 0.6;
                    }
                });

            }
            catch (Exception ex)
            {
                Common.Log(ex, "Failed to refresh Natayark ID info, re-login required");

                Common.RunInUi(() =>
                {
                    LabNatayarkUserName.Text = Lang.Text("Tools.GameLink.Natayark.FetchFailed");
                    LabNatayarkUserName.Opacity = 0.6;
                });

            }
        }, "Natayark Profile Refresh");
    }

    private void LabNatayarkUserName_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // If Not IsLobbyAvailable Then
        // Hint("大厅功能暂不可用，请稍后再试", HintType.Critical)
        // Exit Sub
        // End If

    #endregion
        if (string.IsNullOrWhiteSpace(States.Link.NaidRefreshToken))
        {
            // 当前未登录，显示登录选项
            if (Launcher.MyMsgBox(Lang.Text("Tools.GameLink.Natayark.LoginPrompt"),
                    Lang.Text("Tools.GameLink.Natayark.LoginTitle"),
                    Lang.Text("Tools.GameLink.Natayark.Continue"),
                    Lang.Text("Common.Action.Cancel")) == 1)
            {
                LabNatayarkUserName.Text = Lang.Text("Tools.GameLink.Natayark.BrowserContinue");
                LabNatayarkUserName.Opacity = 0.6d;
                BtnNatayarkUserName.IsEnabled = false;
                WebServer.StartNaidAuthorize(() =>
                {
                    Common.RunInUi(() => BtnNatayarkUserName.IsEnabled = true);
                    HintService.Hint(Lang.Text("Tools.GameLink.Natayark.LoginComplete"), HintType.Success);
                    ReloadNaidData();
                });
            }
        }
        // 当前已登录，显示登出选项
        else if (Launcher.MyMsgBox(Lang.Text("Tools.GameLink.Natayark.LogoutConfirm"),
                     Lang.Text("Tools.GameLink.Natayark.LogoutTitle"),
                     Lang.Text("Common.Action.Confirm"), Lang.Text("Common.Action.Cancel")) == 1)
        {
            States.Link.NaidRefreshTokenConfig.Reset();
            States.Link.NaidRefreshToken = "";
            LabNatayarkUserName.Text = Lang.Text("Tools.GameLink.Natayark.Login");
            Common.Log("[Link] 已退出登录 Natayark Network");
            HintService.Hint(Lang.Text("Tools.GameLink.Natayark.LogoutComplete"), HintType.Success, false);
        }
    }


    // 网络测试功能
    private async void BtnNetTest_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            BtnNatTest.IsEnabled = false;
            LabNatType.Text = Lang.Text("Tools.GameLink.Nat.Testing");
            var status = await CliNetTest.GetNetStatusAsync();
            Common.RunInUi(() => LabNatType.Text = Lang.Text("Tools.GameLink.Nat.Result",
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
    #endregion
    #region PanSelect | 种类选择页面
            BtnNatTest.IsEnabled = true;
            LabNatType.Text = Lang.Text("Tools.GameLink.Nat.Failed");
        }
        finally
        {
            BtnNatTest.IsEnabled = true;
        }
    }

    private void PasteLobbyId(object sender, MouseButtonEventArgs e)
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
            TextJoinLobbyId.Text = lobbyId;
        else
            HintService.Hint(Lang.Text("Tools.GameLink.Join.InvalidText"));
    }

    private void ClearLobbyId(object sender, MouseButtonEventArgs e)
    {
        TextJoinLobbyId.Text = string.Empty;
    }



    // 刷新按钮
    private void BtnRefresh_Click(object sender, MouseButtonEventArgs e)
    {
        var lobby = LobbyService.DiscoverWorldAsync();
    }

    private async void BtnInputPort_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            BtnInputPort.IsEnabled = false;
            if (!GameLink.LobbyPrecheck()) return;
            var input = Launcher.MyMsgBoxInput(Lang.Text("Tools.GameLink.Create.EnterPort"),
                validateRules: [new IntValidator(65535,1024)]);
            int port;
            if (int.TryParse(input, out port))
                using (var ping = McPingServiceFactory.CreateService("127.0.0.1", port, 5000))
                {
                    var res = await ping.PingAsync();
                    if (res is not null && res.Version.Protocol != 0)
                        await CreateLobbyAsync(port);
                    else
                        HintService.Hint(Lang.Text("Tools.GameLink.Create.NotMcPort"), HintType.Error);
                }
        }
        finally
        {
            BtnInputPort.IsEnabled = true;
        }
    }

    // 创建大厅
    private async void BtnCreate_Click(object sender, MouseButtonEventArgs e)
    {
        if (ComboWorldList.SelectedItem is null)
        {
            HintService.Hint(Lang.Text("Tools.GameLink.Create.NoWorld"));
            return;
        }

        BtnCreate.IsEnabled = false;

        if (!GameLink.LobbyPrecheck())
        {
            BtnCreate.IsEnabled = true;
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
            BtnFinishPing.Visibility = Visibility.Collapsed;
            LabFinishPing.Text = "-ms";
            BtnConnectType.Visibility = Visibility.Collapsed;
            LabConnectType.Text = Lang.Text("Tools.GameLink.Finish.Connecting");
            CardPlayerList.Title = Lang.Text("Tools.GameLink.Member.ListLoading");
            StackPlayerList.Children.Clear();
            LabConnectUserName.Text = username;
            LabConnectUserType.Text = Lang.Text("Tools.GameLink.Finish.Host");
            LabFinishId.Text = LobbyService.CurrentLobbyCode;
            BtnFinishCopyIp.Visibility = Visibility.Collapsed;
            BtnCreate.IsEnabled = true;
            BtnFinishExit.Text = Lang.Text("Tools.GameLink.Finish.CloseLobby");
            CurrentSubpage = Subpages.PanFinish;
        });

        var res = await LobbyService.CreateLobbyAsync(port, username).ConfigureAwait(true);

        if (!res)
            Common.RunInUi(() =>
            {
                CardPlayerList.Title = Lang.Text("Tools.GameLink.Member.ListLoading");
                StackPlayerList.Children.Clear();
                CurrentSubpage = Subpages.PanSelect;
            });
    }

    // 加入大厅
    private async void BtnJoin_Click(object sender, MouseButtonEventArgs e)
    {
        if (!GameLink.LobbyPrecheck())
            return;

        Common.Log("Start to join lobby.");

    #endregion
    #region PanLoad | 加载中页面
        var id = TextJoinLobbyId.Text;
        var username = LobbyInfoProvider.GetUsername();

        Common.RunInUi(() =>
        {
            BtnFinishPing.Visibility = Visibility.Visible;
            LabFinishPing.Text = "-ms";
            BtnConnectType.Visibility = Visibility.Visible;
            LabConnectType.Text = Lang.Text("Tools.GameLink.Finish.Connecting");
            CardPlayerList.Title = Lang.Text("Tools.GameLink.Member.ListLoading");
            StackPlayerList.Children.Clear();
            LabConnectUserName.Text = username;
            LabConnectUserType.Text = Lang.Text("Tools.GameLink.Finish.Guest");
            LabFinishId.Text = id;
            BtnFinishCopyIp.Visibility = Visibility.Visible;
            CurrentSubpage = Subpages.PanFinish;
        });

        var res = await LobbyService.JoinLobbyAsync(id, username).ConfigureAwait(true);

        if (!res)
            Common.RunInUi(() =>
            {
                CardPlayerList.Title = Lang.Text("Tools.GameLink.Member.ListLoading");
                StackPlayerList.Children.Clear();
                CurrentSubpage = Subpages.PanSelect;
            });
    }

    private void TextJoinLobbyId_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            BtnJoin_Click(sender, null);
    }



    // 承接状态切换的 UI 改变
    private void OnLoadStateChanged(Loading.LoaderBase loader, Common.LoadState newState,
        Common.LoadState oldState)
    {
    }

    private static string _loadStep = "准备初始化";

    private static void SetLoadDesc(string intro, string step)
    {
        Common.Log("连接步骤：" + intro);
        _loadStep = step;
        Common.RunInUiWait(() =>
        {
            if (Launcher.frmToolsGameLink is null ||
                !Launcher.frmToolsGameLink.LabLoadDesc.IsLoaded)
                return;
            Launcher.frmToolsGameLink.LabLoadDesc.Text = intro;
            Launcher.frmToolsGameLink.UpdateProgress();
        });
    }

    // 承接重试
    private void CardLoad_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (InitLoader.State != Common.LoadState.Failed)
            return;
        InitLoader.Start(isForceRestart: true);
    }

    // 取消加载
    private void CancelLoad(object sender, EventArgs eventArgs)
    {
        if (InitLoader.State == Common.LoadState.Loading)
        {
            CurrentSubpage = Subpages.PanSelect;
            InitLoader.Abort();
        }
        else
        {
            InitLoader.State = Common.LoadState.Waiting;
        }
    #endregion
    #region PanFinish | 加载完成页面
    }

    // 进度改变
    private void UpdateProgress(double value = -1)
    {
        if (value == -1)
            value = InitLoader.Progress;
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
                    Animations.AaGridLengthWidth(ColumnProgressA,
                        newProgress - ColumnProgressA.Width.Value, 300,
                        ease: new Animations.AniEaseOutFluent()),
                    Animations.AaGridLengthWidth(ColumnProgressB,
                        1d - newProgress - ColumnProgressB.Width.Value, 300,
                        ease: new Animations.AniEaseOutFluent())
                }, "LobbyController Progress");
        }
    }

    private void CardResized(object sender, SizeChangedEventArgs sizeChangedEventArgs)
    {
        RectProgressClip.Rect = new Rect(0d, 0d, CardLoad.ActualWidth, 12d);
    }


    #endregion
    #region 子页面管理

    // 退出
    private async void BtnFinishExit_Click(object sender, Common.RouteEventArgs routeEventArgs)
    {
        if (Launcher.MyMsgBox(
                Lang.Text(LobbyService.IsHost
                    ? "Tools.GameLink.Exit.ConfirmMessageWithHost"
                    : "Tools.GameLink.Exit.ConfirmMessage"),
                Lang.Text("Tools.GameLink.Exit.ConfirmTitle"),
                Lang.Text("Common.Action.Confirm"),
                Lang.Text("Common.Action.Cancel"),
                isWarn: true
            ) == 1)
        {
            CurrentSubpage = Subpages.PanSelect;
            BtnFinishExit.Text = Lang.Text("Tools.GameLink.Finish.Exit");
            await LobbyService.LeaveLobbyAsync().ConfigureAwait(true);
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
    #endregion
        if (port == null) return;
        var ip = $"127.0.0.1:{port}";
        Launcher.MyMsgBox(Lang.Text("Tools.GameLink.CopyIp.Message", ip),
            Lang.Text("Tools.GameLink.CopyIp.Title"),
            Lang.Text("Common.Action.Copy"),
            Lang.Text("Tools.GameLink.CopyIp.Back"),
            button1Action: () => Common.ClipboardSet(ip));
    }



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
        Launcher.frmToolsGameLink.PanEula.Visibility =
            CurrentSubpage == Subpages.PanEula ? Visibility.Visible : Visibility.Collapsed;
        Launcher.frmToolsGameLink.PanSelect.Visibility =
            CurrentSubpage == Subpages.PanSelect ? Visibility.Visible : Visibility.Collapsed;
        Launcher.frmToolsGameLink.PanFinish.Visibility =
            CurrentSubpage == Subpages.PanFinish ? Visibility.Visible : Visibility.Collapsed;
    }

}
