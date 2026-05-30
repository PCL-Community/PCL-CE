using PCL.Core.App;
using PCL.Core.IO.Net.Http;


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

}