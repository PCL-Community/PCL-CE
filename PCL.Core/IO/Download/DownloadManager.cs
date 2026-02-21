using PCL.Core.IO.Download.Core;
using System;
using System.Collections.Generic;
using System.Net.Http;

namespace PCL.Core.IO.Download;

public static class DownloadManager
{
    /*
     * TODO:
     * - [] Global worker limit
     * - [x] HttpClient cache keyed by options (dismiss download url)
     * - [x] Global mirror health management (shared across downloads, maybe in a static class or singleton)
     */

    private static int _currentActiveWorker = 0;
    private static readonly Dictionary<int, HttpClient> _HttpClient = [];

    public static DownloadClient CreateJob(DownloadOptions options)
    {
        var httpClient = _GetOrCreateHttpClient(options);
        var downloadClient = new DownloadClient(options, httpClient);

        // TODO: Consider adding the download client to a global registry for better management (e.g., for global worker limits, mirror health tracking, etc.)
        // and limit StartAsync to only started by Manager

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