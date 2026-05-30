using System.Net.Http;
using PCL.Core.App;
using PCL.Core.Utils;
using PCL.Network;
using PCL.Network.Loaders;
using PCL.Core.IO.Net.Http;

namespace PCL;

public class UpdatesMirrorChyanModel : IUpdateSource // Mirror 酱的更新格式
{
    private const string mirrorChyanBaseUrl =
        "https://mirrorchyan.com/api/resources/{cid}/latest?cdk={cdk}&os=win&arch={arch}&channel={channel}";

    private const string myCid = "PCL2-CE";
    public string SourceName { get; set; } = "MirrorChyan";

    public bool IsAvailable()
    {
        return !string.IsNullOrWhiteSpace(Config.Update.MirrorChyanKey);
    }

    public VersionDataModel GetLatestVersion(UpdateChannel channel, UpdateArch arch)
    {
        using (var response = HttpRequest.Create(GetUrl(channel, arch))
                   .SendAsync()
                   .GetAwaiter()
                   .GetResult())
        {
            var ret = (JsonObject)ModBase.GetJson(response.AsString());
            if ((int)ret["code"] != 0)
                throw new Exception("Mirror 酱获取数据不成功");
            var data = ret["data"];
            var upd_url = data["url"]?.ToString();
            if (data is not null && string.IsNullOrWhiteSpace(upd_url))
                throw new Exception("无效 CDK");
            return new VersionDataModel
            {
                source = SourceName,
                versionCode = (int)data["version_number"],
                versionName = (string)data["version_name"],
                sHA256 = (string)data["sha256"],
                changelog = (string)data["release_note"]
            };
        }
    }

    public bool RefreshCache()
    {
        return true;
    }

    public bool IsLatest(UpdateChannel channel, UpdateArch arch, SemVer currentVersion, int currentVersionCode)
    {
        var latest = GetLatestVersion(channel, arch);
        return currentVersion >= SemVer.Parse(latest.versionName);
    }

    public VersionAnnouncementDataModel GetAnnouncementList()
    {
        throw new Exception("Mirror 酱无公告系统");
    }

    public List<ModLoader.LoaderBase> GetDownloadLoader(UpdateChannel channel, UpdateArch arch, string output)
    {
        var loaders = new List<ModLoader.LoaderBase>();
        loaders.Add(new ModLoader.LoaderTask<int, List<DownloadFile>>("获取下载信息", load =>
        {
            var ret = (JsonObject)Requester.FetchJson(GetUrl(channel, arch), RequestParam.WithRetry);
            var dlUrl = ret["data"]["url"]?.ToString();
            if (dlUrl is null)
                throw new Exception("Mirror 酱下载源不可用");
            load.output = new List<DownloadFile> { new(new[] { dlUrl }, output) };
        }));
        loaders.Add(new LoaderDownload("下载更新文件", new List<DownloadFile>()));
        return loaders;
    }

    private string GetUrl(UpdateChannel channel, UpdateArch arch)
    {
        var reqUrl = mirrorChyanBaseUrl;
        reqUrl = reqUrl.Replace("{cid}", myCid);
        reqUrl = reqUrl.Replace("{cdk}", Config.Update.MirrorChyanKey);
        reqUrl = reqUrl.Replace("{arch}", arch.ToString());
        reqUrl = reqUrl.Replace("{channel}", channel.ToString());
        return reqUrl;
    }
}
