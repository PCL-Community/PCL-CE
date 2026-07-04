// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Text;

namespace PCL.Application.Launching;

public static class MinecraftLaunchScriptService
{
    public static async Task SaveAsync(
        MinecraftLaunchScriptRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TargetPath);

        string script = CreateScript(
            request.LaunchPlan.StartInfo,
            IsWindowsScript(request.TargetPath),
            request.PauseOnExit);
        string? directory = Path.GetDirectoryName(Path.GetFullPath(request.TargetPath));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        await File.WriteAllTextAsync(request.TargetPath, script, Encoding.UTF8, cancellationToken)
            .ConfigureAwait(false);
        TryMarkExecutable(request.TargetPath);
    }

    public static string CreateScript(ProcessStartInfo startInfo, bool windowsScript, bool pauseOnExit = true)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        ArgumentException.ThrowIfNullOrWhiteSpace(startInfo.FileName);

        return windowsScript
            ? CreateWindowsScript(startInfo, pauseOnExit)
            : CreateUnixScript(startInfo);
    }

    private static bool IsWindowsScript(string targetPath) =>
        Path.GetExtension(targetPath).ToLowerInvariant() switch
        {
            ".bat" or ".cmd" => true,
            ".sh" => false,
            _ => OperatingSystem.IsWindows()
        };

    private static string CreateWindowsScript(ProcessStartInfo startInfo, bool pauseOnExit)
    {
        StringBuilder builder = new();
        builder.AppendLine("@echo off");
        if (!string.IsNullOrWhiteSpace(startInfo.WorkingDirectory))
            builder.Append("cd /d ").AppendLine(QuoteForCmd(startInfo.WorkingDirectory));
        builder.Append(QuoteForCmd(startInfo.FileName));
        if (!string.IsNullOrWhiteSpace(startInfo.Arguments))
            builder.Append(' ').Append(startInfo.Arguments);
        builder.AppendLine();
        if (pauseOnExit)
            builder.AppendLine("pause");
        return builder.ToString();
    }

    private static string CreateUnixScript(ProcessStartInfo startInfo)
    {
        StringBuilder builder = new();
        builder.AppendLine("#!/usr/bin/env sh");
        builder.AppendLine("set -e");
        if (!string.IsNullOrWhiteSpace(startInfo.WorkingDirectory))
            builder.Append("cd -- ").AppendLine(QuoteForSh(startInfo.WorkingDirectory));
        builder.Append("exec ").Append(QuoteForSh(startInfo.FileName));
        if (!string.IsNullOrWhiteSpace(startInfo.Arguments))
            builder.Append(' ').Append(startInfo.Arguments);
        builder.AppendLine();
        return builder.ToString();
    }

    private static string QuoteForCmd(string value) =>
        '"' + value.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';

    private static string QuoteForSh(string value) =>
        "'" + value.Replace("'", "'\"'\"'", StringComparison.Ordinal) + "'";

    private static void TryMarkExecutable(string targetPath)
    {
        if (OperatingSystem.IsWindows())
            return;

        try
        {
            File.SetUnixFileMode(
                targetPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
        }
    }
}
