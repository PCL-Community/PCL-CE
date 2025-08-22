Imports System.Threading.Tasks
Imports PCL.Core.Link
Imports PCL.Core.Minecraft
Imports PCL.Core.UI

Class MinecraftServer
    Inherits Grid
    
    Private Const FallbackImageUri As String = "pack://application:,,,/Plain Craft Launcher 2;component/Images/Icons/DefaultServer.png"

    Public Property Address As String
        Get
            Return GetValue(AddressProperty)
        End Get
        Set(value As String)
            SetValue(AddressProperty, value)
        End Set
    End Property
    Private Shared ReadOnly AddressProperty As DependencyProperty =
        DependencyProperty.Register(
            NameOf(Address),
            GetType(String),
            GetType(MinecraftServer),
            New PropertyMetadata(String.Empty, AddressOf OnAddressChanged)
        )

    Private Shared Sub OnAddressChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        Dim server As MinecraftServer = d
        d.Dispatcher.BeginInvoke(Function() server.UpdateServerInfoAsync(e.NewValue?.ToString()))
    End Sub

    Public Async Function UpdateServerInfoAsync(address As String) As Task
        If address Is Nothing Then Return
        ' 预先重置UI状态
        LabServerDesc.Foreground = Brushes.White
        LabServerDesc.Text = "查询中..."
        LabServerPlayer.Text = "-/-"
        LabServerPlayer.ToolTip = Nothing
        ImageLoaderHelper.SetFallbackImage(imgServerLogo, FallbackImageUri)

        Try
            ' 获取可达地址（DNS解析）
            Dim addr = Await ServerAddressResolver.GetReachableAddressAsync(address)

            ' Ping服务器
            Using query = New McPing(addr.Ip, addr.Port)
                Dim ret = Await query.PingAsync()

                If ret Is Nothing Then
                    Throw New Exception("未返回服务器信息")
                End If

                ' 处理服务器图标
                Await ImageLoaderHelper.SetServerLogoAsync(ret.Favicon, ImgServerLogo)

                ' 更新UI
                UpdateServerStatus(ret)
            End Using
        Catch ex As Exception
            Log(ex, "[MinecraftServer] 信息查询失败")
            LabServerDesc.Text = $"无法连接: {ex.Message}"
            LabServerDesc.Foreground = Brushes.Red
            ImageLoaderHelper.SetFallbackImage(ImgServerLogo, FallbackImageUri)
        End Try
    End Function

    Private Sub UpdateServerStatus(ret As McPingResult)
        ' 延迟颜色判断
        Dim latencyColor = If(ret.Latency < 150, "a", If(ret.Latency < 400, "6", "c"))

        ' 更新描述
        MinecraftFormatter.SetColorfulTextLab(
            $"Minecraft 服务器{vbCrLf}{ret.Description}",
            LabServerDesc
        )

        ' 更新玩家信息
        Dim playerText = $"{ret.Players.Online}/{ret.Players.Max}{vbCrLf}§{latencyColor}{ret.Latency}ms"
        MinecraftFormatter.SetColorfulTextLab(playerText, LabServerPlayer)

        ' 玩家列表提示
        If ret.Players.Samples?.Any() Then
            LabServerPlayer.ToolTip = String.Join(vbCrLf, ret.Players.Samples.Select(Function(x) x.Name))
            ToolTipService.SetPlacement(LabServerPlayer, Primitives.PlacementMode.Mouse)
        End If
    End Sub
End Class