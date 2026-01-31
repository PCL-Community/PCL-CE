using PCL.Core.IO.Pipe;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace PCL.Core.IO.Pipes;

/// <summary>
/// PipeServer 工厂类，负责创建和管理 PipeServer 实例
/// </summary>
public static class PipeServerFactory
{
    // 存储活跃的 PipeServer 实例，防止被 GC 回收
    private static readonly List<PipeServer> _ActiveServers = [];
    private static readonly object _Lock = new();

    /// <summary>
    /// 创建并启动 PipeServer 实例
    /// </summary>
    /// <param name="pipeName">管道名称</param>
    /// <param name="identifier">服务端标识</param>
    /// <param name="stopWhenException">异常时是否停止</param>
    /// <param name="loopCallback">客户端连接回调</param>
    /// <param name="stopCallback">停止回调</param>
    /// <param name="allowedProcessId">允许的进程ID列表</param>
    /// <returns>创建的 PipeServer 实例</returns>
    public static PipeServer CreateAndStartServer(
        string pipeName,
        string identifier,
        bool stopWhenException,
        Func<StreamReader, StreamWriter, Process?, bool> loopCallback,
        Action? stopCallback = null,
        int[]? allowedProcessId = null)
    {
        var server =
            // 创建服务器实例
            new PipeServer(
                pipeName, identifier, stopWhenException, loopCallback,
                (myself) =>
                {
                    _RemoveServer(myself);

                    stopCallback?.Invoke();
                },
                allowedProcessId);

        _AddServer(server);

        server.Start();

        return server;
    }

    /// <summary>
    /// 添加服务器到活跃列表
    /// </summary>
    /// <param name="server">要添加的服务器实例</param>
    private static void _AddServer(PipeServer server)
    {
        lock (_Lock)
        {
            _ActiveServers.Add(server);
        }
    }

    /// <summary>
    /// 从活跃列表中移除服务器
    /// </summary>
    /// <param name="server">要移除的服务器实例</param>
    private static void _RemoveServer(PipeServer server)
    {
        lock (_Lock)
        {
            _ActiveServers.Remove(server);
        }
    }

    /// <summary>
    /// 获取当前活跃的服务器数量
    /// </summary>
    /// <returns>活跃服务器数量</returns>
    public static int GetActiveServerCount()
    {
        lock (_Lock)
        {
            return _ActiveServers.Count;
        }
    }
}
