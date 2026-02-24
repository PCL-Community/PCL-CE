using PCL.Core.App;
using PCL.Core.IO.Download.Core;
using System;
using System.Net.Http;
using System.Threading;

namespace PCL.Core.IO.Download;

public static class DownloadService
{
    // NOTE:
    // This will make a problem, which is user's changing action cannot apply in runtime
    // That mean if user want to apply their changing action, they should reboot this application
    private static readonly SemaphoreSlim _GlobalThrottle =
        new(Config.Download.ThreadLimit, Config.Download.ThreadLimit);

    public static DownloadClient CreateJob(DownloadOptions options, Func<HttpClient> httpClientFactory)
    {
        var httpClient = httpClientFactory();
        var downloadClient = new DownloadClient(options, _GlobalThrottle, httpClient);

        return downloadClient;
    }
}