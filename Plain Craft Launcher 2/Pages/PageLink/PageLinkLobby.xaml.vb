Imports PCL.Core.Helper
Imports PCL.Core.Extension
Imports PCL.Core.Utils.Minecraft
Imports PCL.Core.Model

Public Class PageLinkLobby
    '记录的启动情况
    Public Shared IsHost As Boolean = False
    Public Shared RemotePort As String = Nothing
    Public Shared JoinerLocalPort As Integer = Nothing
    Public Shared IsConnected As Boolean = False
    Public Shared LocalInfo As ETPlayerInfo = Nothing
    Public Shared HostInfo As ETPlayerInfo = Nothing

#Region "初始化"

    '加载器初始化
    Private Sub LoaderInit() Handles Me.Initialized
        PageLoaderInit(Load, PanLoad, PanContent, PanAlways, InitLoader, AutoRun:=False)
        '注册自定义的 OnStateChanged
        AddHandler InitLoader.OnStateChangedUi, AddressOf OnLoadStateChanged
    End Sub

    Public IsLoad As Boolean = False
    Private IsLoading As Boolean = False
    Public Sub Reload() Handles Me.Loaded
        If IsLoad OrElse IsLoading Then Exit Sub
        IsLoad = True
        IsLoading = True
        HintAnnounce.Visibility = Visibility.Visible
        HintAnnounce.Text = "正在连接到大厅服务器..."
        HintAnnounce.Theme = MyHint.Themes.Blue
        RunInNewThread(Sub()
                           If Not Setup.Get("LinkEula") Then
                               Select Case MyMsgBox($"在使用 PCL CE 大厅之前，请阅读并同意以下条款：{vbCrLf}{vbCrLf}我承诺严格遵守中国大陆相关法律法规，不会将大厅功能用于违法违规用途。{vbCrLf}我已知晓大厅功能使用途中可能需要提供管理员权限以用于必要的操作，并会确保 PCL CE 为从官方发布渠道下载的副本。{vbCrLf}我承诺使用大厅功能带来的一切风险自行承担。{vbCrLf}我已知晓并同意 PCL CE 收集经处理的本机识别码、Natayark ID 与其他信息并在必要时提供给执法部门。{vbCrLf}为保护未成年人个人信息，使用联机大厅前，我确认我已满十四周岁。{vbCrLf}{vbCrLf}另外，你还需要同意 PCL CE 大厅相关隐私政策及《Natayark OpenID 服务条款》。", "联机大厅协议授权",
                                                    "我已阅读并同意", "拒绝并返回", "查看相关隐私协议",
                                                    Button3Action:=Sub() OpenWebsite("https://www.pclc.cc/privacy/personal-info-brief.html"))
                                   Case 1
                                       Setup.Set("LinkEula", True)
                                   Case 2
                                       RunInUi(Sub()
                                                   FrmMain.PageChange(New FormMain.PageStackData With {.Page = FormMain.PageType.Launch})
                                                   FrmLinkLobby = Nothing
                                               End Sub)
                               End Select
                           End If
                       End Sub)
        IsMcWatcherRunning = True
        GetAnnouncement()
        If Not String.IsNullOrWhiteSpace(Setup.Get("LinkNaidRefreshToken")) Then
            If Not String.IsNullOrWhiteSpace(Setup.Get("LinkNaidRefreshExpiresAt")) AndAlso Convert.ToDateTime(Setup.Get("LinkNaidRefreshExpiresAt")).CompareTo(DateTime.Now) < 0 Then
                Setup.Set("LinkNaidRefreshToken", "")
                Hint("Natayark ID 令牌已过期，请重新登录", HintType.Critical)
            Else
                GetNaidData(Setup.Get("LinkNaidRefreshToken"), True, IsSilent:=True)
            End If
        End If
        DetectMcInstance()
        IsLoading = False
    End Sub
    Private Sub OnPageExit() Handles Me.PageExit
        IsMcWatcherRunning = False
    End Sub
    Private Function IsEasyTierExists()
        Return File.Exists(ETPath & "\easytier-core.exe") AndAlso File.Exists(ETPath & "\easytier-cli.exe") AndAlso File.Exists(ETPath & "\wintun.dll")
    End Function
#End Region

#Region "加载步骤"

    Public Shared WithEvents InitLoader As New LoaderCombo(Of Integer)("大厅初始化", {
        New LoaderTask(Of Integer, Integer)("检查 EasyTier 文件", AddressOf InitFileCheck) With {.ProgressWeight = 0.5}
    })
    Private Shared Sub InitFileCheck(Task As LoaderTask(Of Integer, Integer))
        If Not File.Exists(ETPath & "\easytier-core.exe") OrElse Not File.Exists(ETPath & "\Packet.dll") OrElse
            Not File.Exists(ETPath & "\easytier-cli.exe") OrElse Not File.Exists(ETPath & "\wintun.dll") Then
            Log("[Link] EasyTier 不存在，开始下载")
            DownloadEasyTier()
        Else
            Log("[Link] EasyTier 文件检查完毕")
        End If
    End Sub

#End Region

#Region "公告"
    Public Const AllowedVersion As Integer = 2
    Public Sub GetAnnouncement()
        RunInNewThread(Sub()
                           RunInUi(Sub() HintAnnounce.Visibility = Visibility.Visible)
                           Try
                               Dim ServerNumber As Integer = 0
                               Dim Jobj As JObject = Nothing
                               Dim Cache As Integer = Nothing
Retry:
                               Try
                                   Cache = Val(NetRequestOnce($"{LinkServers(ServerNumber)}/api/link/cache.ini", "GET", Nothing, "application/json", Timeout:=7000))
                                   If Cache = Setup.Get("LinkAnnounceCacheVer") Then
                                       Log("[Link] 使用缓存的公告数据")
                                       Jobj = JObject.Parse(Setup.Get("LinkAnnounceCache"))
                                   Else
                                       Log("[Link] 尝试拉取公告数据")
                                       Dim Received As String = NetRequestOnce($"{LinkServers(ServerNumber)}/api/link/announce.json", "GET", Nothing, "application/json", Timeout:=7000)
                                       Jobj = JObject.Parse(Received)
                                       Setup.Set("LinkAnnounceCache", Received)
                                       Setup.Set("LinkAnnounceCacheVer", Cache)
                                   End If
                               Catch ex As Exception
                                   Log(ex, $"[Link] 从服务器 {ServerNumber} 获取公告缓存失败")
                                   ServerNumber += 1
                                   If ServerNumber <= LinkServers.Count - 1 Then GoTo Retry
                               End Try
                               If Jobj Is Nothing Then Throw New Exception("获取联机数据失败")
                               IsLobbyAvailable = Jobj("available")
                               RequiresRealname = Jobj("requireRealname")
                               '公告
                               Dim Notices As JArray = Jobj("notices")
                               Dim NoticeLatest As JObject = Notices(0)
                               If Not String.IsNullOrWhiteSpace(NoticeLatest("content").ToString()) Then
                                   If NoticeLatest("type") = "important" OrElse NoticeLatest("type") = "red" Then
                                       RunInUi(Sub() HintAnnounce.Theme = MyHint.Themes.Red)
                                   ElseIf NoticeLatest("type") = "warning" OrElse NoticeLatest("type") = "yellow" Then
                                       RunInUi(Sub() HintAnnounce.Theme = MyHint.Themes.Yellow)
                                   Else
                                       RunInUi(Sub() HintAnnounce.Theme = MyHint.Themes.Blue)
                                   End If
                                   RunInUi(Sub() HintAnnounce.Text = NoticeLatest("content").ToString().Replace("\n", vbCrLf))
                               Else
                                   HintAnnounce.Visibility = Visibility.Collapsed
                               End If
                               '中继服务器
                               Dim Relays As JArray = Jobj("relays")
                               ETServerDefList = New List(Of ETRelay)
                               For Each Relay In Relays
                                   ETServerDefList.Add(New ETRelay With {
                                       .Name = Relay("name").ToString(),
                                       .Url = Relay("url").ToString(),
                                       .Type = Relay("type").ToString()
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

#Region "ET 用户信息类"
    Public Class ETPlayerInfo
        Public IsHost As Boolean
        ''' <summary>
        ''' EasyTier 的原始主机名
        ''' </summary>
        Public Hostname As String
        Public McName As String
        Public NaidName As String
        ''' <summary>
        ''' 连接方式，可能为 Local, P2P, Relay 等
        ''' </summary>
        Public Cost As String
        ''' <summary>
        ''' 延迟 (ms)
        ''' </summary>
        Public Ping As Double
        ''' <summary>
        ''' 丢包率 (%)
        ''' </summary>
        Public Loss As Double
        Public NatType As String
    End Class
#End Region

#Region "UI 元素"
    Private Function PlayerInfoItem(Info As ETPlayerInfo, OnClick As MyListItem.ClickEventHandler)
        Dim NewItem As New MyListItem With {
                .Title = Info.NaidName,
                .Info = If(Info.IsHost, "[主机] ", "") & If(Info.Cost = "Local", $"[本机] NAT {GetNatTypeChinese(Info.NatType)}", $"{Info.Ping}ms / {GetConnectTypeChinese(Info.Cost)}{If(Not Info.Loss = 0, $" / 丢包 {Info.Loss}%", "")}"),
                .Type = MyListItem.CheckType.Clickable,
                .Tag = Info
        }
        AddHandler NewItem.Click, OnClick
        Return NewItem
    End Function
    Private Sub PlayerInfoClick(sender As MyListItem, e As EventArgs)
        MyMsgBox($"{If(sender.Tag.NaidName IsNot Nothing, "Natayark ID：" & sender.Tag.NaidName, "来自其他启动器")}{If(sender.Tag.McName IsNot Nothing, "，启动器使用的 MC 档案名称：" & sender.Tag.McName, "")}{vbCrLf}延迟：{sender.Tag.Ping}ms，丢包率：{sender.Tag.Loss}%，连接方式：{GetConnectTypeChinese(sender.Tag.Cost)}，NAT 类型：{GetNatTypeChinese(sender.Tag.NatType)}",
                 $"玩家 {sender.Tag.NaidName} 的详细信息")
    End Sub
#End Region

#Region "获取用户友好的描述信息"
    Private Function GetNatTypeChinese(Type As String) As String
        If Type.ContainsF("OpenInternet", True) OrElse Type.ContainsF("NoPAT", True) Then
            Return "开放"
        ElseIf Type.ContainsF("FullCone", True) Then
            Return "中等（完全圆锥）"
        ElseIf Type.ContainsF("PortRestricted", True) Then
            Return "中等（端口受限圆锥）"
        ElseIf Type.ContainsF("Restricted", True) Then
            Return "中等（受限圆锥）"
        ElseIf Type.ContainsF("SymmetricEasy", True) Then
            Return "严格（宽松对称）"
        ElseIf Type.ContainsF("Symmetric", True) Then
            Return "严格（对称）"
        Else
            Return "未知"
        End If
    End Function
    Private Function GetConnectTypeChinese(Type As String) As String
        If Type.ContainsF("peer", True) OrElse Type.ContainsF("p2p", True) Then
            Return "P2P"
        ElseIf Type.ContainsF("relay", True) Then
            Return "中继"
        ElseIf Type.ContainsF("Local", True) Then
            Return "本机"
        Else
            Return "未知"
        End If
    End Function
    Private Function GetQualityDesc(Quality As Integer) As String
        If Quality >= 3 Then
            Return "优秀"
        ElseIf Quality >= 2 Then
            Return "一般"
        Else
            Return "较差"
        End If
    End Function
#End Region

    Private IsWatcherStarted As Boolean = False
    Private IsMcWatcherRunning As Boolean = False
    Public Shared IsETFirstCheckFinished As Boolean = False
    Private IsDetectingMc As Boolean = False
    '检测本地 MC 局域网实例
    Private Sub DetectMcInstance() Handles BtnRefresh.Click
        If IsDetectingMc Then Exit Sub
        IsDetectingMc = True
        ComboWorldList.Items.Clear()
        ComboWorldList.Items.Add(New MyComboBoxItem With {.Tag = Nothing, .Content = "正在检测本地游戏...", .Height = 18, .Margin = New Thickness(8, 4, 0, 0)})
        ComboWorldList.SelectedIndex = 0
        BtnRefresh.IsEnabled = False
        ComboWorldList.IsEnabled = False
        RunInNewThread(Sub()
                           Dim Worlds As List(Of Tuple(Of Integer, McPingResult)) = MCInstanceFinding.GetAwaiter().GetResult()
                           RunInUi(Sub()
                                       ComboWorldList.Items.Clear()
                                       If Worlds.Count = 0 Then
                                           ComboWorldList.Items.Add(New MyComboBoxItem With {
                                                                    .Tag = Nothing,
                                                                    .Content = "无可用实例"
                                                                    })
                                       Else
                                           For Each World In Worlds
                                               ComboWorldList.Items.Add(New MyComboBoxItem With {
                                                                        .Tag = World,
                                                                        .Content = $"{World.Item2.Description} ({World.Item2.Version.Name} / 端口 {World.Item1})"})
                                           Next
                                       End If
                                       IsDetectingMc = False
                                       ComboWorldList.SelectedIndex = 0
                                       BtnRefresh.IsEnabled = True
                                       ComboWorldList.IsEnabled = True
                                   End Sub)
                       End Sub, "Minecraft Port Detect")
    End Sub
    'EasyTier Cli 轮询
    Public Sub StartETWatcher()
        RunInNewThread(Sub()
                           If IsHost Then
                               Log($"[Link] 本机角色：大厅创建者")
                           Else
                               Log("[Link] 本机角色：加入者")
                           End If
                           Log("[Link] 启动 EasyTier 轮询")
                           IsWatcherStarted = True
                           Dim retryCount As Integer = 0
                           While ETProcess Is Nothing AndAlso retryCount < 10
                               Thread.Sleep(1000)
                               retryCount += 1
                           End While
                           While Not IsETReady
                               GetETInfo()
                               Thread.Sleep(1000)
                           End While
                           While ETProcess IsNot Nothing
                               GetETInfo()
                               If String.IsNullOrWhiteSpace(NaidProfile.AccessToken) Then
                                   Hint("请先登录 Natayark ID 再使用大厅！", HintType.Critical)
                                   ExitEasyTier()
                               End If
                               Thread.Sleep(10000)
                           End While
                           If ETProcess Is Nothing Then
                               RunInUi(Sub()
                                           CurrentSubpage = Subpages.PanSelect
                                           Log("[Link] [ETWatcher] ETProcess 为 null，EasyTier 可能已退出")
                                       End Sub)
                           End If
                           Log("[Link] EasyTier 轮询已结束")
                           IsWatcherStarted = False
                       End Sub, "EasyTier Status Watcher", ThreadPriority.BelowNormal)
    End Sub
    'EasyTier Cli 信息获取
    Private Sub GetETInfo(Optional RemainRetry As Integer = 5)
        Dim ETCliProcess As New Process With {
                                   .StartInfo = New ProcessStartInfo With {
                                       .FileName = $"{ETPath}\easytier-cli.exe",
                                       .WorkingDirectory = ETPath,
                                       .Arguments = "-o json peer",
                                       .ErrorDialog = False,
                                       .CreateNoWindow = True,
                                       .WindowStyle = ProcessWindowStyle.Hidden,
                                       .UseShellExecute = False,
                                       .RedirectStandardOutput = True,
                                       .RedirectStandardError = True,
                                       .RedirectStandardInput = True,
                                       .StandardOutputEncoding = Encoding.UTF8,
                                       .StandardErrorEncoding = Encoding.UTF8},
                                   .EnableRaisingEvents = True
                               }
        Try
            ETCliProcess.Start()
            Thread.Sleep(100)

            Dim ETCliOutput As String = Nothing
            ETCliOutput = ETCliProcess.StandardOutput.ReadToEnd() & ETCliProcess.StandardError.ReadToEnd()
            'Log($"[Link] 获取到 EasyTier Cli 信息: {vbCrLf}" + ETCliOutput)
            If Not ETCliOutput.Contains("10.114.51.41") Then
                If Not IsETFirstCheckFinished AndAlso RemainRetry > 0 Then
                    Log($"[Link] 未找到大厅创建者 IP，可能是并不存在该大厅，放弃前再重试 {RemainRetry} 次")
                    Thread.Sleep(1000)
                    GetETInfo(RemainRetry - 1)
                    Exit Sub
                End If
                If IsETFirstCheckFinished Then
                    Hint("大厅已被解散", HintType.Critical)
                    ToastNotification.SendToast("大厅已被解散", "PCL CE 大厅")
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
                ExitEasyTier()
                Exit Sub
            End If
            '查询大厅成员信息
            Dim PlayerNum As Integer = 0
            Dim PlayerList As New List(Of ETPlayerInfo)
            Dim cliJson As JArray = JArray.Parse(ETCliOutput)
            For Each p In cliJson
                If p("hostname").ToString().Contains("PublicServer") Then Continue For '服务器
                Dim hostnameStatus As Integer = p("hostname").ToString().Split("-").Length
                Dim info As New ETPlayerInfo With {
                    .IsHost = Not p("hostname").ToString().StartsWithF("J-", True),
                    .Hostname = p("hostname"),
                    .Cost = p("cost").ToString().BeforeLast("("),
                    .Ping = Math.Round(Val(p("lat_ms"))),
                    .Loss = Math.Round(Val(p("loss_rate")) * 100, 1),
                    .NatType = p("nat_type"),
                    .McName = If(hostnameStatus = 3, p("hostname").ToString().Split("-")(2), Nothing),
                    .NaidName = If(hostnameStatus = 3 OrElse hostnameStatus = 2, p("hostname").ToString().Split("-")(1), Nothing)
                }
                If info.Cost = "Local" Then LocalInfo = info
                If info.IsHost Then
                    HostInfo = info
                Else
                    PlayerList.Add(info)
                End If
                PlayerNum += 1
            Next
            '本地网络质量评估
            Dim Quality As Integer = 0
            'NAT 评估
            If LocalInfo.NatType.ContainsF("OpenInternet", True) OrElse LocalInfo.NatType.ContainsF("NoPAT", True) OrElse LocalInfo.NatType.ContainsF("FullCone", True) Then
                Quality = 3
            ElseIf LocalInfo.NatType.ContainsF("Restricted", True) OrElse LocalInfo.NatType.ContainsF("PortRestricted", True) Then
                Quality = 2
            Else
                Quality = 1
            End If
            '到主机延迟评估
            If HostInfo.Ping > 150 Then
                Quality -= 1
            End If
            RunInUi(Sub() LabFinishQuality.Text = GetQualityDesc(Quality))
            If IsHost Then '确认创建者实例存活状态
                Dim test As New McPing("127.0.0.1", LocalPort)
                Dim info = test.PingAsync().GetAwaiter().GetResult()
                If info Is Nothing Then
                    Log($"[MCDetect] 本地 MC 局域网实例疑似已关闭，关闭大厅")
                    RunInUi(Sub()
                                CardPlayerList.Title = "大厅成员列表（正在获取信息）"
                                StackPlayerList.Children.Clear()
                                CurrentSubpage = Subpages.PanSelect
                            End Sub)
                    ExitEasyTier()
                    MyMsgBox("由于你关闭了联机中的 MC 实例，大厅已自动解散。", "大厅已解散")
                End If
            End If
            '加入方刷新连接信息
            RunInUi(Sub()
                        If Not IsETReady AndAlso Not HostInfo.Ping = 200 Then
                            IsETReady = True
                        ElseIf Not IsETReady AndAlso HostInfo.Ping = 200 Then '如果 ET 还未就绪，则显示延迟为 0，防止用户找茬
                            HostInfo.Ping = 0
                        End If
                        LabFinishPing.Text = HostInfo.Ping.ToString() & "ms"
                        LabConnectType.Text = GetConnectTypeChinese(HostInfo.Cost)
                    End Sub)
            '刷新大厅成员列表 UI
            RunInUi(Sub()
                        StackPlayerList.Children.Clear()
                        StackPlayerList.Children.Add(PlayerInfoItem(HostInfo, AddressOf PlayerInfoClick))
                        For Each Player In PlayerList
                            If Not IsETReady AndAlso Player.Ping = 200 Then Player.Ping = 0 '如果 ET 还未就绪，则显示延迟为 0，防止用户找茬
                            Dim NewItem = PlayerInfoItem(Player, AddressOf PlayerInfoClick)
                            StackPlayerList.Children.Add(NewItem)
                        Next
                        CardPlayerList.Title = $"大厅成员列表（共 {PlayerNum} 人）"
                    End Sub)
            IsETFirstCheckFinished = True
        Catch ex As Exception
            Log(ex, "[Link] EasyTier Cli 线程异常")
        End Try
    End Sub
#End Region

#Region "PanSelect | 种类选择页面"

    Public LocalPort As String = Nothing
    '创建大厅
    Private Sub BtnSelectCreate_MouseLeftButtonUp(sender As Object, e As MouseButtonEventArgs) Handles BtnCreate.Click
        BtnCreate.IsEnabled = False
        If Not LobbyPrecheck() Then
            BtnCreate.IsEnabled = True
            Exit Sub
        End If
        If ComboWorldList.SelectedItem.ToString() = "无可用实例" OrElse ComboWorldList.SelectedItem.ToString() = "正在检测本地游戏..." Then
            Hint("请先启动并选择一个可用的 MC 联机实例！", HintType.Critical)
            Exit Sub
        End If
        LocalPort = CType(ComboWorldList.SelectedItem.Tag, Tuple(Of Integer, McPingResult)).Item1.ToString()
        Log("[Link] 创建大厅，端口：" & LocalPort)
        IsHost = True
        RunInNewThread(Sub()
                           'CreateNATTranversal(LocalPort)
                           RunInUi(Sub()
                                       SplitLineBeforePing.Visibility = Visibility.Collapsed
                                       BtnFinishPing.Visibility = Visibility.Collapsed
                                       SplitLineBeforeType.Visibility = Visibility.Collapsed
                                       BtnConnectType.Visibility = Visibility.Collapsed
                                       CardPlayerList.Title = "大厅成员列表（正在获取信息）"
                                       StackPlayerList.Children.Clear()
                                       LabConnectUserName.Text = NaidProfile.Username
                                       LabConnectUserType.Text = "创建者"
                                   End Sub)
                           Dim id As String = Nothing
                           For i = 1 To 8 '生成 8 位随机编号
                               id += RandomInteger(0, 9).ToString()
                           Next
                           Dim secret As String = Nothing
                           For i = 1 To 2
                               secret += RandomInteger(0, 9).ToString()
                           Next
                           LaunchLink(True, id, secret, LocalPort)
                           Dim retryCount As Integer = 0
                           While Not IsETRunning
                               Thread.Sleep(300)
                               If DlEasyTierLoader IsNot Nothing AndAlso DlEasyTierLoader.State = LoadState.Loading Then Continue While
                               If retryCount > 10 Then
                                   Hint("EasyTier 启动失败", HintType.Critical)
                                   RunInUi(Sub() BtnCreate.IsEnabled = True)
                                   ExitEasyTier()
                                   Exit Sub
                               End If
                               retryCount += 1
                           End While
                           RunInUi(Sub()
                                       BtnCreate.IsEnabled = True
                                       CurrentSubpage = Subpages.PanFinish
                                       BtnFinishExit.Text = "关闭大厅"
                                       BtnCreate.IsEnabled = True
                                   End Sub)
                           Thread.Sleep(1000)
                           StartETWatcher()
                       End Sub)
    End Sub

    Public JoinedLobbyId As String = Nothing
    '加入大厅
    Private Sub BtnSelectJoin_MouseLeftButtonUp(sender As Object, e As MouseButtonEventArgs) Handles BtnSelectJoin.MouseLeftButtonUp
        If Not LobbyPrecheck() Then Exit Sub
        JoinedLobbyId = MyMsgBoxInput("输入大厅编号", HintText:="例如：0150923014")
        If JoinedLobbyId = Nothing Then Exit Sub
        If JoinedLobbyId.Length < 9 Then
            Hint("大厅编号不合法", HintType.Critical)
            Exit Sub
        End If
        IsHost = False
        RunInNewThread(Sub()
                           RunInUi(Sub()
                                       SplitLineBeforePing.Visibility = Visibility.Visible
                                       BtnFinishPing.Visibility = Visibility.Visible
                                       LabFinishPing.Text = "-ms"
                                       SplitLineBeforeType.Visibility = Visibility.Visible
                                       BtnConnectType.Visibility = Visibility.Visible
                                       LabConnectType.Text = "连接中"
                                       CardPlayerList.Title = "大厅成员列表（正在获取信息）"
                                       StackPlayerList.Children.Clear()
                                       LabConnectUserName.Text = NaidProfile.Username
                                       LabConnectUserType.Text = "加入者"
                                   End Sub)
                           Dim processedId As String = JoinedLobbyId.FromB32ToB10()
                           RemotePort = processedId.Substring(10)
                           LaunchLink(False, processedId.Substring(0, 8), processedId.Substring(8, 2), remotePort:=RemotePort)
                           Dim retryCount As Integer = 0
                           While Not IsETRunning
                               Thread.Sleep(300)
                               If DlEasyTierLoader IsNot Nothing AndAlso DlEasyTierLoader.State = LoadState.Loading Then Continue While
                               If retryCount > 10 Then
                                   Hint("EasyTier 启动失败", HintType.Critical)
                                   RunInUi(Sub() BtnCreate.IsEnabled = True)
                                   ExitEasyTier()
                                   Exit Sub
                               End If
                               retryCount += 1
                           End While
                           Thread.Sleep(1000)
                           StartETWatcher()
                           Thread.Sleep(500)
                           While Not IsWatcherStarted OrElse JoinerLocalPort = Nothing OrElse HostInfo Is Nothing
                               Thread.Sleep(500)
                           End While
                           McPortForward("10.114.51.41", JoinerLocalPort, "§ePCL CE 大厅 - " & HostInfo.NaidName)
                           RunInUi(Sub()
                                       BtnFinishExit.Text = $"退出 {HostInfo.NaidName} 的大厅"
                                   End Sub)
                       End Sub)
        CurrentSubpage = Subpages.PanFinish
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
            AniStop("Lobby Progress")
        Else
            Dim NewProgress As Double = If(Value = 1, 1, (Value - DisplayingProgress) * 0.2 + DisplayingProgress)
            AniStart({
                AaGridLengthWidth(ColumnProgressA, NewProgress - ColumnProgressA.Width.Value, 300, Ease:=New AniEaseOutFluent),
                AaGridLengthWidth(ColumnProgressB, (1 - NewProgress) - ColumnProgressB.Width.Value, 300, Ease:=New AniEaseOutFluent)
            }, "Lobby Progress")
        End If
    End Sub
    Private Sub CardResized() Handles CardLoad.SizeChanged
        RectProgressClip.Rect = New Rect(0, 0, CardLoad.ActualWidth, 12)
    End Sub

#End Region

#Region "PanFinish | 加载完成页面"
    Public Shared PublicIPPort As String = Nothing
    '退出
    Private Sub BtnFinishExit_Click(sender As Object, e As EventArgs) Handles BtnFinishExit.Click
        If MyMsgBox($"你确定要退出大厅吗？{If(IsHost, vbCrLf & "由于你是大厅创建者，退出后此大厅将会自动解散。", "")}", "确认退出", "确定", "取消", IsWarn:=True) = 1 Then
            CurrentSubpage = Subpages.PanSelect
            ExitEasyTier()
            'RemoveNATTranversal()
            'ModLink.RemoveUPnPMapping()
            'LocalPort = Nothing
            Exit Sub
        End If
    End Sub

    '复制大厅编号
    Private Sub BtnFinishCopy_Click(sender As Object, e As EventArgs) Handles BtnFinishCopy.Click
        ClipboardSet(LabFinishId.Text)
    End Sub

    '复制 IP
    Private Sub BtnFinishCopyIp_Click(sender As Object, e As EventArgs) Handles BtnFinishCopyIp.Click
        Dim Ip As String = "10.114.51.41:" & RemotePort
        MyMsgBox("大厅创建者的游戏地址：" & Ip & vbCrLf & "仅推荐在 MC 多人游戏列表不显示大厅广播时使用 IP 连接。通过 IP 连接将可能要求使用正版档案。", "复制 IP",
                 Button1:="复制", Button2:="返回", Button1Action:=Sub() ClipboardSet(Ip))
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
