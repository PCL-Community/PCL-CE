Imports System.IO
Imports System.Threading.Tasks
Imports fNbt
Imports PCL.Core.Link

Public Class PageInstanceServer
    Inherits MyPageRight

    Private _serverList As New List(Of MinecraftServerInfo)
    Private _serverCardList As New Dictionary(Of String, ServerCard)

    Private Sub PageLoaded(e As Object, sender As RoutedEventArgs) Handles Me.Loaded
        _serverList.Clear()
        _serverCardList.Clear()
        PanServers.Children.Clear()
        
        LoadServersFromFile()
        For Each server In _serverList
            Dim serverCard = New ServerCard()
            serverCard.UpdateServerInfo(server)
            _serverCardList(server.Address) = serverCard
            PanServers.Children.Add(serverCard)
            Task.Run(Async Function() 
                Await serverCard.RefreshServerStatus(False)
            End Function)
        Next
    End Sub

    Public Sub RefreshServers()
        RunInNewThread(Async Sub()
            Try
                ' 读取服务器信息
                LoadServersFromFile()

                ' 在UI线程中更新界面
                RunInUi(Sub() UpdateServerUi())

                ' 异步ping所有服务器
                Await PingAllServers()
            Catch ex As Exception
                Log(ex, "刷新服务器列表失败", LogLevel.Feedback)
                RunInUi(Sub() Hint("刷新服务器列表失败：" & ex.Message, HintType.Critical))
            End Try
        End Sub, "RefreshServers")
    End Sub

    ''' <summary>
    ''' 从servers.dat文件读取服务器信息
    ''' </summary>
    Private Sub LoadServersFromFile()
        _serverList.Clear()

        Dim serversFile As String = PageInstanceLeft.Instance.PathIndie + "servers.dat"
        If Not File.Exists(serversFile) Then Return

        Try
            ' 读取NBT格式的servers.dat文件
            Dim nbtData = ReadNBTFile(serversFile)
            If nbtData IsNot Nothing Then
                ParseServersFromNBT(nbtData)
            End If
        Catch ex As Exception
            Log(ex, "读取servers.dat文件失败", LogLevel.Debug)
        End Try
    End Sub

    ''' <summary>
    ''' 解析NBT格式的服务器数据
    ''' </summary>
    Private Sub ParseServersFromNBT(serversList As Object)
        If serversList IsNot Nothing Then
            Log($"Found {serversList.Count} servers:")

            ' 遍历 servers 列表中的每个服务器
            For i As Integer = 0 To serversList.Count - 1
                Dim server As NbtCompound = TryCast(serversList(i), NbtCompound)
                If server IsNot Nothing Then
                    ' 提取服务器信息
                    Dim hidden As Byte = If(server.Get(Of NbtByte)("hidden")?.Value, 0)
                    Dim ip As String = If(server.Get(Of NbtString)("ip")?.Value, "Unknown")
                    Dim name As String = If(server.Get(Of NbtString)("name")?.Value, "Unknown")
                    Dim iconBase64 As String = server.Get(Of NbtString)("icon")?.Value
                    
                    Log(vbCrLf & $"Server {i + 1}:")
                    Log($"  Name: {name}")
                    Log($"  IP: {ip}")
                    Log($"  Hidden: {If(hidden = 1, "Yes", "No")}")
                    _serverList.Add(New MinecraftServerInfo With {
                                       .Name = name,
                                       .Address = ip,
                                       .Status = ServerStatus.Unknown,
                                       .Icon = iconBase64
                                       })
                End If
            Next
        Else
            Log("No 'servers' list found in servers.dat.")
        End If
    End Sub

    ''' <summary>
    ''' 更新服务器UI显示
    ''' </summary>
    Private Sub UpdateServerUi()
        PanServers.Children.Clear()

        'If _serverList.Count = 0 Then
         '   HintNoServers.Visibility = Visibility.Visible
          '  Return
        'End If

        
        'HintNoServers.Visibility = Visibility.Collapsed
        
        For Each server In _serverList
            Dim serverCard = New ServerCard()
            serverCard.UpdateServerInfo(server)
            _serverCardList(server.Address) = serverCard
            PanServers.Children.Add(serverCard)
        Next
    End Sub

    ''' <summary>
    ''' 异步ping所有服务器
    ''' </summary>
    Private Async Function PingAllServers() As Task
        For Each server In _serverCardList.Values
            Await server.RefreshServerStatus(False)
        Next
    End Function

    ''' <summary>
    ''' ping单个服务器
    ''' </summary>
    Public Async Shared Function PingServer(server As MinecraftServerInfo) As Task(of MinecraftServerInfo)
        Dim addr = Await MinecraftServer.GetReachableAddressAsync(server.Address)
        
        Try
            ' Ping服务器
            Using query = New McPing(addr.Ip, addr.Port)
                Dim result As McPingResult
                result = Await query.PingAsync()
                If result <> Nothing
                    server.Status = ServerStatus.Online
                    server.PlayerCount = result.Players.Online
                    server.MaxPlayers = result.Players.Max
                    server.Description = result.Description
                    server.Version = result.Version.Name
                    server.Ping = result.Latency
                Else
                    server.Status = ServerStatus.Offline
                End If
            End Using
        Catch ex As Exception
            server.Status = ServerStatus.Offline
            Log(ex, $"Ping服务器失败: {server.Address}:{server.Port}", LogLevel.Debug)
        End Try
        Return server
    End Function
    
    ''' <summary>
    ''' 简化的NBT文件读取
    ''' </summary>
    Private Function ReadNBTFile(filePath As String) As Object
        ' TODO: 实现实际的NBT读取逻辑
        Dim saveDatPath = IO.Path.Combine(PageInstanceLeft.Instance.PathIndie, "servers.dat")
        Using fs As New FileStream(saveDatPath, FileMode.Open, FileAccess.Read, FileShare.Read)
            Dim saveInfo as New NbtFile()
            saveInfo.LoadFromStream(fs, NbtCompression.AutoDetect)
            ' 获取根节点的 "servers" 列表（TAG_List）
            Dim serversList As NbtList = saveInfo.RootTag.Get(Of NbtList)("servers")

            return serversList
        End Using
        Return Nothing
    End Function

End Class

''' <summary>
''' Minecraft服务器信息类
''' </summary>
Public Class MinecraftServerInfo
    Public Property Name As String
    Public Property Address As String  
    Public Property Port As Integer = 25565
    Public Property Status As ServerStatus = ServerStatus.Unknown
    Public Property PlayerCount As Integer = 0
    Public Property MaxPlayers As Integer = 0
    Public Property Description As String = ""
    Public Property Version As String = ""
    Public Property Ping As Integer = 0
    Public Property Icon As String = ""
End Class

''' <summary>
''' 服务器状态枚举
''' </summary>
Public Enum ServerStatus
    Unknown
    Online 
    Offline
    Pinging
End Enum
