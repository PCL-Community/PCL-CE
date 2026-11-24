using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using PCL.Core.App.IoC;
using PCL.Core.App.Localization;
using PCL.Core.IO;
using PCL.Core.Utils;

namespace PCL.Core.App.Essentials;

[LifecycleService(LifecycleState.BeforeLoading, Priority = -2134567890)]
[LifecycleScope("single-instance", "单例", false)]
public sealed partial class SingleInstanceService
{
    private static FileStream? _lockStream;

    private static readonly string _PrimaryLockFilePath =
        Path.Combine(Paths.State, "instance.lock");

    private static readonly string _PrimaryProcessIdFilePath =
        Path.Combine(Paths.State, "instance.pid");

    private static readonly string _FallbackLockFilePath =
        Path.Combine(Paths.Temp, "instance.lock");

    private static readonly string _FallbackProcessIdFilePath =
        Path.Combine(Paths.Temp, "instance.pid");

    private static void _TryRpc(string processId, string content)
    {
        var pipeName = $"{RpcService.PipePrefix}@{processId}";
        using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
        pipe.Connect(1000);
        using var sw = new StreamWriter(pipe, PipeComm.PipeEncoding);
        sw.WriteLine(content);
        sw.Write(PipeComm.PipeEndingChar);
        sw.Flush();
    }

    [LifecycleStart]
    private static void _Start()
    {
        var primary = _TryAcquire(_PrimaryLockFilePath, _PrimaryProcessIdFilePath);
        switch (primary.Status)
        {
            case LockStatus.Duplicate:
            {
                _ForwardToExistingInstance(primary.ProcessId);
                return;
            }

            case LockStatus.Acquired:
            {
                _lockStream = primary.Stream;
                return;
            }

            case LockStatus.Unavailable:
            {
                break;
            }

            default:
            {
                throw new ArgumentOutOfRangeException();
            }
        }

        var fallback = _TryAcquire(_FallbackLockFilePath, _FallbackProcessIdFilePath);
        switch (fallback.Status)
        {
            case LockStatus.Duplicate:
            {
                _ForwardToExistingInstance(fallback.ProcessId);
                return;
            }

            case LockStatus.Acquired:
            {
                Context.Warn("主单例锁不可用，已切换到临时目录锁");
                _lockStream = fallback.Stream;
                return;
            }

            default:
            {
                var error = primary.Error ?? fallback.Error;
                Context.Warn(
                    Lang.Text(
                        "Startup.SingleInstance.Unavailable",
                        error?.Message ?? "unknown"),
                    error);
                break;
            }
        }
    }

    private static void _ForwardToExistingInstance(string? processId)
    {
        Context.Info($"发现重复实例 {processId}，尝试传递参数并拉起主窗口");
        if (string.IsNullOrWhiteSpace(processId))
        {
            Context.RequestExit(1);
            return;
        }

        try
        {
            _TryRpc(
                processId,
                "REQ cli\n"
                + JsonSerializer.Serialize(
                    StartupService.UnhandledCommands,
                    JsonCompat.SerializerOptions));
            _TryRpc(processId, "REQ activate");
        }
        catch (Exception ex)
        {
            Context.Warn("RPC 通信失败", ex);
        }

        Context.RequestExit(1);
    }

    private static LockResult _TryAcquire(string lockFilePath, string processIdFilePath)
    {
        Exception? lastError = null;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            FileStream? stream = null;
            try
            {
                var directory = Path.GetDirectoryName(lockFilePath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                var processIdDirectory = Path.GetDirectoryName(processIdFilePath);
                if (!string.IsNullOrEmpty(processIdDirectory))
                    Directory.CreateDirectory(processIdDirectory);

                stream = new FileStream(
                    lockFilePath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None);
                try
                {
                    _WriteProcessId(processIdFilePath);
                }
                catch
                {
                    stream.Dispose();
                    throw;
                }

                return new LockResult(LockStatus.Acquired, stream, null, null);
            }
            catch (Exception ex)
            {
                stream?.Dispose();
                lastError = ex;
                var processId = _TryReadProcessId(processIdFilePath);

                if (!string.IsNullOrWhiteSpace(processId) && _IsSameProcess(processId))
                    return new LockResult(LockStatus.Duplicate, null, processId, ex);

                if (!string.IsNullOrWhiteSpace(processId) && _IsProcessAlive(processId))
                    return new LockResult(LockStatus.Duplicate, null, null, ex);

                if (ex is UnauthorizedAccessException
                    or DirectoryNotFoundException
                    or DriveNotFoundException
                    or PathTooLongException
                    or NotSupportedException)
                    return new LockResult(LockStatus.Unavailable, null, processId, ex);

                if (attempt < 4)
                {
                    Thread.Sleep(50);
                    continue;
                }

                var lockDirectory = Path.GetDirectoryName(lockFilePath);
                if (!string.IsNullOrEmpty(lockDirectory) && Directories.CanWrite(lockDirectory))
                    return new LockResult(LockStatus.Duplicate, null, null, ex);

                return new LockResult(LockStatus.Unavailable, null, processId, ex);
            }
        }

        return new LockResult(LockStatus.Unavailable, null, null, lastError);
    }

    private static void _WriteProcessId(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read);
        using var writer = new StreamWriter(stream, Encoding.ASCII, 8, false);
        writer.Write(Basics.CurrentProcessId);
        writer.Flush();
    }

    private static string? _TryReadProcessId(string path)
    {
        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding.ASCII);
            var processId = reader.ReadToEnd().Trim();
            return string.IsNullOrWhiteSpace(processId) ? null : processId;
        }
        catch
        {
            return null;
        }
    }

    private static bool _IsProcessAlive(string? processId)
    {
        if (!int.TryParse(processId, out var pid) || pid <= 0) return false;
        if (pid == Basics.CurrentProcessId) return true;

        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private static bool _IsSameProcess(string? processId)
    {
        if (!int.TryParse(processId, out var pid) || pid <= 0)
            return false;

        if (pid == Basics.CurrentProcessId)
            return true;

        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited
                && string.Equals(
                    process.ProcessName,
                    Basics.CurrentProcess.ProcessName,
                    StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    [LifecycleStop]
    private static void _Stop()
    {
        if (_lockStream is null) return;
        try
        {
            _lockStream.Dispose();
        }
        catch (Exception ex)
        {
            Context.Warn("释放单例锁失败", ex);
        }
        finally
        {
            _lockStream = null;
        }
    }

    private enum LockStatus
    {
        Acquired,
        Duplicate,
        Unavailable
    }

    private sealed record LockResult(
        LockStatus Status,
        FileStream? Stream,
        string? ProcessId,
        Exception? Error);
}