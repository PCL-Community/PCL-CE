Imports PCL.Core.Link.EasyTier
Imports PCL.Core.Link.Natayark.NatayarkProfileManager
Imports PCL.Core.Link.Lobby.LobbyInfoProvider
Imports PCL.Core.Link
Imports PCL.Core.App

Class PageSetupGameLink

    Private Shadows IsLoaded As Boolean = False
    Private IsFirstLoad As Boolean = True
    Private Sub PageSetupLink_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded

        '重复加载部分
        PanBack.ScrollToHome()

        '非重复加载部分
        If IsLoaded Then Return
        IsLoaded = True

        AniControlEnabled += 1
        Reload()
        AniControlEnabled -= 1

    End Sub
    Public Sub Reload() Handles Me.Loaded
        TextLinkUsername.Text = Config.Link.Username
        '        TextLinkRelay.Text = Config.Link.RelayServer
        '        ComboRelayType.SelectedIndex = Config.Link.RelayType
        '        ComboServerType.SelectedIndex = Config.Link.ServerType
        CheckLatencyFirstMode.Checked = Config.Link.UseLatencyFirstMode
        ComboPreferProtocol.SelectedIndex = CInt(Config.Link.ProtocolPreference)
        CheckTryPunchSym.Checked = Config.Link.TryPunchSym
        CheckEnableIPv6.Checked = Config.Link.EnableIPv6
        CheckEnableCliOutput.Checked = Config.Link.EnableCliOutput

        '        TextRelays.Text = "正在获取信息..."
        '        Do While Not (PageLinkLobby.LobbyAnnouncementLoader.State = LoadState.Finished OrElse PageLinkLobby.LobbyAnnouncementLoader.State = LoadState.Failed)
        '            Thread.Sleep(500)
        '        Loop
        '        If ETRelay.RelayList.Count > 0 Then
        '            TextRelays.Text = ""
        '            For Each Relay In ETRelay.RelayList
        '                Select Case Relay.Type
        '                    Case ETRelayType.Community
        '                        TextRelays.Text += "[社区] "
        '                    Case ETRelayType.Selfhosted
        '                        TextRelays.Text += "[自有] "
        '                    Case Else 'ETRelayType.Custom
        '                        TextRelays.Text += "[自定义] "
        '                End Select
        '                TextRelays.Text += Relay.Name & "，"
        '            Next
        '            TextRelays.Text = TextRelays.Text.BeforeLast("，")
        '        Else
        '            TextRelays.Text = "暂无，你可能需要手动添加中继服务器"
        '        End If
    End Sub
    '初始化
    Public Sub Reset()
        Try
            Config.Link.Reset()
            Log("[Setup] 已初始化联机页设置")
            Hint("已初始化联机页设置！", HintType.Finish, False)
            Reload()
        Catch ex As Exception
            Log(ex, "初始化联机页设置失败", LogLevel.Msgbox)
        End Try

        Reload()
    End Sub

    '将控件改变路由到设置改变
    Private Shared Sub TextBoxChange(sender As MyTextBox, e As Object) Handles TextLinkUsername.ValidatedTextChanged ', TextLinkRelay.ValidatedTextChanged
        If AniControlEnabled = 0 Then Setup.Set(sender.Tag, sender.Text)
    End Sub
    Private Shared Sub ComboBoxChange(sender As MyComboBox, e As Object) 'Handles ComboRelayType.SelectionChanged, ComboServerType.SelectionChanged
        If AniControlEnabled = 0 Then Setup.Set(sender.Tag, sender.SelectedIndex)
    End Sub
    Private Shared Sub CheckBoxChange(sender As MyCheckBox, e As Object) Handles CheckLatencyFirstMode.Change, CheckEnableIPv6.Change, CheckTryPunchSym.Change, CheckEnableCliOutput.Change
        If AniControlEnabled = 0 Then Setup.Set(sender.Tag, sender.Checked)
    End Sub
    Private Shared Sub LinkProtocolPerferenceChange(sender As MyComboBox, e As Object) Handles ComboPreferProtocol.SelectionChanged
        If AniControlEnabled = 0 Then
            Try
                Dim selection = CType(sender.SelectedIndex, LinkProtocolPreference)
                Config.Link.ProtocolPreference = selection
            Catch ex As Exception
                Log(ex, "改变配置项失败", LogLevel.Hint)
            End Try
        End If
    End Sub

    '网络测试
    Private Sub BtnNetTest_Click(sender As Object, e As RoutedEventArgs) Handles BtnNetTest.Click
        Try
            BtnNetTest.IsEnabled = False
            BtnNetTest.Text = "正在测试"
            RunInNewThread(Sub()
                               Dim status = Scaffolding.EasyTier.CliNetTest.GetNetStatusAsync().GetAwaiter().GetResult()
                               RunInUi(Sub()
                                           TextUdpNatType.Text = "UDP NAT 类型: " & Scaffolding.EasyTier.CliNetTest.GetNatTypeString(status.UdpNatType)
                                           TextTcpNatType.Text = "TCP NAT 类型: " & Scaffolding.EasyTier.CliNetTest.GetNatTypeString(status.TcpNatType)
                                           TextIpv6Status.Text = "IPv6: " & If(status.SupportIPv6, "支持", "不支持")
                                           BtnNetTest.IsEnabled = True
                                           BtnNetTest.Text = "开始测试"
                                       End Sub)
                           End Sub)
        Catch ex As Exception
            Log(ex, "[Link] 获取网络测试结果失败", LogLevel.Hint)
            BtnNetTest.IsEnabled = True
            BtnNetTest.Text = "开始测试"
        End Try
    End Sub

End Class
