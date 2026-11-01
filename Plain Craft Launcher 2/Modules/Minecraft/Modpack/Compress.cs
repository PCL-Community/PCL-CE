using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.Core.UI;
using PCL.Core.UI.Dialogs;
using static PCL.Loading;

namespace PCL;

public static partial class Modpack
{
    private static LoaderCombo<string> InstallPackCompress(string fileAddress, ZipArchive archive)
    {
        var (archiveBaseFolder, instanceName) = _DetectCompressArchiveRoot(archive);
        Common.Log(
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
            OnStateChanged = DownloadUi.McInstallState
        };
        loader.Start(targetFolder);
        LoaderTaskbarAdd(loader);
        Launcher.frmMain!.BtnExtraDownload.ShowRefresh();
        Launcher.frmMain.BtnExtraDownload.Ribble();
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
        _ = DialogService.Instance.ShowAsync(new TextDialogRequest
        {
            Title = Lang.Text("Common.Action.Install"),
            Content = Lang.Text("Minecraft.Download.Modpack.SelectEmptyFolder.Message"),
            Buttons = DialogButtons.From(
                DialogButton.Create(Lang.Text("Common.Action.Continue"))),
            ForceWait = true
        });
        var targetFolder = SystemDialogs.SelectFolder(
            Lang.Text("Minecraft.Download.Modpack.SelectTargetFolder.Title"));
        if (string.IsNullOrEmpty(targetFolder))
            throw new Common.CancelledException();
        if (targetFolder.Contains('!') || targetFolder.Contains(';'))
        {
            HintService.Show(Hints.Error(Lang.Text("Minecraft.Download.Modpack.InvalidGamePathChars", targetFolder)));
            throw new Common.CancelledException();
        }

        if (Directory.GetFileSystemEntries(targetFolder).Length > 0)
        {
            HintService.Show(Hints.Error(Lang.Text("Minecraft.Download.Modpack.TargetFolderMustBeEmpty")));
            throw new Common.CancelledException();
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
        PageSelectLeft.AddFolder(
            Path.Combine(targetFolder, archiveBaseFolder),
            Common.GetFolderNameFromPath(targetFolder),
            false);
        Thread.Sleep(400);
        Common.RunInUi(() => Launcher.frmMain!.PageChange(FormMain.PageType.InstanceSelect));
    }

    [GeneratedRegex(
        @"^.*\/(?=versions\/(?<ver>[^\/]+)\/(\k<ver>)\.json$)",
        RegexOptions.IgnoreCase)]
    private static partial Regex CompressArchiveRegex();
}