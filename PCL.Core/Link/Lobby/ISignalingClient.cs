using System;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Link.Lobby;

/// <summary>
///     P2P 信令客户端接口。房主创建房间，客机加入，双方交换地址。
/// </summary>
public interface ISignalingClient : IDisposable
{
    bool IsConnected { get; }
    event Action<string>? OnPeerInfo;
    event Action<bool>? OnConnectionChanged;

    Task<string?> HostAsync(CancellationToken ct = default);
    Task<bool> JoinAsync(string roomCode, CancellationToken ct = default);
    Task PublishAsync(string address, CancellationToken ct = default);
    Task LeaveAsync(CancellationToken ct = default);
}
