Imports System.Collections.ObjectModel
Imports System.IO
Imports System.Threading.Tasks
Imports fNbt
Imports PCL.Core.Link
Imports PCL.Core.Minecraft

Public Class PageInstanceServer
    Inherits MyPageRight

    Public ReadOnly Shared ServerList As New List(Of MinecraftServerInfo)
    Private ReadOnly Shared ServerCardList As New List(Of ServerCard)
    
    Private _lastRefresh As DateTime = DateTime.MinValue
    Private Const DebounceInterval As Integer = 2000

    Private Async Sub PageLoaded(e As Object, sender As RoutedEventArgs) Handles Me.Loaded
        ServerList.Clear()
        ServerCardList.Clear()
        PanServers.Children.Clear()

        Await LoadServersFromFile()
        RefreshTip()

        PanServers.BeginInit() ' 暂停布局
        For Each server In ServerList
            Dim serverCard = New ServerCard()
            AddHandler serverCard.RemoveServer, AddressOf RemoveServerEvent
            AddHandler serverCard.EditServer, AddressOf EditServer
            serverCard.UpdateServerInfo(server)
            ServerCardList.Add(serverCard)
            PanServers.Children.Add(serverCard)
        Next
        
        PanServers.EndInit() ' 恢复布局
        
        PingAllServers()
    End Sub
    
    Private Sub PageInstanceServer_IsVisibleChanged(sender As Object, e As DependencyPropertyChangedEventArgs) Handles Me.IsVisibleChanged
        If Not IsVisible Then
            If _cts IsNot Nothing Then
                _cts.Cancel()
                _cts.Dispose() ' 清理旧的 CancellationTokenSource
                _cts = Nothing
            End If
        End If
    End Sub
    
    Private Async Sub RemoveServerEvent(sender As Object, e As EventArgs)
        ' Get server index
        Dim index As Integer = PanServers.Children.IndexOf(sender)
        If index < 0 Then
            Hint("无法找到服务器在列表中的索引", HintType.Critical)
            Exit Sub
        End If

        ' Read NBT file
        Dim nbtData As NbtList = Await NbtFileHandler.ReadTagInNbtFileAsync(Of NbtList)(IO.Path.Combine(PageInstanceLeft.Instance.PathIndie, "servers.dat"), "servers")
        If nbtData Is Nothing Then
            Hint("无法读取服务器数据文件", HintType.Critical)
            Exit Sub
        End If
        
        ' Remove server from NBT data
        nbtData.RemoveAt(index)
        Dim clonedNbtData = CType(nbtData.Clone(), NbtList)
    
        ' Write back to NBT file
        If Not await NbtFileHandler.WriteTagInNbtFileAsync(clonedNbtData, PageInstanceLeft.Instance.PathIndie + "servers.dat") Then
            Hint("无法写入服务器数据文件", HintType.Critical)
            Exit Sub
        End If

        ' Remove server from list and UI
        ServerList.RemoveAt(index)
        ServerCardList.Remove(sender)
        If ServerList.Count = 0 Then
            RefreshTip()
        End If

        ' Remove UI element
        PanServers.Children.Remove(sender)

        ' Success message
        Hint("服务器已移除", HintType.Finish)
    End Sub
    
    Private Async Sub EditServer(sender As Object, e As ServerCard.ResultEventArgs)
        ' Read NBT file
        Dim nbtData As NbtList = await NbtFileHandler.ReadTagInNbtFileAsync(Of NbtList)(PageInstanceLeft.Instance.PathIndie + "servers.dat", "servers")
        If nbtData Is Nothing Then
            Hint("无法读取服务器数据文件", HintType.Critical)
            Exit Sub
        End If

        ' Get server index
        Dim index As Integer = PanServers.Children.IndexOf(sender)
        If index < 0 OrElse index >= nbtData.Count Then
            Hint("无法找到服务器在列表中的索引", HintType.Critical)
            Exit Sub
        End If

        ' Verify server data
        Dim server = TryCast(nbtData(index), NbtCompound)

        ' Update server data
        server("name") = New NbtString("name", e.Param1)
        server("ip") = New NbtString("ip", e.Param2)

        ' Write updated NBT data
        Dim clonedNbtData = CType(nbtData.Clone(), NbtList)
        If Not Await NbtFileHandler.WriteTagInNbtFileAsync(clonedNbtData, PageInstanceLeft.Instance.PathIndie + "servers.dat") Then
            Hint("无法写入服务器数据文件", HintType.Critical)
            Exit Sub
        End If
        
        Dim serverCard = TryCast(sender, ServerCard)
        
        serverCard.Server.Name = e.Param1
        serverCard.Server.Address = e.Param2
        
        Await serverCard.RefreshServerStatus(True)

        ' Success message
        Hint("服务器信息已更新", HintType.Finish)
    End Sub

    ''' <summary>
    ''' 刷新服务器列表
    ''' </summary>
    Public Async Sub RefreshServers()
        Log("刷新服务器列表")
        Try
            ' 读取服务器信息
            await LoadServersFromFile()

            ' 在UI线程中更新界面
            RunInUi(Sub() UpdateServerUi())

            ' 异步ping所有服务器
            PingAllServers()
        Catch ex As Exception
            Log(ex, "刷新服务器列表失败", LogLevel.Feedback)
            RunInUi(Sub() Hint("刷新服务器列表失败：" & ex.Message, HintType.Critical))
        End Try
    End Sub

    Private Sub BtnRefresh_Click(sender As Object, e As MouseButtonEventArgs)
        If (DateTime.Now - _lastRefresh).TotalMilliseconds < DebounceInterval Then
            Hint("请勿频繁刷新！", HintType.Info)
            Return
        End If
        _lastRefresh = DateTime.Now
        Hint("正在刷新服务器列表，请稍候...", HintType.Info)
        Try
            RefreshServers()
        Catch ex As Exception
            Log(ex, "刷新服务器列表失败", LogLevel.Feedback)
            Hint("刷新服务器列表失败：" & ex.Message, HintType.Critical)
        End Try
    End Sub

    Private Async Sub BtnAddServer_Click(sender As Object, e As MouseButtonEventArgs)
        Dim result = GetServerInfo(New MinecraftServerInfo() With {.Name = "Minecraft服务器", .Address = ""})
        If result.Success Then
            Dim newServer As New MinecraftServerInfo With {
                .Name = result.Name,
                .Address = result.Address,
                .Status = ServerStatus.Unknown
            }
            ServerList.Add(newServer)

            RefreshTip()

            Dim serverCard = New ServerCard()
            AddHandler serverCard.RemoveServer, AddressOf RemoveServerEvent
            AddHandler serverCard.EditServer, AddressOf EditServer
            serverCard.UpdateServerInfo(newServer)
            ServerCardList.Add(serverCard)
            PanServers.Children.Add(serverCard)

            Task.Run(Async Function()
                Await serverCard.RefreshServerStatus(False)
            End Function)
            
            Dim serversDatPath = Path.Combine(PageInstanceLeft.Instance.PathIndie, "servers.dat")
            
            Dim nbtData
            if Not File.Exists(serversDatPath) Then
                nbtData = New NbtList("servers", NbtTagType.Compound)
                RefreshTip()
            Else 
                nbtData = Await NbtFileHandler.ReadTagInNbtFileAsync(Of NbtList)(serversDatPath, "servers")
            End If
            If nbtData IsNot Nothing Then
                Dim server = New NbtCompound()
                server("name") = New NbtString("name", result.Name)
                server("ip") = New NbtString("ip", result.Address)
                nbtData.Add(server)
                Dim clonedNbtData = CType(nbtData.Clone(), NbtList)
                Await NbtFileHandler.WriteTagInNbtFileAsync(clonedNbtData, serversDatPath)
            End If
        End If
    End Sub

    Public Shared Function GetServerInfo(server As MinecraftServerInfo) As (Name As String, Address As String, Success As Boolean)
        Dim newName As String = MyMsgBoxInput("编辑服务器信息", "请输入新的服务器名称：", server.Name, 
                                              New Collection(Of Validate) From {New ValidateNullOrWhiteSpace()})
        
        If String.IsNullOrEmpty(newName) Then 
            Return (String.Empty, String.Empty, False)
        End If

        Dim newAddress As String = MyMsgBoxInput("编辑服务器信息", "请输入新的服务器地址：", server.Address,
                                                 New Collection(Of Validate) From {New ValidateNullOrWhiteSpace()})
        If String.IsNullOrEmpty(newAddress) Then 
            Return (String.Empty, String.Empty, False)
        End If
        Return (newName, newAddress, True)
    End Function

    ''' <summary>
    ''' 从servers.dat文件读取服务器信息
    ''' </summary>
    Private Async Function LoadServersFromFile() As Task
        ServerList.Clear()

        Dim serversFile As String = PageInstanceLeft.Instance.PathIndie + "servers.dat"
        If Not File.Exists(serversFile) Then Return

        Try
            ' 读取NBT格式的servers.dat文件
            Dim nbtData = await NbtFileHandler.ReadTagInNbtFileAsync(Of NbtList)(serversFile, "servers")
            ParseServersFromNBT(nbtData)
        Catch ex As Exception
            Log(ex, "读取servers.dat文件失败", LogLevel.Debug)
        End Try
    End Function

    ''' <summary>
    ''' 解析NBT格式的服务器数据
    ''' </summary>
    Private Sub ParseServersFromNBT(serversList As NbtList)
        If serversList IsNot Nothing Then
            Log($"Found {serversList.Count} servers:")

            ' 遍历 servers 列表中的每个服务器
            For i = 0 To serversList.Count - 1
                Dim server = TryCast(serversList(i), NbtCompound)
                If server IsNot Nothing Then
                    ' 提取服务器信息
                    ' Dim hidden As Byte = If(server.Get(Of NbtByte)("hidden")?.Value, 0)
                    Dim ip As String = If(server.Get(Of NbtString)("ip")?.Value, "Unknown")
                    Dim name As String = If(server.Get(Of NbtString)("name")?.Value, "Unknown")
                    Dim iconBase64 As String = server.Get(Of NbtString)("icon")?.Value

                    Log($"服务器 {i + 1}:")
                    Log($"  名字: {name}")
                    Log($"  IP: {ip}")
                    ' Log($"  Hidden: {If(hidden = 1, "Yes", "No")}")
                    ServerList.Add(New MinecraftServerInfo With {
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

        RefreshTip()

        For Each server In ServerList
            Dim serverCard = New ServerCard()
            AddHandler serverCard.RemoveServer, AddressOf RemoveServerEvent
            AddHandler serverCard.EditServer, AddressOf EditServer
            serverCard.UpdateServerInfo(server)
            ServerCardList.Add(serverCard)
            PanServers.Children.Add(serverCard)
        Next
    End Sub
    
    Private Sub RefreshTip()
        If ServerList.Count = 0 Then
            Log("没有找到任何服务器")
            PanNoServer.Visibility = Visibility.Visible
            PanContent.Visibility = Visibility.Collapsed
            PanServers.Visibility = Visibility.Collapsed
            Return
        End If
        Log("找到服务器列表")
        PanNoServer.Visibility = Visibility.Collapsed
        PanContent.Visibility = Visibility.Visible
        PanServers.Visibility = Visibility.Visible
    End Sub
    
    Private _cts As CancellationTokenSource = Nothing
    
    Private Async Sub PingAllServers()
        If _cts IsNot Nothing Then
            _cts.Cancel()
            _cts.Dispose()
        End If

        _cts = New CancellationTokenSource()
        Dim token As CancellationToken = _cts.Token
        Dim semaphore As New SemaphoreSlim(5) ' 限制最多 5 个并发任务

        Dim tasks As New List(Of Task)
        Try
            Dim snapshot = ServerCardList.ToList()
            For Each server In snapshot
                Dim currentServer = server
                Await semaphore.WaitAsync(token)
                tasks.Add(Task.Run(Async Function()
                    Try
                        Await currentServer.RefreshServerStatus(False, token)
                    Catch ex As Exception
                        Log(ex, $"Ping 服务器失败: {currentServer}", LogLevel.Debug)
                    Finally
                        semaphore.Release()
                    End Try
                End Function, token))
            Next

            Await Task.WhenAll(tasks) ' 等待所有任务完成
        Catch ex As OperationCanceledException
            Log("PingAllServers 被取消", LogLevel.Debug)
        Catch ex As Exception
            Log(ex, "PingAllServers 失败", LogLevel.Debug)
        End Try
    End Sub

    ''' <summary>
    ''' ping单个服务器
    ''' </summary>
    Public Async Shared Function PingServer(server As MinecraftServerInfo, token As CancellationToken) As Task(Of MinecraftServerInfo)
        Try
            Dim addr = Await ServerAddressResolver.GetReachableAddressAsync(server.Address, token)
            Using query = New McPing(addr.Ip, addr.Port)
                Dim result As McPingResult
                Log("Pinging server: " & server.Address & ":" & addr.Port)
                result = Await query.PingAsync(token) ' 传递 token
                Log("Ping result: " & If(result IsNot Nothing, "Success", "Failed"))
                If result IsNot Nothing Then
                    server.Status = ServerStatus.Online
                    server.PlayerCount = result.Players.Online
                    server.MaxPlayers = result.Players.Max
                    server.Description = result.Description
                    server.Version = result.Version.Name
                    server.Ping = result.Latency
                    server.Icon = result.Favicon
                Else
                    server.Status = ServerStatus.Offline
                End If
            End Using
        Catch ex As OperationCanceledException
            server.Status = ServerStatus.Offline
            Log("Ping 服务器被取消: " & server.Address, LogLevel.Debug)
        Catch ex As Exception
            server.Status = ServerStatus.Offline
            Log(ex, $"Ping 服务器失败: {server.Address}:{server.Port}", LogLevel.Debug)
        End Try
        Return server
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


