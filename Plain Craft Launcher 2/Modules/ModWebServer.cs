using System.IO;
using System.Net;
using System.Net.Http;
using Microsoft.VisualBasic;
using PCL.Core.IO.Net.Http;
using PCL.Core.Link.Natayark;

namespace PCL;

public static class ModWebServer
{
    private static readonly Dictionary<string, HttpServer> _webServers = new();

    /// <summary>
    ///     在新的 <see cref="Task" /> 中开始 HTTP 服务端响应。
    /// </summary>
    /// <param name="name">服务端名称</param>
    /// <param name="server">服务端实例</param>
    /// <returns>是否成功开始，若已存在同名实例则返回 <c>false</c></returns>
    public static bool StartWebServer(string name, HttpServer server)
    {
        name = name.ToLowerInvariant();
        lock (_webServers)
        {
            if (_webServers.ContainsKey(name))
                return false;
            _webServers[name] = server;
        }

        Task.Run(() =>
        {
            ModBase.Log($"[WebServer] 服务端 '{name}' 已启动");
            try
            {
                server.Start();
                // 保持服务器运行直到被停止（通过检查字典中是否还存在该服务器）
                while (true)
                {
                    lock (_webServers)
                    {
                        if (!_webServers.ContainsKey(name))
                            break;
                    }

                    Thread.Sleep(100);
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, $"[WebServer] 服务端 '{name}' 运行出错");
            }
            finally
            {
                try
                {
                    server.Dispose();
                }
                catch
                {
                    // 忽略已释放的异常
                }

                ModBase.Log($"[WebServer] 服务端 '{name}' 已停止");
                lock (_webServers)
                {
                    _webServers.Remove(name);
                }
            }
        });
        return true;
    }

    /// <summary>
    ///     检查指定名称的 HTTP 服务端是否正在运行
    /// </summary>
    /// <param name="name">服务端名称</param>
    /// <returns>是否正在运行</returns>
    public static bool IsWebServerRunning(string name)
    {
        name = name.ToLowerInvariant();
        return _webServers.ContainsKey(name);
    }

    /// <summary>
    ///     销毁 HTTP 服务端。若服务端正在运行，可能会引发异常。
    /// </summary>
    /// <param name="name">服务端名称</param>
    /// <returns>是否成功销毁，若名称不存在或已经销毁则返回 <c>false</c></returns>
    public static bool DisposeWebServer(string name)
    {
        name = name.ToLowerInvariant();
        lock (_webServers)
        {
            if (!_webServers.ContainsKey(name))
                return false;
            try
            {
                _webServers[name].Dispose();
            }
            catch (ObjectDisposedException ex)
            {
                return false;
            }

            _webServers.Remove(name);
            return true;
        }
    }

    #region 网页登录回调

    public class NaidCallbackServer : HttpServer
    {
        private readonly OAuthComplete _completeCallback;
        private readonly string _picAddress;
        private readonly string _serviceName;
        private string _callbackContent;
        private IDictionary<string, string> _callbackParameters;

        private OAuthCompleteStatus _status;

        public NaidCallbackServer(string serviceName, OAuthComplete completeCallback, string picAddress) : base(new[]
            { IPAddress.Parse("127.0.0.1") })
        {
            _serviceName = serviceName;
            _completeCallback = completeCallback;
            _picAddress = picAddress;
        }

        protected override void Init()
        {
            // 注册回调路由
            Register(HttpMethod.Get, "/callback", HandleCallback);
            Register(HttpMethod.Post, "/callback", HandleCallback);

            // 注册状态路由
            Register(HttpMethod.Get, "/status", HandleStatus);

            // 注册资源路由
            Register(HttpMethod.Get, "/assets/background", HandleBackground);
            Register(HttpMethod.Get, "/assets/icon", HandleIcon);
            Register(HttpMethod.Get, "/complete", HandleComplete);
        }

        private Task<HttpRouteResponse> HandleCallback(HttpListenerRequest request)
        {
            if (!request.IsLocal)
                return HttpRouteResponse.Forbidden.AsTask();

            var redirect = HttpRouteResponse.Redirect("/complete").AsTask();

            // 解析回调 URL 参数
            var parameterMap = new Dictionary<string, string>();
            var query = request.Url.Query;
            var queryIndex = query.IndexOf('?');
            if (queryIndex != -1 && query.Length > queryIndex)
                try
                {
                    var sq = query.Substring(queryIndex + 1).Split('&');
                    var splitChar = new[] { '=' };
                    foreach (var iq in sq)
                    {
                        var q = iq.Split(splitChar, 2);
                        if (q.Length == 2) parameterMap[q[0]] = q[1];
                    }
                }
                catch (Exception ex)
                {
                    _status = OAuthCompleteStatus.Failed("回调参数解析出错", ex);
                    return redirect;
                }

            _callbackParameters = parameterMap;

            // 读取回调内容
            if (request.HasEntityBody)
                try
                {
                    using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                    {
                        _callbackContent = reader.ReadToEnd();
                    }
                }
                catch (Exception ex)
                {
                    _status = OAuthCompleteStatus.Failed("读取回调内容出错", ex);
                    return redirect;
                }

            return redirect;
        }

        private Task<HttpRouteResponse> HandleStatus(HttpListenerRequest request)
        {
            if (_callbackParameters is null)
                return HttpRouteResponse.NotFound.AsTask();

            try
            {
                if (_status is null)
                {
                    _callbackParameters["Port"] = Port.ToString();
                    _status = _completeCallback(true, _callbackParameters, _callbackContent);
                }
                else if (!_status.success)
                {
                    ModBase.Log($"[OAuth] {_serviceName}: {_status.message}{"\r\n"}{_status.stacktrace}");
                    var pa = new Dictionary<string, string>();
                    pa["Port"] = Port.ToString();
                    _completeCallback(false, pa, _status.message);
                }
            }
            catch (Exception ex)
            {
                _status = OAuthCompleteStatus.Failed("处理回调出错", ex);
            }

            return HttpRouteResponse.Json(_status).AsTask();
        }

        private Task<HttpRouteResponse> HandleBackground(HttpListenerRequest request)
        {
            if (string.IsNullOrWhiteSpace(_picAddress))
                return Task.FromResult(HttpRouteResponse.NotFound);
            return HttpRouteResponse
                .Input(new FileStream(_picAddress, FileMode.Open, FileAccess.Read, FileShare.None, 16384, true))
                .AsTask();
        }

        private Task<HttpRouteResponse> HandleIcon(HttpListenerRequest request)
        {
            return HttpRouteResponse.Input(ModBase.GetResourceStream("Images/icon.ico")).AsTask();
        }

        private Task<HttpRouteResponse> HandleComplete(HttpListenerRequest request)
        {
            return HttpRouteResponse.Input(ModBase.GetResourceStream("Resources/oauth-complete.html"), "text/html")
                .AsTask();
        }
    }

    private static readonly object ChangeLock = new();
    private static string PicAddress;

    public static object BackgroundPicChangeCallback(string Pic)
    {
        lock (ChangeLock)
        {
            PicAddress = Pic;
            return true;
        }
    }

    public class OAuthCompleteStatus
    {
        public bool success { get; set; }
        public string username { get; set; }
        public string message { get; set; }
        public string stacktrace { get; set; }

        public static OAuthCompleteStatus Complete(string username)
        {
            return new OAuthCompleteStatus { success = true, username = username };
        }

        public static OAuthCompleteStatus Failed(string message, Exception ex = null)
        {
            var init = new OAuthCompleteStatus();
            return (init.success = false, init.message = message, init.stacktrace = ex?.ToString(), init).init;
        }
    }

    public delegate OAuthCompleteStatus? OAuthComplete(bool success, IDictionary<string, string> parameters,
        string content);

    public static bool StartOAuthWaitingCallback(string serviceName, string url, OAuthComplete completeCallback)
    {
        if (IsWebServerRunning(serviceName))
            return false;
        ModBase.RunInNewThread(() =>
            {
                var serverPort = 0;
                lock (_webServers)
                {
                    NaidCallbackServer? server;

                    string currentPicAddress;
                    lock (ChangeLock)
                    {
                        currentPicAddress = PicAddress;
                    }

                    server = new NaidCallbackServer(serviceName, completeCallback, currentPicAddress);
                    serverPort = server.Port;
                    ModBase.Log($"[OAuth] {serviceName}: 已开始监听 {server.Port} 端口，正在初始化路由");
                    // 开始响应请求
                    var webServiceName = $"oauth/{serviceName}";
                    if (DisposeWebServer(webServiceName)) ModBase.Log("[OAuth] 已关闭先前认证服务服务端");
                    StartWebServer(webServiceName, server);
                    ModBase.Log($"[OAuth] {serviceName}: 初始化完成，开始响应 HTTP 请求");
                }

                // 打开 OAuth URL
                ModBase.OpenWebsite(url.Replace("%r", $"http://localhost:{serverPort}/callback"));
            }, $"CallbackWebServerLoading/{serviceName}");
        return true;
    }

    public static void StartNaidAuthorize(Action? completeCallback = null)
    {
        Exception? resultEx = null;
        StartOAuthWaitingCallback("NatayarkID",
            $"https://account.naids.com/oauth2/authorize?response_type=code&client_id={ModSecret.NatayarkClientId}&redirect_uri=%r",
            (success, parameters, content) =>
            {
                OAuthCompleteStatus? status;
                if (!success)
                {
                    ModMain.MyMsgBox(content, IsWarn: true);
                    completeCallback?.Invoke();
                    return null;
                }


                var code = parameters["code"];

                try
                {
                    NatayarkProfileManager.GetNaidDataAsync(code, port: ushort.Parse(parameters["Port"])).Wait();
                }
                catch (AggregateException ex)
                {
                    resultEx = ex.InnerExceptions[0];
                }

                if (resultEx is null)
                    status = OAuthCompleteStatus.Complete(NatayarkProfileManager.NaidProfile.Username);
                else
                    status = OAuthCompleteStatus.Failed("获取用户信息失败，请尝试重新登录", resultEx);
                completeCallback?.Invoke();
                return status;
            });
    }

    #endregion

    #region 旧的 HTTP 服务端实现

    /* TODO ERROR: Skipped IfDirectiveTrivia
    #If False
    */ /* TODO ERROR: Skipped DisabledTextTrivia
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
                            Dim result = NatayarkProfileManager.GetNaidDataSync(OAuthCode)
                            If result Then
                                CurrentStatus.success = True
                                CurrentStatus.username = NatayarkProfileManager.NaidProfile.Username
                            Else
                                CurrentStatus.success = False
                                CurrentStatus.message = $"获取用户信息失败，请尝试重新登录"
                                CurrentStatus.stacktrace = NatayarkProfileManager.Exception.ToString()
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
    */ /* TODO ERROR: Skipped EndIfDirectiveTrivia
    #End If
    */

    #endregion
}