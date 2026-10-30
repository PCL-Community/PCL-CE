using System.IO;
using PCL.Core.App.Localization;
using PCL.Network;
using PCL.Network.Loaders;

namespace PCL;

public static partial class DownloadUi
{
    /// <summary>
    ///     获取下载某个 LabyMod 实例的加载器列表。
    /// </summary>
    private static List<Loading.LoaderBase> McDownloadLabyModLoader(
        string labyModCommitRef,
        string? labyModChannel,
        string minecraftName,
        string? mcFolder = null,
        bool fixLibrary = true)
    {
        mcFolder ??= GameFolders.mcFolderSelected ?? "";
        var id = $"labymod-{labyModCommitRef}-{minecraftName}";
        var versionFolder = Path.Combine(mcFolder ?? "", "versions", id);
        var loaders = new List<Loading.LoaderBase>();

        minecraftName = minecraftName.Replace("∞", "infinite"); // 放在 ID 后面避免影响实例文件夹名称
        loaders.Add(
            new Loading.LoaderTask<string, List<DownloadFile>>(
                Lang.Text("Minecraft.Download.Stage.ObtainLabyModClientUrl"),
                task =>
                {
                    if (fixLibrary)
                        McDownloadClient(
                            NetPreDownloadBehaviour.ExitWhileExistsOrDownloading,
                            minecraftName,
                            $"https://releases.r2.labymod.net/api/v1/download/manifest/labymod4/{labyModChannel}/{minecraftName}/{labyModCommitRef}.json"
                        );
                    task.Progress = 0.5d;
                    task.output =
                    [
                        new DownloadFile(
                            [
                                $"https://releases.r2.labymod.net/api/v1/download/manifest/labymod4/{labyModChannel}/{minecraftName}/{labyModCommitRef}.json"
                            ],
                            Path.Combine(versionFolder, $"{id}.json"),
                            new Common.FileChecker(isJson: true)
                        )
                    ];
                    task.Progress = 1d;
                }
            )
            {
                ProgressWeight = 2d
            }
        );
        loaders.Add(
            new LoaderDownload(
                Lang.Text("Minecraft.Download.Stage.DownloadLoaderLibraries", "Fabric"),
                [])
            {
                ProgressWeight = 10d
            }
        );
        if (fixLibrary)
        {
            loaders.Add(
                new Loading.LoaderTask<string, List<DownloadFile>>(
                    Lang.Text("Minecraft.Download.Stage.AnalyzeLabyModLibraries"),
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
                    Lang.Text("Minecraft.Download.Stage.DownloadLoaderLibraries", "LabyMod"),
                    [])
                {
                    ProgressWeight = 8d
                }
            );
        }

        return loaders;
    }

    /// <summary>
    ///     获取下载某个 Minecraft 实例的加载器列表。
    ///     它必须安装到 PathMcFolder，但是可以自定义实例名（不过自定义的实例名不会修改 Json 中的 id 项）。
    /// </summary>
    private static List<Loading.LoaderBase> McDownloadLabyModClientLoader(
        string id,
        string? labyChannel,
        string labyCommitRef,
        string? versionName = null
    )
    {
        versionName ??= id;
        var versionFolder =
            $@"{Path.Combine(GameFolders.mcFolderSelected ?? "", "versions", versionName)}\";

        var loaders = new List<Loading.LoaderBase>();

        var loadersLib = new List<Loading.LoaderBase>
        {
            new Loading.LoaderTask<string, List<DownloadFile>>(
                Lang.Text("Minecraft.Download.Stage.AnalyzeVanillaAndLabyModLibrariesSide"),
                task =>
                {
                    Common.WaitForFileReady(Path.Combine(versionFolder, $"{versionName}.json"));
                    Common.Log($"[Download] 开始分析原版与 LabyMod 支持库文件：{versionFolder}");
                    task.output =
                        Libraries.McLibNetFilesFromInstance(new McInstance(versionFolder));
                }
            )
            {
                ProgressWeight = 1d,
                show = false
            },
            new LoaderDownload(
                Lang.Text("Minecraft.Download.Stage.DownloadVanillaAndLabyModLibrariesSide"),
                [])
            {
                ProgressWeight = 13d,
                show = false
            }
        };
        loaders.Add(
            new Loading.LoaderCombo<string>(McDownloadClientLibName, loadersLib)
            {
                block = false,
                ProgressWeight = 14d
            });

        var loadersAssets = new List<Loading.LoaderBase>
        {
            new Loading.LoaderTask<string, List<DownloadFile>>(
                Lang.Text("Minecraft.Download.Stage.AnalyzeAssetsIndex.Side"),
                task =>
                {
                    try
                    {
                        var version = new McInstance(versionFolder);
                        task.output = GameDownload.DlClientAssetIndexGet(version) is { } assetIndexFile
                            ? [assetIndexFile]
                            : [];
                    }
                    catch (Exception ex)
                    {
                        throw new Exception(
                            Lang.Text("Minecraft.Download.Error.AssetIndexAnalysisFailed"),
                            ex);
                    }

                    try
                    {
                        var versionJson = (JsonObject)Common.GetJson(
                            Common.ReadFile(
                                Path.Combine(
                                    versionFolder,
                                    $"{versionName}.json")));
                        versionJson.Add("clientVersion", id);
                        Common.WriteFile(
                            Path.Combine(versionFolder, $"{versionName}.json"),
                            versionJson.ToString());
                    }
                    catch (Exception ex)
                    {
                        throw new Exception(
                            Lang.Text("Minecraft.Download.Error.AddClientVersionFailed"),
                            ex);
                    }
                }
            )
            {
                ProgressWeight = 1d,
                show = false
            },
            new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadAssetsIndex.Side"), [])
            {
                ProgressWeight = 3d,
                show = false
            },
            new Loading.LoaderTask<string, List<DownloadFile>>(
                Lang.Text("Minecraft.Download.Stage.AnalyzeRequiredAssets.Side"),
                task =>
                {
                    Loading.LoaderBase argprogressFeed = task;
                    task.output = GameAssets.McAssetsFixList(
                        new McInstance(versionFolder),
                        true,
                        ref argprogressFeed);
                }
            )
            {
                ProgressWeight = 3d,
                show = false
            },
            new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadAssets.Side"), [])
            {
                ProgressWeight = 14d,
                show = false
            }
        };
        loaders.Add(
            new Loading.LoaderCombo<string>(
                Lang.Text("Minecraft.Download.Stage.DownloadVanillaAssets"),
                loadersAssets)
            {
                block = false,
                ProgressWeight = 21d
            }
        );

        return loaders;
    }
}