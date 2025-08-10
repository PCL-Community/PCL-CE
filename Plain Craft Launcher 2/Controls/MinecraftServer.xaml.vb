Imports System.Net
Imports System.Net.Sockets
Imports System.Threading.Tasks
Imports PCL.Core.Link

Class MinecraftServer
    Inherits Grid

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
        SetDefaultLogo()

        Try
            ' 获取可达地址（DNS解析）
            Dim addr = Await GetReachableAddressAsync(address)

            ' Ping服务器
            Using query = New McPing(addr.Ip, addr.Port)
                Dim ret = Await query.PingAsync()

                If ret Is Nothing Then
                    Throw New Exception("未返回服务器信息")
                End If

                ' 处理服务器图标
                If Not String.IsNullOrEmpty(ret.Favicon) Then
                    Await SetServerLogoAsync(ret.Favicon)
                Else
                    SetDefaultLogo()
                End If

                ' 更新UI
                UpdateServerStatus(ret)
            End Using
        Catch ex As Exception
            Log(ex, "[MinecraftServer] 信息查询失败")
            LabServerDesc.Text = $"无法连接: {ex.Message}"
            LabServerDesc.Foreground = Brushes.Red
            SetDefaultLogo()
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
            ImgServerLogo.Source = image
        Catch ex As Exception
            Log(ex, "图标解析失败，使用默认图标")
            SetDefaultLogo()
        End Try
    End Function

    Private Sub SetDefaultLogo()
        ImgServerLogo.Source = New BitmapImage(
            New Uri("pack://application:,,,/Plain Craft Launcher 2;component/Images/Icons/DefaultServer.png")
        )
    End Sub

    Private Shared Async Function GetReachableAddressAsync(address As String) As Task(Of (Ip As String, Port As Integer))
        ' 输入验证
        If String.IsNullOrWhiteSpace(address) Then
            Throw New ArgumentException("服务器地址不能为空")
        End If

        ' 移除协议头（如果存在）
        address = address.Replace("http://", "").Replace("https://", "")

        ' 情况1: 纯IP:端口
        If address.Contains(":") Then
            Dim parts = address.Split(":"c)
            If parts.Length <> 2 OrElse Not Integer.TryParse(parts(1), Nothing) Then
                Throw New FormatException("无效的端口格式")
            End If
            Return (parts(0), Integer.Parse(parts(1)))
        End If

        ' 情况2: 纯IP (IPv4/IPv6)
        If IPAddress.TryParse(address, Nothing) Then
            Return (address, 25565)
        End If

        ' 情况3: 域名 (尝试SRV查询)
        Try
            Log($"尝试SRV查询: _minecraft._tcp.{address}")
            Dim srvRecords = Await ResolveSrvRecordsAsync(address)

            If srvRecords.Any() Then
                Dim ret = ParseSrvRecord(srvRecords(0))
                Return ret
            End If
        Catch ex As SocketException
            Log(ex, "SRV查询失败 (网络错误)")
        Catch ex As Exception
            Log(ex, "SRV查询异常")
        End Try

        ' 默认: 直接使用域名+默认端口
        Return (address, 25565)
    End Function

    Private Shared Async Function ResolveSrvRecordsAsync(domain As String) As Task(Of IEnumerable(Of String))
        Return Await Task.Run(Function()
                                  Try
                                      Return nDnsQuery.GetSRVRecords($"_minecraft._tcp.{domain}")
                                  Catch
                                      Return Enumerable.Empty(Of String)()
                                  End Try
                              End Function)
    End Function

    Private Shared Function ParseSrvRecord(record As String) As (Host As String, Port As Integer)
        ' 标准SRV格式: 优先级 权重 端口 主机
        ' 但nDnsQuery返回格式可能不同，按原逻辑处理
        Dim parts = record.Split({" "c}, StringSplitOptions.RemoveEmptyEntries)
        If parts.Length >= 3 Then
            Return (parts(3), Integer.Parse(parts(2)))
        End If

        ' 回退到原处理逻辑
        parts = record.Split(":"c)
        If parts.Length = 2 Then
            Return (parts(0), Integer.Parse(parts(1)))
        ElseIf parts.Length = 1 Then
            Return (parts(0), 25565)
        Else
            Throw New FormatException("无效的SRV记录格式")
        End If
    End Function
End Class