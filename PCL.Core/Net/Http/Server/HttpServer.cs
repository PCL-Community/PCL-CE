using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Net.Http.Server;

public abstract class HttpServer : IDisposable
{
    private readonly HttpListener _server = new();
    public readonly ushort Port;
    public readonly string[] Host;

    private Task? _handleLoop;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly Dictionary<(HttpMethod method, string path), Func<HttpListenerRequest, Task<HttpRouteResponse>>> _handlers = new();
    private bool _initialized = false;

    protected HttpServer(IPAddress[] listenAddr, ushort port = 0)
    {
        // Check parameters
        ArgumentNullException.ThrowIfNull(listenAddr);

        // Resolve port
        if (port == 0) port = (ushort)NetworkHelper.NewTcpPort();
        Port = port;

        // Resolve host
        if (listenAddr.Length == 0)
            listenAddr = [IPAddress.Loopback, IPAddress.IPv6Loopback];

        var hosts = new List<string>();
        foreach (var address in listenAddr)
        {
            _server.Prefixes.Add($"http://{address}:{port}/");
            hosts.Add(address.ToString());
        }
        Host = hosts.ToArray();
    }

    /// <summary>
    /// 初始化路由。子类应在此方法中调用 Register 方法注册路由。
    /// </summary>
    protected abstract void Init();

    /// <summary>
    /// 注册一个路由处理器。
    /// </summary>
    /// <param name="method">HTTP 方法</param>
    /// <param name="path">路由路径</param>
    /// <param name="handler">请求处理函数</param>
    protected void Register(HttpMethod method, string path, Func<HttpListenerRequest, Task<HttpRouteResponse>> handler)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(handler);

        _handlers[(method, path)] = handler;
    }

    /// <summary>
    /// 启动 HTTP 服务器。
    /// </summary>
    public void Start()
    {
        // 如果没有注册路由，调用 Init 初始化
        if (!_initialized && _handlers.Count == 0)
        {
            Init();
            _initialized = true;
        }

        _cancellationTokenSource = new CancellationTokenSource();
        _server.Start();
        _handleLoop = _handleRequest();
    }

    private async Task _handleRequest()
    {
        var cancellationToken = _cancellationTokenSource?.Token ?? CancellationToken.None;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var context = await _server.GetContextAsync();
                _ = Task.Run(async () => await _processRequest(context), cancellationToken);
            }
            catch (OperationCanceledException) { break; } // Cancellation
            catch (ObjectDisposedException) { break; } // Disposed
            catch (HttpListenerException) { break; } // Closed
        }
    }

    private async Task _processRequest(HttpListenerContext context)
    {
        try
        {
            var request = context.Request;
            var response = context.Response;
            var path = request.Url?.AbsolutePath ?? string.Empty;
            var method = new HttpMethod(request.HttpMethod);

            // 首先尝试精确匹配
            if (_handlers.TryGetValue((method, path), out var handler))
            {
                await _ExecuteHandler(handler, request, response);
                return;
            }

            // 如果没有精确匹配，尝试通配符匹配
            if (_handlers.TryGetValue((method, "*"), out var wildcardHandler))
            {
                await _ExecuteHandler(wildcardHandler, request, response);
                return;
            }

            // 没有找到匹配的路由
            response.StatusCode = (int)HttpStatusCode.NotFound;
        }
        finally
        {
            try
            {
                context.Response.Close();
            }
            catch
            {
                // Ignore errors when closing response
            }
        }
    }

    private static async Task _ExecuteHandler(Func<HttpListenerRequest, Task<HttpRouteResponse>> handler, HttpListenerRequest request, HttpListenerResponse response)
    {
        try
        {
            var routeResponse = await handler(request);
            routeResponse.Pour(response);
        }
        catch (Exception ex)
        {
            response.StatusCode = (int)HttpStatusCode.InternalServerError;
            response.ContentEncoding = System.Text.Encoding.UTF8;
            response.ContentType = "text/plain";
            var errorResponse =
                HttpRouteResponse.Text($"Internal Server Error:\n{ex}", "text/plain", System.Text.Encoding.UTF8);
            errorResponse.Pour(response);
        }
    }

    /// <summary>
    /// 停止 HTTP 服务器。
    /// </summary>
    public void Stop()
    {
        _cancellationTokenSource?.Cancel();
        _server.Stop();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        Stop();
        _server.Close();
        _cancellationTokenSource?.Dispose();
    }
}