using System.IO;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;

namespace PCL;

public static partial class DownloadUi
{
    private const string McInstallDefaultType = "安装";

    /// <summary>
    ///     在加载器状态改变后显示一条提示。
    ///     不会进行任何其他操作。
    /// </summary>
    public static void LoaderStateChangedHintOnly(object loaderObj)
    {
        var loader = (Loading.LoaderBase)loaderObj;
        switch (loader.State)
        {
            case Common.LoadState.Finished:
                HintService.Show(Hints.Success($"{loader.name}{Lang.Text("Common.Status.Success")}"));
                break;
            case Common.LoadState.Failed:
                HintService.Show(Hints.Error($"{loader.name}{Lang.Text("Common.Status.Failure")}{loader.Error.Message}"));
                break;
            case Common.LoadState.Aborted:
                HintService.Show(Hints.Info($"{loader.name}{Lang.Text("Common.Status.Cancelled")}"));
                break;
            case Common.LoadState.Waiting:
            case Common.LoadState.Loading:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    /// <summary>
    ///     安装加载器状态改变后进行提示和重载文件夹列表的方法。
    /// </summary>
    public static void McInstallState(object loaderObj)
    {
        var loader = (Loading.LoaderBase)loaderObj;
        var combo = (Loading.LoaderCombo)loader;
        switch (loader.State)
        {
            case Common.LoadState.Finished:
            {
                _HandleInstallFinished(loader);
                break;
            }
            case Common.LoadState.Failed:
            {
                HintService.Show(Hints.Error($"{loader.name}{Lang.Text("Common.Status.Failure")}{loader.Error.Message}"));
                break;
            }
            case Common.LoadState.Aborted:
            {
                HintService.Show(Hints.Info($"{loader.name}{Lang.Text("Common.Status.Cancelled")}"));
                break;
            }
            case Common.LoadState.Loading:
            {
                return;
            }
            case Common.LoadState.Waiting:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        _RestoreOrClearInstallFolder(loader, combo);
        Loading.LoaderFolderRun(
            InstanceList.mcInstanceListLoader,
            GameFolders.mcFolderSelected ?? "",
            Loading.LoaderFolderRunType.ForceRun,
            1,
            @"versions\"
        );
    }

    private static void _HandleInstallFinished(Loading.LoaderBase loader)
    {
        if (Config.Download.AutoSelectInstance)
        {
            var versionName = loader.name;
            Common.WriteIni(
                $"{GameFolders.mcFolderSelected}PCL.ini",
                "Version",
                versionName.Remove(versionName.Length - 3, 3));
        }

        Common.WriteIni($"{GameFolders.mcFolderSelected}PCL.ini", "InstanceCache", "");
        Common.DeleteDirectory($"{((Loading.LoaderCombo)loader).input}PCLInstallBackups\\");
        HintService.Show(Hints.Success($"{loader.name}{Lang.Text("Common.Status.Success")}"));
    }

    private static void _RestoreOrClearInstallFolder(
        Loading.LoaderBase loader,
        Loading.LoaderCombo combo)
    {
        var backupFolder = $"{combo.input}PCLInstallBackups\\";
        if (loader.State != Common.LoadState.Finished && Directory.Exists(backupFolder))
        {
            Common.CopyDirectory(backupFolder, combo.input?.ToString() ?? "");
            File.Delete($"{combo.input}.pclignore");
            Common.DeleteDirectory(backupFolder);
        }
        else
        {
            McInstallFailedClearFolder(loader);
        }
    }

    public static void McInstallFailedClearFolder(object loader)
    {
        try
        {
            if (((Loading.LoaderBase)loader).State is not (Common.LoadState.Failed
                or Common.LoadState.Aborted))
                return;

            Thread.Sleep(1000); // 防止存在尚未完全释放的文件，导致清理失败（例如整合包安装）
            Common.Log(
                $"[Download] 由于下载失败或取消，清理实例文件夹：{((Loading.LoaderCombo)loader).input}",
                Common.LogLevel.Developer);
            var instancePath = ((Loading.LoaderCombo)loader).input?.ToString() ?? "";
            ((DynamicCacheConfigStorage)ConfigService.GetProvider(ConfigSource.GameInstance))
                .InvalidateCache(instancePath);
            Common.DeleteDirectory(instancePath);
        }
        catch (Exception ex)
        {
            Common.Log(ex, "下载失败或取消后清理实例文件夹失败");
        }
    }

    /// <summary>
    ///     进行合并安装。返回是否已经开始安装（例如如果没有安装 Java 则会进行提示并返回 False）
    /// </summary>
    public static bool McInstall(McInstallRequest request, string type = McInstallDefaultType)
    {
        if (!RestrictedFeaturePolicy.Check("minecraft-install"))
            return false;
        try
        {
            var subLoaders = McInstallLoader(request, ignoreDump: type != McInstallDefaultType);
            if (subLoaders is null)
                return false;

            _StartMcInstallLoader(request, subLoaders, type);
            return true;
        }
        catch (Common.CancelledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "开始合并安装失败",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Minecraft.Download.Error.OperationFailed")
            );
            _ClearIncompleteInstallFolder(request.targetInstanceFolder);
            return false;
        }
    }

    private static void _StartMcInstallLoader(
        McInstallRequest request,
        List<Loading.LoaderBase> subLoaders,
        string type)
    {
        var loader = new Loading.LoaderCombo<string>(
            $"{request.targetInstanceName} {type}",
            subLoaders)
        {
            OnStateChanged = McInstallState
        };

        loader.Start(request.targetInstanceFolder);
        Loading.LoaderTaskbarAdd(loader);
        Launcher.frmMain?.BtnExtraDownload.ShowRefresh();
        Launcher.frmMain?.BtnExtraDownload.Ribble();
    }

    private static void _ClearIncompleteInstallFolder(string instanceFolder)
    {
        try
        {
            if (!Directory.Exists(instanceFolder))
                return;

            var files = Directory.GetFiles(instanceFolder);
            var dirs = Directory.GetDirectories(instanceFolder);
            if (files.Length > 1 || dirs.Length != 0)
                return;

            ((DynamicCacheConfigStorage)ConfigService.GetProvider(ConfigSource.GameInstance))
                .InvalidateCache(instanceFolder);
            Common.DeleteDirectory(instanceFolder);
        }
        catch (Exception innerEx)
        {
            Common.Log(innerEx, "清理未完成的实例文件夹失败");
        }
    }

    /// <summary>
    ///     安装请求。
    /// </summary>
    public class McInstallRequest
    {
        /// <summary>
        ///     欲下载的 Cleanroom。
        /// </summary>
        public GameDownload.DlCleanroomListEntry? cleanroomEntry = null;

        /// <summary>
        ///     欲下载的 Cleanroom 版本名。
        /// </summary>
        public string? cleanroomVersion;

        /// <summary>
        ///     欲下载的 Fabric API 信息。
        /// </summary>
        public Comp.CompFile? fabricApi = null;

        /// <summary>
        ///     欲下载的 Fabric Loader 版本名。
        /// </summary>
        public string? fabricVersion = null;

        /// <summary>
        ///     欲下载的 Forge。
        /// </summary>
        public GameDownload.DlForgeVersionEntry? forgeEntry = null;

        /// <summary>
        ///     欲下载的 Forge 版本名。接受例如 36.1.4 / 14.23.5.2859 / 1.19-41.1.0 的输入。
        /// </summary>
        public string? forgeVersion;

        /// <summary>
        ///     欲下载的 LabyMod 通道。
        /// </summary>
        public string? labyModChannel = null;

        /// <summary>
        ///     欲下载的 LabyMod 版本。
        /// </summary>
        public string? labyModCommitRef = null;

        /// <summary>
        ///     欲下载的 Legacy Fabric API 信息。
        /// </summary>
        public Comp.CompFile? legacyFabricApi = null;

        /// <summary>
        ///     欲下载的 Legacy Fabric Loader 版本名。
        /// </summary>
        public string? legacyFabricVersion = null;

        /// <summary>
        ///     欲下载的 LiteLoader 详细信息。
        /// </summary>
        public GameDownload.DlLiteLoaderListEntry? liteLoaderEntry = null;

        /// <summary>
        ///     可选。欲下载的 Minecraft Json 地址。
        /// </summary>
        public string? minecraftJson = null;

        /// <summary>
        ///     必填。欲下载的 Minecraft 的版本名。
        /// </summary>
        public string? minecraftName = null;

        /// <summary>
        ///     若 MMC 整合包安装包含特殊参数，则填写此项。
        /// </summary>
        public Modpack.MmcPackInfo? mmcPackInfo = null;

        /// <summary>
        ///     欲下载的 NeoForge。
        /// </summary>
        public GameDownload.DlNeoForgeListEntry? neoForgeEntry = null;

        /// <summary>
        ///     欲下载的 NeoForge 版本名。
        /// </summary>
        public string? neoForgeVersion;

        /// <summary>
        ///     欲下载的 OptiFabric 信息。
        /// </summary>
        public Comp.CompFile? optiFabric = null;

        /// <summary>
        ///     欲下载的 OptiFine 详细信息。
        /// </summary>
        public GameDownload.DlOptiFineListEntry? optiFineEntry;

        /// <summary>
        ///     欲下载的 OptiFine 版本名。例如 HD_U_F6_pre1。
        /// </summary>
        public string? optiFineVersion;

        /// <summary>
        ///     必填。安装目标文件夹。
        /// </summary>
        public required string targetInstanceFolder;

        /// <summary>
        ///     必填。安装目标实例名称。
        /// </summary>
        public required string targetInstanceName;
    }

    private sealed class McInstallFolders
    {
        public string? CleanroomFolder;
        public string? FabricFolder;
        public string? ForgeFolder;
        public string? LabyModFolder;
        public string? LegacyFabricFolder;
        public string? LiteLoaderFolder;
        public bool Modable;
        public string ModsTempFolder = "";
        public string? NeoForgeFolder;
        public bool OptiFineAsMod;
        public string? OptiFineFolder;
    }
}