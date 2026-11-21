using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Watcher;
using PCL.Core.Platform;

namespace PCL.Core.Test.Minecraft.Watcher;

internal sealed class FakeGameProcess : IGameProcess
{
    private readonly TaskCompletionSource _exitSource =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int KillCount { get; private set; }

    public bool Disposed { get; private set; }

    public int Id { get; init; } = 4242;

    public bool HasExited { get; private set; }

    public DateTime? StartTime { get; init; } = new(2024, 1, 1, 12, 0, 0, DateTimeKind.Local);

    public int? ExitCode { get; private set; }

    public event Action<string>? OutputLineReceived;

    public event Action<string>? ErrorLineReceived;

    public void StartReading()
    {
    }

    public Task WaitForExitAsync(CancellationToken cancellationToken = default)
    {
        return _exitSource.Task.WaitAsync(cancellationToken);
    }

    public bool WaitForExit(TimeSpan timeout)
    {
        try
        {
            return _exitSource.Task.Wait(timeout);
        }
        catch (AggregateException)
        {
            return HasExited;
        }
    }

    public void Kill(bool entireProcessTree)
    {
        KillCount++;
        Complete(-1);
    }

    public void Dispose()
    {
        Disposed = true;
    }

    public void Emit(string line)
    {
        OutputLineReceived?.Invoke(line);
    }

    public void EmitError(string line)
    {
        ErrorLineReceived?.Invoke(line);
    }

    public void Complete(int exitCode)
    {
        if (HasExited)
            return;

        HasExited = true;
        ExitCode = exitCode;
        _exitSource.TrySetResult();
    }
}

internal sealed class FakeWindowInterop : IWindowInterop
{
    private readonly ConcurrentQueue<(nint Handle, string Title)> _windows = new();

    public int Queries;

    public ConcurrentQueue<string> Titles { get; } = new();

    public ConcurrentQueue<nint> MaximizedHandles { get; } = new();

    public (nint Handle, string Title)? TryFindGameWindow(int processId)
    {
        Interlocked.Increment(ref Queries);
        return _windows.TryDequeue(out var window) ? window : null;
    }

    public void SetWindowTitle(nint handle, string title)
    {
        Titles.Enqueue(title);
    }

    public void MaximizeWindow(nint handle)
    {
        MaximizedHandles.Enqueue(handle);
    }

    public void EnqueueWindow(nint handle, string title)
    {
        _windows.Enqueue((handle, title));
    }
}

internal sealed class FakeExternalCommandRunner : IExternalCommandRunner
{
    public int RunCount;

    public bool ExistsOnPath(string command)
    {
        return true;
    }

    public Task<ExternalCommandResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref RunCount);
        return Task.FromResult(new ExternalCommandResult(0, "dump-output", string.Empty));
    }

    public IExternalCommandSession Start(
        string fileName,
        IReadOnlyList<string> arguments)
    {
        throw new NotSupportedException();
    }

    public IDisposable Watch(
        string fileName,
        IReadOnlyList<string> arguments,
        Action<string> onOutput,
        Action? onExit = null)
    {
        throw new NotSupportedException();
    }
}

internal static class WatcherTestHelpers
{
    public static async Task WaitUntilAsync(
        Func<bool> predicate,
        TimeSpan? timeout = null,
        string? message = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
        while (!predicate())
        {
            if (DateTime.UtcNow > deadline)
                Assert.Fail(message ?? "等待条件超时");

            await Task.Delay(10).ConfigureAwait(false);
        }
    }

    public static GameSessionOptions CreateOptions(
        FakeGameProcess process,
        FakeWindowInterop? windowInterop = null,
        FakeExternalCommandRunner? commandRunner = null,
        DateTime? releaseTime = null,
        bool captureLiveLog = true,
        int recentLogCapacity = 500,
        bool maximizeWindow = false,
        Action<double>? reportProgress = null,
        CancellationToken abortToken = default)
    {
        return new GameSessionOptions
        {
            Metadata = new GameSessionMetadata(
                "Test Instance",
                "Test Description",
                releaseTime ?? new DateTime(2020, 1, 1),
                "/instance",
                "/game"),
            CaptureLiveLog = captureLiveLog,
            WindowTitleTemplate = "Minecraft {date} {time}",
            StackDumpToolPath = "jstack",
            WindowPollInterval = TimeSpan.FromMilliseconds(10),
            WindowTitleRefreshInterval = TimeSpan.FromMilliseconds(10),
            MaximizeDelay = TimeSpan.Zero,
            StackDumpInterval = TimeSpan.Zero,
            RecentLogCapacity = recentLogCapacity,
            MaximizeWindow = maximizeWindow,
            ReportProgress = reportProgress,
            AbortToken = abortToken,
            WindowInterop = windowInterop,
            CommandRunner = commandRunner
        };
    }
}