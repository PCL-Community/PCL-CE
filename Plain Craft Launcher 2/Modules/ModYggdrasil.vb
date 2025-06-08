Imports System.Threading.Tasks

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
            Task.Run(Async Function()
                         While True
                             Try
                                 Dim Context As HttpListenerContext = Await Server.GetContextAsync()
                                 ApiRoute(Context)
                             Catch ex As Exception
                                 Log(ex,"[Test] 处理响应时发生错误")
                             End Try
                         End While
                     End Function)
        End Sub

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

                    For Each Param As String In Query.Split("&"c)
                        If Param.StartsWithF("code=") Then
                            OAuthCode = Param.Substring(5)
                        End If
                    Next
                    Context.Response.StatusCode = 307
                    Context.Response.StatusDescription = "Redirect"
                    Context.Response.AddHeader("location", "/api/naid/oauth20/complete")
                    Context.Response.Close()
                    If OAuthCode IsNot Nothing Then RunInNewThread(
                            Sub()
                                GetNaidData(OAuthCode)
                            End Sub
                            )
                Case "/api/naid/oauth20/complete"
                    Try
                        Dim Res As Byte() = GetResources("Resources/naid-complete.html")
                        Using Buffer As New MemoryStream(Res)
                            Buffer.CopyToAsync(Context.Response.OutputStream)
                        End Using
                        Context.Response.OutputStream.Dispose()
                        Context.Response.StatusCode = 200
                        Context.Response.StatusDescription = "Redirect"
                        Context.Response.AddHeader("location", "/api/naid/oauth20/complete")
                        Context.Response.Close()
                    Catch ex As Exception
                        GoTo NotFound
                    End Try
                Case "/api/backgroud"
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
                Case "/api/icon"
                    Try
                        Dim Data As Byte() = GetResources("Images/icon.ico")
                        If Data Is Nothing Then GoTo NotFound
                        Using Buffer As New MemoryStream(Data)
                            Context.Response.StatusCode = 200
                            Context.Response.StatusDescription = "OK"
                            Context.Response.AddHeader("Content-Type", "application/octet-stream")
                            Buffer.CopyTo(Context.Response.OutputStream)
                            Context.Response.OutputStream.Dispose()
                            Context.Response.Close()
                        End Using
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
