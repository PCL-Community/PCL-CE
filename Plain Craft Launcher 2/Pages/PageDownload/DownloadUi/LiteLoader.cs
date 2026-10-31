using System.IO;
using PCL.Core.App.Localization;
using PCL.Core.UI;
using PCL.Network;
using PCL.Network.Loaders;

namespace PCL;

public static partial class DownloadUi
{
    public static void McDownloadLiteLoader(GameDownload.DlLiteLoaderListEntry downloadInfo)
    {
        try
        {
            var id = downloadInfo.Inherit;
            var versionName = $"{downloadInfo.Inherit}-LiteLoader";
            var versionFolder = Path.Combine(GameFolders.mcFolderSelected ?? "", "versions", versionName);

            if (Loading.loaderTaskbar
                .ToList()
                .Any(ongoingLoader => ongoingLoader.Identifier == DownloadIdentifier("LiteLoader", id)))
            {
                HintService.Hint(
                    Lang.Text("Minecraft.Download.Error.InstanceDownloading"),
                    HintType.Error);
                return;
            }

            if (File.Exists(Path.Combine(versionFolder, $"{versionName}.json")))
            {
                if (Launcher.MyMsgBox(
                        Lang.Text(
                            "Minecraft.Download.Error.InstanceAlreadyExists",
                            versionName,
                            "\r\n"),
                        Lang.Text("Minecraft.Download.Error.InstanceAlreadyExists.Title"),
                        Lang.Text("Common.Action.Continue"),
                        Lang.Text("Common.Action.Cancel"))
                    == 1)
                {
                    File.Delete(Path.Combine(versionFolder, $"{versionName}.jar"));
                    File.Delete(Path.Combine(versionFolder, $"{versionName}.json"));
                }
                else
                {
                    return;
                }
            }

            var loader = new Loading.LoaderCombo<string>(
                Lang.Text("Minecraft.Download.Stage.LiteLoaderDownload", id),
                McDownloadLiteLoaderLoader(downloadInfo)
            )
            {
                OnStateChanged = McInstallState,
                Identifier = DownloadIdentifier("LiteLoader", id)
            };
            loader.Start(versionFolder);
            Loading.LoaderTaskbarAdd(loader);
            Launcher.frmMain?.BtnExtraDownload.ShowRefresh();
            Launcher.frmMain?.BtnExtraDownload.Ribble();
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "开始 LiteLoader 下载失败",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Minecraft.Download.Error.OperationFailed")
            );
        }
    }

    private static void McDownloadLiteLoaderSave(GameDownload.DlLiteLoaderListEntry downloadInfo)
    {
        try
        {
            var id = downloadInfo.Inherit;
            var target = SystemDialogs.SelectSaveFile(
                Lang.Text("Download.Version.SelectSaveLocation"),
                downloadInfo.FileName?.Replace("-SNAPSHOT", "") ?? "",
                Lang.Text("Minecraft.Download.Stage.ForgelikeInstallerFilter", "LiteLoader", "jar")
            );
            if (!target.Contains(@"\"))
                return;

            if (_IsLiteLoaderDownloading(id))
            {
                HintService.Hint(
                    Lang.Text("Minecraft.Download.Error.InstanceDownloading"),
                    HintType.Error);
                return;
            }

            var loaders = new List<Loading.LoaderBase>
            {
                new LoaderDownload(
                    Lang.Text("Minecraft.Download.Stage.DownloadMainFile"),
                    [
                        new DownloadFile(
                            _GetLiteLoaderDownloadAddresses(downloadInfo),
                            target,
                            new Common.FileChecker(1024 * 1024))
                    ]
                )
                {
                    ProgressWeight = 15d
                }
            };
            var loader = new Loading.LoaderCombo<GameDownload.DlLiteLoaderListEntry>(
                Lang.Text("Minecraft.Download.Stage.LiteLoaderInstallerDownload", id),
                loaders
            )
            {
                OnStateChanged = LoaderStateChangedHintOnly,
                Identifier = DownloadIdentifier("LiteLoader", id)
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
                "开始 LiteLoader 安装器下载失败",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Minecraft.Download.Error.OperationFailed")
            );
        }
    }

    private static bool _IsLiteLoaderDownloading(string? id)
    {
        return Loading.loaderTaskbar.Any(loader =>
            loader.Identifier == DownloadIdentifier("LiteLoader", id)
        );
    }

    private static string[] _GetLiteLoaderDownloadAddresses(
        GameDownload.DlLiteLoaderListEntry downloadInfo)
    {
        var inherit = downloadInfo.Inherit ?? "";
        if (!downloadInfo.IsLegacy)
            return
            [
                // 直连 HTTPS：原先写死 http:// 会先走一段明文再被 301 到 https，
                // 攻击者可在跳转前替换响应内容，而本文件随后会被当作安装器执行。
                $"https://jenkins.liteloader.com/job/LiteLoaderInstaller%20{inherit}/lastSuccessfulBuild/artifact/{(inherit == "1.8" ? "ant/dist/" : "build/libs/")}{downloadInfo.FileName}"
            ];

        return inherit switch
        {
            "1.7.10" =>
                ["https://dl.liteloader.com/redist/1.7.10/liteloader-installer-1.7.10-04.jar"],
            "1.7.2" => ["https://dl.liteloader.com/redist/1.7.2/liteloader-installer-1.7.2-04.jar"],
            "1.6.4" => ["https://dl.liteloader.com/redist/1.6.4/liteloader-installer-1.6.4-01.jar"],
            "1.6.2" => ["https://dl.liteloader.com/redist/1.6.2/liteloader-installer-1.6.2-04.jar"],
            "1.5.2" => ["https://dl.liteloader.com/redist/1.5.2/liteloader-installer-1.5.2-01.jar"],
            _ => throw new NotSupportedException(
                Lang.Text("Minecraft.Download.Error.UnknownMinecraftVersion", inherit))
        };
    }

    /// <summary>
    ///     获取下载某个 LiteLoader 实例的加载器列表。
    /// </summary>
    private static List<Loading.LoaderBase> McDownloadLiteLoaderLoader(
        GameDownload.DlLiteLoaderListEntry downloadInfo,
        string? mcFolder = null,
        Loading.LoaderCombo<string>? clientDownloadLoader = null,
        bool fixLibrary = true
    )
    {
        mcFolder ??= GameFolders.mcFolderSelected ?? "";
        var isCustomFolder = (mcFolder ?? "") != (GameFolders.mcFolderSelected ?? "");
        var id = downloadInfo.Inherit;
        var inherit = downloadInfo.Inherit ?? "";
        var target = Path.Combine(Common.pathTemp, "Download", $"{id}-Liteloader.jar");
        var versionName = $"{inherit}-LiteLoader";
        var versionFolder = Path.Combine(mcFolder ?? "", "versions", versionName);
        var loaders = new List<Loading.LoaderBase>();

        if (clientDownloadLoader is null)
            loaders.Add(
                new Loading.LoaderTask<string, string>(
                    Lang.Text("Minecraft.Download.Stage.StartLiteLoaderDependencyDownload"),
                    _ =>
                    {
                        if (isCustomFolder)
                            throw new Exception(
                                Lang.Text("Minecraft.Download.Error.NoVanillaLoaderSpecified"));
                        clientDownloadLoader = McDownloadClient(
                            NetPreDownloadBehaviour.ExitWhileExistsOrDownloading,
                            inherit);
                    }
                )
                {
                    ProgressWeight = 0.2d,
                    show = false,
                    block = false
                }
            );
        loaders.Add(
            new Loading.LoaderTask<string, string>(
                Lang.Text("Minecraft.Download.Stage.InstallLiteLoader"),
                _ =>
                {
                    try
                    {
                        Directory.CreateDirectory(versionFolder);
                        var versionJson = new JsonObject
                        {
                            {
                                "id", versionName
                            }
                        };
                        var releaseDate = downloadInfo.ReleaseTime
                                          ?? new McInstance(Path.Combine(
                                                  mcFolder,
                                                  "versions",
                                                  downloadInfo.Inherit ?? ""))
                                              .releaseTime;
                        versionJson.Add("time", releaseDate);
                        versionJson.Add("releaseTime", releaseDate);
                        versionJson.Add("type", "release");
                        versionJson.Add(
                            "arguments",
                            Common.GetJson(
                                $"{{\"game\":[\"--tweakClass\",\"{downloadInfo.jsonToken?["tweakClass"]}\"]}}")
                        );
                        var libraries = downloadInfo.jsonToken?["libraries"]?.DeepClone()
                            as JsonArray ?? new JsonArray();
                        versionJson.Add("libraries", libraries);
                        libraries.Add(
                            Common.GetJson(
                                $"{{\"name\": \"com.mumfrey:liteloader:{downloadInfo.jsonToken?["version"]}\",\"url\": \"https://dl.liteloader.com/versions/\"}}"
                            )
                        );
                        versionJson.Add("mainClass", "net.minecraft.launchwrapper.Launch");
                        versionJson.Add("minimumLauncherVersion", 18);
                        versionJson.Add("inheritsFrom", inherit);
                        versionJson.Add("jar", inherit);
                        Common.WriteFile(
                            Path.Combine(versionFolder, $"{versionName}.json"),
                            versionJson.ToString());
                    }
                    catch (Exception ex)
                    {
                        throw new Exception(
                            Lang.Text("Minecraft.Download.Error.LiteLoaderInstallFailed"),
                            ex);
                    }
                }
            )
            {
                ProgressWeight = 1d
            }
        );
        if (fixLibrary)
        {
            loaders.Add(
                new Loading.LoaderTask<string, List<DownloadFile>>(
                    Lang.Text("Minecraft.Download.Stage.AnalyzeLiteLoaderLibraries"),
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
                    Lang.Text("Minecraft.Download.Stage.DownloadLiteLoaderLibraries"),
                    [])
                {
                    ProgressWeight = 6d
                });
        }

        return loaders;
    }
}