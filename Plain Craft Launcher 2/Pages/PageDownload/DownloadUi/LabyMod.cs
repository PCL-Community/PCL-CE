using PCL.Core.App.Localization;
using PCL.Core.UI;
using PCL.Network;
using PCL.Network.Loaders;

namespace PCL;

public static partial class DownloadUi
{
    public static void McDownloadLabyModProductionLoaderSave()
    {
        try
        {
            const string url = "https://releases.labymod.net/api/v1/installer/production/java";
            const string fileName = "LabyMod4ProductionInstaller.jar";
            var target = SystemDialogs.SelectSaveFile(
                Lang.Text("Download.Version.SelectSaveLocation"),
                fileName,
                Lang.Text("Download.Version.Installer.LabyMod.Filter")
            );
            if (!target.Contains('\\'))
                return;

            if (Loading
                .loaderTaskbar
                .ToList()
                .Any(ongoingLoader => (ongoingLoader.name ?? "")
                    == (Lang.Text("Minecraft.Download.Stage.LabyModInstallerDownload") ?? "")))
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
                Lang.Text("Minecraft.Download.Stage.LabyModInstallerDownload"),
                loaders)
            {
                OnStateChanged = LoaderStateChangedHintOnly
            };
            loader.Start();
            Loading.LoaderTaskbarAdd(loader);
            Launcher.frmMain?.BtnExtraDownload.ShowRefresh();
            Launcher.frmMain?.BtnExtraDownload.Ribble();
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "开始 LabyMod 安装器下载失败",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Minecraft.Download.Error.OperationFailed")
            );
        }
    }

    public static void McDownloadLabyModSnapshotLoaderSave()
    {
        try
        {
            const string url = "https://releases.labymod.net/api/v1/installer/snapshot/java";
            const string fileName = "LabyMod4SnapshotInstaller.jar";
            var target = SystemDialogs.SelectSaveFile(
                Lang.Text("Download.Version.SelectSaveLocation"),
                fileName,
                Lang.Text("Download.Version.Installer.LabyMod.Filter")
            );
            if (!target.Contains('\\'))
                return;

            if (Loading
                .loaderTaskbar
                .ToList()
                .Any(ongoingLoader => (ongoingLoader.name ?? "")
                    == (Lang.Text("Minecraft.Download.Stage.LabyModInstallerDownload") ?? "")))
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
                Lang.Text("Minecraft.Download.Stage.LabyModInstallerDownload"),
                loaders)
            {
                OnStateChanged = LoaderStateChangedHintOnly
            };
            loader.Start();
            Loading.LoaderTaskbarAdd(loader);
            Launcher.frmMain?.BtnExtraDownload.ShowRefresh();
            Launcher.frmMain?.BtnExtraDownload.Ribble();
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "开始 LabyMod 安装器下载失败",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Minecraft.Download.Error.OperationFailed")
            );
        }
    }
}