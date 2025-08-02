Imports System.Runtime.InteropServices
Imports Open.Nat
Imports System.Net.Sockets
Imports Makaretu.Nat
Imports STUN
Imports System.Threading.Tasks
Imports PCL.Core.Model
Imports PCL.Core.Utils.Minecraft
Imports PCL.Core.Service
Imports PCL.Core.Extension
Imports PCL.Core.Helper

Public Module ModLink

    Public IsLobbyAvailable As Boolean = False
    Public RequiresRealname As Boolean = True

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
    Public Async Function MCInstanceFinding() As Tasks.Task(Of List(Of Tuple(Of Integer, McPingResult, String)))
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

        Dim res As New List(Of Tuple(Of Integer, McPingResult, String))
        Try
            If Not PIDLookupResult.Any Then Return res
            Dim lookupList As New List(Of Tuple(Of Integer, Integer))
            For Each pid In PIDLookupResult
                Dim infos As New List(Of Tuple(Of Integer, Integer))
                Dim ports = PortFinder.GetProcessPort(Integer.Parse(pid))
                For Each port In ports
                    infos.Add(New Tuple(Of Integer, Integer)(port, pid))
                Next
                lookupList.AddRange(infos)
            Next
            Log($"[MCDetect] 获取到端口数量 {lookupList.Count}")
            '并行查找本地，超时 3s 自动放弃
            Dim checkTasks = lookupList.Select(Function(lookup) Task.Run(Async Function()
                                                                             Try
                                                                                 Log($"[MCDetect] 找到疑似端口，开始验证：{lookup}")
                                                                                 Using test As New McPing("127.0.0.1", lookup.Item1, 3000)
                                                                                     Dim info = Await test.PingAsync()
                                                                                     Dim launcher = GetLauncherBrand(lookup.Item2)
                                                                                     If Not String.IsNullOrWhiteSpace(info.Version.Name) Then
                                                                                         Log($"[MCDetect] 端口 {lookup} 为有效 Minecraft 世界")
                                                                                         res.Add(New Tuple(Of Integer, McPingResult, String)(lookup.Item1, info, launcher))
                                                                                     End If
                                                                                 End Using
                                                                             Catch ex As Exception
                                                                                 If TypeOf ex.InnerException Is ObjectDisposedException Then
                                                                                     Log($"[McDetect] {lookup} 验证超时，已强制断开连接")
                                                                                 Else
                                                                                     Log(ex, $"[McDetect] {lookup} 验证出错")
                                                                                 End If
                                                                             End Try
                                                                         End Function)).ToArray()
            Await Task.WhenAll(checkTasks)
        Catch ex As Exception
            Log(ex, "[MCDetect] 获取端口信息错误", LogLevel.Debug)
        End Try
        Return res
    End Function
    Public Function GetLauncherBrand(pid As Integer) As String
        Try
            Dim cmd = CmdLineHelper.GetCommandLine(pid)
            If cmd.Contains("-Dminecraft.launcher.brand=") Then
                Return cmd.AfterFirst("-Dminecraft.launcher.brand=").BeforeFirst("-").TrimEnd("'", " ")
            Else
                Return cmd.AfterFirst("--versionType ").BeforeFirst("-").TrimEnd("'", " ")
            End If
        Catch ex As Exception
            Log(ex, $"[MCDetect] 检测 PID {pid} 进程的启动参数失败")
            Return ""
        End Try
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

#Region "跨启动器联机"
    Public Class TerracottaInfo
        Public Code As String
        Public NetworkName As String
        Public NetworkSecret As String
        Public Port As Integer
    End Class
    Public TcInfo As TerracottaInfo = Nothing
    Public Function ParseTerracottaCode(code As String)
        code = code.ToUpper()
        Dim matches = RegexSearch(code, "([0-9A-Z]{5}-){4}[0-9A-Z]{5}")
        If matches.Count = 0 Then
            Log("[Link] 无效的 Terracotta 联机代码")
            Return Nothing
        End If
        For Each match In matches
            Dim codeString = match.Replace("I", "1").Replace("O", "0").Replace("-", "")
            Dim value As Numerics.BigInteger = 0
            Dim checking As Integer = 0
            Dim baseChars = "0123456789ABCDEFGHJKLMNPQRSTUVWXYZ"
            For i As Integer = 0 To 23
                Dim j As Integer = baseChars.IndexOf(codeString(i))
                value += Numerics.BigInteger.Parse(j.ToString()) * Numerics.BigInteger.Pow(34, i)
                checking = (j + checking) Mod 34
            Next

            If checking <> baseChars.IndexOf(codeString(24)) Then Return Nothing
            Dim port As Integer = CInt(value Mod 65536)
            If port < 100 Then Return Nothing
            Return New TerracottaInfo With {.Code = code, .NetworkName = codeString.Substring(0, 15).ToLower(), .NetworkSecret = codeString.Substring(15, 10).ToLower(), .Port = port}
        Next
        Return Nothing
    End Function
#End Region

#Region "EasyTier"
    Public Class ETRelay
        Public Url As String
        Public Name As String
        Public Type As String
    End Class
    Public Const ETNetworkDefaultName As String = "PCLCELobby"
    Public Const ETNetworkDefaultSecret As String = "PCLCELobbyDebug"
    Public ETVersion As String = "2.4.1"
    Public ETPath As String = IO.Path.Combine(FileService.LocalDataPath, "EasyTier", ETVersion, "easytier-windows-" & If(IsArm64System, "arm64", "x86_64"))
    Public IsETRunning As Boolean = False
    Public ETServerDefList As New List(Of ETRelay)
    Public ETProcess As Process = Nothing
    Public IsETReady As Boolean = False
    Public Function LaunchEasyTier(IsHost As Boolean, Optional Name As String = ETNetworkDefaultName, Optional Secret As String = ETNetworkDefaultSecret, Optional IsAfterDownload As Boolean = False, Optional LocalPort As Integer = 25565, Optional remotePort As Integer = 25565) As Integer
        Try
            ETProcess = New Process With {
                .EnableRaisingEvents = True,
                .StartInfo = New ProcessStartInfo With {
                    .FileName = $"{ETPath}\easytier-core.exe",
                    .WorkingDirectory = ETPath,
                    .UseShellExecute = False,
                    .CreateNoWindow = True,
                    .RedirectStandardOutput = True,
                    .RedirectStandardError = True
                }
            }
            '兜底
            If ((Not File.Exists(ETPath & "\easytier-core.exe")) OrElse (Not File.Exists(ETPath & "\easytier-cli.exe")) OrElse (Not File.Exists(ETPath & "\wintun.dll"))) AndAlso (Not IsAfterDownload) Then
                Log("[Link] EasyTier 不存在，开始下载")
                Return DownloadEasyTier(True, IsHost, Name, Secret)
            End If
            Log($"[Link] EasyTier 路径: {ETPath}")

            Dim Arguments As String = Nothing

            '大厅设置
            Dim lobbyId As String
            If TcInfo IsNot Nothing Then
                lobbyId = TcInfo.Code
                Name = "terracotta-mc-" & Name
            Else
                lobbyId = (Name & Secret & If(IsHost, LocalPort, remotePort).ToString()).FromB10ToB32()
                Name = ETNetworkDefaultName & Name
                Secret = ETNetworkDefaultSecret & Secret
            End If
            If Not IsHost Then
                PageLinkLobby.JoinerLocalPort = PortHelper.GetAvailablePort()
                Log("[Link] ET 本地端口转发端口: " & PageLinkLobby.JoinerLocalPort)
            End If
            If IsHost Then '创建者
                Log($"[Link] 本机作为创建者创建大厅，EasyTier 网络名称: {Name}")
                Arguments = $"-i 10.114.51.41 --network-name {Name} --network-secret {Secret} --no-tun --relay-network-whitelist ""{Name}"" --private-mode true --tcp-whitelist {LocalPort} --udp-whitelist {LocalPort}" '创建者
            Else
                Log($"[Link] 本机作为加入者加入大厅，EasyTier 网络名称: {Name}")
                Arguments = $"-d --network-name {Name} --network-secret {Secret} --no-tun --relay-network-whitelist ""{Name}"" --private-mode true --tcp-whitelist 0 --udp-whitelist 0" '加入者
                Arguments += $" --port-forward=tcp://127.0.0.1:{PageLinkLobby.JoinerLocalPort}/10.114.51.41:{remotePort}" 'TCP
                Arguments += $" --port-forward=udp://127.0.0.1:{PageLinkLobby.JoinerLocalPort}/10.114.51.41:{remotePort}" 'UDP
            End If

            '节点设置
            Dim ServerList As String = Setup.Get("LinkRelayServer")
            Dim Servers As New List(Of String)
            If TcInfo IsNot Nothing Then
                ServerList = "tcp://public.easytier.top:11010;tcp://8.138.6.53:11010;tcp://119.23.65.180:11010;tcp://ah.nkbpal.cn:11010;tcp://gz.minebg.top:11010;tcp://39.108.52.138:11010;tcp://turn.hb.629957.xyz:11010;tcp://turn.sc.629957.xyz:11010;tcp://8.148.29.206:11010;tcp://turn.js.629957.xyz:11012;tcp://103.194.107.246:11010;tcp://sh.993555.xyz:11010;tcp://et.993555.xyz:11010;tcp://turn.bj.629957.xyz:11010;tcp://et.sh.suhoan.cn:11010;tcp://96.9.229.212:11010;tcp://et-hk.clickor.click:11010;tcp://47.113.227.73:11010;tcp://et.01130328.xyz:11010;tcp://et.ie12vps.xyz:11010;tcp://103.40.14.90:35971;tcp://154.9.255.133:11010;tcp://47.103.35.100:11010;tcp://et.gbc.moe:11011;tcp://116.206.178.250:11010"
                For Each Server In ServerList.Split(";")
                    If Not String.IsNullOrWhiteSpace(Server) Then Servers.Add(Server)
                Next
            Else
                For Each Server In ServerList.Split(";")
                    If Not String.IsNullOrWhiteSpace(Server) Then Servers.Add(Server)
                Next
                If Not Setup.Get("LinkServerType") = 2 Then
                    Dim AllowCommunity As Boolean = Setup.Get("LinkServerType") = 1
                    For Each Server In ETServerDefList
                        If Server.Type = "community" AndAlso Not AllowCommunity Then Continue For
                        Servers.Add(Server.Url)
                    Next
                End If
            End If

            '中继行为设置
            For Each Server In Servers
                Arguments += $" -p {Server}"
            Next
            If Setup.Get("LinkRelayType") = 1 Then
                Arguments += " --disable-p2p"
            End If
            '数据处理设置
            Dim proxyType As Integer = Setup.Get("LinkProxyType")
            If proxyType = 0 Then
                Arguments += " --enable-quic-proxy"
            ElseIf proxyType = 1 Then
                Arguments += " --enable-kcp-proxy"
            Else
                Arguments += " --enable-quic-proxy --enable-kcp-proxy"
            End If

            '用户名与其他参数
            Arguments += $" --latency-first --compression=zstd --multi-thread"
            Dim Hostname As String = Nothing
            Hostname = If(IsHost, "H|", "J|") & NaidProfile.Username
            If SelectedProfile IsNot Nothing Then
                Hostname += $"|{SelectedProfile.Username}"
            End If
            Arguments += $" --hostname ""{Hostname}"""

            '启动
            Log($"[Link] 启动 EasyTier")
            'Log($"[Link] EasyTier 参数: {Arguments}")
            ETProcess.StartInfo.Arguments = Arguments
            RunInUi(Sub() FrmLinkLobby.LabFinishId.Text = lobbyId)
            ETProcess.Start()
            IsETRunning = True
            Return 0
        Catch ex As Exception
            Log("[Link] 尝试启动 EasyTier 时遇到问题: " + ex.ToString())
            IsETRunning = False
            ETProcess = Nothing
            Return 1
        End Try
    End Function
    Public DlEasyTierLoader As LoaderCombo(Of JObject) = Nothing
    Public Function DownloadEasyTier(Optional LaunchAfterDownload As Boolean = False, Optional IsHost As Boolean = False, Optional Name As String = ETNetworkDefaultName, Optional Secret As String = ETNetworkDefaultSecret)
        Dim DlTargetPath As String = PathTemp + $"EasyTier\EasyTier-{ETVersion}.zip"
        RunInNewThread(Function()
                           Try
                               '构造步骤加载器
                               Dim Loaders As New List(Of LoaderBase)
                               '下载
                               Dim Address As New List(Of String)
                               Address.Add($"https://staticassets.naids.com/resources/pclce/static/easytier/easytier-windows-{If(IsArm64System, "arm64", "x86_64")}-v{ETVersion}.zip")
                               Address.Add($"https://s3.pysio.online/pcl2-ce/static/easytier/easytier-windows-{If(IsArm64System, "arm64", "x86_64")}-v{ETVersion}.zip")

                               Loaders.Add(New LoaderDownload("下载 EasyTier", New List(Of NetFile) From {New NetFile(Address.ToArray, DlTargetPath, New FileChecker(MinSize:=1024 * 64))}) With {.ProgressWeight = 15})
                               Loaders.Add(New LoaderTask(Of Integer, Integer)("解压文件", Sub() ExtractFile(DlTargetPath, IO.Path.Combine(FileService.LocalDataPath, "EasyTier", ETVersion))) With {.Block = True})
                               Loaders.Add(New LoaderTask(Of Integer, Integer)("清理缓存与冗余组件", Sub()
                                                                                                File.Delete(DlTargetPath)
                                                                                                CleanupOldEasyTier()
                                                                                            End Sub))
                               If LaunchAfterDownload Then
                                   Loaders.Add(New LoaderTask(Of Integer, Integer)("启动 EasyTier", Function() LaunchEasyTier(IsHost, Name, Secret, True)))
                               End If
                               Loaders.Add(New LoaderTask(Of Integer, Integer)("刷新界面", Sub() RunInUi(Sub()
                                                                                                         FrmLinkLobby.BtnCreate.IsEnabled = True
                                                                                                         FrmLinkLobby.BtnSelectJoin.IsEnabled = True
                                                                                                         Hint("联机组件下载完成！", HintType.Finish)
                                                                                                     End Sub)) With {.Show = False})
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
        Return 0
    End Function
    Private Sub CleanupOldEasyTier()
        Dim subDirs As String() = Directory.GetDirectories(IO.Path.Combine(FileService.LocalDataPath, "EasyTier"))
        For Each folderPath As String In subDirs
            Dim name As String = IO.Path.GetFileName(folderPath)
            If Not name.Equals(ETVersion) Then
                Try
                    Directory.Delete(folderPath, True)
                Catch ex As Exception
                    Log(ex, "[Link] 清理旧版本 EasyTier 出错")
                End Try
            End If
        Next
    End Sub
    Public Sub ExitEasyTier()
        PageLinkLobby.IsETFirstCheckFinished = False
        If IsETRunning AndAlso ETProcess IsNot Nothing Then
            Try
                Log($"[Link] 停止 EasyTier（PID: {ETProcess.Id}）")
                ETProcess.Kill()
                ETProcess.WaitForExit(200)
                IsETRunning = False
                IsETReady = False
                ETProcess = Nothing
                TcInfo = Nothing
                PageLinkLobby.HostInfo = Nothing
                PageLinkLobby.RemotePort = Nothing
                PageLinkLobby.JoinerLocalPort = Nothing
                PageLinkLobby.IsETFirstCheckFinished = False
                RunInUi(Sub()
                            FrmLinkLobby.LabFinishId.Text = ""
                            FrmLinkLobby.BtnFinishExit.Text = "退出大厅"
                        End Sub)
                StopMcPortForward()
            Catch ex As InvalidOperationException
                Log("[Link] EasyTier 进程不存在，可能已退出")
                IsETRunning = False
                IsETReady = False
                ETProcess = Nothing
            Catch ex As NullReferenceException
                Log("[Link] EasyTier 进程不存在，可能已退出")
                IsETRunning = False
                IsETReady = False
                ETProcess = Nothing
            Catch ex As Exception
                Log("[Link] 尝试停止 EasyTier 进程时遇到问题: " + ex.ToString())
                IsETRunning = False
                IsETReady = False
                ETProcess = Nothing
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
        If SelectedProfile IsNot Nothing Then
            If SelectedProfile.Username.Contains("|") Then
                Hint("MC 玩家 ID 不可包含分隔符 (|) ！")
                Return False
            End If
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
        If DlEasyTierLoader IsNot Nothing Then
            If DlEasyTierLoader.State = LoadState.Loading Then
                Hint("EasyTier 尚未下载完成，请等待其下载完成后再试！")
                Return False
            ElseIf DlEasyTierLoader.State = LoadState.Failed OrElse DlEasyTierLoader.State = LoadState.Aborted Then
                Hint("正在下载 EasyTier，请稍后...")
                DownloadEasyTier()
                Return False
            End If
        End If
        Return True
    End Function
    Public Function LaunchLink(IsHost As Boolean, Optional Name As String = ETNetworkDefaultName, Optional Secret As String = ETNetworkDefaultSecret, Optional LocalPort As Integer = 25565, Optional remotePort As Integer = 25565) As Integer
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
        Return LaunchEasyTier(IsHost, Name, Secret, LocalPort:=LocalPort, remotePort:=remotePort)
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
    Public NaidIsGettingInfo As Boolean
    Public Sub GetNaidData(Token As String, Optional IsRefresh As Boolean = False, Optional IsRetry As Boolean = False, Optional IsSilent As Boolean = False)
        RunInNewThread(Sub() GetNaidDataSync(Token, IsRefresh, IsRetry, IsSilent))
    End Sub
    Public Function GetNaidDataSync(Token As String, Optional IsRefresh As Boolean = False, Optional IsRetry As Boolean = False, Optional IsSilent As Boolean = False) As Boolean
        If NaidIsGettingInfo Then Return False
        NaidIsGettingInfo = True
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
        Finally
            NaidIsGettingInfo = False
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
    Private UdpThread As Thread = Nothing
    Private TcpThread As Thread = Nothing
    Private ServerSocket As Socket = Nothing
    Private BoardcastClient As Socket
    Private IsMcPortForwardRunning As Boolean = False
    Private PortForwardRetryTimes As Integer = 0
    Public Async Sub McPortForward(remoteIp As String, Optional remotePort As Integer = 25565, Optional desc As String = "§ePCL CE 局域网广播", Optional isRetry As Boolean = False)
        If IsMcPortForwardRunning Then Exit Sub
        If isRetry Then PortForwardRetryTimes += 1
        Log($"[Link] 开始 MC 端口转发，远程 IP: {remoteIp}, 远程端口: {remotePort}")
        Dim Sip As New IPEndPoint((Await Dns.GetHostAddressesAsync(remoteIp))(0), remotePort)

        ServerSocket = New Socket(SocketType.Stream, ProtocolType.Tcp)
        ServerSocket.Bind(New IPEndPoint(IPAddress.Any, 0))
        ServerSocket.Listen(-1)
        Dim localPort As Integer = CType(ServerSocket.LocalEndPoint, IPEndPoint).Port
        IsMcPortForwardRunning = True
        UdpThread = New Thread(Async Sub()
                                   Try
                                       Log($"[Link] 开始进行 MC 局域网广播, 广播的本地端口: {localPort}")
                                       BoardcastClient = New Socket(SocketType.Dgram, ProtocolType.Udp)
                                       BoardcastClient.DualMode = True
                                       'ChatClient = New UdpClient("224.0.2.60", 4445)
                                       'ChatClientV6 = New UdpClient("ff02::1:ff00:60", 4445)
                                       Dim Buffer As Byte() = Encoding.UTF8.GetBytes($"[MOTD]{desc}[/MOTD][AD]{localPort}[/AD]")
                                       Dim boardcastEndpoint = New IPEndPoint(IPAddress.Parse("127.0.0.1"), 4445)
                                       'Dim boardcastEndpointv6 = New IPEndPoint(IPAddress.Parse("::1"), 4445)
                                       Log($"[Link] 端口转发: {remoteIp}:{remotePort} -> 本地 {localPort}")
                                       While IsMcPortForwardRunning
                                           If IsMcPortForwardRunning AndAlso BoardcastClient IsNot Nothing Then
                                               BoardcastClient.SendTo(Buffer, boardcastEndpoint)
                                               'BoardcastClient.SendTo(Buffer, boardcastEndpointv6)
                                               If IsMcPortForwardRunning Then Await Task.Delay(1500)
                                           End If
                                       End While
                                   Catch ex As Exception
                                       If Not IsMcPortForwardRunning Then Exit Sub
                                       If PortForwardRetryTimes < 4 Then
                                           Log(ex, "[Link] Minecraft UDP 组播线程异常", LogLevel.Normal)
                                           Log($"[Link] Minecraft 端口转发线程异常，放弃前再尝试 {3 - PortForwardRetryTimes} 次")
                                           McPortForward(remoteIp, remotePort, desc, True)
                                       Else
                                           Log(ex, "[Link] Minecraft 端口转发线程异常", LogLevel.Hint)
                                           IsMcPortForwardRunning = False
                                       End If
                                   End Try
                               End Sub)

        TcpThread = New Thread(Async Sub()
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
                                   Catch ex As SocketException
                                       If Not IsMcPortForwardRunning Then Exit Sub
                                       Log("[Link] 疑似 MC 断开与创建者的连接，再次进行广播")
                                       StartUdpBoardcast()
                                   Catch ex As Exception
                                       If Not IsMcPortForwardRunning Then Exit Sub
                                       If PortForwardRetryTimes < 4 Then
                                           Log($"[Link] Minecraft TCP 转发线程异常，放弃前再尝试 {3 - PortForwardRetryTimes} 次")
                                           McPortForward(remoteIp, remotePort, desc, True)
                                       Else
                                           Log(ex, "[Link] Minecraft TCP 转发线程异常", LogLevel.Hint)
                                           IsMcPortForwardRunning = False
                                       End If
                                   End Try
                               End Sub)
        Try
            UdpThread.Start()
            TcpThread.Start()
        Catch ex As Exception
            Log(ex, "[Link] 启动 MC 局域网广播失败")
            IsMcPortForwardRunning = False
        End Try
    End Sub
    Private Sub StartUdpBoardcast()
        Try
            Try
                UdpThread.Abort()
            Catch ex As Exception

            End Try
            UdpThread.Start()
        Catch ex As Exception
            Log(ex, "[Link] 启动 MC 局域网广播失败")
        End Try
    End Sub
    Public Sub StopMcPortForward()
        IsMcPortForwardRunning = False
        Log("[Link] 停止 MC 端口转发")
        If UdpThread IsNot Nothing Then
            UdpThread.Abort()
            UdpThread = Nothing
        End If
        If TcpThread IsNot Nothing Then
            TcpThread.Abort()
            TcpThread = Nothing
        End If
        If BoardcastClient IsNot Nothing Then
            BoardcastClient.Close()
            BoardcastClient = Nothing
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
