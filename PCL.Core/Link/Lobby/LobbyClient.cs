using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Link.Lobby;

/// <summary>
///     联机大厅信令客户端，通过 HTTP 与信令服务器交换 P2P 连接信息。
///     协议：
///     - POST /api/lobby/create      → 创建房间，返回房间码
///     - POST /api/lobby/{code}/join  → 加入房间
///     - POST /api/lobby/{code}/offer → 发布本机连接信息
///     - GET  /api/lobby/{code}/peers → 轮询对端连接信息
///     - DELETE /api/lobby/{code}      → 离开房间
/// </summary>
public sealed class LobbyClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _serverBase;
    private string? _roomCode;
    private bool _disposed;

    public LobbyClient(string serverBaseUrl)
    {
        _serverBase = serverBaseUrl.TrimEnd('/');
        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        _http.DefaultRequestHeaders.Add("User-Agent", "PCL-N-Lobby/1.0");
    }

    public string ServerBase => _serverBase;
    public string? RoomCode => _roomCode;

    /// <summary>
    ///     创建房间并返回大厅码。
    /// </summary>
    public async Task<string?> CreateRoomAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _http.PostAsync($"{_serverBase}/api/lobby/create", null, ct);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync(ct);
            var node = JsonNode.Parse(json);
            var code = node?["code"]?.ToString();
            if (!string.IsNullOrEmpty(code))
                _roomCode = code;

            return _roomCode;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     加入已有房间。
    /// </summary>
    public async Task<bool> JoinRoomAsync(string code, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.PostAsync($"{_serverBase}/api/lobby/{code}/join", null, ct);
            if (response.IsSuccessStatusCode)
            {
                _roomCode = code;
                return true;
            }
        }
        catch { }
        return false;
    }

    /// <summary>
    ///     向信令服务器发布本机连接信息（供对端连接）。
    /// </summary>
    /// <param name="formattedAddress">格式：IPv4:Port 或 [IPv6]:Port</param>
    public async Task<bool> PublishAsync(string formattedAddress, CancellationToken ct = default)
    {
        if (_roomCode is null) return false;

        try
        {
            var body = new StringContent(
                JsonSerializer.Serialize(new { address = formattedAddress }),
                Encoding.UTF8,
                "application/json");
            var response = await _http.PostAsync(
                $"{_serverBase}/api/lobby/{_roomCode}/offer", body, ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    ///     轮询获取对端的连接信息。
    /// </summary>
    /// <returns>对端地址列表，或 null 表示无数据/超时</returns>
    public async Task<string[]?> PollPeersAsync(int timeoutMs = 15000, CancellationToken ct = default)
    {
        if (_roomCode is null) return null;

        try
        {
            using var pollCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            pollCts.CancelAfter(timeoutMs);

            var request = new HttpRequestMessage(HttpMethod.Get,
                $"{_serverBase}/api/lobby/{_roomCode}/peers");
            request.Headers.Add("X-Poll-Timeout", timeoutMs.ToString());

            var response = await _http.SendAsync(request, pollCts.Token);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync(pollCts.Token);
            var node = JsonNode.Parse(json);
            var peers = node?["peers"]?.AsArray();
            if (peers is null) return null;

            var result = new string[peers.Count];
            for (var i = 0; i < peers.Count; i++)
                result[i] = peers[i]?["address"]?.ToString() ?? "";

            return result;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     离开房间。
    /// </summary>
    public async Task LeaveAsync(CancellationToken ct = default)
    {
        if (_roomCode is null) return;

        try
        {
            await _http.DeleteAsync($"{_serverBase}/api/lobby/{_roomCode}", ct);
        }
        catch { }
        _roomCode = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_roomCode is not null)
            LeaveAsync().GetAwaiter().GetResult();

        _http.Dispose();
    }
}
