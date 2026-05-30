using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.IO.Net.Http;
using PCL.Core.Utils;
using PCL.Network;
using PCL.Network.Loaders;

namespace PCL;

public static class ModDownload
{
    #region DlClient* | Minecraft 客户端

    /// <summary>
    ///     返回某 Minecraft 版本对应的原版主 Jar 文件的下载信息，要求对应依赖实例已存在。
    ///     失败则抛出异常，不需要下载则返回 Nothing。
    /// </summary>
    public static DownloadFile DlClientJarGet(ModMinecraft.McInstance Version, bool ReturnNothingOnFileUseable)
    {
        // 获取底层继承实例
        try
        {
            while (!string.IsNullOrEmpty(Version.InheritInstanceName))
                Version = new ModMinecraft.McInstance(Version.InheritInstanceName);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "获取底层继承实例失败");
        }

        // 检查 Json 是否标准
        if (Version.JsonObject["downloads"] is null || Version.JsonObject["downloads"]["client"] is null ||
            Version.JsonObject["downloads"]["client"]["url"] is null)
            throw new Exception(Lang.Text("Minecraft.Download.Error.NoJarDownloadInfo", Version.Name));
        // 检查文件
        var Checker = new ModBase.FileChecker(1024L, (long)(Version.JsonObject["downloads"]["client"]["size"] ?? -1),
            (string)Version.JsonObject["downloads"]["client"]["sha1"]);
        if (ReturnNothingOnFileUseable && Checker.Check(Version.PathInstance + Version.Name + ".jar") is null)
            return null; // 通过校验
        // 返回下载信息
        var JarUrl = (string)Version.JsonObject["downloads"]["client"]["url"];
        return new DownloadFile(DlSourceLauncherOrMetaGet(JarUrl), Version.PathInstance + Version.Name + ".jar",
            Checker);
    }

    /// <summary>
    ///     返回某 Minecraft 版本对应的原版主 AssetIndex 文件的下载信息，要求对应依赖实例已存在。
    ///     若未找到，则会返回 Legacy 资源文件或 Nothing。
    /// </summary>
    public static DownloadFile DlClientAssetIndexGet(ModMinecraft.McInstance Version)
    {
        // 获取底层继承实例
        while (!string.IsNullOrEmpty(Version.InheritInstanceName))
            Version = new ModMinecraft.McInstance(Version.InheritInstanceName);
        // 获取信息
        var IndexInfo = ModMinecraft.McAssetsGetIndex(Version, true, true);
        var IndexAddress = Path.Combine(ModMinecraft.McFolderSelected, "assets", "indexes", IndexInfo["id"] + ".json");
        ModBase.Log("[Download] 实例 " + Version.Name + " 对应的资源文件索引为 " + IndexInfo["id"]);
        var IndexUrl = (string)(IndexInfo["url"] ?? "");
        if (string.IsNullOrEmpty(IndexUrl)) return null;

        return new DownloadFile(DlSourceLauncherOrMetaGet(IndexUrl), IndexAddress,
            new ModBase.FileChecker(CanUseExistsFile: false));
    }

    /// <summary>
    ///     构造补全某 Minecraft 版本的所有文件的加载器列表。失败会抛出异常。
    /// </summary>
    public static List<ModLoader.LoaderBase> DlClientFix(ModMinecraft.McInstance Version, bool CheckAssetsHash,
        AssetsIndexExistsBehaviour AssetsIndexBehaviour)
    {
        var Loaders = new List<ModLoader.LoaderBase>();

        #region 下载支持库文件

        if (ModMinecraft.ShouldIgnoreFileCheck(Version))
        {
            ModBase.Log("[Download] 已跳过所有 Libraries 检查");
        }
        else
        {
            var LoadersLib = new List<ModLoader.LoaderBase>
            {
                new ModLoader.LoaderTask<string, List<DownloadFile>>(
                    Lang.Text("Minecraft.Download.Stage.AnalyzeMissingLibraries"),
                    Task => Task.Output = ModMinecraft.McLibNetFilesFromInstance(Version)) { ProgressWeight = 1d },
                new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadLibraries"), new List<DownloadFile>())
                    { ProgressWeight = 15d }
            };
            // 构造加载器
            Loaders.Add(
                new ModLoader.LoaderCombo<string>(Lang.Text("Minecraft.Download.Stage.DownloadLibraries.MainLoader"),
                        LoadersLib)
                { Block = false, Show = false, ProgressWeight = 16d });
        }

        #endregion

        #region 下载资源文件

        if (ModMinecraft.ShouldIgnoreFileCheck(Version))
        {
            ModBase.Log("[Download] 已跳过所有 Assets 检查");
        }
        else
        {
            var LoadersAssets = new List<ModLoader.LoaderBase>();
            // 获取资源文件索引地址
            LoadersAssets.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>(
                Lang.Text("Minecraft.Download.Stage.AnalyzeAssetsIndex"), Task =>
            {
                try
                {
                    var IndexFile = DlClientAssetIndexGet(Version);
                    var IndexFileInfo = new FileInfo(IndexFile.LocalPath);
                    if (AssetsIndexBehaviour != AssetsIndexExistsBehaviour.AlwaysDownload &&
                        IndexFile.Check.Check(IndexFile.LocalPath) is null)
                        Task.Output = new List<DownloadFile>();
                    else
                        Task.Output = new List<DownloadFile> { IndexFile };
                }
                catch (Exception ex)
                {
                    throw new Exception(Lang.Text("Minecraft.Download.Error.AssetIndexAnalysisFailed"), ex);
                }
            }) { ProgressWeight = 0.5d, Show = false });
            // 下载资源文件索引
            LoadersAssets.Add(new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadAssetsIndex"),
                    new List<DownloadFile>())
                { ProgressWeight = 2d });
            // 要求独立更新索引
            if (AssetsIndexBehaviour == AssetsIndexExistsBehaviour.DownloadInBackground)
            {
                var LoadersAssetsUpdate = new List<ModLoader.LoaderBase>();
                string TempAddress = null;
                string RealAddress = null;
                LoadersAssetsUpdate.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>(
                    Lang.Text("Minecraft.Download.Stage.AnalyzeAssetsIndex.Background"), Task =>
                {
                    var BackAssetsFile = DlClientAssetIndexGet(Version);
                    RealAddress = BackAssetsFile.LocalPath;
                    TempAddress = ModBase.PathTemp + @"Cache\" + BackAssetsFile.LocalName;
                    BackAssetsFile.LocalPath = TempAddress;
                    Task.Output = new List<DownloadFile> { BackAssetsFile };
                    // 检查是否需要更新：每天只更新一次
                    if (File.Exists(RealAddress) &&
                        Math.Abs((File.GetLastWriteTime(RealAddress).Date - DateTime.Now.Date).TotalDays) < 1d)
                    {
                        ModBase.Log("[Download] 无需更新资源文件索引，取消");
                        Task.Abort();
                    }
                }));
                LoadersAssetsUpdate.Add(new LoaderDownload(
                    Lang.Text("Minecraft.Download.Stage.DownloadAssetsIndex.Background"), new List<DownloadFile>()));
                LoadersAssetsUpdate.Add(new ModLoader.LoaderTask<List<DownloadFile>, string>(
                    Lang.Text("Minecraft.Download.Stage.CopyAssetsIndex.Background"), Task =>
                {
                    ModBase.CopyFile(TempAddress, RealAddress);
                    ModLaunch.McLaunchLog("后台更新资源文件索引成功：" + TempAddress);
                }));
                var Updater = new ModLoader.LoaderCombo<string>(
                    Lang.Text("Minecraft.Download.Stage.UpdateAssetsIndex.Background"), LoadersAssetsUpdate);
                ModBase.Log("[Download] 开始后台检查资源文件索引");
                Updater.Start();
            }

            // 获取资源文件地址
            LoadersAssets.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>(
                Lang.Text("Minecraft.Download.Stage.AnalyzeMissingAssets"), Task =>
            {
                ModLoader.LoaderBase argprogressFeed = Task;
                Task.Output = ModMinecraft.McAssetsFixList(Version, CheckAssetsHash, ref argprogressFeed);
                Task = (ModLoader.LoaderTask<string, List<DownloadFile>>)argprogressFeed;
            })
            {
                ProgressWeight = 3d
            });
            // 下载资源文件
            LoadersAssets.Add(
                new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadAssets"), new List<DownloadFile>())
                    { ProgressWeight = 25d });
            // 构造加载器
            Loaders.Add(
                new ModLoader.LoaderCombo<string>(Lang.Text("Minecraft.Download.Stage.DownloadAssets.MainLoader"),
                        LoadersAssets)
                { Block = false, Show = false, ProgressWeight = 30.5d });
        }

        #endregion

        return Loaders;
    }

    public enum AssetsIndexExistsBehaviour
    {
        /// <summary>
        ///     如果文件存在，则不进行下载。
        /// </summary>
        DontDownload,

        /// <summary>
        ///     如果文件存在，则启动新的下载加载器进行独立的更新。
        /// </summary>
        DownloadInBackground,

        /// <summary>
        ///     如果文件存在，也同样进行下载。
        /// </summary>
        AlwaysDownload
    }

    #endregion

    #region DlClientList | Minecraft 客户端 版本列表

    /// <summary>
    ///     所有正式版的 Minecraft Drop 序数。
    ///     若从未完成过获取，返回 Nothing；否则必定存在元素，且从高到低排列。
    /// </summary>
    public static List<int> AllDrops
    {
        get
        {
            lock (_allDropsLock)
            {
                if (_allDrops is null)
                {
                    var rawData = States.Game.Drops;
                    if (string.IsNullOrEmpty(rawData))
                        _allDrops = new List<int>();
                    else
                        _allDrops = rawData.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(d => (int)Math.Round(ModBase.Val(d))).ToList();
                }

                return _allDrops.Count != 0 ? _allDrops : null;
            }
        }
        set
        {
            lock (_allDropsLock)
            {
                _allDrops = value;
                States.Game.Drops = value.Join(",");
            }
        }
    }

    private static List<int> _allDrops;
    private static readonly object _allDropsLock = new();

    // 主加载器
    public struct DlClientListResult
    {
        /// <summary>
        ///     数据来源名称，如“Mojang”，“BMCLAPI”。
        /// </summary>
        public string SourceName;

        /// <summary>
        ///     是否为官方的实时数据。
        /// </summary>
        public bool IsOfficial;

        /// <summary>
        ///     获取到的 Json 数据。
        /// </summary>
        public JsonObject Value;
        // ''' <summary>
        // ''' 官方源的失败原因。若没有则为 Nothing。
        // ''' </summary>
        // Public OfficialError As Exception
    }

    /// <summary>
    ///     Minecraft 客户端 版本列表，主加载器。
    ///     若要求镜像源必须包含某个版本，则将该版本 ID 作为输入（#5195）。
    /// </summary>
    public static ModLoader.LoaderTask<string, DlClientListResult> DlClientListLoader =
        new("DlClientList Main", DlClientListMain);

    private static void DlClientListMain(ModLoader.LoaderTask<string, DlClientListResult> loader)
    {
        switch (Config.Download.VersionListSource)
        {
            case 0:
            {
                DlSourceLoader(loader,
                    new List<KeyValuePair<ModLoader.LoaderTask<string, DlClientListResult>, int>>
                        { new(DlClientListBmclapiLoader, 30), new(DlClientListMojangLoader, 30 + 60) },
                    loader.IsForceRestarting);
                break;
            }
            case 1:
            {
                DlSourceLoader(loader,
                    new List<KeyValuePair<ModLoader.LoaderTask<string, DlClientListResult>, int>>
                        { new(DlClientListMojangLoader, 5), new(DlClientListBmclapiLoader, 5 + 30) },
                    loader.IsForceRestarting);
                break;
            }

            default:
            {
                DlSourceLoader(loader,
                    new List<KeyValuePair<ModLoader.LoaderTask<string, DlClientListResult>, int>>
                        { new(DlClientListMojangLoader, 60), new(DlClientListBmclapiLoader, 60 + 60) },
                    loader.IsForceRestarting);
                break;
            }
        }

        // 提取所有 Drop 序数
        var drops = new List<int>();
        foreach (JsonObject version in loader.Output.Value["versions"].AsArray())
            drops.Add(ModMinecraft.McInstanceInfo.VersionToDrop((string)version["id"]));
        AllDrops = drops.Distinct().OrderByDescending(d => d).ToList();
    }

    // 各个下载源的分加载器
    /// <summary>
    ///     Minecraft 客户端 版本列表，Mojang 官方源加载器。
    /// </summary>
    public static ModLoader.LoaderTask<string, DlClientListResult> DlClientListMojangLoader =
        new("DlClientList Mojang", DlClientListMojangMain);

    private static bool IsNewClientVersionHinted = false;

    // MC 更新提示
    private static bool _DlClientListMojangMain_IsHinted;

    private static void DlClientListMojangMain(ModLoader.LoaderTask<string, DlClientListResult> Loader)
    {
        var StartTime = TimeUtils.GetTimeTick();
        var Json = (JsonObject)Requester.FetchJson("https://launchermeta.mojang.com/mc/game/version_manifest.json");
        try
        {
            var Versions = (JsonArray)Json["versions"];
            if (Versions.Count < 200)
                throw new Exception(Lang.Text("Minecraft.Download.Error.VersionListOperationFailed", "Mojang", Json));
            // 添加 UVMC 项
            var CacheFilePath = ModBase.PathTemp + @"Cache\uvmc-download.json";
            if (!File.Exists(CacheFilePath))
                try
                {
                    var UnlistedJson = (JsonObject)Requester.FetchJson(
                        "https://alist.8mi.tech/d/mirror/unlisted-versions-of-minecraft/Auto/version_manifest.json");
                    File.WriteAllText(CacheFilePath, UnlistedJson.ToString());
                }
                catch (Exception ex)
                {
                    ModBase.Log("[Download] 未列出的版本官方源下载失败: " + ex.Message);
                }

            try
            {
                var CachedJson = (JsonObject)ModBase.GetJson(ModBase.ReadFile(CacheFilePath));
                Versions.Merge(CachedJson["versions"]);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "[Download] UVMC 列表加载失败，忽略列表内容");
            }

            // 确定官方源是否可用
            if (!DlPreferMojang)
            {
                var DeltaTime = TimeUtils.GetTimeTick() - StartTime;
                DlPreferMojang = DeltaTime < 4000;
                ModBase.Log($"[Download] Mojang 官方源加载耗时：{DeltaTime}ms，{(DlPreferMojang ? "可优先使用官方源" : "不优先使用官方源")}");
            }

            // 添加 PCL 特供项
            // 这个社区版下不了
            // If File.Exists(PathTemp & "Cache\download.json") Then Versions.Merge(GetJson(ReadFile(PathTemp & "Cache\download.json")))
            // 返回
            Loader.Output = new DlClientListResult
                { IsOfficial = true, SourceName = Lang.Text("Download.Source.MojangOfficial"), Value = Json };
            string Version;
            // 快照版
            Version = (string)Json["latest"]["snapshot"];
            if (Config.Tool.SnapshotNotification &&
                                      States.Tool.LastSnapshot != "" &&
                                      States.Tool.LastSnapshot != Version &&
                                      !_DlClientListMojangMain_IsHinted)
            {
                _DlClientListMojangMain_IsHinted = true;
                ModMinecraft.McDownloadClientUpdateHint(Version, Json);
            }

            States.Tool.LastSnapshot = Version ?? "Nothing";
            // 正式版
            Version = (string)Json["latest"]["release"];
            if (Config.Tool.ReleaseNotification &&
                                      States.Tool.LastRelease != "" &&
                                      States.Tool.LastRelease != Version &&
                                      !_DlClientListMojangMain_IsHinted)
            {
                _DlClientListMojangMain_IsHinted = true;
                ModMinecraft.McDownloadClientUpdateHint(Version, Json);
            }

            States.Tool.LastRelease = Version;
        }
        catch (Exception ex)
        {
            throw new Exception(Lang.Text("Minecraft.Download.Error.VersionListOperationFailed", "Mojang", ""), ex);
        }
    }

    /// <summary>
    ///     Minecraft 客户端 版本列表，BMCLAPI 源加载器。
    /// </summary>
    public static ModLoader.LoaderTask<string, DlClientListResult> DlClientListBmclapiLoader =
        new("DlClientList Bmclapi", DlClientListBmclapiMain);

    private static void DlClientListBmclapiMain(ModLoader.LoaderTask<string, DlClientListResult> Loader)
    {
        var Json = (JsonObject)Requester.FetchJson(
            "https://bmclapi2.bangbang93.com/mc/game/version_manifest.json");
        try
        {
            var Versions = (JsonArray)Json["versions"];
            if (Versions.Count < 200)
                throw new Exception(Lang.Text("Minecraft.Download.Error.VersionListOperationFailed", "BMCLAPI", Json));
            // 添加 UVMC 项
            var CacheFilePath = ModBase.PathTemp + @"Cache\uvmc-download.json";
            if (!File.Exists(CacheFilePath))
                try
                {
                    var UnlistedJson = (JsonObject)Requester.FetchJson(
                        "https://alist.8mi.tech/d/mirror/unlisted-versions-of-minecraft/Auto/version_manifest.json");
                    File.WriteAllText(CacheFilePath, UnlistedJson.ToString());
                }
                catch (Exception ex)
                {
                    ModBase.Log("[Download] 未列出的版本镜像源下载失败: " + ex.Message);
                }

            try
            {
                var CachedJson = (JsonObject)ModBase.GetJson(ModBase.ReadFile(CacheFilePath));
                Versions.Merge(CachedJson["versions"]);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "[Download] UVMC 列表加载失败，忽略列表内容");
            }

            // 检查是否有要求的版本（#5195）
            if (!string.IsNullOrEmpty(Loader.Input))
            {
                var Id = Loader.Input;
                if (DlClientListLoader.Output.Value is not null &&
                    !DlClientListLoader.Output.Value["versions"].AsArray().Any(v => (string)v["id"] == Id))
                    throw new Exception(Lang.Text("Minecraft.Download.Error.BmclapiMissingTargetVersion", Id));
            }

            // 返回
            Loader.Output = new DlClientListResult { IsOfficial = false, SourceName = "BMCLAPI", Value = Json };
        }
        catch (Exception ex)
        {
            throw new Exception(Lang.Text("Minecraft.Download.Error.VersionListOperationFailed", "BMCLAPI", Json), ex);
        }
    }

    /// <summary>
    ///     获取某个版本的 Json 下载地址，若失败则返回 Nothing。必须在工作线程执行。
    /// </summary>
    public static object DlClientListGet(string Id)
    {
        try
        {
            // 确认版本格式标准
            Id = Id.Replace("_", "-"); // 1.7.10_pre4 在版本列表中显示为 1.7.10-pre4
            if (Id != "1.0" && Id.EndsWithF(".0"))
                Id = Id.Substring(0, Id.Length - 2); // OptiFine 1.8 的下载会触发此问题，显示版本为 1.8.0
            // 获取 Minecraft 版本列表
            switch (DlClientListLoader.State)
            {
                case ModBase.LoadState.Finished:
                {
                    // 从当前的结果获取目标版本…
                    foreach (JsonObject Version in DlClientListLoader.Output.Value["versions"].AsArray())
                        if ((string)Version["id"] == Id)
                            return Version["url"].ToString();
                    // …如果没有，则重新尝试获取（在版本刚更新时可能出现这种情况，#5195）
                    DlClientListLoader.WaitForExit(Id, IsForceRestart: true);
                    break;
                }
                case ModBase.LoadState.Loading:
                {
                    DlClientListLoader.WaitForExit(Id);
                    break;
                }
                case ModBase.LoadState.Failed:
                case ModBase.LoadState.Aborted:
                case ModBase.LoadState.Waiting:
                {
                    DlClientListLoader.WaitForExit(Id, IsForceRestart: true);
                    break;
                }
            }

            // 重新查找版本
            foreach (JsonObject Version in DlClientListLoader.Output.Value["versions"].AsArray())
                if ((string)Version["id"] == Id)
                    return Version["url"].ToString();
            ModBase.Log($"未发现版本 {Id} 的 json 下载地址，版本列表返回为：{"\r\n"}{DlClientListLoader.Output.Value}",
                ModBase.LogLevel.Debug);
            return null;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, $"获取版本 {Id} 的 json 下载地址失败");
            return null;
        }
    }

    #endregion

    #region DlOptiFineList | OptiFine 版本列表

    public struct DlOptiFineListResult
    {
        /// <summary>
        ///     数据来源名称，如“Official”，“BMCLAPI”。
        /// </summary>
        public string SourceName;

        /// <summary>
        ///     是否为官方的实时数据。
        /// </summary>
        public bool IsOfficial;

        /// <summary>
        ///     获取到的数据。
        /// </summary>
        public List<DlOptiFineListEntry> Value;
    }

    public class DlOptiFineListEntry
    {
        private string _inherit;

        /// <summary>
        ///     显示名称，已去除 HD_U 字样，如“1.12.2 C8”。
        /// </summary>
        public string DisplayName;

        /// <summary>
        ///     是否为测试版。
        /// </summary>
        public bool IsPreview;

        /// <summary>
        ///     原始文件名称，如“preview_OptiFine_1.11_HD_U_E1_pre.jar”。
        /// </summary>
        public string NameFile;

        /// <summary>
        ///     对应的版本名称，如“1.13.2-OptiFine_HD_U_E6”。
        /// </summary>
        public string NameVersion;

        /// <summary>
        ///     发布时间，格式为“yyyy/mm/dd”。OptiFine 源无此数据。
        /// </summary>
        public string ReleaseTime;

        /// <summary>
        ///     需要的最低 Forge 版本。空字符串为无限制，Nothing 为不兼容，“28.1.56” 表示版本号，“1161” 表示版本号的最后一位。
        /// </summary>
        public string RequiredForgeVersion;

        /// <summary>
        ///     对应的 Minecraft 版本，如“1.12.2”。
        /// </summary>
        public string Inherit
        {
            get => _inherit;
            set
            {
                if (value.EndsWithF(".0"))
                    value = value.Substring(0, value.Length - 2);
                _inherit = value;
            }
        }
    }

    /// <summary>
    ///     OptiFine 版本列表，主加载器。
    /// </summary>
    public static ModLoader.LoaderTask<int, DlOptiFineListResult> DlOptiFineListLoader =
        new("DlOptiFineList Main", DlOptiFineListMain);

    private static void DlOptiFineListMain(ModLoader.LoaderTask<int, DlOptiFineListResult> Loader)
    {
        switch (Config.Download.VersionListSource)
        {
            case 0:
            {
                DlSourceLoader(Loader,
                    new List<KeyValuePair<ModLoader.LoaderTask<int, DlOptiFineListResult>, int>>
                        { new(DlOptiFineListBmclapiLoader, 30), new(DlOptiFineListOfficialLoader, 30 + 60) },
                    Loader.IsForceRestarting);
                break;
            }
            case 1:
            {
                DlSourceLoader(Loader,
                    new List<KeyValuePair<ModLoader.LoaderTask<int, DlOptiFineListResult>, int>>
                        { new(DlOptiFineListOfficialLoader, 5), new(DlOptiFineListBmclapiLoader, 5 + 30) },
                    Loader.IsForceRestarting);
                break;
            }

            default:
            {
                DlSourceLoader(Loader,
                    new List<KeyValuePair<ModLoader.LoaderTask<int, DlOptiFineListResult>, int>>
                        { new(DlOptiFineListOfficialLoader, 60), new(DlOptiFineListBmclapiLoader, 60 + 60) },
                    Loader.IsForceRestarting);
                break;
            }
        }
    }

    /// <summary>
    ///     OptiFine 版本列表，官方源。
    /// </summary>
    public static ModLoader.LoaderTask<int, DlOptiFineListResult> DlOptiFineListOfficialLoader =
        new("DlOptiFineList Official", DlOptiFineListOfficialMain);

    private static void DlOptiFineListOfficialMain(ModLoader.LoaderTask<int, DlOptiFineListResult> Loader)
    {
        string Result = "";
        using var resp = HttpRequest
            .Create("https://optifine.net/downloads")
            .WithHeader("Accept", "application/json, text/javascript, */*; q=0.01")
            .WithHeader("Accept-Language", "en-US,en;q=0.5")
            .WithHeader("X-Requested-With", "XMLHttpRequest")
            .SendAsync()
            .GetAwaiter()
            .GetResult();
        resp.EnsureSuccessStatusCode();
        Result = resp.AsString();
        if (Result.Length < 200)
            throw new Exception(Lang.Text("Minecraft.Download.Error.VersionListOperationFailed", "OptiFine", Result));
        try
        {
            var Forge = Result.RegexSearch("(?<=colForge'>)[^<]*");
            var ReleaseTime = Result.RegexSearch("(?<=colDate'>)[^<]+");
            var Name = Result.RegexSearch("(?<=OptiFine_)[0-9A-Za-z_.]+(?=.jar\")");
            if (ReleaseTime.Count != Name.Count)
                throw new Exception(Lang.Text("Minecraft.Download.Error.OptiFineTimeDataMismatch"));
            if (Forge.Count != Name.Count)
                throw new Exception(Lang.Text("Minecraft.Download.Error.OptiFineForgeCompatMismatch"));
            if (ReleaseTime.Count < 10)
                throw new Exception(
                    Lang.Text("Minecraft.Download.Error.VersionListOperationFailed", "OptiFine", Result));
            // 转化为列表输出
            var Versions = new List<DlOptiFineListEntry>();
            for (int i = 0, loopTo = ReleaseTime.Count - 1; i <= loopTo; i++)
            {
                Name[i] = Name[i].Replace("_", " ");
                var releaseDate = DateTime.ParseExact(ReleaseTime[i],
                    ["d.M.yyyy", "dd.M.yyyy", "d.MM.yyyy", "dd.MM.yyyy"],
                    CultureInfo.InvariantCulture, DateTimeStyles.None);
                var Entry = new DlOptiFineListEntry
                {
                    DisplayName = Name[i].Replace("HD U ", "").Replace(".0 ", " "),
                    ReleaseTime = Lang.Date(releaseDate, "d"),
                    IsPreview = Name[i].ContainsF("pre", true),
                    Inherit = Name[i].Split(" ")[0],
                    NameFile = (Name[i].ContainsF("pre", true) ? "preview_" : "") + "OptiFine_" +
                               Name[i].Replace(" ", "_") + ".jar",
                    RequiredForgeVersion = Forge[i].Replace("Forge ", "").Replace("#", "")
                };
                if (Entry.RequiredForgeVersion.Contains("N/A"))
                    Entry.RequiredForgeVersion = null;
                Entry.NameVersion = Entry.Inherit + "-OptiFine_" +
                                    Name[i].Replace(" ", "_").Replace(Entry.Inherit + "_", "");
                Versions.Add(Entry);
            }

            Loader.Output = new DlOptiFineListResult
                { IsOfficial = true, SourceName = Lang.Text("Download.Source.OptiFineOfficial"), Value = Versions };
        }
        catch (Exception ex)
        {
            throw new Exception(Lang.Text("Minecraft.Download.Error.VersionListOperationFailed", "OptiFine", Result),
                ex);
        }
    }

    /// <summary>
    ///     OptiFine 版本列表，BMCLAPI。
    /// </summary>
    public static ModLoader.LoaderTask<int, DlOptiFineListResult> DlOptiFineListBmclapiLoader =
        new("DlOptiFineList Bmclapi", DlOptiFineListBmclapiMain);

    private static void DlOptiFineListBmclapiMain(ModLoader.LoaderTask<int, DlOptiFineListResult> Loader)
    {
        var Json = (JsonArray)Requester.FetchJson("https://bmclapi2.bangbang93.com/optifine/versionList");
        try
        {
            var Versions = new List<DlOptiFineListEntry>();
            foreach (JsonObject Token in Json)
            {
                var Entry = new DlOptiFineListEntry
                {
                    DisplayName =
                        (Token["mcversion"] + Token["type"].ToString().Replace("HD_U", "").Replace("_", " ") + " " +
                         Token["patch"]).Replace(".0 ", " "),
                    ReleaseTime = "",
                    IsPreview = Token["patch"].ToString().ContainsF("pre", true),
                    Inherit = Token["mcversion"].ToString(),
                    NameFile = Token["filename"].ToString(),
                    RequiredForgeVersion = (Token["forge"] ?? "").ToString().Replace("Forge ", "").Replace("#", "")
                };
                if (Entry.RequiredForgeVersion.Contains("N/A"))
                    Entry.RequiredForgeVersion = null;
                Entry.NameVersion = Entry.Inherit + "-OptiFine_" + (Token["type"] + " " + Token["patch"])
                    .Replace(".0 ", " ").Replace(" ", "_").Replace(Entry.Inherit + "_", "");
                Versions.Add(Entry);
            }

            Loader.Output = new DlOptiFineListResult { IsOfficial = false, SourceName = "BMCLAPI", Value = Versions };
        }
        catch (Exception ex)
        {
            throw new Exception(
                Lang.Text("Minecraft.Download.Error.VersionListOperationFailed", "OptiFine BMCLAPI", Json), ex);
        }
    }

    #endregion

    #region DlForgeList | Forge Minecraft 版本列表

    public struct DlForgeListResult
    {
        /// <summary>
        ///     数据来源名称，如“Official”，“BMCLAPI”。
        /// </summary>
        public string SourceName;

        /// <summary>
        ///     是否为官方的实时数据。
        /// </summary>
        public bool IsOfficial;

        /// <summary>
        ///     获取到的数据。
        /// </summary>
        public List<string> Value;
    }

    /// <summary>
    ///     Forge 版本列表，主加载器。
    /// </summary>
    public static ModLoader.LoaderTask<int, DlForgeListResult> DlForgeListLoader =
        new("DlForgeList Main", DlForgeListMain);

    private static void DlForgeListMain(ModLoader.LoaderTask<int, DlForgeListResult> Loader)
    {
        switch (Config.Download.VersionListSource)
        {
            case 0:
            {
                DlSourceLoader(Loader,
                    new List<KeyValuePair<ModLoader.LoaderTask<int, DlForgeListResult>, int>>
                        { new(DlForgeListBmclapiLoader, 30), new(DlForgeListOfficialLoader, 30 + 60) },
                    Loader.IsForceRestarting);
                break;
            }
            case 1:
            {
                DlSourceLoader(Loader,
                    new List<KeyValuePair<ModLoader.LoaderTask<int, DlForgeListResult>, int>>
                        { new(DlForgeListOfficialLoader, 5), new(DlForgeListBmclapiLoader, 5 + 30) },
                    Loader.IsForceRestarting);
                break;
            }

            default:
            {
                DlSourceLoader(Loader,
                    new List<KeyValuePair<ModLoader.LoaderTask<int, DlForgeListResult>, int>>
                        { new(DlForgeListOfficialLoader, 60), new(DlForgeListBmclapiLoader, 60 + 60) },
                    Loader.IsForceRestarting);
                break;
            }
        }
    }

    /// <summary>
    ///     Forge 版本列表，官方源。
    /// </summary>
    public static ModLoader.LoaderTask<int, DlForgeListResult> DlForgeListOfficialLoader =
        new("DlForgeList Official", DlForgeListOfficialMain);

    private static void DlForgeListOfficialMain(ModLoader.LoaderTask<int, DlForgeListResult> Loader)
    {
        var Result = Requester.FetchString(
            "https://files.minecraftforge.net/maven/net/minecraftforge/forge/index_1.2.4.html", new RequestParam
            {
                Encoding = Encoding.Default,
                UseBrowserUserAgent = true
            });
        if (Result.Length < 200)
            throw new Exception(Lang.Text("Minecraft.Download.Error.VersionListOperationFailed", "Forge", Result));
        // 获取所有版本信息
        var Names = Result.RegexSearch("(?<=a href=\"index_)[0-9.]+(_pre[0-9]?)?(?=.html)");
        Names.Add("1.2.4"); // 1.2.4 不会被匹配上
        if (Names.Count < 10)
            throw new Exception(Lang.Text("Minecraft.Download.Error.VersionListOperationFailed", "Forge", Result));
        Loader.Output = new DlForgeListResult
            { IsOfficial = true, SourceName = Lang.Text("Download.Source.ForgeOfficial"), Value = Names };
    }

    /// <summary>
    ///     Forge 版本列表，BMCLAPI。
    /// </summary>
    public static ModLoader.LoaderTask<int, DlForgeListResult> DlForgeListBmclapiLoader =
        new("DlForgeList Bmclapi", DlForgeListBmclapiMain);

    private static void DlForgeListBmclapiMain(ModLoader.LoaderTask<int, DlForgeListResult> Loader)
    {
        var Result =
            Requester.FetchJson("https://bmclapi2.bangbang93.com/forge/minecraft",
                new RequestParam
                {
                    Encoding = Encoding.Default,
                })?.ToString() ?? "";
        if (Result.Length < 200)
            throw new Exception(Lang.Text("Minecraft.Download.Error.VersionListOperationFailed", "Forge BMCLAPI",
                Result));
        // 获取所有版本信息
        var Names = Result.RegexSearch("[0-9.]+(_pre[0-9]?)?");
        if (Names.Count < 10)
            throw new Exception(Lang.Text("Minecraft.Download.Error.VersionListOperationFailed", "Forge BMCLAPI",
                Result));
        Loader.Output = new DlForgeListResult { IsOfficial = false, SourceName = "BMCLAPI", Value = Names };
    }

    #endregion

    #region DlForgeVersion | Forge 版本列表

    public abstract class DlForgelikeEntry : IComparable<DlForgelikeEntry>
    {
        public enum ForgelikeType
        {
            Forge,
            NeoForge,
            Cleanroom
        }

        /// <summary>
        ///     Forgelike 种类。Forge、NeoForge、Cleanroom。
        /// </summary>
        public ForgelikeType ForgeType;

        /// <summary>
        ///     对应的 Minecraft 版本，如“1.12.2”。
        /// </summary>
        public string Inherit;

        /// <summary>
        ///     标准化后的版本号，仅可用于比较与排序。
        ///     格式：Major.Minor.Build.Revision
        ///     Forge：如 “50.1.9.0”（最后一位固定为 0）、“14.22.1.2478”（Legacy）。
        ///     NeoForge：如 “20.4.30.0”（最后一位固定为 0）、“19.47.1.99”（Legacy：第一位固定为 19）。
        ///     Cleanroom：如 “0.2.4.1”（Alpha：最后一位固定为 1）。
        /// </summary>
        public Version Version;

        /// <summary>
        ///     可对玩家显示的非格式化版本名。
        ///     Forge：如 “50.1.9”、“14.22.1.2478”（Legacy）。
        ///     NeoForge：如 “20.4.30-beta”、“47.1.99”（Legacy）。
        ///     Cleanroom：如 “0.2.4-alpha”。
        /// </summary>
        public string VersionName;

        /// <summary>
        ///     加载器名称。Forge / NeoForge / Cleanroom。
        /// </summary>
        public string LoaderName => ForgeType.ToString();

        /// <summary>
        ///     文件扩展名。不以小数点开头。
        /// </summary>
        public string FileExtension
        {
            get
            {
                if (ForgeType == 0) return ((DlForgeVersionEntry)this).Category == "installer" ? "jar" : "zip";

                return "jar";
            }
        }

        /// <summary>
        ///     Forge：MC 版本是否小于 1.13。
        ///     NeoForge：MC 版本是否为 1.20.1。
        ///     Cleanroom：固定为 False。
        /// </summary>
        public bool IsLegacy
        {
            get
            {
                // Cleanroom 始终为 False
                if ((int)ForgeType == 2)
                    return false;
                // 虽然很抽象，但确实可以这样判断
                // Forge：1.13+ 的版本号首位都大于 20
                // NeoForge：1.20.1 的版本号首位人为规定为 19 开头
                return Version.Major < 20;
            }
        }

        public int CompareTo(DlForgelikeEntry other)
        {
            if (Version != other.Version) return Version.CompareTo(other.Version);

            return ModMinecraft.CompareVersion(VersionName, other.VersionName);
        }
    }

    public class DlForgeVersionEntry : DlForgelikeEntry
    {
        /// <summary>
        ///     安装类型。有 installer、client、universal 三种。
        /// </summary>
        public string Category;

        /// <summary>
        ///     用于下载的文件版本名。可能在 Version 的基础上添加了分支。
        /// </summary>
        public string FileVersion;

        /// <summary>
        ///     文件的 MD5 或 SHA1（BMCLAPI 的老版本是 MD5，新版本是 SHA1；官方源总是 MD5）。
        /// </summary>
        public string Hash;

        /// <summary>
        ///     是否为推荐版本。
        /// </summary>
        public bool IsRecommended;

        /// <summary>
        ///     发布时间，格式为“yyyy/MM/dd HH:mm”。
        /// </summary>
        public string ReleaseTime;

        public DlForgeVersionEntry(string Version, string Branch, string Inherit)
        {
            // 司马版本的特殊处理
            if (Version == "11.15.1.2318" || Version == "11.15.1.1902" || Version == "11.15.1.1890")
                Branch = "1.8.9";
            if (Branch is null && Inherit == "1.7.10" && double.Parse(Version.Split(".")[3]) >= 1300d)
                Branch = "1.7.10";
            // 为 DlForgelikeEntry 提供所有信息
            ForgeType = ForgelikeType.Forge;
            VersionName = Version;
            this.Version = new Version(Version);
            this.Inherit = Inherit;
            FileVersion = Version + (Branch is null ? "" : "-" + Branch);
        }
    }

    /// <summary>
    ///     Forge 版本列表，主加载器。
    /// </summary>
    public static void DlForgeVersionMain(ModLoader.LoaderTask<string, List<DlForgeVersionEntry>> Loader)
    {
        var DlForgeVersionOfficialLoader =
            new ModLoader.LoaderTask<string, List<DlForgeVersionEntry>>("DlForgeVersion Official",
                DlForgeVersionOfficialMain);
        var DlForgeVersionBmclapiLoader =
            new ModLoader.LoaderTask<string, List<DlForgeVersionEntry>>("DlForgeVersion Bmclapi",
                DlForgeVersionBmclapiMain);
        switch (Config.Download.VersionListSource)
        {
            case 0:
            {
                DlSourceLoader(Loader,
                    new List<KeyValuePair<ModLoader.LoaderTask<string, List<DlForgeVersionEntry>>, int>>
                        { new(DlForgeVersionBmclapiLoader, 30), new(DlForgeVersionOfficialLoader, 30 + 60) },
                    Loader.IsForceRestarting);
                break;
            }
            case 1:
            {
                DlSourceLoader(Loader,
                    new List<KeyValuePair<ModLoader.LoaderTask<string, List<DlForgeVersionEntry>>, int>>
                        { new(DlForgeVersionOfficialLoader, 5), new(DlForgeVersionBmclapiLoader, 5 + 30) },
                    Loader.IsForceRestarting);
                break;
            }

            default:
            {
                DlSourceLoader(Loader,
                    new List<KeyValuePair<ModLoader.LoaderTask<string, List<DlForgeVersionEntry>>, int>>
                        { new(DlForgeVersionOfficialLoader, 60), new(DlForgeVersionBmclapiLoader, 60 + 60) },
                    Loader.IsForceRestarting);
                break;
            }
        }
    }

    /// <summary>
    ///     Forge 版本列表，官方源。
    /// </summary>
    public static void DlForgeVersionOfficialMain(ModLoader.LoaderTask<string, List<DlForgeVersionEntry>> Loader)
    {
        string Result;
        try
        {
            Result = Requester.FetchJson(
                "https://files.minecraftforge.net/maven/net/minecraftforge/forge/index_" +
                Loader.Input.Replace("-", "_") + ".html", new RequestParam
                {
                    UseBrowserUserAgent = true
                })?.ToString() ?? ""; // 兼容 Forge 1.7.10-pre4，#4057
        }
        catch (WebException)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("(404)")) throw new Exception(Lang.Text("Minecraft.Download.Error.NotFound"));

            throw;
        }

        if (Result.Length < 1000)
            throw new Exception(Lang.Text("Minecraft.Download.Error.VersionListOperationFailed", "Forge", Result));
        var Versions = new List<DlForgeVersionEntry>();
        try
        {
            // 分割版本信息
            var VersionCodes = Result.Substring(0, Result.LastIndexOfF("</table>"))
                .Split("<td class=\"download-version");
            // 获取所有版本信息
            for (int i = 1, loopTo = VersionCodes.Count() - 1; i <= loopTo; i++)
            {
                var VersionCode = VersionCodes[i];
                try
                {
                    // 基础信息获取
                    var Name = VersionCode.RegexSeek(@"(?<=[^(0-9)]+)[0-9\.]+");
                    var IsRecommended = VersionCode.Contains("fa promo-recommended");
                    var Inherit = Loader.Input;
                    // 分支获取
                    var Branch = VersionCode.RegexSeek($"(?<=-{Name}-)[^-\"]+(?=-[a-z]+.[a-z]{{3}})");
                    if (string.IsNullOrWhiteSpace(Branch))
                        Branch = null;
                    // 发布时间获取
                    var ReleaseTimeOriginal = VersionCode.RegexSeek("(?<=\"download-time\" title=\")[^\"]+");
                    // Dim ReleaseTimeSplit = ReleaseTimeOriginal.Split(" -:".ToCharArray) '原格式："2021-02-15 03:24:02"
                    var ReleaseDate =
                        DateTime.Parse(ReleaseTimeOriginal, null, DateTimeStyles.AssumeUniversal); // 以 UTC 时间作为标准
                    var ReleaseTime = Lang.Date(ReleaseDate.ToLocalTime(), "g"); // 时区与格式转换
                    // 分类与 MD5 获取
                    string MD5;
                    string Category;
                    if (VersionCode.Contains("classifier-installer\""))
                    {
                        // 类型为 installer.jar，支持范围 ~753 (~ 1.6.1 部分), 738~684 (1.5.2 全部)
                        VersionCode = VersionCode.Substring(VersionCode.IndexOfF("installer.jar"));
                        MD5 = VersionCode.RegexSeek("(?<=MD5:</strong> )[^<]+");
                        Category = "installer";
                    }
                    else if (VersionCode.Contains("classifier-universal\""))
                    {
                        // 类型为 universal.zip，支持范围 751~449 (1.6.1 部分), 682~183 (1.5.1 ~ 1.3.2 部分)
                        VersionCode = VersionCode.Substring(VersionCode.IndexOfF("universal.zip"));
                        MD5 = VersionCode.RegexSeek("(?<=MD5:</strong> )[^<]+");
                        Category = "universal";
                    }
                    else if (VersionCode.Contains("client.zip"))
                    {
                        // 类型为 client.zip，支持范围 182~ (1.3.2 部分 ~)
                        VersionCode = VersionCode.Substring(VersionCode.IndexOfF("client.zip"));
                        MD5 = VersionCode.RegexSeek("(?<=MD5:</strong> )[^<]+");
                        Category = "client";
                    }
                    else
                    {
                        // 没有任何下载（1.6.4 有一部分这种情况）
                        continue;
                    }

                    // 添加进列表
                    Versions.Add(new DlForgeVersionEntry(Name, Branch, Inherit)
                    {
                        Category = Category, IsRecommended = IsRecommended,
                        Hash = MD5.Trim('\r', '\n'),
                        ReleaseTime = ReleaseTime
                    });
                }
                catch (Exception ex)
                {
                    throw new Exception(
                        Lang.Text("Minecraft.Download.Error.VersionListOperationFailed", "Forge", VersionCode), ex);
                }
            }
        }
        catch (Exception ex)
        {
            throw new Exception(Lang.Text("Minecraft.Download.Error.VersionListOperationFailed", "Forge", Result), ex);
        }

        if (!Versions.Any())
            throw new Exception(Lang.Text("Minecraft.Download.Error.NotFound"));
        Loader.Output = Versions;
    }

    /// <summary>
    ///     Forge 版本列表，BMCLAPI。
    /// </summary>
    public static void DlForgeVersionBmclapiMain(ModLoader.LoaderTask<string, List<DlForgeVersionEntry>> Loader)
    {
        var Json = (JsonArray)Requester.FetchJson(
            "https://bmclapi2.bangbang93.com/forge/minecraft/" +
            Loader.Input.Replace("-", "_")); // 兼容 Forge 1.7.10-pre4，#4057
        var Versions = new List<DlForgeVersionEntry>();
        try
        {
            var Recommended = ModDownloadLib.McDownloadForgeRecommendedGet(Loader.Input);
            foreach (JsonObject Token in Json)
            {
                // 分类与 Hash 获取
                string Hash = null;
                var Category = "unknown";
                var Proi = -1;
                foreach (JsonObject File in Token["files"].AsArray())
                    switch (File["category"].ToString() ?? "")
                    {
                        case "installer":
                        {
                            if (File["format"].ToString() == "jar")
                            {
                                // 类型为 installer.jar，支持范围 ~753 (~ 1.6.1 部分), 738~684 (1.5.2 全部)
                                Hash = (string)File["hash"];
                                Category = "installer";
                                Proi = 2;
                            }

                            break;
                        }
                        case "universal":
                        {
                            if (Proi <= 1 && File["format"].ToString() == "zip")
                            {
                                // 类型为 universal.zip，支持范围 751~449 (1.6.1 部分), 682~183 (1.5.1 ~ 1.3.2 部分)
                                Hash = (string)File["hash"];
                                Category = "universal";
                                Proi = 1;
                            }

                            break;
                        }
                        case "client":
                        {
                            if (Proi <= 0 && File["format"].ToString() == "zip")
                            {
                                // 类型为 client.zip，支持范围 182~ (1.3.2 部分 ~)
                                Hash = (string)File["hash"];
                                Category = "client";
                                Proi = 0;
                            }

                            break;
                        }
                    }

                // 获取 Entry
                var Branch = (string)Token["branch"];
                var Name = (string)Token["version"];
                // 基础信息获取
                var Entry = new DlForgeVersionEntry(Name, Branch, Loader.Input)
                    { Hash = Hash, Category = Category, IsRecommended = (Recommended ?? "") == (Name ?? "") };
                var TimeSplit = Token["modified"].ToString().Split('-', 'T', ':', '.', ' ', '/');
                Entry.ReleaseTime = Lang.Date(Token["modified"].ToObject<DateTime>().ToLocalTime(), "g");
                // 添加项
                Versions.Add(Entry);
            }
        }
        catch (Exception ex)
        {
            throw new Exception(Lang.Text("Minecraft.Download.Error.VersionListOperationFailed", "Forge BMCLAPI", Json),
                ex);
        }

        if (!Versions.Any())
            throw new Exception(Lang.Text("Minecraft.Download.Error.NotFound"));
        Loader.Output = Versions;
    }

    #endregion

    #region DlNeoForgeList | NeoForge 版本列表

    public struct DlNeoForgeListResult
    {
        /// <summary>
        ///     数据来源名称，如“Official”，“BMCLAPI”。
        /// </summary>
        public string SourceName;

        /// <summary>
        ///     是否为官方的实时数据。
        /// </summary>
        public bool IsOfficial;

        /// <summary>
        ///     所有版本的列表。已经按从新到老排序。
        /// </summary>
        public List<DlNeoForgeListEntry> Value;
    }

    public class DlNeoForgeListEntry : DlForgelikeEntry
    {
        /// <summary>
        ///     API 使用的原始版本字符串，如 “20.4.30-beta”、“1.20.1-47.1.99”（Legacy）。
        /// </summary>
        public string ApiName;

        /// <summary>
        ///     是否是 Beta 版。
        /// </summary>
        public bool IsBeta;

        public DlNeoForgeListEntry(string ApiName)
        {
            ForgeType = ForgelikeType.NeoForge;
            this.ApiName = ApiName;
            IsBeta = ApiName.Contains("beta") || ApiName.Contains("alpha");
            if (ApiName.Contains("1.20.1")) // 1.20.1-47.1.99
            {
                VersionName = ApiName.Replace("1.20.1-", "");
                Version = new Version("19." + VersionName);
                Inherit = "1.20.1";
            }
            else if (ApiName.StartsWith("0.")) // 0.25w14craftmine.3-beta
            {
                VersionName = ApiName;
                var Segments = ApiName.BeforeFirst("-").Split('.');
                Version = new Version(0, 0, int.Parse(Segments.Last()));
                Inherit = Segments[1];
            }
            else // 20.4.30-beta；26.1.0.0-alpha.1+snapshot-1
            {
                VersionName = ApiName;
                Version = new Version(ApiName.BeforeFirst("-"));
                if (Version.Major >= 24)
                    Inherit = $"{Version.Major}.{Version.Minor}{(Version.Build > 0 ? $".{Version.Build}" : "")}";
                else
                    Inherit = "1." + Version.Major + (Version.Minor > 0 ? "." + Version.Minor : "");
                if (VersionName.Contains("+"))
                    Inherit += "-" + VersionName.AfterFirst("+");
            }
        }

        /// <summary>
        ///     文件在官网的基础地址，不包含后缀。
        /// </summary>
        public string UrlBase
        {
            get
            {
                var PackageName = IsLegacy ? "forge" : "neoforge";
                return
                    $"https://maven.neoforged.net/releases/net/neoforged/{PackageName}/{ApiName}/{PackageName}-{ApiName}";
            }
        }
    }

    /// <summary>
    ///     NeoForge 版本列表，主加载器。
    /// </summary>
    public static ModLoader.LoaderTask<int, DlNeoForgeListResult> DlNeoForgeListLoader =
        new("DlNeoForgeList Main", DlNeoForgeListMain);

    private static void DlNeoForgeListMain(ModLoader.LoaderTask<int, DlNeoForgeListResult> loader)
    {
        switch (Config.Download.VersionListSource)
        {
            case 0:
            {
                DlSourceLoader(loader,
                    new List<KeyValuePair<ModLoader.LoaderTask<int, DlNeoForgeListResult>, int>>
                        { new(DlNeoForgeListBmclapiLoader, 30), new(DlNeoForgeListOfficialLoader, 30 + 60) },
                    loader.IsForceRestarting);
                break;
            }
            case 1:
            {
                DlSourceLoader(loader,
                    new List<KeyValuePair<ModLoader.LoaderTask<int, DlNeoForgeListResult>, int>>
                        { new(DlNeoForgeListOfficialLoader, 5), new(DlNeoForgeListBmclapiLoader, 5 + 30) },
                    loader.IsForceRestarting);
                break;
            }

            default:
            {
                DlSourceLoader(loader,
                    new List<KeyValuePair<ModLoader.LoaderTask<int, DlNeoForgeListResult>, int>>
                        { new(DlNeoForgeListOfficialLoader, 60), new(DlNeoForgeListBmclapiLoader, 60 + 60) },
                    loader.IsForceRestarting);
                break;
            }
        }
    }

    /// <summary>
    ///     NeoForge 版本列表，官方源。
    /// </summary>
    public static ModLoader.LoaderTask<int, DlNeoForgeListResult> DlNeoForgeListOfficialLoader =
        new("DlNeoForgeList Official", DlNeoForgeListOfficialMain);

    private static void DlNeoForgeListOfficialMain(ModLoader.LoaderTask<int, DlNeoForgeListResult> loader)
    {
        // 获取版本列表 JSON
        var resultLatest = Requester.FetchJson(
            "https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/neoforge",
            new RequestParam
            {
                UseBrowserUserAgent = true
            }).ToString();
        var resultLegacy = Requester.FetchJson(
            "https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/forge",
            new RequestParam
            {
                UseBrowserUserAgent = true
            }).ToString();
        if (resultLatest.Length < 100 || resultLegacy.Length < 100)
            throw new Exception(Lang.Text("Minecraft.Download.Error.VersionListOperationFailed", "NeoForge",
                resultLatest + "\r\n\r\n" + resultLegacy));
        // 解析
        try
        {
            loader.Output = new DlNeoForgeListResult
            {
                IsOfficial = true,
                SourceName = Lang.Text("Download.Source.NeoForgeOfficial"),
                Value = GetNeoForgeEntries(resultLatest, resultLegacy)
            };
        }
        catch (Exception ex)
        {
            throw new Exception(
                Lang.Text("Minecraft.Download.Error.VersionListOperationFailed", "NeoForge",
                    resultLatest + "\r\n\r\n" + resultLegacy), ex);
        }
    }

    /// <summary>
    ///     NeoForge 版本列表，BMCLAPI。
    /// </summary>
    public static ModLoader.LoaderTask<int, DlNeoForgeListResult> DlNeoForgeListBmclapiLoader =
        new("DlNeoForgeList Bmclapi", DlNeoForgeListBmclapiMain);

    public static void DlNeoForgeListBmclapiMain(ModLoader.LoaderTask<int, DlNeoForgeListResult> loader)
    {
        // 获取版本列表 JSON
        var resultLatest = Requester.FetchJson(
            "https://bmclapi2.bangbang93.com/neoforge/meta/api/maven/details/releases/net/neoforged/neoforge",
            new RequestParam
            {
                UseBrowserUserAgent = true
            }).ToString();
        var resultLegacy = Requester.FetchJson(
            "https://bmclapi2.bangbang93.com/neoforge/meta/api/maven/details/releases/net/neoforged/forge",
            new RequestParam
            {
                UseBrowserUserAgent = true
            }).ToString();
        if (resultLatest.Length < 100 || resultLegacy.Length < 100)
            throw new Exception(Lang.Text("Minecraft.Download.Error.VersionListOperationFailed", "NeoForge BMCLAPI",
                resultLatest + "\r\n\r\n" + resultLegacy));
        // 解析
        try
        {
            loader.Output = new DlNeoForgeListResult
            {
                IsOfficial = true,
                SourceName = "BMCLAPI",
                Value = GetNeoForgeEntries(resultLatest, resultLegacy)
            };
        }
        catch (Exception ex)
        {
            throw new Exception(
                Lang.Text("Minecraft.Download.Error.VersionListOperationFailed", "NeoForge BMCLAPI",
                    resultLatest + "\r\n\r\n" + resultLegacy),
                ex);
        }
    }

    private static List<DlNeoForgeListEntry> GetNeoForgeEntries(string latestJson, string latestLegacyJson)
    {
        var versionNames = ModBase.RegexSearch(latestLegacyJson + latestJson, RegexPatterns.DlNeoForgeVersion);
        var versions = versionNames.Where(name => name != "47.1.82").Select(name => new DlNeoForgeListEntry(name))
            .OrderByDescending(a => a).ToList(); // 这个版本虽然在版本列表中，但不能下载
        if (!versions.Any())
            throw new Exception(Lang.Text("Minecraft.Download.Error.NotFound"));
        return versions;
    }

    #endregion

    #region DlCleanroomList | Cleanroom 版本列表

    public struct DlCleanroomListResult
    {
        /// <summary>
        ///     数据来源名称，如“Official”，“BMCLAPI”。
        /// </summary>
        public string SourceName;

        /// <summary>
        ///     是否为官方的实时数据。
        /// </summary>
        public bool IsOfficial;

        /// <summary>
        ///     所有版本的列表。已经按从新到老排序。
        /// </summary>
        public List<DlCleanroomListEntry> Value;
    }

    public class DlCleanroomListEntry : DlForgelikeEntry
    {
        /// <summary>
        ///     API 使用的原始版本字符串，如 “0.2.4-alpha”。
        /// </summary>
        public string ApiName;

        /// <summary>
        ///     是否是 Beta 版。
        /// </summary>
        public bool IsBeta;

        public DlCleanroomListEntry(string ApiName)
        {
            ForgeType = ForgelikeType.Cleanroom;
            this.ApiName = ApiName;
            IsBeta = ApiName.Contains("alpha");
            VersionName = ApiName;
            Version = new Version(ApiName.BeforeFirst("-"));
            Inherit = "1.12.2";
        }

        /// <summary>
        ///     文件在官网的基础地址，不包含后缀。
        /// </summary>
        public string UrlBase =>
            $"https://github.com/CleanroomMC/Cleanroom/releases/download/{ApiName}/cleanroom-{ApiName}";
    }

    /// <summary>
    ///     Cleanroom 版本列表，主加载器。
    /// </summary>
    public static ModLoader.LoaderTask<int, DlCleanroomListResult> DlCleanroomListLoader =
        new("DlCleanroomList Main", DlCleanroomListMain);

    private static void DlCleanroomListMain(ModLoader.LoaderTask<int, DlCleanroomListResult> Loader)
    {
        switch (Config.Download.VersionListSource)
        {
            case 0:
            {
                DlSourceLoader(Loader,
                    new List<KeyValuePair<ModLoader.LoaderTask<int, DlCleanroomListResult>, int>>
                        { new(DlCleanroomListOfficialLoader, 30) }, Loader.IsForceRestarting);
                break;
            }
            case 1:
            {
                DlSourceLoader(Loader,
                    new List<KeyValuePair<ModLoader.LoaderTask<int, DlCleanroomListResult>, int>>
                        { new(DlCleanroomListOfficialLoader, 5) }, Loader.IsForceRestarting);
                break;
            }

            default:
            {
                DlSourceLoader(Loader,
                    new List<KeyValuePair<ModLoader.LoaderTask<int, DlCleanroomListResult>, int>>
                        { new(DlCleanroomListOfficialLoader, 60) }, Loader.IsForceRestarting);
                break;
            }
        }
    }

    /// <summary>
    ///     Cleanroom 版本列表，官方源。
    /// </summary>
    public static ModLoader.LoaderTask<int, DlCleanroomListResult> DlCleanroomListOfficialLoader =
        new("DlCleanroomList Official", DlCleanroomListOfficialMain);

    private static void DlCleanroomListOfficialMain(ModLoader.LoaderTask<int, DlCleanroomListResult> Loader)
    {
        // 获取版本列表 JSON
        var ResultLatest = Requester.FetchJson(
            "https://api.github.com/repos/CleanroomMC/Cleanroom/releases", new RequestParam
            {
                UseBrowserUserAgent = true
            }).ToString();
        if (ResultLatest.Length < 100)
            throw new Exception(Lang.Text("Minecraft.Download.Error.VersionListOperationFailed", "Cleanroom",
                ResultLatest));
        // 解析
        try
        {
            Loader.Output = new DlCleanroomListResult
            {
                IsOfficial = true,
                SourceName = Lang.Text("Download.Source.CleanroomOfficial"),
                Value = GetCleanroomEntries(ResultLatest)
            };
        }
        catch (Exception ex)
        {
            throw new Exception(
                Lang.Text("Minecraft.Download.Error.VersionListOperationFailed", "Cleanroom", ResultLatest), ex);
        }
    }

    private static List<DlCleanroomListEntry> GetCleanroomEntries(string LatestJson)
    {
        var Versions = new List<DlCleanroomListEntry>();
        var Json = JsonArray.Parse(LatestJson);
        foreach (JsonObject Token in Json.AsArray())
            Versions.Add(new DlCleanroomListEntry(Token["tag_name"].ToString())
                { ForgeType = (DlForgelikeEntry.ForgelikeType)2 });
        if (!Versions.Any())
            throw new Exception(Lang.Text("Minecraft.Download.Error.NoAvailableVersion"));
        Versions = Versions.OrderByDescending(a => a.Version).ToList();
        return Versions;
    }

    #endregion

    #region DlLiteLoaderList | LiteLoader 版本列表

    public struct DlLiteLoaderListResult
    {
        /// <summary>
        ///     数据来源名称，如“Official”，“BMCLAPI”。
        /// </summary>
        public string SourceName;

        /// <summary>
        ///     是否为官方的实时数据。
        /// </summary>
        public bool IsOfficial;

        /// <summary>
        ///     获取到的数据。
        /// </summary>
        public List<DlLiteLoaderListEntry> Value;

        /// <summary>
        ///     官方源的失败原因。若没有则为 Nothing。
        /// </summary>
        public Exception OfficialError;
    }

    public class DlLiteLoaderListEntry
    {
        /// <summary>
        ///     实际的文件名，如“liteloader-installer-1.12-00-SNAPSHOT.jar”。
        /// </summary>
        public string FileName;

        /// <summary>
        ///     对应的 Minecraft 版本，如“1.12.2”。
        /// </summary>
        public string Inherit;

        /// <summary>
        ///     是否为 1.7 及更早的远古版。
        /// </summary>
        public bool IsLegacy;

        /// <summary>
        ///     是否为测试版。
        /// </summary>
        public bool IsPreview;

        /// <summary>
        ///     对应的 Json 项。
        /// </summary>
        public JsonNode JsonToken;

        /// <summary>
        ///     文件的 MD5。
        /// </summary>
        public string MD5;

        /// <summary>
        ///     发布时间，格式为“yyyy/mm/dd HH:mm”。
        /// </summary>
        public string ReleaseTime;
    }

    /// <summary>
    ///     LiteLoader 版本列表，主加载器。
    /// </summary>
    public static ModLoader.LoaderTask<int, DlLiteLoaderListResult> DlLiteLoaderListLoader =
        new("DlLiteLoaderList Main", DlLiteLoaderListMain);

    private static void DlLiteLoaderListMain(ModLoader.LoaderTask<int, DlLiteLoaderListResult> Loader)
    {
        switch (Config.Download.VersionListSource)
        {
            case 0:
            {
                DlSourceLoader(Loader,
                    new List<KeyValuePair<ModLoader.LoaderTask<int, DlLiteLoaderListResult>, int>>
                    {
                        new(DlLiteLoaderListBmclapiLoader, 30), new(DlLiteLoaderListOfficialLoader, 30 + 60)
                    }, Loader.IsForceRestarting);
                break;
            }
            case 1:
            {
                DlSourceLoader(Loader,
                    new List<KeyValuePair<ModLoader.LoaderTask<int, DlLiteLoaderListResult>, int>>
                    {
                        new(DlLiteLoaderListOfficialLoader, 5), new(DlLiteLoaderListBmclapiLoader, 5 + 30)
                    }, Loader.IsForceRestarting);
                break;
            }

            default:
            {
                DlSourceLoader(Loader,
                    new List<KeyValuePair<ModLoader.LoaderTask<int, DlLiteLoaderListResult>, int>>
                    {
                        new(DlLiteLoaderListOfficialLoader, 60), new(DlLiteLoaderListBmclapiLoader, 60 + 60)
                    }, Loader.IsForceRestarting);
                break;
            }
        }
    }

    /// <summary>
    ///     LiteLoader 版本列表，官方源。
    /// </summary>
    public static ModLoader.LoaderTask<int, DlLiteLoaderListResult> DlLiteLoaderListOfficialLoader =
        new("DlLiteLoaderList Official", DlLiteLoaderListOfficialMain);

    private static void DlLiteLoaderListOfficialMain(ModLoader.LoaderTask<int, DlLiteLoaderListResult> Loader)
    {
        var Result =
            (JsonObject)Requester.FetchJson("https://dl.liteloader.com/versions/versions.json");
        try
        {
            var Json = (JsonObject)Result["versions"];
            var Versions = new List<DlLiteLoaderListEntry>();
            foreach (var Pair in Json)
            {
                if (Pair.Key.StartsWithF("1.6") || Pair.Key.StartsWithF("1.5"))
                    continue;
                var RealEntry =
                    (Pair.Value["artefacts"] ?? Pair.Value["snapshots"])["com.mumfrey:liteloader"]["latest"];
                Versions.Add(new DlLiteLoaderListEntry
                {
                    Inherit = Pair.Key,
                    IsLegacy = double.Parse(Pair.Key.Split(".")[1]) < 8d,
                    IsPreview = RealEntry["stream"].ToString().ToLower() == "snapshot",
                    FileName = "liteloader-installer-" + Pair.Key +
                               (Pair.Key == "1.8" || Pair.Key == "1.9" ? ".0" : "") + "-00-SNAPSHOT.jar",
                    MD5 = (string)RealEntry["md5"],
                    ReleaseTime = TimeUtils.FormatUnixTimestamp(long.Parse(RealEntry["timestamp"].ToString())),
                    JsonToken = RealEntry
                });
            }

            Loader.Output = new DlLiteLoaderListResult
                { IsOfficial = true, SourceName = Lang.Text("Download.Source.LiteLoaderOfficial"), Value = Versions };
        }
        catch (Exception ex)
        {
            throw new Exception(Lang.Text("Minecraft.Download.Error.VersionListOperationFailed", "LiteLoader", Result),
                ex);
        }
    }

    /// <summary>
    ///     LiteLoader 版本列表，BMCLAPI。
    /// </summary>
    public static ModLoader.LoaderTask<int, DlLiteLoaderListResult> DlLiteLoaderListBmclapiLoader =
        new("DlLiteLoaderList Bmclapi", DlLiteLoaderListBmclapiMain);

    private static void DlLiteLoaderListBmclapiMain(ModLoader.LoaderTask<int, DlLiteLoaderListResult> Loader)
    {
        var Result =
            (JsonObject)Requester.FetchJson(
                "https://bmclapi2.bangbang93.com/maven/com/mumfrey/liteloader/versions.json");
        try
        {
            var Json = (JsonObject)Result["versions"];
            var Versions = new List<DlLiteLoaderListEntry>();
            foreach (var Pair in Json)
            {
                if (Pair.Key.StartsWithF("1.6") || Pair.Key.StartsWithF("1.5"))
                    continue;
                var RealEntry =
                    (Pair.Value["artefacts"] ?? Pair.Value["snapshots"])["com.mumfrey:liteloader"]["latest"];
                Versions.Add(new DlLiteLoaderListEntry
                {
                    Inherit = Pair.Key,
                    IsLegacy = double.Parse(Pair.Key.Split(".")[1]) < 8d,
                    IsPreview = RealEntry["stream"].ToString().ToLower() == "snapshot",
                    FileName = "liteloader-installer-" + Pair.Key +
                               (Pair.Key == "1.8" || Pair.Key == "1.9" ? ".0" : "") + "-00-SNAPSHOT.jar",
                    MD5 = (string)RealEntry["md5"],
                    ReleaseTime = TimeUtils.FormatUnixTimestamp(long.Parse((string)RealEntry["timestamp"])),
                    JsonToken = RealEntry
                });
            }

            Loader.Output = new DlLiteLoaderListResult { IsOfficial = false, SourceName = "BMCLAPI", Value = Versions };
        }
        catch (Exception ex)
        {
            throw new Exception(
                Lang.Text("Minecraft.Download.Error.VersionListOperationFailed", "LiteLoader BMCLAPI", Result), ex);
        }
    }

    #endregion

    #region DlFabricList | Fabric 列表

    public struct DlFabricListResult
    {
        /// <summary>
        ///     数据来源名称，如“Official”，“BMCLAPI”。
        /// </summary>
        public string SourceName;

        /// <summary>
        ///     是否为官方的实时数据。
        /// </summary>
        public bool IsOfficial;

        /// <summary>
        ///     获取到的数据。
        /// </summary>
        public JsonObject Value;
    }

    /// <summary>
    ///     Fabric 列表，主加载器。
    /// </summary>
    public static ModLoader.LoaderTask<int, DlFabricListResult> DlFabricListLoader =
        new("DlFabricList Main", DlFabricListMain);

    private static void DlFabricListMain(ModLoader.LoaderTask<int, DlFabricListResult> Loader)
    {
        switch (Config.Download.VersionListSource)
        {
            case 0:
            {
                DlSourceLoader(Loader,
                    new List<KeyValuePair<ModLoader.LoaderTask<int, DlFabricListResult>, int>>
                        { new(DlFabricListBmclapiLoader, 30), new(DlFabricListOfficialLoader, 30 + 60) },
                    Loader.IsForceRestarting);
                break;
            }
            case 1:
            {
                DlSourceLoader(Loader,
                    new List<KeyValuePair<ModLoader.LoaderTask<int, DlFabricListResult>, int>>
                        { new(DlFabricListOfficialLoader, 5), new(DlFabricListBmclapiLoader, 5 + 30) },
                    Loader.IsForceRestarting);
                break;
            }

            default:
            {
                DlSourceLoader(Loader,
                    new List<KeyValuePair<ModLoader.LoaderTask<int, DlFabricListResult>, int>>
                        { new(DlFabricListOfficialLoader, 60), new(DlFabricListBmclapiLoader, 60 + 60) },
                    Loader.IsForceRestarting);
                break;
            }
        }
    }

    /// <summary>
    ///     Fabric 列表，官方源。
    /// </summary>
    public static ModLoader.LoaderTask<int, DlFabricListResult> DlFabricListOfficialLoader =
        new("DlFabricList Official", DlFabricListOfficialMain);

    private static void DlFabricListOfficialMain(ModLoader.LoaderTask<int, DlFabricListResult> Loader)
    {
        var Result = (JsonObject)Requester.FetchJson("https://meta.fabricmc.net/v2/versions");
        try
        {
            var Output = new DlFabricListResult
                { IsOfficial = true, SourceName = Lang.Text("Download.Source.FabricOfficial"), Value = Result };
            if (Output.Value["game"] is null || Output.Value["loader"] is null || Output.Value["installer"] is null)
                throw new Exception(Lang.Text("Minecraft.Download.Error.VersionListOperationFailed", "Fabric", Result));
            Loader.Output = Output;
        }
        catch (Exception ex)
        {
            throw new Exception(Lang.Text("Minecraft.Download.Error.VersionListOperationFailed", "Fabric", Result), ex);
        }
    }

    /// <summary>
    ///     Fabric 列表，BMCLAPI。
    /// </summary>
    public static ModLoader.LoaderTask<int, DlFabricListResult> DlFabricListBmclapiLoader =
        new("DlFabricList Bmclapi", DlFabricListBmclapiMain);

    private static void DlFabricListBmclapiMain(ModLoader.LoaderTask<int, DlFabricListResult> Loader)
    {
        var Result = (JsonObject)Requester.FetchJson("https://bmclapi2.bangbang93.com/fabric-meta/v2/versions");
        try
        {
            var Output = new DlFabricListResult { IsOfficial = false, SourceName = "BMCLAPI", Value = Result };
            if (Output.Value["game"] is null || Output.Value["loader"] is null || Output.Value["installer"] is null)
                throw new Exception(Lang.Text("Minecraft.Download.Error.VersionListOperationFailed", "Fabric BMCLAPI",
                    Result));
            Loader.Output = Output;
        }
        catch (Exception ex)
        {
            throw new Exception(
                Lang.Text("Minecraft.Download.Error.VersionListOperationFailed", "Fabric BMCLAPI", Result), ex);
        }
    }

    /// <summary>
    ///     Fabric API 列表，官方源。
    /// </summary>
    public static ModLoader.LoaderTask<int, List<ModComp.CompFile>> DlFabricApiLoader = new("Fabric API List Loader",
        Task => Task.Output = ModComp.CompFilesGet("fabric-api", false));

    /// <summary>
    ///     OptiFabric 列表，官方源。
    /// </summary>
    public static ModLoader.LoaderTask<int, List<ModComp.CompFile>> DlOptiFabricLoader =
        new("OptiFabric List Loader", Task => Task.Output = ModComp.CompFilesGet("322385", true));

    #endregion

    #region DlQuiltList | Quilt 列表

    public struct DlQuiltListResult
    {
        /// <summary>
        ///     数据来源名称，如“Official”，“BMCLAPI”。
        /// </summary>
        public string SourceName;

        /// <summary>
        ///     是否为官方的实时数据。
        /// </summary>
        public bool IsOfficial;

        /// <summary>
        ///     获取到的数据。
        /// </summary>
        public JsonObject Value;
    }

    /// <summary>
    ///     Quilt 列表，主加载器。
    /// </summary>
    public static ModLoader.LoaderTask<int, DlQuiltListResult> DlQuiltListLoader =
        new("DlQuiltList Main", DlQuiltListMain);

    private static void DlQuiltListMain(ModLoader.LoaderTask<int, DlQuiltListResult> Loader)
    {
        switch (Config.Download.VersionListSource)
        {
            case 0:
            {
                DlSourceLoader(Loader,
                    new List<KeyValuePair<ModLoader.LoaderTask<int, DlQuiltListResult>, int>>
                        { new(DlQuiltListOfficialLoader, 30), new(DlQuiltListOfficialLoader, 60) },
                    Loader.IsForceRestarting);
                break;
            }
            case 1:
            {
                DlSourceLoader(Loader,
                    new List<KeyValuePair<ModLoader.LoaderTask<int, DlQuiltListResult>, int>>
                        { new(DlQuiltListOfficialLoader, 5), new(DlQuiltListOfficialLoader, 35) },
                    Loader.IsForceRestarting);
                break;
            }

            default:
            {
                DlSourceLoader(Loader,
                    new List<KeyValuePair<ModLoader.LoaderTask<int, DlQuiltListResult>, int>>
                        { new(DlQuiltListOfficialLoader, 60), new(DlQuiltListOfficialLoader, 60) },
                    Loader.IsForceRestarting);
                break;
            }
        }
    }

    /// <summary>
    ///     Quilt 列表，官方源。
    /// </summary>
    public static ModLoader.LoaderTask<int, DlQuiltListResult> DlQuiltListOfficialLoader =
        new("DlQuiltList Official", DlQuiltListOfficialMain);

    private static void DlQuiltListOfficialMain(ModLoader.LoaderTask<int, DlQuiltListResult> Loader)
    {
        var Result = (JsonObject)Requester.FetchJson("https://meta.quiltmc.org/v3/versions");
        try
        {
            var Output = new DlQuiltListResult
                { IsOfficial = true, SourceName = Lang.Text("Download.Source.QuiltOfficial"), Value = Result };
            if (Output.Value["game"] is null || Output.Value["loader"] is null || Output.Value["installer"] is null)
                throw new Exception(Lang.Text("Minecraft.Download.Error.VersionListOperationFailed", "Quilt", Result));
            Loader.Output = Output;
        }
        catch (Exception ex)
        {
            throw new Exception(Lang.Text("Minecraft.Download.Error.VersionListOperationFailed", "Quilt", Result), ex);
        }
    }

    // ''' <summary>
    // ''' TODO: Quilt 列表，BMCLAPI。
    // ''' </summary>
    // Public DlQuiltListBmclapiLoader As New LoaderTask(Of Integer, DlQuiltListResult)("DlQuiltList Bmclapi", AddressOf DlQuiltListBmclapiMain)
    // Private Sub DlQuiltListBmclapiMain(Loader As LoaderTask(Of Integer, DlQuiltListResult))
    // Dim Result As JsonObject = NetGetCodeByRequestRetry("https://bmclapi2.bangbang93.com/Quilt-meta/v2/versions")
    // Try
    // Dim Output = New DlQuiltListResult With {.IsOfficial = False, .SourceName = "BMCLAPI", .Value = Result}
    // If Output.Value("game") Is Nothing OrElse Output.Value("loader") Is Nothing OrElse Output.Value("installer") Is Nothing Then Throw New Exception("获取到的列表缺乏必要项")
    // Loader.Output = Output
    // Catch ex As Exception
    // Throw New Exception("Quilt BMCLAPI 版本列表解析失败（" & Result.ToString & "）", ex)
    // End Try
    // End Sub

    /// <summary>
    ///     QSL 列表，官方源。
    /// </summary>
    public static ModLoader.LoaderTask<int, List<ModComp.CompFile>> DlQSLLoader = new("QSL List Loader",
        Task => Task.Output = ModComp.CompFilesGet("qsl", false));

    #endregion

    #region DlLabyModList | LabyMod 列表

    public struct DlLabyModListResult
    {
        /// <summary>
        ///     获取到的数据。
        /// </summary>
        public JsonObject Value;
    }

    /// <summary>
    ///     LabyMod 列表，主加载器。
    /// </summary>
    public static ModLoader.LoaderTask<int, DlLabyModListResult> DlLabyModListLoader =
        new("DlLabyModList Main", DlLabyModListMain);

    private static void DlLabyModListMain(ModLoader.LoaderTask<int, DlLabyModListResult> Loader)
    {
        switch (Config.Download.VersionListSource)
        {
            case 0:
            {
                DlSourceLoader(Loader,
                    new List<KeyValuePair<ModLoader.LoaderTask<int, DlLabyModListResult>, int>>
                        { new(DlLabyModListOfficialLoader, 30), new(DlLabyModListOfficialLoader, 60) },
                    Loader.IsForceRestarting);
                break;
            }
            case 1:
            {
                DlSourceLoader(Loader,
                    new List<KeyValuePair<ModLoader.LoaderTask<int, DlLabyModListResult>, int>>
                        { new(DlLabyModListOfficialLoader, 5), new(DlLabyModListOfficialLoader, 35) },
                    Loader.IsForceRestarting);
                break;
            }

            default:
            {
                DlSourceLoader(Loader,
                    new List<KeyValuePair<ModLoader.LoaderTask<int, DlLabyModListResult>, int>>
                        { new(DlLabyModListOfficialLoader, 60), new(DlLabyModListOfficialLoader, 60) },
                    Loader.IsForceRestarting);
                break;
            }
        }
    }

    /// <summary>
    ///     LabyMod 列表，官方源。
    /// </summary>
    public static ModLoader.LoaderTask<int, DlLabyModListResult> DlLabyModListOfficialLoader =
        new("DlLabyModList Official", DlLabyModListOfficialMain);

    private static void DlLabyModListOfficialMain(ModLoader.LoaderTask<int, DlLabyModListResult> Loader)
    {
        JsonObject ResultProduction;
        using (var productionResponse = HttpRequest
                   .Create("https://releases.r2.labymod.net/api/v1/manifest/production/latest.json")
                   .WithHttpVersionOption(HttpVersion.Version20)
                   .SendAsync()
                   .GetAwaiter()
                   .GetResult())
        {
            ResultProduction = (JsonObject)ModBase.GetJson(productionResponse.AsString());
        }

        JsonObject ResultSnapshot;
        using (var snapshotResponse = HttpRequest
                   .Create("https://releases.r2.labymod.net/api/v1/manifest/snapshot/latest.json")
                   .WithHttpVersionOption(HttpVersion.Version20)
                   .SendAsync()
                   .GetAwaiter()
                   .GetResult())
        {
            snapshotResponse.EnsureSuccessStatusCode();
            ResultSnapshot = (JsonObject)ModBase.GetJson(snapshotResponse.AsString());
        }

        var Result = new JsonObject();
        Result.Add("production", ResultProduction);
        Result.Add("snapshot", ResultSnapshot);
        try
        {
            var Output = new DlLabyModListResult { Value = Result };
            if (Output.Value["production"]["labyModVersion"] is null ||
                Output.Value["snapshot"]["labyModVersion"] is null)
                throw new Exception(Lang.Text("Minecraft.Download.Error.VersionListOperationFailed", "LabyMod",
                    Result));
            Loader.Output = Output;
        }
        catch (Exception ex)
        {
            throw new Exception(Lang.Text("Minecraft.Download.Error.VersionListOperationFailed", "LabyMod", Result),
                ex);
        }
    }

    #endregion

    #region DlMod | Mod 镜像源请求

    /// <summary>
    ///     对可能涉及 Mod 镜像源的请求进行处理，返回字符串。
    ///     调用 NetGetCodeByRequest，会进行重试。
    /// </summary>
    public static string DlModRequest(string url) => DlModRequest<string>(url);

    /// <summary>
    ///     对可能涉及 Mod 镜像源的请求进行处理，返回字符串或 JSON 对象。
    ///     调用 NetGetCodeByRequest，会进行重试。
    /// </summary>
    public static T DlModRequest<T>(string url)
    {
        var Urls = new List<KeyValuePair<string, int>>();
        var McimUrl = DlSourceModGet(url);
        if ((McimUrl ?? "") != (url ?? ""))
            switch (Config.Download.Comp.CompSourceSolution)
            {
                case 0:
                {
                    Urls.Add(new KeyValuePair<string, int>(McimUrl, 5));
                    Urls.Add(new KeyValuePair<string, int>(McimUrl, 10));
                    Urls.Add(new KeyValuePair<string, int>(url, 15));
                    break;
                }
                case 1:
                {
                    Urls.Add(new KeyValuePair<string, int>(url, 5));
                    Urls.Add(new KeyValuePair<string, int>(McimUrl, 5));
                    Urls.Add(new KeyValuePair<string, int>(url, 15));
                    Urls.Add(new KeyValuePair<string, int>(McimUrl, 10));
                    break;
                }

                default:
                {
                    Urls.Add(new KeyValuePair<string, int>(url, 5));
                    Urls.Add(new KeyValuePair<string, int>(url, 15));
                    Urls.Add(new KeyValuePair<string, int>(McimUrl, 10));
                    break;
                }
            }

        var Exs = "";
        foreach (var Source in Urls)
            try
            {
                var json = Requester.FetchString(Source.Key, new RequestParam
                {
                    Timeout = Source.Value * 1000,
                    UseBrowserUserAgent = true
                });
                if (typeof(T) == typeof(string)) return (T)(object)json;
                return (T)(object)ModBase.GetJson(json);
            }
            catch (Exception ex)
            {
                // 镜像源可能随机爆炸，忽略就好
                if (!ex.Message.ContainsF("mcimirror")) Exs += ex.Message + "\r\n";
            }

        throw new Exception(Exs);
    }

    /// <summary>
    ///     非泛型版本的 DlModRequest，返回 string
    ///     对可能涉及 Mod 镜像源的请求进行处理。
    ///     调用 NetRequest，会进行重试。
    /// </summary>
    public static string DlModRequest(string url, string method, string data, string contentType,
        bool allowMirror = false) => DlModRequest<string>(url, method, data, contentType, allowMirror);
    
    /// <summary>
    ///     对可能涉及 Mod 镜像源的请求进行处理。
    ///     调用 NetRequest，会进行重试。
    /// </summary>
    public static T DlModRequest<T>(string url, string method, string data, string contentType,
        bool allowMirror = false)
    {
        var Urls = new List<KeyValuePair<string, int>>();
        var McimUrl = DlSourceModGet(url);
        if ((McimUrl ?? "") != (url ?? ""))
            switch (allowMirror ? Config.Download.Comp.CompSourceSolution : 2)
            {
                case 0:
                {
                    Urls.Add(new KeyValuePair<string, int>(McimUrl, 5));
                    Urls.Add(new KeyValuePair<string, int>(McimUrl, 10));
                    Urls.Add(new KeyValuePair<string, int>(url, 15));
                    break;
                }
                case 1:
                {
                    Urls.Add(new KeyValuePair<string, int>(url, 5));
                    Urls.Add(new KeyValuePair<string, int>(McimUrl, 5));
                    Urls.Add(new KeyValuePair<string, int>(url, 15));
                    Urls.Add(new KeyValuePair<string, int>(McimUrl, 10));
                    break;
                }

                default:
                {
                    Urls.Add(new KeyValuePair<string, int>(url, 5));
                    Urls.Add(new KeyValuePair<string, int>(url, 15));
                    Urls.Add(new KeyValuePair<string, int>(McimUrl, 10));
                    break;
                }
            }

        var Exs = "";
        foreach (var Source in Urls)
            try
            {
                string json = Requester.Fetch(Source.Key, new FetchParam
                {
                    Method = method,
                    Content = data, 
                    ContentType = contentType,
                    Timeout = Source.Value * 1000
                });
                if (typeof(T) == typeof(string)) return (T)(object)json; // 沟槽的，为什么不能写 T is string
                return (T)(object)ModBase.GetJson(json);
            }
            catch (Exception ex)
            {
                if (!ex.Message.ContainsF("mcimirror")) Exs += ex.Message + "\r\n";
            }

        throw new Exception(Exs);
    }

    #endregion

    #region DlSource | 镜像下载源

    private static bool DlPreferMojang;

    /// <summary>
    ///     下载文件（而非获取版本列表）的时候，是否优先使用官方源。
    /// </summary>
    public static bool DlSourcePreferMojang =>
        Config.Download.FileSource == 2 ||
        (Config.Download.FileSource == 1 && DlPreferMojang);

    /// <summary>
    ///     下载文件（而非获取版本列表）的时候，根据是否优先使用官方源决定使用 Url 的顺序。
    /// </summary>
    public static IEnumerable<string> DlSourceOrder(IEnumerable<string> OfficialUrls, IEnumerable<string> MirrorUrls)
    {
        return DlSourcePreferMojang ? OfficialUrls.Union(MirrorUrls) : MirrorUrls.Union(OfficialUrls);
    }

    /// <summary>
    ///     获取版本列表（而非下载文件）的时候，是否优先使用官方源。
    /// </summary>
    public static bool DlVersionListPreferMojang =>
        Config.Download.VersionListSource == 2 ||
        (Config.Download.VersionListSource == 1 && DlPreferMojang);

    /// <summary>
    ///     获取版本列表（而非下载文件）的时候，根据是否优先使用官方源决定使用 Url 的顺序。
    /// </summary>
    public static IEnumerable<string> DlVersionListOrder(IEnumerable<string> OfficialUrls,
        IEnumerable<string> MirrorUrls)
    {
        return DlVersionListPreferMojang ? OfficialUrls.Union(MirrorUrls) : MirrorUrls.Union(OfficialUrls);
    }


    /// <summary>
    ///     下载 Assets 文件。
    /// </summary>
    public static IEnumerable<string> DlSourceAssetsGet(string Original)
    {
        Original = Original.Replace("http://resources.download.minecraft.net",
            "https://resources.download.minecraft.net");
        return DlSourceOrder(new[] { Original },
            new[]
            {
                Original.Replace("https://piston-data.mojang.com", "https://bmclapi2.bangbang93.com/assets")
                    .Replace("https://piston-meta.mojang.com", "https://bmclapi2.bangbang93.com/assets")
                    .Replace("https://resources.download.minecraft.net", "https://bmclapi2.bangbang93.com/assets")
            });
    }

    /// <summary>
    ///     下载 Libraries 文件。
    /// </summary>
    public static IEnumerable<string> DlSourceLibraryGet(string Original)
    {
        if (new[] { "minecraftforge", "fabricmc", "neoforged" }.Any(k => Original.Contains(k))) // 不添加原版源
            return new[]
            {
                Original.Replace("https://piston-data.mojang.com", "https://bmclapi2.bangbang93.com/maven")
                    .Replace("https://piston-meta.mojang.com", "https://bmclapi2.bangbang93.com/maven")
                    .Replace("https://libraries.minecraft.net", "https://bmclapi2.bangbang93.com/maven")
                    .Replace("https://zkitefly.github.io/unlisted-versions-of-minecraft",
                        "https://alist.8mi.tech/d/mirror/unlisted-versions-of-minecraft/Auto"),
                Original.Replace("https://piston-data.mojang.com", "https://bmclapi2.bangbang93.com/libraries")
                    .Replace("https://piston-meta.mojang.com", "https://bmclapi2.bangbang93.com/libraries")
                    .Replace("https://libraries.minecraft.net", "https://bmclapi2.bangbang93.com/libraries")
                    .Replace("https://zkitefly.github.io/unlisted-versions-of-minecraft",
                        "https://alist.8mi.tech/d/mirror/unlisted-versions-of-minecraft/Auto")
            };

        return DlSourceOrder(new[] { Original },
            new[]
            {
                Original.Replace("https://piston-data.mojang.com", "https://bmclapi2.bangbang93.com/maven")
                    .Replace("https://piston-meta.mojang.com", "https://bmclapi2.bangbang93.com/maven")
                    .Replace("https://libraries.minecraft.net", "https://bmclapi2.bangbang93.com/maven")
                    .Replace("https://zkitefly.github.io/unlisted-versions-of-minecraft",
                        "https://alist.8mi.tech/d/mirror/unlisted-versions-of-minecraft/Auto"),
                Original.Replace("https://piston-data.mojang.com", "https://bmclapi2.bangbang93.com/libraries")
                    .Replace("https://piston-meta.mojang.com", "https://bmclapi2.bangbang93.com/libraries")
                    .Replace("https://libraries.minecraft.net", "https://bmclapi2.bangbang93.com/libraries")
                    .Replace("https://zkitefly.github.io/unlisted-versions-of-minecraft",
                        "https://alist.8mi.tech/d/mirror/unlisted-versions-of-minecraft/Auto"),
                Original
            });
    }

    /// <summary>
    ///     下载 Launcher 或 Meta 文件。
    ///     不应使用它来获取版本列表（因为它只使用文件下载源设置来决定源顺序）。
    /// </summary>
    public static IEnumerable<string> DlSourceLauncherOrMetaGet(string Original)
    {
        if (Original is null)
            throw new Exception(Lang.Text("Minecraft.Download.Error.NoJsonDownloadAddress"));
        return DlSourceOrder(new[] { Original },
            new[]
            {
                Original.Replace("https://piston-data.mojang.com", "https://bmclapi2.bangbang93.com")
                    .Replace("https://piston-meta.mojang.com", "https://bmclapi2.bangbang93.com")
                    .Replace("https://launcher.mojang.com", "https://bmclapi2.bangbang93.com")
                    .Replace("https://launchermeta.mojang.com", "https://bmclapi2.bangbang93.com")
                    .Replace("https://zkitefly.github.io/unlisted-versions-of-minecraft",
                        "https://alist.8mi.tech/d/mirror/unlisted-versions-of-minecraft/Auto"),
                Original
            });
    }

    /// <summary>
    ///     Mod Api 镜像源
    /// </summary>
    /// <param name="Original"></param>
    /// <returns></returns>
    public static string DlSourceModGet(string Original)
    {
        return Original.Replace("https://api.modrinth.com", "https://mod.mcimirror.top/modrinth")
            .Replace("https://api.curseforge.com", "https://mod.mcimirror.top/curseforge");
    }

    /// <summary>
    ///     Mod 下载镜像源
    /// </summary>
    /// <param name="original"></param>
    /// <returns></returns>
    public static List<string> DlSourceModDownloadGet(string original)
    {
        var res = new List<string>();
        var mirrorDl = original.Replace("https://cdn.modrinth.com", "https://mod.mcimirror.top")
            .Replace("https://edge.forgecdn.net",
                "https://mod.mcimirror.top"); // like https://cdn.modrinth.com/data/P7dR8mSH/versions/X2hTodix/fabric-api-0.129.0%2B1.21.8.jar
        // like https://edge.forgecdn.net/files/6767/951/jei-1.21.5-neoforge-21.4.0.27.jar
        switch (Config.Download.Comp.CompSourceSolution)
        {
            case 0: // 镜像源
            {
                res.Add(mirrorDl);
                res.Add(mirrorDl);
                break;
            }
            case 1: // 平衡
            {
                res.Add(original);
                res.Add(mirrorDl);
                break;
            }
            case 2: // 官方源
            {
                res.Add(original);
                res.Add(original); // 错误
                break;
            }

            default:
            {
                Config.Download.Comp.CompSourceSolution = 1;
                res.Add(original);
                break;
            }
        }

        res.Add(original);
        return res;
    }

    // Loader 自动切换
    private static void DlSourceLoader<InputType, OutputType>(ModLoader.LoaderTask<InputType, OutputType> MainLoader,
        List<KeyValuePair<ModLoader.LoaderTask<InputType, OutputType>, int>> LoaderList, bool IsForceRestart = false)
    {
        var WaitCycle = 0;
        while (true)
        {
            // 检查状态
            var BeforeLoadersAllFailed = true;
            foreach (var SubLoader in LoaderList)
            {
                if (WaitCycle == 0) // 判断是否可以不加载，直接使用已经加载好的结果
                {
                    if (IsForceRestart)
                        continue; // 强制刷新，不行
                    if (SubLoader.Key.Input is null ^ MainLoader.Input is null || (SubLoader.Key.Input is not null &&
                            !SubLoader.Key.Input.Equals(MainLoader.Input)))
                        continue; // 父子加载器的输入不一样，也不行
                }

                if (SubLoader.Key.State != ModBase.LoadState.Failed)
                    BeforeLoadersAllFailed = false;
                if (SubLoader.Key.State == ModBase.LoadState.Finished)
                {
                    // 检查加载器成功
                    MainLoader.Output = SubLoader.Key.Output;
                    DlSourceLoaderAbort(LoaderList);
                    return;
                }

                if (BeforeLoadersAllFailed)
                    // 此前的加载器全部失败，直接启动后续加载器
                    if (WaitCycle < SubLoader.Value * 100)
                        WaitCycle = SubLoader.Value * 100;
            }

            // 第一轮时：既然不直接使用已经加载好的结果，那就启动第一个加载器
            if (WaitCycle == 0)
            {
                LoaderList.First().Key.Start(MainLoader.Input, IsForceRestart);
                foreach (var Loader in LoaderList.Skip(1))
                    Loader.Key.State = ModBase.LoadState.Waiting; // 将其他源标记为未启动，以确保可以切换下载源（#184）
            }

            // 检查加载器失败或超时
            for (int i = 0, loopTo = LoaderList.Count - 1; i <= loopTo; i++)
            {
                if (WaitCycle != LoaderList[i].Value * 100)
                    continue;
                if (i < LoaderList.Count - 1 && !LoaderList.All(l => l.Key.State == ModBase.LoadState.Failed))
                {
                    // 若还有下一个源，则启动下一个源
                    LoaderList[i + 1].Key.Start(MainLoader.Input, IsForceRestart);
                }
                else
                {
                    // 若没有，则失败
                    Exception ErrorInfo = null;
                    for (int ii = 0, loopTo1 = LoaderList.Count - 1; ii <= loopTo1; ii++)
                    {
                        LoaderList[ii].Key.Input = default; // 重置输入，以免以同样的输入“重试加载”时直接失败
                        if (LoaderList[ii].Key.Error is null) continue;
                        if (ErrorInfo is null || LoaderList[ii].Key.Error.Message
                                .Contains(Lang.Text("Minecraft.Download.Error.NotFound")))
                            ErrorInfo = LoaderList[ii].Key.Error;
                    }

                    ErrorInfo ??= new TimeoutException(Lang.Text("Minecraft.Download.Error.Timeout"));
                    DlSourceLoaderAbort(LoaderList);
                    throw ErrorInfo;
                }

                break;
            }

            // 计时
            Thread.Sleep(10);
            WaitCycle += 1;
            // 检查父加载器中断
            if (MainLoader.IsAborted)
            {
                DlSourceLoaderAbort(LoaderList);
                return;
            }
        }
    }

    private static void DlSourceLoaderAbort<InputType, OutputType>(
        List<KeyValuePair<ModLoader.LoaderTask<InputType, OutputType>, int>> LoaderList)
    {
        foreach (var Loader in LoaderList)
            if (Loader.Key.State == ModBase.LoadState.Loading)
                Loader.Key.Abort();
    }

    #endregion

    #region DlLegacyFabricList | LegacyFabric 列表

    public struct DlLegacyFabricListResult
    {
        /// <summary>
        ///     数据来源名称，如“Official”，“BMCLAPI”。
        /// </summary>
        public string SourceName;

        /// <summary>
        ///     是否为官方的实时数据。
        /// </summary>
        public bool IsOfficial;

        /// <summary>
        ///     获取到的数据。
        /// </summary>
        public JsonObject Value;
    }

    /// <summary>
    ///     LegacyFabric 列表，主加载器。
    /// </summary>
    public static ModLoader.LoaderTask<int, DlLegacyFabricListResult> DlLegacyFabricListLoader =
        new("DlLegacyFabricList Main", DlLegacyFabricListMain);

    private static void DlLegacyFabricListMain(ModLoader.LoaderTask<int, DlLegacyFabricListResult> Loader)
    {
        switch (Config.Download.VersionListSource)
        {
            case 0:
            {
                DlSourceLoader(Loader,
                    new List<KeyValuePair<ModLoader.LoaderTask<int, DlLegacyFabricListResult>, int>>
                        { new(DlLegacyFabricListOfficialLoader, 30) }, Loader.IsForceRestarting);
                break;
            }
            case 1:
            {
                DlSourceLoader(Loader,
                    new List<KeyValuePair<ModLoader.LoaderTask<int, DlLegacyFabricListResult>, int>>
                        { new(DlLegacyFabricListOfficialLoader, 5) }, Loader.IsForceRestarting);
                break;
            }

            default:
            {
                DlSourceLoader(Loader,
                    new List<KeyValuePair<ModLoader.LoaderTask<int, DlLegacyFabricListResult>, int>>
                        { new(DlLegacyFabricListOfficialLoader, 60) }, Loader.IsForceRestarting);
                break;
            }
        }
    }

    /// <summary>
    ///     LegacyFabric 列表，官方源。
    /// </summary>
    public static ModLoader.LoaderTask<int, DlLegacyFabricListResult> DlLegacyFabricListOfficialLoader =
        new("DlLegacyFabricList Official", DlLegacyFabricListOfficialMain);

    private static void DlLegacyFabricListOfficialMain(ModLoader.LoaderTask<int, DlLegacyFabricListResult> Loader)
    {
        var Result =
            (JsonObject)Requester.FetchJson("https://meta.legacyfabric.net/v2/versions");
        try
        {
            var Output = new DlLegacyFabricListResult
                { IsOfficial = true, SourceName = Lang.Text("Download.Source.LegacyFabricOfficial"), Value = Result };
            if (Output.Value["game"] is null || Output.Value["loader"] is null || Output.Value["installer"] is null)
                throw new Exception(Lang.Text("Minecraft.Download.Error.VersionListOperationFailed", "LegacyFabric",
                    Result));
            Loader.Output = Output;
        }
        catch (Exception ex)
        {
            throw new Exception(
                Lang.Text("Minecraft.Download.Error.VersionListOperationFailed", "LegacyFabric", Result), ex);
        }
    }

    /// <summary>
    ///     Legacy Fabric API 列表，官方源。
    /// </summary>
    public static ModLoader.LoaderTask<int, List<ModComp.CompFile>> DlLegacyFabricApiLoader =
        new("Legacy Fabric API List Loader", Task => Task.Output = ModComp.CompFilesGet("legacy-fabric-api", false));

    #endregion
}
