using System.IO.Compression;
using System.Text.RegularExpressions;
using PCL.Core.App.Localization;
using PCL.Core.UI;
using static PCL.Loading;

namespace PCL;

public static partial class Modpack
{
    private static LoaderCombo<string> InstallPackCompress(string fileAddress, ZipArchive archive)
    {
        var (archiveBaseFolder, instanceName) = _DetectCompressArchiveRoot(archive);
        LauncherCommon.Log(
            $"[ModPack] 检测到压缩包的 .minecraft 根目录：{archiveBaseFolder}，命中的实例名：{instanceName}");
        var targetFolder = _SelectCompressTargetFolder();
        var loader = new LoaderCombo<string>(
            Lang.Text("Minecraft.Download.Modpack.Stage.ExtractArchive"),
            [
                new LoaderTask<string, int>(
                    Lang.Text("Minecraft.Download.Modpack.Stage.ExtractArchive"),
                    task => _ExtractCompressPack(targetFolder, fileAddress, archiveBaseFolder, task)
                )
            ])
        {
            OnStateChanged = ModpackUi.OnInstallStateChanged
        };
        loader.Start(targetFolder);
        LoaderTaskbarAdd(loader);
        CompUi.Current?.RefreshDownloadButton();

        return loader;
    }

    private static (string ArchiveBaseFolder, string InstanceName) _DetectCompressArchiveRoot(
        ZipArchive archive)
    {
        var match = archive
            .Entries.Select(entry => CompressArchiveRegex().Match($"/{entry.FullName}"))
            .FirstOrDefault(entryMatch => entryMatch.Success);
        return match is null
            ? throw new Exception(Lang.Text("Minecraft.Download.Modpack.UnknownArchiveStructure"))
            : (match.Value.Replace("/", @"\").TrimStart('\\'), match.Groups[1].Value);
    }

    private static string _SelectCompressTargetFolder()
    {
        LaunchUi.ShowMessageBox(
            Lang.Text("Minecraft.Download.Modpack.SelectEmptyFolder.Message"),
            Lang.Text("Common.Action.Install"),
            Lang.Text("Common.Action.Continue"),
            forceWait: true);
        var targetFolder = SystemDialogs.SelectFolder(
            Lang.Text("Minecraft.Download.Modpack.SelectTargetFolder.Title"));
        if (string.IsNullOrEmpty(targetFolder))
            throw new CancelledException();
        if (targetFolder.Contains('!') || targetFolder.Contains(';'))
        {
            LauncherUi.Interaction?.ShowHint(
                Lang.Text("Minecraft.Download.Modpack.InvalidGamePathChars", targetFolder),
                NotificationType.Error);
            throw new CancelledException();
        }

        if (Directory.GetFileSystemEntries(targetFolder).Length > 0)
        {
            LauncherUi.Interaction?.ShowHint(
                Lang.Text("Minecraft.Download.Modpack.TargetFolderMustBeEmpty"),
                NotificationType.Error);
            throw new CancelledException();
        }

        return targetFolder;
    }

    private static void _ExtractCompressPack(
        string targetFolder,
        string fileAddress,
        string archiveBaseFolder,
        LoaderTask<string, int> task)
    {
        ExtractModpackFiles(targetFolder, fileAddress, task, 0.95d);
        ModpackUi.AddFolder(
            Path.Combine(targetFolder, archiveBaseFolder),
            LauncherCommon.GetFolderNameFromPath(targetFolder),
            false);
        Thread.Sleep(400);
        CompNavigation.ShowInstanceSelect();
    }

    [GeneratedRegex(
        @"^.*\/(?=versions\/(?<ver>[^\/]+)\/(\k<ver>)\.json$)",
        RegexOptions.IgnoreCase)]
    private static partial Regex CompressArchiveRegex();
}