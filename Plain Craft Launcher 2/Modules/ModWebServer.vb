Imports System.Net.NetworkInformation
Imports System.Threading.Tasks
Imports Newtonsoft.Json
Imports PCL.Core.Utils

Public Module ModWebServer

    Private _webServers As New Dictionary(Of String, WebServer)

    ''' <summary>
    ''' 在新的 <see cref="Task"/> 中开始 HTTP 服务端响应。
    ''' </summary>
    ''' <param name="name">服务端名称</param>
    ''' <param name="server">服务端实例</param>
    ''' <returns>是否成功开始，若已存在同名实例则返回 <c>false</c></returns>
    Public Function StartWebServer(name As String, server As WebServer) As Boolean
        name = name.ToLowerInvariant()
        SyncLock _webServers
            If _webServers.ContainsKey(name) Then Return False
            _webServers(name) = server
        End SyncLock
        Task.Run(
            Async Function() As Task
                Log($"[WebServer] 服务端 '{name}' 已启动")
                Try
                    Await server.StartResponse()
                Catch ex As Exception
                    Log(ex, $"[WebServer] 服务端 '{name}' 运行出错")
                End Try
                server.Dispose()
                Log($"[WebServer] 服务端 '{name}' 已停止")
                SyncLock _webServers
                    _webServers.Remove(name)
                End SyncLock
            End Function)
        Return True
    End Function

    ''' <summary>
    ''' 检查指定名称的 HTTP 服务端是否正在运行
    ''' </summary>
    ''' <param name="name">服务端名称</param>
    ''' <returns>是否正在运行</returns>
    Public Function IsWebServerRunning(name As String) As Boolean
        name = name.ToLowerInvariant()
        Return _webServers.ContainsKey(name)
    End Function

    ''' <summary>
    ''' 销毁 HTTP 服务端。若服务端正在运行，可能会引发异常。
    ''' </summary>
    ''' <param name="name">服务端名称</param>
    ''' <returns>是否成功销毁，若名称不存在或已经销毁则返回 <c>false</c></returns>
    Public Function DisposeWebServer(name As String) As Boolean
        name = name.ToLowerInvariant()
        SyncLock _webServers
            If Not _webServers.ContainsKey(name) Then Return False
            Try
                _webServers(name).Dispose()
            Catch ex As ObjectDisposedException
                Return False
            End Try
            _webServers.Remove(name)
            Return True
        End SyncLock
    End Function

#Region "网页登录回调"

    Private ChangeLock As New Object
    Private PicAddress As String
    Public Function BackgroundPicChangeCallback(Pic As String)
        SyncLock ChangeLock
            PicAddress = Pic
            Return True
        End SyncLock
    End Function

    <Serializable>
    Public Class OAuthCompleteStatus
        Public Property success As Boolean = False
        Public Property username As String
        Public Property message As String
        Public Property stacktrace As String
        Public Shared Function Complete(username As String) As OAuthCompleteStatus
            Return New OAuthCompleteStatus With {.success = True, .username = username}
        End Function
        Public Shared Function Failed(message As String, Optional ex As Exception = Nothing) As OAuthCompleteStatus
            Return New OAuthCompleteStatus With {.success = False, .message = message, .stacktrace = ex?.ToString()}
        End Function
    End Class

    Public Delegate Function OAuthComplete(success As Boolean, parameters As IDictionary(Of String, String), content As String) As OAuthCompleteStatus

    Public Function StartOAuthWaitingCallback(serviceName As String, url As String, completeCallback As OAuthComplete) As Boolean
        If IsWebServerRunning(serviceName) Then Return False
        RunInNewThread(
            Sub()
                Dim port As UShort
                SyncLock _webServers
                    Dim server As RoutedWebServer = Nothing
                    '寻找可用端口号创建服务端实例
                    For port = 29992 To 30992
                        If Not Array.Exists(IPGlobalProperties.GetIPGlobalProperties.GetActiveTcpListeners, Function(i) i.Port = port) Then
                            server = New RoutedWebServer($"127.0.0.1:{port}")
                            Log($"[OAuth] {serviceName}: 已开始监听 {port} 端口，正在初始化路由")
                            Exit For
                        End If
                    Next
                    If port > 30992 Then
                        Dim message = "29992 ~ 30992 范围内没有任何可用端口号"
                        completeCallback(False, Nothing, message)
                        Log($"[OAuth] {serviceName}: {message}")
                        Exit Sub
                    End If
                    '状态数据
                    Dim status As OAuthCompleteStatus = Nothing
                    Dim callbackParameters As IDictionary(Of String, String) = Nothing
                    Dim callbackContent As String = Nothing
                    '设置路由
                    Dim redirect = RoutedResponse.Redirect("/complete")
                    server.Route("/callback",
                        Function(path, request)
                            '解析回调 URL 参数
                            Dim parameterMap As New Dictionary(Of String, String)
                            Dim query = request.Url.Query
                            Dim queryIndex = query.IndexOf("?"c)
                            If queryIndex <> -1 AndAlso query.Length > queryIndex Then
                                Try
                                    Dim sq = query.Substring(queryIndex + 1).Split("&"c)
                                    Dim splitChar = {"="c}
                                    For Each iq In sq
                                        Dim q = iq.Split(splitChar, 2)
                                        parameterMap(q(0)) = q(1)
                                    Next
                                Catch ex As Exception
                                    status = OAuthCompleteStatus.Failed("回调参数解析出错", ex)
                                    Return redirect
                                End Try
                            End If
                            callbackParameters = parameterMap
                            '读取回调内容
                            If request.HasEntityBody Then
                                Try
                                    Using reader As New StreamReader(request.InputStream, request.ContentEncoding)
                                        callbackContent = reader.ReadToEnd()
                                    End Using
                                Catch ex As Exception
                                    status = OAuthCompleteStatus.Failed("读取回调内容出错", ex)
                                    Return redirect
                                End Try
                            End If
                            Return redirect
                        End Function)
                    server.Route("/status",
                        Function()
                            If callbackParameters Is Nothing Then Return RoutedResponse.NotFound
                            server.StopResponse()
                            Try
                                If status Is Nothing Then
                                    status = completeCallback(True, callbackParameters, callbackContent)
                                ElseIf Not status.success Then
                                    Log($"[OAuth] {serviceName}: {status.message}{vbCrLf}{status.stacktrace}")
                                    completeCallback(False, Nothing, status.message)
                                End If
                            Catch ex As Exception
                                status = OAuthCompleteStatus.Failed("处理回调出错", ex)
                            End Try
                            Return RoutedResponse.Json(status)
                        End Function)
                    server.Route("/assets/background",
                        Function()
                            If String.IsNullOrWhiteSpace(PicAddress) Then Return RoutedResponse.NotFound
                            Return RoutedResponse.Input(New FileStream(PicAddress, FileMode.Open, FileAccess.Read, FileShare.None, 16384, True))
                        End Function)
                    server.Route("/assets/icon", Function() RoutedResponse.Input(GetResourceStream("Images/icon.ico")))
                    server.Route("/complete", Function() RoutedResponse.Input(GetResourceStream("Resources/oauth-complete.html"), "text/html"))
                    '开始响应请求
                    StartWebServer($"oauth/{serviceName}", server)
                    Log($"[OAuth] {serviceName}: 初始化完成，开始响应 HTTP 请求")
                End SyncLock
                '打开 OAuth URL
                OpenWebsite(url.Replace("%r", $"http://localhost:{port}/callback"))
            End Sub, $"CallbackWebServerLoading/{serviceName}")
        Return True
    End Function

    Public Sub StartNaidAuthorize(Optional completeCallback As Action = Nothing)
        StartOAuthWaitingCallback("NatayarkID", $"https://account.naids.com/oauth2/authorize?response_type=code&client_id={NatayarkClientId}&redirect_uri=%r",
            Function(success, parameters, content)
                If Not success Then
                    MyMsgBox(content, IsWarn:=True)
                    completeCallback?.Invoke()
                    Return Nothing
                End If
                Dim status As OAuthCompleteStatus = Nothing
                Dim code = parameters("code")
                Dim result = GetNaidDataSync(code)
                If result Then
                    status = OAuthCompleteStatus.Complete(NaidProfile.Username)
                Else
                    status = OAuthCompleteStatus.Failed("获取用户信息失败，请尝试重新登录", NaidProfileException)
                End If
                completeCallback?.Invoke()
                Return status
            End Function)
    End Sub

#End Region

#Region "旧的 HTTP 服务端实现"

    Private Server As HttpListener
    Public Class HttpServer
        Public Sub New()
            Server = New HttpListener()
            Server.Prefixes.Add("http://127.0.0.1:29992/")
            Server.Start()
            Task.Run(
                Async Function()
                    While True
                        Try
                            Dim Context As HttpListenerContext = Await Server.GetContextAsync()
                            ApiRoute(Context)
                        Catch ex As Exception
                            Log(ex, "[Server] 处理响应时发生错误")
                        End Try
                    End While
                End Function)
        End Sub

        Private CurrentStatus As New OAuthCompleteStatus()

        Public Sub ApiRoute(Context As HttpListenerContext)


            Dim RequestUrl As String = Context.Request.Url.AbsolutePath
            Dim OAuthCode As String = Nothing

            ' 多斜杠处理
            While RequestUrl.Contains("//")
                RequestUrl = RequestUrl.Replace("//", "/")
            End While

            Select Case RequestUrl
                Case "/api/naid/oauth20/callback"

                    Dim Query = Context.Request.Url.Query
                    If Query.StartsWith("?") Then Query = Query.Substring(1)

                    '在 URL 参数中寻找授权码
                    For Each Param As String In Query.Split("&"c)
                        If Param.StartsWithF("code=") Then
                            OAuthCode = Param.Substring(5)
                        End If
                    Next

                    '设置状态信息
                    If OAuthCode IsNot Nothing Then
                        Dim result = GetNaidDataSync(OAuthCode)
                        If result Then
                            CurrentStatus.success = True
                            CurrentStatus.username = NaidProfile.Username
                        Else
                            CurrentStatus.success = False
                            CurrentStatus.message = $"获取用户信息失败，请尝试重新登录"
                            CurrentStatus.stacktrace = NaidProfileException.ToString()
                        End If
                    Else
                        CurrentStatus.success = False
                        CurrentStatus.message = $"回调参数无效: {Query}"
                    End If

                    '重定向至结束页
                    Context.Response.StatusCode = HttpStatusCode.Redirect
                    Context.Response.AddHeader("location", "/complete")
                    Context.Response.Close()
                Case "/complete"
                    Try
                        Dim Data = GetResourceStream("Resources/oauth-complete.html")
                        If Data Is Nothing Then GoTo NotFound
                        Context.Response.StatusCode = HttpStatusCode.OK
                        Context.Response.AddHeader("Content-Type", "text/html, charset=utf-8")
                        Data.CopyTo(Context.Response.OutputStream)
                        Context.Response.OutputStream.Dispose()
                        Context.Response.Close()
                    Catch ex As Exception
                        GoTo NotFound
                    End Try
                Case "/assets/background"
                    SyncLock ChangeLock
                        If PicAddress Is Nothing OrElse String.IsNullOrWhiteSpace(PicAddress) Then GoTo NotFound
                        Using FileReadStream As New FileStream(PicAddress, FileMode.Open, FileAccess.Read, FileShare.None, 16384, True)
                            Context.Response.StatusCode = HttpStatusCode.OK
                            Context.Response.AddHeader("Content-Type", "application/octet-stream")
                            FileReadStream.CopyTo(Context.Response.OutputStream)
                            Context.Response.OutputStream.Dispose()
                            Context.Response.Close()
                        End Using
                    End SyncLock
                Case "/assets/icon.ico"
                    Try
                        Dim Data = GetResourceStream("Images/icon.ico")
                        If Data Is Nothing Then GoTo NotFound
                        Context.Response.StatusCode = HttpStatusCode.OK
                        Context.Response.AddHeader("Content-Type", "application/octet-stream")
                        Data.CopyTo(Context.Response.OutputStream)
                        Context.Response.OutputStream.Dispose()
                        Context.Response.Close()
                    Catch ex As Exception
                        GoTo NotFound
                    End Try
                Case "/api/naid/oauth20/status"
                    Try
                        Dim status = JsonConvert.SerializeObject(CurrentStatus)
                        Dim buffer = Encoding.UTF8.GetBytes(status)
                        Context.Response.StatusCode = HttpStatusCode.OK
                        Context.Response.AddHeader("Content-Type", "application/json, charset=utf-8")
                        Context.Response.OutputStream.Write(buffer, 0, buffer.Length)
                        Context.Response.OutputStream.Dispose()
                        Context.Response.Close()
                    Catch ex As Exception
                        GoTo NotFound
                    End Try
                Case Else
NotFound:
                    Context.Response.StatusCode = HttpStatusCode.NotFound
                    Context.Response.Close()
            End Select
        End Sub
    End Class

#End Region

End Module
