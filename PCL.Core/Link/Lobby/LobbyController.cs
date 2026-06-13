using System;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.IO.Net;
using PCL.Core.Link.Natayark;
using PCL.Core.Link.Scaffolding;

namespace PCL.Core.Link.Lobby;

public enum LobbyRole { Host, Client }

public enum LobbyState
{
    Idle,
    Connecting,      // 正在建立 P2P 连接
    WaitingPeer,     // 等待对端加入
    Connected,       // 已连接，端口转发中
    Failed           // 连接失败
}

public sealed class LobbySession : IDisposable
{
    private readonly LobbyClient _signaling;
    private readonly P2pConnectionManager _p2p;
    private readonly FirewallService _firewall;
    private TcpForward? _forward;
    private CancellationTokenSource? _sessionCts;
    private bool _disposed;

    public LobbySession(string signalingServerUrl)
    {
        _signaling = new LobbyClient(signalingServerUrl);
        _p2p = new P2pConnectionManager();
        _firewall = new FirewallService();
    }

    public LobbyRole Role { get; private set; }
    public LobbyState State { get; private set; } = LobbyState.Idle;
    public string? RoomCode { get; private set; }
    public string? LocalAddress { get; private set; }
    public string? RemoteAddress { get; private set; }
    public P2pConnectionResult? P2pResult { get; private set; }

    public event Action<LobbyState, string?>? StateChanged;

    /// <summary>
    ///     创建房间（房主）。
    /// </summary>
    /// <param name="mcPort">本地 Minecraft 端口（用于端口转发）</param>
    /// <param name="externalPort">期望的公网端口</param>
    public async Task<bool> HostAsync(int mcPort = 25565, int externalPort = 25565,
        CancellationToken ct = default)
    {
        Role = LobbyRole.Host;
        _sessionCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        // ---- 阶段 1：生成房间码 ----
        RoomCode = LobbyCodeGenerator.Generate();
        SetState(LobbyState.Connecting, $"正在建立连接... (房间 {RoomCode})");

        // ---- 阶段 2：P2P 连接建立 ----
        P2pResult = await _p2p.ConnectAsync(mcPort, externalPort, _sessionCts.Token);

        if (P2pResult.Phase == P2pPhase.Failed)
        {
            SetState(LobbyState.Failed, "无法建立 P2P 连接");
            return false;
        }

        LocalAddress = P2pResult.FormattedAddress ?? $"{P2pResult.Port}";
        if (string.IsNullOrEmpty(LocalAddress))
        {
            SetState(LobbyState.Failed, "无法获取本地地址");
            return false;
        }

        // ---- 阶段 3：向信令服务器注册 ----
        var serverCode = await _signaling.CreateRoomAsync(_sessionCts.Token);
        if (serverCode is null)
        {
            SetState(LobbyState.Failed, "无法连接到信令服务器");
            return false;
        }

        RoomCode = serverCode;

        await _signaling.PublishAsync(LocalAddress, _sessionCts.Token);
        SetState(LobbyState.WaitingPeer, $"等待对端连接... 大厅码: {RoomCode}");

        // ---- 阶段 4：等待对端 ----
        var peers = await _signaling.PollPeersAsync(120000, _sessionCts.Token);
        if (peers is null || peers.Length == 0)
        {
            SetState(LobbyState.Failed, "等待对端超时");
            return false;
        }

        RemoteAddress = peers[0];
        return await StartForwarding(mcPort, _sessionCts.Token);
    }

    /// <summary>
    ///     加入房间（客机）。
    /// </summary>
    /// <param name="code">大厅码</param>
    /// <param name="mcPort">本地 Minecraft 端口</param>
    public async Task<bool> JoinAsync(string code, int mcPort = 25565, int externalPort = 25565,
        CancellationToken ct = default)
    {
        Role = LobbyRole.Client;
        _sessionCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        // 解析大厅码
        var parsed = LobbyCodeGenerator.TryParse(code);
        if (parsed is null)
        {
            SetState(LobbyState.Failed, "无效的大厅码");
            return false;
        }

        RoomCode = parsed;
        SetState(LobbyState.Connecting, $"正在加入房间 {RoomCode}...");

        // ---- P2P 连接建立 ----
        P2pResult = await _p2p.ConnectAsync(mcPort, externalPort, _sessionCts.Token);

        if (P2pResult.Phase == P2pPhase.Failed)
        {
            SetState(LobbyState.Failed, "无法建立 P2P 连接");
            return false;
        }

        LocalAddress = P2pResult.FormattedAddress ?? $"{P2pResult.Port}";
        if (string.IsNullOrEmpty(LocalAddress))
        {
            SetState(LobbyState.Failed, "无法获取本地地址");
            return false;
        }

        // ---- 加入信令服务器房间 ----
        var joined = await _signaling.JoinRoomAsync(RoomCode, _sessionCts.Token);
        if (!joined)
        {
            SetState(LobbyState.Failed, "房间不存在或已过期");
            return false;
        }

        await _signaling.PublishAsync(LocalAddress, _sessionCts.Token);
        SetState(LobbyState.WaitingPeer, "等待房主响应...");

        // ---- 获取对端地址 ----
        var peers = await _signaling.PollPeersAsync(120000, _sessionCts.Token);
        if (peers is null || peers.Length == 0)
        {
            SetState(LobbyState.Failed, "无法连接到房主");
            return false;
        }

        RemoteAddress = peers[0];
        return await StartForwarding(mcPort, _sessionCts.Token);
    }

    private async Task<bool> StartForwarding(int mcPort, CancellationToken ct)
    {
        if (RemoteAddress is null)
            return false;

        SetState(LobbyState.Connected, $"已连接: {RemoteAddress}");

        // 在客户端启动 TCP 端口转发到本地 MC 端口
        if (Role == LobbyRole.Client)
        {
            try
            {
                // 解析对端地址
                var (host, port) = ParseAddress(RemoteAddress);
                var ip = System.Net.IPAddress.TryParse(host, out var parsed)
                    ? parsed
                    : (await System.Net.Dns.GetHostAddressesAsync(host, ct))[0];

                _forward = new TcpForward(
                    System.Net.IPAddress.Loopback,
                    mcPort,
                    ip,
                    port,
                    maxConnections: 10);
                _forward.Start();
            }
            catch (Exception ex)
            {
                SetState(LobbyState.Failed, $"端口转发启动失败: {ex.Message}");
                return false;
            }
        }

        return true;
    }

    private static (string host, int port) ParseAddress(string address)
    {
        // 处理 [IPv6]:Port 格式
        if (address.StartsWith('['))
        {
            var bracketEnd = address.IndexOf(']');
            if (bracketEnd > 1)
            {
                var host = address[1..bracketEnd];
                var port = address[(bracketEnd + 2)..]; // skip "]:"
                return int.TryParse(port, out var p) ? (host, p) : (host, 25565);
            }
        }

        // 处理 IPv4:Port 格式
        var lastColon = address.LastIndexOf(':');
        if (lastColon > 0)
        {
            var host = address[..lastColon];
            var port = address[(lastColon + 1)..];
            return int.TryParse(port, out var p) ? (host, p) : (address, 25565);
        }

        return (address, 25565);
    }

    private void SetState(LobbyState state, string? message)
    {
        State = state;
        StateChanged?.Invoke(state, message);
    }

    public DiagnosticsReport? GetDiagnostics()
    {
        return P2pResult is { Phase: P2pPhase.Failed } or null
            ? new DiagnosticsReport(P2pResult)
            : null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _sessionCts?.Cancel();
        _sessionCts?.Dispose();
        _forward?.Dispose();
        _signaling.Dispose();
        _p2p.Dispose();
        _firewall.Dispose();
    }
}

/// <summary>
///     诊断报告，用于 UI 层展示失败原因和解决方案。
/// </summary>
public sealed class DiagnosticsReport
{
    public string Title { get; }
    public System.Collections.Generic.List<DiagnosticEntry> Entries { get; } = [];

    public DiagnosticsReport(P2pConnectionResult? result)
    {
        Title = PCL.Core.App.Localization.Lang.Text("Link.Lobby.ConnectionFailed.Title");

        if (result?.Diagnostics is not null)
        {
            foreach (var d in result.Diagnostics)
                Entries.Add(new DiagnosticEntry(d.Phase, d.Symptom, d.Suggestion));
        }
    }
}

public sealed record DiagnosticEntry(
    string Phase, string Symptom, string Suggestion
);
