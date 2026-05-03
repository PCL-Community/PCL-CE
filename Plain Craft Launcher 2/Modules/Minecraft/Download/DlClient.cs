using System.IO;
using Microsoft.VisualBasic.CompilerServices;
using PCL.Network;
using PCL.Network.Loaders;

namespace PCL;

public class DlClient
{
    /// DlClient* | Minecraft 客户端

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
            throw new Exception("底层实例 " + Version.Name + " 中无 Jar 文件下载信息");
        // 检查文件
        var Checker = new ModBase.FileChecker(1024L, (long)(Version.JsonObject["downloads"]["client"]["size"] ?? -1),
            (string)Version.JsonObject["downloads"]["client"]["sha1"]);
        if (ReturnNothingOnFileUseable && Checker.Check(Version.PathInstance + Version.Name + ".jar") is null)
            return null; // 通过校验
        // 返回下载信息
        var JarUrl = (string)Version.JsonObject["downloads"]["client"]["url"];
        return new DownloadFile(DlSource.DlSourceLauncherOrMetaGet(JarUrl), Version.PathInstance + Version.Name + ".jar",
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
        var IndexAddress = ModMinecraft.McFolderSelected + @"assets\indexes\" + IndexInfo["id"] + ".json";
        ModBase.Log("[Download] 实例 " + Version.Name + " 对应的资源文件索引为 " + IndexInfo["id"]);
        var IndexUrl = (string)(IndexInfo["url"] ?? "");
        if (string.IsNullOrEmpty(IndexUrl)) return null;

        return new DownloadFile(DlSource.DlSourceLauncherOrMetaGet(IndexUrl), IndexAddress,
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

        if (Conversions.ToBoolean(ModMinecraft.ShouldIgnoreFileCheck(Version)))
        {
            ModBase.Log("[Download] 已跳过所有 Libraries 检查");
        }
        else
        {
            var LoadersLib = new List<ModLoader.LoaderBase>
            {
                new ModLoader.LoaderTask<string, List<DownloadFile>>("分析缺失支持库文件",
                    Task => Task.Output = ModMinecraft.McLibNetFilesFromInstance(Version)) { ProgressWeight = 1d },
                new LoaderDownload("下载支持库文件", new List<DownloadFile>()) { ProgressWeight = 15d }
            };
            // 构造加载器
            Loaders.Add(new ModLoader.LoaderCombo<string>("下载支持库文件（主加载器）", LoadersLib)
                { Block = false, Show = false, ProgressWeight = 16d });
        }

        #endregion

        #region 下载资源文件

        if (Conversions.ToBoolean(ModMinecraft.ShouldIgnoreFileCheck(Version)))
        {
            ModBase.Log("[Download] 已跳过所有 Assets 检查");
        }
        else
        {
            var LoadersAssets = new List<ModLoader.LoaderBase>();
            // 获取资源文件索引地址
            LoadersAssets.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>("分析资源文件索引地址", Task =>
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
                    throw new Exception("分析资源文件索引地址失败", ex);
                }
            }) { ProgressWeight = 0.5d, Show = false });
            // 下载资源文件索引
            LoadersAssets.Add(new LoaderDownload("下载资源文件索引", new List<DownloadFile>())
                { ProgressWeight = 2d });
            // 要求独立更新索引
            if (AssetsIndexBehaviour == AssetsIndexExistsBehaviour.DownloadInBackground)
            {
                var LoadersAssetsUpdate = new List<ModLoader.LoaderBase>();
                string TempAddress = null;
                string RealAddress = null;
                LoadersAssetsUpdate.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>("后台分析资源文件索引地址", Task =>
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
                LoadersAssetsUpdate.Add(new LoaderDownload("后台下载资源文件索引", new List<DownloadFile>()));
                LoadersAssetsUpdate.Add(new ModLoader.LoaderTask<List<DownloadFile>, string>("后台复制资源文件索引", Task =>
                {
                    ModBase.CopyFile(TempAddress, RealAddress);
                    ModLaunch.McLaunchLog("后台更新资源文件索引成功：" + TempAddress);
                }));
                var Updater = new ModLoader.LoaderCombo<string>("后台更新资源文件索引", LoadersAssetsUpdate);
                ModBase.Log("[Download] 开始后台检查资源文件索引");
                Updater.Start();
            }

            // 获取资源文件地址
            LoadersAssets.Add(new ModLoader.LoaderTask<string, List<DownloadFile>>("分析缺失资源文件", Task =>
            {
                ModLoader.LoaderBase argprogressFeed = Task;
                Task.Output = ModMinecraft.McAssetsFixList(Version, CheckAssetsHash, ref argprogressFeed);
                Task = (ModLoader.LoaderTask<string, List<DownloadFile>>)argprogressFeed;
            })
            {
                ProgressWeight = 3d
            });
            // 下载资源文件
            LoadersAssets.Add(new LoaderDownload("下载资源文件", new List<DownloadFile>()) { ProgressWeight = 25d });
            // 构造加载器
            Loaders.Add(new ModLoader.LoaderCombo<string>("下载资源文件（主加载器）", LoadersAssets)
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
}