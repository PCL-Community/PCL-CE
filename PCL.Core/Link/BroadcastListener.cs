using PCL.Core.Utils;
using PCL.Core.Utils.OS;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Link;

public class BroadcastListener(bool receiveLocalOnly = true) : IDisposable
{
    private UdpClient? _client;
    private UdpClient? _clientV6;
    private CancellationTokenSource? _cts;
    private static readonly IPAddress _MulticastAddress = IPAddress.Parse("224.0.2.60");
    private static readonly IPAddress _MulticastAddressV6 = IPAddress.Parse("ff75:230::60");
    private Task? _listenTask;
    private Task? _listenTaskV6;

    public event Action<BroadcastRecord, IPEndPoint>? OnReceive;

    public void Start()
    {
        if (_client is not null || _clientV6 is not null) return;
        _cts = new CancellationTokenSource();

        // IPv4
        _client = new UdpClient();
        _client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _client.Client.Bind(new IPEndPoint(IPAddress.Any, 4445));
        _client.JoinMulticastGroup(_MulticastAddress);

        // IPv6
        _clientV6 = new UdpClient(AddressFamily.InterNetworkV6);
        _clientV6.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _clientV6.Client.Bind(new IPEndPoint(IPAddress.IPv6Any, 4445));
        _clientV6.JoinMulticastGroup(_MulticastAddressV6);

        _listenTask = _ListenThreadAsync(_client);
        _listenTaskV6 = _ListenThreadAsync(_clientV6);
    }

    private async Task _ListenThreadAsync(UdpClient? client)
    {
        while (_cts is not null && client is not null && !_cts.IsCancellationRequested)
        {
            try
            {
                var result = await client.ReceiveAsync(_cts.Token);
                var receivedData = result.Buffer;
                var senderEndpoint = result.RemoteEndPoint;

                // 转换为 UTF-8 字符串
                var message = Encoding.UTF8.GetString(receivedData);

                // 解析服务端信息
                if (!_TryParseServerInfo(message, out var serverInfo) || serverInfo is null) continue;
                if (receiveLocalOnly && !_isAddressLocal(senderEndpoint.Address)) continue;
                OnReceive?.Invoke(serverInfo, senderEndpoint);
            }
            catch (OperationCanceledException)
            {
                // 正常取消，不报错
                break;
            }
            catch (Exception ex)
            {
                // 忽略解析错误或网络异常，继续监听
                Console.WriteLine($"Error processing packet: {ex.Message}");
            }
        }
    }

    private static bool _isAddressLocal(IPAddress address)
    {
        var ips = NetworkUtils.GetAllLocalAddress();
        return ips.Contains(address);
    }

    private static bool _TryParseServerInfo(string rawMessage, out BroadcastRecord? serverInfo)
    {
        // 使用正则提取 [MOTD]...[/MOTD] 和 [AD]...[/AD]
        var motdMatch = RegexPatterns.BroadcastMotd.Match(rawMessage);
        var adMatch = RegexPatterns.BroadcastAd.Match(rawMessage);

        // 端口是必须的
        if (!adMatch.Success || !int.TryParse(adMatch.Groups[1].Value.Trim(), out int port))
        {
            serverInfo = null;
            return false; // 端口无效，忽略整个消息
        }

        serverInfo = new BroadcastRecord(
            motdMatch.Success ? motdMatch.Groups[1].Value.Trim() : "missing no",
            new IPEndPoint(IPAddress.Loopback, port),
            DateTime.Now);
        return true;
    }

    public void Stop()
    {
        if (_client is null) return;

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _client?.Close();
        _client?.Dispose();
        _client = null;
        _clientV6?.Close();
        _clientV6?.Dispose();
        _clientV6 = null;

        _listenTask?.Wait(500);
        _listenTaskV6?.Wait(500);
    }

    private bool _isDisposed;
    public void Dispose()
    {
        if (_isDisposed) return;

        _isDisposed = true;
        Stop();
    }
}