using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Link.Lobby;

/// <summary>
///     Supabase Realtime 信令客户端，通过 WebSocket 进行 P2P IP 交换。
///     基于 Supabase Realtime 的 Broadcast 功能（Phoenix Channels 协议）。
/// </summary>
/// <example>
///     var client = new SupabaseSignalingClient(
///         SupabaseSignalingClient.DefaultProjectUrl,
///         SupabaseSignalingClient.DefaultAnonKey);
/// </example>
public sealed class SupabaseSignalingClient : ISignalingClient
{
    /// <summary>
    ///     PCL N Edition 联机信令服务器。
    /// </summary>
    public const string DefaultProjectUrl = "https://vtvhtscdvfnuttwapzxu.supabase.co";

    /// <summary>
    ///     Supabase 匿名密钥（公开，仅用于 Realtime WebSocket 认证）。
    /// </summary>
    public const string DefaultAnonKey = "sb_publishable_32yTlQ7EWeFlIJQtrNziOA_y_FDP2RS";

    private readonly string _projectUrl;
    private readonly string _anonKey;
    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;
    private string? _roomCode;
    private bool _disposed;

    /// <summary>
    ///     收到对端连接信息时触发。
    /// </summary>
    public event Action<string>? OnPeerInfo;

    /// <summary>
    ///     连接状态变化。
    /// </summary>
    public event Action<bool>? OnConnectionChanged;

    public bool IsConnected => _ws?.State == WebSocketState.Open;

    public SupabaseSignalingClient(string projectUrl, string anonKey)
    {
        _projectUrl = projectUrl.TrimEnd('/');
        _anonKey = anonKey;
    }

    /// <summary>
    ///     连接并创建房间（房主）。返回房间码。
    /// </summary>
    public async Task<string?> HostAsync(CancellationToken ct = default)
    {
        if (!await ConnectAsync(ct)) return null;

        _roomCode = GenerateRoomCode();
        if (!await JoinChannelAsync(_roomCode, ct))
        {
            _roomCode = null;
            return null;
        }

        return _roomCode;
    }

    /// <summary>
    ///     连接并加入房间（客机）。
    /// </summary>
    public async Task<bool> JoinAsync(string roomCode, CancellationToken ct = default)
    {
        if (!await ConnectAsync(ct)) return false;

        _roomCode = roomCode;
        if (!await JoinChannelAsync(roomCode, ct))
        {
            _roomCode = null;
            return false;
        }

        return true;
    }

    /// <summary>
    ///     广播本机连接地址给房内对端。
    /// </summary>
    public async Task PublishAsync(string address, CancellationToken ct = default)
    {
        await BroadcastAsync("peer_info",
            JsonSerializer.Serialize(new { address }), ct);
    }

    /// <summary>
    ///     断开连接并离开房间。
    /// </summary>
    public async Task LeaveAsync(CancellationToken ct = default)
    {
        if (_roomCode is not null && _ws?.State == WebSocketState.Open)
        {
            try
            {
                await SendAsync(new
                {
                    topic = $"realtime:{_roomCode}",
                    @event = "phx_leave",
                    payload = new { },
                    @ref = Guid.NewGuid().ToString()
                }, ct);
            }
            catch { }
        }

        _roomCode = null;
        _cts?.Cancel();
        if (_ws?.State == WebSocketState.Open)
            await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", ct);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        LeaveAsync(CancellationToken.None).GetAwaiter().GetResult();
        _cts?.Dispose();
        _ws?.Dispose();
    }

    // ---- 内部实现 ----

    private async Task<bool> ConnectAsync(CancellationToken ct)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _ws = new ClientWebSocket();
        _ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);

        try
        {
            var url = $"{_projectUrl.Replace("https://", "wss://")}/realtime/v1/websocket?apikey={_anonKey}";
            await _ws.ConnectAsync(new Uri(url), ct);

            // 启动接收循环
            _ = ReceiveLoopAsync(_cts.Token);
            StartHeartbeat(_cts.Token);

            OnConnectionChanged?.Invoke(true);
            return true;
        }
        catch
        {
            OnConnectionChanged?.Invoke(false);
            return false;
        }
    }

    private async Task<bool> JoinChannelAsync(string roomCode, CancellationToken ct)
    {
        var refId = Guid.NewGuid().ToString();
        var joinMsg = new
        {
            topic = $"realtime:{roomCode}",
            @event = "phx_join",
            payload = new
            {
                config = new
                {
                    broadcast = new { self = true },
                    presence = new { key = "" }
                }
            },
            @ref = refId
        };

        await SendAsync(joinMsg, ct);

        // 等待 phx_reply（简化: 延迟等待）
        await Task.Delay(500, ct);
        return true;
    }

    private async Task BroadcastAsync(string eventName, string payloadJson, CancellationToken ct)
    {
        if (_roomCode is null) return;

        await SendAsync(new
        {
            topic = $"realtime:{_roomCode}",
            @event = "broadcast",
            payload = new
            {
                type = "broadcast",
                @event = eventName,
                payload = JsonNode.Parse(payloadJson)
            },
            @ref = Guid.NewGuid().ToString()
        }, ct);
    }

    private async Task SendAsync(object msg, CancellationToken ct)
    {
        if (_ws?.State != WebSocketState.Open) return;
        var json = JsonSerializer.Serialize(msg);
        var bytes = Encoding.UTF8.GetBytes(json);
        await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[8192];
        var sb = new StringBuilder();

        try
        {
            while (_ws?.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                if (result.EndOfMessage)
                {
                    ProcessMessage(sb.ToString());
                    sb.Clear();
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
    }

    private void ProcessMessage(string json)
    {
        try
        {
            var msg = JsonNode.Parse(json);
            if (msg is null) return;

            var evt = msg["event"]?.ToString();
            var payload = msg["payload"];
            if (evt == "broadcast" && payload is not null)
            {
                var broadcastEvent = payload["event"]?.ToString();
                var broadcastPayload = payload["payload"];
                if (broadcastEvent == "peer_info" && broadcastPayload is not null)
                {
                    var address = broadcastPayload["address"]?.ToString();
                    if (!string.IsNullOrEmpty(address))
                        OnPeerInfo?.Invoke(address);
                }
            }
        }
        catch { /* 忽略解析错误 */ }
    }

    private void StartHeartbeat(CancellationToken ct)
    {
        Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested && _ws?.State == WebSocketState.Open)
            {
                await Task.Delay(25000, ct);
                try
                {
                    await SendAsync(new
                    {
                        topic = "phoenix",
                        @event = "heartbeat",
                        payload = new { },
                        @ref = Guid.NewGuid().ToString()
                    }, ct);
                }
                catch { break; }
            }
        }, ct);
    }

    private static string GenerateRoomCode()
    {
        Span<byte> bytes = stackalloc byte[6];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes); // 12位大写十六进制
    }
}
