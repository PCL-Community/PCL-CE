using System.IO;
using System.IO.Compression;
using System.Text;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using static PCL.Loading;

namespace PCL;

public static partial class Modpack
{
    private static LoaderCombo<string> InstallPackHmcl(
        string fileAddress,
        ZipArchive archive,
        string archiveBaseFolder)
    {
        var json = _ReadHmclManifest(archive, archiveBaseFolder);
        var instanceName = _ResolveModpackInstanceName(null, json["name"]?.ToString() ?? "");
        var installTemp = Launcher.RequestTaskTempFolder();
        var installLoaders = new List<LoaderBase>
        {
            _CreateHmclExtractLoader(fileAddress, archiveBaseFolder, instanceName, installTemp)
        };

        if (json["gameVersion"] is null)
            throw new Exception(Lang.Text("Minecraft.Download.Modpack.MissingGameVersion.Hmcl"));
        var request = new DownloadUi.McInstallRequest
        {
            targetInstanceName = instanceName,
            targetInstanceFolder = $@"{GameFolders.mcFolderSelected}versions\{instanceName}\",
            minecraftName = json["gameVersion"]!.ToString()
        };
        var mergeLoaders = DownloadUi.McInstallLoader(request);
        var loaders = _CreateModpackInstallLoaderList(installLoaders, mergeLoaders);

        var loaderName = Lang.Text("Minecraft.Download.Modpack.Task.HmclInstall", instanceName);
        if (loaderTaskbar.Any(l => (l.name ?? "") == (loaderName ?? "")))
        {
            HintService.Show(Hints.Error(Lang.Text("Minecraft.Download.Modpack.Installing")));
            throw new Common.CancelledException();
        }

        var loader = new LoaderCombo<string>(loaderName, loaders)
        {
            OnStateChanged = DownloadUi.McInstallState
        };
        loader.Start(request.targetInstanceFolder);
        LoaderTaskbarAdd(loader);
        Launcher.frmMain!.BtnExtraDownload.ShowRefresh();
        Common.RunInUi(() => Launcher.frmMain.PageChange(FormMain.PageType.TaskManager));
        return loader;
    }

    private static JsonObject _ReadHmclManifest(ZipArchive archive, string archiveBaseFolder)
    {
        try
        {
            return (JsonObject)
                Common.GetJson(
                    Common.ReadFile(
                        archive.GetEntry($"{archiveBaseFolder}modpack.json")!.Open(),
                        Encoding.UTF8));
        }
        catch (Exception ex)
        {
            throw new Exception("HMCL 整合包安装信息存在问题", ex);
        }
    }

    private static LoaderBase _CreateHmclExtractLoader(
        string fileAddress,
        string archiveBaseFolder,
        string instanceName,
        string installTemp)
    {
        return new LoaderTask<string, int>(
            Lang.Text("Minecraft.Download.Modpack.Stage.ExtractModpack"),
            task =>
            {
                ExtractModpackFiles(installTemp, fileAddress, task, 0.6d);
                CopyOverrideDirectory(
                    Path.Combine(installTemp, archiveBaseFolder, "minecraft"),
                    Path.Combine(GameFolders.mcFolderSelected!, "versions", instanceName),
                    task,
                    0.4d);
            })
        {
            ProgressWeight = new FileInfo(fileAddress).Length / 1024d / 1024d / 6d,
            block = false
        };
    }
}