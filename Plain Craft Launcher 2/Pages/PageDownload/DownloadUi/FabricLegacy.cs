using System.IO;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.Core.UI;
using PCL.Network;
using PCL.Network.Loaders;

namespace PCL;

public static partial class DownloadUi
{
    public static void McDownloadLegacyFabricLoaderSave(JsonObject downloadInfo)
    {
        try
        {
            var url = downloadInfo["url"]?.ToString() ?? "";
            var fileName = Common.GetFileNameFromPath(url);
            var version = Common.GetFileNameFromPath(downloadInfo["version"]?.ToString() ?? "");
            var target = SystemDialogs.SelectSaveFile(
                Lang.Text("Download.Version.SelectSaveLocation"),
                fileName,
                Lang.Text("Download.Version.Installer.LegacyFabric.Filter")
            );
            if (!target.Contains('\\'))
                return;

            if (Loading
                .loaderTaskbar
                .ToList()
                .Any(ongoingLoader =>
                    ongoingLoader.Identifier == DownloadIdentifier("LegacyFabricInstaller", version)))
            {
                HintService.Show(Hints.Error(Lang.Text("Minecraft.Download.Error.InstanceDownloading")));
                return;
            }

            var loaders = new List<Loading.LoaderBase>();
            var address = new List<string>
            {
                url
            };
            loaders.Add(
                new LoaderDownload(
                    Lang.Text("Minecraft.Download.Stage.DownloadMainFile"),
                    [new DownloadFile([.. address], target, new Common.FileChecker(1024 * 64))]
                )
                {
                    ProgressWeight = 15d
                }
            );
            var loader = new Loading.LoaderCombo<JsonObject>(
                Lang.Text("Minecraft.Download.Stage.LegacyFabricInstallerDownload", version),
                loaders
            )
            {
                OnStateChanged = LoaderStateChangedHintOnly,
                Identifier = DownloadIdentifier("LegacyFabricInstaller", version)
            };
            loader.Start(downloadInfo);
            Loading.LoaderTaskbarAdd(loader);
            Launcher.frmMain?.BtnExtraDownload.ShowRefresh();
            Launcher.frmMain?.BtnExtraDownload.Ribble();
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "开始 Legacy Fabric 安装器下载失败",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Minecraft.Download.Error.OperationFailed")
            );
        }
    }

    /// <summary>
    ///     获取下载某个 LegacyFabric 实例的加载器列表。
    /// </summary>
    private static List<Loading.LoaderBase> McDownloadLegacyFabricLoader(
        string legacyFabricVersion,
        string minecraftName,
        string? mcFolder = null,
        bool fixLibrary = true
    )
    {
        mcFolder ??= GameFolders.mcFolderSelected ?? "";
        var id = $"legacy-fabric-loader-{legacyFabricVersion}-{minecraftName}";
        var versionFolder = Path.Combine(mcFolder ?? "", "versions", id);
        var loaders = new List<Loading.LoaderBase>
        {
            new Loading.LoaderTask<string, List<DownloadFile>>(
                Lang.Text("Minecraft.Download.Stage.ObtainLegacyFabricMainFileUrl"),
                task =>
                {
                    if (fixLibrary)
                        McDownloadClient(
                            NetPreDownloadBehaviour.ExitWhileExistsOrDownloading,
                            minecraftName);
                    task.Progress = 0.5d;
                    task.output =
                    [
                        new DownloadFile(
                            [
                                $"https://meta.legacyfabric.net/v2/versions/loader/{minecraftName}/{legacyFabricVersion}/profile/json"
                            ],
                            Path.Combine(versionFolder, $"{id}.json"),
                            new Common.FileChecker(isJson: true)
                        )
                    ];
                }
            )
            {
                ProgressWeight = 0.5d
            },
            new LoaderDownload(
                Lang.Text("Minecraft.Download.Stage.DownloadLoaderMainFile", "Legacy Fabric"),
                [])
            {
                ProgressWeight = 2.5d
            }
        };

        if (fixLibrary)
        {
            loaders.Add(
                new Loading.LoaderTask<string, List<DownloadFile>>(
                    Lang.Text("Minecraft.Download.Stage.AnalyzeLegacyFabricLibraries"),
                    task => task.output =
                        Libraries.McLibNetFilesFromInstance(new McInstance(versionFolder))
                )
                {
                    ProgressWeight = 1d,
                    show = false
                }
            );
            loaders.Add(
                new LoaderDownload(
                    Lang.Text("Minecraft.Download.Stage.DownloadLoaderLibraries", "Legacy Fabric"),
                    [])
                {
                    ProgressWeight = 8d
                }
            );
        }

        return loaders;
    }
}