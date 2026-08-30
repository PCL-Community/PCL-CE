using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Utils.OS;

/// <summary>
///     外部进程执行工具。
/// </summary>
public static class ProcessRunner
{
    public static async Task<ProcessRunResult> CaptureAsync(
        string fileName,
        string arguments = "",
        int timeoutMs = 1_000_000,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        if (!string.IsNullOrWhiteSpace(workingDirectory))
            startInfo.WorkingDirectory = workingDirectory.TrimEnd('\\', '/');

        using var process = new Process();
        process.StartInfo = startInfo;
        process.EnableRaisingEvents = true;
        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        var timedOut = false;

        using var timeoutCts = timeoutMs >= 0
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : null;
        timeoutCts?.CancelAfter(timeoutMs);

        try
        {
            await process.WaitForExitAsync(timeoutCts?.Token ?? cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _TryKill(process);
            if (cancellationToken.IsCancellationRequested)
                throw;
            timedOut = true;
        }

        string output, error;
        try
        {
            output = await outputTask.ConfigureAwait(false);
            error = await errorTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            output = string.Empty;
            error = string.Empty;
        }

        int? exitCode = null;
        if (!timedOut && process.HasExited) exitCode = process.ExitCode;
        return new ProcessRunResult(exitCode, timedOut, output, error);
    }

    public static async Task<int?> RunAsync(
        string fileName,
        string arguments = "",
        int timeoutMs = 1_000_000,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        var result = await CaptureAsync(fileName, arguments, timeoutMs, workingDirectory, cancellationToken)
            .ConfigureAwait(false);
        return result.ExitCode;
    }

    private static void _TryKill(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(true);
        }
        catch
        {
            // ignored: the process may have exited between HasExited and Kill.
        }
    }
}

public sealed record ProcessRunResult(
    int? ExitCode,
    bool TimedOut,
    string StandardOutput,
    string StandardError)
{
    public string CombinedOutput => StandardOutput + StandardError;
}