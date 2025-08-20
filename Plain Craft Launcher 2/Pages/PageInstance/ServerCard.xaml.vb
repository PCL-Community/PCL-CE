Imports System.Windows.Controls.Primitives

Public Class ServerCard
    Dim _server As MinecraftServerInfo
    
    Private Sub BtnSkin_Click(sender As Object, e As RoutedEventArgs) Handles BtnSetting.Click
        BtnSetting.ContextMenu.IsOpen = True
    End Sub

    ''' <summary>
    ''' 初始化服务器卡片
    ''' </summary>
    Public Sub UpdateServerInfo(server As MinecraftServerInfo)
        _server = server
        RunInUi(Sub() UpdateServerUi())
    End Sub
    
    ''' <summary>
    ''' 更新服务器UI
    ''' </summary>
    Private Async Sub UpdateServerUi()
        If _server Is Nothing Then Return
        
        ' 更新服务器名称
        ServerName.Text = _server.Name
        ' ServerPlayer.Text = _server.PlayerCount & " / " & _server.MaxPlayers
        If Not String.IsNullOrEmpty(_server.Icon) Then
            Await SetServerLogoAsync(_server.Icon)
        Else
            SetDefaultLogo()
        End If
        If _server.Status  = ServerStatus.Online
            Signal.Source = New BitmapImage(New Uri("/Images/Icons/" & GetSignalIcon(_server.Ping), UriKind.Relative))
            Signal.ToolTip = _server.Ping.ToString() & "ms"
            ToolTipService.SetPlacement(Signal, PlacementMode.Top)
            
            If _server.PlayerCount <> Nothing AndAlso _server.MaxPlayers <> Nothing Then
                ServerPlayer.Text = $"{_server.PlayerCount} / {_server.MaxPlayers}"
            Else
                ServerPlayer.Text = "???"
            End If
            
            MinecraftFormatter.SetColorfulTextLab(
                _server.Description,
                ServerMotD
            )
        Else If _server.Status = ServerStatus.Pinging
            Signal.Source = New BitmapImage(New Uri("/Images/Icons/loading.png", UriKind.Relative))
            ServerPlayer.Text = "正在连接"
            ServerMotD.Text = "正在连接..."
        Else If _server.Status = ServerStatus.Offline
            Signal.Source = New BitmapImage(New Uri("/Images/Icons/signal_offline.png", UriKind.Relative))
            Signal.ToolTip = "服务器离线"
            ServerPlayer.Text = "离线"
            ServerMotD.Text = "服务器离线"
        End If
    End Sub
    
    Private Function GetSignalIcon(ping As Integer) As String
        Select Case ping
            Case 0 To 99
                Return "signal_5.png" ' 5 条信号
            Case 100 To 299
                Return "signal_4.png" ' 4 条信号
            Case 300 To 599
                Return "signal_3.png" ' 3 条信号
            Case 600 To 999
                Return "signal_2.png" ' 2 条信号
            Case Else
                Return "signal_1.png" ' 1 条信号
        End Select
    End Function
    
    Private Async Function SetServerLogoAsync(base64String As String) As Task
        Try
            ' 提取Base64数据部分
            Dim base64Data = If(base64String.Contains(","),
                                base64String.Split(","c)(1),
                                base64String)

            ' 异步转换图像
            Dim image = Await Task.Run(Function()
                Using ms = New MemoryStream(Convert.FromBase64String(base64Data))
                    Dim bitmap = New BitmapImage()
                    bitmap.BeginInit()
                    bitmap.CacheOption = BitmapCacheOption.OnLoad
                    bitmap.StreamSource = ms
                    bitmap.EndInit()
                    bitmap.Freeze() ' 确保跨线程安全
                    Return bitmap
                End Using
            End Function)
            ServerIcon.Source = image
        Catch ex As Exception
            Log(ex, "图标解析失败，使用默认图标")
            SetDefaultLogo()
        End Try
    End Function

    Private Sub SetDefaultLogo()
        ServerIcon.Source = New BitmapImage(
            New Uri("pack://application:,,,/Plain Craft Launcher 2;component/Images/Icons/DefaultServer.png")
            )
    End Sub
    
    ''' <summary>
    ''' 刷新服务器状态
    ''' </summary>
    Public Async Function RefreshServerStatus(withHint As Boolean) As Task
        If withHint Then
            Hint($"正在刷新服务器 {_server.Name} 的状态...", HintType.Info)
        End If
        _server.Status = ServerStatus.Pinging
        RunInUi(Sub() UpdateServerUi())
        Dim server = Await PageInstanceServer.PingServer(_server)
        UpdateServerInfo(server)
    End Function
    
    ''' <summary>
    ''' 连接到服务器
    ''' </summary>
    Private Sub BtnConnect_Click(sender As Object, e As EventArgs)
        Dim server As MinecraftServerInfo = sender.Tag
        Try
            ' 使用PCL的启动逻辑，参考实例设置中的自动进入服务器选项
            Dim launchArgs As String = $"--server {server.Address} --port {server.Port}"
            
            ' 这里需要调用PCL的启动游戏功能
            ' 具体实现需要参考现有的启动代码
            Hint($"正在连接到服务器 {server.Name}...", HintType.Info)
            
            ' TODO: 实现实际的游戏启动逻辑
            
        Catch ex As Exception
            Log(ex, "连接服务器失败", LogLevel.Feedback)
            Hint("连接服务器失败：" & ex.Message, HintType.Critical)
        End Try
    End Sub
    
    ''' <summary>
    ''' 复制服务器地址
    ''' </summary>
    Private Sub BtnCopy_Click(sender As Object, e As RoutedEventArgs)
        Try
            Clipboard.SetText(_server.Address)
            Hint($"已复制服务器地址：{_server.Address}", HintType.Finish)
        Catch ex As Exception
            Log(ex, "复制服务器地址失败", LogLevel.Debug)
            Hint("复制服务器地址失败", HintType.Critical)
        End Try
    End Sub
    
    ''' <summary>
    ''' 刷新服务器状态
    ''' </summary>
    Private Async Sub BtnRefresh_Click(sender As Object, e As RoutedEventArgs)
        Await RefreshServerStatus(True)
    End Sub
    
    ''' <summary>
    ''' 编辑服务器信息
    ''' </summary>
    Private Sub BtnEdit_Click(sender As Object, e As RoutedEventArgs)
        Dim server As MinecraftServerInfo = sender.Tag
        Try
            Dim newName As String = MyMsgBoxInput("编辑服务器信息", "请输入新的服务器名称：", server.Name)
            If String.IsNullOrEmpty(newName) Then Return
            
            Dim newAddress As String = MyMsgBoxInput("编辑服务器信息", "请输入新的服务器地址：", 
                server.Address & If(server.Port <> 25565, ":" & server.Port, ""))
            If String.IsNullOrEmpty(newAddress) Then Return
            
            ' 解析地址和端口
            Dim addressParts = newAddress.Split(":"c)
            server.Name = newName
            server.Address = addressParts(0)
            server.Port = If(addressParts.Length > 1, Integer.Parse(addressParts(1)), 25565)
            
            ' 保存到文件
            SaveServersToFile()
            
            ' 刷新UI
            RunInUi(Sub() UpdateServerUi())
            
            Hint("服务器信息已更新", HintType.Finish)
        Catch ex As Exception
            Log(ex, "编辑服务器信息失败", LogLevel.Feedback)
            Hint("编辑服务器信息失败：" & ex.Message, HintType.Critical)
        End Try
    End Sub
    
    ''' <summary>
    ''' 保存服务器信息到文件
    ''' </summary>
    Private Sub SaveServersToFile()
        ' TODO: 实现保存到servers.dat文件的逻辑
        ' 这需要NBT写入功能
    End Sub
End Class