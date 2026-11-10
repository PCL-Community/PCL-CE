using System.IO;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.Network.Loaders;

namespace PCL;

public static partial class DownloadUi
{
    /// <summary>
    ///     获取合并安装加载器列表，并进行前期的缓存清理与 Java 检查工作。
    /// </summary>
    /// <exception cref="CancelledException" />
    public static List<Loading.LoaderBase> McInstallLoader(
        McInstallRequest request,
        bool dontFixLibraries = false,
        bool ignoreDump = false)
    {
        RestrictedFeaturePolicy.ThrowIfRestricted("minecraft-install");
        // 安装 Mod 加载器的文件夹不能包含空格
        var tempMcFolder = Launcher.RequestTaskTempFolder(
            request.optiFineEntry is not null
            || request.forgeEntry is not null
            || request.neoForgeEntry is not null
        );

        var minecraftName = request.minecraftName
            ?? throw new Exception(
                Lang.Text("Minecraft.Download.Modpack.MissingGameVersion.Generic"));
        var instanceFolder = Path.Combine(
            GameFolders.mcFolderSelected ?? "",
            "versions",
            request.targetInstanceName);
        if (Directory.Exists(tempMcFolder))
            Common.DeleteDirectory(tempMcFolder);

        var folders = _PrepareMcInstallFolders(request, tempMcFolder, minecraftName);
        _LogMcInstallFolders(folders, minecraftName);

        if (File.Exists(Path.Combine(instanceFolder, $"{request.targetInstanceName}.json"))
            && !ignoreDump)
        {
            HintService.Show(Hints.Error(Lang.Text(
                    "Minecraft.Download.Error.InstanceAlreadyExists",
                    request.targetInstanceName,
                    "")));
            throw new CancelledException();
        }

        var loaderList = new List<Loading.LoaderBase>
        {
            _CreateIgnoreFlagLoader(instanceFolder)
        };
        _AddApiDownloadLoaders(loaderList, request, folders.ModsTempFolder);

        var clientLoader = _AddClientOrLabyModLoader(
            loaderList,
            request,
            tempMcFolder,
            minecraftName);
        if (clientLoader is not null)
            _AddLoaderSpecificLoaders(
                loaderList,
                request,
                tempMcFolder,
                folders,
                clientLoader,
                minecraftName);

        loaderList.Add(_CreateMergeInstallLoader(instanceFolder, tempMcFolder, folders, request));
        if (!dontFixLibraries && _ShouldFixInstallLibraries(request))
            _AddLibraryFixLoader(loaderList, request, instanceFolder, minecraftName);
        loaderList.Add(_CreateDeleteIgnoreFlagLoader(instanceFolder));
        return loaderList;
    }

    private static McInstallFolders _PrepareMcInstallFolders(
        McInstallRequest request,
        string tempMcFolder,
        string minecraftName)
    {
        var folders = new McInstallFolders();

        if (request.optiFineVersion is not null)
        {
            if (request.optiFineVersion.Contains("_HD_U_"))
                request.optiFineVersion = $"HD_U_{request.optiFineVersion.AfterLast("_HD_U_")}";
            request.optiFineEntry = new GameDownload.DlOptiFineListEntry
            {
                DisplayName =
                    $"{minecraftName} {request.optiFineVersion.Replace("HD_U_", "").Replace("_", "").Replace("pre", " pre")}",
                Inherit = minecraftName,
                IsPreview = request.optiFineVersion.ContainsF("pre", true),
                NameVersion = $"{minecraftName}-OptiFine_{request.optiFineVersion}",
                NameFile =
                    $"{(request.optiFineVersion.ContainsF("pre", true) ? "preview_" : "")}OptiFine_{minecraftName}_{request.optiFineVersion}.jar"
            };
        }

        if (request.optiFineEntry is not null)
            folders.OptiFineFolder = Path.Combine(
                tempMcFolder,
                "versions",
                request.optiFineEntry.NameVersion
                ?? throw new InvalidOperationException("OptiFine 版本缺少实例名"));

        if (request.forgeEntry is not null)
            request.forgeVersion = request.forgeVersion ?? request.forgeEntry.VersionName;
        if (request.forgeVersion is not null)
            folders.ForgeFolder = Path.Combine(
                tempMcFolder,
                "versions",
                $"forge-{request.forgeVersion}");

        if (request.neoForgeEntry is not null)
            request.neoForgeVersion = request.neoForgeVersion ?? request.neoForgeEntry.VersionName;
        if (request.neoForgeVersion is not null)
            folders.NeoForgeFolder = Path.Combine(
                tempMcFolder,
                "versions",
                $"neoforge-{request.neoForgeVersion}");

        if (request.cleanroomEntry is not null)
            request.cleanroomVersion =
                request.cleanroomVersion ?? request.cleanroomEntry.VersionName;
        if (request.cleanroomVersion is not null)
            folders.CleanroomFolder = Path.Combine(
                tempMcFolder,
                "versions",
                $"cleanroom-{request.cleanroomVersion}");

        if (request.fabricVersion is not null)
            folders.FabricFolder = Path.Combine(
                tempMcFolder,
                "versions",
                $"fabric-loader-{request.fabricVersion}-{minecraftName}"
            );

        if (request.legacyFabricVersion is not null)
            folders.LegacyFabricFolder = Path.Combine(
                tempMcFolder,
                "versions",
                $"legacy-fabric-loader-{request.legacyFabricVersion}-{minecraftName}"
            );

        if (request.labyModCommitRef is not null)
            folders.LabyModFolder = Path.Combine(
                tempMcFolder,
                "versions",
                $"labymod-{request.labyModCommitRef}-{minecraftName}"
            );

        if (request.liteLoaderEntry is not null)
            folders.LiteLoaderFolder = Path.Combine(
                tempMcFolder,
                "versions",
                $"{minecraftName}-LiteLoader");

        folders.Modable =
            request.fabricVersion is not null
            || request.legacyFabricVersion is not null
            || request.forgeEntry is not null
            || request.neoForgeEntry is not null
            || request.liteLoaderEntry is not null;
        folders.ModsTempFolder = $@"{Path.Combine(tempMcFolder, "mods")}\";
        folders.OptiFineAsMod = request.optiFineEntry is not null && folders.Modable;
        if (folders.OptiFineAsMod)
        {
            Common.Log("[Download] OptiFine 将作为 Mod 进行下载");
            if (request.liteLoaderEntry is not null)
                folders.OptiFineFolder = CombineCacheSubfolder(
                    folders.ModsTempFolder,
                    minecraftName);
            else
                folders.OptiFineFolder = folders.ModsTempFolder;
        }

        return folders;
    }

    private static void _LogMcInstallFolders(McInstallFolders folders, string minecraftName)
    {
        if (folders.OptiFineFolder is not null)
            Common.Log($"[Download] OptiFine 缓存：{folders.OptiFineFolder}");
        if (folders.ForgeFolder is not null)
            Common.Log($"[Download] Forge 缓存：{folders.ForgeFolder}");
        if (folders.NeoForgeFolder is not null)
            Common.Log($"[Download] NeoForge 缓存：{folders.NeoForgeFolder}");
        if (folders.CleanroomFolder is not null)
            Common.Log($"[Download] Cleanroom 缓存：{folders.CleanroomFolder}");
        if (folders.FabricFolder is not null)
            Common.Log($"[Download] Fabric 缓存：{folders.FabricFolder}");
        if (folders.LegacyFabricFolder is not null)
            Common.Log($"[Download] LegacyFabric 缓存：{folders.LegacyFabricFolder}");
        if (folders.LabyModFolder is not null)
            Common.Log($"[Download] LabyMod 缓存：{folders.LabyModFolder}");
        if (folders.LiteLoaderFolder is not null)
            Common.Log($"[Download] LiteLoader 缓存：{folders.LiteLoaderFolder}");
        Common.Log($"[Download] 对应的原版版本：{minecraftName}");
    }

    private static Loading.LoaderTask<int, int> _CreateIgnoreFlagLoader(string instanceFolder)
    {
        return new Loading.LoaderTask<int, int>(
            Lang.Text("Minecraft.Download.Stage.AddIgnoreFlag"),
            _ => Common.WriteFile(
                Path.Combine(instanceFolder, ".pclignore"),
                "用于临时地在 PCL 的实例列表中屏蔽此实例。")
        )
        {
            show = false,
            block = false
        };
    }

    private static Loading.LoaderTask<int, int> _CreateDeleteIgnoreFlagLoader(string instanceFolder)
    {
        return new Loading.LoaderTask<int, int>(
            Lang.Text("Minecraft.Download.Stage.DeleteIgnoreFlag"),
            _ => File.Delete(Path.Combine(instanceFolder, ".pclignore"))
        )
        {
            show = false
        };
    }

    private static void _AddApiDownloadLoaders(
        List<Loading.LoaderBase> loaderList,
        McInstallRequest request,
        string modsTempFolder)
    {
        if (request.fabricApi is not null)
            loaderList.Add(
                _CreateApiDownloadLoader(
                    "Minecraft.Download.Stage.DownloadFabricApi",
                    request.fabricApi,
                    modsTempFolder));
        if (request.legacyFabricApi is not null)
            loaderList.Add(
                _CreateApiDownloadLoader(
                    "Minecraft.Download.Stage.DownloadLegacyFabricApi",
                    request.legacyFabricApi,
                    modsTempFolder)
            );
        if (request.optiFabric is not null)
            loaderList.Add(
                _CreateApiDownloadLoader(
                    "Minecraft.Download.Stage.DownloadOptiFabric",
                    request.optiFabric,
                    modsTempFolder));
    }

    private static LoaderDownload _CreateApiDownloadLoader(
        string stageName,
        Comp.CompFile api,
        string modsTempFolder)
    {
        return new LoaderDownload(
            Lang.Text(stageName),
            [
                api.ToNetFile(
                    modsTempFolder,
                    Comp.DownloadReason.Dependency,
                    null,
                    Comp.CompLoaderType.Fabric)
            ]
        )
        {
            ProgressWeight = 3d,
            block = false
        };
    }

    private static Loading.LoaderCombo<string>? _AddClientOrLabyModLoader(
        List<Loading.LoaderBase> loaderList,
        McInstallRequest request,
        string tempMcFolder,
        string minecraftName
    )
    {
        if (request.labyModCommitRef is not null)
        {
            loaderList.Add(
                new Loading.LoaderCombo<string>(
                    Lang.Text(
                        "Minecraft.Download.Stage.LoaderDownloadCombo",
                        "LabyMod",
                        request.labyModCommitRef),
                    McDownloadLabyModLoader(
                        request.labyModCommitRef,
                        request.labyModChannel,
                        minecraftName,
                        tempMcFolder,
                        false)
                )
                {
                    show = false,
                    ProgressWeight = 10d,
                    block = true
                }
            );
            return null;
        }

        var clientLoader = new Loading.LoaderCombo<string>(
            Lang.Text(
                "Minecraft.Download.Stage.LoaderDownloadCombo",
                Lang.Text("Minecraft.Version.Vanilla"),
                minecraftName),
            McDownloadClientLoader(
                minecraftName,
                request.minecraftJson,
                request.targetInstanceName)
        )
        {
            show = false,
            ProgressWeight = 39d,
            block =
                request.forgeVersion is null
                && request.neoForgeVersion is null
                && request.optiFineEntry is null
                && request.fabricVersion is null
                && request.liteLoaderEntry is null
                && request.cleanroomEntry is null
                && request.legacyFabricVersion is null
        };
        loaderList.Add(clientLoader);
        return clientLoader;
    }
}