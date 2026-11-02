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
            }
        ];
        // 权重竞速：候选集取权重前 2，权重随成功/失败动态涨落，
        // 被阻断或劣质的上游会自动跌出候选集；5 秒超时由竞速器内建
        _resolver = new DnsCachingClient(
            new WeightedDnsRacerClient(2, TimeSpan.FromSeconds(5),
                _httpClients.Select(static x => new DnsHttpClient(x)).ToArray<IDnsClient>()),
            new MemoryCache("DoH Query Cache"));
    }

    public async Task<DnsMessage?> QueryAsync(string host, DnsQueryType qType, CancellationToken cts = default)
    {
        try
        {
            // 竞速超时由 WeightedDnsRacerClient 内建管理，超时已对
            // 悬挂上游扣分并向此抛出取消异常
            return await _resolver.Query(DnsQueryFactory.CreateQuery(host, qType), cts);
        }
        catch (OperationCanceledException) when (!cts.IsCancellationRequested)
        {
            // 竞速超时，回落系统 DNS。系统DNS可能返回污染结果，作为可能可用的方式
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

        var addresses = queryResponse
            .Where(static x => x is not null)
            .SelectMany(static x => x!.Answers)
            .Where(static x => x.Type is DnsQueryType.A or DnsQueryType.AAAA)
            .Select(static x => x.Resource as DnsIpAddressResource)
            .Where(static x => x is not null)
            .Select(static x => x!.IPAddress)
            .ToArray();

        // 任一地址族失败/超时，或应答中无任何地址记录时回落系统 DNS；
        // 系统结果与 DoH 已成功部分合并去重，避免丢失可用地址族
        if (queryResponse.Any(static x => x is null) || addresses.Length == 0)
        {
            var system = await _SystemResolveAsync(host, cts);
            return addresses.Length == 0
                ? system
                : addresses.Union(system).ToArray();
        }

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