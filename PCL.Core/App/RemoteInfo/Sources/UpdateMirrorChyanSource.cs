using System;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Net.Downloader;
using PCL.Core.Net.Http.Client;

namespace PCL.Core.App.RemoteInfo.Sources;

public class UpdateMirrorChyanSource : IUpdateSource
{
    #region Data Models

    private record VersionResponseModel(
        [property: JsonPropertyName("code")] int Code,
        [property: JsonPropertyName("data")] VersionDataModel Data
    );
    
    private record VersionDataModel(
        [property: JsonPropertyName("url")] string Url,
        [property: JsonPropertyName("version_number")] int VersionNumber,
        [property: JsonPropertyName("version_name")] string VersionName,
        [property: JsonPropertyName("release_note")] string ReleaseNote,
        [property: JsonPropertyName("sha256")] string Sha256
    );

    #endregion
    
    private const string BaseUrl = 
        "https://mirrorchyan.com/api/resources/{cid}/latest?cdk={cdk}&os=win&arch={arch}&channel={channel}";

    private const string Cid = "PCL2-CE";
    
    private readonly string _cdk = Config.System.MirrorChyanKey;
    
    public bool IsAvailable => !string.IsNullOrWhiteSpace(_cdk);

    public string SourceName => "MirrorChyan";

    public async Task<VersionData> GetLatestVersionAsync()
    {
        var response = await HttpRequestBuilder.Create(_GetRequestUrl(), HttpMethod.Get).SendAsync();
        var jsonData = await response.AsJsonAsync<VersionResponseModel>();
        if (jsonData is null || jsonData.Code != 0 || jsonData.Data is null)
        {
            throw new HttpRequestException("获取版本信息失败，响应数据无效");
        }
        var data = jsonData.Data;
        if (data is null)
        {
            throw new HttpRequestException("获取版本信息失败，响应数据无效");
        }
        return new VersionData(
            data.VersionNumber,
            data.VersionName,
            data.ReleaseNote,
            data.Sha256);
    }

    public Task<AnnouncementsListModel> GetAnnouncementAsync()
    {
        throw new NotSupportedException("MirrorChyan 更新源不支持公告功能");
    }

    public async Task<bool> DownloadAsync(string outputPath)
    {
        var response = await HttpRequestBuilder.Create(_GetRequestUrl(), HttpMethod.Get).SendAsync();
        var jsonData = await response.AsJsonAsync<VersionResponseModel>();
        if (jsonData is null || jsonData.Code != 0 || jsonData.Data is null)
        {
            throw new HttpRequestException("获取公告信息失败，响应数据无效");
        }
        var url = jsonData.Data.Url;
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new HttpRequestException("获取公告信息失败，响应数据无效");
        }

        var downloadTask = new DownloadTask(new Uri(url), outputPath);
        var downloadManager = new DownloadManager(new FastMirrorSelector(new HttpClient()));
        await downloadManager.DownloadAsync(downloadTask, CancellationToken.None);
        return true;
    }
    
    private string _GetRequestUrl()
    {
        var channel = Config.System.Update.UpdateChannel switch
        {
            0 => "stable",
            1 => "beta",
            _ => "stable"
        };
        return BaseUrl
            .Replace("{cid}", Cid)
            .Replace("{cdk}", _cdk)
            .Replace("{arch}", RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64")
            .Replace("{channel}", channel);
    }
}