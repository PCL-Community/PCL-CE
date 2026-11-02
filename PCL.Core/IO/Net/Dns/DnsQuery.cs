using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Caching;
using System.Threading;
using System.Threading.Tasks;
using Ae.Dns.Client;
using Ae.Dns.Protocol;
using Ae.Dns.Protocol.Enums;
using Ae.Dns.Protocol.Records;
using PCL.Core.IO.Net.Http;
using PCL.Core.Logging;

namespace PCL.Core.IO.Net.Dns;

public class DnsQuery : IDisposable
{
    private const string ModuleName = "DoH query";
    public static DnsQuery Instance { get; } = new();

    private readonly DnsCachingClient _resolver;
    private readonly HttpClient[] _httpClients;

    private DnsQuery()
    {
        var proxyHandler = new HttpClientHandler()
        {
            Proxy = HttpProxyManager.Instance
        };
        // 使用Ae.Dns创建DoH客户端，支持多个DoH服务器
        _httpClients =
        [
            new HttpClient(proxyHandler)
            {
                BaseAddress = new Uri("https://doh.pub/")
            },
            new HttpClient(proxyHandler)
            {
                BaseAddress = new Uri("https://doh.pysio.online/")
            },
            new HttpClient(proxyHandler)
            {
                BaseAddress = new Uri("https://cloudflare-dns.com/")
            },
            new HttpClient(proxyHandler)
            {
                BaseAddress = new Uri("https://dns.google/")
            }
        ];
        _resolver = new DnsCachingClient(
            new WeightedDnsRacerClient(_httpClients.Length, _httpClients.Select(static x => new DnsHttpClient(x)).ToArray<IDnsClient>()),
            new MemoryCache("DoH Query Cache"));
    }

    public async Task<DnsMessage?> QueryAsync(string host, DnsQueryType qType, CancellationToken cts = default)
    {
        try
        {
            // DoH 独立超时：超时视为解析失败并回落系统 DNS，避免拖死上层下载
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cts);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));
            return await _resolver.Query(DnsQueryFactory.CreateQuery(host, qType), timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cts.IsCancellationRequested)
        {
            // 仅 DoH 超时（非调用方取消）：交由调用方按需回退
            LogWrapper.Warn(ModuleName, $"DoH query timeout for {host}, use system default DNS");
            return null;
        }
        catch (OperationCanceledException)
        {
            // 调用方主动取消：必须向上传播，不能被吞成"无结果"
            throw;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ModuleName, $"Failed to resolve DNS for {host}: {ex.Message}, use system default DNS");
        }

        return null;
    }

    public async Task<IPAddress[]?> QueryForIpAsync(string host, CancellationToken cts = default)
    {
        var queryResponse = await Task.WhenAll(
            [
                QueryAsync(host, DnsQueryType.A, cts),
                QueryAsync(host, DnsQueryType.AAAA, cts)
            ]
        );

        // 任一地址族查询失败/超时：整体回落系统 DNS，
        // 避免遗漏仅通过该地址族可达的主机
        if (queryResponse.Any(static x => x is null))
            return await _SystemResolveAsync(host, cts);

        var addresses = queryResponse
            .Where(static x => x is not null)
            .SelectMany(static x => x!.Answers)
            .Where(static x => x.Type is DnsQueryType.A or DnsQueryType.AAAA)
            .Select(static x => x.Resource as DnsIpAddressResource)
            .Where(static x => x is not null)
            .Select(static x => x!.IPAddress)
            .ToArray();

        // DoH 成功应答但无任何地址记录（域名不存在或无 A/AAAA）：
        // 回退系统 DNS 判定，避免把 NXDOMAIN 误报为"解析成功但无地址"
        if (addresses.Length == 0)
            return await _SystemResolveAsync(host, cts);

        return addresses;
    }

    private static async Task<IPAddress[]> _SystemResolveAsync(string host, CancellationToken ct)
    {
        try
        {
            return await System.Net.Dns.GetHostAddressesAsync(host, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ModuleName, $"System DNS resolution failed for {host}: {ex.Message}");
            return [];
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        foreach (var client in _httpClients)
        {
            client.Dispose();
        }
        _resolver.Dispose(); // 好像这个包的 Dispose 并没有做什么 lol
    }
}