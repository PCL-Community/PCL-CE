using System.Diagnostics;
using System.IO;
using System.IO.Compression;
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

    private static LoaderCombo<string> _InstallLauncherPack(string sourcePath, IModpackArchiveReader archive)
    {
        // 阶段 0：只探测压缩包是否附带启动器（仅提取顶层 exe 读取产品名，不完整解压）
        string bundledLauncherEntry = null;
        var probeFolder = ModMain.RequestTaskTempFolder();
        try
        {
            foreach (var entryName in archive.EntryNames)
            {
                if (entryName.Contains("/") || !entryName.EndsWithF(".exe", true))
                    continue; // 只探测顶层的 *.exe
                try
                {
                    var probePath = Path.Combine(probeFolder, Path.GetFileName(entryName));
                    archive.ExtractEntryToFile(entryName, probePath);
                    var info = FileVersionInfo.GetVersionInfo(probePath);
                    var productName = info.ProductName;
                    ModBase.Log($"[Modpack] 探测到附带可执行文件 {entryName}，产品名：{productName}");
                    // PCL 或第三方启动器，排除 PCL 管理助手
                    if (productName == "Plain Craft Launcher Community Edition" ||
                        productName == "Plain Craft Launcher" ||
                        (productName is not null &&
                         (productName.ContainsF("Launcher", true) || productName.ContainsF("启动", true)) &&
                         productName != "Plain Craft Launcher Admin Manager")) //我不知道这个”PCL 管理助手“是什么，原来在重构前的ModModpack.cs第1150行
                    {
                        bundledLauncherEntry = entryName;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "探测压缩包内附带启动器失败：" + entryName);
                }
            }
        }
        finally
        {
            ModBase.DeleteDirectory(probeFolder);
        }

        // 询问用户是否换用附带启动器
        if (bundledLauncherEntry is not null)
        {
            var bundledPath = bundledLauncherEntry.Replace("/", @"\");
            ModBase.Log("[Modpack] 找到压缩包中附带的启动器：" + bundledPath);
            if (ModMain.MyMsgBox(Lang.Text("Minecraft.Download.Modpack.BundledLauncher.Message", bundledPath),
                    Lang.Text("Minecraft.Download.Modpack.BundledLauncher.Title"),
                    Lang.Text("Minecraft.Download.Modpack.BundledLauncher.UseBundled"),
                    Lang.Text("Minecraft.Download.Modpack.BundledLauncher.DoNotUse")) == 1)
            {
                // 附带启动器需要把文件安装到它自己的持久目录里，因此让用户选一个空文件夹并直接解压进去。
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

                // 直接解压到所选文件夹（不走临时目录），然后从该文件夹运行附带启动器
                var launcherLoader = new LoaderCombo<string>(Lang.Text("Minecraft.Download.Modpack.Stage.ExtractArchive"), new[]
                {
                    new LoaderTask<string, int>(Lang.Text("Minecraft.Download.Modpack.Stage.ExtractArchive"), task =>
                    {
                        _ExtractModpackFiles(targetFolder, sourcePath, task, 0.9d);
                        task.Progress = 0.95d;
                        ModBase.OpenExplorer(targetFolder);
                        ModBase.ShellOnly(Path.Combine(targetFolder, bundledPath), "--wait"); // 要求等待已有的 PCL 退出
                        ModBase.Log("[Modpack] 为换用整合包中的启动器启动，强制结束程序");
                        ModMain.frmMain.EndProgram(false);
                    })
                });
                launcherLoader.Start(targetFolder);
                LoaderTaskbarAdd(launcherLoader);
                ModMain.frmMain.BtnExtraDownload.ShowRefresh();
                ModMain.frmMain.BtnExtraDownload.Ribble();
                return launcherLoader;
            }

            ModBase.Log("[Modpack] 用户选择不使用附带启动器，继续安装到 PCL");
        }
        else
        {
            ModBase.Log("[Modpack] 未找到压缩包中附带的启动器");
        }

        // 阶段 1：安装到 PCL（解压到临时目录，找内层整合包安装，临时目录由系统自动清理）
        var installTemp = ModMain.RequestTaskTempFolder();
        var installLoader = new LoaderCombo<string>(Lang.Text("Minecraft.Download.Modpack.Stage.ExtractArchive"), new[]
        {
            new LoaderTask<string, int>(Lang.Text("Minecraft.Download.Modpack.Stage.ExtractArchive"), task =>
            {
                _ExtractModpackFiles(installTemp, sourcePath, task, 0.9d);
                Thread.Sleep(400); // 避免文件争用
                task.Progress = 0.95d;
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
        installLoader.Start(installTemp);
        LoaderTaskbarAdd(installLoader);
        ModMain.frmMain.BtnExtraDownload.ShowRefresh();
        ModMain.frmMain.BtnExtraDownload.Ribble();
        return installLoader;
    }

    /// <summary>
    ///     在解压后的目录中寻找内层整合包：优先 <c>modpack.*</c> 压缩包文件，
    ///     其次寻找包含已知清单的文件夹，可识别 CurseForge / Modrinth / MultiMC / MCBBS / HMCL 等任意格式。
    /// </summary>
    /// <param name="rootFolder">解压后的外层目录。</param>
    /// <returns>内层整合包的文件路径或文件夹路径；未找到时返回 null。</returns>
    private static string? _FindInnerModpack(string rootFolder)
    {
        // 优先：modpack.* 压缩包文件（常见命名，无需校验）
        foreach (var file in Directory.GetFiles(rootFolder, "modpack.*", SearchOption.AllDirectories))
            if (file.EndsWithF(".zip", true) || file.EndsWithF(".mrpack", true))
                return file;

        // 其次：任意名称的 .zip / .mrpack 文件（部分懒人包的内层整合包不叫 modpack.*），校验确实是整合包
        foreach (var file in Directory.EnumerateFiles(rootFolder, "*.zip", SearchOption.AllDirectories)
                     .Concat(Directory.EnumerateFiles(rootFolder, "*.mrpack", SearchOption.AllDirectories)))
            if (_IsModpackArchive(file))
                return file;

        // 再次：包含已知清单的文件夹（按目录深度由浅到深搜索，优先匹配最外层的整合包）
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

    /// <summary>
    ///     判断一个压缩包文件是否是整合包：使用格式识别器检查其根目录或一级目录是否存在已知整合包清单。
    ///     资源包等普通 zip 不包含这些清单，会被排除，避免误把非整合包文件当作内层整合包。
    /// </summary>
    /// <param name="filePath">压缩包文件路径。</param>
    private static bool _IsModpackArchive(string filePath)
    {
        try
        {
            using var archive = ZipFile.OpenRead(filePath);
            var format = ModpackArchiveDetector.Detect(new ZipModpackArchiveReader(archive)).Format;
            return format != ModpackFormat.Unknown;
        }
        catch (Exception)
        {
            return false; // 无法打开或识别失败的压缩包不算整合包
        }
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
                ProgressWeight = _GetModpackProgressWeight(sourcePath)
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
    ///     先复制根目录内容，再复制实例目录（版本文件夹）内容，两者同名文件统一走“校验+复制”，
    ///     内容不同时弹窗让用户决定（默认覆盖，即实例目录内容优先）。
    /// </summary>
    private static void _InstallLazyPackInstance(string installTemp, string archiveBaseFolder, string packVersionName,
        string instanceName)
    {
        var mcRoot = Path.Combine(installTemp, archiveBaseFolder.TrimEnd('\\'));
        var instanceFolder = _GetVersionFolder(instanceName);
        Directory.CreateDirectory(instanceFolder);
        // 本次安装的冲突处理状态（“全部覆盖 / 全部跳过”在两个复制阶段间保持）
        var conflictState = new CopyConflictState();

        // 先复制 .minecraft 根目录下的实例内容（mods/config/saves/options.txt 等），跳过共享目录
        foreach (var item in Directory.GetFileSystemEntries(mcRoot))
        {
            var name = Path.GetFileName(item);
            if (_LazyPackSharedFolders.Contains(name))
                continue;
            var dest = Path.Combine(instanceFolder, name);
            if (Directory.Exists(item))
                _CopyDirectoryWithConflict(item, dest, conflictState, overwriteAsDefault: true);
            else
                _CopyFileWithConflict(item, dest, name, conflictState, overwriteAsDefault: true);
        }

        // 再复制版本文件夹全部内容（json/jar 改名并修正 id、jar 字段，其余文件如 mods/config 一并复制，兼容隔离与非隔离两种布局）。
        // 与根目录同名且内容不同的文件弹窗让用户决定，默认覆盖（实例目录内容优先）。
        var versionDir = Path.Combine(mcRoot, "versions", packVersionName);
        if (Directory.Exists(versionDir))
            foreach (var item in Directory.GetFileSystemEntries(versionDir))
            {
                var name = Path.GetFileName(item);
                if (name.Equals(packVersionName + ".json", StringComparison.OrdinalIgnoreCase))
                {
                    var versionJson = (JsonObject)ModBase.GetJson(ModBase.ReadFile(item));
                    if (!string.Equals(instanceName, packVersionName, StringComparison.OrdinalIgnoreCase))
                    {
                        versionJson["id"] = instanceName;
                        // jar 字段指向原名时一并改写为实例名
                        if (string.Equals(versionJson["jar"]?.ToString(), packVersionName, StringComparison.OrdinalIgnoreCase))
                            versionJson["jar"] = instanceName;
                    }
                    ModBase.WriteFile(Path.Combine(instanceFolder, instanceName + ".json"), versionJson.ToString());
                }
                else if (name.Equals(packVersionName + ".jar", StringComparison.OrdinalIgnoreCase))
                {
                    ModBase.CopyFile(item, Path.Combine(instanceFolder, instanceName + ".jar"));
                }
                else if (Directory.Exists(item))
                {
                    _CopyDirectoryWithConflict(item, Path.Combine(instanceFolder, name), conflictState,
                        overwriteAsDefault: true);
                }
                else
                {
                    _CopyFileWithConflict(item, Path.Combine(instanceFolder, name), name, conflictState,
                        overwriteAsDefault: true);
                }
            }

        // 复制共享目录（libraries/assets）到游戏文件夹根目录，避免重复下载并保留整合包的特殊依赖版本；
        // 同名文件内容不同时由用户决定跳过还是覆盖（默认跳过）。
        _CopyDirectoryWithConflict(Path.Combine(mcRoot, "libraries"),
            Path.Combine(ModFolder.mcFolderSelected, "libraries"));
        _CopyDirectoryWithConflict(Path.Combine(mcRoot, "assets"),
            Path.Combine(ModFolder.mcFolderSelected, "assets"));

        // 开启版本隔离
        var versionIni = instanceFolder + @"PCL\Setup.ini";
        ModBase.WriteIni(versionIni, "VersionArgumentIndie", 1.ToString());
        ModBase.WriteIni(versionIni, "VersionArgumentIndieV2", true.ToString());
        ModBase.IniClearCache(versionIni);
    }

    /// <summary>
    ///     复制一个目录到目标目录（含子目录），同名文件先比较内容（相同则跳过）；
    ///     内容不同时弹窗让用户选择（跳过 / 覆盖 / 全部覆盖 / 全部跳过）。
    /// </summary>
    /// <param name="sourceFolder">源目录。</param>
    /// <param name="destFolder">目标目录。</param>
    /// <param name="state">本次复制操作的冲突处理状态；未提供时本次复制独立新建。</param>
    /// <param name="overwriteAsDefault">弹窗默认选项是否为“覆盖”；false 时默认“跳过”。</param>
    private static void _CopyDirectoryWithConflict(string sourceFolder, string destFolder, CopyConflictState? state = null,
        bool overwriteAsDefault = false)
    {
        state ??= new CopyConflictState();
        if (!Directory.Exists(sourceFolder))
            return;
        Directory.CreateDirectory(destFolder);
        foreach (var sourceFile in Directory.EnumerateFiles(sourceFolder, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceFolder, sourceFile);
            _CopyFileWithConflict(sourceFile, Path.Combine(destFolder, relativePath), relativePath, state,
                overwriteAsDefault);
        }
    }

    /// <summary>
    ///     复制单个文件到目标路径，同名文件先比较内容（相同则跳过）；
    ///     内容不同时弹窗让用户选择（跳过 / 覆盖 / 全部覆盖 / 全部跳过）。
    ///     弹窗默认选项由 <paramref name="overwriteAsDefault"/> 决定：MyMsgText 会高亮第一个按钮，因此默认按钮放在第一位。
    /// </summary>
    /// <param name="sourceFile">源文件。</param>
    /// <param name="destFile">目标文件。</param>
    /// <param name="displayPath">弹窗提示中显示的文件路径；为 null 时显示文件名。</param>
    /// <param name="state">本次复制操作的冲突处理状态。</param>
    /// <param name="overwriteAsDefault">弹窗默认选项是否为“覆盖”；false 时默认“跳过”。</param>
    private static void _CopyFileWithConflict(string sourceFile, string destFile, string displayPath,
        CopyConflictState state, bool overwriteAsDefault = false)
    {
        if (File.Exists(destFile))
        {
            // 同名文件：先比较内容，相同则无需处理
            if (_FilesEqual(sourceFile, destFile))
                return;
            if (state.SkipAll)
                return;
            if (!state.OverwriteAll)
            {
                var choice = ModMain.MyMsgBox(
                    Lang.Text("Minecraft.Download.Modpack.OverwriteConfirm.Message",
                        displayPath ?? Path.GetFileName(sourceFile)),
                    Lang.Text("Minecraft.Download.Modpack.OverwriteConfirm.Title"),
                    overwriteAsDefault
                        ? Lang.Text("Common.Action.Overwrite")
                        : Lang.Text("Minecraft.Download.Modpack.OptionalFile.Skip"),
                    overwriteAsDefault
                        ? Lang.Text("Minecraft.Download.Modpack.OptionalFile.Skip")
                        : Lang.Text("Common.Action.Overwrite"),
                    Lang.Text("Minecraft.Download.Modpack.OverwriteConfirm.OverwriteAll"),
                    highLight: true, forceWait: true,
                    button4: Lang.Text("Minecraft.Download.Modpack.OverwriteConfirm.SkipAll"));
                switch (choice)
                {
                    case 1: // 第一个按钮：覆盖（overwriteAsDefault）或跳过（非 overwriteAsDefault）
                        if (!overwriteAsDefault)
                            return;
                        break;
                    case 2: // 第二个按钮：跳过（overwriteAsDefault）或覆盖（非 overwriteAsDefault）
                        if (overwriteAsDefault)
                            return;
                        break;
                    case 3: // 全部覆盖
                        state.OverwriteAll = true;
                        break;
                    case 4: // 全部跳过
                        state.SkipAll = true;
                        return;
                }
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destFile) ?? "");
        File.Copy(sourceFile, destFile, true);
    }

    /// <summary>
    ///     一次复制操作中“全部覆盖 / 全部跳过”的持久状态。
    /// </summary>
    private sealed class CopyConflictState
    {
        public bool OverwriteAll;
        public bool SkipAll;
    }

    /// <summary>
    ///     比较两个文件内容是否相同（先比大小，再比 MD5）。
    /// </summary>
    private static bool _FilesEqual(string fileA, string fileB)
    {
        try
        {
            if (new FileInfo(fileA).Length != new FileInfo(fileB).Length)
                return false;
            return ModBase.GetFileMD5(fileA) == ModBase.GetFileMD5(fileB);
        }
        catch (Exception)
        {
            return false;
        }
    }

    #endregion
}
