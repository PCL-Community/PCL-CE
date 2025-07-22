Imports Newtonsoft.Json
Imports PCL.Core.Helper
Imports PCL.Core.Service

Public Module ModNativeInterop

#Region "命名管道通信"

    Private Const LogPipePrefix As String = "PCLCE_LOG@"

    Private ReadOnly PredefinedProperties As RPCProperty() = {
        New RPCProperty("version",
            Function()
                Return VersionBaseName
            End Function),
        New RPCProperty("branch",
            Function()
                Return VersionBranchName
            End Function)
    }

    Private Function JsonIndent(indent As Boolean) As Formatting
        Return If(indent, Formatting.Indented, Formatting.None)
    End Function

    ' 用于序列化 JSON 并响应客户端 info 请求的类型
    Private Class RPCLauncherInfo
        Public path As String = PathWithName
        Public config_path As String = PathAppdataConfig
        Public window As Long = Handle.ToInt64()
        Public version As New LauncherVersion()
        Class LauncherVersion
            Public name As String = VersionBaseName
            Public commit As String = CommitHash
            Public branch As String = VersionBranchName
            Public branch_code As String = VersionBranchCode
            Public upstream As String = UpstreamVersion
        End Class
    End Class

    Private Function _InfoCallback(argument As String, content As String, indent As Boolean) As RPCResponse
        Dim json = JsonConvert.SerializeObject(New RPCLauncherInfo(), JsonIndent(indent))
        Return RPCResponse.Success(RPCResponseType.JSON, json)
    End Function

    Private ReadOnly PendingLauncherLogs As New List(Of String)
    Private ReadOnly LastUpdatedWatchers As New Dictionary(Of String, Watcher)()
    Private ReadOnly OpenLogPipes As New HashSet(Of String)()

    Public Sub PrintLog(line As String)
        PendingLauncherLogs.Add(line)
    End Sub

    '用于反序列化客户端 log 读取请求 JSON 的类型
    Private Class RPCLogOpenRequest
        Public id As String '请求读取的 log id
        Public client As Integer '前来连接的客户端的 process id，用于鉴权
        Public timeout As Integer = 5 '期望等待时间 (s)，最大 30
    End Class

    '用于序列化服务端 log 信息响应 JSON 的类型
    Private Class RPCLogInfoResponse
        Public launcher As New LauncherLog()
        Class LauncherLog
            Public id As String = "launcher"
            Public pending As Integer = PendingLauncherLogs.Count
        End Class
        Public minecraft As MinecraftLog() = MinecraftLog.GenerateLogInfo()
        Class MinecraftLog
            Public id As String
            Public pending As Integer
            Public name As String
            Public version As String
            Public state As Watcher.MinecraftState
            Public realtime As Boolean
            Shared Function GenerateLogInfo() As MinecraftLog()
                LastUpdatedWatchers.Clear()
                If Not HasRunningMinecraft Then Return {}
                Dim infoList As New List(Of MinecraftLog)
                For Each watcher In McWatcherList
                    Dim id = $"mc@{watcher.GameProcess.Id}"
                    LastUpdatedWatchers(id) = watcher
                    Dim state = watcher.State
                    Dim pending = watcher.FullLog.Count
                    Dim realtime = watcher.RealTimeLog
                    Dim game = watcher.Version
                    Dim name = game.Name
                    Dim version = game.Version.McInstance.ToString()
                    infoList.Add(New MinecraftLog With {
                        .id = id, .name = name, .pending = pending, .realtime = realtime,
                        .state = state, .version = version})
                Next
                Return infoList.ToArray()
            End Function
        End Class
    End Class

    Private Function LogPipeCallback(reader As StreamReader, writer As StreamWriter, request As RPCLogOpenRequest) As Boolean
        Return False
    End Function

    Private Function _LogCallback(argument As String, content As String, indent As Boolean) As RPCResponse
        If argument Is Nothing Then Return RPCResponse.Err("请求参数过少")
        argument = argument.ToLowerInvariant()

        If argument = "info" Then
            Dim json = JsonConvert.SerializeObject(New RPCLogInfoResponse(), JsonIndent(indent))
            Return RPCResponse.Success(RPCResponseType.JSON, json)
        End If

        If argument = "open" Then
            Dim request As RPCLogOpenRequest
            Dim clientProcess As Process
            Try
                '解析请求 JSON
                request = JsonConvert.DeserializeObject(Of RPCLogOpenRequest)(content)
                clientProcess = Process.GetProcessById(request.client)
            Catch ex As Exception
                Dim r = If(TypeOf ex Is ArgumentException AndAlso TypeOf ex IsNot ArgumentNullException, "进程 ID 不存在", "JSON 解析出错")
                Log(ex, $"[PipeRPC] log: 日志管道请求错误")
                Return RPCResponse.Err(r)
            End Try
            Dim id = request.id
            If Not id = "launcher" AndAlso Not LastUpdatedWatchers.ContainsKey(id) Then Return RPCResponse.Err("日志 ID 不存在")
            If OpenLogPipes.Contains(id) Then Return RPCResponse.Err("日志 ID 正在使用")
            Dim pipeName = LogPipePrefix & RandomInteger(10000, 99999)
            OpenLogPipes.Add(id)
            NativeInterop.StartPipeServer($"Log({id})", pipeName,
                Function(r, w, c) LogPipeCallback(r, w, request),
                Sub() OpenLogPipes.Remove(id),
                True, {clientProcess.Id})
            Return RPCResponse.Success(RPCResponseType.TEXT, pipeName, "pipe_name")
        End If

        Return RPCResponse.Err("请求参数只能是 info 或 open")
    End Function

    Private Sub AddPredefinedFunctions()
        RpcService.AddFunction("info", AddressOf _InfoCallback)
        RpcService.AddFunction("log", AddressOf _LogCallback)
    End Sub
    
    Private Sub Start()
        Log("[RPC] 正在加载预设 RPC 属性")
        For Each prop In PredefinedProperties
            RpcService.AddProperty(prop)
        Next
        Log("[RPC] 正在加载预设 RPC 函数")
        AddPredefinedFunctions()
    End Sub

    ''' <summary>
    ''' 初始化并启动 Pipe RPC 服务端，该方法应在启动器初始化时被调用，请勿重复调用
    ''' </summary>
    Public Sub StartEchoPipe()
        RunInNewThread(AddressOf Start, "RPC-Loading")
    End Sub

#End Region

End Module
