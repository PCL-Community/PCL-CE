Imports System.Collections.ObjectModel
Imports System.Collections.Specialized
Imports PCL.Core.App
Imports PCL.Core.Link
Imports PCL.Core.Link.EasyTier
Imports PCL.Core.Link.Lobby
Imports PCL.Core.Link.Lobby.LobbyInfoProvider
Imports PCL.Core.Link.Scaffolding.Client.Models

Public Class PageLinkLobby

#Region "初始化"

    '加载器初始化
    Private Sub LoaderInit() Handles Me.Initialized
        PageLoaderInit(Load, PanLoad, PanContent, Nothing, InitLoader, AutoRun:=False)
        '注册自定义的 OnStateChanged
        AddHandler InitLoader.OnStateChangedUi, AddressOf OnLoadStateChanged

        AddHandler LobbyService.OnNeedDownloadEasyTier, AddressOf DownloadEasyTier
        AddHandler LobbyService.OnHint, AddressOf ShowHintFromService
        AddHandler LobbyService.DiscoveredWorlds.CollectionChanged, AddressOf OnDiscoveredWorldsChanged
        AddHandler LobbyService.Players.CollectionChanged, AddressOf OnPlayersChanged
        AddHandler LobbyService.OnUserStopGame, AddressOf OnUserStopGame
        AddHandler LobbyService.OnClientPing, AddressOf OnClientPingHandler
        AddHandler LobbyService.OnServerShutDown, AddressOf OnServerShuttedDownHandler
        AddHandler LobbyService.OnServerStarted, AddressOf OnServerStartedHandler
        AddHandler LobbyService.OnServerException, AddressOf OnServerExceptionHandler

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

    Private Async Sub OnServerExceptionHandler(ex As Exception)
        RunInUi(Sub() Hint(ex.Message, HintType.Critical))

        Try
            Await LobbyService.LeaveLobbyAsync()

            RunInUi(Sub()
                        CardPlayerList.Title = "大厅成员列表（正在获取信息）"
                        StackPlayerList.Children.Clear()
                        CurrentSubpage = Subpages.PanSelect
                    End Sub)
        Catch secEx As Exception
            Log(secEx, "Occured an exception when exit server.")
            Hint("在服务器退出时发生了错误！", HintType.Critical)
        End Try
    End Sub


    Public Async Sub Reload() Handles Me.Loaded
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
        Await Dispatcher.BeginInvoke(Async Sub() Await _LinkAnnounceUpdate()) '我实在不理解为啥 BeginInvoke 这个委托要 MustBeInherit

        Await LobbyService.InitializeAsync().ConfigureAwait(False)
    End Sub

#End Region

#Region "加载步骤"

    Private Shared WithEvents InitLoader As New LoaderCombo(Of Integer)("大厅初始化", {
        New LoaderTask(Of Integer, Integer)("初始化", AddressOf InitTask) With {.ProgressWeight = 0.5}
    })
    Private Shared Async Sub InitTask(task As LoaderTask(Of Integer, Integer))
        Await LobbyService.InitializeAsync()
    End Sub

#Region "Subscribser"
    Private Sub OnServerStartedHandler()
        Log("Received server started event.")
        RunInUi(Sub()
            LabFinishId.Text = LobbyService.CurrentLobbyCode
            StackPlayerList.Children.Clear()
            For Each player As PlayerProfile In LobbyService.Players
                StackPlayerList.Children.Add(PlayerInfoItem(player, AddressOf PlayerInfoClick))
            Next
        End Sub)
    End Sub

    Private Async Sub OnServerShuttedDownHandler()
        Try
            Await LobbyService.LeaveLobbyAsync()

            RunInUi(Sub()
                        CardPlayerList.Title = "大厅成员列表（正在获取信息）"
                        StackPlayerList.Children.Clear()
                        CurrentSubpage = Subpages.PanSelect
                    End Sub)
        Catch ex As Exception
            Log(ex, "Occured an exception when exit server.")
            Hint("在服务器退出时发生了错误！", HintType.Critical)
        End Try
    End Sub
    Private Sub OnClientPingHandler(latency As Long)
        RunInUi(Sub()
                    LabFinishQuality.Text = "已连接"
                    LabFinishPing.Text = latency.ToString() + "ms"
                    LabConnectType.Text = "暂不可用"
                End Sub)
    End Sub

    Private Sub OnUserStopGame()
        RunInUi(Sub()
                    CardPlayerList.Title = "大厅成员列表（正在获取信息）"
                    StackPlayerList.Children.Clear()
                    CurrentSubpage = Subpages.PanSelect
                End Sub)
        MyMsgBox("由于你关闭了联机中的 MC 实例，大厅已自动解散。", "大厅已解散")
    End Sub

    
    Private Sub OnPlayersChanged(sender As Object, e As NotifyCollectionChangedEventArgs)
        Log("接收到玩家列表改变事件")
        RunInUi(Sub()
            Select Case e.Action
                Case NotifyCollectionChangedAction.Add
                    For Each player As PlayerProfile In e.NewItems
                        StackPlayerList.Children.Add(PlayerInfoItem(player, AddressOf PlayerInfoClick))
                    Next

                Case NotifyCollectionChangedAction.Remove
                    For Each player As PlayerProfile In e.OldItems
                        Dim itemToRemove = StackPlayerList.Children.OfType(Of MyListItem)().
                                FirstOrDefault(Function(item) item.Tag.MachineId = player.MachineId)
                        If itemToRemove IsNot Nothing Then
                            StackPlayerList.Children.Remove(itemToRemove)
                        End If
                    Next

                Case Else
                    StackPlayerList.Children.Clear()
                    For Each player As PlayerProfile In LobbyService.Players
                        StackPlayerList.Children.Add(PlayerInfoItem(player, AddressOf PlayerInfoClick))
                    Next
            End Select

            LabFinishQuality.Text = "已连接"
            CardPlayerList.Title = $"大厅成员列表（共 {LobbyService.Players.Count} 人）"
        End Sub)
    End Sub

    Private Sub OnDiscoveredWorldsChanged(sender As Object, e As NotifyCollectionChangedEventArgs)
        Log("Found new world.")

        RunInUi(Sub()
                    If e.Action = NotifyCollectionChangedAction.Reset Then
                        ComboWorldList.Items.Clear()
                        For Each world As FoundWorld In LobbyService.DiscoveredWorlds
                            ComboWorldList.Items.Add(New MyComboBoxItem() With {
                                .Tag = world.Port,
                                .Content = world.Name
                            })
                        Next
                    End If

                    ' 当有新项目添加时
                    If e.NewItems IsNot Nothing Then
                        For Each world As FoundWorld In e.NewItems
                            ComboWorldList.Items.Add(New MyComboBoxItem() With {
                                .Tag = world.Port,
                                .Content = world.Name
                            })
                        Next
                    End If

                    ' 当有项目被移除时
                    If e.OldItems IsNot Nothing Then
                        Dim portsToRemove = e.OldItems.Cast(Of FoundWorld)().Select(Function(w) w.Port).ToHashSet()
                        Dim itemsToRemove = ComboWorldList.Items.Cast(Of MyComboBoxItem)().Where(Function(item) portsToRemove.Contains(CType(item.Tag, Integer))).ToList()
                        For Each item In itemsToRemove
                            ComboWorldList.Items.Remove(item)
                        Next
                    End If

                    ' 更新UI状态
                    Dim hasItems = ComboWorldList.Items.Count > 0
                    ComboWorldList.IsEnabled = hasItems
                    BtnCreate.IsEnabled = hasItems
                    If hasItems AndAlso ComboWorldList.SelectedIndex = -1 Then
                        ComboWorldList.SelectedIndex = 0
                    End If
                End Sub)
    End Sub

    Private Shared Sub ShowHintFromService(msg As String, type As CoreHintType)
        RunInUi(Sub()
                    Select Case type
                        Case CoreHintType.Info
                            Hint(msg, HintType.Info)
                        Case CoreHintType.Finish
                            Hint(msg, HintType.Finish)
                        Case CoreHintType.Critical
                            Hint(msg, HintType.Critical)
                    End Select
                End Sub)
    End Sub
#End Region


#End Region

#Region "公告"
    Public Shared LobbyAnnouncementLoader As LoaderCombo(Of Integer) = Nothing
    Private ReadOnly _linkAnnounces As New ObservableCollection(Of LinkAnnounceInfo)
    Private _linkAnnounceUpdateCancelSource As CancellationTokenSource = Nothing
    '公告轮播实现
    Private Async Function _LinkAnnounceUpdate() As Task
        Dim currentIndex = 0
        Dim globalCancelToken As CancellationToken = _linkAnnounceUpdateCancelSource.Token
        Dim waiterCts As CancellationTokenSource = Nothing

        AddHandler _linkAnnounces.CollectionChanged,
            Sub(sender, e)
                If waiterCts IsNot Nothing Then waiterCts.Cancel()
            End Sub

        While Not globalCancelToken.IsCancellationRequested
            waiterCts = CancellationTokenSource.CreateLinkedTokenSource(globalCancelToken)
            Dim waiterCancelToken = waiterCts.Token

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
            waiterCts = Nothing
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
                    For Each notice As JObject In notices
                        Dim announceContent = notice("content").ToString()
                        If Not String.IsNullOrWhiteSpace(announceContent) Then

                            If VersionCode < Val(notice("minVer")) OrElse VersionCode > Val(notice("maxVer")) Then Continue For

                            Dim type As LinkAnnounceType

                            If notice("type") = "important" OrElse notice("type") = "red" Then
                                type = LinkAnnounceType.Important
                            ElseIf notice("type") = "warning" OrElse notice("type") = "yellow" Then
                                type = LinkAnnounceType.Warning
                            Else
                                type = LinkAnnounceType.Notice
                            End If

                            Dim announces As String() = announceContent.Split(vbLf)

                            For Each announce As String In announces
                                _linkAnnounces.Add(New LinkAnnounceInfo(type, announce))
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
    Private Function PlayerInfoItem(info As PlayerProfile, onClick As MyListItem.ClickEventHandler)
        Dim details As String = Nothing
        If info.Kind = PlayerKind.HOST Then details += "[主机] "
        details += info.Vendor
        'If info.Cost = ETConnectionType.Local Then
        'details += $"[本机] NAT {LobbyTextHandler.GetNatTypeChinese(info.NatType)}"
        'Else
        'details += $"{info.Ping}ms / {LobbyTextHandler.GetConnectTypeChinese(info.Cost)}"
        'End If
        Dim newItem As New MyListItem With {
                .Title = info.Name,
                .Info = details,
                .Type = MyListItem.CheckType.Clickable,
                .Tag = info
        }
        AddHandler newItem.Click, onClick
        Return newItem
    End Function
    Private Sub PlayerInfoClick(sender As MyListItem, e As EventArgs)
        Dim info As PlayerProfile = sender.Tag
        Dim msg As String = Nothing
        msg += $"用户名：{info.Name}"
        msg += vbCrLf
        msg += $"联机协议客户端标识：{info.Vendor}"
        'msg += $"{If(info.Cost = ETConnectionType.Local, "本机 ", $"延迟：{info.Ping}ms，丢包率：{info.Loss}%，连接方式：{LobbyTextHandler.GetConnectTypeChinese(info.Cost)}，")}NAT 类型：{LobbyTextHandler.GetNatTypeChinese(info.NatType)}"
        msg += vbCrLf
        msg += "此处数据仅供参考，请以实际游玩体验为准。"
        msg += vbCrLf + vbCrLf
        msg += "若想了解 NAT 类型与其如何影响联机体验，请前往界面左侧的常见问题一栏。"
        MyMsgBox(msg, $"玩家 {info.Name} 的详细信息")
    End Sub
#End Region
    Private Sub PasteLobbyId() Handles BtnPaste.Click
        Dim lobbyId As String
        Try
            Dim clipText = Clipboard.GetText(TextDataFormat.Text)
            lobbyId = clipText
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

    '刷新按钮
    Private Sub BtnRefresh_Click(sender As Object, e As EventArgs) Handles BtnRefresh.Click
        Dim lobby = LobbyService.DiscoverWorldAsync()
    End Sub
    
    '创建大厅
    Private Async Sub BtnCreate_Click(sender As Object, e As EventArgs) Handles BtnCreate.Click
        If ComboWorldList.SelectedItem Is Nothing Then
            Hint("请先选择一个要联机的世界！", HintType.Info)
            Return
        End If

        BtnCreate.IsEnabled = False

        If Not LobbyPrecheck() Then
            BtnCreate.IsEnabled = True
            Exit Sub
        End If

        Dim port = CType(ComboWorldList.SelectedItem.Tag, Integer)
        Log("[Link] 创建大厅，端口：" & port)


        Dim username = GetUsername()

        RunInUi(Sub()
                    BtnFinishPing.Visibility = Visibility.Collapsed
                    LabFinishPing.Text = "-ms"
                    BtnConnectType.Visibility = Visibility.Collapsed
                    LabConnectType.Text = "连接中"
                    CardPlayerList.Title = "大厅成员列表（正在获取信息）"
                    StackPlayerList.Children.Clear()
                    LabConnectUserName.Text = username
                    LabConnectUserType.Text = "创建者"
                    LabFinishId.Text = LobbyService.CurrentLobbyCode
                    BtnFinishCopyIp.Visibility = Visibility.Collapsed
                    BtnCreate.IsEnabled = True
                    BtnFinishExit.Text = "关闭大厅"
                    CurrentSubpage = Subpages.PanFinish
                End Sub)

        Dim res = Await LobbyService.CreateLobbyAsync(port, username).ConfigureAwait(True)

        If res = False Then
            RunInUi(Sub()
                        CardPlayerList.Title = "大厅成员列表（正在获取信息）"
                        StackPlayerList.Children.Clear()
                        CurrentSubpage = Subpages.PanSelect
                    End Sub)
        Else

        End If
    End Sub

    '加入大厅
    Private Async Sub BtnJoin_Click(sender As Object, e As EventArgs) Handles BtnJoin.Click
        If Not LobbyPrecheck() Then Exit Sub

        Log("Start to join lobby.")

        Dim id = TextJoinLobbyId.Text
        Dim username = GetUsername()

        RunInUi(Sub()
                    BtnFinishPing.Visibility = Visibility.Visible
                    LabFinishPing.Text = "-ms"
                    BtnConnectType.Visibility = Visibility.Visible
                    LabConnectType.Text = "连接中"
                    CardPlayerList.Title = "大厅成员列表（正在获取信息）"
                    StackPlayerList.Children.Clear()
                    LabConnectUserName.Text = username
                    LabConnectUserType.Text = "加入者"
                    LabFinishId.Text = id
                    BtnFinishCopyIp.Visibility = Visibility.Visible
                    CurrentSubpage = Subpages.PanFinish
                End Sub)

        Dim res = Await LobbyService.JoinLobbyAsync(id, username).ConfigureAwait(True)

        If res = False Then
            RunInUi(Sub()
                        CardPlayerList.Title = "大厅成员列表（正在获取信息）"
                        StackPlayerList.Children.Clear()
                        CurrentSubpage = Subpages.PanSelect
                    End Sub)
        End If
    End Sub
    Private Sub TextJoinLobbyId_KeyDown(sender As Object, e As KeyEventArgs) Handles TextJoinLobbyId.KeyDown
        If e.Key = Key.Enter Then BtnJoin_Click(sender, e)
    End Sub

#End Region

#Region "PanLoad | 加载中页面"

    '承接状态切换的 UI 改变
    Private Sub OnLoadStateChanged(loader As LoaderBase, newState As LoadState, oldState As LoadState)
    End Sub
    Private Shared _loadStep As String = "准备初始化"
    Private Shared Sub SetLoadDesc(intro As String, [step] As String)
        Log("连接步骤：" & intro)
        _loadStep = [step]
        RunInUiWait(Sub()
                        If FrmLinkLobby Is Nothing OrElse Not FrmLinkLobby.LabLoadDesc.IsLoaded Then Exit Sub
                        FrmLinkLobby.LabLoadDesc.Text = intro
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
    Private Sub UpdateProgress(Optional value As Double = -1)
        If value = -1 Then value = InitLoader.Progress
        Dim displayingProgress As Double = ColumnProgressA.Width.Value
        If Math.Round(value - displayingProgress, 3) = 0 Then Exit Sub
        If displayingProgress > value Then
            ColumnProgressA.Width = New GridLength(value, GridUnitType.Star)
            ColumnProgressB.Width = New GridLength(1 - value, GridUnitType.Star)
            AniStop("LobbyController Progress")
        Else
            Dim newProgress As Double = If(value = 1, 1, (value - displayingProgress) * 0.2 + displayingProgress)
            AniStart({
                AaGridLengthWidth(ColumnProgressA, newProgress - ColumnProgressA.Width.Value, 300, Ease:=New AniEaseOutFluent),
                AaGridLengthWidth(ColumnProgressB, (1 - newProgress) - ColumnProgressB.Width.Value, 300, Ease:=New AniEaseOutFluent)
            }, "LobbyController Progress")
        End If
    End Sub
    Private Sub CardResized() Handles CardLoad.SizeChanged
        RectProgressClip.Rect = New Rect(0, 0, CardLoad.ActualWidth, 12)
    End Sub

#End Region

#Region "PanFinish | 加载完成页面"
    '退出
    Private Async Sub BtnFinishExit_Click(sender As Object, e As EventArgs) Handles BtnFinishExit.Click
        Dim creatorHint = If(LobbyService.IsHost, vbCrLf & "由于你是大厅创建者，退出后此大厅将会自动解散。", "")
        If MyMsgBox($"你确定要退出大厅吗？{creatorHint}", "确认退出", "确定", "取消", IsWarn:=True) = 1 Then
            CurrentSubpage = Subpages.PanSelect
            BtnFinishExit.Text = "退出大厅"
            Await LobbyService.LeaveLobbyAsync().ConfigureAwait(True)
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
