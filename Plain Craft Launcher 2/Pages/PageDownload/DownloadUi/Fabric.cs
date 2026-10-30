using System.IO;
using System.Text;
using PCL.Core.App.Localization;
using PCL.Core.UI;
using PCL.Network;
using PCL.Network.Loaders;

namespace PCL;

public static partial class DownloadUi
{
    public static void McDownloadFabricLoaderSave(JsonObject downloadInfo)
    {
        try
        {
            var url = downloadInfo["url"]?.ToString() ?? "";
            var fileName = Common.GetFileNameFromPath(url);
            var version = Common.GetFileNameFromPath(downloadInfo["version"]?.ToString() ?? "");
            var target = SystemDialogs.SelectSaveFile(
                Lang.Text("Download.Version.SelectSaveLocation"),
                fileName,
                Lang.Text("Download.Version.Installer.Fabric.Filter")
            );
            if (!target.Contains(@"\"))
                return;

            if (Loading
                .loaderTaskbar.ToList()
                .Any(ongoingLoader =>
                    (ongoingLoader.name ?? "")
                    == (Lang.Text(
                            "Minecraft.Download.Stage.FabricInstallerDownload",
                            version)
                        ?? "")))
            {
                HintService.Hint(
                    Lang.Text("Minecraft.Download.Error.InstanceDownloading"),
                    HintType.Error);
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
                Lang.Text("Minecraft.Download.Stage.FabricInstallerDownload", version),
                loaders
            )
            {
                OnStateChanged = LoaderStateChangedHintOnly
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
                "开始 Fabric 安装器下载失败",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Minecraft.Download.Error.OperationFailed")
            );
        }
    }

    /// <summary>
    ///     获取下载某个 Fabric 实例的加载器列表。
    /// </summary>
    private static List<Loading.LoaderBase> McDownloadFabricLoader(
        string fabricVersion,
        string minecraftName,
        string? mcFolder = null,
        bool fixLibrary = true
    )
    {
        mcFolder ??= GameFolders.mcFolderSelected ?? "";
        var id = $"fabric-loader-{fabricVersion}-{minecraftName}";
        var versionFolder = Path.Combine(mcFolder ?? "", "versions", id);
        var loaders = new List<Loading.LoaderBase>();

        minecraftName = minecraftName.Replace("∞", "infinite");
        loaders.Add(
            new Loading.LoaderTask<string, List<DownloadFile>>(
                Lang.Text("Minecraft.Download.Stage.ObtainFabricMainFileUrl"),
                task =>
                {
                    if (fixLibrary)
                        McDownloadClient(
                            NetPreDownloadBehaviour.ExitWhileExistsOrDownloading,
                            minecraftName);
                    task.Progress = 0.5d;

                    var safeName = minecraftName.Replace("∞", "infinite");
                    var bmclapiUrl =
                        $"https://bmclapi2.bangbang93.com/fabric-meta/v2/versions/loader/{safeName}/{fabricVersion}/profile/json";
                    var officialUrl =
                        $"https://meta.fabricmc.net/v2/versions/loader/{safeName}/{fabricVersion}/profile/json";

                    string? json = null;
                    foreach (var url in new[]
                             {
                                 bmclapiUrl,
                                 officialUrl
                             })
                        try
                        {
                            json = Requester.FetchString(
                                url,
                                new RequestParam
                                {
                                    UseBrowserUserAgent = true,
                                    Timeout = 5000,
                                    Retries = 2
                                }
                            );
                            if (json is not null)
                                break;
                        }
                        catch (Exception ex)
                        {
                            Common.Log(ex, $"[Download] 从 {url} 下载 Fabric meta 失败");
                        }

                    if (json is null)
                        throw new Exception(
                            Lang.Text(
                                "Minecraft.Download.Error.FabricMetaDownloadFailed",
                                $"{bmclapiUrl} and {officialUrl}")
                        );

                    Directory.CreateDirectory(versionFolder);
                    File.WriteAllText(
                        Path.Combine(versionFolder, $"{id}.json"),
                        json,
                        Encoding.UTF8);
                    task.output = [];
                }
            )
            {
                ProgressWeight = 0.5d
            }
        );
        loaders.Add(
            new LoaderDownload(
                Lang.Text("Minecraft.Download.Stage.DownloadLoaderMainFile", "Fabric"),
                [])
            {
                ProgressWeight = 2.5d
            }
        );

        if (fixLibrary)
        {
            loaders.Add(
                new Loading.LoaderTask<string, List<DownloadFile>>(
                    Lang.Text("Minecraft.Download.Stage.AnalyzeFabricLibraries"),
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
                    Lang.Text("Minecraft.Download.Stage.DownloadLabyModClientJson"),
                    [])
                {
                    ProgressWeight = 8d
                });
        }

        return loaders;
    }
}