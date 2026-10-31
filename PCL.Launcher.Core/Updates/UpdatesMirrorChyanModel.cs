using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.IO.Net.Http;
using PCL.Core.Utils;
using PCL.Network;
using PCL.Network.Loaders;

namespace PCL;

public class UpdatesMirrorChyanModel : IUpdateSource // Mirror 酱的更新格式
{
    private const string MirrorChyanBaseUrl =
        "https://mirrorchyan.com/api/resources/{cid}/latest?cdk={cdk}&os=win&arch={arch}&channel={channel}";

    private const string MyCid = "PCL2-CE";
    public string SourceName { get; set; } = "MirrorChyan";

    public bool IsAvailable()
    {
        return !string.IsNullOrWhiteSpace(Config.Update.MirrorChyanKey);
    }

    public VersionDataModel GetLatestVersion(UpdateChannel channel, UpdateArch arch)
    {
        using var response = HttpRequest
            .Create(GetUrl(channel, arch))
            .SendAsync()
            .GetAwaiter()
            .GetResult();
        var ret = (JsonObject)LauncherCommon.GetJson(response.AsString());
        if ((int)ret["code"] != 0)
            throw new Exception("Mirror 酱获取数据不成功");
        var data = ret["data"];
        var updUrl = data["url"]?.ToString();
        if (data is not null && string.IsNullOrWhiteSpace(updUrl))
            throw new Exception("无效 CDK");
        return new VersionDataModel
        {
            Source = SourceName,
            VersionCode = (int)data["version_number"],
            VersionName = (string)data["version_name"],
            Sha256 = (string)data["sha256"],
            Changelog = (string)data["release_note"]
        };
    }

    public bool RefreshCache()
    {
        return true;
    }

    public bool IsLatest(
        UpdateChannel channel,
        UpdateArch arch,
        SemVer currentVersion,
        int currentVersionCode)
    {
        var latest = GetLatestVersion(channel, arch);
        return currentVersion >= SemVer.Parse(latest.VersionName);
    }

    public VersionAnnouncementDataModel GetAnnouncementList()
    {
        throw new Exception("Mirror 酱无公告系统");
    }

    public List<Loading.LoaderBase> GetDownloadLoader(
        UpdateChannel channel,
        UpdateArch arch,
        string output)
    {
        var loaders = new List<Loading.LoaderBase>
        {
            new Loading.LoaderTask<int, List<DownloadFile>>(
                Lang.Text("Update.Task.GetDownloadInfo"),
                load =>
                {
                    var ret = (JsonObject)Requester.FetchJson(
                        GetUrl(channel, arch),
                        RequestParam.WithRetry);
                    var dlUrl = ret["data"]["url"]?.ToString();
                    if (dlUrl is null)
                        throw new Exception("Mirror 酱下载源不可用");
                    load.output =
                    [
                        new DownloadFile(
                            [
                                dlUrl
                            ],
                            output)
                    ];
                }),
            new LoaderDownload(Lang.Text("Update.Task.DownloadUpdateFile"), [])
        };
        return loaders;
    }

    private static string GetUrl(UpdateChannel channel, UpdateArch arch)
    {
        var reqUrl = MirrorChyanBaseUrl;
        reqUrl = reqUrl.Replace("{cid}", MyCid);
        reqUrl = reqUrl.Replace("{cdk}", Config.Update.MirrorChyanKey);
        reqUrl = reqUrl.Replace("{arch}", arch.ToString());
        reqUrl = reqUrl.Replace("{channel}", channel.ToString());
        return reqUrl;
    }
}