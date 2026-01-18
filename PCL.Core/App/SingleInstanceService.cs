using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using PCL.Core.IO;

namespace PCL.Core.App;

[LifecycleService(LifecycleState.BeforeLoading, Priority = -2134567890)]
[LifecycleScope("single-instance", "单例", false)]
public sealed partial class SingleInstanceService
{
    private static FileStream? _lockStream;
    private static readonly string _LockFilePath = Path.Combine(FileService.LocalDataPath, "instance.lock");

    private static void _TryActivateProcessWindow(int processId)
    {
        var pipeName = $"{RpcService.PipePrefix}@{processId}";
        using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
        pipe.Connect(1000);
        using var sw = new StreamWriter(pipe, PipeComm.PipeEncoding);
        sw.WriteLine("REQ activate");
        sw.Write(PipeComm.PipeEndingChar);
        sw.Flush();
    }

    [LifecycleStart]
    private static void _Start()
    {
        try
        {
            var stream = File.Open(_LockFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
            Context.Debug("未发现重复实例，正在向单例锁写入信息");
            using var sw = new StreamWriter(stream, Encoding.ASCII, 8, true);
            sw.Write(Basics.CurrentProcessId);
            sw.Flush();
            _lockStream = stream;
        }
        catch (Exception)
        {
            try
            {
                using var stream = File.Open(_LockFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);
                var pid = reader.ReadToEnd();
                Context.Info($"发现重复实例 {pid}，尝试拉起主窗口");
                _TryActivateProcessWindow(int.Parse(pid));
            }
            catch (Exception ex) { Context.Error("读取单例锁出错", ex); }
            finally { Context.RequestExit(1); }
        }
    }

    [LifecycleStop]
    private static void _Stop()
    {
        if (_lockStream == null) return;
        Context.Debug("正在删除单例锁");
        _lockStream.Dispose();
        File.Delete(_LockFilePath);
    }
}
