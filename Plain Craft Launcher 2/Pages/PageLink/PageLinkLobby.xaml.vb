Public Class PageLinkLobby
    '记录的启动情况
    Public Shared IsHost As Boolean = Nothing
    Public Shared RemotePort As String = Nothing
    Public Shared Hostname As String = Nothing
    Public Shared IsLoading As Boolean = False

#Region "初始化"

    '加载器初始化
    Private Sub LoaderInit() Handles Me.Initialized
        PageLoaderInit(Load, PanLoad, PanContent, PanAlways, InitLoader, AutoRun:=False)
        '注册自定义的 OnStateChanged
        AddHandler InitLoader.OnStateChangedUi, AddressOf OnLoadStateChanged
    End Sub

    Private IsLoad As Boolean = False
    Private Sub OnLoaded() Handles Me.Loaded
        RunInNewThread(Sub()
                           If Not Setup.Get("LinkEula") Then
                               Select Case MyMsgBox($"在使用 PCL CE 大厅之前，请阅读并同意以下条款：{vbCrLf}{vbCrLf}我承诺严格遵守中国大陆相关法律法规，不会将大厅功能用于违法违规用途。{vbCrLf}我承诺使用大厅功能带来的一切风险自行承担。{vbCrLf}我已知晓并同意 PCL CE 收集经处理的本机识别码、Natayark ID 与其他信息并在必要时提供给执法部门。{vbCrLf}{vbCrLf}另外，你还需要同意《Natayark OpenID 服务条款》。", "联机大厅协议授权",
                                                    "我已阅读并同意", "拒绝并返回", "查看 Natayark 服务协议",
                                                    Button3Action:=Sub() OpenWebsite(""))
                                   Case 1
                                       Setup.Set("LinkEula", True)
                                   Case 2
                                       RunInUi(Sub() FrmMain.PageChange(New FormMain.PageStackData With {.Page = FormMain.PageType.Launch}))
                               End Select
                           End If
                       End Sub)
        If IsLoad Then Exit Sub
        IsLoad = True
        IsMcWatcherRunning = True
        DetectMcInstance()
    End Sub
    Private Sub OnPageExit() Handles Me.PageExit
        IsMcWatcherRunning = False
    End Sub

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

#Region "信息获取与展示"

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
    Private Function PlayerInfoItem(Info As ETPlayerInfo, OnClick As MyListItem.ClickEventHandler)
        Dim NewItem As New MyListItem With {
                .Title = Info.NaidName,
                .Info = If(Info.IsHost, "[主机] ", "") & If(Info.Cost = "Local", "[本机]", $"{Info.Ping}ms / {Info.Cost}{If(Not Info.Loss = 0, $" / 丢包 {Info.Loss}%", "")}"),
                .Type = MyListItem.CheckType.Clickable,
                .Tag = Info
        }
        AddHandler NewItem.Click, OnClick
        Return NewItem
    End Function
    Private Sub PlayerInfoClick(sender As MyListItem, e As EventArgs)
        MyMsgBox($"Natayark ID：{sender.Tag.NaidName}{If(sender.Tag.McName IsNot Nothing, "，启动器使用的 MC 档案名称：" & sender.Tag.McName, "")}{vbCrLf}延迟：{sender.Tag.Ping}ms，丢包率：{sender.Tag.Loss}%，连接方式：{sender.Tag.Cost}，NAT 类型：{sender.Tag.NatType}",
                 $"玩家 {If(sender.Tag.McName, sender.Tag.NaidName)} 的详细信息")
    End Sub

    Private IsWatcherStarted As Boolean = False
    Private IsMcWatcherRunning As Boolean = False
    Private IsETFirstCheckFinished As Boolean = False
    '检测本地 MC 局域网实例
    Private Sub DetectMcInstance() Handles BtnRefresh.Click
        ComboWorldList.Items.Clear()
        ComboWorldList.Items.Add(New ComboBoxItem With {.Tag = Nothing, .Content = "正在检测本地游戏...", .Height = 18, .Margin = New Thickness(8, 4, 0, 0)})
        ComboWorldList.SelectedIndex = 0
        BtnCreate.IsEnabled = False
        BtnRefresh.IsEnabled = False
        ComboWorldList.IsEnabled = False
        RunInNewThread(Sub()
                           Dim Worlds As List(Of WorldInfo) = MCInstanceFinding.GetAwaiter().GetResult()
                           RunInUi(Sub()
                                       ComboWorldList.Items.Clear()
                                       If Worlds.Count = 0 Then
                                           ComboWorldList.Items.Add(New ComboBoxItem With {.Tag = Nothing, .Content = "无可用实例", .Height = 18, .Margin = New Thickness(8, 4, 0, 0)})
                                       Else
                                           For Each World In Worlds
                                               ComboWorldList.Items.Add(New ComboBoxItem With {.Tag = World, .Content = $"{World.Description} ({World.VersionName} / 端口 {World.Port})",
                                                                        .Height = 18, .Margin = New Thickness(8, 4, 0, 0)})
                                           Next
                                           BtnCreate.IsEnabled = True
                                       End If
                                       ComboWorldList.SelectedIndex = 0
                                       BtnRefresh.IsEnabled = True
                                       ComboWorldList.IsEnabled = True
                                   End Sub)
                       End Sub)
    End Sub
    'EasyTier Cli 信息获取
    Private Sub StartWatcherThread()
        RunInNewThread(Sub()
                           If IsHost Then
                               Log($"[Link] 本机角色：大厅创建者，隐藏 Ping 信息和连接类型信息")
                               RunInUi(Sub()
                                           SplitLineBeforePing.Visibility = Visibility.Collapsed
                                           BtnFinishPing.Visibility = Visibility.Collapsed
                                           SplitLineBeforeType.Visibility = Visibility.Collapsed
                                           BtnConnectType.Visibility = Visibility.Collapsed
                                       End Sub)
                           Else
                               Log("[Link] 本机角色：加入者，开始获取 Ping 信息和连接类型信息")
                           End If
                           Log("[Link] 启动 EasyTier 监视")
                           IsWatcherStarted = True
                           While ETProcess IsNot Nothing AndAlso ETProcess.HasExited = False
                               GetETInfo()
                               'ETCliProcess.Kill()
                               Thread.Sleep(15000)
                           End While
                           If ETProcess Is Nothing OrElse ETProcess.HasExited Then
                               RunInUi(Sub()
                                           CurrentSubpage = Subpages.PanSelect
                                           Log("[Link] EasyTier 已退出")
                                       End Sub)
                           End If
                           Log("[Link] EasyTier 监视线程已退出")
                           IsWatcherStarted = False
                       End Sub, "EasyTier Status Watcher", ThreadPriority.BelowNormal)
    End Sub
    Private Sub GetETInfo(Optional RemainRetry As Integer = 3)
        Dim ETCliProcess As New Process With {
                                   .StartInfo = New ProcessStartInfo With {
                                       .FileName = $"{ETPath}\easytier-cli.exe",
                                       .WorkingDirectory = ETPath,
                                       .Arguments = ETProcess.StartInfo.Arguments,
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
        Dim ETCliOutput As String = Nothing
        Dim HostPing As String = Nothing
        Dim ConnectType As String = Nothing
        Dim ConnectTypeOriginal As String = Nothing

        ETCliProcess.StartInfo.Arguments = "peer"
        Try
            ETCliProcess.Start()
            Thread.Sleep(100)
            ETCliOutput = ETCliProcess.StandardOutput.ReadToEnd() & ETCliProcess.StandardError.ReadToEnd()
            'Log($"[Link] 获取到 EasyTier Cli 信息: {vbCrLf}" + ETCliOutput)
            If Not ETCliOutput.Contains("10.114.51.41/24") Then
                If RemainRetry > 0 Then
                    Log($"[Link] 未找到大厅创建者 IP，可能是并不存在该大厅，放弃前再重试 {RemainRetry} 次")
                    Thread.Sleep(1000)
                    GetETInfo(RemainRetry - 1)
                    Exit Sub
                End If
                Hint("该大厅不存在", HintType.Critical)
                RunInUi(Sub() CurrentSubpage = Subpages.PanSelect)
                ExitEasyTier()
                Exit Sub
            End If

            HostPing = Math.Round(Val(ETCliOutput.Split("│ 10.114.51.41/24 │")(1).Split("│")(2).Trim().Split(".")(0))).ToString()
            Hostname = ETCliOutput.Split("│ 10.114.51.41/24 │")(1).Split("│")(0).Split("-")(1).Trim()
            RemotePort = ETCliOutput.Split("│ 10.114.51.41/24 │")(1).Split("-")(0).Trim()

            ConnectTypeOriginal = ETCliOutput.Split("│ 10.114.51.41/24 │")(1).Split("│")(1).Trim()
            If ConnectTypeOriginal.Contains("peer") OrElse ConnectTypeOriginal.Contains("p2p") Then
                ConnectType = "P2P"
            ElseIf ConnectTypeOriginal.Contains("relay") Then
                ConnectType = "中继"
            ElseIf ConnectTypeOriginal.Contains("Local") Then
                ConnectType = "本机"
            Else
                ConnectType = "未知"
            End If
            Dim PlayerNum As Integer = 0
            Dim PlayerList As New List(Of ETPlayerInfo)
            'e.g. │ ipv4 │ hostname │ cost │ lat_ms │ loss_rate │ rx_bytes │ tx_bytes │ tunnel_proto │ nat_type │ id │ version │
            For Each PlayerInfo In ETCliOutput.Split(New String(vbLf))
                'Log("当前行：" & PlayerInfo)
                If PlayerInfo.Contains("───────") OrElse PlayerInfo.ContainsF("hostname", True) OrElse String.IsNullOrWhiteSpace(PlayerInfo) Then Continue For
                If PlayerInfo.Split("│")(2).Trim().Contains("PublicServer") Then Continue For '服务器
                Dim ETInfo As New ETPlayerInfo With {
                    .IsHost = Not PlayerInfo.Split("│")(2).Trim().StartsWithF("J-", True),
                    .Hostname = PlayerInfo.Split("│")(2).Trim(),
                    .Cost = PlayerInfo.Split("│")(3).Trim(),
                    .Ping = Math.Round(Val(PlayerInfo.Split("│")(4).Trim())),
                    .Loss = Math.Round(Val(PlayerInfo.Split("│")(5).Trim()) * 100, 1),
                    .NatType = PlayerInfo.Split("│")(9).Trim(),
                    .McName = If(PlayerInfo.Split("│")(2).Split("-").Length = 3, PlayerInfo.Split("│")(2).Split("-")(2).Trim(), Nothing),
                    .NaidName = PlayerInfo.Split("│")(2).Trim().Split("-")(1).Trim()
                }
                PlayerList.Add(ETInfo)
                PlayerNum += 1
                If ETInfo.Cost.ContainsF("Local", True) Then
                    Dim Quality As String = "较差"
                    If ETInfo.NatType.ContainsF("OpenInternet", True) OrElse ETInfo.NatType.ContainsF("NoPAT", True) OrElse ETInfo.NatType.ContainsF("FullCone", True) Then
                        Quality = "优秀"
                    ElseIf ETInfo.NatType.ContainsF("Restricted", True) OrElse ETInfo.NatType.ContainsF("PortRestricted", True) Then
                        Quality = "一般"
                    Else
                        Quality = "较差"
                    End If
                    RunInUi(Sub() LabFinishQuality.Text = Quality)
                End If
            Next
            RunInUi(Sub()
                        StackPlayerList.Children.Clear()
                        For Each Player In PlayerList
                            Dim NewItem = PlayerInfoItem(Player, AddressOf PlayerInfoClick)
                            StackPlayerList.Children.Add(NewItem)
                        Next
                        CardPlayerList.Title = $"大厅成员列表（共 {PlayerNum} 人）"
                    End Sub)
            If Not IsHost Then
                RunInUi(Sub()
                            LabFinishPing.Text = HostPing + "ms"
                            SplitLineBeforePing.Visibility = Visibility.Visible
                            BtnFinishPing.Visibility = Visibility.Visible
                            LabConnectType.Text = ConnectType
                            SplitLineBeforeType.Visibility = Visibility.Visible
                            BtnConnectType.Visibility = Visibility.Visible
                        End Sub)
            End If
            IsETFirstCheckFinished = True
        Catch ex As Exception
            Log(ex, "[Link] EasyTier Cli 线程异常")
            IsWatcherStarted = False
        End Try
    End Sub
#End Region

#Region "PanSelect | 种类选择页面"

    Public LocalPort As String = Nothing
    '创建房间
    Private Sub BtnSelectCreate_MouseLeftButtonUp(sender As Object, e As MouseButtonEventArgs) Handles BtnCreate.Click
        If Not IsAdmin() Then
            MyMsgBox($"现阶段要使用大厅，需要以管理员身份启动 PCL。{vbCrLf}请退出启动器，右键点击启动器程序，选择 ⌈以管理员身份运行⌋，然后继续操作。", "需要管理员权限", "我知道了", ForceWait:=True)
            Exit Sub
        End If
        BtnCreate.IsEnabled = False
        IsLoading = True
        Dim LocalPort As String = ComboWorldList.SelectedItem.Tag.Port.ToString()
        Log("[Link] 创建大厅，端口：" & LocalPort)
        IsHost = True
        RunInNewThread(Sub()
                           'CreateNATTranversal(LocalPort)
                           LaunchLink(True, LocalPort:=LocalPort)
                           RunInUi(Sub()
                                       CardPlayerList.Title = "大厅成员列表（正在获取信息）"
                                       LabFinishTitle.Text = "大厅创建中..."
                                       LabFinishDesc.Text = $"您是大厅创建者，使用 {NaidProfile.Username} 的身份进行联机"
                                   End Sub)
                           Dim RetryCount As Integer = 0
                           While Not IsETRunning
                               Thread.Sleep(300)
                               If DlEasyTierLoader IsNot Nothing AndAlso DlEasyTierLoader.State = LoadState.Loading Then Continue While
                               If RetryCount > 10 Then
                                   Hint("EasyTier 启动失败", HintType.Critical)
                                   RunInUi(Sub() BtnCreate.IsEnabled = True)
                                   ExitEasyTier()
                                   Exit Sub
                               End If
                               RetryCount += 1
                           End While
                           RunInUi(Sub()
                                       BtnCreate.IsEnabled = True
                                       CurrentSubpage = Subpages.PanFinish
                                       LabFinishTitle.Text = "大厅已创建"
                                       BtnCreate.IsEnabled = True
                                   End Sub)
                           Thread.Sleep(1000)
                           StartWatcherThread()
                       End Sub)
    End Sub

    Public JoinedLobbyId As String = Nothing
    '加入房间
    Private Sub BtnSelectJoin_MouseLeftButtonUp(sender As Object, e As MouseButtonEventArgs) Handles BtnSelectJoin.MouseLeftButtonUp
        If Not IsAdmin() Then
            MyMsgBox($"现阶段要使用大厅，需要以管理员身份启动 PCL。{vbCrLf}请退出启动器，右键点击启动器程序，选择 ⌈以管理员身份运行⌋，然后继续操作。", "需要管理员权限", "我知道了", ForceWait:=True)
            Exit Sub
        End If
        JoinedLobbyId = MyMsgBoxInput("输入大厅编号", HintText:="例如：01509230")
        If JoinedLobbyId = Nothing Then Exit Sub
        If JoinedLobbyId.Length < 8 Then
            Hint("大厅编号不合法", HintType.Critical)
            Exit Sub
        End If
        RunInNewThread(Sub()
                           LaunchLink(False, JoinedLobbyId)
                           RunInUi(Sub()
                                       CardPlayerList.Title = "大厅成员列表（正在获取信息）"
                                       LabFinishTitle.Text = "加入大厅中..."
                                       LabFinishDesc.Text = $"您是加入者，使用 {NaidProfile.Username} 的身份进行联机"
                                   End Sub)
                           Dim RetryCount As Integer = 0
                           While Not IsETRunning
                               Thread.Sleep(300)
                               If DlEasyTierLoader IsNot Nothing AndAlso DlEasyTierLoader.State = LoadState.Loading Then Continue While
                               If RetryCount > 10 Then
                                   Hint("EasyTier 启动失败", HintType.Critical)
                                   RunInUi(Sub() BtnCreate.IsEnabled = True)
                                   ExitEasyTier()
                                   Exit Sub
                               End If
                               RetryCount += 1
                           End While
                           Thread.Sleep(1000)
                           StartWatcherThread()
                           Thread.Sleep(500)
                           While IsWatcherStarted AndAlso RemotePort Is Nothing
                               Thread.Sleep(500)
                           End While
                           McPortForward("10.114.51.41", RemotePort, "§ePCL CE 大厅 - " & Hostname)
                           RunInUi(Sub() LabFinishTitle.Text = $"已加入 {Hostname} 的大厅")
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
        If MyMsgBox("你确定要退出大厅吗？", "确认退出", "确定", "取消", IsWarn:=True) = 1 Then
            CurrentSubpage = Subpages.PanSelect
            ExitEasyTier()
            StopMcPortForward()
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
