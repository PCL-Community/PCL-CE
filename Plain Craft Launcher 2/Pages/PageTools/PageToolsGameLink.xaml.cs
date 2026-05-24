using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
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
using PCL.Core.Utils.Validate;
using PCL.Network;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageToolsGameLink
{
    static PageToolsGameLink()
    {
        InitLoader = new ModLoader.LoaderCombo<int>("大厅初始化",
            new[] { new ModLoader.LoaderTask<int, int>(Lang.Text("Common.Action.Initialize"), InitTask) { ProgressWeight = 0.5d } });
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
        PageLoaderInit(Load, PanLoad, PanContent, null, InitLoader, AutoRun: false);
        // 注册自定义的 OnStateChanged
        InitLoader.OnStateChangedUi += OnLoadStateChanged;

        LobbyService.OnNeedDownloadEasyTier += () => ModLink.DownloadEasyTier();
        LobbyService.DiscoveredWorlds.CollectionChanged += OnDiscoveredWorldsChanged;
        LobbyService.Players.CollectionChanged += OnPlayersChanged;
        LobbyService.OnUserStopGame += OnUserStopGame;
        LobbyService.OnClientPing += OnClientPingHandler;
        LobbyService.OnServerShutDown += OnServerShuttedDownHandler;
        LobbyService.OnServerStarted += OnServerStartedHandler;
        LobbyService.OnServerException += OnServerExceptionHandler;

        if (LobbyAnnouncementLoader is null)
        {
            var loaders = new List<ModLoader.LoaderBase>();
            loaders.Add(new ModLoader.LoaderTask<int, int>("大厅界面初始化", _ => ModBase.RunInUi(() =>
            {
                HintAnnounce.Visibility = Visibility.Visible;
                HintAnnounce.Theme = MyHint.Themes.Blue;
                HintAnnounce.Text = "正在连接到大厅服务器...";
            })));
            loaders.Add(new ModLoader.LoaderTask<int, int>("大厅公告获取", _ => GetAnnouncement()) { ProgressWeight = 0.5d });
            LobbyAnnouncementLoader = new ModLoader.LoaderCombo<int>("Lobby Announcement", loaders) { Show = false };
        }
    }

    private async void OnServerExceptionHandler(Exception ex)
    {
        ModBase.RunInUi(() => ModMain.Hint(ex.Message, ModMain.HintType.Critical));

        try
        {
            await LobbyService.LeaveLobbyAsync();

            ModBase.RunInUi(() =>
            {
                CardPlayerList.Title = "大厅成员列表（正在获取信息）";
                StackPlayerList.Children.Clear();
                CurrentSubpage = Subpages.PanSelect;
            });
        }
        catch (Exception secEx)
        {
            ModBase.Log(secEx, "Occured an exception when exit server.");
            ModMain.Hint("在服务器退出时发生了错误！", ModMain.HintType.Critical);
        }
    }


    public async void Reload()
    {
        HintAnnounce.Visibility = Visibility.Visible;
        HintAnnounce.Text = "正在连接到大厅服务器...";
        HintAnnounce.Theme = MyHint.Themes.Blue;

        // 加载公告
        LobbyAnnouncementLoader.Start();
        if (_linkAnnounceUpdateCancelSource is not null)
            _linkAnnounceUpdateCancelSource.Cancel();
        _linkAnnounceUpdateCancelSource = new CancellationTokenSource();
        await Dispatcher.BeginInvoke(new Action(async () =>
            await _LinkAnnounceUpdate())); // 我实在不理解为啥 BeginInvoke 这个委托要 MustBeInherit

        await LobbyService.InitializeAsync().ConfigureAwait(false);
    }

    private void BtnAgreeEula_Click(object sender, MouseButtonEventArgs e)
    {
        States.Link.LinkEula = true;
        CurrentSubpage = Subpages.PanSelect;
    }

    private void BtnEulaStop_Click(object sender, EventArgs eventArgs)
    {
        if (ModMain.MyMsgBox("你确定要撤销联机协议授权吗？", "撤销授权确认", Lang.Text("Common.Action.Confirm"), Lang.Text("Common.Action.Cancel"), IsWarn: true) == 1)
        {
            States.Link.NaidRefreshTokenConfig.Reset();
            States.Link.LinkEulaConfig.Reset();
            ModMain.Hint("联机功能已停用！");
            CurrentSubpage = Subpages.PanEula;
        }
    }

    private static readonly ModLoader.LoaderCombo<int> InitLoader;

    private static async void InitTask(ModLoader.LoaderTask<int, int> task)
    {
        await LobbyService.InitializeAsync();
    }

    #region Subscribser

    private void OnServerStartedHandler()
    {
        ModBase.Log("Received server started event.");
        ModBase.RunInUi(() =>
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

            ModBase.RunInUi(() =>
            {
                CardPlayerList.Title = "大厅成员列表（正在获取信息）";
                StackPlayerList.Children.Clear();
                CurrentSubpage = Subpages.PanSelect;
            });
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "Occured an exception when exit server.");
            ModMain.Hint("在服务器退出时发生了错误！", ModMain.HintType.Critical);
        }
    }

    private void OnClientPingHandler(long latency)
    {
        ModBase.RunInUi(() =>
        {
            LabFinishQuality.Text = "已连接";
            LabFinishPing.Text = latency + "ms";
            LabConnectType.Text = "暂不可用";
        });
    }

    private void OnUserStopGame()
    {
        ModBase.RunInUi(() =>
        {
            CardPlayerList.Title = "大厅成员列表（正在获取信息）";
            StackPlayerList.Children.Clear();
            CurrentSubpage = Subpages.PanSelect;
        });
        ModMain.MyMsgBox("由于你关闭了联机中的 MC 实例，大厅已自动解散。", "大厅已解散");
    }


    private void OnPlayersChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        ModBase.Log("接收到玩家列表改变事件");
        ModBase.RunInUi(() =>
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    if (e.NewItems != null)
                        foreach (PlayerProfile player in e.NewItems)
                            StackPlayerList.Children.Add((UIElement)PlayerInfoItem(player, PlayerInfoClick));
                    break;
                case NotifyCollectionChangedAction.Remove:
                    if (e.OldItems != null)
                        foreach (PlayerProfile player in e.OldItems)
                        {
                            var itemToRemove = StackPlayerList.Children.OfType<MyListItem>()
                                .FirstOrDefault(item => ((PlayerProfile)item.Tag).MachineId == player.MachineId);
                            if (itemToRemove != null) StackPlayerList.Children.Remove(itemToRemove);
                        }

                    break;
                default:
                    StackPlayerList.Children.Clear();
                    foreach (var player in LobbyService.Players)
                        StackPlayerList.Children.Add((UIElement)PlayerInfoItem(player, PlayerInfoClick));
                    break;
            }

            LabFinishQuality.Text = "已连接";
            CardPlayerList.Title = $"大厅成员列表（共 {LobbyService.Players.Count} 人）";
        });
    }


    private void OnDiscoveredWorldsChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        LogWrapper.Info("[Lobby] Found new world changes");

        ModBase.RunInUi(() =>
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
                    if (e.NewItems != null)
                        foreach (FoundWorld world in e.NewItems)
                            ComboWorldList.Items.Add(new MyComboBoxItem
                            {
                                Tag = world.Port,
                                Content = world.Name
                            });

                    break;

                case NotifyCollectionChangedAction.Remove:
                    if (e.OldItems != null)
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
            ComboWorldList.IsEnabled = hasItems;
            BtnCreate.IsEnabled = hasItems;

            if (hasItems && ComboWorldList.SelectedIndex == -1) ComboWorldList.SelectedIndex = 0;

            #endregion
        });
    }

    #endregion

    #endregion

    #region 公告

    public static ModLoader.LoaderCombo<int> LobbyAnnouncementLoader;
    private readonly ObservableCollection<LinkAnnounceInfo> _linkAnnounces = new();

    private CancellationTokenSource _linkAnnounceUpdateCancelSource;

    // 公告轮播实现
    private async Task _LinkAnnounceUpdate()
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

            if (_linkAnnounces.Count > 0)
            {
                var info = _linkAnnounces[currentIndex];
                string prefix;
                if (info.Type == LinkAnnounceType.Important)
                {
                    HintAnnounce.Theme = MyHint.Themes.Red;
                    prefix = "重要";
                }
                else if (info.Type == LinkAnnounceType.Warning)
                {
                    HintAnnounce.Theme = MyHint.Themes.Yellow;
                    prefix = "注意";
                }
                else
                {
                    HintAnnounce.Theme = MyHint.Themes.Blue;
                    prefix = "提示";
                }

                HintAnnounce.Text = "[" + prefix + "] " + info.Content.Replace("\n", "\r\n");
            }
            else
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
    {
        ModBase.RunInNewThread(() =>
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
                            jObj = (JsonObject)ModBase.GetJson(States.Link.AnnounceCache);
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
                            jObj = (JsonObject)ModBase.GetJson(received);

                            // 更新缓存
                            States.Link.AnnounceCache = received;
                            States.Link.AnnounceCacheVer = cacheVer;
                        }

                        break; // 成功获取，跳出轮询
                    }
                    catch (Exception ex)
                    {
                        LogWrapper.Error(ex, $"[Link] Failed to get announcement from server {serverNumber}");
                        States.Link.AnnounceCacheConfig.Reset();
                        States.Link.AnnounceCacheVerConfig.Reset();
                        serverNumber++;
                    }

                #endregion

                if (jObj == null) throw new Exception("Failed to fetch lobby data");

                #region 解析基础状态与版本限制

                LobbyInfoProvider.IsLobbyAvailable = (bool)jObj["available"];
                LobbyInfoProvider.AllowCustomName = (bool)jObj["allowCustomName"];
                LobbyInfoProvider.RequiresLogin = (bool)jObj["requireLogin"];
                LobbyInfoProvider.RequiresRealName = (bool)jObj["requireRealname"];

                if (Convert.ToDouble(jObj["version"]) > LobbyInfoProvider.ProtocolVersion)
                {
                    ModBase.RunInUi(() =>
                    {
                        HintAnnounce.Theme = MyHint.Themes.Red;
                        HintAnnounce.Text = "Please update to the latest PCL CE to use the lobby";
                        LobbyInfoProvider.IsLobbyAvailable = false;
                    });
                    return;
                }

                #endregion

                #region 解析公告列表 (Notices)

                var notices = (JsonArray)jObj["notices"];
                foreach (JsonObject notice in notices)
                {
                    var content = notice["content"]?.ToString();
                    if (string.IsNullOrWhiteSpace(content)) continue;

                    // 版本过滤
                    var minVer = Convert.ToDouble(notice["minVer"]);
                    var maxVer = Convert.ToDouble(notice["maxVer"]);
                    if (ModBase.VersionCode < minVer || ModBase.VersionCode > maxVer) continue;

                    // 类型映射
                    var type = LinkAnnounceType.Notice;
                    var typeStr = notice["type"]?.ToString().ToLower();
                    if (typeStr == "important" || typeStr == "red") type = LinkAnnounceType.Important;
                    else if (typeStr == "warning" || typeStr == "yellow") type = LinkAnnounceType.Warning;

                    // 按行拆分公告
                    foreach (var announce in content.Split('\n'))
                    {
                        if (string.IsNullOrWhiteSpace(announce)) continue;
                        _linkAnnounces.Add(new LinkAnnounceInfo(type, announce));
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
                    ModBase.RunInUi(() => LabNatayarkUserName.Text = "Click to login Natayark account");
                }
                else
                {
                    ModBase.RunInUi(() => LabNatayarkUserName.Text = "Loading...");
                    if (string.IsNullOrEmpty(NatayarkProfileManager.NaidProfile.Username))
                        ReloadNaidData();
                    else
                        ModBase.RunInUi(() =>
                        {
                            if (NatayarkProfileManager.NaidProfile.Status == 0)
                            {
                                LabNatayarkUserName.Text = NatayarkProfileManager.NaidProfile.Username;
                                LabNatayarkUserName.Opacity = 1;
                            }
                            else
                            {
                                LabNatayarkUserName.Text = $"{NatayarkProfileManager.NaidProfile.Username} (Abnormal)";
                                LabNatayarkUserName.Opacity = 0.6;
                            }
                        });
                }

                #endregion
            }
            catch (Exception ex)
            {
                LobbyInfoProvider.IsLobbyAvailable = false;
                ModBase.RunInUi(() =>
                {
                    HintAnnounce.Theme = MyHint.Themes.Red;
                    HintAnnounce.Text = "Failed to connect to lobby server";
                });
                LogWrapper.Error(ex, "[Link] Failed to get lobby announcement");
            }
        });
    }

    #endregion

    #region 信息获取与展示

    #region UI 元素

    private object PlayerInfoItem(PlayerProfile info, MyListItem.ClickEventHandler onClick)
    {
        string details = null;
        if (info.Kind == PlayerKind.HOST)
            details += "[主机] ";
        details += info.Vendor;
        // If info.Cost = ETConnectionType.Local Then
        // details += $"[本机] NAT {LobbyTextHandler.GetNatTypeChinese(info.NatType)}"
        // Else
        // details += $"{info.Ping}ms / {LobbyTextHandler.GetConnectTypeChinese(info.Cost)}"
        // End If
        var newItem = new MyListItem
        {
            Title = info.Name,
            Info = details,
            Type = MyListItem.CheckType.Clickable,
            Tag = info
        };
        newItem.Click += onClick;
        return newItem;
    }

    private void PlayerInfoClick(object sender, MouseButtonEventArgs e)
    {
        var info = (PlayerProfile)((MyListItem)sender).Tag;
        string msg = null;
        msg += $"用户名：{info.Name}";
        msg += "\r\n";
        msg += $"联机协议客户端标识：{info.Vendor}";
        // msg += $"{If(info.Cost = ETConnectionType.Local, "本机 ", $"延迟：{info.Ping}ms，丢包率：{info.Loss}%，连接方式：{LobbyTextHandler.GetConnectTypeChinese(info.Cost)}，")}NAT 类型：{LobbyTextHandler.GetNatTypeChinese(info.NatType)}"
        msg += "\r\n";
        msg += "此处数据仅供参考，请以实际游玩体验为准。";
        msg += "\r\n\r\n";
        msg += "若想了解 NAT 类型与其如何影响联机体验，请前往界面左侧的常见问题一栏。";
        ModMain.MyMsgBox(msg, $"玩家 {info.Name} 的详细信息");
    }

    #endregion

    #region Natayark 账户相关功能

    private void ReloadNaidData()
    {
        ModBase.RunInNewThread(() =>
        {
            try
            {
                #region 1. 登录令牌有效期检查

                // 检查 Token 是否过期
                var expireTime = Convert.ToDateTime(States.Link.NaidRefreshExpireTime);
                if (expireTime.CompareTo(DateTime.Now) < 0)
                {
                    States.Link.NaidRefreshToken = "";
                    ModMain.Hint("Natayark ID token expired, please login again", ModMain.HintType.Critical);
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

                ModBase.RunInUi(() =>
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
                        LabNatayarkUserName.Text = $"{profile.Username} (Abnormal)";
                        LabNatayarkUserName.Opacity = 0.6;
                    }
                });

                #endregion
            }
            catch (Exception ex)
            {
                #region 错误处理

                ModBase.Log(ex, "Failed to refresh Natayark ID info, re-login required");

                ModBase.RunInUi(() =>
                {
                    LabNatayarkUserName.Text = "Failed to fetch info";
                    LabNatayarkUserName.Opacity = 0.6;
                });

                #endregion
            }
        }, "Natayark Profile Refresh");
    }

    private void LabNatayarkUserName_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // If Not IsLobbyAvailable Then
        // Hint("大厅功能暂不可用，请稍后再试", HintType.Critical)
        // Exit Sub
        // End If

        if (string.IsNullOrWhiteSpace(States.Link.NaidRefreshToken))
        {
            // 当前未登录，显示登录选项
            if (ModMain.MyMsgBox("PCL 将会打开一个登录页面，请在浏览器中完成登录操作，然后回到启动器继续操作。", "登录至 Natayark Network", "继续", Lang.Text("Common.Action.Cancel")) == 1)
            {
                LabNatayarkUserName.Text = "请在浏览器中继续...";
                LabNatayarkUserName.Opacity = 0.6d;
                BtnNatayarkUserName.IsEnabled = false;
                ModWebServer.StartNaidAuthorize(() =>
                {
                    ModBase.RunInUi(() => BtnNatayarkUserName.IsEnabled = true);
                    ModMain.Hint("已完成登录操作", ModMain.HintType.Finish);
                    ReloadNaidData();
                });
            }
        }
        // 当前已登录，显示登出选项
        else if (ModMain.MyMsgBox("你确定要退出登录吗？", "退出登录", Lang.Text("Common.Action.Confirm"), Lang.Text("Common.Action.Cancel")) == 1)
        {
            States.Link.NaidRefreshTokenConfig.Reset();
            States.Link.NaidRefreshToken = "";
            LabNatayarkUserName.Text = "点击登录 Natayark 账户";
            ModBase.Log("[Link] 已退出登录 Natayark Network");
            ModMain.Hint("已退出登录！", ModMain.HintType.Finish, false);
        }
    }

    #endregion

    // 网络测试功能
    private async void BtnNetTest_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            BtnNatTest.IsEnabled = false;
            LabNatType.Text = "正在测试";
            var status = await CliNetTest.GetNetStatusAsync();
            ModBase.RunInUi(() =>
                LabNatType.Text =
                    $"{CliNetTest.GetNatTypeString(status.UdpNatType)} (UDP), {CliNetTest.GetNatTypeString(status.TcpNatType)}(TCP)");
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[Link] 获取网络测试结果失败", ModBase.LogLevel.Hint);
            BtnNatTest.IsEnabled = true;
            LabNatType.Text = "测试失败";
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
            ModBase.Log(ex, "从剪贴板识别大厅编号出错");
            return;
        }

        if (!string.IsNullOrEmpty(lobbyId))
            TextJoinLobbyId.Text = lobbyId;
        else
            ModMain.Hint("大厅编号不正确，请检查后重新输入");
    }

    private void ClearLobbyId(object sender, MouseButtonEventArgs e)
    {
        TextJoinLobbyId.Text = string.Empty;
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
            BtnInputPort.IsEnabled = false;
            if (!ModLink.LobbyPrecheck()) return;
            var input = ModMain.MyMsgBoxInput("请输入端口",
                ValidateRules: [new IntValidator(65535,1024)]);
            int port;
            if (int.TryParse(input, out port))
                using (var ping = McPingServiceFactory.CreateService("127.0.0.1", port, 5000))
                {
                    var res = await ping.PingAsync();
                    if (res is not null && res.Version.Protocol != 0)
                        await CreateLobby(port);
                    else
                        ModMain.Hint("这似乎不是个 MC 服务端口...", ModMain.HintType.Critical);
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
            ModMain.Hint("请先选择一个要联机的世界！");
            return;
        }

        BtnCreate.IsEnabled = false;

        if (!ModLink.LobbyPrecheck())
        {
            BtnCreate.IsEnabled = true;
            return;
        }

        var port = (int)((MyComboBoxItem)ComboWorldList.SelectedItem).Tag;
        await CreateLobby(port);
    }

    private async Task CreateLobby(int port)
    {
        ModBase.Log("[Link] 创建大厅，端口：" + port);


        var username = LobbyInfoProvider.GetUsername();

        ModBase.RunInUi(() =>
        {
            BtnFinishPing.Visibility = Visibility.Collapsed;
            LabFinishPing.Text = "-ms";
            BtnConnectType.Visibility = Visibility.Collapsed;
            LabConnectType.Text = "连接中";
            CardPlayerList.Title = "大厅成员列表（正在获取信息）";
            StackPlayerList.Children.Clear();
            LabConnectUserName.Text = username;
            LabConnectUserType.Text = "创建者";
            LabFinishId.Text = LobbyService.CurrentLobbyCode;
            BtnFinishCopyIp.Visibility = Visibility.Collapsed;
            BtnCreate.IsEnabled = true;
            BtnFinishExit.Text = "关闭大厅";
            CurrentSubpage = Subpages.PanFinish;
        });

        var res = await LobbyService.CreateLobbyAsync(port, username).ConfigureAwait(true);

        if (!res)
            ModBase.RunInUi(() =>
            {
                CardPlayerList.Title = "大厅成员列表（正在获取信息）";
                StackPlayerList.Children.Clear();
                CurrentSubpage = Subpages.PanSelect;
            });
    }

    // 加入大厅
    private async void BtnJoin_Click(object sender, MouseButtonEventArgs e)
    {
        if (!ModLink.LobbyPrecheck())
            return;

        ModBase.Log("Start to join lobby.");

        var id = TextJoinLobbyId.Text;
        var username = LobbyInfoProvider.GetUsername();

        ModBase.RunInUi(() =>
        {
            BtnFinishPing.Visibility = Visibility.Visible;
            LabFinishPing.Text = "-ms";
            BtnConnectType.Visibility = Visibility.Visible;
            LabConnectType.Text = "连接中";
            CardPlayerList.Title = "大厅成员列表（正在获取信息）";
            StackPlayerList.Children.Clear();
            LabConnectUserName.Text = username;
            LabConnectUserType.Text = "加入者";
            LabFinishId.Text = id;
            BtnFinishCopyIp.Visibility = Visibility.Visible;
            CurrentSubpage = Subpages.PanFinish;
        });

        var res = await LobbyService.JoinLobbyAsync(id, username).ConfigureAwait(true);

        if (!res)
            ModBase.RunInUi(() =>
            {
                CardPlayerList.Title = "大厅成员列表（正在获取信息）";
                StackPlayerList.Children.Clear();
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
    private void OnLoadStateChanged(ModLoader.LoaderBase loader, ModBase.LoadState newState, ModBase.LoadState oldState)
    {
    }

    private static string _loadStep = "准备初始化";

    private static void SetLoadDesc(string intro, string step)
    {
        ModBase.Log("连接步骤：" + intro);
        _loadStep = step;
        ModBase.RunInUiWait(() =>
        {
            if (ModMain.FrmToolsGameLink is null || !ModMain.FrmToolsGameLink.LabLoadDesc.IsLoaded)
                return;
            ModMain.FrmToolsGameLink.LabLoadDesc.Text = intro;
            ModMain.FrmToolsGameLink.UpdateProgress();
        });
    }

    // 承接重试
    private void CardLoad_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!(InitLoader.State == ModBase.LoadState.Failed))
            return;
        InitLoader.Start(IsForceRestart: true);
    }

    // 取消加载
    private void CancelLoad(object sender, EventArgs eventArgs)
    {
        if (InitLoader.State == ModBase.LoadState.Loading)
        {
            CurrentSubpage = Subpages.PanSelect;
            InitLoader.Abort();
        }
        else
        {
            InitLoader.State = ModBase.LoadState.Waiting;
        }
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
            ModAnimation.AniStop("LobbyController Progress");
        }
        else
        {
            var newProgress = value == 1d ? 1d : (value - displayingProgress) * 0.2d + displayingProgress;
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaGridLengthWidth(ColumnProgressA, newProgress - ColumnProgressA.Width.Value, 300,
                        Ease: new ModAnimation.AniEaseOutFluent()),
                    ModAnimation.AaGridLengthWidth(ColumnProgressB, 1d - newProgress - ColumnProgressB.Width.Value, 300,
                        Ease: new ModAnimation.AniEaseOutFluent())
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
    private async void BtnFinishExit_Click(object sender, ModBase.RouteEventArgs routeEventArgs)
    {
        var creatorHint = LobbyService.IsHost ? "\r\n由于你是大厅创建者，退出后此大厅将会自动解散。" : "";
        if (ModMain.MyMsgBox($"你确定要退出大厅吗？{creatorHint}", "确认退出", Lang.Text("Common.Action.Confirm"), Lang.Text("Common.Action.Cancel"), IsWarn: true) == 1)
        {
            CurrentSubpage = Subpages.PanSelect;
            BtnFinishExit.Text = "退出大厅";
            await LobbyService.LeaveLobbyAsync().ConfigureAwait(true);
        }
    }

    // 复制大厅编号
    private void BtnFinishCopy_Click(object sender, ModBase.RouteEventArgs routeEventArgs)
    {
        ModBase.ClipboardSet(LabFinishId.Text);
    }

    // 复制 IP
    private void BtnFinishCopyIp_Click(object sender, ModBase.RouteEventArgs routeEventArgs)
    {
        var ip = $"127.0.0.1:{LobbyInfoProvider.McForward.LocalPort}";
        ModMain.MyMsgBox(
            $"大厅创建者的游戏地址：{ip}\r\n注意：仅推荐在 MC 多人游戏列表不显示大厅广播时使用 IP 连接！通过 IP 连接将可能要求使用正版档案。", "复制 IP",
            Lang.Text("Common.Action.Copy"), "返回", Button1Action: () => ModBase.ClipboardSet(ip));
    }

    #endregion

    #region 子页面管理

    public enum Subpages
    {
        PanEula,
        PanSelect,
        PanFinish
    }

    private Subpages _CurrentSubpage = States.Link.LinkEula ? Subpages.PanSelect : Subpages.PanEula;

    public Subpages CurrentSubpage
    {
        get => _CurrentSubpage;
        set
        {
            if (_CurrentSubpage == value)
                return;
            _CurrentSubpage = value;
            ModBase.Log("[Link] 子页面更改为 " + ModBase.GetStringFromEnum(value));
            PageOnContentExit();
        }
    }

    private void PageLinkLobby_OnPageEnter()
    {
        ModMain.FrmToolsGameLink.PanEula.Visibility =
            CurrentSubpage == Subpages.PanEula ? Visibility.Visible : Visibility.Collapsed;
        ModMain.FrmToolsGameLink.PanSelect.Visibility =
            CurrentSubpage == Subpages.PanSelect ? Visibility.Visible : Visibility.Collapsed;
        ModMain.FrmToolsGameLink.PanFinish.Visibility =
            CurrentSubpage == Subpages.PanFinish ? Visibility.Visible : Visibility.Collapsed;
    }

    #endregion
}
