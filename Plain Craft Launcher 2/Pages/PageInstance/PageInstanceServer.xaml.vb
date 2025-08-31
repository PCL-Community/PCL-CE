Imports System.IO
Imports System.Threading.Tasks
Imports fNbt
Imports PCL.Core.Link
Imports PCL.Core.Minecraft

Public Class PageInstanceServer
    Inherits MyPageRight

    Public ReadOnly Shared ServerList As New List(Of MinecraftServerInfo)
    Private ReadOnly Shared ServerCardList As New List(Of ServerCard)

    Private Async Sub PageLoaded(e As Object, sender As RoutedEventArgs) Handles Me.Loaded
        ServerList.Clear()
        ServerCardList.Clear()
        PanServers.Children.Clear()
        
        await LoadServersFromFile()
        
        RefreshTip()
        For Each server In ServerList
            Dim serverCard = New ServerCard()
            AddHandler serverCard.ChildCountZero, AddressOf MyChild_ChildCountZero
            serverCard.UpdateServerInfo(server)
            ServerCardList.Add(serverCard)
            PanServers.Children.Add(serverCard)
            Task.Run(Async Function() 
                Await serverCard.RefreshServerStatus(False)
            End Function)
        Next
    End Sub
    
    Private Sub MyChild_ChildCountZero(sender As Object, e As EventArgs)
        RefreshTip()
    End Sub
    
    Public Shared Function GetServerIndex(serverCard) As Integer
        ' 查找服务器在列表中的索引
        Return ServerCardList.IndexOf(serverCard)
    End Function

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
        Hint("正在刷新服务器列表，请稍候...", HintType.Info)
        RefreshServers()
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
            AddHandler serverCard.ChildCountZero, AddressOf MyChild_ChildCountZero
            serverCard.UpdateServerInfo(newServer)
            ServerCardList.Add(serverCard)
            PanServers.Children.Add(serverCard)
            
            Await serverCard.RefreshServerStatus(False)
            
            Dim nbtData = await NbtFileHandler.ReadNbTFileAsync(PageInstanceLeft.Instance.PathIndie + "servers.dat", "servers")
            If nbtData IsNot Nothing Then
                Dim server = new NbtCompound()
                server("name") = New NbtString("name", result.Name)
                server("ip") = New NbtString("ip", result.Address)
                nbtData.Add(server)
                Dim clonedNbtData = CType(nbtData.Clone(), NbtList)
                await NbtFileHandler.WriteNbtFileAsync(clonedNbtData, PageInstanceLeft.Instance.PathIndie + "servers.dat")
            End If
        End If
    End Sub
    
    Public Shared Function GetServerInfo(server As MinecraftServerInfo) As (Name As String, Address As String, Success As Boolean)
        Dim newName As String = MyMsgBoxInput("编辑服务器信息", "请输入新的服务器名称：", server.Name)
        If String.IsNullOrEmpty(newName) Then 
            Hint("服务器名称不能为空", HintType.Info)
            Return (String.Empty, String.Empty, False)
        End If

        Dim newAddress As String = MyMsgBoxInput("编辑服务器信息", "请输入新的服务器地址：", server.Address)
        If String.IsNullOrEmpty(newAddress) Then 
            Hint("服务器地址不能为空", HintType.Info)
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
            Dim nbtData = await NbtFileHandler.ReadNbtFileAsync(serversFile, "servers")
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
                    
                    Log(vbCrLf & $"服务器 {i + 1}:")
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
            AddHandler serverCard.ChildCountZero, AddressOf MyChild_ChildCountZero
            serverCard.UpdateServerInfo(server)
            ServerCardList.Add(serverCard)
            PanServers.Children.Add(serverCard)
        Next
    End Sub
    
    Public Sub RefreshTip()
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

    ''' <summary>
    ''' 异步ping所有服务器
    ''' </summary>
    Private Sub PingAllServers()
        For Each server In ServerCardList
            Dim currentServer = server
            Task.Run(Async Function() 
                Await currentServer.RefreshServerStatus(False) 
            End Function)
        Next
    End Sub

    ''' <summary>
    ''' ping单个服务器
    ''' </summary>
    Public Async Shared Function PingServer(server As MinecraftServerInfo) As Task(of MinecraftServerInfo)
        Dim addr = Await ServerAddressResolver.GetReachableAddressAsync(server.Address)
        
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
                    server.Icon = result.Favicon
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
    
    Public Shared Sub RemoveServer(server As ServerCard)
        Dim index = GetServerIndex(server)
        ServerCardList.Remove(server)
        Log("index: " & index)
        Log("_serverList.Count: " & ServerList.Count)
        If index >= 0 AndAlso index < ServerList.Count Then
            ServerList.RemoveAt(index)
        End If
    End Sub
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
