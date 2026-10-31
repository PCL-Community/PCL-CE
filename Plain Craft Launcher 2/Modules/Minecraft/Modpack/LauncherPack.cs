using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using PCL.Core.App.Localization;
using PCL.Core.UI;
using PCL.Core.UI.Dialogs;
using static PCL.Loading;

namespace PCL;

public static partial class Modpack
{
    private static LoaderCombo<string> InstallPackLauncherPack(
        string fileAddress,
        ZipArchive archive,
        string archiveBaseFolder)
    {
        var targetFolder = _SelectLauncherPackTargetFolder();
        var loader = new LoaderCombo<string>(
            Lang.Text("Minecraft.Download.Modpack.Stage.ExtractArchive"),
            [
                new LoaderTask<string, int>(
                    Lang.Text("Minecraft.Download.Modpack.Stage.ExtractArchive"),
                    task => _ExtractLauncherPack(targetFolder, fileAddress, archiveBaseFolder, task)
                )
            ]);
        loader.Start(targetFolder);
        LoaderTaskbarAdd(loader);
        Launcher.frmMain!.BtnExtraDownload.ShowRefresh();
        Launcher.frmMain.BtnExtraDownload.Ribble();
        return loader;
    }

    private static string _SelectLauncherPackTargetFolder()
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

        if (Directory.GetFileSystemEntries(targetFolder).Length <= 0)
            return targetFolder;

        HintService.Hint(
            Lang.Text("Minecraft.Download.Modpack.TargetFolderMustBeEmpty"),
            HintType.Error);
        throw new Common.CancelledException();
    }

    private static bool _ExtractLauncherPack(
        string targetFolder,
        string fileAddress,
        string archiveBaseFolder,
        LoaderTask<string, int> task)
    {
        ExtractModpackFiles(targetFolder, fileAddress, task, 0.9d);
        Thread.Sleep(400);
        var launcher = _FindBundledLauncher(targetFolder);

        task.Progress = 0.95d;
        if (launcher is not null)
        {
            Common.Log($"[Modpack] 找到压缩包中附带的启动器：{launcher}");
            if (DialogService.Instance.Show(new TextDialogRequest
                {
                    Title = Lang.Text("Minecraft.Download.Modpack.BundledLauncher.Title"),
                    Content = Lang.Text("Minecraft.Download.Modpack.BundledLauncher.Message", launcher),
                    Buttons = DialogButtons.From(
                        DialogButton.Create(Lang.Text("Minecraft.Download.Modpack.BundledLauncher.UseBundled")),
                        DialogButton.Create(Lang.Text("Minecraft.Download.Modpack.BundledLauncher.DoNotUse"))),
                    ForceWait = true
                }).Result
                ?.Button == 1)
            {
                Common.OpenExplorer(targetFolder);
                Common.ShellOnly(launcher, "--wait");
                Common.Log("[Modpack] 为换用整合包中的启动器启动，强制结束程序");
                Launcher.frmMain!.EndProgram(false);
                return true;
            }
        }
        else
        {
            Common.Log("[Modpack] 未找到压缩包中附带的启动器");
        }

        Common.OpenExplorer(targetFolder);
        _RegisterLauncherPackInstance(targetFolder, archiveBaseFolder);
        return false;
    }

    private static string? _FindBundledLauncher(string targetFolder)
    {
        string? launcher = null;
        foreach (var exeFile in Directory.GetFiles(
                     targetFolder,
                     "*.exe",
                     SearchOption.TopDirectoryOnly))
        {
            var info = FileVersionInfo.GetVersionInfo(exeFile);
            Common.Log($"[Modpack] 文件 {exeFile} 的产品名标识为 {info.ProductName}");
            if (info.ProductName == "Plain Craft Launcher")
            {
                launcher = exeFile;
                Common.Log($"[Modpack] 发现整合包附带的 PCL 启动器：{exeFile}");
            }
            else if (
                info.ProductName is not null
                && (
                    info.ProductName.ContainsF("Launcher", true)
                    || info.ProductName.ContainsF("启动", true))
                && info.ProductName != "Plain Craft Launcher Admin Manager")
            {
                if (launcher is not null)
                    continue;

                launcher = exeFile;
                Common.Log($"[Modpack] 发现整合包附带的疑似第三方启动器：{exeFile}");
            }
        }

        return launcher;
    }

    private static void _RegisterLauncherPackInstance(string targetFolder, string archiveBaseFolder)
    {
        var instanceName = Common.GetFolderNameFromPath(targetFolder);
        Directory.CreateDirectory(Path.Combine(targetFolder, ".minecraft"));
        PageSelectLeft.AddFolder(
            Path.Combine(
                targetFolder,
                ".minecraft",
                archiveBaseFolder.Replace("/", @"\").TrimStart('\\')
            ),
            instanceName,
            false);
        var modpackFile = Directory
            .GetFiles(targetFolder, "modpack.*", SearchOption.AllDirectories)
            .First();
        Common.Log($"[Modpack] 调用 modpack 文件继续安装：{modpackFile}");
        ModpackInstall(modpackFile);
    }
}