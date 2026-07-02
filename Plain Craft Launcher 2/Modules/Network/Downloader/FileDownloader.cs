using System.IO;
using System.Net.Http;
using Downloader;
using PCL.Core.IO.Net;


namespace PCL.Network;

public static class FileDownloader
{
    public static async Task DownloadAsync(string url, string localPath, bool useBrowserUserAgent = false,
        string customUserAgent = "", CancellationToken cancellationToken = default,
        bool enableParallelChunks = true, DownloadFile? trackedFile = null)
    {
        await DownloadCoreAsync([url], localPath, useBrowserUserAgent, customUserAgent, cancellationToken,
            enableParallelChunks, trackedFile).ConfigureAwait(false);
    }

    public static async Task DownloadAsync(IEnumerable<string> urls, string localPath, bool useBrowserUserAgent = false,
        string customUserAgent = "", CancellationToken cancellationToken = default,
        bool enableParallelChunks = true, DownloadFile? trackedFile = null)
    {
        await DownloadCoreAsync(urls, localPath, useBrowserUserAgent, customUserAgent, cancellationToken,
            enableParallelChunks, trackedFile).ConfigureAwait(false);
    }

    public static void DownloadByLoader(string url, string localPath, bool useBrowserUserAgent = false,
        string customUserAgent = "")
    {
        DownloadAsync(url, localPath, useBrowserUserAgent, customUserAgent).GetAwaiter().GetResult();
    }

    public static void DownloadByLoader(IEnumerable<string> urls, string localPath, bool useBrowserUserAgent = false,
        string customUserAgent = "")
    {
        DownloadAsync(urls, localPath, useBrowserUserAgent, customUserAgent).GetAwaiter().GetResult();
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
        var isCurseForgeUrl = IsCurseForgeUrl(url);
        var requestCustomUserAgent = !isCurseForgeUrl && !string.IsNullOrWhiteSpace(customUserAgent)
            ? customUserAgent
            : null;
        var configuration = new DownloadConfiguration
        {
            ChunkCount = perFileThreadLimit,
            ParallelCount = perFileThreadLimit,
            ParallelDownload = perFileThreadLimit > 1,
            MaximumBytesPerSecond = ModNet.NetTaskSpeedLimitHigh > 0 ? ModNet.NetTaskSpeedLimitHigh : 0,
            MaxTryAgainOnFailure = 2,
            BlockTimeout = 60000,
            DownloadFileExtension = ModNet.netDownloadEnd,
            EnableAutoResumeDownload = false,
            MinimumSizeOfChunking = 1024 * 1024L,
        };

        if (requestCustomUserAgent is not null)
            configuration.CustomHttpMessageHandlerFactory = () => new CustomUserAgentMessageHandler(requestCustomUserAgent);
        else
            configuration.CustomHttpClientFactory = () =>
                NetworkService.GetClient(isCurseForgeUrl ? NetworkService.CurseForgeApi : NetworkService.Default);

        using var downloader = new DownloadService(configuration);
        using var cancelReg = cancellationToken.Register(() =>
        {
            try { downloader.CancelAsync(); } catch {  } // 忽略
        });
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
            var tempPath = localPath + ModNet.netDownloadEnd;
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
        catch (TaskCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (TaskCanceledException ex)
        {
            throw new TimeoutException($"下载超时（{url}）", ex);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new IOException($"下载失败：{url}", ex);
        }
    }

    private static void CleanupTempFiles(string localPath)
    {
        var tempPath = localPath + ModNet.netDownloadEnd;
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

    private static bool IsCurseForgeUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var parsedUri)
            && IsCurseForgeHost(parsedUri.Host);
    }

    private static bool IsCurseForgeHost(string host)
    {
        return host.Equals("edge.forgecdn.net", StringComparison.OrdinalIgnoreCase)
            || host.Equals("mediafilez.forgecdn.net", StringComparison.OrdinalIgnoreCase)
            || host.Equals("forgecdn.net", StringComparison.OrdinalIgnoreCase)
            || host.Equals("api.curseforge.com", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class CustomUserAgentMessageHandler(string customUserAgent) : HttpMessageHandler
    {
        private readonly HttpClient _client = NetworkService.GetClient();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var forwardedRequest = CloneRequest(request);
            forwardedRequest.Headers.Remove("User-Agent");
            forwardedRequest.Headers.TryAddWithoutValidation("User-Agent", customUserAgent);

            try
            {
                return await _client.SendAsync(forwardedRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch
            {
                forwardedRequest.Dispose();
                throw;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _client.Dispose();

            base.Dispose(disposing);
        }

        private static HttpRequestMessage CloneRequest(HttpRequestMessage request)
        {
            var clone = new HttpRequestMessage(request.Method, request.RequestUri)
            {
                Version = request.Version,
                VersionPolicy = request.VersionPolicy
            };

            foreach (var header in request.Headers)
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

            return clone;
        }
    }

}
