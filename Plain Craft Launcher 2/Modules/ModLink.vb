Imports System.Runtime.InteropServices
Imports System.Net.Sockets
Imports Makaretu.Nat
Imports STUN
Imports System.Threading.Tasks
Imports PCL.Core.IO
Imports PCL.Core.Link
Imports PCL.Core.Link.EasyTier
Imports PCL.Core.Link.Lobby
Imports PCL.Core.Link.Natayark.NatayarkProfileManager
Imports PCL.Core.Utils
Imports PCL.Core.Utils.OS

Public Module ModLink

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

#Region "Minecraft 实例探测"
    Public Async Function MCInstanceFinding() As Task(Of List(Of Tuple(Of Integer, McPingResult, String)))
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
                                                                             Log($"[MCDetect] 找到疑似端口，开始验证：{lookup}")
                                                                             Using test As New McPing("127.0.0.1", lookup.Item1, 3000)
                                                                                 Dim info As McPingResult
                                                                                 Try
                                                                                     info = Await test.PingAsync()
                                                                                     Dim launcher = GetLauncherBrand(lookup.Item2)
                                                                                     If Not String.IsNullOrWhiteSpace(info.Version.Name) Then
                                                                                         Log($"[MCDetect] 端口 {lookup} 为有效 Minecraft 世界")
                                                                                         res.Add(New Tuple(Of Integer, McPingResult, String)(lookup.Item1, info, launcher))
                                                                                         Return
                                                                                     End If
                                                                                 Catch ex As Exception
                                                                                     If TypeOf ex.InnerException Is ObjectDisposedException Then
                                                                                         Log($"[McDetect] {lookup} 验证超时，已强制断开连接，将尝试旧版检测")
                                                                                     Else
                                                                                         Log(ex, $"[McDetect] {lookup} 验证出错，将尝试旧版检测")
                                                                                     End If
                                                                                 End Try
                                                                                 Try
                                                                                     info = Await test.PingOldAsync()
                                                                                     If Not String.IsNullOrWhiteSpace(info.Version.Name) Then
                                                                                         Log($"[MCDetect] 端口 {lookup} 为有效 Minecraft 世界")
                                                                                         res.Add(New Tuple(Of Integer, McPingResult, String)(lookup.Item1, info, String.Empty))
                                                                                         Return
                                                                                     End If
                                                                                 Catch ex As Exception
                                                                                     If TypeOf ex.InnerException Is ObjectDisposedException Then
                                                                                         Log($"[McDetect] {lookup} 验证超时，已强制断开连接")
                                                                                     Else
                                                                                         Log(ex, $"[McDetect] {lookup} 验证出错")
                                                                                     End If
                                                                                 End Try
                                                                             End Using
                                                                         End Function)).ToArray()
            Await Task.WhenAll(checkTasks)
        Catch ex As Exception
            Log(ex, "[MCDetect] 获取端口信息错误", LogLevel.Debug)
        End Try
        Return res
    End Function
    Public Function GetLauncherBrand(pid As Integer) As String
        Try
            Dim cmd = ProcessInterop.GetCommandLine(pid)
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

#Region "EasyTier"
    Public DlEasyTierLoader As LoaderCombo(Of JObject) = Nothing
    Public Function DownloadEasyTier(Optional LaunchAfterDownload As Boolean = False, Optional isHost As Boolean = False, Optional boardcastDesc As String = Nothing)
        Dim DlTargetPath As String = PathTemp + $"EasyTier\EasyTier-{ETInfoProvider.ETVersion}.zip"
        RunInNewThread(Sub()
            Try
                '构造步骤加载器
                Dim Loaders As New List(Of LoaderBase)
                '下载
                Dim Address As New List(Of String)
                Address.Add($"https://staticassets.naids.com/resources/pclce/static/easytier/easytier-windows-{If(IsArm64System, "arm64", "x86_64")}-v{ETInfoProvider.ETVersion}.zip")
                Address.Add($"https://s3.pysio.online/pcl2-ce/static/easytier/easytier-windows-{If(IsArm64System, "arm64", "x86_64")}-v{ETInfoProvider.ETVersion}.zip")

                Loaders.Add(New LoaderDownload("下载 EasyTier", New List(Of NetFile) From {New NetFile(Address.ToArray, DlTargetPath, New FileChecker(MinSize:=1024 * 64))}) With {.ProgressWeight = 15})
                Loaders.Add(New LoaderTask(Of Integer, Integer)("解压文件", Sub() ExtractFile(DlTargetPath, IO.Path.Combine(FileService.LocalDataPath, "EasyTier", ETInfoProvider.ETVersion))) With {.Block = True})
                Loaders.Add(New LoaderTask(Of Integer, Integer)("清理缓存与冗余组件", Sub()
                    File.Delete(DlTargetPath)
                    CleanupEasyTierCache()
                End Sub))
                If LaunchAfterDownload Then
                    Loaders.Add(New LoaderTask(Of Integer, Integer)("启动大厅", Sub()
                        LobbyController.Launch(isHost, If(SelectedProfile IsNot Nothing, SelectedProfile.Username, ""))
                    End Sub))
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
            Catch ex As Exception
                Log(ex, "[Link] 下载 EasyTier 依赖文件失败", LogLevel.Hint)
                Hint("下载 EasyTier 依赖文件失败，请检查网络连接", HintType.Critical)
            End Try
        End Sub)
        Return 0
    End Function
    Private Sub CleanupEasyTierCache()
        Dim subDirs As String() = Directory.GetDirectories(IO.Path.Combine(FileService.LocalDataPath, "EasyTier"))
        For Each folderPath As String In subDirs
            Dim name As String = IO.Path.GetFileName(folderPath)
            If Not name.Equals(ETInfoProvider.ETVersion) Then
                Try
                    Directory.Delete(folderPath, True)
                Catch ex As Exception
                    Log(ex, "[Link] 清理旧版本 EasyTier 出错")
                End Try
            End If
        Next
    End Sub

#End Region

#Region "大厅操作"
    Public Function LobbyPrecheck() As Boolean
        If Not LobbyInfoProvider.IsLobbyAvailable Then
            Hint("大厅功能暂不可用，请稍后再试", HintType.Critical)
            Return False
        End If
        If SelectedProfile IsNot Nothing Then
            If SelectedProfile.Username.Contains("|") Then
                Hint("MC 玩家 ID 不可包含分隔符 (|) ！")
                Return False
            End If
        End If
        If LobbyInfoProvider.RequiresLogin Then
            If String.IsNullOrWhiteSpace(Setup.Get("LinkNaidRefreshToken")) Then
                Hint("请先前往联机设置并登录至 Natayark Network 再进行联机！", HintType.Critical)
                Return False
            End If
            Try
                GetNaidData(Setup.Get("LinkNaidRefreshToken"), True)
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
            If LobbyInfoProvider.RequiresRealName AndAlso Not NaidProfile.IsRealNamed Then
                Hint("请先前往 Natayark 账户中心进行实名验证再尝试操作！", HintType.Critical)
                Return False
            End If
            If Not NaidProfile.Status = 0 Then
                Hint("你的 Natayark Network 账号状态异常，可能已被封禁！", HintType.Critical)
                Return False
            End If
        End If
        If String.IsNullOrWhiteSpace(Setup.Get("LinkUsername")) AndAlso String.IsNullOrWhiteSpace(NaidProfile.Username) Then
            Hint("请先前往设置输入一个用户名，或登录至 Natayark Network 再进行联机！", HintType.Critical)
            Return False
        End If
        If ETController.Precheck() = 1 Then
            Hint("正在下载联机依赖组件，请稍后...")
            DownloadEasyTier()
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
#End Region

End Module
