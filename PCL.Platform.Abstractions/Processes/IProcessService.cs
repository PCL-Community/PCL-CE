// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Platform.Abstractions.Processes;

public interface IProcessService
{
    Task<ProcessResult> RunAsync(ProcessStartRequest request, CancellationToken cancellationToken);
}

public sealed record ProcessStartRequest
{
    public required string FileName { get; init; }
    public IReadOnlyList<string> Arguments { get; init; } = [];
    public string? WorkingDirectory { get; init; }
    public IReadOnlyDictionary<string, string?> EnvironmentVariables { get; init; } =
        new Dictionary<string, string?>(StringComparer.Ordinal);
    public bool CaptureOutput { get; init; } = true;
}

public sealed record ProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);
