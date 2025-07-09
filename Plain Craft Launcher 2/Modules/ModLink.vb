Imports System.Runtime.InteropServices
Imports Open.Nat
Imports System.Net.Sockets
Imports Makaretu.Nat
Imports STUN
Imports System.Threading.Tasks
Imports PCL.Core.Helper
Imports PCL.Core.Service

Public Module ModLink

    Public IsLobbyAvailable As Boolean = False
    Public RequiresRealname As Boolean = True

#Region "MCPing"
    Public Class WorldInfo
        Public Property Port As Integer
        Public Property VersionName As String
        Public Property PlayerMax As Integer
        Public Property PlayerOnline As Integer
        Public Property Description As String
        Public Property Favicon As String
        Public Property Latency As Integer = -1

        Public Overrides Function ToString() As String
            Return $"[MCPing] Version: {VersionName}, Players: {PlayerOnline}/{PlayerMax}, Description: {Description}"
        End Function
    End Class

    Public Class MCPing


        Sub New(IP As String, Optional Port As UInt16 = 25565)
            _IP = IP
            _Port = Port
        End Sub

        Private _IP As String
        Private _Port As UInt16

        ''' <summary>
        ''' 对疑似 MC 端口进行 MCPing，并返回相关信息
        ''' </summary>
        Public Async Function GetInfo(Optional DoLog As Boolean = True) As Tasks.Task(Of WorldInfo)
            Try
                ' 创建 TCP 客户端并连接到服务器
                Using client As New TcpClient(_IP, _Port)
                    If DoLog Then Log($"[MCPing] Established connection ({_IP}:{_Port})", LogLevel.Debug)
                    ' 向服务器发送握手数据包
                    Using stream = client.GetStream()
                        If Not stream.CanWrite OrElse Not stream.CanRead Then Return Nothing
                        Dim latency As New Stopwatch

                        Dim handshake As Byte() = BuildHandshake(_IP, _Port)
                        If DoLog Then Log($"[MCPing] Sending {String.Join(" ", handshake)}", LogLevel.Debug)
                        Await stream.WriteAsync(handshake, 0, handshake.Length)
                        If DoLog Then Log($"[MCPing] Sended handshake", LogLevel.Debug)

                        ' 向服务器发送查询状态信息的数据包
                        Dim statusRequest As Byte() = BuildStatusRequest()
                        If DoLog Then Log($"[MCPing] Sending {String.Join(" ", statusRequest)}")
                        Await stream.WriteAsync(statusRequest, 0, statusRequest.Length)
                        If DoLog Then Log($"[MCPing] Sended statusrequest", LogLevel.Debug)

                        ' 读取服务器响应的数据
                        Dim res As New List(Of Byte)
                        Dim buffer(4096) As Byte

                        ' 读取varInt头部
                        latency.Start()
                        Dim packetLength = VarInt.ReadFromStream(stream)
                        latency.Stop()
                        If DoLog Then Log($"[MCPing] Got packet length ({packetLength})", LogLevel.Debug)

                        ' 读取剩余数据包
                        Dim totalBytes = 0
                        Using cts As New CancellationTokenSource
                            cts.CancelAfter(TimeSpan.FromSeconds(5))
                            Do
                                Dim bytesRead = Await stream.ReadAsync(buffer, 0, buffer.Length, cts.Token)
                                If bytesRead = 0 Then Exit Do
                                res.AddRange(buffer.Take(bytesRead))
                                totalBytes += bytesRead
                                If DoLog Then Log($"[MCPing] Received part ({bytesRead})", LogLevel.Debug)
                            Loop While totalBytes < packetLength
                        End Using

                        If DoLog Then Log($"[MCPing] Received ({res.Count})", LogLevel.Debug)
                        Dim response As String = Encoding.UTF8.GetString(res.ToArray(), 0, res.Count)
                        Dim startIndex = response.IndexOf("{""", StringComparison.Ordinal)
                        If startIndex > 10 Then Return Nothing
                        response = response.Substring(startIndex)
                        If DoLog Then Log("[MCPing] Server Response: " & response, LogLevel.Debug)

                        '查找并截取第一段 JSON
                        '有些 mod 或是整合包定制服务端会在返回的 JSON 后面添加新的内容，比如 Better MC
                        '这时候需要把第一段合法的 JSON 截出来，否则下面解析 JSON 会炸掉
                        '但是它们完全可以在返回的 JSON 内部添加自定义内容，添加在后面估计就是为了图 mixin 省事
                        '不守规范一时爽，第三方解析火葬场
                        Dim stack = 0, index = 0, stackStr = False, length = response.Length
                        While index < length
                            Select Case response(index)
                                Case "\"c
                                    If stackStr Then index += 1
                                Case """"c
                                    stackStr = Not stackStr
                                Case "{"c
                                    If Not stackStr Then stack += 1
                                Case "}"c
                                    stack -= 1
                                    If stack = 0 Then
                                        response = response.Substring(0, index + 1)
                                        If DoLog Then Log("[MCPing] Correct Response: " & response, LogLevel.Debug)
                                        Exit While
                                    End If
                            End Select
                            index += 1
                        End While

                        '解析返回的 JSON 文本
                        Dim j = JObject.Parse(response)

                        Dim world As New WorldInfo With {
                        .VersionName = j("version")("name"),
                        .PlayerMax = j("players")("max"),
                        .PlayerOnline = j("players")("online"),
                        .Favicon = If(j("favicon"), ""),
                        .Port = _Port,
                        .Latency = Math.Round(latency.ElapsedMilliseconds)
                        }
                        Dim descObj = j("description")
                        world.Description = ""
                        If descObj.Type = JTokenType.Object AndAlso descObj("extra") IsNot Nothing Then
                            If DoLog Then Log("[MCPing] 获取到的内容为 extra 形式", LogLevel.Debug)
                            world.Description = MinecraftFormatter.ConvertToMinecraftFormat(descObj)
                        ElseIf descObj.Type = JTokenType.Object AndAlso descObj("text") IsNot Nothing Then
                            If DoLog Then Log("[MCPing] 获取到的内容为 text 形式", LogLevel.Debug)
                            world.Description = descObj("text").ToString()
                        ElseIf descObj.Type = JTokenType.String Then
                            If DoLog Then Log("[MCPing] 获取到的内容为 string 形式", LogLevel.Debug)
                            world.Description = descObj.ToString()
                        End If
                        Return world
                    End Using
                End Using
            Catch ex As Exception
                Log(ex, "[MCPing] Error: " & ex.Message)
            End Try
            Return Nothing
        End Function


        Function BuildHandshake(serverIp As String, serverPort As Integer) As Byte()
            ' 构建握手数据包
            Dim handshake As New List(Of Byte)
            handshake.AddRange(VarInt.Encode(0)) ' 数据包 ID 握手包
            handshake.AddRange(VarInt.Encode(578)) ' 协议
            Dim encodedIP = Encoding.UTF8.GetBytes(serverIp)
            handshake.AddRange(VarInt.Encode(CULng(encodedIP.Length))) ' 服务器地址长度
            handshake.AddRange(encodedIP) ' 服务器地址
            handshake.AddRange(BitConverter.GetBytes(CUShort(serverPort)).Reverse()) ' 服务器端口
            handshake.AddRange(VarInt.Encode(1)) ' 下一个状态 获取服务器状态

            handshake.InsertRange(0, VarInt.Encode(CULng(handshake.Count)))

            Return handshake.ToArray()
        End Function

        Function BuildStatusRequest() As Byte()
            ' 构建状态请求数据包
            Dim packet As New List(Of Byte)
            packet.AddRange(VarInt.Encode(1))
            packet.AddRange(VarInt.Encode(0))
            Return packet.ToArray() ' 状态请求数据包
        End Function
    End Class
#End Region

#Region "端口查找"
    Public Class PortFinder
        ' 定义需要的结构和常量
        <StructLayout(LayoutKind.Sequential)>
        Public Structure MIB_TCPROW_OWNER_PID
            Public dwState As Integer
            Public dwLocalAddr As Integer
            Public dwLocalPort As Integer
            Public dwRemoteAddr As Integer
            Public dwRemotePort As Integer
            Public dwOwningPid As Integer
        End Structure

        <DllImport("iphlpapi.dll", SetLastError:=True)>
        Public Shared Function GetExtendedTcpTable(
        ByVal pTcpTable As IntPtr,
        ByRef dwOutBufLen As Integer,
        ByVal bOrder As Boolean,
        ByVal ulAf As Integer,
        ByVal TableClass As Integer,
        ByVal reserved As Integer) As Integer
        End Function

        Public Shared Function GetProcessPort(ByVal dwProcessId As Integer) As List(Of Integer)
            Dim ports As New List(Of Integer)
            Dim tcpTable As IntPtr = IntPtr.Zero
            Dim dwSize As Integer = 0
            Dim dwRetVal As Integer

            If dwProcessId = 0 Then
                Return ports
            End If

            dwRetVal = GetExtendedTcpTable(IntPtr.Zero, dwSize, True, 2, 3, 0)
            If dwRetVal <> 0 AndAlso dwRetVal <> 122 Then ' 122 表示缓冲区不足
                Return ports
            End If

            tcpTable = Marshal.AllocHGlobal(dwSize)
            Try
                If GetExtendedTcpTable(tcpTable, dwSize, True, 2, 3, 0) <> 0 Then
                    Return ports
                End If

                Dim tablePtr As IntPtr = tcpTable
                Dim dwNumEntries As Integer = Marshal.ReadInt32(tablePtr)
                tablePtr = IntPtr.Add(tablePtr, 4)

                For i As Integer = 0 To dwNumEntries - 1
                    Dim row As MIB_TCPROW_OWNER_PID = Marshal.PtrToStructure(Of MIB_TCPROW_OWNER_PID)(tablePtr)
                    If row.dwOwningPid = dwProcessId Then
                        ports.Add(row.dwLocalPort >> 8 Or (row.dwLocalPort And &HFF) << 8) ' 转换端口号
                    End If
                    tablePtr = IntPtr.Add(tablePtr, Marshal.SizeOf(Of MIB_TCPROW_OWNER_PID)())
                Next
            Finally
                Marshal.FreeHGlobal(tcpTable)
            End Try

            Return ports
        End Function
    End Class
#End Region

#Region "UPnP 映射"

    Public Enum UPnPStatusType
        Disabled
        Enabled
        Unsupported
        Failed
    End Enum
    ''' <summary>
    ''' UPnP 状态，可能值："Disabled", "Enabled", "Unsupported", "Failed"
    ''' </summary>
    Public UPnPStatus As UPnPStatusType = Nothing
    Public UPnPMappingName As String = "PCL2 CE Link Lobby"
    Public UPnPDevice = Nothing
    Public CurrentUPnPMapping As Mapping = Nothing
    Public UPnPPublicPort As String = Nothing

    ''' <summary>
    ''' 寻找 UPnP 设备并尝试创建一个 UPnP 映射
    ''' </summary>
    Public Async Sub CreateUPnPMapping(Optional LocalPort As Integer = 25565, Optional PublicPort As Integer = 10240)
        Log($"[UPnP] 尝试创建 UPnP 映射，本地端口：{LocalPort}，远程端口：{PublicPort}，映射名称：{UPnPMappingName}")

        UPnPPublicPort = PublicPort
        Dim UPnPDiscoverer = New NatDiscoverer()
        Dim cts = New CancellationTokenSource(10000)
        Try
            UPnPDevice = Await UPnPDiscoverer.DiscoverDeviceAsync(PortMapper.Upnp, cts)

            CurrentUPnPMapping = New Mapping(Protocol.Tcp, LocalPort, PublicPort, UPnPMappingName)
            Await UPnPDevice.CreatePortMapAsync(CurrentUPnPMapping)

            Await UPnPDevice.CreatePortMapAsync(New Mapping(Protocol.Tcp, LocalPort, PublicPort, "PCL2 Link Lobby"))

            UPnPStatus = UPnPStatusType.Enabled
            Log("[UPnP] UPnP 映射已创建")
        Catch NotFoundEx As NatDeviceNotFoundException
            UPnPStatus = UPnPStatusType.Unsupported
            CurrentUPnPMapping = Nothing
            Log("[UPnP] 找不到可用的 UPnP 设备")
        Catch ex As Exception
            UPnPStatus = UPnPStatusType.Failed
            CurrentUPnPMapping = Nothing
            Log("[UPnP] UPnP 映射创建失败: " + ex.ToString())
        End Try
    End Sub

    ''' <summary>
    ''' 尝试移除现有 UPnP 映射记录
    ''' </summary>
    Public Async Sub RemoveUPnPMapping()
        Log($"[UPnP] 尝试移除 UPnP 映射，本地端口：{CurrentUPnPMapping.PrivatePort}，远程端口：{CurrentUPnPMapping.PublicPort}，映射名称：{UPnPMappingName}")

        Try
            Await UPnPDevice.DeletePortMapAsync(CurrentUPnPMapping)

            UPnPStatus = UPnPStatusType.Disabled
            CurrentUPnPMapping = Nothing
            Log("[UPnP] UPnP 映射移除成功")
        Catch ex As Exception
            UPnPStatus = UPnPStatusType.Failed
            CurrentUPnPMapping = Nothing
            Log("[UPnP] UPnP 映射移除失败: " + ex.ToString())
        End Try
    End Sub

#End Region

#Region "Minecraft 实例探测"
    Public Async Function MCInstanceFinding() As Tasks.Task(Of List(Of WorldInfo))
        'Java 进程 PID 查询
        Dim PIDLookupResult As New List(Of String)
        Dim JavaNames As New List(Of String)
        JavaNames.Add("java")
        JavaNames.Add("javaw")

        For Each TargetJava In JavaNames
            Dim JavaProcesses As Process() = Process.GetProcessesByName(TargetJava)
            Log($"[MCDetect] 找到 {TargetJava} 进程 {JavaProcesses.Length} 个")

            If JavaProcesses Is Nothing OrElse JavaProcesses.Length = 0 Then
                Continue For
            Else
                For Each p In JavaProcesses
                    Log("[MCDetect] 检测到 Java 进程，PID: " + p.Id.ToString())
                    PIDLookupResult.Add(p.Id.ToString())
                Next
            End If
        Next

        Dim res As New List(Of WorldInfo)
        Try
            If Not PIDLookupResult.Any Then Return res
            Dim ports As New List(Of Integer)
            For Each pid In PIDLookupResult
                ports.AddRange(PortFinder.GetProcessPort(Integer.Parse(pid)))
            Next
            Log($"[MCDetect] 获取到端口数量 {ports.Count}")
            Dim checkTasks = ports.Select(Function(port) Task.Run(Async Function()
                                                                      Log($"[MCDetect] 找到疑似端口，开始验证：{port}")
                                                                      Dim test As New MCPing("127.0.0.1", port)
                                                                      Dim info = Await test.GetInfo()
                                                                      If Not String.IsNullOrWhiteSpace(info.VersionName) Then
                                                                          Log($"[MCDetect] 端口 {port} 为有效 Minecraft 世界")
                                                                          res.Add(info)
                                                                      End If
                                                                  End Function)).ToArray()
            Await Task.WhenAll(checkTasks)
        Catch ex As Exception
            Log(ex, "[MCDetect] 获取端口信息错误", LogLevel.Debug)
        End Try
        Return res
    End Function
#End Region

#Region "NAT 穿透"
    Public NATEndpoints As List(Of LeasedEndpoint) = Nothing
    ''' <summary>
    ''' 尝试进行 NAT 映射
    ''' </summary>
    ''' <param name="localPort">本地端口</param>
    Public Async Sub CreateNATTranversal(LocalPort As String)
        Log($"开始尝试进行 NAT 穿透，本地端口 {LocalPort}")
        Try
            NATEndpoints = New List(Of LeasedEndpoint) '寻找 NAT 设备
            For Each nat In NatDiscovery.GetNats()
                Dim lease = Await nat.CreatePublicEndpointAsync(ProtocolType.Tcp, LocalPort)
                Dim endpoint = New LeasedEndpoint(lease)
                NATEndpoints.Add(endpoint)
                PageLinkLobby.PublicIPPort = endpoint.ToString()
                Log($"NAT 穿透完成，公网地址: {endpoint}")
            Next
        Catch ex As Exception
            Log("尝试进行 NAT 穿透失败: " + ex.ToString())
        End Try

    End Sub

    ''' <summary>
    ''' 移除 NAT 映射
    ''' </summary>
    Public Sub RemoveNATTranversal()
        Log("开始尝试移除 NAT 映射")
        Try
            For Each endpoint In NATEndpoints
                endpoint.Dispose()
            Next
            Log("NAT 映射已移除")
        Catch ex As Exception
            Log("尝试移除 NAT 映射失败: " + ex.ToString())
        End Try
    End Sub
#End Region

#Region "EasyTier"
    Public Class ETRelay
        Public Url As String
        Public Name As String
        Public Type As String
    End Class
    Public Const ETNetworkDefaultName As String = "PCLCELobby"
    Public Const ETNetworkDefaultSecret As String = "PCLCELobbyDebug"
    Public ETVersion As String = "2.3.2"
    Public ETPath As String = PathTemp + $"EasyTier\{ETVersion}\easytier-windows-{If(IsArm64System, "arm64", "x86_64")}"
    Public IsETRunning As Boolean = False
    Public ETServerDefList As New List(Of ETRelay)
    Public ETProcessPid As String = Nothing
    Public Function LaunchEasyTier(IsHost As Boolean, Optional Name As String = ETNetworkDefaultName, Optional Secret As String = ETNetworkDefaultSecret, Optional IsAfterDownload As Boolean = False, Optional LocalPort As Integer = 25565) As Integer
        Try
            '兜底
            If ((Not File.Exists(ETPath & "\easytier-core.exe")) OrElse (Not File.Exists(ETPath & "\easytier-cli.exe")) OrElse (Not File.Exists(ETPath & "\wintun.dll"))) AndAlso (Not IsAfterDownload) Then
                Log("[Link] EasyTier 不存在，开始下载")
                Return DownloadEasyTier(True, IsHost, Name, Secret)
            End If
            Log($"[Link] EasyTier 路径: {ETPath}")

            Dim Arguments As String = Nothing

            '大厅设置
            Secret = ETNetworkDefaultSecret & Secret
            Name = ETNetworkDefaultName & Name
            If IsHost Then
                Log($"[Link] 本机作为创建者创建大厅，EasyTier 网络名称: {Name}")
                Arguments = $"-i 10.114.51.41 --network-name {Name} --network-secret {Secret} --no-tun --relay-network-whitelist ""{Name}"" --private-mode true" '创建者
            Else
                Log($"[Link] 本机作为加入者加入大厅，EasyTier 网络名称: {Name}")
                Arguments = $"-d --network-name {Name} --network-secret {Secret} --dev-name ""PCLCELobby"" --relay-network-whitelist ""{Name}"" --private-mode true" '加入者
            End If

            '节点设置
            Dim ServerList As String = Setup.Get("LinkRelayServer")
            Dim Servers As New List(Of String)
            For Each Server In ServerList.Split(";")
                If Not String.IsNullOrWhiteSpace(Server) Then Servers.Add(Server)
            Next
            If Not Setup.Get("LinkServerType") = 2 Then
                Dim AllowCommunity As Boolean = Setup.Get("LinkServerType") = 2
                For Each Server In ETServerDefList
                    If Server.Type = "community" AndAlso Not AllowCommunity Then Continue For
                    Servers.Add(Server.Url)
                Next
            End If
            '中继行为设置
            For Each Server In Servers
                Arguments += $" -p {Server}"
            Next
            If Setup.Get("LinkRelayType") = 1 Then
                Arguments += " --disable-p2p"
            End If

            '创建防火墙规则
            If IsHost Then
                PromoteService.Append($"start cmd. ; /c netsh advfirewall firewall add rule name=""PCLCE Lobby - EasyTier"" dir=in action=allow program=""{ETPath}\easytier-core.exe"" protocol=any localport={LocalPort}")
            End If
            PromoteService.Append($"start cmd. ; /c netsh advfirewall firewall add rule name=""PCLCE Lobby - EasyTier"" dir=in action=deny program=""{ETPath}\easytier-core.exe"" protocol=any")
            PromoteService.Activate()

            '用户名与其他参数
            Arguments += $" --enable-kcp-proxy --latency-first --use-smoltcp"
            Dim Hostname As String = Nothing
            Hostname = If(IsHost, LocalPort & "-", "J-") & NaidProfile.Username
            If SelectedProfile IsNot Nothing Then
                Hostname += $"-{SelectedProfile.Username}"
            End If
            Arguments += $" --hostname ""{Hostname}"""

            '启动
            Log($"[Link] 启动 EasyTier")
            'Log($"[Link] EasyTier 参数: {Arguments}")
            RunInUi(Sub() FrmLinkLobby.LabFinishId.Text = Name.Replace(ETNetworkDefaultName, "") & Secret.Replace(ETNetworkDefaultSecret, ""))
            PromoteService.Append($"start {ETPath}\easytier-core.exe. ; {Arguments}", Sub(s As String) ETProcessPid = s, False)
            IsETRunning = PromoteService.Activate()
            Return 0
        Catch ex As Exception
            Log("[Link] 尝试启动 EasyTier 时遇到问题: " + ex.ToString())
            IsETRunning = False
            ETProcessPid = Nothing
            Return 1
        End Try
    End Function
    Public DlEasyTierLoader As LoaderCombo(Of JObject) = Nothing
    Public Function DownloadEasyTier(Optional LaunchAfterDownload As Boolean = False, Optional IsHost As Boolean = False, Optional Name As String = ETNetworkDefaultName, Optional Secret As String = ETNetworkDefaultSecret)
        Dim DlTargetPath As String = PathTemp + $"EasyTier\EasyTier-{ETVersion}.zip"
        Return RunInNewThread(Function()
                                  Try
                                      '构造步骤加载器
                                      Dim Loaders As New List(Of LoaderBase)
                                      '下载
                                      Dim Address As New List(Of String)
                                      Address.Add($"https://s3.pysio.online/pcl2-ce/static/easytier/easytier-windows-{If(IsArm64System, "arm64", "x86_64")}-v{ETVersion}.zip")
                                      Address.Add($"https://github.com/EasyTier/EasyTier/releases/download/v{ETVersion}/easytier-windows-{If(IsArm64System, "arm64", "x86_64")}-v{ETVersion}.zip")

                                      Loaders.Add(New LoaderDownload("下载 EasyTier", New List(Of NetFile) From {New NetFile(Address.ToArray, DlTargetPath, New FileChecker(MinSize:=1024 * 64))}) With {.ProgressWeight = 15})
                                      Loaders.Add(New LoaderTask(Of Integer, Integer)("解压文件", Sub() ExtractFile(DlTargetPath, PathTemp + "EasyTier\" + ETVersion)))
                                      Loaders.Add(New LoaderTask(Of Integer, Integer)("清理文件", Sub() File.Delete(DlTargetPath)))
                                      If LaunchAfterDownload Then
                                          Loaders.Add(New LoaderTask(Of Integer, Integer)("启动 EasyTier", Function() LaunchEasyTier(IsHost, Name, Secret, True)))
                                      End If
                                      Loaders.Add(New LoaderTask(Of Integer, Integer)("刷新界面", Sub() RunInUi(Sub()
                                                                                                                PageLinkLobby.IsEasyTierExist = True
                                                                                                                FrmLinkLobby.BtnCreate.IsEnabled = True
                                                                                                                FrmLinkLobby.BtnSelectJoin.IsEnabled = True
                                                                                                                Hint("联机组件下载完成！", HintType.Finish)
                                                                                                            End Sub)))
                                      '启动
                                      DlEasyTierLoader = New LoaderCombo(Of JObject)("大厅初始化", Loaders)
                                      DlEasyTierLoader.Start()
                                      LoaderTaskbarAdd(DlEasyTierLoader)
                                      FrmMain.BtnExtraDownload.ShowRefresh()
                                      FrmMain.BtnExtraDownload.Ribble()
                                      Return 0
                                  Catch ex As Exception
                                      Log(ex, "[Link] 下载 EasyTier 依赖文件失败", LogLevel.Hint)
                                      Hint("下载 EasyTier 依赖文件失败，请检查网络连接", HintType.Critical)
                                      Return 1
                                  End Try
                              End Function)
    End Function

    Public Sub ExitEasyTier()

        If IsETRunning AndAlso ETProcessPid IsNot Nothing Then
            Try
                Log($"[Link] 停止 EasyTier（PID: {ETProcessPid}）")
                Dim returns = Nothing
                PromoteService.Append("start cmd. ; /c netsh advfirewall firewall delete rule name=""PCLCE Lobby - EasyTier""")
                PromoteService.Append($"kill {ETProcessPid}", Function(s) returns = s)
                PromoteService.Activate()
                IsETRunning = False
                ETProcessPid = Nothing
                PageLinkLobby.RemotePort = Nothing
                PageLinkLobby.Hostname = Nothing
                PageLinkLobby.IsETFirstCheckFinished = False
                StopMcPortForward()
            Catch ex As InvalidOperationException
                Log("[Link] EasyTier 进程不存在，可能已退出")
                IsETRunning = False
                ETProcessPid = Nothing
            Catch ex As NullReferenceException
                Log("[Link] EasyTier 进程不存在，可能已退出")
                IsETRunning = False
                ETProcessPid = Nothing
            Catch ex As Exception
                Log("[Link] 尝试停止 EasyTier 进程时遇到问题: " + ex.ToString())
                ETProcessPid = Nothing
            End Try
        End If
    End Sub

#End Region

#Region "大厅操作"
    Public Function LobbyPrecheck() As Boolean
        If Not IsLobbyAvailable Then
            Hint("大厅功能暂不可用，请稍后再试", HintType.Critical)
            Return False
        End If
        If String.IsNullOrWhiteSpace(Setup.Get("LinkNaidRefreshToken")) Then
            Hint("请先前往联机设置并登录至 Natayark Network 再进行联机！", HintType.Critical)
            Return False
        End If
        Try
            GetNaidData(Setup.Get("LinkNaidRefreshToken"), True, IsSilent:=True)
        Catch ex As Exception
            Log("[Link] 刷新 Natayark ID 信息失败，需要重新登录")
            Hint("请重新登录 Natayark Network 账号再试！", HintType.Critical)
            Return False
        End Try
        Dim WaitCount As Integer = 0
        While String.IsNullOrWhiteSpace(NaidProfile.Username)
            If WaitCount > 30 Then Exit While
            Thread.Sleep(500)
            WaitCount += 1
        End While
        If String.IsNullOrWhiteSpace(NaidProfile.Username) Then
            Hint("尝试获取 Natayark ID 信息失败", HintType.Critical)
            Return False
        End If
        If RequiresRealname AndAlso Not NaidProfile.IsRealname Then
            Hint("请先前往 Natayark 账户中心进行实名验证再尝试操作！", HintType.Critical)
            Return False
        End If
        If Not NaidProfile.Status = 0 Then
            Hint("你的 Natayark Network 账号状态异常，可能已被封禁！", HintType.Critical)
            Return False
        End If
        Return True
    End Function
    Public Function LaunchLink(IsHost As Boolean, Optional Name As String = ETNetworkDefaultName, Optional Secret As String = ETNetworkDefaultSecret, Optional LocalPort As Integer = 25565) As Integer
        '回传联机数据
        Log("[Link] 开始发送联机数据")
        Dim Servers As String = Nothing
        If Not Setup.Get("LinkServerType") = 2 Then
            Dim AllowCommunity As Boolean = Setup.Get("LinkServerType") = 2
            For Each Server In ETServerDefList
                If Server.Type = "community" AndAlso Not AllowCommunity Then Continue For
                Servers &= Server.Url & ";"
            Next
        End If
        Servers &= Setup.Get("LinkRelayServer")
        Dim Data As New JObject From {
                {"Tag", "Link"},
                {"Id", UniqueAddress},
                {"NaidId", NaidProfile.Id},
                {"NaidEmail", NaidProfile.Email},
                {"NaidLastIp", NaidProfile.LastIp},
                {"NetworkName", Name},
                {"Server", Servers},
                {"IsHost", IsHost}
            }
        Dim SendData = New JObject From {{"data", Data}}
        Try
            Dim Result As String = NetRequestRetry("https://pcl2ce.pysio.online/post", "POST", SendData.ToString(), "application/json")
            If Result.Contains("数据已成功保存") Then
                Log("[Link] 联机数据已发送")
            Else
                Log("[Link] 联机数据发送失败，原始返回内容: " + Result)
                Hint("无法连接到数据服务器，请检查网络连接或稍后再试！", HintType.Critical)
                Return 1
            End If
        Catch ex As Exception
            If ex.Message.Contains("429") Then
                Log("[Link] 联机数据发送失败，请求过于频繁")
                Hint("请求过于频繁，请稍后再试", HintType.Critical, False)
            Else
                Log(ex, "[Link] 联机数据发送失败", LogLevel.Normal)
                Hint("无法连接到数据服务器，请检查网络连接或稍后再试！", HintType.Critical, False)
            End If
            Return 1
        End Try
        StopMcPortForward()
        Return LaunchEasyTier(IsHost, Name, Secret, LocalPort:=LocalPort)
    End Function
#End Region

#Region "Natayark ID"
    Public Class NaidUser
        Public Id As Int32
        Public Email As String
        Public Username As String
        Public AccessToken As String
        Public RefreshToken As String
        Public Status As Integer = 1
        Public IsRealname As Boolean = False
        Public LastIp As String
    End Class
    Public NaidProfile As New NaidUser()
    Public NaidProfileException As Exception
    Public Sub GetNaidData(Token As String, Optional IsRefresh As Boolean = False, Optional IsRetry As Boolean = False, Optional IsSilent As Boolean = False)
        RunInNewThread(Sub() GetNaidDataSync(Token, IsRefresh, IsRetry, IsSilent))
    End Sub
    Public Function GetNaidDataSync(Token As String, Optional IsRefresh As Boolean = False, Optional IsRetry As Boolean = False, Optional IsSilent As Boolean = False) As Boolean
        Try
            '获取 AccessToken 和 RefreshToken
            Dim RequestData As String = $"grant_type={If(IsRefresh, "refresh_token", "authorization_code")}&client_id={NatayarkClientId}&client_secret={NatayarkClientSecret}&{If(IsRefresh, "refresh_token", "code")}={Token}&redirect_uri=http://localhost:29992/callback"
            'Log("[Link] Naid 请求数据: " & RequestData)
            Thread.Sleep(500)
            Dim Received As String = NetRequestRetry("https://account.naids.com/api/oauth2/token", "POST", RequestData, "application/x-www-form-urlencoded")
            Dim Data As JObject = JObject.Parse(Received)
            NaidProfile.AccessToken = Data("access_token").ToString()
            NaidProfile.RefreshToken = Data("refresh_token").ToString()
            Dim ExpiresAt As String = Data("refresh_token_expires_at").ToString()

            '获取用户信息
            Dim Headers As New Dictionary(Of String, String)
            Headers.Add("Authorization", $"Bearer {NaidProfile.AccessToken}")
            Dim ReceivedUserData As String = NetRequestRetry("https://account.naids.com/api/api/user/data", "GET", "", "application/json", Headers:=Headers)
            Dim UserData As JObject = JObject.Parse(ReceivedUserData)("data")
            NaidProfile.Id = UserData("id").ToObject(Of Int32)()
            NaidProfile.Username = UserData("username").ToString()
            NaidProfile.Email = UserData("email").ToString()
            NaidProfile.Status = UserData("status")
            NaidProfile.IsRealname = UserData("realname")
            NaidProfile.LastIp = UserData("last_ip").ToString()
            '保存数据
            Setup.Set("LinkNaidRefreshToken", NaidProfile.RefreshToken)
            Setup.Set("LinkNaidRefreshExpiresAt", ExpiresAt)
            '若处于联机设置界面，则进行刷新
            If FrmSetupLink IsNot Nothing Then RunInUi(Sub() FrmSetupLink.Reload())
            If Not IsSilent Then Hint("已登录至 Natayark Network！", HintType.Finish)
            Return True
        Catch ex As Exception
            If IsRetry Then '如果重试了还失败就报错
                Log(ex, "[Link] Naid 登录失败，请尝试前往设置重新登录", LogLevel.Msgbox)
                NaidProfile = New NaidUser
                Setup.Set("LinkNaidRefreshToken", "")
            End If
            If ex.Message.Contains("invalid access token") Then
                Log("[Link] Naid Access Token 无效，尝试刷新登录")
                Return GetNaidDataSync(Token:=Setup.Get("LinkNaidRefreshToken"), IsRefresh:=True, IsRetry:=True)
            ElseIf ex.Message.Contains("invalid_grant") Then
                Log("[Link] Naid 验证代码无效，原始信息: " & ex.ToString())
            ElseIf ex.Message.Contains("401") Then
                NaidProfile = New NaidUser
                Setup.Set("LinkNaidRefreshToken", "")
                Hint("Natayark 账号信息已过期，请前往设置重新登录！", HintType.Critical)
            Else
                Log(ex, "[Link] Naid 登录失败，请尝试前往设置重新登录", LogLevel.Msgbox)
                NaidProfile = New NaidUser
                Setup.Set("LinkNaidRefreshToken", "")
            End If
            NaidProfileException = ex
            Return False
        End Try
    End Function
#End Region

#Region "NAT 测试"
    ''' <summary>
    ''' 使用 EasyTier Cli 进行网络测试。
    ''' </summary>
    ''' <returns></returns>
    Public Function NetTestET()
        Dim ETCliProcess As New Process With {
                                   .StartInfo = New ProcessStartInfo With {
                                       .FileName = $"{ETPath}\easytier-cli.exe",
                                       .WorkingDirectory = ETPath,
                                       .Arguments = "stun",
                                       .ErrorDialog = False,
                                       .CreateNoWindow = True,
                                       .WindowStyle = ProcessWindowStyle.Hidden,
                                       .UseShellExecute = False,
                                       .RedirectStandardOutput = True,
                                       .RedirectStandardError = True,
                                       .RedirectStandardInput = True,
                                       .StandardOutputEncoding = Encoding.UTF8},
                                   .EnableRaisingEvents = True
                               }
        If Not File.Exists(ETCliProcess.StartInfo.FileName) Then
            Log("[Link] EasyTier 不存在，开始下载")
            DownloadEasyTier()
        End If
        Log($"[Link] EasyTier 路径: {ETCliProcess.StartInfo.FileName}")
        Dim Output As String = Nothing

        ETCliProcess.Start()
        Output = ETCliProcess.StandardOutput.ReadToEnd()
        Output.Replace("stun info: StunInfo ", "")

        Dim OutJObj As JObject = JObject.Parse(Output)
        Dim NatType As String = OutJObj("udp_nat_type")
        Dim SupportIPv6 As Boolean = False
        Dim Ips As Array = OutJObj("public_ip").ToArray()
        For Each Ip In Ips
            If Ip.contains(":") Then
                SupportIPv6 = True
                Exit For
            End If
        Next
        Return {NatType, SupportIPv6}
    End Function
    ''' <summary>
    ''' 进行网络测试，包括 IPv4 NAT 类型测试和 IPv6 支持情况测试
    ''' </summary>
    ''' <returns>NAT 类型 + IPv6 支持与否</returns>
    Public Function NetTest() As String()
        '申请通过防火墙以准确测试 NAT 类型
        Dim RetryTime As Integer = 0
        Try
PortRetry:
            Dim TestTcpListener = TcpListener.Create(RandomInteger(20000, 65000))
            TestTcpListener.Start()
            Thread.Sleep(200)
            TestTcpListener.Stop()
        Catch ex As Exception
            Log(ex, "[Link] 请求防火墙通过失败")
            If RetryTime >= 3 Then
                Log("[Link] 请求防火墙通过失败次数已达 3 次，不再重试")
                Exit Try
            End If
            GoTo PortRetry
        End Try
        'IPv4 NAT 测试
        Dim NATType As String
        Dim STUNServerDomain As String = "stun.miwifi.com" '指定 STUN 服务器
        Log("[STUN] 指定的 STUN 服务器: " + STUNServerDomain)
        Try
            Dim STUNServerIP As String = Dns.GetHostAddresses(STUNServerDomain)(0).ToString() '解析 STUN 服务器 IP
            Log("[STUN] 解析目标 STUN 服务器 IP: " + STUNServerIP)
            Dim STUNServerEndPoint As IPEndPoint = New IPEndPoint(IPAddress.Parse(STUNServerIP), 3478) '设置 IPEndPoint

            STUNClient.ReceiveTimeout = 500 '设置超时
            Log("[STUN] 开始进行 NAT 测试")
            Dim STUNTestResult = STUNClient.Query(STUNServerEndPoint, STUNQueryType.ExactNAT, True) '进行 STUN 测试

            NATType = STUNTestResult.NATType.ToString()
            Log("[STUN] 本地 NAT 类型: " + NATType)
        Catch ex As Exception
            Log(ex, "[STUN] 进行 NAT 测试失败", LogLevel.Normal)
            NATType = "TestFailed"
        End Try

        'IPv6
        Dim IPv6Status As String = "Unsupported"
        Try
            For Each ip In NatDiscovery.GetIPAddresses()
                If ip.AddressFamily() = AddressFamily.InterNetworkV6 Then 'IPv6
                    If ip.IsIPv6LinkLocal() OrElse ip.IsIPv6SiteLocal() OrElse ip.IsIPv6Teredo() OrElse ip.IsIPv4MappedToIPv6() Then
                        Continue For
                    ElseIf ip.IsPublic() Then
                        Log("[IP] 检测到 IPv6 公网地址")
                        IPv6Status = "Public"
                        Exit For
                    ElseIf ip.IsPrivate() AndAlso Not IPv6Status = "Supported" Then
                        Log("[IP] 检测到 IPv6 支持")
                        IPv6Status = "Supported"
                        Continue For
                    End If
                End If
            Next
        Catch ex As Exception
            Log(ex, "[IP] 进行 IPv6 测试失败", LogLevel.Normal)
            IPv6Status = "Unknown"
        End Try

        Return {NATType, IPv6Status}
    End Function
#End Region

#Region "局域网广播"
    Private tr1 As Thread = Nothing
    Private tr2 As Thread = Nothing
    Private ServerSocket As Socket = Nothing
    Private ChatClient As UdpClient = Nothing
    Private IsMcPortForwardRunning As Boolean = False
    Private PortForwardRetryTimes As Integer = 0
    Public Async Sub McPortForward(Ip As String, Optional Port As Integer = 25565, Optional Desc As String = "§ePCL CE 局域网广播", Optional IsRetry As Boolean = False)
        If IsMcPortForwardRunning Then Exit Sub
        If IsRetry Then PortForwardRetryTimes += 1
        Log($"[Link] 开始 MC 端口转发，IP: {Ip}, 端口: {Port}")
        Dim Sip As New IPEndPoint((Await Dns.GetHostAddressesAsync(Ip))(0), Port)

        ServerSocket = New Socket(SocketType.Stream, ProtocolType.Tcp)
        ServerSocket.Bind(New IPEndPoint(IPAddress.Any, 0))
        ServerSocket.Listen(-1)

        IsMcPortForwardRunning = True

        tr1 = New Thread(Async Sub()
                             Try
                                 Log("[Link] 开始进行 MC 局域网广播")
                                 ChatClient = New UdpClient("224.0.2.60", 4445)
                                 Dim Buffer As Byte() = Encoding.UTF8.GetBytes($"[MOTD]{Desc}[/MOTD][AD]{CType(ServerSocket.LocalEndPoint, IPEndPoint).Port}[/AD]")
                                 While IsMcPortForwardRunning
                                     If ChatClient IsNot Nothing Then
                                         ChatClient.EnableBroadcast = True
                                         ChatClient.MulticastLoopback = True
                                     End If

                                     If IsMcPortForwardRunning AndAlso ChatClient IsNot Nothing Then
                                         Await ChatClient.SendAsync(Buffer, Buffer.Length)
                                         If IsMcPortForwardRunning Then Await Task.Delay(1500)
                                     End If
                                 End While
                             Catch ex As Exception
                                 If PortForwardRetryTimes < 4 Then
                                     Log($"[Link] Minecraft 端口转发线程异常，放弃前再尝试 {3 - PortForwardRetryTimes} 次")
                                     McPortForward(Ip, Port, Desc, True)
                                 Else
                                     Log(ex, "[Link] Minecraft 端口转发线程异常", LogLevel.Msgbox)
                                     IsMcPortForwardRunning = False
                                 End If
                             End Try
                         End Sub)

        tr2 = New Thread(Async Sub()
                             Dim c As Socket
                             Dim s As Socket
                             Try
                                 While IsMcPortForwardRunning
                                     c = ServerSocket.Accept()
                                     s = New Socket(SocketType.Stream, ProtocolType.Tcp)

                                     s.Connect(Sip)
                                     Dim Count As Integer = 0
                                     While Not s.Connected
                                         If Count <= 5 Then
                                             Count += 1
                                             Await Task.Delay(1000)
                                         Else
                                             Log("[Link] 连接到目标 MC 服务器失败")
                                             Return
                                         End If
                                     End While
                                     RunInNewThread(Sub() Forward(c, s))
                                     RunInNewThread(Sub() Forward(s, c))
                                 End While
                             Catch ex As Exception
                                 If PortForwardRetryTimes < 4 Then
                                     Log($"[Link] Minecraft 端口转发线程异常，放弃前再尝试 {3 - PortForwardRetryTimes} 次")
                                     McPortForward(Ip, Port, Desc, True)
                                 Else
                                     Log(ex, "[Link] Minecraft 端口转发线程异常", LogLevel.Msgbox)
                                     IsMcPortForwardRunning = False
                                 End If
                             End Try
                         End Sub)
        Try
            tr1.Start()
            tr2.Start()
        Catch ex As Exception
            Log(ex, "[Link] 启动 MC 局域网广播失败")
            IsMcPortForwardRunning = False
        End Try
    End Sub
    Public Sub StopMcPortForward()
        Log("[Link] 停止 MC 端口转发")
        If tr1 IsNot Nothing Then
            tr1.Abort()
            tr1 = Nothing
        End If
        If tr2 IsNot Nothing Then
            tr2.Abort()
            tr2 = Nothing
        End If
        If ChatClient IsNot Nothing Then
            ChatClient.Close()
            ChatClient = Nothing
        End If
        If ServerSocket IsNot Nothing Then
            ServerSocket.Close()
            ServerSocket = Nothing
        End If
        If fw_s IsNot Nothing Then
            fw_s.Disconnect(False)
            fw_s.Close()
            fw_s = Nothing
        End If
        If fw_c IsNot Nothing Then
            fw_c.Disconnect(False)
            fw_c.Close()
            fw_c = Nothing
        End If
        IsMcPortForwardRunning = False
    End Sub

    Private fw_s As Socket = Nothing
    Private fw_c As Socket = Nothing
    Private Sub Forward(s As Socket, c As Socket)
        fw_s = s
        fw_c = c
        Try
            Dim Buffer As Byte() = New Byte(8192) {}

            While IsMcPortForwardRunning
                If IsMcPortForwardRunning Then
                    Dim Count As Integer = s.Receive(Buffer, 0, Buffer.Length, SocketFlags.None)
                    If Count > 0 Then
                        c.Send(Buffer, 0, Count, SocketFlags.None)
                    Else
                        fw_s = Nothing
                        fw_c = Nothing
                        Exit While
                    End If
                End If
            End While
        Catch ex As Exception
            Try
                c.Disconnect(False)
            Catch ex1 As Exception
            End Try
            Try
                s.Disconnect(False)
            Catch ex1 As Exception
            End Try
            fw_s = Nothing
            fw_c = Nothing
        End Try

    End Sub
#End Region

End Module
