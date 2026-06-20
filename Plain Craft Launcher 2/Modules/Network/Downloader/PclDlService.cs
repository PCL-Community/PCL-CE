using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.IO.Download;

namespace PCL.Network.Downloader;

/// <summary>
/// Result of a download operation.
/// </summary>
public class DownloadResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public long TotalBytes { get; init; }
    public TimeSpan Duration { get; init; }
}

/// <summary>
/// Download orchestrator with deduplication, URL failover, and progress tracking.
/// </summary>
public class PclDlService
{
    public static PclDlService Default { get; } = new();

    private readonly PclDlFactory _factory = new();
    private readonly DownloadService _service = new();

    /// <summary>
    /// Download a file with automatic URL failover and deduplication.
    /// Multiple callers requesting the same LocalPath will share a single download.
    /// </summary>
    public async Task<DownloadResult> DownloadAsync(DownloadFile file, CancellationToken cancellationToken)
    {
        var request = new DownloadRequest
        {
            Sources = file.Urls,
            DestinationPath = file.LocalPath,
            ConnectionFactory = url => _factory.CreateConnection(
                new DownloadSourceParams(
                    url,
                    file.UseBrowserUserAgent,
                    file.CustomUserAgent)),
            WriterFactory = _factory.MakeWriter
        };
        var result = await _service
            .DownloadAsync(
                request,
                progress => ApplyProgress(file, progress),
                cancellationToken)
            .ConfigureAwait(false);

        foreach (var error in result.Errors)
        {
            file.Errors.Add(error.Exception);
            ModBase.Log(
                error.Exception,
                $"[Download] 下载失败：{error.Source}",
                ModBase.LogLevel.Debug);
        }

        if (result.Success)
        {
            ModBase.Log(
                $"[Download] 下载成功：{file.LocalPath} ({result.SuccessfulSource})");
            return new DownloadResult
            {
                Success = true,
                TotalBytes = result.TotalBytes,
                Duration = result.Duration
            };
        }

        var errorMessage = $"下载失败：{file.LocalPath}\n" +
                           string.Join(
                               "\n",
                               result.Errors.Select(
                                   static error =>
                                       $"- {error.Source}: {error.Message}"));
        ModBase.Log($"[Download] {errorMessage}");
        return new DownloadResult
        {
            Success = false,
            ErrorMessage = errorMessage,
            Duration = result.Duration
        };
    }

    private static void ApplyProgress(
        DownloadFile file,
        DownloadProgress progress)
    {
        file.ActiveThreads = progress.Stage is DownloadStage.Completed or
            DownloadStage.Failed
            ? 0
            : 1;
        file.DownloadedBytes = progress.DownloadedBytes;
        file.Speed = progress.BytesPerSecond;
        if (progress.TotalBytes > 0)
        {
            file.TotalSize = progress.TotalBytes;
            file.IsUnknownSize = false;
        }
        else
        {
            file.IsUnknownSize = true;
        }

        file.State = progress.Stage switch
        {
            DownloadStage.Connecting => NetState.Connecting,
            DownloadStage.Reading => NetState.Reading,
            DownloadStage.Downloading => NetState.Downloading,
            DownloadStage.Committing => NetState.Merging,
            DownloadStage.Completed => NetState.Finished,
            DownloadStage.Failed => NetState.Interrupted,
            _ => file.State
        };
    }
}
