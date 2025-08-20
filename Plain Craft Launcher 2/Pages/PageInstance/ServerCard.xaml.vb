Imports System.Windows.Controls.Primitives
Imports fNbt
Imports PCL.Core.IO
Imports PCL.Core.UI

Public Class ServerCard
    Dim _server As MinecraftServerInfo
    Dim _manager As IconManager
    
    Public Sub New()
        InitializeComponent()
        DataContext = New IconManager()
        
        ' 示例：可在代码中切换图标
        _manager = TryCast(DataContext, IconManager)
        _manager.AddIconFromXaml("signal_1", "<Viewbox xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" Width=""20"" Height=""20""><Canvas UseLayoutRounding=""False"" Width=""1024.0"" Height=""1024.0""><Canvas.Clip><RectangleGeometry Rect=""0.0,0.0,1024.0,1024.0""/></Canvas.Clip><Canvas UseLayoutRounding=""False""><Rectangle RadiusX=""0.0"" RadiusY=""0.0"" Canvas.Left=""234.666667"" Canvas.Top=""610.56"" Width=""80.853333"" Height=""127.04"" Fill=""#ff00ff21""/></Canvas><Canvas UseLayoutRounding=""False""><Rectangle RadiusX=""0.0"" RadiusY=""0.0"" Canvas.Left=""353.066667"" Canvas.Top=""541.226667"" Width=""80.853333"" Height=""196.373333"" Fill=""#ff888888""/><Rectangle RadiusX=""0.0"" RadiusY=""0.0"" Canvas.Left=""471.445333"" Canvas.Top=""460.373333"" Width=""80.896"" Height=""277.226667"" Fill=""#ff888888""/><Rectangle RadiusX=""0.0"" RadiusY=""0.0"" Canvas.Left=""589.866667"" Canvas.Top=""379.52"" Width=""80.853333"" Height=""358.08"" Fill=""#ff888888""/><Rectangle RadiusX=""0.0"" RadiusY=""0.0"" Canvas.Left=""708.266667"" Canvas.Top=""298.666667"" Width=""80.853333"" Height=""438.933333"" Fill=""#ff888888""/></Canvas></Canvas></Viewbox>")
        _manager.AddIconFromXaml("signal_2", "<Viewbox xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" Width=""20"" Height=""20""><Canvas UseLayoutRounding=""False"" Width=""1024.0"" Height=""1024.0""><Canvas.Clip><RectangleGeometry Rect=""0.0,0.0,1024.0,1024.0""/></Canvas.Clip><Canvas UseLayoutRounding=""False""><Rectangle RadiusX=""0.0"" RadiusY=""0.0"" Canvas.Left=""234.666667"" Canvas.Top=""610.56"" Width=""80.853333"" Height=""127.04"" Fill=""#ff00ff21""/><Rectangle RadiusX=""0.0"" RadiusY=""0.0"" Canvas.Left=""353.066667"" Canvas.Top=""541.226667"" Width=""80.853333"" Height=""196.373333"" Fill=""#ff00ff21""/></Canvas><Canvas UseLayoutRounding=""False""><Rectangle RadiusX=""0.0"" RadiusY=""0.0"" Canvas.Left=""471.445333"" Canvas.Top=""460.373333"" Width=""80.896"" Height=""277.226667"" Fill=""#ff888888""/><Rectangle RadiusX=""0.0"" RadiusY=""0.0"" Canvas.Left=""589.866667"" Canvas.Top=""379.52"" Width=""80.853333"" Height=""358.08"" Fill=""#ff888888""/><Rectangle RadiusX=""0.0"" RadiusY=""0.0"" Canvas.Left=""708.266667"" Canvas.Top=""298.666667"" Width=""80.853333"" Height=""438.933333"" Fill=""#ff888888""/></Canvas></Canvas></Viewbox>")
        _manager.AddIconFromXaml("signal_3", "<Viewbox Width=""20"" Height=""20"" xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""><Canvas UseLayoutRounding=""False"" Width=""1024.0"" Height=""1024.0""><Canvas.Clip><RectangleGeometry Rect=""0.0,0.0,1024.0,1024.0""/></Canvas.Clip><Canvas UseLayoutRounding=""False""><Rectangle RadiusX=""0.0"" RadiusY=""0.0"" Canvas.Left=""234.666667"" Canvas.Top=""610.56"" Width=""80.853333"" Height=""127.04"" Fill=""#ff00ff21""/><Rectangle RadiusX=""0.0"" RadiusY=""0.0"" Canvas.Left=""353.066667"" Canvas.Top=""541.226667"" Width=""80.853333"" Height=""196.373333"" Fill=""#ff00ff21""/><Rectangle RadiusX=""0.0"" RadiusY=""0.0"" Canvas.Left=""471.445333"" Canvas.Top=""460.373333"" Width=""80.896"" Height=""277.226667"" Fill=""#ff00ff21""/></Canvas><Canvas UseLayoutRounding=""False""><Rectangle RadiusX=""0.0"" RadiusY=""0.0"" Canvas.Left=""589.866667"" Canvas.Top=""379.52"" Width=""80.853333"" Height=""358.08"" Fill=""#ff888888""/><Rectangle RadiusX=""0.0"" RadiusY=""0.0"" Canvas.Left=""708.266667"" Canvas.Top=""298.666667"" Width=""80.853333"" Height=""438.933333"" Fill=""#ff888888""/></Canvas></Canvas></Viewbox>")
        _manager.AddIconFromXaml("signal_4", "<Viewbox xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" Width=""20"" Height=""20""><Canvas UseLayoutRounding=""False"" Width=""1024.0"" Height=""1024.0""><Canvas.Clip><RectangleGeometry Rect=""0.0,0.0,1024.0,1024.0""/></Canvas.Clip><Canvas UseLayoutRounding=""False""><Rectangle RadiusX=""0.0"" RadiusY=""0.0"" Canvas.Left=""234.666667"" Canvas.Top=""610.56"" Width=""80.853333"" Height=""127.04"" Fill=""#ff00ff21""/><Rectangle RadiusX=""0.0"" RadiusY=""0.0"" Canvas.Left=""353.066667"" Canvas.Top=""541.226667"" Width=""80.853333"" Height=""196.373333"" Fill=""#ff00ff21""/><Rectangle RadiusX=""0.0"" RadiusY=""0.0"" Canvas.Left=""471.445333"" Canvas.Top=""460.373333"" Width=""80.896"" Height=""277.226667"" Fill=""#ff00ff21""/><Rectangle RadiusX=""0.0"" RadiusY=""0.0"" Canvas.Left=""589.866667"" Canvas.Top=""379.52"" Width=""80.853333"" Height=""358.08"" Fill=""#ff00ff21""/></Canvas><Canvas UseLayoutRounding=""False""><Rectangle RadiusX=""0.0"" RadiusY=""0.0"" Canvas.Left=""708.266667"" Canvas.Top=""298.666667"" Width=""80.853333"" Height=""438.933333"" Fill=""#ff888888""/></Canvas></Canvas></Viewbox>")
        _manager.AddIconFromXaml("signal_5", "<Viewbox Width=""20"" Height=""20"" xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""><Canvas UseLayoutRounding=""False"" Width=""1024.0"" Height=""1024.0""><Canvas.Clip><RectangleGeometry Rect=""0.0,0.0,1024.0,1024.0""/></Canvas.Clip><Canvas UseLayoutRounding=""False""><Rectangle RadiusX=""0.0"" RadiusY=""0.0"" Canvas.Left=""234.666667"" Canvas.Top=""610.56"" Width=""80.853333"" Height=""127.04"" Fill=""#ff00ff21""/><Rectangle RadiusX=""0.0"" RadiusY=""0.0"" Canvas.Left=""353.066667"" Canvas.Top=""541.226667"" Width=""80.853333"" Height=""196.373333"" Fill=""#ff00ff21""/><Rectangle RadiusX=""0.0"" RadiusY=""0.0"" Canvas.Left=""471.445333"" Canvas.Top=""460.373333"" Width=""80.896"" Height=""277.226667"" Fill=""#ff00ff21""/><Rectangle RadiusX=""0.0"" RadiusY=""0.0"" Canvas.Left=""589.866667"" Canvas.Top=""379.52"" Width=""80.853333"" Height=""358.08"" Fill=""#ff00ff21""/><Rectangle RadiusX=""0.0"" RadiusY=""0.0"" Canvas.Left=""708.266667"" Canvas.Top=""298.666667"" Width=""80.853333"" Height=""438.933333"" Fill=""#ff00ff21""/></Canvas></Canvas></Viewbox>")
        _manager.AddIconFromXaml("signal_offline", "<Viewbox xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" Width=""14"" Height=""14"" Margin=""3""><Canvas UseLayoutRounding=""False"" Width=""1280.0"" Height=""1024.0""><Canvas.Clip><RectangleGeometry Rect=""0.0,0.0,1280.0,1024.0""/></Canvas.Clip><Path Fill=""#ff000000""><Path.Data><PathGeometry Figures=""M 317.63 349.235 l -67.951 -67.951 l -67.95 67.95 c -18.964 18.964 -48.988 18.964 -67.951 0 c -18.963 -18.962 -18.963 -48.987 0 -67.95 l 67.95 -67.95 l -66.37 -67.951 c -18.963 -18.963 -18.963 -48.988 0 -67.95 c 18.963 -18.964 48.988 -18.964 67.95 0 l 67.951 67.95 l 67.95 -67.95 c 18.964 -18.964 48.989 -18.964 67.951 0 c 18.963 18.962 18.963 48.987 0 67.95 l -67.95 67.95 l 67.95 67.951 c 18.963 18.963 18.963 48.988 0 67.95 c -9.481 9.482 -20.543 14.223 -33.185 14.223 c -14.222 0 -26.864 -6.321 -36.345 -14.222 z M 216.494 752.198 h -48.988 c -26.864 0 -48.987 26.864 -48.987 60.049 v 120.099 c 0 33.185 22.123 60.05 48.987 60.05 h 48.988 c 26.864 0 48.987 -26.865 48.987 -60.05 v -120.1 c 0 -33.184 -22.123 -60.048 -48.987 -60.048 z M 516.74 512 h -48.988 c -26.864 0 -48.988 26.864 -48.988 60.05 v 360.296 c 0 33.185 22.124 60.05 48.988 60.05 h 48.988 c 26.864 0 48.987 -26.865 48.987 -60.05 V 572.049 c 0 -33.185 -22.123 -60.049 -48.987 -60.049 z m 300.247 -240.198 H 768 c -26.864 0 -48.988 26.865 -48.988 60.05 v 600.494 c 0 33.185 22.124 60.05 48.988 60.05 h 48.988 c 26.864 0 48.987 -26.865 48.987 -60.05 V 331.852 c 0 -33.185 -22.123 -60.05 -48.987 -60.05 z m 300.247 -240.197 h -48.988 c -26.864 0 -48.988 26.864 -48.988 60.05 v 840.69 c 0 33.186 22.124 60.05 48.988 60.05 h 48.988 c 26.864 0 48.987 -26.864 48.987 -60.05 V 91.656 c -1.58 -33.186 -22.123 -60.05 -48.987 -60.05 z"" FillRule=""Nonzero""/></Path.Data></Path></Canvas></Viewbox>")
        _manager.AddIconFromXaml("loading", "<Viewbox xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" Width=""20"" Height=""20""><Canvas UseLayoutRounding=""False"" Width=""1024.0"" Height=""1024.0""><Canvas.Clip><RectangleGeometry Rect=""0.0,0.0,1024.0,1024.0""/></Canvas.Clip><Path Fill=""#ff000000""><Path.Data><PathGeometry Figures=""M 256 490.667 a 64 64 0 1 1 -128 0 a 64 64 0 0 1 128 0 z m -42.6667 0 a 21.3333 21.3333 0 1 0 -42.6667 0 a 21.3333 21.3333 0 0 0 42.6667 0 z m 384 0 a 106.667 106.667 0 1 1 -213.376 -0.042667 A 106.667 106.667 0 0 1 597.333 490.667 z m -42.6667 0 a 64 64 0 1 0 -128.043 0.042666 A 64 64 0 0 0 554.667 490.667 z m 298.667 0 a 64 64 0 1 1 -128 0 a 64 64 0 0 1 128 0 z m -42.6667 0 a 21.3333 21.3333 0 1 0 -42.6667 0 a 21.3333 21.3333 0 0 0 42.6667 0 z"" FillRule=""Nonzero""/></Path.Data></Path></Canvas></Viewbox>")
    End Sub
    
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
        If Not String.IsNullOrEmpty(_server.Icon) Then
            Await SetServerLogoAsync(_server.Icon)
        Else
            SetDefaultLogo()
        End If
        If _server.Status = ServerStatus.Online
            _manager.SetSelectedIconByName(GetSignalIcon(_server.Ping))
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
            _manager.SetSelectedIconByName("loading")
            ServerPlayer.Text = "正在连接"
            ServerMotD.Text = "正在连接..."
        Else If _server.Status = ServerStatus.Offline
            _manager.SetSelectedIconByName("signal_offline")
            ServerPlayer.Text = "离线"
            ServerMotD.Text = "服务器离线"
        End If
    End Sub
    
    Private Function GetSignalIcon(ping As Integer) As String
        Select Case ping
            Case 0 To 99
                Return "signal_5" ' 5 条信号
            Case 100 To 299
                Return "signal_4" ' 4 条信号
            Case 300 To 599
                Return "signal_3" ' 3 条信号
            Case 600 To 999
                Return "signal_2" ' 2 条信号
            Case Else
                Return "signal_1" ' 1 条信号
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
        Try
            Dim launchOptions As New McLaunchOptions With {.ServerIp = _server.Address}
            McLaunchStart(LaunchOptions)
            FrmMain.PageChange(New FormMain.PageStackData With {.Page = FormMain.PageType.Launch})
            Hint($"正在连接到服务器 {_server.Name}...", HintType.Info)
        Catch ex As Exception
            Log(ex, "启动服务器失败", LogLevel.Feedback)
            Hint("启动服务器失败：" & ex.Message, HintType.Critical)
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
        Try
            Dim newName As String = MyMsgBoxInput("编辑服务器信息", "请输入新的服务器名称：", _server.Name)
            If String.IsNullOrEmpty(newName) Then Return
            
            Dim newAddress As String = MyMsgBoxInput("编辑服务器信息", "请输入新的服务器地址：", 
                _server.Address)
            If String.IsNullOrEmpty(newAddress) Then Return
            
            Dim nbtData = NbtFileHandler.ReadNbTFile(PageInstanceLeft.Instance.PathIndie + "servers.dat", "servers")
            If nbtData IsNot Nothing Then
                Dim index = PageInstanceServer.GetServerIndex(Me)
                Dim server = TryCast(nbtData(index), NbtCompound)
                If server.Get(Of NbtString)("name").Value = _server.Name AndAlso
                   server.Get(Of NbtString)("ip").Value = _server.Address Then
                    server("name") = New NbtString("name", newName)
                    server("ip") = New NbtString("ip", newAddress)
                    Dim clonedNbtData As NbtList = CType(nbtData.Clone(), NbtList)
                    NbtFileHandler.WriteNbtFile(clonedNbtData, PageInstanceLeft.Instance.PathIndie + "servers.dat")
                    ' 更改地址和端口
                    _server.Name = newName
                    _server.Address = newAddress
            
                    ' 刷新UI
                    RunInUi(Sub() UpdateServerUi())
            
                    Hint("服务器信息已更新", HintType.Finish)
                End If
            End If
        Catch ex As Exception
            Log(ex, "编辑服务器信息失败", LogLevel.Feedback)
            Hint("编辑服务器信息失败：" & ex.Message, HintType.Critical)
        End Try
    End Sub
End Class