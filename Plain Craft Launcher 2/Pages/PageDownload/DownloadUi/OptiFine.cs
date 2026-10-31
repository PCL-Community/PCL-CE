using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.Core.UI;

namespace PCL;

public static partial class DownloadUi
{
    private static bool _IsOptiFineDownloadRunning(string? displayName)
    {
        if (Loading
            .loaderTaskbar
            .ToList()
            .All(ongoingLoader =>
                (ongoingLoader.name ?? "")
                != (Lang.Text("Minecraft.Download.Stage.OptiFineDownload", displayName) ?? "")))
            return false;
        HintService.Show(Hints.Error(Lang.Text("Minecraft.Download.Error.InstanceDownloading")));
        return true;
    }

    private static void McDownloadOptiFineSave(GameDownload.DlOptiFineListEntry downloadInfo)
    {
        try
        {
            var target = SystemDialogs.SelectSaveFile(
                Lang.Text("Download.Version.SelectSaveLocation"),
                downloadInfo.NameFile ?? "",
                "OptiFine Jar (*.jar)|*.jar"
            );
            if (!target.Contains(@"\"))
                return;

            if (_IsOptiFineDownloadRunning(downloadInfo.DisplayName))
                return;

            var loader = new Loading.LoaderCombo<GameDownload.DlOptiFineListEntry>(
                Lang.Text("Minecraft.Download.Stage.OptiFineDownload", downloadInfo.DisplayName),
                McDownloadOptiFineSaveLoader(downloadInfo, target)
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
                "开始 OptiFine 下载失败",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Minecraft.Download.Error.OperationFailed")
            );
        }
    }
}