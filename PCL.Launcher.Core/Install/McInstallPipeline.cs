using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.Network;
using PCL.Network.Loaders;

namespace PCL;

/// <summary>
///     合并安装管线：下载原版客户端文件，按需追加加载器版本 Json 并合并为单个实例 Json，
///     最后补全合并后实例缺失的支持库与资源文件。
///     Forge / NeoForge / Cleanroom 使用官方安装器；Fabric / Legacy Fabric 使用 meta Json；
///     OptiFine / LiteLoader / LabyMod 及 Fabric API / OptiFabric 等附属组件使用各自的官方链路。
/// </summary>
public static class McInstallPipeline
{
    private const string IgnoreFlagFileName = ".pclignore";

    /// <summary>获取安装任务的稳定标识，供任务栏去重使用。</summary>
    public static string GetIdentifier(string instanceFolder)
    {
        return "McInstall:" + McInstallRecovery.NormalizeFolder(instanceFolder);
    }

    /// <summary>
    ///     校验安装组合。返回本地化错误信息；返回 <see langword="null" /> 表示组合可安装。
    ///     该方法不是“功能白名单”，只拒绝会互相破坏或物理上无法共存的组合。
    /// </summary>
    public static string? GetCombinationError(McInstallRequest request)
    {
        var minecraftName = request.minecraftName ?? "";
        var mainLoaders = new List<(string Name, bool Selected)>
        {
            ("Forge", request.forgeEntry is not null || request.forgeVersion is not null),
            ("NeoForge", request.neoForgeEntry is not null || request.neoForgeVersion is not null),
            ("Cleanroom",
                request.cleanroomEntry is not null || request.cleanroomVersion is not null),
            ("Fabric", request.fabricVersion is not null),
            ("Legacy Fabric", request.legacyFabricVersion is not null),
            ("LabyMod", request.labyModCommitRef is not null)
        };
        var selectedMain = mainLoaders.Where(loader => loader.Selected).ToList();
        if (selectedMain.Count > 1)
            return Lang.Text(
                "Download.Install.Compat.IncompatibleWithLoader",
                selectedMain[1].Name);

        if (selectedMain.Any(loader => loader.Name == "Cleanroom")
            && !string.Equals(minecraftName, "1.12.2", StringComparison.Ordinal))
            return Lang.Text("Download.Install.State.NoVersion");

        if (request.optiFabric is not null
            && (request.fabricVersion is null
                || (request.optiFineEntry is null && request.optiFineVersion is null)))
            return Lang.Text("Download.Install.Compat.RequiresOptiFineAndFabric");

        if (request.fabricApi is not null && request.fabricVersion is null)
            return Lang.Text("Download.Install.Compat.RequiresFabric");
        if (request.legacyFabricApi is not null && request.legacyFabricVersion is null)
            return Lang.Text("Download.Install.Compat.RequiresLegacyFabric");

        if (request.liteLoaderEntry is not null
            && (request.neoForgeEntry is not null
                || request.neoForgeVersion is not null
                || request.legacyFabricVersion is not null
                || request.labyModCommitRef is not null
                || request.cleanroomEntry is not null
                || request.cleanroomVersion is not null))
            return Lang.Text(
                "Download.Install.Compat.IncompatibleWithLoader",
                "LiteLoader");

        var hasOptiFine = request.optiFineEntry is not null || request.optiFineVersion is not null;
        if (hasOptiFine
            && (request.neoForgeEntry is not null
                || request.neoForgeVersion is not null
                || request.labyModCommitRef is not null
                || request.cleanroomEntry is not null
                || request.cleanroomVersion is not null))
            return Lang.Text(
                "Download.Install.Compat.IncompatibleWithLoader",
                "OptiFine");

        if (hasOptiFine
            && request.fabricVersion is not null
            && McVersionComparer.CompareVersionGe(minecraftName, "1.20.5"))
            return Lang.Text("Download.Install.Compat.IncompatibleWithOptiFine");

        return null;
    }

    public static bool IsSupported(McInstallRequest request)
    {
        return GetCombinationError(request) is null;
    }

    /// <summary>
    ///     构建合并安装的加载器列表。调用方负责创建外层 <see cref="Loading.LoaderCombo" />、绑定状态事件并启动。
    ///     安装开始前会登记本次安装会话，失败或取消后由 <see cref="McInstallRecovery" /> 回滚。
    /// </summary>
    /// <param name="request">安装请求。</param>
    /// <param name="ignoreDump">实例已存在时是否允许覆盖安装（覆盖前会备份实例核心文件）。</param>
    public static List<Loading.LoaderBase> CreateInstallLoader(
        McInstallRequest request,
        bool ignoreDump = false)
    {
        RestrictedFeatures.ThrowIfRestricted("minecraft-install");

        NormalizeRequest(request);
        var combinationError = GetCombinationError(request);
        if (combinationError is not null)
            throw new Exception(combinationError);

        var minecraftName = request.minecraftName
            ?? throw new Exception(
                Lang.Text("Minecraft.Download.Modpack.MissingGameVersion.Generic"));
        var instanceName = request.targetInstanceName;
        if (!LauncherCommon.IsValidInstanceName(instanceName))
            throw new Exception(Lang.Text("Download.Install.NameInvalid"));

        var instanceFolder = McInstallRecovery.NormalizeFolder(request.targetInstanceFolder);
        if (string.IsNullOrEmpty(instanceFolder))
            throw new Exception(Lang.Text("Download.Install.NameInvalid"));

        var instanceJsonPath = McInstallRecovery.GetJsonPath(instanceFolder, instanceName);
        McInstallRecovery.RegisterSession(instanceFolder, instanceName);
        if (File.Exists(instanceJsonPath) && !ignoreDump)
        {
            McInstallRecovery.UnregisterSession(instanceFolder);
            HintService.Show(
                Hints.Error(
                    Lang.Text("Minecraft.Download.Error.InstanceAlreadyExists", instanceName, "")));
            throw new CancelledException();
        }

        if (!File.Exists(instanceJsonPath))
            McInstallRecovery.TryClearIncompleteInstanceFolder(instanceFolder, instanceName);
        McInstallRecovery.PrepareBackup(instanceFolder, instanceName);

        var tempFolder = LauncherCommon.GetTempFolder("Install");
        var preserveCoreJar = request.mmcPackInfo?.isMinecraftOverrided == true
            && File.Exists(instanceJsonPath)
            && File.Exists(McInstallRecovery.GetJarPath(instanceFolder, instanceName));
        var customCoreJarBackup = Path.Combine(tempFolder, "mmc-core.jar");
        if (preserveCoreJar)
            try
            {
                LauncherCommon.CopyFile(
                    McInstallRecovery.GetJarPath(instanceFolder, instanceName),
                    customCoreJarBackup);
                LauncherCommon.Log("[Install] 已备份 MMC 修改后的原版核心 Jar");
            }
            catch (Exception ex)
            {
                preserveCoreJar = false;
                LauncherCommon.Log(ex, "备份 MMC 修改后的原版核心 Jar 失败");
            }

        var sources = new InstallJsonSourceCollection();
        var loaders = new List<Loading.LoaderBase>
        {
            CreateIgnoreFlagLoader(instanceFolder)
        };

        AddVanillaLoaders(loaders, request, instanceFolder, instanceName, minecraftName);
        AddForgelikeLoaders(
            loaders,
            request,
            instanceFolder,
            instanceName,
            minecraftName,
            tempFolder,
            sources);
        AddFabricLoaders(loaders, request, instanceName, minecraftName, tempFolder, sources);
        LoaderSpecialInstall.AddLiteLoaderLoaders(
            loaders,
            request,
            instanceFolder,
            instanceName,
            minecraftName,
            tempFolder,
            sources);
        LoaderSpecialInstall.AddLabyModLoaders(
            loaders,
            request,
            instanceFolder,
            instanceName,
            minecraftName,
            tempFolder,
            sources);
        LoaderSpecialInstall.AddOptiFineLoaders(
            loaders,
            request,
            instanceFolder,
            instanceName,
            minecraftName,
            tempFolder,
            sources);
        AddApiModLoaders(loaders, request, instanceFolder, minecraftName);

        loaders.Add(
            new Loading.LoaderTask<int, int>(
                Lang.Text("Minecraft.Download.Stage.InstallGame"),
                _ =>
                {
                    InstanceJsonMerge.MergeInstall(
                        instanceFolder,
                        instanceName,
                        sources.Snapshot(),
                        request.mmcPackInfo,
                        request.labyModChannel ?? "production",
                        minecraftName);
                    CreateInstanceFolders(instanceFolder, request);
                })
            {
                ProgressWeight = 2d,
                block = true
            });
        var fixLoaders = GameDownload.DlClientFix(
            new McInstance(instanceFolder),
            false,
            GameDownload.AssetsIndexExistsBehaviour.DontDownload);
        loaders.AddRange(fixLoaders);
        if (preserveCoreJar)
            loaders.Add(
                new Loading.LoaderTask<int, int>(
                    Lang.Text("Minecraft.Download.Stage.DownloadCoreJar"),
                    _ =>
                    {
                        foreach (var fixLoader in fixLoaders)
                            while (fixLoader.State is LoadState.Waiting or LoadState.Loading)
                                Thread.Sleep(50);
                        LauncherCommon.CopyFile(
                            customCoreJarBackup,
                            McInstallRecovery.GetJarPath(instanceFolder, instanceName));
                    })
                {
                    ProgressWeight = 0.1d,
                    show = false
                });
        loaders.Add(
            new Loading.LoaderTask<int, int>(
                Lang.Text("Minecraft.Download.Stage.DeleteIgnoreFlag"),
                _ => DeleteIgnoreFlag(instanceFolder))
            {
                ProgressWeight = 0.5d
            });

        LauncherCommon.Log(
            $"[Install] 已创建 {instanceName} 的安装管线，原版版本：{minecraftName}，加载器 Json {sources.Snapshot().Count} 个",
            LauncherLogLevel.Developer);
        return loaders;
    }

    private static void NormalizeRequest(McInstallRequest request)
    {
        if (request.optiFineEntry is { } existingOptiFine
            && string.IsNullOrEmpty(existingOptiFine.NameFile))
        {
            var minecraftName = request.minecraftName ?? existingOptiFine.Inherit ?? "";
            var inherit = string.IsNullOrEmpty(existingOptiFine.Inherit)
                ? minecraftName
                : existingOptiFine.Inherit;
            var version = (existingOptiFine.DisplayName ?? "")
                .Replace(minecraftName + " ", "")
                .Replace("HD U ", "")
                .Replace(" ", "_")
                .Trim();
            if (string.IsNullOrEmpty(version))
                version = existingOptiFine.NameVersion?.AfterLast("OptiFine_") ?? "HD_U_LATEST";
            if (!version.StartsWith("HD_U", StringComparison.OrdinalIgnoreCase))
                version = $"HD_U_{version}";
            existingOptiFine.NameFile =
                $"{(existingOptiFine.IsPreview ? "preview_" : "")}OptiFine_{inherit}_{version}.jar";
            existingOptiFine.NameVersion ??= $"{inherit}-OptiFine_{version}";
            if (string.IsNullOrEmpty(existingOptiFine.Inherit))
                request.optiFineEntry = new GameDownload.DlOptiFineListEntry
                {
                    DisplayName = existingOptiFine.DisplayName,
                    IsPreview = existingOptiFine.IsPreview,
                    NameFile = existingOptiFine.NameFile,
                    NameVersion = existingOptiFine.NameVersion,
                    ReleaseTime = existingOptiFine.ReleaseTime,
                    RequiredForgeVersion = existingOptiFine.RequiredForgeVersion,
                    Inherit = inherit
                };
        }

        if (request.optiFineEntry is null && request.optiFineVersion is not null)
        {
            var version = request.optiFineVersion;
            if (version.Contains("_HD_U_"))
                version = $"HD_U_{version.AfterLast("_HD_U_")}";
            var minecraftName = request.minecraftName ?? "";
            request.optiFineVersion = version;
            request.optiFineEntry = new GameDownload.DlOptiFineListEntry
            {
                DisplayName =
                    $"{minecraftName} {version.Replace("HD_U_", "").Replace("_", "").Replace("pre", " pre")}",
                Inherit = minecraftName,
                IsPreview = version.Contains("pre", StringComparison.OrdinalIgnoreCase),
                NameVersion = $"{minecraftName}-OptiFine_{version}",
                NameFile =
                    $"{(version.Contains("pre", StringComparison.OrdinalIgnoreCase) ? "preview_" : "")}OptiFine_{minecraftName}_{version}.jar"
            };
        }

        if (request.liteLoaderEntry is not null
            && string.IsNullOrEmpty(request.liteLoaderEntry.Inherit))
            request.liteLoaderEntry.Inherit = request.minecraftName;

        if (request.labyModCommitRef is not null
            && string.IsNullOrEmpty(request.labyModChannel))
            request.labyModChannel = "production";
    }

    private static void AddVanillaLoaders(
        List<Loading.LoaderBase> loaders,
        McInstallRequest request,
        string instanceFolder,
        string instanceName,
        string minecraftName)
    {
        var instanceJsonPath = McInstallRecovery.GetJsonPath(instanceFolder, instanceName);
        var instanceJarPath = McInstallRecovery.GetJarPath(instanceFolder, instanceName);
        var useExistingCore = request.mmcPackInfo?.isMinecraftOverrided == true
            && File.Exists(instanceJsonPath)
            && File.Exists(instanceJarPath);
        if (useExistingCore)
        {
            LauncherCommon.Log(
                $"[Install] 检测到 MMC 修改过原版核心，使用实例现有 Json/Jar 作为合并基准：{instanceFolder}");
            loaders.Add(
                new Loading.LoaderTask<int, int>(
                    Lang.Text("Minecraft.Download.Stage.VanillaJsonDownload"),
                    _ =>
                    {
                        if (!File.Exists(instanceJsonPath) || !File.Exists(instanceJarPath))
                            throw new Exception(
                                Lang.Text(
                                    "Minecraft.Download.Error.JsonInvalid",
                                    instanceName,
                                    instanceJsonPath,
                                    "MMC 修改后的实例核心文件缺失"));
                    })
                {
                    ProgressWeight = 4d,
                    show = false
                });
            return;
        }

        loaders.Add(
            new Loading.LoaderTask<int, List<DownloadFile>>(
                Lang.Text("Minecraft.Download.Stage.ObtainVanillaJsonUrl"),
                task =>
                {
                    var jsonUrl = request.minecraftJson;
                    if (string.IsNullOrEmpty(jsonUrl))
                        jsonUrl = GameDownload.DlClientListGet(minecraftName)?.ToString();
                    if (string.IsNullOrEmpty(jsonUrl))
                        throw new Exception(
                            Lang.Text("Minecraft.Download.Error.NoJsonDownloadAddress"));
                    task.output =
                    [
                        new DownloadFile(
                            GameDownload.DlSourceLauncherOrMetaGet(jsonUrl),
                            instanceJsonPath,
                            new FileChecker(canUseExistsFile: false, isJson: true))
                    ];
                })
            {
                ProgressWeight = 1d,
                show = false
            });
        loaders.Add(
            new LoaderDownload(
                Lang.Text("Minecraft.Download.Stage.VanillaJsonDownload"),
                [])
            {
                ProgressWeight = 3d
            });
        loaders.Add(
            new Loading.LoaderTask<int, List<DownloadFile>>(
                Lang.Text("Minecraft.Download.Stage.AnalyzeCoreJarUrl"),
                task => task.output =
                    GameDownload.DlClientJarGet(new McInstance(instanceFolder), false) is { } jar
                        ? [jar]
                        : [])
            {
                ProgressWeight = 0.5d,
                show = false
            });
        loaders.Add(
            new LoaderDownload(
                Lang.Text("Minecraft.Download.Stage.DownloadCoreJar"),
                [])
            {
                ProgressWeight = 5d
            });
    }

    private static void AddForgelikeLoaders(
        List<Loading.LoaderBase> loaders,
        McInstallRequest request,
        string instanceFolder,
        string instanceName,
        string minecraftName,
        string tempFolder,
        InstallJsonSourceCollection sources)
    {
        if (!request.HasForgelikeLoader)
            return;

        var holder = new ForgelikePlanHolder();
        loaders.Add(
            LoaderInstallerInstall.CreatePlanLoader(
                request,
                minecraftName,
                tempFolder,
                holder));
        loaders.Add(LoaderInstallerInstall.CreateInstallerDownloader(holder));
        loaders.Add(
            LoaderInstallerInstall.CreateDownloadLoader(
                request.cleanroomEntry is not null || request.cleanroomVersion is not null
                    ? "Cleanroom"
                    : request.neoForgeEntry is not null || request.neoForgeVersion is not null
                        ? "NeoForge"
                        : "Forge"));
        if (holder.Plan?.IsInstaller != false)
        {
            loaders.Add(
                LoaderInstallerInstall.CreateLibraryAnalyzeLoader(
                    holder,
                    instanceFolder,
                    instanceName,
                    request.minecraftJson));
            loaders.Add(
                new LoaderDownload(
                    Lang.Text(
                        "Minecraft.Download.Stage.DownloadLoaderLibraries",
                        holder.Plan?.LoaderName ?? "Forgelike"),
                    [])
                {
                    ProgressWeight = 8d
                });
        }

        var runLoader = LoaderInstallerInstall.CreateInstallerRunLoader(
            holder,
            instanceFolder,
            instanceName);
        loaders.Add(runLoader);
        loaders.Add(
            new Loading.LoaderTask<int, int>(
                Lang.Text("Minecraft.Download.Stage.InstallGame"),
                _ =>
                {
                    var loaderJsonPath = runLoader.output;
                    if (string.IsNullOrEmpty(loaderJsonPath) || !File.Exists(loaderJsonPath))
                        throw new Exception(
                            Lang.Text(
                                "Minecraft.Download.Error.LoaderInstallFailed",
                                holder.Plan?.LoaderName ?? "Forgelike"));
                    sources.Add(GetForgelikeJsonKind(request), loaderJsonPath);
                })
            {
                ProgressWeight = 2d
            });
        LauncherCommon.Log(
            $"[Install] 已为 {instanceName} 追加 {holder.Plan?.LoaderName ?? "Forgelike"} 安装步骤");
    }

    private static InstallJsonKind GetForgelikeJsonKind(McInstallRequest request)
    {
        if (request.cleanroomEntry is not null || request.cleanroomVersion is not null)
            return InstallJsonKind.Cleanroom;
        if (request.neoForgeEntry is not null || request.neoForgeVersion is not null)
            return InstallJsonKind.NeoForge;
        return InstallJsonKind.Forge;
    }

    private static void AddFabricLoaders(
        List<Loading.LoaderBase> loaders,
        McInstallRequest request,
        string instanceName,
        string minecraftName,
        string tempFolder,
        InstallJsonSourceCollection sources)
    {
        var isLegacy = request.legacyFabricVersion is not null;
        if (request.fabricVersion is null && !isLegacy)
            return;

        var profileLoader = LoaderProfileInstall.CreateProfileLoader(
            request,
            tempFolder,
            isLegacy);
        loaders.Add(profileLoader);
        loaders.Add(
            new Loading.LoaderTask<int, int>(
                Lang.Text("Minecraft.Download.Stage.InstallGame"),
                _ =>
                {
                    var loaderJsonPath = profileLoader.output;
                    if (string.IsNullOrEmpty(loaderJsonPath) || !File.Exists(loaderJsonPath))
                        throw new Exception(
                            Lang.Text(
                                "Minecraft.Download.Error.FabricMetaDownloadFailed",
                                loaderJsonPath ?? ""));
                    sources.Add(
                        isLegacy ? InstallJsonKind.LegacyFabric : InstallJsonKind.Fabric,
                        loaderJsonPath);
                })
            {
                ProgressWeight = 1d
            });
        LauncherCommon.Log(
            $"[Install] 已为 {instanceName} 追加 {(isLegacy ? "Legacy Fabric" : "Fabric")} 安装步骤，原版版本：{minecraftName}",
            LauncherLogLevel.Developer);
    }

    private static Loading.LoaderBase CreateIgnoreFlagLoader(string instanceFolder)
    {
        return new Loading.LoaderTask<int, int>(
            Lang.Text("Minecraft.Download.Stage.AddIgnoreFlag"),
            _ =>
            {
                Directory.CreateDirectory(instanceFolder);
                LauncherCommon.WriteFile(
                    Path.Combine(instanceFolder, IgnoreFlagFileName),
                    "用于临时地在 PCL 的实例列表中屏蔽此实例。");
            })
        {
            show = false,
            block = false
        };
    }

    private static void DeleteIgnoreFlag(string instanceFolder)
    {
        var path = Path.Combine(instanceFolder, IgnoreFlagFileName);
        if (File.Exists(path))
            File.Delete(path);
    }

    private static void CreateInstanceFolders(string instanceFolder, McInstallRequest request)
    {
        var instance = new McInstance(instanceFolder);
        var resourcepacksFolder = Path.Combine(instance.PathIndie, "resourcepacks");
        Directory.CreateDirectory(resourcepacksFolder);
        LauncherCommon.Log($"[Install] 已创建资源包文件夹：{resourcepacksFolder}");

        var modable = request.fabricVersion is not null
            || request.legacyFabricVersion is not null
            || request.HasForgelikeLoader
            || request.liteLoaderEntry is not null
            || request.fabricApi is not null
            || request.legacyFabricApi is not null
            || request.optiFabric is not null
            || request.OptiFineAsMod;
        if (!modable)
            return;
        var modsFolder = Path.Combine(instance.PathIndie, "mods");
        Directory.CreateDirectory(modsFolder);
        LauncherCommon.Log($"[Install] 已创建模组文件夹：{modsFolder}");
    }

    #region 模组型加载器附属文件（Fabric API / Legacy Fabric API / OptiFabric）

    private static void AddApiModLoaders(
        List<Loading.LoaderBase> loaders,
        McInstallRequest request,
        string instanceFolder,
        string minecraftName)
    {
        if (request.fabricApi is not null)
            AddApiModLoader(
                loaders,
                request.fabricApi,
                "Fabric API",
                Comp.CompLoaderType.Fabric,
                instanceFolder,
                minecraftName);
        if (request.legacyFabricApi is not null)
            AddApiModLoader(
                loaders,
                request.legacyFabricApi,
                "Legacy Fabric API",
                Comp.CompLoaderType.Fabric,
                instanceFolder,
                minecraftName);
        if (request.optiFabric is not null)
            AddApiModLoader(
                loaders,
                request.optiFabric,
                "OptiFabric",
                Comp.CompLoaderType.Fabric,
                instanceFolder,
                minecraftName);
    }

    private static void AddApiModLoader(
        List<Loading.LoaderBase> loaders,
        Comp.CompFile file,
        string displayName,
        Comp.CompLoaderType loaderType,
        string instanceFolder,
        string minecraftName)
    {
        loaders.Add(
            new Loading.LoaderTask<int, List<DownloadFile>>(
                Lang.Text("Minecraft.Download.Stage.DownloadLoaderLibraries", displayName),
                task =>
                {
                    var modsFolder = Path.Combine(new McInstance(instanceFolder).PathIndie, "mods")
                        + Path.DirectorySeparatorChar;
                    Directory.CreateDirectory(modsFolder);
                    var downloadFile = file.ToNetFile(
                        modsFolder,
                        Comp.DownloadReason.Dependency,
                        minecraftName,
                        loaderType);
                    if (!File.Exists(downloadFile.LocalPath))
                        McInstallRecovery.RegisterCreatedFile(downloadFile.LocalPath);
                    task.output = [downloadFile];
                })
            {
                ProgressWeight = 2d,
                show = false
            });
        loaders.Add(
            new LoaderDownload(
                Lang.Text("Minecraft.Download.Stage.DownloadLoaderLibraries", displayName),
                [])
            {
                ProgressWeight = 2d
            });
        LauncherCommon.Log($"[Install] 已追加 {displayName} 下载步骤：{file.FileName}");
    }

    #endregion
}