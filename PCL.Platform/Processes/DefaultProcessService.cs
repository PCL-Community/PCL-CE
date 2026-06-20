// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using PCL.Platform.Abstractions.Processes;

namespace PCL.Platform.Processes;

public sealed class DefaultProcessService : IProcessService
{
    public async Task<ProcessResult> RunAsync(ProcessStartRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FileName);

        ProcessStartInfo startInfo = new()
        {
            FileName = request.FileName,
            UseShellExecute = false,
            RedirectStandardOutput = request.CaptureOutput,
            RedirectStandardError = request.CaptureOutput
        };

        foreach (string argument in request.Arguments)
            startInfo.ArgumentList.Add(argument);

        if (!string.IsNullOrWhiteSpace(request.WorkingDirectory))
            startInfo.WorkingDirectory = request.WorkingDirectory;

        foreach ((string key, string? value) in request.EnvironmentVariables)
            startInfo.Environment[key] = value;

        using Process process = new() { StartInfo = startInfo };
        if (!process.Start())
            throw new InvalidOperationException($"Failed to start process '{request.FileName}'.");

        Task<string> outputTask = request.CaptureOutput
            ? process.StandardOutput.ReadToEndAsync(cancellationToken)
            : Task.FromResult(string.Empty);
        Task<string> errorTask = request.CaptureOutput
            ? process.StandardError.ReadToEndAsync(cancellationToken)
            : Task.FromResult(string.Empty);

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        return new ProcessResult(
            process.ExitCode,
            await outputTask.ConfigureAwait(false),
            await errorTask.ConfigureAwait(false));
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
    }
}
