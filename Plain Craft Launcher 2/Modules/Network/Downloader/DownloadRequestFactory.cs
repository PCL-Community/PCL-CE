using System.Net.Http;
using Downloader;

namespace PCL.Network;

internal static class DownloadRequestFactory
{
    internal static RequestConfiguration Create(string url, bool useBrowserUserAgent, string customUserAgent = "")
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        ModSecret.SecretHeadersSign(url, ref request, useBrowserUserAgent, customUserAgent);
        try
        {
            var configuration = new RequestConfiguration();
            if (request.Headers.UserAgent.Count > 0)
                configuration.UserAgent = request.Headers.UserAgent.ToString();
            foreach (var header in request.Headers)
                if (!header.Key.Equals("User-Agent", StringComparison.OrdinalIgnoreCase))
                    configuration.Headers.Add($"{header.Key}: {string.Join(", ", header.Value)}");
            return configuration;
        }
        finally
        {
            request.Dispose();
        }
    }
}
