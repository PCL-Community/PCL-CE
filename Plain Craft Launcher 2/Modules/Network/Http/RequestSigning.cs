using System.Net;
using System.Net.Http;

namespace PCL.Network;

public static class RequestSigning
{
    internal static string SecretCdnSign(string UrlWithMark)
    {
        if (!UrlWithMark.EndsWithF("{CDN}"))
            return UrlWithMark;
        return UrlWithMark.Replace("{CDN}", "").Replace(" ", "%20");
    }
    
    /// <summary>
    ///     设置 Headers 的 UA、Referer。
    /// </summary>
    internal static void SecretHeadersSign(string Url, ref HttpRequestMessage Client, bool UseBrowserUserAgent = false,
        string CustomUserAgent = "")
    {
        Client.Version = HttpVersion.Version20;
        Client.VersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
        if (Url.Contains("api.curseforge.com"))
            Client.Headers.Add("x-api-key", ModSecret.CurseForgeAPIKey);
        var userAgent = !string.IsNullOrEmpty(CustomUserAgent)
            ? CustomUserAgent
            : UseBrowserUserAgent
                ? $"PCL2/{ModBase.UpstreamVersion}.{ModBase.VersionBranchCode} PCLCE/{ModBase.VersionStandardCode} Mozilla/5.0 AppleWebKit/537.36 (KHTML, like Gecko) Chrome/136.0.0.0 Safari/537.36 Edg/136.0.0.0"
                : $"PCL2/{ModBase.UpstreamVersion}.{ModBase.VersionBranchCode} PCLCE/{ModBase.VersionStandardCode}";
        Client.Headers.Add("User-Agent", userAgent);
    }
}