using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Link.Natayark;

/// <summary>
///     UPnP / NAT-PMP 端口映射服务。
///     优先使用 Windows COM NATUPnP API，失败时回退到 SSDP + HTTP 手动实现。
/// </summary>
public sealed class UpnpService : IDisposable
{
    private readonly List<PortMapping> _mappings = [];
    private bool _disposed;

    /// <summary>
    ///     尝试发现网关并添加端口映射。
    /// </summary>
    /// <param name="localAddress">本地网卡 IP（避免绑定到虚拟网卡）</param>
    /// <param name="internalPort">内网端口</param>
    /// <param name="externalPort">外网端口</param>
    /// <param name="protocol">TCP 或 UDP</param>
    /// <param name="description">映射描述</param>
    /// <param name="discoverTimeout">发现超时（毫秒）</param>
    /// <returns>公网地址 + 端口</returns>
    public async Task<UpnpResult?> MapAsync(
        IPAddress localAddress,
        int internalPort,
        int externalPort,
        string protocol,
        string description,
        CancellationToken ct = default,
        int discoverTimeout = 3000)
    {
        _disposed = false;

        // 获取公网 IP
        var publicIp = await GetPublicIpAsync(ct);
        if (publicIp is null)
            return null;

        // 优先尝试 Windows COM API
        var result = await TryComMapAsync(localAddress, internalPort, externalPort, protocol, description, ct);
        if (result is not null)
        {
            _mappings.Add(new PortMapping(externalPort, protocol));
            return new UpnpResult(publicIp, externalPort);
        }

        // 回退：SSDP 发现 + SOAP 请求
        result = await TrySsdpMapAsync(localAddress, internalPort, externalPort, protocol, description,
            discoverTimeout, ct);
        if (result is not null)
        {
            _mappings.Add(new PortMapping(externalPort, protocol));
            return new UpnpResult(publicIp, externalPort);
        }

        return null;
    }

    private static async Task<IPAddress?> GetPublicIpAsync(CancellationToken ct)
    {
        var urls = new[]
        {
            "https://api.ipify.org",
            "https://checkip.amazonaws.com",
            "https://ifconfig.me/ip",
            "https://icanhazip.com"
        };

        using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        foreach (var url in urls)
        {
            try
            {
                var response = await http.GetStringAsync(url, ct);
                var ipStr = response.Trim();
                if (IPAddress.TryParse(ipStr, out var ip) && !IPAddress.IsLoopback(ip))
                    return ip;
            }
            catch
            {
                // try next
            }
        }

        return null;
    }

    /// <summary>
    ///     使用 Windows COM NATUPnP API 添加映射。
    /// </summary>
    private static async Task<bool?> TryComMapAsync(
        IPAddress local,
        int internalPort,
        int externalPort,
        string protocol,
        string description,
        CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows())
            return null;

        try
        {
            var natType = Type.GetTypeFromProgID("HNetCfg.NATUPnP");
            if (natType is null) return null;

            var nat = Activator.CreateInstance(natType);
            if (nat is null) return null;

            // 获取 StaticPortMappingCollection
            var scProp = natType.InvokeMember("StaticPortMappingCollection",
                System.Reflection.BindingFlags.GetProperty, null, nat, null);
            if (scProp is null) return null;

            var scType = scProp.GetType();
            var addArgs = new object[] { externalPort, protocol.ToUpper(), internalPort, local.ToString(), true, description };
            scType.InvokeMember("Add",
                System.Reflection.BindingFlags.InvokeMethod, null, scProp, addArgs);

            await Task.CompletedTask;
            return true;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     通过 SSDP 发现 + SOAP 请求手动添加映射。
    /// </summary>
    private static async Task<bool?> TrySsdpMapAsync(
        IPAddress local,
        int internalPort,
        int externalPort,
        string protocol,
        string description,
        int timeoutMs,
        CancellationToken ct)
    {
        try
        {
            var gatewayLocation = await DiscoverGatewayAsync(timeoutMs, ct);
            if (string.IsNullOrEmpty(gatewayLocation))
                return null;

            // 解析网关 IP
            var uri = new Uri(gatewayLocation);
            var soapRequest = BuildSoapAddMapping(internalPort, externalPort, protocol, description,
                local.ToString(), uri);

            var data = Encoding.UTF8.GetBytes(soapRequest);
            var endpoint = new IPEndPoint(IPAddress.Parse(uri.Host), uri.Port);
            // Use synchronous Send on a background thread since UdpClient.SendAsync
            // does not support CancellationToken in all target frameworks
            await Task.Run(() =>
            {
                using var udp = new UdpClient();
                udp.Send(data, data.Length, endpoint);
            }, ct);

            await Task.Delay(500, ct);
            return true;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string?> DiscoverGatewayAsync(int timeoutMs, CancellationToken ct)
    {
        var searchMsg = "M-SEARCH * HTTP/1.1\r\n" +
                        "HOST: 239.255.255.250:1900\r\n" +
                        "MAN: \"ssdp:discover\"\r\n" +
                        "MX: 2\r\n" +
                        "ST: urn:schemas-upnp-org:device:InternetGatewayDevice:1\r\n" +
                        "\r\n";

        var endpoint = new IPEndPoint(IPAddress.Parse("239.255.255.250"), 1900);
        using var receiveCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        receiveCts.CancelAfter(timeoutMs);

        var result = await Task.Run(() =>
        {
            using var udp = new UdpClient();
            udp.EnableBroadcast = true;
            udp.Send(Encoding.ASCII.GetBytes(searchMsg), searchMsg.Length, endpoint);

            try
            {
                using var timeoutCts = new CancellationTokenSource(timeoutMs);
                var recvTask = udp.ReceiveAsync(timeoutCts.Token).AsTask();
                recvTask.Wait(timeoutMs);
                return (recvTask.Result.Buffer, true);
            }
            catch (AggregateException)
            {
                return (Array.Empty<byte>(), false);
            }
            catch (OperationCanceledException)
            {
                return (Array.Empty<byte>(), false);
            }
        }, ct);

        if (!result.Item2 || result.Item1.Length == 0) return null;
        var response = Encoding.ASCII.GetString(result.Item1);

        // 解析 LOCATION header
        foreach (var line in response.Split("\r\n"))
        {
            if (line.StartsWith("LOCATION:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Location:", StringComparison.OrdinalIgnoreCase))
                return line.Split(':', 2)[1].Trim();
        }

        return null;
    }

    private static string BuildSoapAddMapping(
        int internalPort, int externalPort, string protocol, string description,
        string internalClient, Uri controlUrl)
    {
        var host = $"{controlUrl.Scheme}://{controlUrl.Host}:{controlUrl.Port}";
        return $"POST {controlUrl.PathAndQuery} HTTP/1.1\r\n" +
               $"HOST: {controlUrl.Host}:{controlUrl.Port}\r\n" +
               "CONTENT-TYPE: text/xml; charset=\"utf-8\"\r\n" +
               "SOAPACTION: \"urn:schemas-upnp-org:service:WANIPConnection:1#AddPortMapping\"\r\n" +
               "\r\n" +
               "<?xml version=\"1.0\"?>\r\n" +
               "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" " +
               "s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">\r\n" +
               "<s:Body>\r\n" +
               "<u:AddPortMapping xmlns:u=\"urn:schemas-upnp-org:service:WANIPConnection:1\">\r\n" +
               $"<NewRemoteHost></NewRemoteHost>\r\n" +
               $"<NewExternalPort>{externalPort}</NewExternalPort>\r\n" +
               $"<NewProtocol>{protocol.ToUpper()}</NewProtocol>\r\n" +
               $"<NewInternalPort>{internalPort}</NewInternalPort>\r\n" +
               $"<NewInternalClient>{internalClient}</NewInternalClient>\r\n" +
               "<NewEnabled>1</NewEnabled>\r\n" +
               $"<NewPortMappingDescription>{description}</NewPortMappingDescription>\r\n" +
               "<NewLeaseDuration>0</NewLeaseDuration>\r\n" +
               "</u:AddPortMapping>\r\n" +
               "</s:Body>\r\n" +
               "</s:Envelope>\r\n";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var map in _mappings)
        {
            try
            {
                RemoveMappingAsync(map.ExternalPort, map.Protocol, CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
            catch
            {
                // best effort cleanup
            }
        }

        _mappings.Clear();
    }

    public async Task RemoveMappingAsync(int externalPort, string protocol, CancellationToken ct)
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                var natType = Type.GetTypeFromProgID("HNetCfg.NATUPnP");
                if (natType is null) return;
                var nat = Activator.CreateInstance(natType);
                if (nat is null) return;

                var scProp = natType.InvokeMember("StaticPortMappingCollection",
                    System.Reflection.BindingFlags.GetProperty, null, nat, null);
                if (scProp is null) return;

                scProp.GetType().InvokeMember("Remove",
                    System.Reflection.BindingFlags.InvokeMethod, null, scProp,
                    new object[] { externalPort, protocol.ToUpper() });
            }
            catch
            {
                // best effort
            }
        }

        await Task.CompletedTask;
    }

    private record PortMapping(int ExternalPort, string Protocol);
}

public sealed record UpnpResult(IPAddress PublicIp, int ExternalPort);
