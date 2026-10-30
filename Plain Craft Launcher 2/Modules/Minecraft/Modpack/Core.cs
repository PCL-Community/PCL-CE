using System.IO;
using System.IO.Compression;
using System.Text;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI;
using PCL.Core.Utils.Validate;
using static PCL.Loading;

namespace PCL;

public static partial class Modpack
{
    /// <summary>
    ///     弹窗要求选择一个整合包文件并进行安装。
    /// </summary>
    public static void ModpackInstall()
    {
        if (!RestrictedFeaturePolicy.Check("modpack-install"))
            return;
        var file = SystemDialogs.SelectFile(
            Lang.Text("Minecraft.Download.Modpack.FileDialog.Filter"),
            Lang.Text("Minecraft.Download.Modpack.FileDialog.Title"));
        if (string.IsNullOrEmpty(file))
            return;
        Common.RunInThread(() =>
        {
            try
            {
                ModpackInstall(file);
            }
            catch (Common.CancelledException)
            {
            }
            catch (Exception ex)
            {
                Common.Log(
                    ex,
                    "手动安装整合包失败",
                    Common.LogLevel.Msgbox,
                    userSummary: Lang.Text("Minecraft.Download.Modpack.Error.OperationFailed"));
            }
        });
    }

    /// <summary>
    ///     构建并启动安装给定的整合包文件的加载器，并返回该加载器。若失败则抛出异常。
    ///     必须在工作线程执行。
    /// </summary>
    /// <exception cref="Common.CancelledException" />
    public static LoaderCombo<string> ModpackInstall(
        string file,
        string? instanceName = null,
        string? logo = null,
        string? resourceId = null,
        bool isOnlineInstall = false)
    {
        RestrictedFeaturePolicy.ThrowIfRestricted("modpack-install");
        Common.Log($"[ModPack] 整合包安装请求：{file}");
        ZipArchive? archive = null;
        try
        {
            _ValidateModpackTargetPath(instanceName);
            archive = _ReadModpackArchive(file, out var packType, out var archiveBaseFolder);
            return _InstallDetectedModpack(
                packType,
                file,
                archive,
                archiveBaseFolder,
                instanceName,
                logo,
                resourceId,
                isOnlineInstall);
        }
        finally
        {
            archive?.Dispose();
        }
    }

    private static void _ValidateModpackTargetPath(string? instanceName)
    {
        var targetFolder = $@"{GameFolders.mcFolderSelected}versions\{instanceName}\";
        if (!targetFolder.Contains('!') && !targetFolder.Contains(';'))
            return;

        HintService.Hint(
            Lang.Text("Minecraft.Download.Modpack.InvalidGamePathChars", targetFolder),
            HintType.Error);
        throw new Common.CancelledException();
    }

    private static ZipArchive _ReadModpackArchive(
        string file,
        out int packType,
        out string archiveBaseFolder)
    {
        archiveBaseFolder = "";
        ZipArchive? archive = null;
        try
        {
            archive = new ZipArchive(
                new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read));
            if (archive.Entries.Any(e => e.IsEncrypted))
                throw new Exception(
                    Lang.Text("Minecraft.Download.Modpack.EncryptedArchiveUnsupported"));

            packType = _DetectModpackType(archive, out archiveBaseFolder);
            return archive;
        }
        catch (Exception ex)
        {
            archive?.Dispose();
            if (ex.Message.Contains("Error.WinIOError"))
                throw new Exception(Lang.Text("Minecraft.Download.Modpack.OpenFailed"), ex);
            if (file.EndsWithF(".rar", true))
                throw new Exception(Lang.Text("Minecraft.Download.Modpack.RarUnsupported"), ex);
            throw new Exception(Lang.Text("Minecraft.Download.Modpack.UnsupportedArchive"), ex);
        }
    }

    private static int _DetectModpackType(ZipArchive archive, out string archiveBaseFolder)
    {
        archiveBaseFolder = "";

        var rootEntry = archive.GetEntry("mcbbs.packmeta")
            ?? archive.GetEntry("mmc-pack.json")
            ?? archive.GetEntry("modrinth.index.json")
            ?? archive.GetEntry("manifest.json")
            ?? archive.GetEntry("modpack.json")
            ?? archive.GetEntry("modpack.zip")
            ?? archive.GetEntry("modpack.mrpack");

        if (rootEntry is not null)
            return DetectEntryType(rootEntry);

        foreach (var entry in archive.Entries)
        {
            var fullNames = entry.FullName.Split('/');
            archiveBaseFolder = $"{fullNames[0]}/";

            if (fullNames.Length != 2)
                continue;

            var type = DetectEntryType(entry);
            if (type == -1)
                continue;

            if (entry.Name == "manifest.json" && type == 3)
                archiveBaseFolder = "overrides/";

            return type;
        }

        return -1;

        static int DetectEntryType(ZipArchiveEntry entry)
        {
            return entry.Name switch
            {
                "mcbbs.packmeta" => 3,
                "mmc-pack.json" => 2,
                "modrinth.index.json" => 4,
                "manifest.json" => DetectManifestType(entry),
                "modpack.json" => 1,
                "modpack.zip" or "modpack.mrpack" => 9,
                _ => -1
            };
        }

        static int DetectManifestType(ZipArchiveEntry entry)
        {
            var json = (JsonObject)Common.GetJson(
                Common.ReadFile(entry.Open(), Encoding.UTF8));

            return json["addons"] is null ? 0 : 3;
        }
    }

    private static LoaderCombo<string> _InstallDetectedModpack(
        int packType,
        string file,
        ZipArchive archive,
        string archiveBaseFolder,
        string? instanceName,
        string? logo,
        string? resourceId,
        bool isOnlineInstall)
    {
        switch (packType)
        {
            case 0:
                Common.Log("[ModPack] 整合包种类：CurseForge");
                return InstallPackCurseForge(
                    file,
                    archive,
                    archiveBaseFolder,
                    instanceName,
                    logo,
                    resourceId,
                    isOnlineInstall);

            case 1:
                Common.Log("[ModPack] 整合包种类：HMCL");
                return InstallPackHmcl(file, archive, archiveBaseFolder);

            case 2:
                Common.Log("[ModPack] 整合包种类：MMC");
                return InstallPackMmc(file, archive, archiveBaseFolder);

            case 3:
                Common.Log("[ModPack] 整合包种类：MCBBS");
                return InstallPackMcbbs(file, archive, archiveBaseFolder, instanceName)!;

            case 4:
                Common.Log("[ModPack] 整合包种类：Modrinth");
                return InstallPackModrinth(
                    file,
                    archive,
                    archiveBaseFolder,
                    instanceName,
                    logo,
                    resourceId,
                    isOnlineInstall);

            case 9:
                Common.Log("[ModPack] 整合包种类：带启动器的压缩包");
                return InstallPackLauncherPack(file, archive, archiveBaseFolder);

            default:
                Common.Log("[ModPack] 整合包种类：未能识别，假定为压缩包");
                return InstallPackCompress(file, archive);
        }
    }

    private static string _ResolveModpackInstanceName(string? instanceName, string defaultName)
    {
        if (instanceName is not null)
            return instanceName;

        instanceName = defaultName;
        var validate = new FolderNameValidator(
            Path.Combine(GameFolders.mcFolderSelected!, "versions"));
        if (!validate.Validate(instanceName).IsValid)
            instanceName = "";
        if (string.IsNullOrEmpty(instanceName))
            instanceName = Launcher.MyMsgBoxInput(
                Lang.Text("Minecraft.Download.Modpack.InputInstanceName"),
                "",
                "",
                [validate]);
        return string.IsNullOrEmpty(instanceName)
            ? throw new Common.CancelledException()
            : instanceName;
    }

    private static List<LoaderBase> _CreateModpackInstallLoaderList(
        List<LoaderBase> installLoaders,
        List<LoaderBase> mergeLoaders)
    {
        return
        [
            new LoaderCombo<string>(
                Lang.Text("Minecraft.Download.Modpack.Stage.ModpackInstall"),
                installLoaders)
            {
                show = false,
                block = false,
                ProgressWeight = installLoaders.Sum(l => l.ProgressWeight)
            },
            new LoaderCombo<string>(
                Lang.Text("Minecraft.Download.Modpack.Stage.GameInstall"),
                mergeLoaders)
            {
                show = false,
                ProgressWeight = mergeLoaders.Sum(l => l.ProgressWeight)
            }
        ];
    }

    private static void _FinalizeModpackInstance(
        string fileAddress,
        string instanceName,
        string? logo,
        string? resourceId,
        string source,
        string? modpackVersion)
    {
        var versionFolder = $@"{GameFolders.mcFolderSelected}versions\{instanceName}\";
        if (logo is not null && File.Exists(logo))
        {
            File.Copy(
                logo,
                Path.Combine(versionFolder, "PCL", "Logo.png"),
                true);
            States.Instance.LogoPath[versionFolder] = @"PCL\Logo.png";
            States.Instance.IsLogoCustom[versionFolder] = true;
            Common.Log($"[ModPack] 已设置整合包 Logo：{logo}");
        }

        foreach (var target in new[]
                 {
                     Path.Combine(versionFolder, "原始整合包.zip"),
                     Path.Combine(versionFolder, "原始整合包.mrpack")
                 })
            if (File.Exists(target))
            {
                Common.Log($"[ModPack] 删除原始整合包文件：{target}");
                File.Delete(target);
            }

        if (File.Exists(fileAddress) && Path.GetFileNameWithoutExtension(fileAddress) == "modpack")
        {
            Common.Log($"[ModPack] 删除安装整合包文件：{fileAddress}");
            File.Delete(fileAddress);
        }

        if (modpackVersion is not null)
            States.Instance.ModpackVersion[versionFolder] = modpackVersion;
        States.Instance.ModpackSource[versionFolder] = source;
        States.Instance.ModpackId[versionFolder] = resourceId ?? "";
        try
        {
            var projects = Comp.CompRequest.GetCompProjectsByIds([resourceId ?? ""]);
            if (projects.Count == 0)
                return;
            States.Instance.CustomInfo[versionFolder] = projects.First().Description;
        }
        catch (Exception ex)
        {
            Common.Log(ex, "[ModPack] 获取整合包描述文本失败");
        }
    }

    private static void ExtractModpackFiles(
        string installTemp,
        string fileAddress,
        LoaderBase loader,
        double progressIncrement)
    {
        var retryCount = 1;
        var encode = Encoding.GetEncoding("GB18030");
        var initialProgress = loader.Progress;

        while (retryCount <= 5)
            try
            {
                loader.Progress = initialProgress;

                Common.DeleteDirectory(installTemp);

                Common.ExtractFile(
                    fileAddress,
                    installTemp,
                    encode,
                    delta => loader.Progress += delta * progressIncrement);

                loader.Progress = initialProgress + progressIncrement;
                return;
            }
            catch (Exception ex)
            {
                Common.Log(ex, $"第 {retryCount} 次解压尝试失败");

                if (ex is ArgumentException or IOException)
                {
                    encode = Encoding.UTF8;
                    Common.Log("[ModPack] 已切换压缩包解压编码为 UTF8");
                }

                if (loader.LoadingState != MyLoading.MyLoadingState.Run)
                    return;

                retryCount++;

                if (retryCount <= 5)
                    Thread.Sleep((retryCount - 1) * 2000);
                else
                    throw new Exception("解压整合包文件失败", ex);
            }
    }

    /// <summary>
    ///     从整合包的 override 目录复制文件，同时设置 PCL 的配置文件与版本隔离。
    ///     对路径末尾是否为 \ 没有要求。
    /// </summary>
    private static void CopyOverrideDirectory(
        string overridesFolder,
        string versionFolder,
        LoaderBase loader,
        double progressIncrement)
    {
        if (!overridesFolder.EndsWithF(@"\"))
            overridesFolder += @"\";
        if (!versionFolder.EndsWithF(@"\"))
            versionFolder += @"\";
        if (Directory.Exists(overridesFolder))
        {
            Common.Log($"[ModPack] 处理整合包覆写文件夹：{overridesFolder} → {versionFolder}");
            Common.CopyDirectory(
                overridesFolder,
                versionFolder,
                delta => loader.Progress += delta * progressIncrement);
        }
        else
        {
            Common.Log($"[ModPack] 整合包中没有覆写文件夹：{overridesFolder}");
            loader.Progress += progressIncrement;
        }

        var overridesIni = $@"{overridesFolder}PCL\Setup.ini";
        var versionIni = $@"{versionFolder}PCL\Setup.ini";
        if (File.Exists(overridesIni))
        {
            Common.WriteIni(overridesIni, "VersionArgumentIndie", 1.ToString());
            Common.WriteIni(overridesIni, "VersionArgumentIndieV2", true.ToString());
            Common.CopyFile(overridesIni, versionIni);
        }
        else
        {
            Common.WriteIni(versionIni, "VersionArgumentIndie", 1.ToString());
            Common.WriteIni(versionIni, "VersionArgumentIndieV2", true.ToString());
        }

        Common.IniClearCache(versionIni);
    }
}