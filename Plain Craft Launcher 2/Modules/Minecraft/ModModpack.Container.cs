using System.Diagnostics;
using System.IO;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft.Modpack;
using PCL.Core.UI;
using PCL.Core.Utils;
using static PCL.ModLoader;

namespace PCL;

public static partial class ModModpack
{
    #region 带启动器的压缩包

    /// <summary>
    ///     懒人包 .minecraft 根目录下属于共享/系统目录、应由启动器在运行时自动补全的内容，
    ///     安装时不需要复制进实例文件夹。
    /// </summary>
    private static readonly HashSet<string> _LazyPackSharedFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "versions", "libraries", "assets", "crash-reports", "logs",
        "launcher_profiles.json", "usercache.json", "usernamecache.json"
    };

    private static LoaderCombo<string> _InstallLauncherPack(string sourcePath)
    {
        // 解压到临时目录（无需用户选择空文件夹），内层整合包安装完成后由系统自动清理
        var installTemp = ModMain.RequestTaskTempFolder();
        var loader = new LoaderCombo<string>(Lang.Text("Minecraft.Download.Modpack.Stage.ExtractArchive"), new[]
        {
            new LoaderTask<string, int>(Lang.Text("Minecraft.Download.Modpack.Stage.ExtractArchive"), task =>
            {
                _ExtractModpackFiles(installTemp, sourcePath, task, 0.9d);
                Thread.Sleep(400); // 避免文件争用
                // 查找解压后的 exe 文件
                string launcher = null;
                foreach (var exeFile in Directory.GetFiles(installTemp, "*.exe", SearchOption.TopDirectoryOnly))
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
                        // 换用附带启动器：保留临时目录供其使用
                        ModBase.OpenExplorer(installTemp);
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

                // 寻找并安装内层整合包（任意格式：modpack.* 压缩包文件或文件夹形式的整合包）
                var innerPackPath = _FindInnerModpack(installTemp);
                if (innerPackPath is not null)
                {
                    ModBase.Log("[Modpack] 调用内层整合包继续安装：" + innerPackPath);
                    if (Directory.Exists(innerPackPath))
                        _InstallSource(new FolderModpackArchiveReader(innerPackPath), innerPackPath);
                    else
                        ModpackInstall(innerPackPath);
                }
                else
                {
                    HintService.Hint(Lang.Text("Minecraft.Download.Modpack.UnknownArchiveStructure"), HintType.Error);
                    ModBase.Log("[Modpack] 未在压缩包中找到内层整合包");
                }
            })
        });
        loader.Start(installTemp);
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

    #region 懒人包（完整实例压缩包）

    private static LoaderCombo<string> _InstallCompress(string sourcePath, IModpackArchiveReader archive)
    {
        // 尝试定位 .minecraft 文件夹：寻找形如 “/versions/XXX/XXX.json” 的路径
        Match match = null;
        foreach (var entryName in archive.EntryNames)
        {
            var entryMatch = RegexPatterns.ModpackLazyInstance.Match("/" + entryName);
            if (entryMatch.Success)
            {
                match = entryMatch;
                break;
            }
        }

        if (match is null)
            throw new Exception(Lang.Text("Minecraft.Download.Modpack.UnknownArchiveStructure")); // 没有匹配
        var archiveBaseFolder = match.Value.Replace("/", @"\").TrimStart('\\'); // 格式例如：包裹文件夹\.minecraft\（最短为空字符串）
        var packVersionName = match.Groups[1].Value;
        ModBase.Log("[ModPack] 检测到懒人包的 .minecraft 根目录：" + archiveBaseFolder + "，命中的实例名：" + packVersionName);

        // 实例名：与现有实例不冲突时直接用懒人包自己的，冲突时让用户改名
        var instanceName = _PromptInstanceName(packVersionName);

        // 解压到临时目录，复制实例内容到版本文件夹后删除临时目录
        var installTemp = ModMain.RequestTaskTempFolder();
        var loader = new LoaderCombo<string>(Lang.Text("Minecraft.Download.Modpack.Stage.ExtractArchive"), new[]
        {
            new LoaderTask<string, int>(Lang.Text("Minecraft.Download.Modpack.Stage.ExtractArchive"), task =>
            {
                _ExtractModpackFiles(installTemp, sourcePath, task, 0.6d);
                task.Progress = 0.6d;
                _InstallLazyPackInstance(installTemp, archiveBaseFolder, packVersionName, instanceName);
                task.Progress = 0.95d;
                // 用完即删
                ModBase.DeleteDirectory(installTemp);
                ModBase.RunInUi(() => ModMain.frmMain.PageChange(FormMain.PageType.InstanceSelect));
            })
            {
                ProgressWeight = new FileInfo(sourcePath).Length / 1024d / 1024d / 6d
            } // 每 6M 需要 1s
        })
        {
            OnStateChanged = ModDownloadLib.McInstallState
        };
        loader.Start(_GetVersionFolder(instanceName));
        LoaderTaskbarAdd(loader);
        ModMain.frmMain.BtnExtraDownload.ShowRefresh();
        ModMain.frmMain.BtnExtraDownload.Ribble();
        return loader;
    }

    /// <summary>
    ///     将懒人包解压后的 .minecraft 实例内容安装到当前游戏文件夹的 <c>versions\{实例名}</c> 下。
    ///     仅复制实例自身内容（版本配置、mods/config/saves 等），共享目录（libraries/assets 等）由启动器在运行时自动补全。
    /// </summary>
    private static void _InstallLazyPackInstance(string installTemp, string archiveBaseFolder, string packVersionName,
        string instanceName)
    {
        var mcRoot = Path.Combine(installTemp, archiveBaseFolder.TrimEnd('\\'));
        var instanceFolder = _GetVersionFolder(instanceName);
        Directory.CreateDirectory(instanceFolder);

        // 复制版本文件夹全部内容（json/jar 改名并修正 id，其余文件如 mods/config 一并复制，兼容隔离与非隔离两种布局）
        var versionDir = Path.Combine(mcRoot, "versions", packVersionName);
        if (Directory.Exists(versionDir))
            foreach (var item in Directory.GetFileSystemEntries(versionDir))
            {
                var name = Path.GetFileName(item);
                if (name.Equals(packVersionName + ".json", StringComparison.OrdinalIgnoreCase))
                {
                    var versionJson = (JsonObject)ModBase.GetJson(ModBase.ReadFile(item));
                    if (!string.Equals(instanceName, packVersionName, StringComparison.OrdinalIgnoreCase))
                        versionJson["id"] = instanceName;
                    ModBase.WriteFile(Path.Combine(instanceFolder, instanceName + ".json"), versionJson.ToString());
                }
                else if (name.Equals(packVersionName + ".jar", StringComparison.OrdinalIgnoreCase))
                {
                    ModBase.CopyFile(item, Path.Combine(instanceFolder, instanceName + ".jar"));
                }
                else if (Directory.Exists(item))
                {
                    ModBase.CopyDirectory(item, Path.Combine(instanceFolder, name), null);
                }
                else
                {
                    ModBase.CopyFile(item, Path.Combine(instanceFolder, name));
                }
            }

        // 复制 .minecraft 根目录下的实例内容（mods/config/saves/options.txt 等），跳过共享目录
        foreach (var item in Directory.GetFileSystemEntries(mcRoot))
        {
            var name = Path.GetFileName(item);
            if (_LazyPackSharedFolders.Contains(name))
                continue;
            var dest = Path.Combine(instanceFolder, name);
            if (Directory.Exists(item))
                ModBase.CopyDirectory(item, dest, null);
            else
                ModBase.CopyFile(item, dest);
        }

        // 开启版本隔离
        var versionIni = instanceFolder + @"PCL\Setup.ini";
        ModBase.WriteIni(versionIni, "VersionArgumentIndie", 1.ToString());
        ModBase.WriteIni(versionIni, "VersionArgumentIndieV2", true.ToString());
        ModBase.IniClearCache(versionIni);
    }

    #endregion
}
