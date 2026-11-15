using System.Text;

namespace PCL.Network;

public static class NetFacade
{
    public const string NetDownloadEnd = ".PCLDownloading";
    public const int NetTaskConnectionLimitMax = 256;

    public const int NetTaskSingleFileConnectionLimitMax = 8;

    public const int NetTaskConnectionsPerHostLimit = NetTaskConnectionLimitMax;
    public const long NetTaskBufferBudgetBytes = 512L * 1024 * 1024;

    /// <summary>所有下载任务共享的最大活跃 HTTP 连接数。</summary>
    public static int NetTaskConnectionLimit { get; set; } = 64;

    /// <summary>单个大文件可同时使用的最大连接数。</summary>
    public static int NetTaskSingleFileConnectionLimit { get; set; } = 8;

    public static long NetTaskSpeedLimitLow { get; set; } = 256 * 1024L;
    public static long NetTaskSpeedLimitHigh { get; set; } = -1;
    public static long NetTaskSpeedLimitLeft { get; set; } = -1;
    public static int NetTaskThreadCount { get; set; }
    public static NetManager NetManager => NetManager.Instance;

    public static object NetGetCodeByRequestRetry(
        string url,
        Encoding? encode = null,
        string accept = "",
        bool isJson = false,
        string? backupUrl = null,
        bool useBrowserUserAgent = false)
    {
        var param = new RequestParam
        {
            Encoding = encode,
            Accept = accept,
            FallbackUrl = backupUrl,
            UseBrowserUserAgent = useBrowserUserAgent,
            Timeout = 30000,
            Retries = 3
        };
        var result = Requester.FetchString(url, param);
        return isJson ? (object)LauncherCommon.GetJson(result) : result;
    }

    public static string NetGetCodeByLoader(
        string url,
        int timeout = 45000,
        bool isJson = false,
        bool useBrowserUserAgent = false)
    {
        return NetGetCodeByLoader(
            [
                url
            ],
            timeout,
            isJson,
            useBrowserUserAgent);
    }

    public static string NetGetCodeByLoader(
        IEnumerable<string> urls,
        int timeout = 45000,
        bool isJson = false,
        bool useBrowserUserAgent = false)
    {
        Exception? lastException = null;

        foreach (var url in urls)
            try
            {
                var content = Requester.Fetch(
                    url,
                    new FetchParam
                    {
                        Method = "GET",
                        Timeout = timeout,
                        UseBrowserUserAgent = useBrowserUserAgent
                    });

                return isJson ? LauncherCommon.GetJson(content).ToString() : content;
            }
            catch (Exception ex)
            {
                lastException = ex;
                LauncherCommon.Log(ex, $"[Fetch] 获取文件内容失败，尝试下一个源：{url}");
            }

        throw new Exception("无法获取文件内容", lastException);
    }

    public static Task NetDownloadByClient(
        string url,
        string localFile,
        bool useBrowserUserAgent = false)
    {
        return FileDownloader.DownloadAsync(url, localFile, useBrowserUserAgent);
    }

    public static bool HasDownloadingTask(bool ignoreCustomDownload = false)
    {
        return Loading
            .loaderTaskbar.ToList()
            .Any(task => task is { show: true, State: LoadState.Loading }
                && (!ignoreCustomDownload || !task.name.Contains("自定义下载")));
    }
}