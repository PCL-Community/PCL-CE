using System.IO;
using PCL.Core.App.Localization;
using PCL.Network;
using PCL.Network.Loaders;

namespace PCL;

public static partial class DownloadUi
{
    /// <summary>
    ///     获取下载某个 Forgelike 实例的加载器列表。
    /// </summary>
    private static List<Loading.LoaderBase> McDownloadForgelikeLoader(
        GameDownload.DlForgelikeEntry.ForgelikeType forgeType,
        string loaderVersion,
        string targetVersion,
        string inherit,
        GameDownload.DlForgelikeEntry? info = null,
        string? mcFolder = null,
        Loading.LoaderCombo<string>? clientDownloadLoader = null,
        string? clientFolder = null)
    {
        mcFolder = mcFolder ?? GameFolders.mcFolderSelected ?? "";
        info = _ResolveForgelikeInfo(forgeType, ref loaderVersion, ref inherit, info);

        var loaderName = Common.GetStringFromEnum(forgeType) ?? forgeType.ToString();
        var isCustomFolder = (mcFolder ?? "") != (GameFolders.mcFolderSelected ?? "");
        var installerAddress = Path.Combine(
            Launcher.RequestTaskTempFolder(),
            "forge_installer.jar");
        var versionFolder = $@"{Path.Combine(mcFolder ?? "", "versions", targetVersion)}\";
        var loaders = new List<Loading.LoaderBase>();

        if (info is null)
            loaders.Add(
                new Loading.LoaderTask<string, string>(
                    Lang.Text("Minecraft.Download.Stage.ObtainLoaderDetails", loaderName),
                    task => info = _FindForgelikeEntry(task, inherit, loaderVersion, loaderName)
                )
                {
                    ProgressWeight = 3d
                }
            );

        loaders.Add(
            new Loading.LoaderTask<string, List<DownloadFile>>(
                Lang.Text("Minecraft.Download.Stage.PrepareLoaderDownload", loaderName),
                task =>
                {
                    clientDownloadLoader = _EnsureForgelikeClientLoader(
                        clientDownloadLoader,
                        isCustomFolder,
                        inherit);
                    task.output = _CreateForgelikeDownloadFiles(
                        info ?? throw new InvalidOperationException("未获取到加载器信息。"),
                        installerAddress,
                        true);
                }
            )
            {
                ProgressWeight = 0.5d,
                show = false
            }
        );
        loaders.Add(
            new LoaderDownload(
                Lang.Text("Minecraft.Download.Stage.DownloadLoaderMainFile", loaderName),
                [])
            {
                ProgressWeight = 9d
            }
        );

        if (forgeType == GameDownload.DlForgelikeEntry.ForgelikeType.NeoForge
            || Convert.ToDouble(loaderVersion.BeforeFirst(".")) >= 20d)
            _AddModernForgelikeInstallLoaders(
                loaders,
                forgeType,
                loaderName,
                inherit,
                loaderVersion,
                targetVersion,
                installerAddress,
                versionFolder,
                mcFolder ?? "",
                isCustomFolder,
                clientFolder,
                () => clientDownloadLoader
            );
        else
            _AddLegacyForgelikeInstallLoader(
                loaders,
                forgeType,
                loaderVersion,
                installerAddress,
                versionFolder,
                targetVersion,
                mcFolder ?? "",
                inherit
            );

        return loaders;
    }

    private static GameDownload.DlForgelikeEntry? _ResolveForgelikeInfo(
        GameDownload.DlForgelikeEntry.ForgelikeType forgeType,
        ref string loaderVersion,
        ref string inherit,
        GameDownload.DlForgelikeEntry? info)
    {
        switch (forgeType)
        {
            case GameDownload.DlForgelikeEntry.ForgelikeType.NeoForge when info is null:
            {
                if (inherit == "1.20.1" && !loaderVersion.StartsWithF("1.20.1-"))
                    info = new GameDownload.DlNeoForgeListEntry($"1.20.1-{loaderVersion}");
                else
                    info = new GameDownload.DlNeoForgeListEntry(loaderVersion);
                break;
            }

            case GameDownload.DlForgelikeEntry.ForgelikeType.Cleanroom when info is null:
                info = new GameDownload.DlCleanroomListEntry(loaderVersion);
                break;

            case GameDownload.DlForgelikeEntry.ForgelikeType.Forge:
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(forgeType), forgeType, null);
        }

        if (forgeType == GameDownload.DlForgelikeEntry.ForgelikeType.NeoForge
            || !loaderVersion.StartsWithF("1.")
            || !loaderVersion.Contains('-')) return info;
        inherit = loaderVersion.BeforeFirst("-");
        loaderVersion = loaderVersion.AfterLast("-");

        return info;
    }

    private static GameDownload.DlForgelikeEntry _FindForgelikeEntry(
        Loading.LoaderTask<string, string> task,
        string inherit,
        string loaderVersion,
        string loaderName)
    {
        var forgeLoader = new Loading.LoaderTask<string, List<GameDownload.DlForgeVersionEntry>>(
            $"McDownloadForgeLoader {inherit}",
            GameDownload.DlForgeVersionMain
        );
        forgeLoader.WaitForExit(inherit);
        task.Progress = 0.8d;
        foreach (var forgeVersion in (forgeLoader.output ?? []).Where(forgeVersion =>
                     McVersionComparer.CompareVersion(
                         forgeVersion.version.ToString(),
                         loaderVersion)
                     == 0))
            return forgeVersion;

        throw new Exception(
            Lang.Text(
                "Minecraft.Download.Error.LoaderDetailsNotFound",
                loaderName,
                inherit,
                loaderVersion));
    }

    private static Loading.LoaderCombo<string>? _EnsureForgelikeClientLoader(
        Loading.LoaderCombo<string>? clientDownloadLoader,
        bool isCustomFolder,
        string inherit)
    {
        if (clientDownloadLoader is not null)
            return clientDownloadLoader;

        return isCustomFolder
            ? throw new Exception(Lang.Text("Minecraft.Download.Error.NoVanillaLoaderSpecified"))
            : McDownloadClient(NetPreDownloadBehaviour.ExitWhileExistsOrDownloading, inherit);
    }
}