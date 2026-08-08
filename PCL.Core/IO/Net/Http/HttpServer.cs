using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.IO.Net.Http;

public abstract class HttpServer : IDisposable
{
    private readonly HttpListener _server = new();
    public readonly ushort Port;
    public readonly string[] Host;

    private Task? _handleLoop;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly Dictionary<(HttpMethod method, string path), Func<HttpListenerRequest, Task<HttpRouteResponse>>> _handlers = new();
    private readonly Dictionary<(HttpMethod method, string path), Func<HttpListenerRequest, IReadOnlyDictionary<string, string>, Task<HttpRouteResponse>>> _templateHandlers = new();
    private bool _initialized = false;
    private bool _disposed = false;

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
            // IPv6 地址在 URI host 中必须用方括号包裹（如 [::1]），否则 HttpListener.AddPrefix 抛
            // "Only Uri prefixes with a valid hostname are supported"
            var host = address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
                ? $"[{address}]"
                : address.ToString();
            _server.Prefixes.Add($"http://{host}:{port}/");
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
    /// 注册一个带路径参数的路由处理器。
    /// </summary>
    /// <param name="method">HTTP 方法</param>
    /// <param name="pathTemplate">路由路径模板，<c>{xxx}</c> 段为路径参数，匹配任意单个路径段并捕获其值</param>
    /// <param name="handler">请求处理函数，第二个参数为捕获的路径参数集合</param>
    protected void RegisterWithParams(HttpMethod method, string pathTemplate, Func<HttpListenerRequest, IReadOnlyDictionary<string, string>, Task<HttpRouteResponse>> handler)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(pathTemplate);
        ArgumentNullException.ThrowIfNull(handler);

        _templateHandlers[(method, pathTemplate)] = handler;
    }

    /// <summary>
    /// 启动 HTTP 服务器。
    /// </summary>
    public void Start()
    {
        // 若未注册任何路由（精确或模板），调用 Init 初始化。检查两者确保子类若在 Start 前
        // 通过 Register 注册了精确路由、而 Init 里只注册模板路由时，模板路由也不会被跳过。
        if (!_initialized && _handlers.Count == 0 && _templateHandlers.Count == 0)
        {
            Init();
            _initialized = true;
        }

        _cancellationTokenSource = new CancellationTokenSource();
        _server.Start();
        _handleLoop = _HandleRequestAsync();
    }

    private async Task _HandleRequestAsync()
    {
        var cancellationToken = _cancellationTokenSource?.Token ?? CancellationToken.None;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var context = await _server.GetContextAsync();
                _ = Task.Run(async () => await _ProcessRequestAsync(context), cancellationToken);
            }
            catch (OperationCanceledException) { break; } // Cancellation
            catch (ObjectDisposedException) { break; } // Disposed
            catch (HttpListenerException) { break; } // Closed
        }
    }

    private async Task _ProcessRequestAsync(HttpListenerContext context)
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
                await _ExecuteHandlerAsync(handler, request, response);
                return;
            }

            // 其次尝试模板路由匹配：{param} 段匹配任意单个路径段并捕获其值
            foreach (var ((templateMethod, templatePath), templateHandler) in _templateHandlers)
            {
                if (templateMethod != method) continue;
                if (!_TryMatchTemplate(templatePath, path, out var parameters)) continue;
                await _ExecuteHandlerAsync(templateHandler, parameters, request, response);
                return;
            }

            // 如果没有精确匹配，尝试通配符匹配
            if (_handlers.TryGetValue((method, "*"), out var wildcardHandler))
            {
                await _ExecuteHandlerAsync(wildcardHandler, request, response);
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

    private static async Task _ExecuteHandlerAsync(Func<HttpListenerRequest, Task<HttpRouteResponse>> handler, HttpListenerRequest request, HttpListenerResponse response)
    {
        await _ExecuteCoreAsync(() => handler(request), response);
    }

    private static async Task _ExecuteHandlerAsync(Func<HttpListenerRequest, IReadOnlyDictionary<string, string>, Task<HttpRouteResponse>> handler, IReadOnlyDictionary<string, string> parameters, HttpListenerRequest request, HttpListenerResponse response)
    {
        await _ExecuteCoreAsync(() => handler(request, parameters), response);
    }

    private static async Task _ExecuteCoreAsync(Func<Task<HttpRouteResponse>> invoke, HttpListenerResponse response)
    {
        try
        {
            var routeResponse = await invoke();
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
    /// 尝试将请求路径与路径模板匹配。<c>{param}</c> 段匹配任意单个路径段并捕获其值，
    /// 非参数段必须与请求段完全一致（区分大小写），且两边的路径段数必须相等。
    /// 路径以 <c>/</c> 开头，模板与请求使用相同的 <c>/</c> 分段方式，保证首尾空段互相抵消。
    /// </summary>
    private static bool _TryMatchTemplate(string template, string requestPath, out Dictionary<string, string> parameters)
    {
        parameters = new Dictionary<string, string>();
        var templateSegments = template.Split('/');
        var requestSegments = requestPath.Split('/');
        if (templateSegments.Length != requestSegments.Length) return false;

        for (var i = 0; i < templateSegments.Length; i++)
        {
            var templateSegment = templateSegments[i];
            if (templateSegment.Length > 2 && templateSegment[0] == '{' && templateSegment[^1] == '}')
                parameters[templateSegment[1..^1]] = requestSegments[i];
            else if (!string.Equals(templateSegment, requestSegments[i], StringComparison.Ordinal))
                return false;
        }
        return true;
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
        if (_disposed) return; // 幂等：重复 Dispose 安全（进程退出事件与启动流程兜底可能竞态调用）
        _disposed = true;
        Stop();
        _server.Close();
        _cancellationTokenSource?.Dispose();
    }
}
