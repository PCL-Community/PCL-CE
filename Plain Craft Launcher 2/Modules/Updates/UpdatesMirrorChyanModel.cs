using System.Net.Http;
using Newtonsoft.Json.Linq;
using PCL.Core.App;
using PCL.Core.Utils;
using PCL.Network;
using PCL.Network.Loaders;
using PCL.Core.IO.Net.Http;

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
        using (var response = HttpRequest.Create(GetUrl(channel, arch))
                   .SendAsync()
                   .GetAwaiter()
                   .GetResult())
        {
            var ret = (JObject)ModBase.GetJson(response.AsString());
            if ((int)ret["code"] != 0)
                throw new Exception("Mirror 酱获取数据不成功");
            var data = ret["data"];
            var upd_url = data["url"]?.ToString();
            if (data is not null && string.IsNullOrWhiteSpace(upd_url))
                throw new Exception("无效 CDK");
            return new VersionDataModel
            {
                Source = SourceName,
                VersionCode = (int)data["version_number"],
                VersionName = (string)data["version_name"],
                SHA256 = (string)data["sha256"],
                Changelog = (string)data["release_note"]
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
        return currentVersion >= SemVer.Parse(latest.VersionName);
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
            var ret = (JObject)Requester.FetchJson(GetUrl(channel, arch), RequestParam.WithRetry);
            var dlUrl = ret["data"]["url"]?.ToString();
            if (dlUrl is null)
                throw new Exception("Mirror 酱下载源不可用");
            load.Output = new List<DownloadFile> { new(new[] { dlUrl }, output) };
        }));
        loaders.Add(new LoaderDownload("下载更新文件", new List<DownloadFile>()));
        return loaders;
    }

    private string GetUrl(UpdateChannel channel, UpdateArch arch)
    {
        var ReqUrl = MirrorChyanBaseUrl;
        ReqUrl = ReqUrl.Replace("{cid}", MyCid);
        ReqUrl = ReqUrl.Replace("{cdk}", Config.Update.MirrorChyanKey);
        ReqUrl = ReqUrl.Replace("{arch}", arch.ToString());
        ReqUrl = ReqUrl.Replace("{channel}", channel.ToString());
        return ReqUrl;
    }
}
