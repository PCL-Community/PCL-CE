Imports System.Collections.ObjectModel
Imports System.Threading.Tasks
Imports PCL.Core.Link
Imports PCL.Core.UI
Imports PCL.Core.Utils.Exts
Imports PCL.Core.Link.EasyTier
Imports PCL.Core.Link.Lobby
Imports PCL.Core.Link.Lobby.LobbyInfoProvider
Imports PCL.Core.Link.Natayark.NatayarkProfileManager
Imports PCL.Core.Utils
Imports PCL.Core.App

Public Class PageLinkLobby
    '记录的启动情况
    Private IsHost As Boolean = False
    Private HostInfo As ETPlayerInfo = Nothing

#Region "初始化"

    '加载器初始化
    Private Sub LoaderInit() Handles Me.Initialized
        PageLoaderInit(Load, PanLoad, PanContent, Nothing, InitLoader, AutoRun:=False)
        '注册自定义的 OnStateChanged
        AddHandler InitLoader.OnStateChangedUi, AddressOf OnLoadStateChanged
        If LobbyAnnouncementLoader Is Nothing Then
            Dim loaders As New List(Of LoaderBase)
            loaders.Add(New LoaderTask(Of Integer, Integer)("大厅界面初始化", Sub() RunInUi(Sub()
                                                                                         HintAnnounce.Visibility = Visibility.Visible
                                                                                         HintAnnounce.Theme = MyHint.Themes.Blue
                                                                                         HintAnnounce.Text = "正在连接到大厅服务器..."
                                                                                     End Sub)))
            loaders.Add(New LoaderTask(Of Integer, Integer)("大厅公告获取", AddressOf GetAnnouncement) With {.ProgressWeight = 0.5})
            LobbyAnnouncementLoader = New LoaderCombo(Of Integer)("Lobby Announcement", loaders) With {.Show = False}
        End If
    End Sub

    Private IsLoad As Boolean = False
    Private IsLoading As Boolean = False
    Public Sub Reload() Handles Me.Loaded
        If IsLoad OrElse IsLoading Then Exit Sub
        IsLoad = True
        IsLoading = True
        HintAnnounce.Visibility = Visibility.Visible
        HintAnnounce.Text = "正在连接到大厅服务器..."
        HintAnnounce.Theme = MyHint.Themes.Blue
        RunInNewThread(
            Sub()
                If Not Setup.Get("LinkEula") Then
                    Select Case MyMsgBox($"在使用 PCL CE 大厅之前，请阅读并同意以下条款：{vbCrLf}{vbCrLf}我承诺严格遵守中国大陆相关法律法规，不会将大厅功能用于违法违规用途。{vbCrLf}我已知晓大厅功能使用途中可能需要提供管理员权限以用于必要的操作，并会确保 PCL CE 为从官方发布渠道下载的副本。{vbCrLf}我承诺使用大厅功能带来的一切风险自行承担。{vbCrLf}我已知晓并同意 PCL CE 收集经处理的本机识别码、Natayark ID 与其他信息并在必要时提供给执法部门。{vbCrLf}为保护未成年人个人信息，使用联机大厅前，我确认我已满十四周岁。{vbCrLf}{vbCrLf}另外，你还需要同意 PCL CE 大厅相关隐私政策及《Natayark OpenID 服务条款》。", "联机大厅协议授权",
                                                    "我已阅读并同意", "拒绝并返回", "查看相关隐私协议",
                                                    Button3Action:=Sub() OpenWebsite("https://www.pclc.cc/privacy/personal-info-brief.html"))
                        Case 1
                            Setup.Set("LinkEula", True)
                        Case 2
                            RunInUi(
                            Sub()
                                FrmMain.PageChange(New FormMain.PageStackData With {.Page = FormMain.PageType.Launch})
                                FrmLinkLobby = Nothing
                            End Sub)
                    End Select
                End If
            End Sub)
        '加载公告
        LobbyAnnouncementLoader.Start()
        If _linkAnnounceUpdateCancelSource IsNot Nothing Then _linkAnnounceUpdateCancelSource.Cancel()
        _linkAnnounceUpdateCancelSource = New CancellationTokenSource()
        Dispatcher.BeginInvoke(Async Sub() Await _LinkAnnounceUpdate()) '我实在不理解为啥 BeginInvoke 这个委托要 MustBeInherit
        '刷新 NAID 令牌
        If Not String.IsNullOrWhiteSpace(Setup.Get("LinkNaidRefreshToken")) Then
            If Not String.IsNullOrWhiteSpace(Setup.Get("LinkNaidRefreshExpiresAt")) AndAlso Convert.ToDateTime(Setup.Get("LinkNaidRefreshExpiresAt")).CompareTo(DateTime.Now) < 0 Then
                Setup.Set("LinkNaidRefreshToken", "")
                Hint("Natayark ID 令牌已过期，请重新登录", HintType.Critical)
            Else
                GetNaidData(Setup.Get("LinkNaidRefreshToken"), True)
            End If
        End If
        DetectMcInstance()
        IsLoading = False
    End Sub
#End Region

#Region "加载步骤"

    Public Shared WithEvents InitLoader As New LoaderCombo(Of Integer)("大厅初始化", {
        New LoaderTask(Of Integer, Integer)("检查 EasyTier 文件", AddressOf InitFileCheck) With {.ProgressWeight = 0.5}
    })
    Private Shared Sub InitFileCheck(Task As LoaderTask(Of Integer, Integer))
        If Not File.Exists(ETInfoProvider.ETPath & "\easytier-core.exe") OrElse Not File.Exists(ETInfoProvider.ETPath & "\Packet.dll") OrElse
            Not File.Exists(ETInfoProvider.ETPath & "\easytier-cli.exe") OrElse Not File.Exists(ETInfoProvider.ETPath & "\wintun.dll") Then
            Log("[Link] EasyTier 不存在，开始下载")
            DownloadEasyTier()
        Else
            Log("[Link] EasyTier 文件检查完毕")
        End If
    End Sub

#End Region

#Region "公告"
    Public Shared LobbyAnnouncementLoader As LoaderCombo(Of Integer) = Nothing
    Private _linkAnnounces As New ObservableCollection(Of LinkAnnounceInfo)
    Private _linkAnnounceUpdateCancelSource As CancellationTokenSource = Nothing
    '公告轮播实现
    Private Async Function _LinkAnnounceUpdate() As Task
        Dim currentIndex = 0
        Dim globalCancelToken As CancellationToken = _linkAnnounceUpdateCancelSource.Token
        Dim waiterCancelSource As CancellationTokenSource = Nothing
        Dim contentChanged As Boolean = False
        AddHandler _linkAnnounces.CollectionChanged,
            Sub(sender, e)
                If waiterCancelSource IsNot Nothing Then waiterCancelSource.Cancel()
            End Sub
        While Not globalCancelToken.IsCancellationRequested
            waiterCancelSource = CancellationTokenSource.CreateLinkedTokenSource(globalCancelToken)
            Dim waiterCancelToken = waiterCancelSource.Token
            If _linkAnnounces.Count > 0 Then
                Dim info As LinkAnnounceInfo = _linkAnnounces(currentIndex)
                Dim prefix As String
                If info.Type = LinkAnnounceType.Important Then
                    HintAnnounce.Theme = MyHint.Themes.Red
                    prefix = "重要"
                ElseIf info.Type = LinkAnnounceType.Warning Then
                    HintAnnounce.Theme = MyHint.Themes.Yellow
                    prefix = "注意"
                Else
                    HintAnnounce.Theme = MyHint.Themes.Blue
                    prefix = "提示"
                End If
                HintAnnounce.Text = "[" & prefix & "] " & info.Content.Replace(vbLf, vbCrLf)
            Else
                HintAnnounce.Visibility = Visibility.Collapsed
            End If
            Try
                Await Task.Delay(10000, waiterCancelToken)
            Catch ex As TaskCanceledException
                '忽略取消任务的异常
            End Try
            If Not waiterCancelToken.IsCancellationRequested Then currentIndex += 1
            If currentIndex >= _linkAnnounces.Count Then currentIndex = 0
            waiterCancelSource = Nothing
        End While
    End Function
    '获取公告信息
    Private Sub GetAnnouncement()
        RunInNewThread(
            Sub()
                Try
                    Dim serverNumber = 0
                    Dim jObj As JObject = Nothing
                    Dim cache As Integer
                    While serverNumber < LinkServers.Length
                        Try
                            cache = Integer.Parse(NetRequestOnce($"{LinkServers(serverNumber)}/api/link/v2/cache.ini", "GET", Nothing, "application/json", Timeout:=7000).Trim())
                            If cache = Config.Link.AnnounceCacheVer Then
                                Log("[Link] 使用缓存的公告数据")
                                jObj = GetJson(Config.Link.AnnounceCache)
                            Else
                                Log("[Link] 尝试拉取公告数据")
                                Dim received As String = NetRequestOnce($"{LinkServers(serverNumber)}/api/link/v2/announce.json", "GET", Nothing, "application/json", Timeout:=7000)
                                jObj = GetJson(received)
                                Config.Link.AnnounceCache = received
                                Config.Link.AnnounceCacheVer = cache
                            End If
                            Exit While
                        Catch ex As Exception
                            Log(ex, $"[Link] 从服务器 {serverNumber} 获取公告缓存失败")
                            Config.Link.AnnounceCacheConfig.Reset()
                            Config.Link.AnnounceCacheVerConfig.Reset()
                            serverNumber += 1
                        End Try
                    End While
                    If jObj Is Nothing Then Throw New Exception("获取联机数据失败")
                    IsLobbyAvailable = jObj("available")
                    AllowCustomName = jObj("allowCustomName")
                    RequiresLogin = jObj("requireLogin")
                    RequiresRealName = jObj("requireRealname")
                    If Not Val(jObj("version")) <= ProtocolVersion Then
                        RunInUi(
                            Sub()
                                HintAnnounce.Theme = MyHint.Themes.Red
                                HintAnnounce.Text = "请更新到最新版本 PCL CE 以使用大厅"
                                IsLobbyAvailable = False
                            End Sub)
                        Exit Sub
                    End If
                    '公告
                    Dim notices As JArray = jObj("notices")
                    For Each noticeLatest As JObject In notices
                        Dim announceContent = noticeLatest("content").ToString()
                        If Not String.IsNullOrWhiteSpace(announceContent) Then
                            Dim announceType As LinkAnnounceType
                            If noticeLatest("type") = "important" OrElse noticeLatest("type") = "red" Then
                                announceType = LinkAnnounceType.Important
                            ElseIf noticeLatest("type") = "warning" OrElse noticeLatest("type") = "yellow" Then
                                announceType = LinkAnnounceType.Warning
                            Else
                                announceType = LinkAnnounceType.Notice
                            End If
                            Dim announces As String() = announceContent.Split(vbLf)
                            For Each announce As String In announces
                                _linkAnnounces.Add(New LinkAnnounceInfo(announceType, announce))
                            Next
                        End If
                    Next
                    '中继服务器
                    Dim relays As JArray = jObj("relays")
                    ETRelay.RelayList = New List(Of ETRelay)
                    For Each relay In relays
                        ETRelay.RelayList.Add(New ETRelay With {
                            .Name = relay("name").ToString(),
                            .Url = relay("url").ToString(),
                            .Type = If(relay("type") = "official", ETRelayType.Selfhosted, ETRelayType.Community)
                        })
                    Next
                Catch ex As Exception
                    IsLobbyAvailable = False
                    RunInUi(Sub()
                                HintAnnounce.Theme = MyHint.Themes.Red
                                HintAnnounce.Text = "连接到大厅服务器失败"
                            End Sub)
                    Log(ex, "[Link] 获取大厅公告失败")
                End Try
            End Sub)
    End Sub
#End Region

#Region "信息获取与展示"

#Region "UI 元素"
    Private Function PlayerInfoItem(info As ETPlayerInfo, onClick As MyListItem.ClickEventHandler)
        Dim details As String = Nothing
        If info.IsHost Then details += "[主机] "
        If String.IsNullOrEmpty(info.Username) Then details += "[第三方] "
        If info.Cost = ETConnectionType.Local Then
            details += $"[本机] NAT {LobbyTextHandler.GetNatTypeChinese(info.NatType)}"
        Else
            details += $"{info.Ping}ms / {LobbyTextHandler.GetConnectTypeChinese(info.Cost)}"
        End If
        Dim newItem As New MyListItem With {
                .Title = If(Not String.IsNullOrEmpty(info.Username), info.Username, info.Hostname),
                .Info = details,
                .Type = MyListItem.CheckType.Clickable,
                .Tag = info
        }
        AddHandler newItem.Click, onClick
        Return newItem
    End Function
    Private Sub PlayerInfoClick(sender As MyListItem, e As EventArgs)
        Dim info As ETPlayerInfo = sender.Tag
        Dim msg As String = Nothing
        If Not String.IsNullOrEmpty(info.Username) Then
            msg += $"启动器用户名：{info.Username}"
            If Not String.IsNullOrEmpty(info.McName) Then
                msg += $"，启动器使用的 MC 档案名称：{info.McName}"
            End If
        Else
            msg += $"主机名称：{info.Hostname}"
        End If
        msg += vbCrLf
        msg += $"{If(info.Cost = ETConnectionType.Local, "本机 ", $"延迟：{info.Ping}ms，丢包率：{info.Loss}%，连接方式：{LobbyTextHandler.GetConnectTypeChinese(info.Cost)}，")}NAT 类型：{LobbyTextHandler.GetNatTypeChinese(info.NatType)}"
        msg += vbCrLf
        msg += "此处数据仅供参考，请以实际游玩体验为准。"
        msg += vbCrLf + vbCrLf
        msg += "若想了解 NAT 类型与其如何影响联机体验，请前往界面左侧的常见问题一栏。"
        MyMsgBox(msg, $"玩家 {If(Not String.IsNullOrEmpty(info.Username), info.Username, info.Hostname)} 的详细信息")
    End Sub
#End Region

    Private IsWatcherStarted As Boolean = False
    Private IsETFirstCheckFinished As Boolean = False
    Private IsDetectingMc As Boolean = False
    '检测本地 MC 局域网实例
    Private Sub DetectMcInstance() Handles BtnRefresh.Click
        If IsDetectingMc Then Return
        IsDetectingMc = True
        ComboWorldList.Items.Clear()
        ComboWorldList.Items.Add(New MyComboBoxItem With {.Tag = Nothing, .Content = "正在检测本地游戏...", .Height = 18, .Margin = New Thickness(8, 4, 0, 0)})
        ComboWorldList.SelectedIndex = 0
        BtnRefresh.IsEnabled = False
        BtnCreate.IsEnabled = False
        ComboWorldList.IsEnabled = False
        RunInNewThread(
            Sub()
                Dim Worlds As List(Of Tuple(Of Integer, McPingResult, String)) = MCInstanceFinding.GetAwaiter().GetResult()
                RunInUi(
                    Sub()
                        ComboWorldList.Items.Clear()
                        If Worlds.Count = 0 Then
                            ComboWorldList.Items.Add(New MyComboBoxItem With {.Tag = Nothing, .Content = "无可用实例"})
                        Else
                            For Each World In Worlds
                                Dim content = $"{World.Item2.Description} ({World.Item2.Version.Name} / 端口 {World.Item1}{If(Not String.IsNullOrWhiteSpace(World.Item3), $" / 由 {World.Item3} 启动", Nothing)})"
                                ComboWorldList.Items.Add(New MyComboBoxItem With {.Tag = World, .Content = content})
                            Next
                            ComboWorldList.IsEnabled = True
                            BtnCreate.IsEnabled = True
                        End If
                        IsDetectingMc = False
                        ComboWorldList.SelectedIndex = 0
                        BtnRefresh.IsEnabled = True
                    End Sub)
            End Sub, "Minecraft Port Detect")
    End Sub
    'EasyTier Cli 轮询
    Private Sub StartETWatcher()
        RunInNewThread(Sub()
                           If IsWatcherStarted Then Return
                           Log("[Link] 启动 EasyTier 轮询")
                           IsWatcherStarted = True
                           Dim retryCount = 0
                           While ETInfoProvider.CheckETStatusAsync().GetAwaiter().GetResult() = 0 AndAlso retryCount <= 15
                               retryCount += GetETInfo()
                               If RequiresLogin AndAlso String.IsNullOrWhiteSpace(NaidProfile.AccessToken) Then
                                   Hint("请先登录 Natayark ID 再使用大厅！", HintType.Critical)
                                   LobbyController.Close()
                               End If
                               Thread.Sleep(2000)
                           End While
                           RunInUi(Sub() CurrentSubpage = Subpages.PanSelect)
                           LobbyController.Close()
                           Log("[Link] EasyTier 轮询已结束")
                           IsWatcherStarted = False
                       End Sub, "EasyTier Status Watcher", ThreadPriority.BelowNormal)
    End Sub
    'EasyTier Cli 信息获取
    Private Function GetETInfo(Optional RemainRetry As Integer = 8) As Integer
        Try
            Dim info = ETInfoProvider.GetPlayerList()
            Dim playerList = info.Item1
            Dim localInfo = info.Item2
            If playerList Is Nothing OrElse Not playerList(0).IsHost OrElse localInfo Is Nothing Then
                If RemainRetry > 0 Then
                    Log($"[Link] 未找到大厅创建者或本机信息，放弃前再重试 {RemainRetry} 次")
                    Thread.Sleep(800)
                    GetETInfo(RemainRetry - 1)
                    Return 1
                End If
                If IsETFirstCheckFinished Then
                    MyMsgBox($"大厅创建者关闭了大厅。{vbCrLf}有可能是创建者累了，或者是他的游戏 / 网络连接炸了。", "大厅已解散")
                    ToastNotification.SendToast("大厅已解散", "PCL CE 大厅")
                Else
                    If IsHost Then
                        Hint("大厅创建失败", HintType.Critical)
                    Else
                        Hint("该大厅不存在", HintType.Critical)
                    End If
                End If
                RunInUi(Sub()
                            CardPlayerList.Title = "大厅成员列表（正在获取信息）"
                            StackPlayerList.Children.Clear()
                            CurrentSubpage = Subpages.PanSelect
                            Log("[Link] [ETInfo] 大厅不存在或已被解散，返回选择界面")
                        End Sub)
                LobbyController.Close()
                Return 1
            End If
            Dim hostInfo = playerList(0)
            If hostInfo.ETVersion <> localInfo.ETVersion Then
                RunInUi(Sub() HintEasyTierVersion.Visibility = Visibility.Visible)
            Else
                RunInUi(Sub() HintEasyTierVersion.Visibility = Visibility.Collapsed)
            End If

            '本地网络质量评估
            Dim quality
            'NAT 评估
            If localInfo.NatType.ContainsF("OpenInternet", True) OrElse localInfo.NatType.ContainsF("NoPAT", True) OrElse localInfo.NatType.ContainsF("FullCone", True) Then
                quality = 3
            ElseIf localInfo.NatType.ContainsF("Restricted", True) OrElse localInfo.NatType.ContainsF("PortRestricted", True) Then
                quality = 2
            Else
                quality = 1
            End If
            '到主机延迟评估
            If hostInfo.Ping > 150 Then
                quality -= 1
            End If
            RunInUi(Sub()
                        Dim texts = LobbyTextHandler.GetQualityDesc(quality)
                        LabFinishQuality.Text = texts.Keyword
                        BtnFinishQuality.ToolTip = "连接状况" & vbCrLf & texts.Desc
                    End Sub)

            If IsHost AndAlso Not LobbyController.IsHostInstanceAvailable(TargetLobby.Port) Then '确认创建者实例存活状态
                RunInUi(Sub()
                            CardPlayerList.Title = "大厅成员列表（正在获取信息）"
                            StackPlayerList.Children.Clear()
                            CurrentSubpage = Subpages.PanSelect
                        End Sub)
                LobbyController.Close()
                MyMsgBox("由于你关闭了联机中的 MC 实例，大厅已自动解散。", "大厅已解散")
            End If

            '加入方刷新连接信息
            Dim etStatus = ETController.Status
            RunInUi(Sub()
                        If Not etStatus = ETState.Ready AndAlso Not hostInfo.Ping = 1000 Then
                            etStatus = ETState.Ready
                        ElseIf Not etStatus = ETState.Ready AndAlso hostInfo.Ping = 1000 Then '如果 ET 还未就绪，则显示延迟为 0，防止用户找茬
                            hostInfo.Ping = 0
                        End If
                        LabFinishPing.Text = hostInfo.Ping.ToString() & "ms"
                        LabConnectType.Text = LobbyTextHandler.GetConnectTypeChinese(hostInfo.Cost)
                    End Sub)

            '刷新大厅成员列表 UI
            RunInUi(Sub()
                        StackPlayerList.Children.Clear()
                        For Each player In playerList
                            If Not etStatus = ETState.Ready AndAlso player.Ping = 1000 Then player.Ping = 0 '如果 ET 还未就绪，则显示延迟为 0，防止用户找茬
                            Dim newItem = PlayerInfoItem(player, AddressOf PlayerInfoClick)
                            StackPlayerList.Children.Add(newItem)
                        Next
                        CardPlayerList.Title = $"大厅成员列表（共 {playerList.Count} 人）"
                    End Sub)
            IsETFirstCheckFinished = True
            Return 0
        Catch ex As Exception
            Log(ex, "[Link] EasyTier Cli 线程异常")
            If ETController.Status = ETState.Stopped Then LobbyController.Close()
            Return 1
        End Try
    End Function
    Private Sub PasteLobbyId() Handles BtnPaste.Click
        Dim lobbyId As String
        Try
            Dim clipText = Clipboard.GetText(TextDataFormat.Text)
            lobbyId = ParseCode(clipText).OriginalCode
        Catch ex As Exception
            Log(ex, "从剪贴板识别大厅编号出错")
            Exit Sub
        End Try
        If lobbyId IsNot Nothing Then
            TextJoinLobbyId.Text = lobbyId
        Else
            Hint("大厅编号不正确，请检查后重新输入")
        End If
    End Sub
    Private Sub ClearLobbyId() Handles BtnClearLobbyId.Click
        TextJoinLobbyId.Text = String.Empty
    End Sub
#End Region

#Region "PanSelect | 种类选择页面"

    '创建大厅
    Private Sub BtnCreate_Click(sender As Object, e As EventArgs) Handles BtnCreate.Click
        BtnCreate.IsEnabled = False
        If Not LobbyPrecheck() Then
            BtnCreate.IsEnabled = True
            Exit Sub
        End If
        Dim port = CType(ComboWorldList.SelectedItem.Tag, Tuple(Of Integer, McPingResult, String)).Item1.ToString()
        Log("[Link] 创建大厅，端口：" & port)
        IsHost = True
        RunInNewThread(Sub()
                           Dim id As String = RandomUtils.NextInt(10000000, 99999999).ToString()
                           Dim secret As String = RandomUtils.NextInt(10, 99).ToString()
                           TargetLobby = New LobbyInfo With {
                               .NetworkName = id,
                               .NetworkSecret = secret,
                               .OriginalCode = $"{id}{secret}{port}".FromB10ToB32,
                               .Type = LobbyType.PCLCE,
                               .Port = port
                           }

                           RunInUi(Sub()
                                       BtnFinishPing.Visibility = Visibility.Collapsed
                                       BtnConnectType.Visibility = Visibility.Collapsed
                                       CardPlayerList.Title = "大厅成员列表（正在获取信息）"
                                       StackPlayerList.Children.Clear()
                                       LabConnectUserName.Text = GetUsername()
                                       LabConnectUserType.Text = "创建者"
                                       LabFinishId.Text = TargetLobby.OriginalCode
                                       BtnFinishCopyIp.Visibility = Visibility.Collapsed
                                       BtnCreate.IsEnabled = True
                                       BtnFinishExit.Text = "关闭大厅"
                                       CurrentSubpage = Subpages.PanFinish
                                   End Sub)

                           Dim result = LobbyController.Launch(True, If(SelectedProfile IsNot Nothing, SelectedProfile.Username, ""))
                           If result = 1 Then
                               RunInUi(Sub() CurrentSubpage = Subpages.PanSelect)
                               Hint("创建大厅失败，请向开发者反馈", HintType.Critical)
                               Return
                           End If

                           Dim retryCount As Integer = 0
                           While ETController.Status = ETState.Stopped
                               Thread.Sleep(300)
                               If DlEasyTierLoader IsNot Nothing AndAlso DlEasyTierLoader.State = LoadState.Loading Then Continue While
                               If retryCount > 10 Then
                                   Hint("EasyTier 启动失败", HintType.Critical)
                                   RunInUi(Sub() BtnCreate.IsEnabled = True)
                                   LobbyController.Close()
                                   BtnCreate.IsEnabled = True
                                   RunInUi(Sub() CurrentSubpage = Subpages.PanSelect)
                                   Exit Sub
                               End If
                               retryCount += 1
                           End While
                           Thread.Sleep(1000)
                           StartETWatcher()
                       End Sub, "Link Create Lobby")
    End Sub

    '加入大厅
    Private Sub BtnJoin_Click(sender As Object, e As EventArgs) Handles BtnJoin.Click
        If Not LobbyPrecheck() Then Exit Sub
        Dim id = TextJoinLobbyId.Text
        IsHost = False
        RunInNewThread(Sub()
                           TargetLobby = ParseCode(id)

                           If TargetLobby Is Nothing Then
                               Hint("大厅编号不正确，请检查后重新输入", HintType.Critical)
                               Return
                           End If

                           RunInUi(Sub()
                                       BtnFinishPing.Visibility = Visibility.Visible
                                       LabFinishPing.Text = "-ms"
                                       BtnConnectType.Visibility = Visibility.Visible
                                       LabConnectType.Text = "连接中"
                                       CardPlayerList.Title = "大厅成员列表（正在获取信息）"
                                       StackPlayerList.Children.Clear()
                                       LabConnectUserName.Text = GetUsername()
                                       LabConnectUserType.Text = "加入者"
                                       LabFinishId.Text = TargetLobby.OriginalCode
                                       BtnFinishCopyIp.Visibility = Visibility.Visible
                                       CurrentSubpage = Subpages.PanFinish
                                   End Sub)

                           Dim result = LobbyController.Launch(False, If(SelectedProfile IsNot Nothing, SelectedProfile.Username, ""))
                           If result = 1 Then
                               RunInUi(Sub() CurrentSubpage = Subpages.PanSelect)
                               Hint("加入大厅失败，请向开发者反馈", HintType.Critical)
                               Return
                           End If

                           Dim retryCount As Integer = 0
                           While ETController.Status = ETState.Stopped
                               Thread.Sleep(300)
                               If DlEasyTierLoader IsNot Nothing AndAlso DlEasyTierLoader.State = LoadState.Loading Then Continue While
                               If retryCount > 10 Then
                                   Hint("EasyTier 启动失败", HintType.Critical)
                                   RunInUi(Sub() BtnCreate.IsEnabled = True)
                                   LobbyController.Close()
                                   Exit Sub
                               End If
                               retryCount += 1
                           End While
                           Thread.Sleep(1000)
                           StartETWatcher()
                           Thread.Sleep(500)
                           While Not IsWatcherStarted OrElse McForward Is Nothing OrElse HostInfo Is Nothing
                               Thread.Sleep(500)
                           End While
                           Dim hostname As String = If(String.IsNullOrWhiteSpace(HostInfo.Username), HostInfo.Hostname, HostInfo.Username)
                           RunInUi(Sub() PanNetInfo.Title = $"{hostname} 的大厅")
                       End Sub, "Link Join Lobby")
    End Sub
    Private Sub TextJoinLobbyId_KeyDown(sender As Object, e As KeyEventArgs) Handles TextJoinLobbyId.KeyDown
        If e.Key = Key.Enter Then BtnJoin_Click(sender, e)
    End Sub

#End Region

#Region "PanLoad | 加载中页面"

    '承接状态切换的 UI 改变
    Private Sub OnLoadStateChanged(Loader As LoaderBase, NewState As LoadState, OldState As LoadState)
    End Sub
    Private Shared LoadStep As String = "准备初始化"
    Private Shared Sub SetLoadDesc(Intro As String, [Step] As String)
        Log("连接步骤：" & Intro)
        LoadStep = [Step]
        RunInUiWait(Sub()
                        If FrmLinkLobby Is Nothing OrElse Not FrmLinkLobby.LabLoadDesc.IsLoaded Then Exit Sub
                        FrmLinkLobby.LabLoadDesc.Text = Intro
                        FrmLinkLobby.UpdateProgress()
                    End Sub)
    End Sub

    '承接重试
    Private Sub CardLoad_MouseLeftButtonUp(sender As Object, e As MouseButtonEventArgs) Handles CardLoad.MouseLeftButtonUp
        If Not InitLoader.State = LoadState.Failed Then Exit Sub
        InitLoader.Start(IsForceRestart:=True)
    End Sub

    '取消加载
    Private Sub CancelLoad() Handles BtnLoadCancel.Click
        If InitLoader.State = LoadState.Loading Then
            CurrentSubpage = Subpages.PanSelect
            InitLoader.Abort()
        Else
            InitLoader.State = LoadState.Waiting
        End If
    End Sub

    '进度改变
    Private Sub UpdateProgress(Optional Value As Double = -1)
        If Value = -1 Then Value = InitLoader.Progress
        Dim DisplayingProgress As Double = ColumnProgressA.Width.Value
        If Math.Round(Value - DisplayingProgress, 3) = 0 Then Exit Sub
        If DisplayingProgress > Value Then
            ColumnProgressA.Width = New GridLength(Value, GridUnitType.Star)
            ColumnProgressB.Width = New GridLength(1 - Value, GridUnitType.Star)
            AniStop("LobbyController Progress")
        Else
            Dim NewProgress As Double = If(Value = 1, 1, (Value - DisplayingProgress) * 0.2 + DisplayingProgress)
            AniStart({
                AaGridLengthWidth(ColumnProgressA, NewProgress - ColumnProgressA.Width.Value, 300, Ease:=New AniEaseOutFluent),
                AaGridLengthWidth(ColumnProgressB, (1 - NewProgress) - ColumnProgressB.Width.Value, 300, Ease:=New AniEaseOutFluent)
            }, "LobbyController Progress")
        End If
    End Sub
    Private Sub CardResized() Handles CardLoad.SizeChanged
        RectProgressClip.Rect = New Rect(0, 0, CardLoad.ActualWidth, 12)
    End Sub

#End Region

#Region "PanFinish | 加载完成页面"
    '退出
    Private Sub BtnFinishExit_Click(sender As Object, e As EventArgs) Handles BtnFinishExit.Click
        Dim creatorHint = If(IsHost, vbCrLf & "由于你是大厅创建者，退出后此大厅将会自动解散。", "")
        If MyMsgBox($"你确定要退出大厅吗？{creatorHint}", "确认退出", "确定", "取消", IsWarn:=True) = 1 Then
            CurrentSubpage = Subpages.PanSelect
            BtnFinishExit.Text = "退出大厅"
            LobbyController.Close()
            DetectMcInstance()
        End If
    End Sub

    '复制大厅编号
    Private Sub BtnFinishCopy_Click(sender As Object, e As EventArgs) Handles BtnFinishCopy.Click
        ClipboardSet(LabFinishId.Text)
    End Sub

    '复制 IP
    Private Sub BtnFinishCopyIp_Click(sender As Object, e As EventArgs) Handles BtnFinishCopyIp.Click
        Dim ip As String = "127.0.0.1:" & McForward.LocalPort
        MyMsgBox("大厅创建者的游戏地址：" & ip & vbCrLf & "注意：仅推荐在 MC 多人游戏列表不显示大厅广播时使用 IP 连接！通过 IP 连接将可能要求使用正版档案。", "复制 IP",
                 Button1:="复制", Button2:="返回", Button1Action:=Sub() ClipboardSet(ip))
    End Sub

#End Region

#Region "子页面管理"

    Public Enum Subpages
        PanSelect
        PanFinish
    End Enum
    Private _CurrentSubpage As Subpages = Subpages.PanSelect
    Public Property CurrentSubpage As Subpages
        Get
            Return _CurrentSubpage
        End Get
        Set(value As Subpages)
            If _CurrentSubpage = value Then Exit Property
            _CurrentSubpage = value
            Log("[Link] 子页面更改为 " & GetStringFromEnum(value))
            PageOnContentExit()
        End Set
    End Property

    Private Sub PageLinkLobby_OnPageEnter() Handles Me.PageEnter
        FrmLinkLobby.PanSelect.Visibility = If(CurrentSubpage = Subpages.PanSelect, Visibility.Visible, Visibility.Collapsed)
        FrmLinkLobby.PanFinish.Visibility = If(CurrentSubpage = Subpages.PanFinish, Visibility.Visible, Visibility.Collapsed)
    End Sub

#End Region

End Class
