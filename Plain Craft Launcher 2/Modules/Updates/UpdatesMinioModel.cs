using System.IO;
using System.IO.Compression;
using System.Net.Http;
using PCL.Core.App;
using PCL.Core.Utils;
using PCL.Core.Utils.Diff;
using PCL.Network;
using PCL.Network.Loaders;
using PCL.Core.IO.Net.Http;

namespace PCL;

public class UpdatesMinioModel : IUpdateSource // 社区自己的更新系统格式
{
    private readonly string _baseUrl;

    private Dictionary<string, string> _remoteCache;

    public UpdatesMinioModel(string baseUrl, string name = "Minio")
    {
        _baseUrl = baseUrl;
        SourceName = name;
    }

    public string SourceName { get; set; }

    public bool IsAvailable()
    {
        return !string.IsNullOrWhiteSpace(_baseUrl);
    }

    public bool RefreshCache()
    {
        // 先检查缓存
        var remoteCache =
            ModBase.GetJson(Requester.FetchString($"{_baseUrl}apiv2/cache.json", RequestParam.WithRetry));
        _remoteCache = remoteCache.ToObject<Dictionary<string, string>>();
        return true;
    }

    public VersionDataModel GetLatestVersion(UpdateChannel channel, UpdateArch arch)
    {
        if (_remoteCache is null)
            RefreshCache();
        // 确定版本通道名称
        return GetChannelInfo(channel, arch);
    }

    public bool IsLatest(UpdateChannel channel, UpdateArch arch, SemVer currentVersion, int currentVersionCode)
    {
        if (_remoteCache is null)
            RefreshCache();
        var latestVersion = GetChannelInfo(channel, arch);
        return currentVersion >= SemVer.Parse(latestVersion.versionName);
    }

    public VersionAnnouncementDataModel GetAnnouncementList()
    {
        if (_remoteCache is null)
            RefreshCache();
        var deJsonData = GetRemoteInfoByName("announcement")?.ToObject<VersionAnnouncementDataModel>();
        if (deJsonData is null)
            throw new NullReferenceException("Can not get remote announcement info!");
        return deJsonData;
    }

    public List<ModLoader.LoaderBase> GetDownloadLoader(UpdateChannel channel, UpdateArch arch, string output)
    {
        if (_remoteCache is null)
            RefreshCache();
        var loaders = new List<ModLoader.LoaderBase>();
        var patchUpdate = true;
        var tempPath = $@"{ModBase.pathTemp}Cache\Update\Download\";
        loaders.Add(new ModLoader.LoaderTask<int, List<DownloadFile>>("获取版本信息", load =>
        {
            var channelName = GetChannelName(channel, arch);
            var deJsonData = GetRemoteInfoByName($"updates-{channelName}", "updates/")
                ?.ToObject<MinioUpdateModel>()
                ?.assets
                ?.FirstOrDefault();
            if (deJsonData is null)
                throw new Exception("No assets can download!");
            var selfSha256 = ModBase.GetFileSHA256(Basics.ExecutablePath);
            var remoteUpdSha256 = deJsonData.sha256;
            var patchFileName = $"{selfSha256}_{remoteUpdSha256}.patch";
            if (deJsonData.patches.Contains(patchFileName))
            {
                patchUpdate = true;
                tempPath += patchFileName;
                load.output = new List<DownloadFile>
                    { new(new[] { $"{_baseUrl}static/patch/{patchFileName}" }, tempPath) };
            }
            else
            {
                patchUpdate = false;

                tempPath += $"{deJsonData.sha256}.bin";
                load.output = new List<DownloadFile> { new(RandomUtils.Shuffle(deJsonData.downloads), tempPath) };
            }
        }));
        loaders.Add(new LoaderDownload("下载文件", new List<DownloadFile>()));
        loaders.Add(new ModLoader.LoaderTask<string, int>("应用文件", _ =>
        {
            if (patchUpdate)
            {
                var diff = new BsDiff();
                var newFile = diff
                    .ApplyAsync(ModBase.ReadFileBytes(Basics.ExecutablePath), ModBase.ReadFileBytes(tempPath))
                    .GetAwaiter().GetResult();
                ModBase.WriteFile(output, newFile);
            }
            else
            {
                using (var fs = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var zip = new ZipArchive(fs))
                {
                    // 尝试找到目标条目
                    var entry = zip.Entries
                        .FirstOrDefault(x => x.Name.Contains("Plain Craft Launcher Community Edition.exe")) ?? zip
                        .Entries
                        .FirstOrDefault(x => x.Name.Contains("Plain Craft Launcher"));

                    entry ??= zip.Entries
                        .FirstOrDefault(x => x.Name.Contains("Launcher"));

                    entry ??= zip.Entries
                        .FirstOrDefault(x => x.Name.Contains(".exe"));

                    if (entry is null)
                        throw new Exception("找不到更新文件");

                    // 解压到指定文件（覆盖已存在文件）
                    entry.ExtractToFile(output, true);
                }
            }
        }));
        return loaders;
    }

    private VersionDataModel GetChannelInfo(UpdateChannel channel, UpdateArch arch)
    {
        var channelName = GetChannelName(channel, arch);
        var deJsonData = GetRemoteInfoByName($"updates-{channelName}", "updates/")?.ToObject<MinioUpdateModel>().assets
            .FirstOrDefault();
        if (deJsonData is null)
            throw new NullReferenceException("Can not get remote update info!");
        return new VersionDataModel
        {
            versionName = deJsonData.version.name,
            versionCode = deJsonData.version.code,
            sha256 = deJsonData.sha256,
            source = SourceName,
            changelog = deJsonData.changelog
        };
    }

    private JsonNode GetRemoteInfoByName(string name, string path = "")
    {
        var localInfoFile = Path.Combine(ModBase.pathTemp, "Cache", "Update", $"{name}.json");
        JsonNode jsonData;
        if (IsCacheValid($"{name}.json", _remoteCache[name]))
        {
            jsonData = ModBase.GetJson(ModBase.ReadFile(localInfoFile));
        }
        else
        {
            var response = HttpRequest.Create($"{_baseUrl}apiv2/{path}{name}.json")
                .SendAsync()
                .GetAwaiter()
                .GetResult();

            var content = response.AsString();
            jsonData = ModBase.GetJson(content);
            ModBase.WriteFile(localInfoFile, content);
        }

        return jsonData;
    }

    /// <summary>
    ///     缓存是否有效
    /// </summary>
    /// <param name="path"></param>
    /// <param name="hash"></param>
    /// <returns></returns>
    private bool IsCacheValid(string path, string hash)
    {
        var cacheFile = Path.Combine(ModBase.pathTemp, "Cache", "Update", path);
        var fileInfo = new FileInfo(cacheFile);
        return fileInfo.Exists && (DateTime.Now - fileInfo.LastWriteTime).TotalHours < 1 &&
               (ModBase.GetFileMD5(cacheFile) ?? "") == (hash ?? "");
    }

    private string GetChannelName(UpdateChannel channel, UpdateArch arch)
    {
        var channelName = string.Empty;
        switch (channel)
        {
            case UpdateChannel.stable:
            {
                channelName += "sr";
                break;
            }
            case UpdateChannel.beta:
            {
                channelName += "fr";
                break;
            }

            default:
            {
                channelName += "sr";
                break;
            }
        }

        switch (arch)
        {
            case UpdateArch.x64:
            {
                channelName += "x64";
                break;
            }
            case UpdateArch.arm64:
            {
                channelName += "arm64";
                break;
            }

            default:
            {
                channelName += "x64";
                break;
            }
        }

        return channelName;
    }

    private class MinioUpdateModel
    {
        public List<MinioUpdateAsset> assets { get; set; }
    }

    private class MinioUpdateAsset
    {
        public string file_name { get; set; }
        public MinioUpdateAssetVersionInfo version { get; set; }
        public string upd_time { get; set; }
        public List<string> downloads { get; set; }
        public List<string> patches { get; set; }
        public string sha256 { get; set; }
        public string changelog { get; set; }
    }

    private class MinioUpdateAssetVersionInfo
    {
        public string channel { get; set; }
        public string name { get; set; }
        public int code { get; set; }
    }
}