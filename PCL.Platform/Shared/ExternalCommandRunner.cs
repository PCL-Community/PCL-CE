using System.Diagnostics;
using System.Runtime.InteropServices;
using PCL.Core.Platform;

namespace PCL.Platform;

/// <summary>跨平台外部命令执行器。只负责进程生命周期和标准输出采集，不携带业务参数。</summary>
public sealed class ExternalCommandRunner : IExternalCommandRunner
{
    private static readonly HashSet<Process> ActiveProcesses = [];
    private static readonly Lock LifecycleLock = new();
    private static readonly PosixSignalRegistration? _sigint;
    private static readonly PosixSignalRegistration? _sigterm;
    private static bool _shuttingDown;

    static ExternalCommandRunner()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) => _KillActiveProcesses();
        if (OperatingSystem.IsWindows()) return;

        _sigterm = PosixSignalRegistration.Create(
            PosixSignal.SIGTERM,
            _ => _KillActiveProcesses());
        _sigint = PosixSignalRegistration.Create(
            PosixSignal.SIGINT,
            _ => _KillActiveProcesses());
    }

    public bool ExistsOnPath(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return false;

        if (Path.IsPathRooted(command))
            return File.Exists(command);

        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var extensions = OperatingSystem.IsWindows()
            ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT;.COM")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [""];

        foreach (
            var directory in path.Split(
                Path.PathSeparator,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (File.Exists(Path.Combine(directory, command)))
                return true;

            if (!OperatingSystem.IsWindows())
                continue;

            if (extensions
                .Select(extension => Path.Combine(directory, command + extension))
                .Any(File.Exists))
                return true;
        }

        return false;
    }

    public async Task<ExternalCommandResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(arguments);

        using var process = new Process();
        process.StartInfo = CreateStartInfo(fileName, arguments);

        if (!process.Start())
            throw new InvalidOperationException($"无法启动外部命令：{fileName}");

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        using var timeoutSource = timeout > TimeSpan.Zero
            ? new CancellationTokenSource(timeout)
            : null;
        using var linkedSource = timeoutSource is null
            ? null
            : CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutSource.Token);
        var token = linkedSource?.Token ?? cancellationToken;

        try
        {
            await process.WaitForExitAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            if (cancellationToken.IsCancellationRequested)
                throw;

            throw new TimeoutException($"外部命令超时：{fileName}");
        }

        var output = await outputTask.ConfigureAwait(false);
        var error = await errorTask.ConfigureAwait(false);
        return new ExternalCommandResult(process.ExitCode, output, error);
    }

    public IExternalCommandSession Start(
        string fileName,
        IReadOnlyList<string> arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(arguments);

        var process = new Process
        {
            StartInfo = CreateStartInfo(fileName, arguments)
        };

        if (!process.Start())
        {
            process.Dispose();
            throw new InvalidOperationException($"无法启动外部命令：{fileName}");
        }

        lock (LifecycleLock)
        {
            if (_shuttingDown)
            {
                TryKill(process);
                process.Dispose();
                throw new InvalidOperationException("启动器正在退出。");
            }

            ActiveProcesses.Add(process);
        }

        return new ExternalCommandSession(process);
    }

    public IDisposable Watch(
        string fileName,
        IReadOnlyList<string> arguments,
        Action<string> onOutput,
        Action? onExit = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(onOutput);

        var info = CreateStartInfo(fileName, arguments);
        info.RedirectStandardError = false;

        var process = new Process
        {
            StartInfo = info,
            EnableRaisingEvents = true
        };

        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null)
                onOutput(eventArgs.Data);
        };
        if (onExit is not null)
            process.Exited += (_, _) => onExit();

        if (!process.Start())
        {
            process.Dispose();
            throw new InvalidOperationException($"无法启动监视命令：{fileName}");
        }

        process.BeginOutputReadLine();
        return new ProcessWatch(process);
    }

    private static ProcessStartInfo CreateStartInfo(
        string fileName,
        IReadOnlyList<string> arguments)
    {
        var info = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var argument in arguments)
            info.ArgumentList.Add(argument);

        return info;
    }

    private static void _KillActiveProcesses()
    {
        Process[] processes;
        lock (LifecycleLock)
        {
            _shuttingDown = true;
            processes = [.. ActiveProcesses];
            ActiveProcesses.Clear();
        }

        foreach (var process in processes)
            TryKill(process);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(true);
        }
        catch
        {
            // 进程可能恰好在超时和终止之间退出。
        }
    }

    private sealed class ExternalCommandSession(Process process) : IExternalCommandSession
    {
        private bool _disposed;

        public Stream StandardOutput => process.StandardOutput.BaseStream;

        public Stream StandardError => process.StandardError.BaseStream;

        public bool HasExited
        {
            get
            {
                if (_disposed) return true;
                try
                {
                    return process.HasExited;
                }
                catch
                {
                    return true;
                }
            }
        }

        public int ExitCode
        {
            get
            {
                if (_disposed) return -1;
                try
                {
                    return process.ExitCode;
                }
                catch
                {
                    return -1;
                }
            }
        }

        public Task WaitForExitAsync(CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return process.WaitForExitAsync(cancellationToken);
        }

        public void Kill()
        {
            if (_disposed) return;
            TryKill(process);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            lock (LifecycleLock)
            {
                ActiveProcesses.Remove(process);
            }

            TryKill(process);
            process.Dispose();
        }
    }

    private sealed class ProcessWatch(Process process) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            TryKill(process);
            process.Dispose();
        }
    }
}