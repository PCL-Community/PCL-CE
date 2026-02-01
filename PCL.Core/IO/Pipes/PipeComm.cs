using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;

namespace PCL.Core.IO.Pipes;

/// <summary>
/// 命名管道通信工具类
/// </summary>
public static class PipeComm
{
    #region 常量和字段

    /// <summary>
    /// 用于命名管道通信的统一字符编码
    /// </summary>
    public static readonly Encoding PipeEncoding = Encoding.UTF8;

    /// <summary>
    /// 用于命名管道通信的统一终止符
    /// </summary>
    public const char PipeEndingChar = (char)27; // '\e' (Escape)

    #endregion

    #region Factory

    // 存储活跃的 PipeServer 实例，防止被 GC 回收
    private static readonly List<PipeServer> _ActiveServers = [];
    private static readonly object _Lock = new();

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

    #endregion

    /// <summary>
    /// 在新的工作线程启动命名管道服务端
    /// </summary>
    /// <param name="identifier">服务端标识，用于日志标识及工作线程的命名</param>
    /// <param name="pipeName">命名管道名称</param>
    /// <param name="loopCallback">客户端连接后的回调函数，将会提供用于读取和写入数据的流，以及客户端进程 ID，返回 <c>true</c> 表示继续等待下一个客户端连接，返回 <c>false</c> 则停止服务端运行</param>
    /// <param name="stopCallback">服务端停止后的回调函数</param>
    /// <param name="stopWhenException">指定当回调函数抛出异常时是否停止服务端运行，使用 <c>true</c> 表示停止</param>
    /// <param name="allowedProcessId">允许连接的客户端进程 ID，如为 Nothing 则允许所有</param>
    /// <returns>创建的命名管道服务器流实例</returns>
    public static NamedPipeServerStream StartPipeServer(string identifier,
        string pipeName,
        Func<StreamReader, StreamWriter, Process?, bool> loopCallback,
        Action? stopCallback = null,
        bool stopWhenException = false,
        int[]? allowedProcessId = null)
    {
        var server =
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

        return server.PipeServerStream;
    }
}
