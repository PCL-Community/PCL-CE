using PCL.Core.App;
using PCL.Core.IO.Download.Core;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;

namespace PCL.Core.IO.Download;

public static class DownloadService
{
    // I think this is not a good design, because as long as the application running,
    // the cache memory size will grow without limit (if there are many download job has different http options)
    // But I found that CacheCow.Client nupkg is only applied on 'Plain Craft Launcher 2' project. I cannot use it in 'PCL.Core'
    // So I give up designing a more complete cache. Maybe this is not a big problem?
    private static readonly Dictionary<int, HttpClient> _HttpClient = [];

    // NOTE:
    // This will make a problem, which is user's changing action cannot apply in runtime
    // That mean if user want to apply their changing action, they should reboot this application
    private static readonly SemaphoreSlim _GlobalThrottle =
        new(Config.Download.ThreadLimit, Config.Download.ThreadLimit);

    public static DownloadClient CreateJob(DownloadOptions options)
    {
        var httpClient = _GetOrCreateHttpClient(options);
        var downloadClient = new DownloadClient(options, httpClient, _GlobalThrottle);

        return downloadClient;
    }

    private record HttpClientOptions(int MaxConcurrentWorkers, TimeSpan TimeOut);

    private static HttpClient _GetOrCreateHttpClient(DownloadOptions options)
    {
        var key = (new HttpClientOptions(options.MaxConcurrentWorkers, options.TimeOut)).GetHashCode();
        if (_HttpClient.TryGetValue(key, out var client))
        {
            return client;
        }

        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            MaxConnectionsPerServer = options.MaxConcurrentWorkers * 2,
            ConnectTimeout = options.TimeOut
        };
        var newClient = new HttpClient(handler) { Timeout = options.TimeOut };
        _HttpClient[key] = newClient;

        return newClient;
    }
}