using System.IO;
using System.Text;

namespace PCL.Network;

public static class ModNet
{
    public const string netDownloadEnd = ".PCLDownloading";
    public static int NetTaskThreadLimit { get; set; } = 16;
    public static long NetTaskSpeedLimitLow { get; set; } = 256 * 1024L;
    public static long NetTaskSpeedLimitHigh { get; set; } = -1;
    public static long NetTaskSpeedLimitLeft { get; set; } = -1;
    public static int NetTaskThreadCount { get; set; }
    public static NetManager NetManager => NetManager.Instance;

    public static object NetGetCodeByRequestRetry(string url, Encoding? encode = null, string accept = "",
        bool isJson = false, string? backupUrl = null, bool useBrowserUserAgent = false)
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
        return isJson ? (object)ModBase.GetJson(result) : result;
    }

    public static object NetGetCodeByRequestOnce(string url, Encoding? encode = null, int timeout = 30000,
        bool isJson = false, string accept = "", bool useBrowserUserAgent = false)
    {
        var param = new RequestParam
        {
            Encoding = encode,
            Accept = accept,
            UseBrowserUserAgent = useBrowserUserAgent,
            Timeout = timeout,
            Retries = 1
        };
        var result = Requester.FetchString(url, param);
        return isJson ? (object)ModBase.GetJson(result) : result;
    }

    public static string NetGetCodeByLoader(string url, int timeout = 45000, bool isJson = false,
        bool useBrowserUserAgent = false)
    {
        return NetGetCodeByLoader(new[] { url }, timeout, isJson, useBrowserUserAgent);
    }

    public static string NetGetCodeByLoader(IEnumerable<string> urls, int timeout = 45000, bool isJson = false,
        bool useBrowserUserAgent = false)
    {
        Exception? lastException = null;

        foreach (var url in urls)
        {
            try
            {
                var content = Requester.Fetch(url, new FetchParam
                {
                    Method = "GET",
                    Timeout = timeout,
                    UseBrowserUserAgent = useBrowserUserAgent
                });
                
                return isJson ? ModBase.GetJson(content).ToString() : content;
            }
            catch (Exception ex)
            {
                lastException = ex;
                ModBase.Log(ex, $"[Fetch] 获取文件内容失败，尝试下一个源：{url}", ModBase.LogLevel.Debug);
            }
        }

        throw new Exception("无法获取文件内容", lastException);
    }

    public static string NetRequestRetry(string url, string method, string data = "", string? contentType = null,
        Encoding? encoding = null, string? accept = null, bool useBrowserUserAgent = false)
    {
        return Requester.Fetch(url, new FetchParam
        {
            Method = method,
            Content = data,
            ContentType = contentType,
            Encoding = encoding,
            Accept = accept,
            UseBrowserUserAgent = useBrowserUserAgent,
            Timeout = 30000
        });
    }

    public static string NetRequestOnce(string url, string method, string data = "", string? contentType = null,
        Encoding? encoding = null, string? accept = null, bool useBrowserUserAgent = false)
    {
        return NetRequestRetry(url, method, data, contentType, encoding, accept, useBrowserUserAgent);
    }

    public static Task NetDownloadByClient(string url, string localFile, bool useBrowserUserAgent = false)
    {
        return FileDownloader.Download(url, localFile, useBrowserUserAgent);
    }

    public static void NetDownloadByLoader(string url, string localFile, ModLoader.LoaderBase? loaderToSyncProgress = null,
        ModBase.FileChecker? check = null, bool useBrowserUserAgent = false)
    {
        FileDownloader.Download(url, localFile, useBrowserUserAgent).GetAwaiter().GetResult();
    }

    public static void NetDownloadByLoader(IEnumerable<string> urls, string localFile,
        ModLoader.LoaderBase? loaderToSyncProgress = null, ModBase.FileChecker? check = null,
        bool useBrowserUserAgent = false)
    {
        FileDownloader.Download(urls, localFile, useBrowserUserAgent).GetAwaiter().GetResult();
    }

    public static bool HasDownloadingTask(bool ignoreCustomDownload = false)
    {
        foreach (var task in ModLoader.loaderTaskbar.ToList())
        {
            if (task.show && task.State == ModBase.LoadState.Loading &&
                (!ignoreCustomDownload || !task.name.Contains("自定义下载")))
                return true;
        }
        return false;
    }
}
