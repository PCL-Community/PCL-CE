using PCL.Core.Logging;
using PCL.Core.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Link;

public class McPing : IDisposable
{
    private readonly IPEndPoint _endpoint;
    private readonly string _host;
    private const int DefaultTimeout = 10000;
    private readonly int _timeout;

    public McPing(IPEndPoint endpoint, int timeout = DefaultTimeout)
    {
        _endpoint = endpoint;
        _host = _endpoint.Address.ToString();
        _timeout = timeout;
    }

    public McPing(string ip, int port = 25565, int timeout = DefaultTimeout)
    {
        _endpoint = IPAddress.TryParse(ip, out var ipAddress)
            ? new IPEndPoint(ipAddress, port)
            : new IPEndPoint(Dns.GetHostAddresses(ip).First(), port);
        _host = ip;
        _timeout = timeout;
    }

    /// <summary>
    /// 执行一次 Mc 服务器信息 Ping
    /// </summary>
    /// <returns></returns>
    /// <exception cref="NullReferenceException">获取的结果出现字段缺失时</exception>
    public async Task<McPingResult?> PingAsync(CancellationToken cancellationToken = default)
    {
        using var so = new Socket(SocketType.Stream, ProtocolType.Tcp);
        using var timeoutCts = new CancellationTokenSource(_timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            LogWrapper.Debug("McPing", $"Connecting to {_endpoint}");
            await so.ConnectAsync(_endpoint.Address, _endpoint.Port, linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            LogWrapper.Error(new TimeoutException("连接超时"), "McPing", $"Failed to connect to the {_endpoint}");
            return null;
        }
        catch (Exception e)
        {
            LogWrapper.Error(e, "McPing", $"Failed to connect to the {_endpoint}");
            return null;
        }

        LogWrapper.Debug("McPing", $"Connection established: {_endpoint}");
        await using var stream = new NetworkStream(so, false);
        var handshakePacket = _BuildHandshakePacket(_host, _endpoint.Port);
        var statusPacket = _BuildStatusRequestPacket();

        using var res = new MemoryStream();
        var watcher = new Stopwatch();
        try
        {
            await stream.WriteAsync(handshakePacket, linkedCts.Token);
            LogWrapper.Debug("McPing", $"Handshake sent, packet length: {handshakePacket.Length}");

            await stream.WriteAsync(statusPacket, linkedCts.Token);
            LogWrapper.Debug("McPing", $"Status sent, packet length: {statusPacket.Length}");

            var buffer = new byte[4096];
            watcher.Start();

            var totalLength = Convert.ToInt64(await VarIntHelper.ReadFromStreamAsync(stream, linkedCts.Token));
            watcher.Stop();
            LogWrapper.Debug("McPing", $"Total length: {totalLength}");

            long readLength = 0;
            while (readLength < totalLength)
            {
                var curReaded = await stream.ReadAsync(buffer, linkedCts.Token);
                readLength += curReaded;
                await res.WriteAsync(buffer, 0, curReaded, linkedCts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            LogWrapper.Error(new TimeoutException("数据读写超时"), "McPing", $"Operation timed out on {_endpoint}");
            return null;
        }
        catch (Exception e)
        {
            LogWrapper.Error(e, "McPing", $"Failed to communicate with {_endpoint}: {e.Message}");
            return null;
        }
        finally
        {
            if (so.Connected) so.Shutdown(SocketShutdown.Both);
        }

        so.Close();

        var retBinary = res.ToArray();
        var dataLength =
            Convert.ToInt32(VarIntHelper.Decode(retBinary.Skip(1).ToArray(), out var packDataHeaderLength));
        LogWrapper.Debug("McPing", $"ServerDataLength: {dataLength}");
        if (dataLength > retBinary.Length) throw new Exception("The server data is too large");
        var retCtx = Encoding.UTF8.GetString(retBinary.Skip(1 + packDataHeaderLength).Take(dataLength).ToArray());

        var retJson = JsonNode.Parse(retCtx) ?? throw new NullReferenceException("服务器返回了错误的信息");
#if DEBUG
        var resJsonDebug = retJson.DeepClone();
        if (resJsonDebug is JsonObject jsonObject && jsonObject.ContainsKey("favicon"))
        {
            jsonObject["favicon"] = "...";
        }

        LogWrapper.Debug("McPing", resJsonDebug.ToJsonString());
#endif
        var versionNode = retJson["version"] ?? throw new NullReferenceException("服务器返回了错误的字段，缺失: version");
        var playersNode = retJson["players"] ?? new JsonObject();
        var descNode = _ConvertJNodeToMcString(retJson["description"] ?? new JsonObject());
        var modInfoNode = retJson["modinfo"];
        var ret = new McPingResult(
            new McPingVersionResult(
                versionNode["name"]?.ToString() ?? "未知服务端版本名",
                Convert.ToInt32(versionNode["id"]?.ToString() ?? "-1")),
            new McPingPlayerResult(
                Convert.ToInt32(playersNode["max"]?.ToString() ?? "0"),
                Convert.ToInt32(playersNode["online"]?.ToString() ?? "0"),
                (playersNode["sample"]?.AsArray() ?? []).Select(x =>
                    new McPingPlayerSampleResult(x!["name"]?.ToString() ?? "", x["id"]?.ToString() ?? "")).ToList()),
            descNode,
            retJson["favicon"]?.ToString() ?? string.Empty,
            watcher.ElapsedMilliseconds,
            modInfoNode is null
                ? null
                : new McPingModInfoResult(
                    modInfoNode["type"]?.ToString() ?? "未知服务端类型",
                    (modInfoNode["modList"]?.AsArray() ?? [])
                    .Where(x => x!.AsObject().TryGetPropertyValue("modid", out _))
                    .Select(x => new McPingModInfoModResult(
                        x!["modid"]?.ToString() ?? string.Empty,
                        x["version"]?.ToString() ?? string.Empty))
                    .ToList())
        );
        return ret;
    }

    public async Task<McPingResult?> PingOldAsync()
    {
        using var so = new Socket(SocketType.Stream, ProtocolType.Tcp);
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(_timeout);
        cts.Token.Register(() =>
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
        await so.ConnectAsync(_endpoint, cts.Token);
        LogWrapper.Debug("McPing", $"Connected to {_endpoint}");
        await using var stream = new NetworkStream(so, false);
        var queryPack = new byte[] { 0xfe, 0x01 };
        await stream.WriteAsync(queryPack.AsMemory(0, queryPack.Length), cts.Token);
        var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cts.Token);
        so.Close();
        var retData = ms.ToArray();
        if (retData.Length < 21 || (retData.Length >= 21 && retData[0] != 0xff))
        {
            LogWrapper.Info("McPing", $"Unknown response from {_endpoint}, ignore");
            return null;
        }

        var retRep = Encoding.UTF8.GetString(retData);
        try
        {
            var retPart = retRep.Split(["\0\0\0"], StringSplitOptions.None);
            retPart = retPart.Select(s => new string(s
                    .Where((_, index) => index % 2 == 0)
                    .ToArray()))
                .ToArray();
            if (retPart.Length < 6)
                return null;
            return new McPingResult(new McPingVersionResult(retPart[2], int.Parse(retPart[1])),
                new McPingPlayerResult(int.Parse(retPart[5]), int.Parse(retPart[4]), []), retPart[3], string.Empty, 0,
                new McPingModInfoResult(string.Empty, []));
        }
        catch (Exception e)
        {
            LogWrapper.Error(e, "McPing", $"Unable to serialize response from {_endpoint}");
            return null;
        }
    }

    /// <summary>
    /// 构建握手包
    /// </summary>
    /// <param name="serverIp">服务器的地址</param>
    /// <param name="serverPort">服务器的端口</param>
    /// <returns>返回握手包的字节数组</returns>
    private byte[] _BuildHandshakePacket(string serverIp, int serverPort)
    {
        List<byte> handshake = [];
        handshake.AddRange(VarIntHelper.Encode(0)); //状态头 表明这是一个握手包
        handshake.AddRange(VarIntHelper.Encode(772)); //协议头 表明请求客户端的版本
        var binaryIp = Encoding.UTF8.GetBytes(serverIp);
        if (binaryIp.Length > 255) throw new Exception("服务器地址过长");
        handshake.AddRange(VarIntHelper.Encode((uint)binaryIp.Length)); //服务器地址长度
        handshake.AddRange(binaryIp); //服务器地址
        handshake.AddRange(BitConverter.GetBytes((ushort)serverPort).AsEnumerable().Reverse()); //服务器端口
        handshake.AddRange(VarIntHelper.Encode(1)); //1 表明当前状态为 ping 2 表明当前的状态为连接

        handshake.InsertRange(0, VarIntHelper.Encode((uint)handshake.Count)); //包长度
        return handshake.ToArray();
    }

    private byte[] _BuildStatusRequestPacket()
    {
        List<byte> statusRequest = [];
        statusRequest.AddRange(VarIntHelper.Encode(1)); //包长度
        statusRequest.AddRange(VarIntHelper.Encode(0)); //包 ID
        return statusRequest.ToArray();
    }

    private static string _ConvertJNodeToMcString(JsonNode? jsonNode)
    {
        if (jsonNode == null) return string.Empty;
        StringBuilder result = new();
        Stack<JsonNode> stack = new();
        stack.Push(jsonNode);

        while (stack.Count > 0)
        {
            var current = stack.Pop();

            switch (current.GetValueKind())
            {
                // 处理对象
                case JsonValueKind.Object:
                    {
                        var obj = current.AsObject();
                        // LogWrapper.Debug("McPing",$"Treat {obj} as JObject");
                        // 检查并处理 extra 数组
                        if (obj.TryGetPropertyValue("extra", out var extraNode) && extraNode is JsonArray extraArray)
                            // 逆序压栈保证原始顺序
                            for (var i = extraArray.Count - 1; i >= 0; i--)
                                if (extraArray[i] != null)
                                    stack.Push(extraArray[i]!);
                        // 检查并处理 text 属性
                        if (obj.TryGetPropertyValue("text", out _))
                        {
                            var formatCode = _GetTextStyleString(
                                obj["color"]?.ToString() ?? string.Empty,
                                Convert.ToBoolean(obj["bold"]?.ToString() ?? "false"),
                                Convert.ToBoolean(obj["obfuscated"]?.ToString() ?? "false"),
                                Convert.ToBoolean(obj["strikethrough"]?.ToString() ?? "false"),
                                Convert.ToBoolean(obj["underline"]?.ToString() ?? "false"),
                                Convert.ToBoolean(obj["italic"]?.ToString() ?? "false")
                            );
                            result.Append($"{formatCode}{obj["text"] ?? string.Empty}");
                        }

                        break;
                    }
                // 处理字符串值
                case JsonValueKind.String:
                    {
                        // LogWrapper.Debug("McPing",$"Treat {value} as JValue");
                        result.Append(current);
                        break;
                    }
                // 处理数组
                // 逆序压栈保证原始顺序
                case JsonValueKind.Array:
                    {
                        var jArr = current.AsArray();
                        // LogWrapper.Debug("McPing",$"Treat {array} as JArray");
                        for (var i = jArr.Count - 1; i >= 0; i--)
                            if (jArr[i] != null)
                                stack.Push(jArr[i]!);
                        break;
                    }
                default:
                    {
                        LogWrapper.Warn("McPing", $"解析到无法处理的 Motd 内容({current.GetValueKind()})：{current}");
                        break;
                    }
            }
        }

        LogWrapper.Debug("McPing", $"处理 Motd 内容完成，结果：{result}");
        return result.ToString();
    }

    private static readonly Dictionary<string, string> _ColorMap = new()
    {
        ["black"] = "0",
        ["dark_blue"] = "1",
        ["dark_green"] = "2",
        ["dark_aqua"] = "3",
        ["dark_red"] = "4",
        ["dark_purple"] = "5",
        ["gold"] = "6",
        ["gray"] = "7",
        ["dark_gray"] = "8",
        ["blue"] = "9",
        ["green"] = "a",
        ["aqua"] = "b",
        ["red"] = "c",
        ["light_purple"] = "d",
        ["yellow"] = "e",
        ["white"] = "f"
    };

    private static string _GetTextStyleString(
        string color,
        bool bold = false,
        bool obfuscated = false,
        bool strikethrough = false,
        bool underline = false,
        bool italic = false)
    {
        var sb = new StringBuilder();
        if (_ColorMap.TryGetValue(color, out var colorCode)) sb.Append($"§{colorCode}");
        if (bold) sb.Append("§l");
        if (italic) sb.Append("§o");
        // if (obfuscated) sb.Append("§k"); // 暂时别用
        if (underline) sb.Append("§n");
        if (strikethrough) sb.Append("§m");
        if (color.StartsWith('#')) sb.Append(color);
        return sb.ToString();
    }

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
