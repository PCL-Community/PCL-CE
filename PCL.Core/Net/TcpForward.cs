using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Logging;

namespace PCL.Core.Net;
public sealed class TcpForward(
    IPAddress listenAddress,
    int listenPort,
    IPAddress targetAddress,
    int targetPort,
    int maxConnections = 10)
    : IDisposable
{
    private Socket? _listenerSocket;
    private CancellationTokenSource? _cts;
    private readonly SemaphoreSlim _connectionSemaphore = new(maxConnections, maxConnections);
    private readonly ConcurrentDictionary<Guid, ConnectionPair> _activeConnections = new();

    private bool _isRunning;

    public int LocalPort { get; private set; }

    public int ActiveConnections => _activeConnections.Count;

    public void Start()
    {
        if (_isRunning) return;

        _cts = new CancellationTokenSource();
        _isRunning = true;

        try
        {
            // 创建并启动监听 Socket
            _listenerSocket = new Socket(SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true, // 禁用 Nagle 算法以提高响应速度
                ReceiveBufferSize = 8192,
                SendBufferSize = 8192
            };

            _listenerSocket.Bind(new IPEndPoint(listenAddress, listenPort));
            _listenerSocket.Listen(100); // 设置挂起连接队列的最大长度

            if (_listenerSocket.LocalEndPoint is not IPEndPoint endPoint) throw new InvalidCastException("出现了意外的转换操作");
            LocalPort = endPoint.Port;

            // 启动 TCP 接受连接任务
            _ = Task.Run(() => _AcceptConnectionsAsync(_cts.Token), _cts.Token);

            LogWrapper.Info("TcpForward", $"MC 端口转发已启动，监听 {listenAddress}:{LocalPort}，目标 {targetAddress}:{targetPort}");
        }
        catch (Exception ex)
        {
            _isRunning = false;
            LogWrapper.Error(ex, "TcpForward",  $"启动 MC 端口转发时发生错误: {ex.Message}");
            throw;
        }
    }

    public void Stop()
    {
        if (!_isRunning) return;

        _cts?.Cancel();
        _isRunning = false;

        // 关闭所有活动连接
        foreach (var connection in _activeConnections.Values)
        {
            connection.ClientSocket.SafeClose();
            connection.TargetSocket.SafeClose();
        }
        _activeConnections.Clear();

        _listenerSocket?.SafeClose();

        LogWrapper.Info("TcpForward", "MC 端口转发已停止");
    }

    private async Task _AcceptConnectionsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.Register(() =>
        {
            _listenerSocket.SafeClose();
        });
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_listenerSocket == null) break;
                var clientSocket = await _listenerSocket.AcceptAsync(cancellationToken);

                // 检查是否达到最大连接限制
                if (_activeConnections.Count >= maxConnections)
                {
                    clientSocket.SafeClose();
                    LogWrapper.Warn("TcpForward", $"已达到最大连接数限制({maxConnections})，拒绝新连接");
                    continue;
                }

                // 使用信号量控制并发处理
                await _connectionSemaphore.WaitAsync(cancellationToken);

                // 异步处理连接，不等待完成
                _ = Task.Run(() => _HandleConnectionAsync(clientSocket, cancellationToken), cancellationToken)
                    .ContinueWith(_ => _connectionSemaphore.Release(), TaskScheduler.Default);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                LogWrapper.Error(ex, "TcpForward", $"接受连接时发生错误");
                await Task.Delay(1000, cancellationToken); // 出错后等待 1 秒再继续
            }
        }
    }

    private async Task _HandleConnectionAsync(Socket clientSocket, CancellationToken cancellationToken)
    {
        var connectionId = Guid.NewGuid();

        try
        {
            LogWrapper.Info("TcpForward", $"接受来自 {clientSocket.RemoteEndPoint} 的连接");

            // 连接到目标服务器
            var targetSocket = new Socket(SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true,
                ReceiveBufferSize = 8192,
                SendBufferSize = 8192
            };

            await targetSocket.ConnectAsync(targetAddress, targetPort, cancellationToken);

            // 保存连接对
            var connectionPair = new ConnectionPair(clientSocket, targetSocket);
            _activeConnections[connectionId] = connectionPair;

            LogWrapper.Info("TcpForward", $"开始端口转发 {clientSocket.RemoteEndPoint} <-> {targetSocket.RemoteEndPoint}({connectionId})");

            // 使用高性能的 SocketAsyncEventArgs 进行双向转发
            var forwardTask1 = _ForwardDataAsync(clientSocket, targetSocket, cancellationToken);
            var forwardTask2 = _ForwardDataAsync(targetSocket, clientSocket, cancellationToken);

            // 等待任意一个方向的数据转发完成
            await Task.WhenAny(forwardTask1, forwardTask2);

            Console.WriteLine($"端口转发 {connectionId} 已完成");
        }
        catch (OperationCanceledException)
        {
            // 取消操作，正常退出
        }
        catch (Exception ex)
        {
            Console.WriteLine($"处理连接 {connectionId} 时发生错误: {ex.Message}");
        }
        finally
        {
            // 清理资源
            clientSocket.SafeClose();

            // 从活动连接中移除
            _activeConnections.TryRemove(connectionId, out _);
        }
    }

    private static async Task _ForwardDataAsync(Socket source, Socket destination, CancellationToken cancellationToken)
    {
        using var bufferOwner = MemoryPool<byte>.Shared.Rent(8192);
        try
        {
            var buffer = bufferOwner.Memory;
            while (!cancellationToken.IsCancellationRequested)
            {
                var bytesRead = await source.ReceiveAsync(buffer, SocketFlags.None, cancellationToken);
                if (bytesRead == 0) break; // 连接已关闭

                await destination.SendAsync(buffer[..bytesRead], SocketFlags.None, cancellationToken);
            }
        }
        catch {/* 忽略错误 */}
    }

    private bool _disposed;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!disposing) return;
        if (_disposed) return;
        Stop();
        _cts?.Dispose();
        _connectionSemaphore.Dispose();
        _disposed = true;
    }

    ~TcpForward()
    {
        Dispose(false);
    }

    private class ConnectionPair(Socket clientSocket, Socket targetSocket)
    {
        public Socket ClientSocket { get; } = clientSocket;
        public Socket TargetSocket { get; } = targetSocket;
    }
}