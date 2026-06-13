using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
    private readonly ConcurrentDictionary<string, Task<DownloadResult>> _active =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _completed = new(StringComparer.OrdinalIgnoreCase);

    private const int BufferSize = 81920; // 80KB

    /// <summary>
    /// Download a file with automatic URL failover and deduplication.
    /// Multiple callers requesting the same LocalPath will share a single download.
    /// </summary>
    public async Task<DownloadResult> DownloadAsync(DownloadFile file, CancellationToken cancellationToken)
    {
        if (_completed.Contains(file.LocalPath))
            return new DownloadResult { Success = true };

        var isOwner = false;
        var task = _active.GetOrAdd(file.LocalPath, _ =>
        {
            isOwner = true;
            return DownloadCoreAsync(file, cancellationToken);
        });

        if (!isOwner)
        {
            return await task.ConfigureAwait(false);
        }

        try
        {
            var result = await task.ConfigureAwait(false);
            if (result.Success)
                _completed.Add(file.LocalPath);
            return result;
        }
        finally
        {
            _active.TryRemove(file.LocalPath, out _);
        }
    }

    private async Task<DownloadResult> DownloadCoreAsync(DownloadFile file, CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        var sw = Stopwatch.StartNew();

        foreach (var url in file.Urls)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IDlConnection? connection = null;
            IDlWriter? writer = null;
            try
            {
                var source = new DownloadSourceParams(url, file.UseBrowserUserAgent, file.CustomUserAgent);
                connection = _factory.CreateConnection(source);
                if (connection is null)
                {
                    errors.Add($"无法创建连接：{url}");
                    continue;
                }

                file.State = NetState.Connecting;
                var info = await connection.StartAsync(0, cancellationToken).ConfigureAwait(false);

                writer = _factory.MakeWriter(file.LocalPath);
                if (writer is null)
                {
                    errors.Add($"无法创建写入器：{file.LocalPath}");
                    continue;
                }

                var writeStream = await writer.CreateStreamAsync(cancellationToken).ConfigureAwait(false);

                file.TotalSize = Math.Max(file.TotalSize, info.Length);
                file.IsUnknownSize = info.Length <= 0;
                file.DownloadedBytes = 0;
                file.State = NetState.Reading;

                var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
                try
                {
                    var readSw = Stopwatch.StartNew();
                    long totalRead = 0;
                    while (true)
                    {
                        var read = await connection
                            .ReadAsync(buffer.AsMemory(0, BufferSize), cancellationToken)
                            .ConfigureAwait(false);
                        if (read == 0)
                            break;

                        await writeStream
                            .WriteAsync(buffer.AsMemory(0, read), cancellationToken)
                            .ConfigureAwait(false);
                        totalRead += read;

                        file.State = NetState.Downloading;
                        file.DownloadedBytes = totalRead;
                        if (totalRead > file.TotalSize)
                            file.TotalSize = totalRead;

                        var elapsed = readSw.Elapsed.TotalSeconds;
                        file.Speed = elapsed > 0.1 ? (long)(totalRead / elapsed) : 0;
                    }

                    await writeStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                    await writer.FinishAsync(cancellationToken).ConfigureAwait(false);

                    file.DownloadedBytes = totalRead;
                    file.TotalSize = totalRead;
                    file.Speed = 0;
                    file.ActiveThreads = 0;

                    sw.Stop();
                    ModBase.Log($"[Download] 下载成功：{file.LocalPath} ({url})");
                    return new DownloadResult
                    {
                        Success = true,
                        TotalBytes = totalRead,
                        Duration = sw.Elapsed,
                    };
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                errors.Add($"{url}: {ex.Message}");
                file.Errors.Add(ex);
                ModBase.Log(ex, $"[Download] 下载失败：{url}", ModBase.LogLevel.Debug);
            }
            finally
            {
                if (writer is not null)
                    await writer.StopAsync(CancellationToken.None).ConfigureAwait(false);
                if (connection is not null)
                    await connection.StopAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }

        sw.Stop();
        var errorMessage = $"下载失败：{file.LocalPath}\n" +
                           string.Join("\n", errors.Select(e => $"- {e}"));
        ModBase.Log($"[Download] {errorMessage}");
        return new DownloadResult
        {
            Success = false,
            ErrorMessage = errorMessage,
            Duration = sw.Elapsed,
        };
    }
}
