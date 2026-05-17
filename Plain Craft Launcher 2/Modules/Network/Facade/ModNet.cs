using System.IO;
using System.Text;

namespace PCL.Network;

public static class ModNet
{
    public const string NetDownloadEnd = ".PCLDownloading";
    public static int NetTaskThreadLimit { get; set; } = 16;
    public static long NetTaskSpeedLimitLow { get; set; } = 256 * 1024L;
    public static long NetTaskSpeedLimitHigh { get; set; } = -1;
    public static long NetTaskSpeedLimitLeft { get; set; } = -1;
    public static int NetTaskThreadCount { get; set; }
    public static NetManager NetManager => NetManager.Instance;

    public static object NetGetCodeByRequestRetry(string url, Encoding? Encode = null, string Accept = "",
        bool IsJson = false, string? BackupUrl = null, bool UseBrowserUserAgent = false)
    {
        var param = new RequestParam
        {
            Encoding = Encode,
            Accept = Accept,
            FallbackUrl = BackupUrl,
            UseBrowserUserAgent = UseBrowserUserAgent,
            Timeout = 30000,
            Retries = 3
        };
        var result = Requester.FetchString(url, param);
        return IsJson ? ModBase.GetJson(result) : result;
    }

    public static object NetGetCodeByRequestOnce(string url, Encoding? Encode = null, int Timeout = 30000,
        bool IsJson = false, string Accept = "", bool UseBrowserUserAgent = false)
    {
        var param = new RequestParam
        {
            Encoding = Encode,
            Accept = Accept,
            UseBrowserUserAgent = UseBrowserUserAgent,
            Timeout = Timeout,
            Retries = 1
        };
        var result = Requester.FetchString(url, param);
        return IsJson ? ModBase.GetJson(result) : result;
    }

    public static string NetGetCodeByLoader(string url, int Timeout = 45000, bool IsJson = false,
        bool UseBrowserUserAgent = false)
    {
        return NetGetCodeByLoader(new[] { url }, Timeout, IsJson, UseBrowserUserAgent);
    }

    public static string NetGetCodeByLoader(IEnumerable<string> urls, int Timeout = 45000, bool IsJson = false,
        bool UseBrowserUserAgent = false)
    {
        Exception? lastException = null;

        foreach (var url in urls)
        {
            try
            {
                // 使用原生的 Requester.Fetch，它支持 HttpClient 的自动解压功能，并直接返回字符。
                var content = Requester.Fetch(url, new FetchParam
                {
                    Method = "GET",
                    Timeout = Timeout,
                    UseBrowserUserAgent = UseBrowserUserAgent
                });
                
                return IsJson ? ModBase.GetJson(content).ToString() : content;
            }
            catch (Exception ex)
            {
                lastException = ex;
                // 如果是多个链接用于重试，这里捕获异常并继续尝试下一个源
                ModBase.Log(ex, $"[Fetch] 获取文本/JSON失败，尝试下一个源：{url}", ModBase.LogLevel.Debug);
            }
        }

        throw new Exception("所有的获取源均已失败，无法拿到文本数据。", lastException);
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

    public static bool HasDownloadingTask(bool IgnoreCustomDownload = false)
    {
        foreach (var task in ModLoader.LoaderTaskbar.ToList())
        {
            if (task.Show && task.State == ModBase.LoadState.Loading &&
                (!IgnoreCustomDownload || !task.Name.Contains("自定义下载")))
                return true;
        }
        return false;
    }
}
