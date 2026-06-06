using System;
using System.IO;
using System.Net;

namespace PCL;

/// <summary>
/// 本地 HTTP 服务器，为 Java Agent 提供皮肤 PNG 文件。
/// Java Agent 将 Mojang 皮肤 URL 替换为 http://127.0.0.1:{Port}/skin.png，
/// 本服务器直接返回缓存的皮肤文件。
/// </summary>
public static class ModSkinProxy
{
    private static HttpListener? _listener;
    public static int Port { get; private set; }

    public static void Start(string skinFilePath)
    {
        Stop();
        Port = new Random().Next(49152, 65535);
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
        _listener.Start();
        _listener.BeginGetContext(OnRequest, skinFilePath);
        ModBase.Log($"[SkinProxy] 已启动皮肤服务: http://127.0.0.1:{Port}/skin.png");
    }

    private static void OnRequest(IAsyncResult ar)
    {
        var skinPath = (string)ar.AsyncState!;
        HttpListenerContext ctx;
        try
        {
            ctx = _listener!.EndGetContext(ar);
        }
        catch (ArgumentException)
        {
            return; // Stale callback from a previous listener instance
        }
        catch (HttpListenerException)
        {
            return; // Listener was stopped
        }
        catch (ObjectDisposedException)
        {
            return; // Listener was disposed
        }

        try
        {
            var data = File.ReadAllBytes(skinPath);
            ctx.Response.ContentType = "image/png";
            ctx.Response.ContentLength64 = data.Length;
            ctx.Response.OutputStream.Write(data, 0, data.Length);
        }
        catch
        {
            ctx.Response.StatusCode = 404;
        }
        ctx.Response.Close();

        // Continue listening
        if (_listener?.IsListening == true)
            _listener.BeginGetContext(OnRequest, skinPath);
    }

    public static void Stop()
    {
        try { _listener?.Stop(); _listener?.Close(); } catch { }
        _listener = null;
    }
}
