using PCL.Core.App.Localization;
using PCL.Core.UI;
using PCL.Network;
using PCL.Network.Loaders;

namespace PCL;

public static partial class DownloadUi
{
    public static void McDownloadForgelikeSave(GameDownload.DlForgelikeEntry info)
    {
        try
        {
            var target = SystemDialogs.SelectSaveFile(
                Lang.Text("Download.Version.SelectSaveLocation"),
                $"{info.LoaderName}-{info.Inherit}-{info.VersionName}.{info.FileExtension}",
                Lang.Text(
                    "Minecraft.Download.Stage.ForgelikeInstallerFilter",
                    info.LoaderName,
                    info.FileExtension)
            );
            var displayName = $"{info.LoaderName} {info.Inherit} - {info.VersionName}";
            if (!target.Contains(@"\"))
                return;

            if (_IsForgelikeDownloadRunning(info))
                return;

            var loaders = new List<Loading.LoaderBase>
            {
                new LoaderDownload(
                    Lang.Text("Minecraft.Download.Stage.DownloadMainFile"),
                    _CreateForgelikeDownloadFiles(info, target, false)
                )
                {
                    ProgressWeight = 6d
                }
            };

            var loader = new Loading.LoaderCombo<GameDownload.DlForgelikeEntry>(
                Lang.Text("Minecraft.Download.Stage.ForgelikeDownload", displayName),
                loaders
            )
            {
                OnStateChanged = LoaderStateChangedHintOnly,
                Identifier = DownloadIdentifier(
                    "Forgelike",
                    $"{info.forgeType}:{info.Inherit}:{info.VersionName}")
            };
            loader.Start(info);
            Loading.LoaderTaskbarAdd(loader);
            Launcher.frmMain?.BtnExtraDownload.ShowRefresh();
            Launcher.frmMain?.BtnExtraDownload.Ribble();
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                $"开始 {info.LoaderName} 安装器下载失败",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Minecraft.Download.Error.OperationFailed")
            );
        }
    }

    private static bool _IsForgelikeDownloadRunning(GameDownload.DlForgelikeEntry info)
    {
        if (Loading
            .loaderTaskbar
            .ToList()
            .Any(ongoingLoader => ongoingLoader.Identifier == DownloadIdentifier(
                "Forgelike",
                $"{info.forgeType}:{info.Inherit}:{info.VersionName}")))
        {
            HintService.Hint(
                Lang.Text("Minecraft.Download.Error.InstanceDownloading"),
                HintType.Error);
            return true;
        }

        return false;
    }

    private static List<DownloadFile> _CreateForgelikeDownloadFiles(
        GameDownload.DlForgelikeEntry info,
        string target,
        bool normalizeForgeInherit
    )
    {
        var files = new List<DownloadFile>();
        switch (info.forgeType)
        {
            case GameDownload.DlForgelikeEntry.ForgelikeType.NeoForge:
            {
                var neo = (GameDownload.DlNeoForgeListEntry)info;
                var url = $"{neo.UrlBase}-installer.jar";
                files.Add(
                    new DownloadFile(
                        [
                            url.Replace(
                                "maven.neoforged.net/releases",
                                "bmclapi2.bangbang93.com/maven"),
                            url
                        ],
                        target,
                        new Common.FileChecker(64 * 1024)
                    )
                );
                break;
            }

            case GameDownload.DlForgelikeEntry.ForgelikeType.Cleanroom:
            {
                var cleanroom = (GameDownload.DlCleanroomListEntry)info;
                var url = $"{cleanroom.UrlBase}-installer.jar";
                files.Add(new DownloadFile([url], target, new Common.FileChecker(64 * 1024)));
                break;
            }

            case GameDownload.DlForgelikeEntry.ForgelikeType.Forge:
            default:
            {
                var forge = (GameDownload.DlForgeVersionEntry)info;
                var inheritName = normalizeForgeInherit
                    ? forge.Inherit.Replace("-", "_")
                    : forge.Inherit;
                var fileName =
                    $"{inheritName}-{forge.FileVersion}/forge-{inheritName}-{forge.FileVersion}-{forge.Category}.{forge.FileExtension}";
                files.Add(
                    new DownloadFile(
                        [
                            $"https://bmclapi2.bangbang93.com/maven/net/minecraftforge/forge/{fileName}",
                            $"https://files.minecraftforge.net/maven/net/minecraftforge/forge/{fileName}"
                        ],
                        target,
                        new Common.FileChecker(64 * 1024, hash: forge.Hash)
                    )
                );
                break;
            }
        }

        return files;
    }
}