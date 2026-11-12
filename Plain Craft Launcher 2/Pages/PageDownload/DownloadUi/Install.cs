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
            case LoadState.Finished:
                HintService.Show(Hints.Success($"{loader.name}{Lang.Text("Common.Status.Success")}"));
                break;
            case LoadState.Failed:
                HintService.Show(Hints.Error($"{loader.name}{Lang.Text("Common.Status.Failure")}{loader.Error.Message}"));
                break;
            case LoadState.Aborted:
                HintService.Show(Hints.Info($"{loader.name}{Lang.Text("Common.Status.Cancelled")}"));
                break;
            case LoadState.Waiting:
            case LoadState.Loading:
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
            case LoadState.Finished:
            {
                _HandleInstallFinished(loader);
                break;
            }
            case LoadState.Failed:
            {
                HintService.Show(Hints.Error($"{loader.name}{Lang.Text("Common.Status.Failure")}{loader.Error.Message}"));
                break;
            }
            case LoadState.Aborted:
            {
                HintService.Show(Hints.Info($"{loader.name}{Lang.Text("Common.Status.Cancelled")}"));
                break;
            }
            case LoadState.Loading:
            {
                return;
            }
            case LoadState.Waiting:
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
        if (loader.State != LoadState.Finished && Directory.Exists(backupFolder))
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
            if (((Loading.LoaderBase)loader).State is not (LoadState.Failed
                or LoadState.Aborted))
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
        catch (CancelledException)
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