using System.IO;
using System.IO.Compression;
using System.Text;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft.Modpack;
using PCL.Core.UI;
using PCL.Core.Utils.Validate;
using PCL.Network;
using PCL.Network.Loaders;
using static PCL.ModLoader;

namespace PCL;

public static partial class ModModpack
{
    // 触发整合包安装的外部接口
    /// <summary>
    ///     弹窗要求选择一个整合包文件并进行安装。
    /// </summary>
    public static void ModpackInstall()
    {
        var file = SystemDialogs.SelectFile(Lang.Text("Minecraft.Download.Modpack.FileDialog.Filter"),
            Lang.Text("Minecraft.Download.Modpack.FileDialog.Title")); // 选择整合包文件
        if (string.IsNullOrEmpty(file))
            return;
        ModBase.RunInThread(() =>
        {
            try
            {
                ModpackInstall(file);
            }
            catch (ModBase.CancelledException ex)
            {
            }
            catch (Exception ex)
            {
                ModBase.Log(
                    ex,
                    "手动安装整合包失败",
                    ModBase.LogLevel.Msgbox,
                    userSummary: Lang.Text("Minecraft.Download.Modpack.Error.OperationFailed"));
            }
        });
    }

    /// <summary>
    ///     构建并启动安装给定的整合包文件的加载器，并返回该加载器。若失败则抛出异常。
    ///     必须在工作线程执行。
    /// </summary>
    /// <exception cref="ModBase.CancelledException" />
    public static LoaderCombo<string> ModpackInstall(string file, string instanceName = null, string logo = null,
        string resourceId = null, bool isOnlineInstall = false)
    {
        ModBase.Log("[ModPack] 整合包安装请求：" + (file ?? "null"));
        ZipArchive archive = null;
        try
        {
            archive = new ZipArchive(new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read));
            if (archive.Entries.Any(e => e.IsEncrypted))
                throw new Exception(Lang.Text("Minecraft.Download.Modpack.EncryptedArchiveUnsupported"));
        }
        catch (Exception ex)
        {
            // 打开压缩包失败（非压缩包、文件损坏等）
            throw _WrapSourceOpenError(ex, file);
        }

        try
        {
            return _InstallSource(new ZipModpackArchiveReader(archive), file, instanceName, logo, resourceId,
                isOnlineInstall);
        }
        finally
        {
            if (archive is not null)
                archive.Dispose();
        }
    }

    /// <summary>
    ///     从统一的整合包来源（zip 文件或文件夹）安装整合包，并返回顶层加载器。
    ///     内部完成格式检测与分派。
    /// </summary>
    private static LoaderCombo<string> _InstallSource(IModpackArchiveReader source, string sourcePath,
        string instanceName = null, string logo = null, string resourceId = null, bool isOnlineInstall = false)
    {
        // 字符校验
        var targetFolder = $@"{ModFolder.mcFolderSelected}versions\{instanceName}\";
        if (targetFolder.Contains("!") || targetFolder.Contains(";"))
        {
            HintService.Hint(Lang.Text("Minecraft.Download.Modpack.InvalidGamePathChars", targetFolder),
                HintType.Error);
            throw new ModBase.CancelledException();
        }

        // 获取整合包种类与关键 Json
        ModpackFormat packType;
        string archiveBaseFolder;
        try
        {
            var detection = ModpackArchiveDetector.Detect(source);
            packType = detection.Format;
            archiveBaseFolder = detection.ArchiveBaseFolder;
            ModBase.Log($"[ModPack] 整合包格式识别结果：{packType}（基础目录：{archiveBaseFolder}）");
        }
        catch (Exception ex)
        {
            // 格式检测阶段失败
            throw _WrapSourceOpenError(ex, sourcePath);
        }

        // 执行对应的安装方法
        switch (packType)
        {
            case ModpackFormat.CurseForge:
                return _InstallCurseForge(sourcePath, source, archiveBaseFolder, instanceName, logo, resourceId,
                    isOnlineInstall);
            case ModpackFormat.Modrinth:
                return _InstallModrinth(sourcePath, source, archiveBaseFolder, instanceName, logo, resourceId,
                    isOnlineInstall);
            case ModpackFormat.MultiMc:
                return _InstallMultiMc(sourcePath, source, archiveBaseFolder);
            case ModpackFormat.Mcbbs:
                return _InstallMcbbs(sourcePath, source, archiveBaseFolder, instanceName);
            case ModpackFormat.Hmcl:
                return _InstallHmcl(sourcePath, source, archiveBaseFolder);
            case ModpackFormat.LauncherPack:
                return _InstallLauncherPack(sourcePath, source);
            case ModpackFormat.LazyPack:
                return _InstallCompress(sourcePath, source);
            default:
                ModBase.Log("[ModPack] 整合包种类：未能识别，假定为压缩包");
                return _InstallCompress(sourcePath, source);
        }
    }

    /// <summary>
    ///     将压缩包打开/格式检测阶段的异常包装为“无法处理该压缩包”类的用户可见错误。
    /// </summary>
    private static Exception _WrapSourceOpenError(Exception ex, string sourcePath)
    {
        if (ex.Message.Contains("Error.WinIOError"))
            return new Exception(Lang.Text("Minecraft.Download.Modpack.OpenFailed"), ex);
        if (sourcePath.EndsWithF(".rar", true))
            return new Exception(Lang.Text("Minecraft.Download.Modpack.RarUnsupported"), ex);
        return new Exception(Lang.Text("Minecraft.Download.Modpack.UnsupportedArchive"), ex);
    }

    #region 共享流程

    /// <summary>
    ///     解压整合包文件到临时目录，失败时切换编码并重试。
    /// </summary>
    private static void _ExtractModpackFiles(string installTemp, string sourcePath, LoaderBase loader,
        double progressIncrement)
    {
        // 文件夹来源：直接复制，无需编码切换重试
        if (Directory.Exists(sourcePath))
        {
            var folderInitialProgress = loader.Progress;
            loader.Progress = folderInitialProgress;
            ModBase.CopyDirectory(sourcePath, installTemp, delta => loader.Progress += delta * progressIncrement);
            loader.Progress = folderInitialProgress + progressIncrement;
            return;
        }

        // 解压文件
        var retryCount = 1;
        var encode = Encoding.GetEncoding("GB18030");
        var initialProgress = loader.Progress;

        while (retryCount <= 5)
            try
            {
                loader.Progress = initialProgress;

                // 删除旧目录
                ModBase.DeleteDirectory(installTemp);

                // 解压文件，ProgressIncrementHandler 通过 Lambda 更新进度
                ModBase.ExtractFile(sourcePath, installTemp, encode,
                    delta => loader.Progress += delta * progressIncrement);

                // 解压成功，更新进度并退出循环
                loader.Progress = initialProgress + progressIncrement;
                return;
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, $"第 {retryCount} 次解压尝试失败");

                if (ex is ArgumentException || ex is IOException)
                {
                    encode = Encoding.UTF8;
                    ModBase.Log("[ModPack] 已切换压缩包解压编码为 UTF8");
                }

                // 检查加载器状态，决定是否中止
                if (loader is not null && loader.LoadingState != MyLoading.MyLoadingState.Run)
                    return;

                // 增加重试次数
                retryCount++;

                if (retryCount <= 5)
                    // 等待一段时间再重试
                    Thread.Sleep((retryCount - 1) * 2000);
                else
                    throw new Exception("解压整合包文件失败", ex);
            }
    }

    /// <summary>
    ///     从整合包的 override 目录复制文件，同时设置 PCL 的配置文件与版本隔离。
    ///     对路径末尾是否为 \ 没有要求。
    /// </summary>
    private static void _CopyOverrideDirectory(string overridesFolder, string versionFolder, LoaderBase loader,
        double progressIncrement)
    {
        if (!overridesFolder.EndsWithF(@"\"))
            overridesFolder += @"\";
        if (!versionFolder.EndsWithF(@"\"))
            versionFolder += @"\";
        // 复制文件
        if (Directory.Exists(overridesFolder))
        {
            ModBase.Log($"[ModPack] 处理整合包覆写文件夹：{overridesFolder} → {versionFolder}");
            ModBase.CopyDirectory(overridesFolder, versionFolder,
                delta => loader.Progress += delta * progressIncrement);
        }
        else
        {
            ModBase.Log($"[ModPack] 整合包中没有覆写文件夹：{overridesFolder}");
            loader.Progress += progressIncrement;
        }

        // 设置 ini
        var overridesIni = $@"{overridesFolder}PCL\Setup.ini";
        var versionIni = $@"{versionFolder}PCL\Setup.ini";
        if (File.Exists(overridesIni))
        {
            ModBase.WriteIni(overridesIni, "VersionArgumentIndie", 1.ToString()); // 开启版本隔离
            ModBase.WriteIni(overridesIni, "VersionArgumentIndieV2", true.ToString());
            ModBase.CopyFile(overridesIni, versionIni); // 覆写已有的 ini
        }
        else
        {
            ModBase.WriteIni(versionIni, "VersionArgumentIndie", 1.ToString()); // 开启版本隔离
            ModBase.WriteIni(versionIni, "VersionArgumentIndieV2", true.ToString());
        }

        ModBase.IniClearCache(versionIni); // 重置缓存，避免被安装过程中写入的 ini 覆盖
    }

    /// <summary>
    ///     校验并获取实例名；给定的默认名无效时弹窗要求输入。
    /// </summary>
    /// <exception cref="ModBase.CancelledException">用户取消输入实例名。</exception>
    private static string _PromptInstanceName(string fallback)
    {
        var validate = new FolderNameValidator(Path.Combine(ModFolder.mcFolderSelected, "versions"));
        if (!validate.Validate(fallback).IsValid)
            fallback = "";
        if (string.IsNullOrEmpty(fallback))
            fallback = ModMain.MyMsgBoxInput(Lang.Text("Minecraft.Download.Modpack.InputInstanceName"), "", "",
                [validate]);
        if (string.IsNullOrEmpty(fallback))
            throw new ModBase.CancelledException();
        return fallback;
    }

    /// <summary>
    ///     拼接指定实例的版本文件夹路径（以 \ 结尾）。
    /// </summary>
    private static string _GetVersionFolder(string instanceName)
    {
        return $@"{ModFolder.mcFolderSelected}versions\{instanceName}\";
    }

    /// <summary>
    ///     组装顶层安装加载器：重复任务检查、启动、加入任务栏、刷新下载按钮并跳转任务管理器页。
    /// </summary>
    private static LoaderCombo<string> _StartInstall(string loaderName, List<LoaderBase> loaders, string input,
        bool isOnlineInstall)
    {
        // 重复任务检查
        if (loaderTaskbar.Any(l => (l.name ?? "") == (loaderName ?? "")))
        {
            HintService.Hint(Lang.Text("Minecraft.Download.Modpack.Installing"), HintType.Error);
            throw new ModBase.CancelledException();
        }

        // 启动
        var loader = new LoaderCombo<string>(loaderName, loaders) { OnStateChanged = ModDownloadLib.McInstallState };
        loader.Start(input);
        LoaderTaskbarAdd(loader);
        ModMain.frmMain.BtnExtraDownload.ShowRefresh();
        if (!isOnlineInstall)
            ModBase.RunInUi(() => ModMain.frmMain.PageChange(FormMain.PageType.TaskManager));
        return loader;
    }

    /// <summary>
    ///     整合包安装收尾：设置图标、删除原始整合包文件、写入整合包版本/来源/ID 信息并尝试获取整合包描述。
    /// </summary>
    private static void _FinalizeInstance(string versionFolder, string fileAddress, string logo, string modpackSource,
        string modpackVersion, string resourceId)
    {
        // 设置图标
        if (logo is not null)
            _SetInstanceIcon(versionFolder, logo);

        // 删除原始整合包文件
        foreach (var target in new[] { Path.Combine(versionFolder, "原始整合包.zip"), Path.Combine(versionFolder, "原始整合包.mrpack") })
            if (File.Exists(target))
            {
                ModBase.Log("[ModPack] 删除原始整合包文件：" + target);
                File.Delete(target);
            }

        if (File.Exists(fileAddress) && ModBase.GetFileNameWithoutExtentionFromPath(fileAddress) == "modpack")
        {
            ModBase.Log("[ModPack] 删除安装整合包文件：" + fileAddress);
            File.Delete(fileAddress);
        }

        // 整合包版本
        if (modpackVersion is not null) States.Instance.ModpackVersion[versionFolder] = modpackVersion;
        States.Instance.ModpackSource[versionFolder] = modpackSource;
        States.Instance.ModpackId[versionFolder] = resourceId;
        do
        {
            try
            {
                var projects = ModComp.CompRequest.GetCompProjectsByIds([resourceId]);
                if (projects.Count == 0)
                    break;
                States.Instance.CustomInfo[versionFolder] = projects.First().Description;
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "[ModPack] 获取整合包描述文本失败");
            }
        } while (false);
    }

    /// <summary>
    ///     将图标文件复制到实例的 <c>PCL\Logo.png</c> 并标记为自定义图标。
    /// </summary>
    private static void _SetInstanceIcon(string versionFolder, string iconSourcePath)
    {
        if (!File.Exists(iconSourcePath))
            return;
        var logoPath = Path.Combine(versionFolder, "PCL", "Logo.png");
        Directory.CreateDirectory(Path.GetDirectoryName(logoPath) ?? "");
        File.Copy(iconSourcePath, logoPath, true);
        States.Instance.LogoPath[versionFolder] = @"PCL\Logo.png";
        States.Instance.IsLogoCustom[versionFolder] = true;
        ModBase.Log("[ModPack] 已设置整合包 Logo：" + iconSourcePath);
    }

    #endregion
}
