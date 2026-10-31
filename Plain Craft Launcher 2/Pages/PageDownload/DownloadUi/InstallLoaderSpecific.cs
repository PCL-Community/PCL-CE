using System.IO;
using PCL.Core.App.Localization;

namespace PCL;

public static partial class DownloadUi
{
    private static void _AddLoaderSpecificLoaders(
        List<Loading.LoaderBase> loaderList,
        McInstallRequest request,
        string tempMcFolder,
        McInstallFolders folders,
        Loading.LoaderCombo<string> clientLoader,
        string minecraftName
    )
    {
        _AddOptiFineLoader(loaderList, request, tempMcFolder, folders, clientLoader);

        if (request.forgeVersion is not null)
            _AddForgelikeLoader(
                loaderList,
                GameDownload.DlForgelikeEntry.ForgelikeType.Forge,
                "Forge",
                request.forgeVersion,
                $"forge-{request.forgeVersion}",
                minecraftName,
                request.forgeEntry,
                tempMcFolder,
                clientLoader,
                request.targetInstanceFolder,
                request.fabricVersion is null
                && request.liteLoaderEntry is null
                && request.neoForgeEntry is null
            );

        if (request.neoForgeVersion is not null)
            _AddForgelikeLoader(
                loaderList,
                GameDownload.DlForgelikeEntry.ForgelikeType.NeoForge,
                "NeoForge",
                request.neoForgeVersion,
                $"neoforge-{request.neoForgeVersion}",
                minecraftName,
                request.neoForgeEntry,
                tempMcFolder,
                clientLoader,
                request.targetInstanceFolder,
                request.forgeEntry is null
                && request.fabricVersion is null
                && request.liteLoaderEntry is null
            );

        if (request.cleanroomVersion is not null)
            _AddForgelikeLoader(
                loaderList,
                GameDownload.DlForgelikeEntry.ForgelikeType.Cleanroom,
                "Cleanroom",
                request.cleanroomVersion,
                $"cleanroom-{request.cleanroomVersion}",
                minecraftName,
                request.cleanroomEntry,
                tempMcFolder,
                clientLoader,
                request.targetInstanceFolder,
                request.forgeEntry is null
                && request.fabricVersion is null
                && request.liteLoaderEntry is null
            );

        if (request.liteLoaderEntry is not null)
            _AddLiteLoaderLoader(
                loaderList,
                request,
                tempMcFolder,
                clientLoader,
                minecraftName);

        if (request.fabricVersion is not null)
            _AddFabricLoader(loaderList, request, tempMcFolder, minecraftName);

        if (request.legacyFabricVersion is not null)
            _AddLegacyFabricLoader(loaderList, request, tempMcFolder, minecraftName);
    }

    private static void _AddOptiFineLoader(
        List<Loading.LoaderBase> loaderList,
        McInstallRequest request,
        string tempMcFolder,
        McInstallFolders folders,
        Loading.LoaderCombo<string> clientLoader
    )
    {
        if (request.optiFineEntry is null)
            return;

        if (folders.OptiFineAsMod)
        {
            if (folders.OptiFineFolder is not { } optiFineFolder
                || request.optiFineEntry.NameFile is not { } optiFineFile)
                throw new InvalidOperationException("OptiFine 缓存信息不完整");
            loaderList.Add(
                new Loading.LoaderCombo<string>(
                    Lang.Text(
                        "Minecraft.Download.Stage.LoaderDownloadCombo",
                        "OptiFine",
                        request.optiFineEntry.DisplayName),
                    McDownloadOptiFineSaveLoader(
                        request.optiFineEntry,
                        Path.Combine(optiFineFolder, optiFineFile)
                    )
                )
                {
                    show = false,
                    ProgressWeight = 16d,
                    block =
                        request.forgeVersion is null
                        && request.neoForgeVersion is null
                        && request.fabricVersion is null
                        && request.liteLoaderEntry is null,
                    Identifier = DownloadIdentifier(
                        "OptiFine",
                        request.optiFineEntry.NameVersion)
                }
            );
        }
        else
            loaderList.Add(
                new Loading.LoaderCombo<string>(
                    Lang.Text(
                        "Minecraft.Download.Stage.LoaderDownloadCombo",
                        "OptiFine",
                        request.optiFineEntry.DisplayName),
                    McDownloadOptiFineLoader(
                        request.optiFineEntry,
                        tempMcFolder,
                        clientLoader,
                        request.targetInstanceFolder,
                        false)
                )
                {
                    show = false,
                    ProgressWeight = 24d,
                    block =
                        request.forgeVersion is null
                        && request.neoForgeVersion is null
                        && request.fabricVersion is null
                        && request.liteLoaderEntry is null,
                    Identifier = DownloadIdentifier(
                        "OptiFine",
                        request.optiFineEntry.NameVersion)
                }
            );
    }

    private static void _AddForgelikeLoader(
        List<Loading.LoaderBase> loaderList,
        GameDownload.DlForgelikeEntry.ForgelikeType forgeType,
        string displayName,
        string loaderVersion,
        string targetVersion,
        string minecraftName,
        GameDownload.DlForgelikeEntry? entry,
        string tempMcFolder,
        Loading.LoaderCombo<string> clientLoader,
        string targetInstanceFolder,
        bool block
    )
    {
        loaderList.Add(
            new Loading.LoaderCombo<string>(
                Lang.Text(
                    "Minecraft.Download.Stage.LoaderDownloadCombo",
                    displayName,
                    loaderVersion),
                McDownloadForgelikeLoader(
                    forgeType,
                    loaderVersion,
                    targetVersion,
                    minecraftName,
                    entry,
                    tempMcFolder,
                    clientLoader,
                    targetInstanceFolder
                )
            )
            {
                show = false,
                ProgressWeight = 25d,
                block = block
            }
        );
    }

    private static void _AddLiteLoaderLoader(
        List<Loading.LoaderBase> loaderList,
        McInstallRequest request,
        string tempMcFolder,
        Loading.LoaderCombo<string> clientLoader,
        string minecraftName
    )
    {
        if (request.liteLoaderEntry is not { } liteLoaderEntry)
            return;
        loaderList.Add(
            new Loading.LoaderCombo<string>(
                Lang.Text(
                    "Minecraft.Download.Stage.LoaderDownloadCombo",
                    "LiteLoader",
                    minecraftName),
                McDownloadLiteLoaderLoader(
                    liteLoaderEntry,
                    tempMcFolder,
                    clientLoader,
                    false)
            )
            {
                show = false,
                ProgressWeight = 1d,
                block = request.fabricVersion is null
            }
        );
    }

    private static void _AddFabricLoader(
        List<Loading.LoaderBase> loaderList,
        McInstallRequest request,
        string tempMcFolder,
        string minecraftName)
    {
        if (request.fabricVersion is not { } fabricVersion)
            return;
        loaderList.Add(
            new Loading.LoaderCombo<string>(
                Lang.Text(
                    "Minecraft.Download.Stage.LoaderDownloadCombo",
                    "Fabric",
                    fabricVersion),
                McDownloadFabricLoader(
                    fabricVersion,
                    minecraftName,
                    tempMcFolder,
                    false)
            )
            {
                show = false,
                ProgressWeight = 2d,
                block = true
            }
        );
    }

    private static void _AddLegacyFabricLoader(
        List<Loading.LoaderBase> loaderList,
        McInstallRequest request,
        string tempMcFolder,
        string minecraftName)
    {
        if (request.legacyFabricVersion is not { } legacyFabricVersion)
            return;
        loaderList.Add(
            new Loading.LoaderCombo<string>(
                Lang.Text(
                    "Minecraft.Download.Stage.LoaderDownloadCombo",
                    "Legacy Fabric",
                    legacyFabricVersion),
                McDownloadLegacyFabricLoader(
                    legacyFabricVersion,
                    minecraftName,
                    tempMcFolder,
                    false)
            )
            {
                show = false,
                ProgressWeight = 2d,
                block = true
            }
        );
    }
}