using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Ae.Dns.Protocol.Enums;
using Ae.Dns.Protocol.Records;
using PCL.Core.Logging;
using PCL.Core.Net;
using PCL.Core.Net.Dns;
using PCL.Core.Utils;

namespace PCL.Core.Minecraft;

public static class ServerAddressResolver
{
    // Minecraft Java 默认端口
    private const int DefaultPort = 25565;
    // Happy Eyeballs 族间启动间隔（降低首包时延）
    private static readonly TimeSpan _HappyEyeballsStagger = TimeSpan.FromMilliseconds(250);
    // 单次 TCP 连接超时
    private static readonly TimeSpan _ConnectTimeout = TimeSpan.FromSeconds(2.5);

    // [IPv6]:port 或 [IPv6]
    private static readonly Regex _BracketedIpv6 =
        new(@"^\[(?<ip>.+?)\](?::(?<port>\d{1,5}))?$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // 纯端口（用于 host:port 末尾匹配）
    private static readonly Regex _TrailingPort =
        new(@":(?<port>\d{1,5})$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// 解析服务器地址并获取可达的 IP 与端口。
    /// </summary>
    /// <param name="address">服务器地址，支持 IP / IP:port / [IPv6]:port / domain / domain:port</param>
    /// <param name="cancelToken">取消令牌</param>
    /// <returns>包含 IP 与端口的元组</returns>
    /// <exception cref="ArgumentException">地址为空</exception>
    /// <exception cref="FormatException">端口无效或地址格式无效</exception>
    public static async Task<(string Ip, int Port)> GetReachableAddressAsync(string address, CancellationToken cancelToken = default)
    {
        if (string.IsNullOrWhiteSpace(address))
            throw new ArgumentException("服务器地址不能为空", nameof(address));

        // 规范化：去除 scheme、空白、尾随斜杠
        address = _NormalizeInput(address);

        // 1) 解析出 host/ip 与端口（若端口未提供则为 null）
        var (hostOrIp, portOpt) = _ParseHostAndPort(address);

        // 2) 显式端口 => 禁止 SRV，直接解析 IP 并尝试连接
        if (portOpt is { } explicitPort)
        {
            _ValidatePort(explicitPort);
            LogWrapper.Info($"使用显式端口，跳过 SRV：{hostOrIp}:{explicitPort}");
            var target = await _ResolveReachableAsync(hostOrIp, explicitPort, cancelToken).ConfigureAwait(false);
            if (target is not null)
                return target.Value;

            // 回退策略：无法连接则仍返回解析到的首个 IP
            var fallbackIp = await _ResolveFirstIpAsync(hostOrIp, cancelToken).ConfigureAwait(false);
            return (fallbackIp ?? hostOrIp, explicitPort);
        }

        // 3) 未指定端口
        // 3.1 纯 IP（IPv4/IPv6）=> 直接使用默认端口
        if (IPAddress.TryParse(hostOrIp, out _))
        {
            var target = await _ResolveReachableAsync(hostOrIp, DefaultPort, cancelToken).ConfigureAwait(false);
            if (target is not null)
                return target.Value;

            return (hostOrIp, DefaultPort);
        }

        // 3.2 域名 => 先尝试 SRV（_minecraft._tcp.），成功则按 SRV 顺序与加权尝试
        var idnHost = _ToAsciiIdn(hostOrIp);
        var srvOrdered = await _QuerySrvOrderedAsync(idnHost, cancelToken).ConfigureAwait(false);

        if (srvOrdered.Count > 0)
        {
            LogWrapper.Info($"SRV 记录可用（{srvOrdered.Count}）: _minecraft._tcp.{idnHost}");
            foreach (var srv in srvOrdered)
            {
                var targetHost = _TrimTrailingDot(srv.Target);
                var port = srv.Port;

                var reachable = await _ResolveReachableAsync(targetHost, port, cancelToken).ConfigureAwait(false);
                if (reachable is not null)
                {
                    LogWrapper.Info($"SRV 命中：{targetHost}:{port} -> {reachable.Value.Ip}:{port}");
                    return reachable.Value;
                }
            }

            // SRV 全部不可达则回退到 SRV 第一条的解析 IP 或域名默认端口
            var first = srvOrdered[0];
            var firstIp = await _ResolveFirstIpAsync(_TrimTrailingDot(first.Target), cancelToken).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(firstIp)) return (firstIp, first.Port);
        }
        else
        {
            LogWrapper.Info($"无 SRV 记录或查询失败，回退默认端口：{idnHost}:{DefaultPort}");
        }

        // 3.3 最终回退：域名 + 默认端口
        var ip = await _ResolveFirstIpAsync(idnHost, cancelToken).ConfigureAwait(false);
        return (ip ?? idnHost, DefaultPort);
    }

    // 规范化地址输入：去掉 scheme、空白、尾随 '/'
    private static string _NormalizeInput(string input)
    {
        var s = input.Trim();

        // 去掉任意 scheme:// 前缀（例如 http://、https://、minecraft://）
        var schemeIdx = s.IndexOf("://", StringComparison.Ordinal);
        if (schemeIdx > 0)
            s = s[(schemeIdx + 3)..];

        // 去掉尾随的 '/'
        while (s.EndsWith("/", StringComparison.Ordinal))
            s = s[..^1];

        return s;
    }

    private static void _ValidatePort(int port)
    {
        if (port is < 1 or > 65535)
            throw new FormatException($"无效的端口：{port}");
    }

    private static string _ToAsciiIdn(string host)
    {
        try
        {
            // 处理国际化域名
            var idn = new IdnMapping();
            // 允许末尾点号（FQDN）
            var h = _TrimTrailingDot(host);
            return idn.GetAscii(h) + (host.EndsWith(".", StringComparison.Ordinal) ? "." : "");
        }
        catch
        {
            // 无法转换时返回原值，交给后续 DNS 解析处理
            return host;
        }
    }

    private static string _TrimTrailingDot(string host)
        => host.EndsWith(".", StringComparison.Ordinal) ? host[..^1] : host;

    private static (string HostOrIp, int? Port) _ParseHostAndPort(string input)
    {
        // 1) [IPv6] 或 [IPv6]:port
        var m = _BracketedIpv6.Match(input);
        if (m.Success)
        {
            var ip = m.Groups["ip"].Value;
            if (!IPAddress.TryParse(ip, out _))
                throw new FormatException("无效的 IPv6 地址格式");
            var portGroup = m.Groups["port"];
            if (portGroup.Success)
            {
                var port = int.Parse(portGroup.Value, CultureInfo.InvariantCulture);
                _ValidatePort(port);
                return (ip, port);
            }
            return (ip, null);
        }

        // 2) 试图解析为纯 IP（IPv4 或 IPv6 无端口）
        if (IPAddress.TryParse(input, out _))
            return (input, null);

        // 3) host:port（仅在末尾存在且为纯数字端口时成立）
        var pm = _TrailingPort.Match(input);
        if (pm.Success)
        {
            // 防止误把 IPv6 当作 host:port（IPv6 必须用中括号携带端口）
            // 此处 input 中若包含多个 ':' 则极可能是 IPv6 而非 host:port
            var colonCount = input.Count(c => c == ':');
            if (colonCount == 1)
            {
                var port = int.Parse(pm.Groups["port"].Value, CultureInfo.InvariantCulture);
                _ValidatePort(port);
                var host = input[..^pm.Value.Length];
                if (string.IsNullOrWhiteSpace(host))
                    throw new FormatException("无效的主机名");
                return (host, port);
            }
        }

        // 4) 其余情况按“域名（无端口）”处理
        return (input, null);
    }

    // ===== SRV 查询与排序（RFC 2782） =====

    private sealed record SrvRecord(int Priority, int Weight, int Port, string Target);

    private static async Task<List<SrvRecord>> _QuerySrvOrderedAsync(string domain, CancellationToken ct)
    {
        try
        {
            var name = $"_minecraft._tcp.{_TrimTrailingDot(domain)}";
            LogWrapper.Info($"尝试 SRV 查询：{name}");

            // NDnsQuery.GetSrvRecords 返回 string 列表，为兼容不同实现，这里进行鲁棒解析
            var raw = await DnsQuery.Instance.QueryAsync(name, DnsQueryType.SRV, ct);
            if (raw == null || raw.Answers.Count == 0) return [];
            List<SrvRecord> parsed = [];
            foreach (var answer in raw.Answers)
            {
                if (answer.Resource is not DnsUnknownResource dnsRaw) return [];
                var srcRecord = new DnsSrvResource();
                var offset = 0;
                srcRecord.ReadBytes(dnsRaw.Raw, ref offset, dnsRaw.Raw.Length);
                parsed.Add(new SrvRecord(srcRecord.Priority, srcRecord.Weight, srcRecord.Port, srcRecord.Target));
            }

            // 过滤 target 为 "."（表示服务不可用）
            parsed.RemoveAll(p => p.Target == ".");

            if (parsed.Count == 0)
                return [];

            // RFC 2782：按 priority 升序；相同 priority 内按权重加权随机选择顺序
            var ordered = new List<SrvRecord>(parsed.Count);
            foreach (var group in parsed.GroupBy(p => p.Priority).OrderBy(g => g.Key))
            {
                var pool = group.ToList();
                while (pool.Count > 0)
                {
                    var next = _PopByWeight(pool);
                    ordered.Add(next);
                }
            }
            return ordered;
        }
        catch (SocketException ex)
        {
            LogWrapper.Warn(ex, "SRV 查询失败（网络错误）");
            return [];
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "SRV 查询异常");
            return [];
        }
    }

    private static SrvRecord _PopByWeight(List<SrvRecord> pool)
    {
        // RFC 2782 加权随机：在组内以 weight 为权重抽取
        var total = pool.Sum(p => p.Weight);
        if (total <= 0)
        {
            // 无权重时等概率
            var i = RandomUtils.NextInt(0, pool.Count - 1);
            var chosen = pool[i];
            pool.RemoveAt(i);
            return chosen;
        }

        var r = RandomUtils.NextInt(1, total); // (1..total)
        var sum = 0;
        for (var i = 0; i < pool.Count; i++)
        {
            sum += pool[i].Weight;
            if (sum >= r)
            {
                var chosen = pool[i];
                pool.RemoveAt(i);
                return chosen;
            }
        }

        // 理论不可达，兜底返回末尾
        var last = pool[^1];
        pool.RemoveAt(pool.Count - 1);
        return last;
    }

    // ===== DNS 与连接可达性 =====

    private static async Task<(string Ip, int Port)?> _ResolveReachableAsync(string hostOrIp, int port, CancellationToken ct)
    {
        try
        {
            // 已是字面量 IP
            if (IPAddress.TryParse(hostOrIp, out var ipLiteral))
            {
                var result = await _ConnectOneAsync(ipLiteral, port, ct).ConfigureAwait(false);
                if (result.ok) return (ipLiteral.ToString(), port);
                return null;
            }

            var addresses = await Dns.GetHostAddressesAsync(_TrimTrailingDot(hostOrIp), ct).ConfigureAwait(false);
            if (addresses.Length == 0)
                return null;

            // Happy Eyeballs：分组（IPv6、IPv4），按组分阶段并行连接，取首个成功
            var v6 = addresses.Where(a => a.AddressFamily == AddressFamily.InterNetworkV6).ToArray();
            var v4 = addresses.Where(a => a.AddressFamily == AddressFamily.InterNetwork).ToArray();

            // 第一阶段：IPv6
            var winner = await _ConnectAnyAsync(v6, port, TimeSpan.Zero, ct).ConfigureAwait(false);
            if (winner is not null)
                return (winner, port);

            // 第二阶段：IPv4（稍作延迟以避免同时轰炸）
            winner = await _ConnectAnyAsync(v4, port, _HappyEyeballsStagger, ct).ConfigureAwait(false);
            if (winner is not null)
                return (winner, port);

            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogWrapper.Warn(ex, $"解析或连接失败：{hostOrIp}:{port}");
            return null;
        }
    }

    private static async Task<string?> _ResolveFirstIpAsync(string hostOrIp, CancellationToken ct)
    {
        try
        {
            if (IPAddress.TryParse(hostOrIp, out var ip))
                return ip.ToString();

            var addresses = await Dns.GetHostAddressesAsync(_TrimTrailingDot(hostOrIp), ct).ConfigureAwait(false);
            var chosen = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetworkV6)
                         ?? addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
            return chosen?.ToString();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogWrapper.Warn(ex, $"DNS 解析失败：{hostOrIp}");
            return null;
        }
    }

    private static async Task<string?> _ConnectAnyAsync(IReadOnlyList<IPAddress> addrs, int port, TimeSpan delay, CancellationToken ct)
    {
        if (addrs.Count == 0) return null;
        if (delay > TimeSpan.Zero)
            await Task.Delay(delay, ct).ConfigureAwait(false);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var tasks = new List<Task<(bool ok, string ip)>>(addrs.Count);
        tasks.AddRange(addrs.Select(ip => _ConnectOneAsync(ip, port, cts.Token)));

        while (tasks.Count > 0)
        {
            var done = await Task.WhenAny(tasks).ConfigureAwait(false);
            tasks.Remove(done);
            var (ok, ip) = await done.ConfigureAwait(false);
            if (!ok) continue;
            // 取消其余连接尝试
            try { await cts.CancelAsync().ConfigureAwait(false); } catch { /* ignore */ }
            return ip;
        }

        return null;
    }

    private static async Task<(bool ok, string ip)> _ConnectOneAsync(IPAddress ip, int port, CancellationToken ct)
    {
        try
        {
            using var sock = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            sock.NoDelay = true;

            using var timeoutCts = new CancellationTokenSource(_ConnectTimeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
#if NET8_0_OR_GREATER
            await sock.ConnectAsync(new IPEndPoint(ip, port), linked.Token).ConfigureAwait(false);
#else
            await sock.ConnectAsync(new IPEndPoint(ip, port)).WaitAsync(ConnectTimeout, linked.Token).ConfigureAwait(false);
#endif
            return (true, ip.ToString());
        }
        catch
        {
            return (false, ip.ToString());
        }
    }
}
