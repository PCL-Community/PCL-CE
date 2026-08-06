using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using PCL.Core.App.Localization;
using PCL.Core.UI;
using PCL.Core.Utils;
using static PCL.ModLoader;

namespace PCL;

public static partial class ModModpack
{
    #region 带启动器的压缩包

    private static LoaderCombo<string> _InstallLauncherPack(string fileAddress, ZipArchive archive,
        string archiveBaseFolder)
    {
        // 获取解压路径
        ModMain.MyMsgBox(Lang.Text("Minecraft.Download.Modpack.SelectEmptyFolder.Message"),
            Lang.Text("Common.Action.Install"), Lang.Text("Common.Action.Continue"), forceWait: true);
        var targetFolder = SystemDialogs.SelectFolder(Lang.Text("Minecraft.Download.Modpack.SelectTargetFolder.Title"));
        if (string.IsNullOrEmpty(targetFolder))
            throw new ModBase.CancelledException();
        if (Directory.GetFileSystemEntries(targetFolder).Length > 0)
        {
            HintService.Hint(Lang.Text("Minecraft.Download.Modpack.TargetFolderMustBeEmpty"), HintType.Error);
            throw new ModBase.CancelledException();
        }

        // 解压
        var loader = new LoaderCombo<string>(Lang.Text("Minecraft.Download.Modpack.Stage.ExtractArchive"), new[]
        {
            new LoaderTask<string, int>(Lang.Text("Minecraft.Download.Modpack.Stage.ExtractArchive"), task =>
            {
                _ExtractModpackFiles(targetFolder, fileAddress, task, 0.9d);
                Thread.Sleep(400); // 避免文件争用
                // 查找解压后的 exe 文件
                string launcher = null;
                foreach (var exeFile in Directory.GetFiles(targetFolder, "*.exe", SearchOption.TopDirectoryOnly))
                {
                    var info = FileVersionInfo.GetVersionInfo(exeFile);
                    ModBase.Log($"[Modpack] 文件 {exeFile} 的产品名标识为 {info.ProductName}");
                    if (info.ProductName == "Plain Craft Launcher")
                    {
                        launcher = exeFile;
                        ModBase.Log($"[Modpack] 发现整合包附带的 PCL 启动器：{exeFile}");
                    }
                    else if ((info.ProductName.ContainsF("Launcher", true) || info.ProductName.ContainsF("启动", true)) &&
                             !(info.ProductName == "Plain Craft Launcher Admin Manager"))
                    {
                        if (launcher is null)
                        {
                            launcher = exeFile;
                            ModBase.Log($"[Modpack] 发现整合包附带的疑似第三方启动器：{exeFile}");
                        }
                    }
                }

                task.Progress = 0.95d;
                // 尝试使用附带的启动器打开
                if (launcher is not null)
                {
                    ModBase.Log("[Modpack] 找到压缩包中附带的启动器：" + launcher);
                    if (ModMain.MyMsgBox(Lang.Text("Minecraft.Download.Modpack.BundledLauncher.Message", launcher),
                            Lang.Text("Minecraft.Download.Modpack.BundledLauncher.Title"),
                            Lang.Text("Minecraft.Download.Modpack.BundledLauncher.UseBundled"),
                            Lang.Text("Minecraft.Download.Modpack.BundledLauncher.DoNotUse")
                        ) == 1)
                    {
                        ModBase.OpenExplorer(targetFolder);
                        ModBase.ShellOnly(launcher, "--wait"); // 要求等待已有的 PCL 退出
                        ModBase.Log("[Modpack] 为换用整合包中的启动器启动，强制结束程序");
                        ModMain.frmMain.EndProgram(false);
                        return;
                    }
                }
                else
                {
                    ModBase.Log("[Modpack] 未找到压缩包中附带的启动器");
                }

                ModBase.OpenExplorer(targetFolder);
                // 加入文件夹列表
                var instanceName = ModBase.GetFolderNameFromPath(targetFolder);
                Directory.CreateDirectory(Path.Combine(targetFolder, ".minecraft"));
                PageSelectLeft.AddFolder(
                    Path.Combine(targetFolder, ".minecraft", archiveBaseFolder.Replace("/", @"\").TrimStart('\\')), instanceName,
                    false); // 格式例如：包裹文件夹\.minecraft\（最短为空字符串）
                // 寻找并安装内层整合包（任意格式：modpack.* 压缩包文件或文件夹形式的整合包）
                var innerPackPath = _FindInnerModpack(targetFolder);
                if (innerPackPath is not null)
                {
                    if (Directory.Exists(innerPackPath))
                    {
                        // 文件夹形式的整合包：重新打包为临时 zip 后走统一安装流程
                        var tempZip = Path.Combine(ModMain.RequestTaskTempFolder(), "modpack.zip");
                        ModBase.Log("[Modpack] 内层整合包为文件夹，重新打包后安装：" + innerPackPath);
                        ZipFile.CreateFromDirectory(innerPackPath, tempZip);
                        ModpackInstall(tempZip);
                    }
                    else
                    {
                        ModBase.Log("[Modpack] 调用内层整合包文件继续安装：" + innerPackPath);
                        ModpackInstall(innerPackPath);
                    }
                }
                else
                {
                    ModBase.Log("[Modpack] 未在压缩包中找到内层整合包");
                }
            })
        });
        loader.Start(targetFolder);
        LoaderTaskbarAdd(loader);
        ModMain.frmMain.BtnExtraDownload.ShowRefresh();
        ModMain.frmMain.BtnExtraDownload.Ribble();
        return loader;
    }

    /// <summary>
    ///     在解压后的目录中寻找内层整合包：优先 <c>modpack.*</c> 压缩包文件，
    ///     其次寻找包含已知清单的文件夹，可识别 CurseForge / Modrinth / MultiMC / MCBBS / HMCL 等任意格式。
    /// </summary>
    /// <param name="rootFolder">解压后的外层目录。</param>
    /// <returns>内层整合包的文件路径或文件夹路径；未找到时返回 null。</returns>
    private static string? _FindInnerModpack(string rootFolder)
    {
        // 优先：modpack.zip / modpack.mrpack 等压缩包文件
        foreach (var file in Directory.GetFiles(rootFolder, "modpack.*", SearchOption.AllDirectories))
            if (file.EndsWithF(".zip", true) || file.EndsWithF(".mrpack", true))
                return file;

        // 其次：包含已知清单的文件夹（按目录深度由浅到深搜索，优先匹配最外层的整合包）
        foreach (var directory in Directory.GetDirectories(rootFolder, "*", SearchOption.AllDirectories)
                     .OrderBy(directory => directory.Length))
        {
            if (File.Exists(Path.Combine(directory, "modrinth.index.json")) ||
                File.Exists(Path.Combine(directory, "mmc-pack.json")) ||
                File.Exists(Path.Combine(directory, "mcbbs.packmeta")) ||
                File.Exists(Path.Combine(directory, "modpack.json")))
                return directory;
            if (File.Exists(Path.Combine(directory, "manifest.json")))
                try
                {
                    var json = ModBase.GetJson(ModBase.ReadFile(Path.Combine(directory, "manifest.json")));
                    if (json?["minecraft"] is not null)
                        return directory;
                }
                catch (Exception)
                {
                    // 忽略无法解析的 manifest.json
                }
        }

        return null;
    }

    #endregion

    #region 普通压缩包

    private static LoaderCombo<string> _InstallCompress(string fileAddress, ZipArchive archive)
    {
        // 尝试定位 .minecraft 文件夹：寻找形如 “/versions/XXX/XXX.json” 的路径
        Match match = null;
        foreach (var entry in archive.Entries)
        {
            var entryMatch = RegexPatterns.ModpackLazyInstance.Match("/" + entry.FullName);
            if (entryMatch.Success)
            {
                match = entryMatch;
                break;
            }
        }

        if (match is null)
            throw new Exception(Lang.Text("Minecraft.Download.Modpack.UnknownArchiveStructure")); // 没有匹配
        var archiveBaseFolder = match.Value.Replace("/", @"\").TrimStart('\\'); // 格式例如：包裹文件夹\.minecraft\（最短为空字符串）
        var instanceName = match.Groups[1].Value;
        ModBase.Log("[ModPack] 检测到压缩包的 .minecraft 根目录：" + archiveBaseFolder + "，命中的实例名：" + instanceName);
        // 获取解压路径
        ModMain.MyMsgBox(Lang.Text("Minecraft.Download.Modpack.SelectEmptyFolder.Message"),
            Lang.Text("Common.Action.Install"), Lang.Text("Common.Action.Continue"), forceWait: true);
        var targetFolder = SystemDialogs.SelectFolder(Lang.Text("Minecraft.Download.Modpack.SelectTargetFolder.Title"));
        if (string.IsNullOrEmpty(targetFolder))
            throw new ModBase.CancelledException();
        if (targetFolder.Contains("!") || targetFolder.Contains(";"))
        {
            HintService.Hint(Lang.Text("Minecraft.Download.Modpack.InvalidGamePathChars", targetFolder),
                HintType.Error);
            throw new ModBase.CancelledException();
        }

        if (Directory.GetFileSystemEntries(targetFolder).Length > 0)
        {
            HintService.Hint(Lang.Text("Minecraft.Download.Modpack.TargetFolderMustBeEmpty"), HintType.Error);
            throw new ModBase.CancelledException();
        }

        // 解压
        var loader = new LoaderCombo<string>(Lang.Text("Minecraft.Download.Modpack.Stage.ExtractArchive"), new[]
        {
            new LoaderTask<string, int>(Lang.Text("Minecraft.Download.Modpack.Stage.ExtractArchive"), task =>
            {
                _ExtractModpackFiles(targetFolder, fileAddress, task, 0.95d);
                // 加入文件夹列表
                PageSelectLeft.AddFolder(Path.Combine(targetFolder, archiveBaseFolder), ModBase.GetFolderNameFromPath(targetFolder),
                    false);
                Thread.Sleep(400); // 避免文件争用
                ModBase.RunInUi(() => ModMain.frmMain.PageChange(FormMain.PageType.InstanceSelect));
            })
        })
        {
            OnStateChanged = ModDownloadLib.McInstallState
        };
        loader.Start(targetFolder);
        LoaderTaskbarAdd(loader);
        ModMain.frmMain.BtnExtraDownload.ShowRefresh();
        ModMain.frmMain.BtnExtraDownload.Ribble();
        return loader;
    }

    #endregion
}
