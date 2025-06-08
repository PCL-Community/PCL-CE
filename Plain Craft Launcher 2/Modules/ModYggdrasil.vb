Imports System.Threading.Tasks
Imports Newtonsoft.Json

Public Module ModYggdrasil
    Private Server As HttpListener
    Private _Server As HttpServer
    Private ChangeLock As New Object
    Private PicAddress As String
    Public Function BackgroundPicChangeCallback(Pic As String)
        SyncLock ChangeLock
            PicAddress = Pic
            Return True
        End SyncLock
    End Function
    Public Sub LoadHttpServer()
        _Server = New HttpServer()
    End Sub
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
                            Log(ex, "[Test] 处理响应时发生错误")
                        End Try
                    End While
                End Function)
        End Sub

        Private Status As New CompleteStatus()
        Private Class CompleteStatus
            Public success As Boolean = False
            Public username As String
            Public message As String
            Public stacktrace As String
        End Class

        Public Sub ApiRoute(Context As HttpListenerContext)

            Thread.Sleep(10)

            Dim RequestUrl As String = Context.Request.Url.AbsolutePath
            Dim OAuthCode As String = Nothing


            ' 多斜杠处理
            While RequestUrl.Contains("//")
                RequestUrl = RequestUrl.Replace("//", "/")
            End While

            Select Case RequestUrl
                Case "/api/naid/oauth20/callback"
                    If Not Context.Request.HttpMethod.ToUpper() = "GET" Then
                        Context.Response.StatusCode = 400
                        Context.Response.StatusDescription = "Bad Request"
                        Context.Response.Close()
                        Return
                    End If

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
                            Status.success = True
                            Status.username = NaidProfile.Username
                        Else
                            Status.success = False
                            Status.message = $"获取用户信息失败，请尝试重新登录"
                            Status.stacktrace = NaidProfileException.ToString()
                        End If
                    Else
                        Status.success = False
                        Status.message = $"回调参数无效: {Query}"
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
                            Context.Response.StatusCode = 200
                            Context.Response.StatusDescription = "OK"
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
                        Dim status = JsonConvert.SerializeObject(Me.Status)
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
                    Context.Response.StatusCode = 404
                    Context.Response.StatusDescription = "NotFound"
                    Context.Response.Close()
            End Select
        End Sub
    End Class
    Public Function OpenNaidAuthorizeUrl()
        OpenWebsite($"https://account.naids.com/oauth2/authorize?response_type=code&client_id={NatayarkClientId}&redirect_uri=http://local.luotianyi-0712.top:29992/api/naid/oauth20/callback")
        Return Nothing
    End Function
End Module
