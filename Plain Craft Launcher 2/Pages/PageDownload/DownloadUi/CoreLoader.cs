using System.IO;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Network;
using PCL.Network.Loaders;

namespace PCL;

public static partial class DownloadUi
{
    /// <summary>
    ///     获取下载某个 Minecraft 版本的加载器列表。
    ///     它必须安装到 McFolderSelected，但是可以自定义版本名（不过自定义的实例名不会修改 Json 中的 id 项）。
    /// </summary>
    private static List<Loading.LoaderBase> McDownloadClientLoader(
        string id,
        string? jsonUrl = null,
        string? instanceName = null)
    {
        instanceName ??= id;
        var instanceFolder = Path.Combine(GameFolders.mcFolderSelected ?? "", "versions", instanceName);

        var loaders = new List<Loading.LoaderBase>();

        if (jsonUrl is null)
            loaders.Add(
                new Loading.LoaderTask<string, List<DownloadFile>>(
                    Lang.Text("Minecraft.Download.Stage.ObtainVanillaJsonUrl"),
                    task =>
                    {
                        var jsonAddress = GameDownload.DlClientListGet(id)?.ToString();
                        if (jsonAddress is null)
                            throw new Exception(
                                Lang.Text("Minecraft.Download.Error.NoJsonDownloadAddress"));
                        task.output =
                        [
                            new DownloadFile(
                                GameDownload.DlSourceLauncherOrMetaGet(jsonAddress),
                                Path.Combine(instanceFolder, $"{instanceName}.json")
                            )
                        ];
                    }
                )
                {
                    ProgressWeight = 2d,
                    show = false
                }
            );
        loaders.Add(
            new LoaderDownload(
                Lang.Text("Minecraft.Download.Stage.VanillaJsonDownload"),
                [
                    new DownloadFile(
                        GameDownload.DlSourceLauncherOrMetaGet(jsonUrl ?? ""),
                        Path.Combine(instanceFolder, $"{instanceName}.json"),
                        new Common.FileChecker(canUseExistsFile: false, isJson: true)
                    )
                ]
            )
            {
                ProgressWeight = 3d,
                Identifier = McDownloadClientJsonId
            }
        );

        var loadersLib = new List<Loading.LoaderBase>
        {
            new Loading.LoaderTask<string, List<DownloadFile>>(
                Lang.Text("Minecraft.Download.Stage.AnalyzeVanillaLibraries.Side"),
                task =>
                {
                    var jsonPath = Path.Combine(instanceFolder, $"{instanceName}.json");
                    Common.WaitForFileReady(jsonPath);
                    Common.Log($"[Download] 开始分析原版支持库文件：{instanceFolder}");
                    if (id == "1.16.5" && Config.Download.FixAuthLib)
                        try
                        {
                            var json = Common.ReadFile(jsonPath);
                            json = json
                                .Replace("2.1.28/authlib-2.1.28.jar", "2.3.31/authlib-2.3.31.jar")
                                .Replace("com.mojang:authlib:2.1.28", "com.mojang:authlib:2.3.31")
                                .Replace(
                                    "ad54da276bf59983d02d5ed16fc14541354c71fd",
                                    "bbd00ca33b052f73a6312254780fc580d2da3535")
                                .Replace("76328", "87662");
                            Common.WriteFile(jsonPath, json);
                        }
                        catch (Exception ex)
                        {
                            Common.Log($"[Download] 替换 Authlib 版本失败: {ex.Message}");
                        }

                    task.output =
                        Libraries.McLibNetFilesFromInstance(new McInstance(instanceFolder));
                }
            )
            {
                ProgressWeight = 1d,
                show = false
            },
            new LoaderDownload(
                Lang.Text("Minecraft.Download.Stage.DownloadVanillaLibraries.Side"),
                [])
            {
                ProgressWeight = 13d,
                show = false
            }
        };
        loaders.Add(
            new Loading.LoaderCombo<string>(
                Lang.Text("Minecraft.Download.Stage.VanillaLibrariesDownload"),
                loadersLib)
            {
                block = false,
                ProgressWeight = 14d,
                Identifier = McDownloadClientLibId
            });

        var loadersAssets = new List<Loading.LoaderBase>
        {
            new Loading.LoaderTask<string, List<DownloadFile>>(
                Lang.Text("Minecraft.Download.Stage.AnalyzeAssetsIndex.Side"),
                task =>
                {
                    Common.WaitForFileReady(Path.Combine(instanceFolder, $"{instanceName}.json"));
                    try
                    {
                        var assetIndex = new McInstance(instanceFolder);
                        task.output = GameDownload.DlClientAssetIndexGet(assetIndex) is { } assetIndexFile
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
                                    instanceFolder,
                                    $"{instanceName}.json")));
                        versionJson.Add("clientVersion", id);
                        Common.WriteFile(
                            Path.Combine(instanceFolder, $"{instanceName}.json"),
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
                        new McInstance(instanceFolder),
                        true,
                        ref argprogressFeed);
                }
            )
            {
                ProgressWeight = 0.01d,
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
                ProgressWeight = 18d
            }
        );

        return loaders;
    }
}