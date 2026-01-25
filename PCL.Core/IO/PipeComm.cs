using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using PCL.Core.App;
using PCL.Core.Logging;
using PCL.Core.Utils.OS;

namespace PCL.Core.IO;

public static class PipeComm
{
    private static void _PipeLog(string message) => LogWrapper.Trace("Pipe", message);
    private static void _PipeLogDebug(string message) => LogWrapper.Debug("Pipe", message);

    /// <summary>
    /// 用于命名管道通信的统一字符编码
    /// </summary>
    public static readonly Encoding PipeEncoding = Encoding.UTF8;

    /// <summary>
    /// 用于命名管道通信的统一终止符
    /// </summary>
    public const char PipeEndingChar = (char)27; // '\e' (ESC)

    /// <summary>
    /// 在新的工作线程启动命名管道服务端
    /// </summary>
    /// <param name="identifier">服务端标识，用于日志标识及工作线程的命名</param>
    /// <param name="pipeName">命名管道名称</param>
    /// <param name="loopCallback">客户端连接后的回调函数，将会提供用于读取和写入数据的流，以及客户端进程 ID，返回 <c>true</c> 表示继续等待下一个客户端连接，返回 <c>false</c> 则停止服务端运行</param>
    /// <param name="stopCallback">服务端停止后的回调函数</param>
    /// <param name="stopWhenException">指定当回调函数抛出异常时是否停止服务端运行，使用 <c>true</c> 表示停止</param>
    /// <param name="allowedProcessId">允许连接的客户端进程 ID，如为 Nothing 则允许所有</param>
    public static NamedPipeServerStream StartPipeServer(string identifier, string pipeName, Func<StreamReader, StreamWriter, Process?, bool> loopCallback, Action? stopCallback = null, bool stopWhenException = false, int[]? allowedProcessId = null)
    {
        var pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.None, 1024, 1024);
        var threadName = $"PipeServer/{identifier}";

        Basics.RunInNewThread(() =>
        {
            LogWrapper.Debug("Pipe", $"{identifier}: {pipeName} 服务端已在 '{threadName}' 工作线程启动");
            var hasNextLoop = true;
            var connected = false;

            while (hasNextLoop)
            {
                try
                {
                    hasNextLoop = false;
                    pipe.WaitForConnection(); // 等待客户端连接
                    // 获取客户端进程实例并校验
                    Process? clientProcess = null;
                    var clientProcessId = 0;
                    try
                    {
                        KernelInterop.GetNamedPipeClientProcessId(pipe.SafePipeHandle.DangerousGetHandle(), out var pid);
                        clientProcessId = (int)pid;
                        if (allowedProcessId != null)
                        {
                            var denied = allowedProcessId.All(id => id != clientProcessId);
                            if (denied)
                            {
                                hasNextLoop = true;
                                pipe.Disconnect();
                                _PipeLog($"[Pipe] {identifier}: 已拒绝 {clientProcessId}");
                                continue;
                            }
                        }
                        clientProcess = Process.GetProcessById(clientProcessId);
                    }
                    catch (Exception)
                    {
                        if (allowedProcessId != null)
                        {
                            hasNextLoop = true;
                            throw;
                        }
                    }
                    connected = true;
                    LogWrapper.Debug("Pipe", $"{identifier}: {clientProcessId} 已连接");
                    // 初始化读取/写入流
                    var reader = new StreamReader(pipe, PipeEncoding, false, 1024, true);
                    var writer = new StreamWriter(pipe, PipeEncoding, 1024, true);
                    // 执行回调函数
                    hasNextLoop = loopCallback(reader, writer, clientProcess);
                    // 写入终止符
                    writer.Write(PipeEndingChar);
                    writer.Flush(); // 刷新写入缓冲
                    reader.Read(); // 等待客户端
                }
                catch (Exception ex)
                {
                    if (!pipe.IsConnected && connected && ex is IOException)
                    {
                        _PipeLogDebug($"{identifier}: 客户端连接已丢失");
                        hasNextLoop = true;
                    }
                    else
                    {
                        LogWrapper.Warn(ex, "Pipe",  $"{identifier}: 服务端出错");
                        if (stopWhenException) hasNextLoop = false;
                    }
                }
                try
                {
                    pipe.Disconnect();
                }
                catch (InvalidOperationException)
                {
                    // 由于没妈的巨硬给的 IsConnected 不一定是准确的，需要运行 Disconnect() 确保管道断开连接
                    // 如果已经断开会抛出 InvalidOperationException 这里直接忽略掉
                }
                connected = false;
                _PipeLogDebug($"{identifier}: 已断开连接");
            }

            // 释放资源并执行停止回调
            pipe.Dispose();
            _PipeLogDebug($"{identifier}: 服务端已停止");
            stopCallback?.Invoke();
        }, threadName);

        return pipe;
    }
}
