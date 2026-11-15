using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Link.McPing.Model;
using PCL.Core.Logging;

namespace PCL.Core.Link.McPing;

/// <summary>
///     旧版Minecraft协议服务器探测服务实现
///     支持1.6及以下版本的服务器信息查询协议
/// </summary>
public class LegacyMcPingService : IMcPingService
{
    private const int DefaultTimeout = 10000;
    private bool _disposed;

    public LegacyMcPingService(IPEndPoint endpoint, int timeout = DefaultTimeout)
    {
        Endpoint = endpoint;
        Host = Endpoint.Address.ToString();
        Timeout = timeout;
    }

    public LegacyMcPingService(string ip, int port = 25565, int timeout = DefaultTimeout)
    {
        Endpoint = IPAddress.TryParse(ip, out var ipAddress)
            ? new IPEndPoint(ipAddress, port)
            : new IPEndPoint(Dns.GetHostAddresses(ip).First(), port);
        Host = ip;
        Timeout = timeout;
    }

    public IPEndPoint Endpoint { get; }

    public string Host { get; }

    public int Timeout { get; }

    /// <summary>
    ///     执行旧版Minecraft协议的服务器探测
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<McPingResult?> PingAsync(CancellationToken cancellationToken = default)
    {
        // TODO: 实现旧版协议的探测逻辑
        // 这里需要迁移原来McPing类中的PingOldAsync方法逻辑

        using var so = new Socket(SocketType.Stream, ProtocolType.Tcp);
        using var timeoutCts = new CancellationTokenSource(Timeout);
        using var linkedCts =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        linkedCts.Token.Register(() =>
        {
            try
            {
                if (so.Connected) so.Close();
            }
            catch (ObjectDisposedException)
            {
                /* Ignore */
            }
        });

        await so.ConnectAsync(Endpoint, linkedCts.Token);
        LogWrapper.Debug("LegacyMcPing", $"Connected to {Endpoint}");
        await using var stream = new NetworkStream(so, false);

        var queryPack = new byte[]
        {
            0xfe,
            0x01
        };
        await stream.WriteAsync(queryPack.AsMemory(0, queryPack.Length), linkedCts.Token);
        var ms = new MemoryStream();
        await stream.CopyToAsync(ms, linkedCts.Token);
        so.Close();
        var retData = ms.ToArray();
        if (retData.Length < 21 || (retData.Length >= 21 && retData[0] != 0xff))
        {
            LogWrapper.Info("McPing", $"Unknown response from {Endpoint}, ignore");
            return null;
        }

        var retRep = Encoding.UTF8.GetString(retData);
        try
        {
            var retPart = retRep.Split(["\0\0\0"], StringSplitOptions.None);
            retPart =
            [
                .. retPart
                    .Select(s => new string([.. s.Where((_, index) => index % 2 == 0)]))
            ];
            if (retPart.Length < 6)
                return null;
            return new McPingResult(
                new McPingVersionResult(retPart[2], int.Parse(retPart[1])),
                new McPingPlayerResult(int.Parse(retPart[5]), int.Parse(retPart[4]), []),
                retPart[3],
                string.Empty,
                0,
                new McPingModInfoResult(string.Empty, []),
                null);
        }
        catch (Exception e)
        {
            LogWrapper.Warn(e, "McPing", $"Unable to serialize response from {Endpoint}");
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}