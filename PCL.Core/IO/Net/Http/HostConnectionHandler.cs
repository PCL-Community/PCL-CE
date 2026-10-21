using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.IO.Net.Dns;
using PCL.Core.Logging;
using PCL.Core.Utils.Exts;

namespace PCL.Core.IO.Net.Http;

public class HostConnectionHandler
{
    public static HostConnectionHandler Instance { get; } = new();
    private const string ModuleName = "HostConnectionHandler";

    private readonly DnsQuery _dnsQuery = DnsQuery.Instance;
    private static readonly TimeSpan _CacheDuration = TimeSpan.FromMinutes(10); // 10 分钟缓存(RFC 建议值)
    private static readonly TimeSpan _FailureDuration = TimeSpan.FromMinutes(2); // 连接失败地址的降权时长

    /// <summary>
    ///     单次连接最多尝试的候选地址数。
    ///     仅在少数地址上并发尝试时，一旦它们都不可用（例如被污染的 DNS 结果、无法访问的 IPv6），
    ///     整次连接就会失败，表现为“下载出错但重试后又能成功”。因此这里保留更多候选地址作为兜底。
    /// </summary>
    private const int MaxConnectAttempts = 6;

    // 缓存结构: (host, port) -> (IPAddress -> LastSuccessUtc)
    private readonly ConcurrentDictionary<(string host, int port), ConcurrentDictionary<IPAddress, DateTime>>
        _connectionCache = new();

    // 失败结构: (host, port) -> (IPAddress -> LastFailureUtc)，用于把连不通的地址排到末尾
    private readonly ConcurrentDictionary<(string host, int port), ConcurrentDictionary<IPAddress, DateTime>>
        _failureCache = new();

    public async ValueTask<Stream> GetConnectionAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var host = context.DnsEndPoint.Host;
        var port = context.DnsEndPoint.Port;
        var now = DateTime.UtcNow;

        // 代理地址或者直连 IP 直接返回结果
        if (IPAddress.TryParse(host, out var directIp))
        {
            return await _ConnectToAddressAsync(directIp, port, cancellationToken).ConfigureAwait(false);
        }

        var addresses = await _dnsQuery.QueryForIpAsync(host, cancellationToken).ConfigureAwait(false);
        if (addresses is null || addresses.Length == 0)
        {
            throw new HttpRequestException($"DNS resolution failed for {host}");
        }

        // 对待连接地址进行排序 (缓存成功优先 + 失败地址降权 + IPv6 优先)
        var sortedAddresses = _SortAddresses(host, port, addresses, now);

        // Happy Eyeballs 连接逻辑，优先 IPv6 稍后
        var connectionTasks = new List<Task<NetworkStream>>(MaxConnectAttempts);
        var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var connectCancellationToken = cancellationSource.Token;

        try
        {
            for (int i = 0; i < sortedAddresses.Length; i++)
            {
                var address = sortedAddresses[i];
                var delayMs = i * (address.AddressFamily.Equals(AddressFamily.InterNetwork) ? 150 : 80); // 偏向使用 v6

                connectionTasks.Add(_DelayedConnectAsync(host, port, address, delayMs, connectCancellationToken));
            }

            // 等待首个成功连接
            var winner = await connectionTasks
                .ToArray()
                .WhenAnySuccessAsync()
                .ConfigureAwait(false);
            var stream = await winner.ConfigureAwait(false);

            // 更新缓存 + 记录成功
            var remoteIp = ((IPEndPoint)stream.Socket.RemoteEndPoint!).Address;
            _UpdateConnectionCache(host, port, remoteIp, now);
            LogWrapper.Debug(ModuleName, $"Connected to {host} via {remoteIp}");

            // 取消其他连接
            // ReSharper disable once MethodHasAsyncOverload
            cancellationSource.Cancel();
            _ = _CleanupUnusedConnectionsAsync(connectionTasks, winner).ConfigureAwait(false);

            return stream;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // 清理所有连接
            // ReSharper disable once MethodHasAsyncOverload
            cancellationSource.Cancel();
            _ = _CleanupUnusedConnectionsAsync(connectionTasks, null).ConfigureAwait(false);

            // 连接失败属于可重试的临时性网络问题，调用方（HTTP 重试 / 下载换源）通常能自行恢复，
            // 因此只记录警告，避免每次瞬时失败都向用户弹出错误提示
            LogWrapper.Warn(ex, ModuleName, $"All connection attempts failed for {host}");
            throw new HttpRequestException($"Connection failed for {host}", ex);
        }
        finally
        {
            cancellationSource.Dispose();
        }
    }

    private IPAddress[] _SortAddresses(string host, int port, IPAddress[] addresses, DateTime now)
    {
        var cacheKey = (host, port);
        var addressCache = _connectionCache.GetValueOrDefault(cacheKey);
        var failureCache = _failureCache.GetValueOrDefault(cacheKey);

        var cachedAddresses = new List<IPAddress>();
        var uncachedAddresses = new List<IPAddress>();
        var failedAddresses = new List<IPAddress>();

        foreach (var ip in addresses)
        {
            if (addressCache is not null &&
                addressCache.TryGetValue(ip, out var successTime) &&
                now - successTime <= _CacheDuration)
            {
                cachedAddresses.Add(ip);
                continue;
            }

            // 最近连不通的地址放到最后，仅在其余地址不足时才使用
            if (failureCache is not null &&
                failureCache.TryGetValue(ip, out var failureTime) &&
                now - failureTime <= _FailureDuration)
                failedAddresses.Add(ip);
            else
                uncachedAddresses.Add(ip);
        }

        // 缓存地址: 按成功时间倒序 (最近成功的优先)
        cachedAddresses.Sort((a, b) =>
            addressCache![b].CompareTo(addressCache[a]));

        // 非缓存地址: IPv6 优先 + 保持原始顺序
        var ipv6List = uncachedAddresses
            .Where(static ip => ip.AddressFamily == AddressFamily.InterNetworkV6)
            .OrderBy(ip => Array.IndexOf(addresses, ip))
            .ToList();

        var ipv4List = uncachedAddresses
            .Where(static ip => ip.AddressFamily == AddressFamily.InterNetwork)
            .OrderBy(ip => Array.IndexOf(addresses, ip))
            .ToList();

        // 交错合并，确保有一个 v6 和一个 v4
        var sortedUncached = new List<IPAddress>();
        int i = 0, j = 0;
        while (i < ipv6List.Count || j < ipv4List.Count)
        {
            if (i < ipv6List.Count)
                sortedUncached.Add(ipv6List[i++]);
            if (j < ipv4List.Count)
                sortedUncached.Add(ipv4List[j++]);
        }

        return cachedAddresses
            .Concat(sortedUncached)
            .Concat(failedAddresses)
            .Take(MaxConnectAttempts)
            .ToArray();
    }

    private void _UpdateConnectionCache(string host, int port, IPAddress ip, DateTime now)
    {
        var cacheKey = (host, port);
        var addressCache = _connectionCache.GetOrAdd(cacheKey, static _ =>
            new ConcurrentDictionary<IPAddress, DateTime>());

        // 移除过期条目 (懒清理)
        foreach (var entry in addressCache)
        {
            if (now - entry.Value > _CacheDuration)
            {
                addressCache.TryRemove(entry.Key, out _);
            }
        }

        // 更新当前IP
        addressCache[ip] = now;
    }

    private void _UpdateFailureCache(string host, int port, IPAddress ip, DateTime now)
    {
        var cacheKey = (host, port);
        var failureCache = _failureCache.GetOrAdd(cacheKey, static _ =>
            new ConcurrentDictionary<IPAddress, DateTime>());

        // 移除过期条目 (懒清理)
        foreach (var entry in failureCache)
        {
            if (now - entry.Value > _FailureDuration)
            {
                failureCache.TryRemove(entry.Key, out _);
            }
        }

        failureCache[ip] = now;
    }

    private async Task<NetworkStream> _DelayedConnectAsync(string host, int port, IPAddress ip, int delay,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }

        try
        {
            var stream = await _ConnectToAddressAsync(ip, port, cancellationToken).ConfigureAwait(false);
            _UpdateConnectionCache(host, port, ip, DateTime.UtcNow);
            return stream;
        }
        catch (OperationCanceledException)
        {
            // 已找到可用连接，其余连接被主动取消，不计入失败
            throw;
        }
        catch
        {
            _UpdateFailureCache(host, port, ip, DateTime.UtcNow);
            throw;
        }
    }

    private static async Task<NetworkStream> _ConnectToAddressAsync(IPAddress ip, int port, CancellationToken cancellationToken)
    {
        var socket = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true
        };

        try
        {
            await socket.ConnectAsync(ip, port, cancellationToken).ConfigureAwait(false);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            socket.Dispose();
            throw new HttpRequestException($"Connection to {ip}:{port} failed", ex);
        }
        catch
        {
            // 连接被取消时同样需要释放套接字（候选地址更多后取消会更频繁）
            socket.Dispose();
            throw;
        }
    }

    private static async Task _CleanupUnusedConnectionsAsync(
        List<Task<NetworkStream>> allTasks,
        Task<NetworkStream>? winnerTask)
    {
        foreach (var task in allTasks.Where(t => t != winnerTask))
        {
            try
            {
                if (task.IsCompletedSuccessfully)
                {
                    var stream = await task.ConfigureAwait(false);
                    await stream.DisposeAsync().ConfigureAwait(false);
                }
                else if (task.IsCompleted)
                {
                    _ = task.Exception;
                }
                else
                {
                    _ = task.ContinueWith(static t =>
                    {
                        if (t.IsCompletedSuccessfully)
                        {
                            t.Result.Dispose();
                        }
                        else
                        {
                            _ = t.Exception;
                        }
                    }, TaskScheduler.Default);
                }
            }
            catch (Exception ex)
            {
                LogWrapper.Warn(ex, ModuleName, "Dispose connection with an error");
            }
        }
    }
}