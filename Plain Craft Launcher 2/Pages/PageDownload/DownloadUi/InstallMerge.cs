using System.IO;
using PCL.Core.App.Localization;
using PCL.Network;
using PCL.Network.Loaders;

namespace PCL;

public static partial class DownloadUi
{
    private static Loading.LoaderTask<string, string> _CreateMergeInstallLoader(
        string instanceFolder,
        string tempMcFolder,
        McInstallFolders folders,
        McInstallRequest request
    )
    {
        return new Loading.LoaderTask<string, string>(
            Lang.Text("Minecraft.Download.Stage.InstallGame"),
            task =>
            {
                MergeJson(
                    instanceFolder,
                    instanceFolder,
                    folders.OptiFineFolder,
                    folders.OptiFineAsMod,
                    folders.ForgeFolder,
                    request.forgeVersion,
                    folders.NeoForgeFolder,
                    request.neoForgeVersion,
                    folders.CleanroomFolder,
                    request.cleanroomVersion,
                    folders.FabricFolder,
                    folders.LabyModFolder,
                    request.labyModChannel,
                    folders.LiteLoaderFolder,
                    request.mmcPackInfo,
                    folders.LegacyFabricFolder
                );
                task.Progress = 0.2d;
                if (Directory.Exists(Path.Combine(tempMcFolder, "libraries")))
                    Common.CopyDirectory(
                        Path.Combine(tempMcFolder, "libraries"),
                        Path.Combine(GameFolders.mcFolderSelected ?? "", "libraries"));
                task.Progress = 0.8d;
                var modsFolder = Path.Combine(new McInstance(instanceFolder).PathIndie, "mods");
                if (Directory.Exists(folders.ModsTempFolder))
                {
                    Common.CopyDirectory(folders.ModsTempFolder, modsFolder);
                }
                else if (folders.Modable)
                {
                    Directory.CreateDirectory(modsFolder);
                    Common.Log($"[Download] 自动创建 Mod 文件夹：{modsFolder}");
                }

                var resourcepacksFolder = Path.Combine(
                    new McInstance(instanceFolder).PathIndie,
                    "resourcepacks");
                Directory.CreateDirectory(resourcepacksFolder);
                Common.Log($"[Download] 自动创建资源包文件夹：{resourcepacksFolder}");
            }
        )
        {
            ProgressWeight = 2d,
            block = true
        };
    }

    private static bool _ShouldFixInstallLibraries(McInstallRequest request)
    {
        return request.mmcPackInfo is not null
            || request.optiFineEntry is not null
            || (request.forgeVersion is not null
                && Convert.ToDouble(request.forgeVersion.BeforeFirst(".")) >= 20d)
            || request.neoForgeVersion is not null
            || request.fabricVersion is not null
            || request.cleanroomVersion is not null
            || request.liteLoaderEntry is not null
            || request.labyModCommitRef is not null;
    }

    private static void _AddLibraryFixLoader(
        List<Loading.LoaderBase> loaderList,
        McInstallRequest request,
        string instanceFolder,
        string minecraftName)
    {
        if (request.labyModCommitRef is not null)
        {
            var labyModClientLoader = new Loading.LoaderCombo<string>(
                Lang.Text(
                    "Minecraft.Download.Stage.LoaderDownloadCombo",
                    Lang.Text("Minecraft.Version.Vanilla"),
                    minecraftName),
                McDownloadLabyModClientLoader(
                    minecraftName,
                    request.labyModChannel,
                    request.labyModCommitRef,
                    request.targetInstanceName
                )
            )
            {
                show = false,
                ProgressWeight = 39d,
                block = false
            };
            loaderList.Add(labyModClientLoader);
            return;
        }

        var loadersLib = new List<Loading.LoaderBase>
        {
            new Loading.LoaderTask<string, List<DownloadFile>>(
                Lang.Text("Minecraft.Download.Stage.AnalyzeGameLibrariesSide"),
                task => task.output =
                    Libraries.McLibNetFilesFromInstance(new McInstance(instanceFolder))
            )
            {
                ProgressWeight = 1d,
                show = false
            },
            new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadGameLibrariesSide"), [])
            {
                ProgressWeight = 7d,
                show = false
            }
        };
        loaderList.Add(
            new Loading.LoaderCombo<string>(
                Lang.Text("Minecraft.Download.Stage.DownloadGameLibraries"),
                loadersLib)
            {
                ProgressWeight = 8d
            }
        );
    }
}