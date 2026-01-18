using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.Caching;
using System.Threading;
using System.Threading.Tasks;
using Ae.Dns.Client;
using Ae.Dns.Protocol;
using Ae.Dns.Protocol.Enums;
using Ae.Dns.Protocol.Records;
using PCL.Core.Logging;
using PCL.Core.Net.Dns;
using PCL.Core.Utils.Exts;

namespace PCL.Core.Net.Http.Client;

public class HostConnectionHandler
{
    public static HostConnectionHandler Instance { get; } = new();
    private const string ModuleName = "CustomHttpHandler";

    private readonly DnsCachingClient? _resolver;
    private readonly DnsQuery _dnsQuery = DnsQuery.Instance;

    private HostConnectionHandler()
    {
        var proxyHandler = new HttpClientHandler()
        {
            Proxy = HttpProxyManager.Instance
        };
        // 使用Ae.Dns创建DoH客户端，支持多个DoH服务器
        IDnsClient[] clients =
        [
            new DnsHttpClient(new HttpClient(proxyHandler)
            {
                BaseAddress = new Uri("https://doh.pub/")
            }),
            new DnsHttpClient(new HttpClient(proxyHandler)
            {
                BaseAddress = new Uri("https://doh.pysio.online/")
            }),
            new DnsHttpClient(new HttpClient(proxyHandler)
            {
                BaseAddress = new Uri("https://cloudflare-dns.com/")
            })
        ];

        _resolver = new DnsCachingClient(new DnsRacerClient(clients), new MemoryCache("DNS Query Cache"));
    }

    public async ValueTask<Stream> GetConnectionAsync(SocketsHttpConnectionContext context, CancellationToken cts)
    {
        ArgumentNullException.ThrowIfNull(_resolver, "_resolver != null");
        // 获取主机名和端口
        var host = context.DnsEndPoint.Host;
        var port = context.DnsEndPoint.Port;

        if (IPEndPoint.TryParse(host, out var remoteAddr)) // 是否为纯 IP 地址
        {
            return await _ConnectToAddressAsync(remoteAddr.Address, port, cts);
        }

        var addresses = await _dnsQuery.QueryForIpAsync(host, cts);

        if (addresses == null || addresses.Length == 0)
            throw new HttpRequestException($"No IP address for {host}");

        // 并行连接所有地址，返回第一个成功的连接
        var connectionTasks = addresses.Select(ip => _ConnectToAddressAsync(ip, port, cts)).ToArray();

        try
        {
            var completedTask = await connectionTasks.WhenAnySuccess().ConfigureAwait(false);
            var stream = await completedTask.ConfigureAwait(false);

            // 取消其他连接任务
            foreach (var task in connectionTasks.Where(task => task != completedTask))
            {
                _ = task.ContinueWith(t => {
                    if (t.IsCompletedSuccessfully)
                    {
                        t.Result.Dispose();
                    }
                    else if (t is { IsFaulted: true, Exception: not null })
                    {
                        LogWrapper.Debug(ModuleName, $"Connection to alternative address failed: {t.Exception.GetBaseException().Message}");
                    }
                }, cts);
            }

            LogWrapper.Debug(ModuleName, $"Success resolve DoH endpoint: {host} -> {stream.Socket.RemoteEndPoint}");
            return stream;
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, ModuleName, $"No address reachable for {host}");
            throw new HttpRequestException($"No address reachable: {host} -> {string.Join(", ", addresses.Select(x => x.ToString()))}", ex);
        }
    }

    private static async Task<NetworkStream> _ConnectToAddressAsync(IPAddress ip, int port, CancellationToken cts)
    {
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        socket.NoDelay = true;
        try
        {
            await socket.ConnectAsync(ip, port, cts);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            socket.Dispose();
            throw new OperationCanceledException($"Connection to {ip}:{port} was cancelled");
        }
        catch (Exception ex)
        {
            socket.Dispose();
            throw new HttpRequestException($"Failed to connect to {ip}:{port}", ex);
        }
    }
}