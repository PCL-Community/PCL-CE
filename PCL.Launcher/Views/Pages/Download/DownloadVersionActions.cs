using System.Text;
using System.Text.Json.Nodes;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft;
using PCL.Core.UI;
using PCL.Core.UI.Hints;
using PCL.Core.Utils.Codecs;
using PCL.Network;
using PCL.Network.Loaders;

namespace PCL.Views.Pages.Download;

internal static class DownloadVersionActions
{
    private const string SaveIdentifierPrefix = "McDownloadSave:";
    private const string ServerIdentifierPrefix = "McDownloadServer:";

    /// <summary>
    ///     另存 Minecraft 版本。
    /// </summary>
    /// <param name="version">版本清单项。</param>
    /// <param name="title">列表中显示的标题；与原版 PCL 一致，用于实例文件夹名。</param>
    public static void SaveVersion(JsonObject version, string title)
    {
        if (!RestrictedFeatures.Check("minecraft-client-save"))
            return;
        try
        {
            var id = string.IsNullOrEmpty(title) ? version["id"]?.ToString() ?? "" : title;
            var jsonUrl = version["url"]?.ToString() ?? "";
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(jsonUrl))
                return;
            if (IsTaskRunning(SaveIdentifierPrefix + id))
            {
                HintService.Show(
                    Hints.Error(Lang.Text("Minecraft.Download.Error.InstanceDownloading")));
                return;
            }

            var selectedFolder = SystemDialogs.SelectFolder();
            if (string.IsNullOrEmpty(selectedFolder))
                return;
            var versionFolder = Path.Combine(selectedFolder, id);
            Directory.CreateDirectory(versionFolder);

            var loaders = new List<Loading.LoaderBase>
            {
                new LoaderDownload(
                    Lang.Text("Minecraft.Download.Stage.DownloadInstanceJson"),
                    [
                        new DownloadFile(
                            GameDownload.DlSourceLauncherOrMetaGet(jsonUrl),
                            Path.Combine(versionFolder, $"{id}.json"),
                            new FileChecker(canUseExistsFile: false, isJson: true))
                    ])
                {
                    ProgressWeight = 2d
                },
                new Loading.LoaderTask<string, List<DownloadFile>>(
                    Lang.Text("Minecraft.Download.Stage.AnalyzeCoreJarUrl"),
                    task =>
                    {
                        var instance = new McInstance(versionFolder);
                        if (!instance.IsLoaded)
                            instance.Load();
                        task.output = GameDownload.DlClientJarGet(instance, false) is { } jar
                            ? [jar]
                            : [];
                    })
                {
                    ProgressWeight = 0.5d,
                    show = false
                },
                new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadCoreJar"), [])
                {
                    ProgressWeight = 5d
                }
            };

            StartTask(
                Lang.Text("Minecraft.Download.Stage.MinecraftDownload", id),
                loaders,
                SaveIdentifierPrefix + id);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "开始 Minecraft 版本另存失败",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Minecraft.Download.Error.OperationFailed"));
        }
    }

    public static void SaveServer(JsonObject version, string title)
    {
        try
        {
            var id = string.IsNullOrEmpty(title) ? version["id"]?.ToString() ?? "" : title;
            var jsonUrl = version["url"]?.ToString() ?? "";
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(jsonUrl))
                return;
            if (IsTaskRunning(ServerIdentifierPrefix + id))
            {
                HintService.Show(
                    Hints.Error(Lang.Text("Minecraft.Download.Error.ServerDownloading")));
                return;
            }

            var selectedFolder = SystemDialogs.SelectFolder();
            if (string.IsNullOrEmpty(selectedFolder))
                return;
            var versionFolder = Path.Combine(selectedFolder, id);
            Directory.CreateDirectory(versionFolder);

            var loaders = new List<Loading.LoaderBase>
            {
                new LoaderDownload(
                    Lang.Text("Minecraft.Download.Stage.DownloadInstanceJson"),
                    [
                        new DownloadFile(
                            GameDownload.DlSourceLauncherOrMetaGet(jsonUrl),
                            Path.Combine(versionFolder, $"{id}.json"),
                            new FileChecker(canUseExistsFile: false, isJson: true))
                    ])
                {
                    ProgressWeight = 2d
                },
                new Loading.LoaderTask<string, List<DownloadFile>>(
                    Lang.Text("Minecraft.Download.Stage.BuildServer"),
                    task => task.output = BuildServerDownloadFiles(task, id, versionFolder))
                {
                    ProgressWeight = 0.5d,
                    show = false
                },
                new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadServerFile"), [])
                {
                    ProgressWeight = 5d
                }
            };

            StartTask(
                Lang.Text("Minecraft.Download.Stage.MinecraftServerDownload", id),
                loaders,
                ServerIdentifierPrefix + id);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "开始 Minecraft 服务端下载失败",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Minecraft.Download.Error.OperationFailed"));
        }
    }

    public static void ShowChangelog(JsonObject version)
    {
        try
        {
            var wikiName = McFormatter.GetWikiUrlSuffix(version["id"]?.ToString() ?? "");
            var wikiUrl = $"{McFormatter.GetWikiBaseUrl()}/{wikiName.TrimStart('/')}";
            Basics.OpenPath(wikiUrl);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "打开 Minecraft 更新日志失败");
        }
    }

    private static void StartTask(string name, List<Loading.LoaderBase> loaders, string identifier)
    {
        var loader = new Loading.LoaderCombo<string>(name, loaders)
        {
            Identifier = identifier
        };
        loader.OnStateChangedUi += (sender, newState, _) =>
        {
            switch (newState)
            {
                case LoadState.Finished:
                    HintService.Show(Hints.Success(Lang.Text("Common.Status.Success")));
                    break;

                case LoadState.Failed:
                    HintService.Show(
                        Hints.Error(
                            sender.Error?.Message
                            ?? Lang.Text("Minecraft.Download.Error.OperationFailed")));
                    break;

                case LoadState.Waiting:
                case LoadState.Loading:
                case LoadState.Aborted:
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(newState), newState, null);
            }
        };
        loader.Start();
        Loading.LoaderTaskbarAdd(loader);
    }

    private static bool IsTaskRunning(string identifier)
    {
        return Loading.loaderTaskbar.Any(loader => loader.Identifier == identifier);
    }

    private static List<DownloadFile> BuildServerDownloadFiles(
        Loading.LoaderTask<string, List<DownloadFile>> task,
        string id,
        string versionFolder)
    {
        var jsonPath = Path.Combine(versionFolder, $"{id}.json");
        var instance = new McInstance(versionFolder);
        if (!instance.IsLoaded)
            instance.Load();

        var serverDownload = instance.JsonObject["downloads"]?["server"];
        if (serverDownload?["url"] is null)
        {
            TryDeleteFile(jsonPath);
            TryDeleteEmptyFolder(versionFolder);
            HintService.Show(
                Hints.Error(
                    Lang.Text("Minecraft.Download.Error.NoOfficialServerDownload", id)));
            Thread.Sleep(2000);
            task.Abort();
            return [];
        }

        var jarUrl = serverDownload["url"]?.ToString() ?? "";
        var checker = new FileChecker(
            1024L,
            serverDownload["size"]?.GetValue<long>() ?? -1L,
            serverDownload["sha1"]?.ToString() ?? "");
        var files = new List<DownloadFile>
        {
            new(
                GameDownload.DlSourceLauncherOrMetaGet(jarUrl),
                Path.Combine(versionFolder, $"{id}-server.jar"),
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
        LauncherCommon.WriteFile(
            Path.Combine(versionFolder, "Launch Server.bat"),
            bat.ReplaceLineEndings("\r\n"),
            Encoding.Default.Equals(Encoding.UTF8) ? Encoding.UTF8 : Encodings.GB18030);
        TryDeleteFile(jsonPath);
        return files;
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "删除临时版本 JSON 失败");
        }
    }

    private static void TryDeleteEmptyFolder(string folder)
    {
        try
        {
            if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
                Directory.Delete(folder);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "删除空版本文件夹失败");
        }
    }
}