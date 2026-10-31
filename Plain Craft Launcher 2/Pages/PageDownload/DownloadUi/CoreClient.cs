using System.IO;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.Core.UI.Dialogs;
using PCL.Network;

namespace PCL;

public static partial class DownloadUi
{
    /// <summary>
    ///     下载某个 Minecraft 实例，这会创造一个单独的下载任务，失败会跳过执行并要求反馈。
    ///     返回正在下载的任务，若跳过或失败，则返回 Nothing。
    /// </summary>
    /// <param name="Id">所下载的 Minecraft 的版本名。</param>
    /// <param name="JsonUrl">Json 文件的 Mojang 官方地址。</param>
    public static Loading.LoaderCombo<string>? McDownloadClient(
        NetPreDownloadBehaviour behaviour,
        string id,
        string? jsonUrl = null)
    {
        if (!RestrictedFeaturePolicy.Check("minecraft-client-download"))
            return null;
        try
        {
            var versionFolder = Path.Combine(GameFolders.mcFolderSelected ?? "", "versions", id);

            foreach (var ongoingLoader in Loading
                         .loaderTaskbar
                         .ToList()
                         .Where(ongoingLoader =>
                             ongoingLoader.name
                             == Lang.Text("Minecraft.Download.Stage.MinecraftDownload", id)))
            {
                if (behaviour == NetPreDownloadBehaviour.ExitWhileExistsOrDownloading)
                    return (Loading.LoaderCombo<string>)ongoingLoader;
                HintService.Show(Hints.Error(Lang.Text("Minecraft.Download.Error.InstanceDownloading")));
                return (Loading.LoaderCombo<string>)ongoingLoader;
            }

            if (behaviour != NetPreDownloadBehaviour.IgnoreCheck
                && File.Exists(Path.Combine(versionFolder, $"{id}.json"))
                && File.Exists(Path.Combine(versionFolder, $"{id}.jar")))
            {
                if (behaviour == NetPreDownloadBehaviour.ExitWhileExistsOrDownloading)
                    return null;
                if ((DialogService.Instance.Show(new TextDialogRequest
                {
                    Title = Lang.Text("Minecraft.Download.Error.InstanceAlreadyExists.Title"),
                    Content = Lang.Text("Minecraft.Download.Error.InstanceAlreadyExists", id, "\r\n"),
                    Buttons = DialogButtons.ContinueCancel,
                    ForceWait = true
                }).Result?.Button ?? 0)
                    == 1)
                {
                    File.Delete(Path.Combine(versionFolder, $"{id}.jar"));
                    File.Delete(Path.Combine(versionFolder, $"{id}.json"));
                }
                else
                {
                    return null;
                }
            }

            var loader = new Loading.LoaderCombo<string>(
                Lang.Text("Minecraft.Download.Stage.MinecraftDownload", id),
                McDownloadClientLoader(id, jsonUrl)
            )
            {
                OnStateChanged = McInstallState
            };
            loader.Start(versionFolder);
            Loading.LoaderTaskbarAdd(loader);
            Launcher.frmMain?.BtnExtraDownload.ShowRefresh();
            Launcher.frmMain?.BtnExtraDownload.Ribble();
            return loader;
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "开始 Minecraft 下载失败",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Minecraft.Download.Error.OperationFailed")
            );
            return null;
        }
    }
}