using System.Net;
using System.Net.Http;
using PCL.Core.App;
using PCL.Core.IO.Download;
using PCL.Core.IO.Net;

namespace PCL.Network.Downloader;

/// <summary>
/// Parameters for creating a download connection.
/// </summary>
public record DownloadSourceParams(string Url, bool UseBrowserUserAgent, string CustomUserAgent);

/// <summary>
/// Factory that wires HttpDlConnection + FileDlWriter for PCL's HTTP download needs.
/// </summary>
public class PclDlFactory : NDlFactory
{
    public IDlConnection CreateConnection(DownloadSourceParams source)
    {
        var client = NetworkService.GetClient();
        return new HttpDlConnection(client, source.Url, request =>
        {
            request.Version = HttpVersion.Version20;
            request.VersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
            if (source.Url.Contains("api.curseforge.com"))
                request.Headers.Add("x-api-key", Secrets.CurseForgeAPIKey);
            var userAgent = !string.IsNullOrEmpty(source.CustomUserAgent)
                ? source.CustomUserAgent
                : source.UseBrowserUserAgent
                    ? $"PCL2/{ModBase.upstreamVersion}.{ModBase.versionBranchCode} PCLN/{ModBase.versionStandardCode} Mozilla/5.0 AppleWebKit/537.36 (KHTML, like Gecko) Chrome/136.0.0.0 Safari/537.36 Edg/136.0.0.0"
                    : $"PCL2/{ModBase.upstreamVersion}.{ModBase.versionBranchCode} PCLN/{ModBase.versionStandardCode}";
            request.Headers.Add("User-Agent", userAgent);
        });
    }

    public IDlWriter MakeWriter(string target)
        => new FileDlWriter(target);

    public override IDlConnection? CreateConnection(string resId)
        => null;

    public override IDlWriter? CreateWriter(string resId)
        => null;
}
