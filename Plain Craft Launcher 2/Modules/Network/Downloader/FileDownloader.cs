using System.IO;
using System.Net.Http;
using System.Threading;
using Downloader;
using PCL.Core.IO.Net;
using PCL.Core.Utils;

namespace PCL.Network;

public static class FileDownloader
{
    public static Task Download(string url, string localPath, bool useBrowserUserAgent = false,
        string customUserAgent = "", CancellationToken cancellationToken = default,
        bool enableParallelChunks = true, DownloadFile? trackedFile = null)
    {
        return DownloadCoreAsync(new[] { url }, localPath, useBrowserUserAgent, customUserAgent, cancellationToken,
            enableParallelChunks, trackedFile);
    }

    public static Task Download(IEnumerable<string> urls, string localPath, bool useBrowserUserAgent = false,
        string customUserAgent = "", CancellationToken cancellationToken = default,
        bool enableParallelChunks = true, DownloadFile? trackedFile = null)
    {
        return DownloadCoreAsync(urls, localPath, useBrowserUserAgent, customUserAgent, cancellationToken,
            enableParallelChunks, trackedFile);
    }

    public static void DownloadByLoader(string url, string localPath, bool useBrowserUserAgent = false,
        string customUserAgent = "")
    {
        Download(url, localPath, useBrowserUserAgent, customUserAgent).GetAwaiter().GetResult();
    }

    public static void DownloadByLoader(IEnumerable<string> urls, string localPath, bool useBrowserUserAgent = false,
        string customUserAgent = "")
    {
        Download(urls, localPath, useBrowserUserAgent, customUserAgent).GetAwaiter().GetResult();
    }

    private static async Task DownloadCoreAsync(IEnumerable<string> urls, string localPath, bool useBrowserUserAgent,
        string customUserAgent, CancellationToken cancellationToken, bool enableParallelChunks, DownloadFile? trackedFile)
    {
        var urlList = urls.Select(url => RequestSigning.SecretCdnSign(url.Trim())).Where(url => !string.IsNullOrWhiteSpace(url))
            .Distinct().ToList();
        if (urlList.Count == 0)
            throw new ArgumentException("未提供可用的下载地址", nameof(urls));

        Directory.CreateDirectory(Path.GetDirectoryName(localPath) ?? throw new ArgumentException("下载路径无效", nameof(localPath)));

        Exception? lastException = null;
        foreach (var url in urlList)
        {
            try
            {
                await DownloadSingleAsync(url, localPath, useBrowserUserAgent, customUserAgent, cancellationToken,
                    enableParallelChunks, trackedFile).ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException)
            {
                CleanupTempFiles(localPath);
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;
                CleanupTempFiles(localPath);
                ModBase.Log(ex, $"[Download] 下载失败，尝试下一个源：{url}", ModBase.LogLevel.Debug);
            }
        }

        throw new IOException($"下载失败：{localPath}", lastException);
    }

    private static async Task DownloadSingleAsync(string url, string localPath, bool useBrowserUserAgent,
        string customUserAgent, CancellationToken cancellationToken, bool enableParallelChunks, DownloadFile? trackedFile)
    {
        ModBase.Log($"[Download] 开始下载：{url} -> {localPath}");
        CleanupTempFiles(localPath);

        var perFileThreadLimit = enableParallelChunks ? Math.Max(1, ModNet.NetTaskThreadLimit) : 1;
        var configuration = new DownloadConfiguration
        {
            ChunkCount = perFileThreadLimit,
            ParallelCount = perFileThreadLimit,
            ParallelDownload = perFileThreadLimit > 1,
            MaximumBytesPerSecond = ModNet.NetTaskSpeedLimitHigh > 0 ? ModNet.NetTaskSpeedLimitHigh : 0,
            MaxTryAgainOnFailure = 2,
            BlockTimeout = 60000,
            DownloadFileExtension = ModNet.NetDownloadEnd,
            EnableAutoResumeDownload = false,
            RequestConfiguration = DownloadRequestFactory.Create(url, useBrowserUserAgent, customUserAgent),
            CustomHttpClientFactory = () => NetworkService.GetClient(),
            MinimumSizeOfChunking = 1024 * 1024L,
        };

        using var downloader = new DownloadService(configuration);
        var tcs = new TaskCompletionSource<bool>();
        void UpdateDownloadStat(DownloadProgressChangedEventArgs args)
        {
            if (trackedFile is null)
                return;

            trackedFile.State = PCL.Network.NetState.Downloading;
            trackedFile.TotalSize = Math.Max(trackedFile.TotalSize, args.TotalBytesToReceive);
            trackedFile.IsUnknownSize = trackedFile.TotalSize <= 0;
            trackedFile.DownloadedBytes = Math.Max(trackedFile.DownloadedBytes, args.ReceivedBytesSize);
            trackedFile.Speed = Math.Max(0L, (long)Math.Round(args.BytesPerSecondSpeed));
            trackedFile.ActiveThreads = Math.Max(0, args.ActiveChunks);
        }

        downloader.DownloadStarted += (_, args) =>
        {
            if (trackedFile is null)
                return;

            trackedFile.State = PCL.Network.NetState.Reading;
            trackedFile.TotalSize = Math.Max(trackedFile.TotalSize, args.TotalBytesToReceive);
            trackedFile.IsUnknownSize = args.TotalBytesToReceive <= 0;
            trackedFile.DownloadedBytes = 0;
            trackedFile.Speed = 0;
            trackedFile.ActiveThreads = 0;
        };
        downloader.DownloadProgressChanged += (_, args) => UpdateDownloadStat(args);
        downloader.ChunkDownloadProgressChanged += (_, args) => UpdateDownloadStat(args);
        downloader.DownloadFileCompleted += (_, args) =>
        {
            if (trackedFile is not null)
            {
                trackedFile.Speed = 0;
                trackedFile.ActiveThreads = 0;
                trackedFile.DownloadedBytes = Math.Max(trackedFile.DownloadedBytes, trackedFile.TotalSize);
            }

            if (args.Cancelled)
                tcs.TrySetCanceled();
            else if (args.Error != null)
                tcs.TrySetException(args.Error);
            else
                tcs.TrySetResult(true);
        };
        try
        {
            await downloader.DownloadFileTaskAsync(url, localPath, cancellationToken).ConfigureAwait(false);
            await tcs.Task.ConfigureAwait(false);
            var tempPath = localPath + ModNet.NetDownloadEnd;
            if (!File.Exists(localPath) && File.Exists(tempPath))
            {
                for (var retry = 0; retry < 5; retry++)
                {
                    try
                    {
                        File.Move(tempPath, localPath, true);
                        break;
                    }
                    catch (IOException)
                    {
                        Thread.Sleep(100);
                    }
                }
            }
            if (!File.Exists(localPath))
                throw new IOException($"下载未产生任何文件：{localPath}");
            ModBase.Log($"[Download] 下载成功：{localPath}");
        }
        catch (TaskCanceledException ex)
        {
            throw new TimeoutException($"下载超时（{url}）", ex);
        }
        catch (Exception ex)
        {
            throw new IOException($"下载失败：{url}", ex);
        }
    }

    private static void CleanupTempFiles(string localPath)
    {
        var tempPath = localPath + ModNet.NetDownloadEnd;
        TryDeleteFile(localPath);
        TryDeleteFile(tempPath);
    }

    private static void TryDeleteFile(string path)
    {
        for (var retry = 0; retry < 5; retry++)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(100);
            }
        }
    }
}
