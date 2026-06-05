using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json.Serialization;
using PCL.Core.App;
using PCL.Core.App.Localization;
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
        return currentVersion >= SemVer.Parse(latestVersion.VersionName);
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
        loaders.Add(new ModLoader.LoaderTask<int, List<DownloadFile>>(Lang.Text("Update.Task.GetVersionInfo"), load =>
        {
            var channelName = GetChannelName(channel, arch);
            var deJsonData = GetRemoteInfoByName($"updates-{channelName}", "updates/")
                ?.ToObject<MinioUpdateModel>()
                ?.Assets
                ?.FirstOrDefault();
            if (deJsonData is null)
                throw new Exception("No assets can download!");
            var selfSha256 = ModBase.GetFileSHA256(Basics.ExecutablePath);
            var remoteUpdSha256 = deJsonData.Sha256;
            var patchFileName = $"{selfSha256}_{remoteUpdSha256}.patch";
            if (deJsonData.Patches.Contains(patchFileName))
            {
                patchUpdate = true;
                tempPath += patchFileName;
                load.output = new List<DownloadFile>
                    { new(new[] { $"{_baseUrl}static/patch/{patchFileName}" }, tempPath) };
            }
            else
            {
                patchUpdate = false;

                tempPath += $"{deJsonData.Sha256}.bin";
                load.output = new List<DownloadFile> { new(RandomUtils.Shuffle(deJsonData.Downloads), tempPath) };
            }
        }));
        loaders.Add(new LoaderDownload(Lang.Text("Update.Task.DownloadFile"), new List<DownloadFile>()));
        loaders.Add(new ModLoader.LoaderTask<string, int>(Lang.Text("Update.Task.ApplyFile"), _ =>
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
                        throw new Exception(Lang.Text("Update.Error.FileNotFound"));

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
        var deJsonData = GetRemoteInfoByName($"updates-{channelName}", "updates/")?.ToObject<MinioUpdateModel>().Assets
            .FirstOrDefault();
        if (deJsonData is null)
            throw new NullReferenceException("Can not get remote update info!");
        return new VersionDataModel
        {
            VersionName = deJsonData.Version.Name,
            VersionCode = deJsonData.Version.Code,
            Sha256 = deJsonData.Sha256,
            Source = SourceName,
            Changelog = deJsonData.Changelog
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
        [JsonPropertyName("assets")]
        public List<MinioUpdateAsset> Assets { get; set; }
    }

    private class MinioUpdateAsset
    {
        [JsonPropertyName("file_name")]
        public string FileName { get; set; }

        [JsonPropertyName("version")]
        public MinioUpdateAssetVersionInfo Version { get; set; }

        [JsonPropertyName("upd_time")]
        public string UpdTime { get; set; }

        [JsonPropertyName("downloads")]
        public List<string> Downloads { get; set; }

        [JsonPropertyName("patches")]
        public List<string> Patches { get; set; }

        [JsonPropertyName("sha256")]
        public string Sha256 { get; set; }

        [JsonPropertyName("changelog")]
        public string Changelog { get; set; }
    }

    private class MinioUpdateAssetVersionInfo
    {
        [JsonPropertyName("channel")]
        public string Channel { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("code")]
        public int Code { get; set; }
    }
}