using System.IO;
using System.Text;
using System.Windows;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.Core.UI;
using PCL.Network;
using PCL.Network.Loaders;

namespace PCL;

public static partial class DownloadUi
{
    private static void McDownloadMenuSaveServer(object sender, RoutedEventArgs e)
    {
        var version = _ResolveVersionListItem(sender);
        try
        {
            var id = version.Title;
            string jsonUrl = ((dynamic)version.Tag)["url"].ToString();
            var versionFolder = SystemDialogs.SelectFolder();
            if (!versionFolder.Contains(@"\"))
                return;
            versionFolder = Path.Combine(versionFolder, id);

            if (Loading
                .loaderTaskbar
                .ToList()
                .Any(ongoingLoader => ongoingLoader.Identifier == DownloadIdentifier("Server", id)))
            {
                HintService.Show(Hints.Error(Lang.Text("Minecraft.Download.Error.ServerDownloading")));
                return;
            }

            var loaders = new List<Loading.LoaderBase>
            {
                new LoaderDownload(
                    Lang.Text("Minecraft.Download.Stage.DownloadInstanceJson"),
                    [
                        new DownloadFile(
                            GameDownload.DlSourceLauncherOrMetaGet(jsonUrl),
                            Path.Combine(versionFolder, $"{id}.json"),
                            new FileChecker(canUseExistsFile: false, isJson: true)
                        )
                    ]
                )
                {
                    ProgressWeight = 2d
                },
                new Loading.LoaderTask<string, List<DownloadFile>>(
                    Lang.Text("Minecraft.Download.Stage.BuildServer"),
                    task => task.output = _BuildServerDownloadFiles(task, id, versionFolder)
                )
                {
                    ProgressWeight = 0.5d,
                    show = false
                },
                new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadServerFile"), [])
                {
                    ProgressWeight = 5d
                }
            };

            var loader = new Loading.LoaderCombo<string>(
                Lang.Text("Minecraft.Download.Stage.MinecraftServerDownload", id),
                loaders)
            {
                OnStateChanged = LoaderStateChangedHintOnly,
                Identifier = DownloadIdentifier("Server", id)
            };
            loader.Start(id);
            Loading.LoaderTaskbarAdd(loader);
            Launcher.frmMain?.BtnExtraDownload.ShowRefresh();
            Launcher.frmMain?.BtnExtraDownload.Ribble();
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "开始 Minecraft 服务端下载失败",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Minecraft.Download.Error.OperationFailed")
            );
        }
    }

    private static List<DownloadFile> _BuildServerDownloadFiles(
        Loading.LoaderTask<string, List<DownloadFile>> task,
        string id,
        string versionFolder
    )
    {
        var mcInstance = new McInstance(versionFolder);
        var serverDownload = mcInstance.JsonObject["downloads"]?["server"];
        if (serverDownload is null || serverDownload["url"] is null)
        {
            File.Delete(Path.Combine(versionFolder, $"{id}.json"));
            if (new DirectoryInfo(versionFolder).GetFileSystemInfos().Length == 0)
                Directory.Delete(versionFolder);
            task.output = [];
            HintService.Show(Hints.Error(Lang.Text("Minecraft.Download.Error.NoOfficialServerDownload", id)));
            Thread.Sleep(2000);
            task.Abort();
            return task.output;
        }

        var jarUrl = serverDownload["url"]?.ToString() ?? "";
        var checker = new FileChecker(
            1024L,
            (long?)serverDownload["size"] ?? -1,
            serverDownload["sha1"]?.ToString() ?? ""
        );
        var result = new List<DownloadFile>
        {
            new(
                GameDownload.DlSourceLauncherOrMetaGet(jarUrl),
                Path.Combine(
                    versionFolder,
                    $"{id}-server.jar"),
                checker)
        };
        var bat = $"""
                   @echo off
                   title {Lang.Text("Minecraft.Download.ServerBatch.Title", id)}
                   echo {Lang.Text("Minecraft.Download.ServerBatch.InstructionJavaPath")}
                   echo {Lang.Text("Minecraft.Download.ServerBatch.InstructionPclSettings")}
                   echo ------------------------------
                   echo {Lang.Text("Minecraft.Download.ServerBatch.InstructionEula")}
                   echo ------------------------------
                   "java" -server -XX:+UseG1GC -Xmx4096M -Xms1024M -XX:+UseCompressedOops -jar {id}-server.jar nogui
                   echo ----------------------
                   echo {Lang.Text("Minecraft.Download.ServerBatch.ServerStopped")}
                   pause
                   """;
        Common.WriteFile(
            Path.Combine(versionFolder, "Launch Server.bat"),
            bat.Replace("\n", "\r\n"),
            encoding: Encoding.Default.Equals(Encoding.UTF8)
                ? Encoding.UTF8
                : Encoding.GetEncoding("GB18030")
        );
        File.Delete(Path.Combine(versionFolder, $"{id}.json"));
        return result;
    }
}