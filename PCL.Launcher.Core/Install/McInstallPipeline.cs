using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.Network;
using PCL.Network.Loaders;

namespace PCL;

/// <summary>
///     合并安装管线：下载原版客户端文件，按需追加加载器版本 Json 并合并为单个实例 Json，
///     最后补全合并后实例缺失的支持库与资源文件。
///     目前支持原版、Fabric 与 Legacy Fabric；其余加载器需要运行官方安装器，尚未接入。
/// </summary>
/// <remarks>
///     TODO(FIX-INSTALL-01): 接入 Forge/NeoForge/Cleanroom 的安装器运行链路（需要 Java 与 Forge 安装器包装）。
///     TODO(FIX-INSTALL-02): 接入 OptiFine/LiteLoader/LabyMod 的安装器运行链路。
///     TODO(FIX-INSTALL-03): 接入 Fabric API/Legacy Fabric API/OptiFabric 的模组文件下载与合并安装。
/// </remarks>
public static class McInstallPipeline
{
    private const string IgnoreFlagFileName = ".pclignore";

    public static string? GetUnsupportedLoaderName(McInstallRequest request)
    {
        if (request.optiFabric is not null)
            return Lang.Text("Common.Installation.OptiFabric");
        if (request.optiFineEntry is not null || request.optiFineVersion is not null)
            return Lang.Text("Common.Installation.OptiFine");
        if (request.liteLoaderEntry is not null)
            return Lang.Text("Common.Installation.LiteLoader");
        if (request.labyModCommitRef is not null || request.labyModChannel is not null)
            return Lang.Text("Common.Installation.LabyMod");
        if (request.fabricVersion is not null && request.legacyFabricVersion is not null)
            return Lang.Text("Common.Installation.LegacyFabric");
        return null;
    }

    public static bool IsSupported(McInstallRequest request)
    {
        return GetUnsupportedLoaderName(request) is null;
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

        var minecraftName = request.minecraftName
            ?? throw new Exception(
                Lang.Text("Minecraft.Download.Modpack.MissingGameVersion.Generic"));
        var instanceName = request.targetInstanceName;
        if (!LauncherCommon.IsValidInstanceName(instanceName))
            throw new Exception(Lang.Text("Download.Install.NameInvalid"));

        var instanceFolder = McInstallRecovery.NormalizeFolder(request.targetInstanceFolder);
        if (string.IsNullOrEmpty(instanceFolder))
            throw new Exception(Lang.Text("Download.Install.NameInvalid"));

        var unsupportedLoader = GetUnsupportedLoaderName(request);
        if (unsupportedLoader is not null)
            throw new Exception(
                Lang.Text("Download.Install.Loader.Unsupported", unsupportedLoader));

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
        var loaders = new List<Loading.LoaderBase>
        {
            CreateIgnoreFlagLoader(instanceFolder)
        };

        AddVanillaLoaders(loaders, request, instanceFolder, instanceName, minecraftName);
        var hasLoader = request.fabricVersion is not null
            || request.legacyFabricVersion is not null
            || IsForgelikeRequested(request);
        if (!hasLoader)
            loaders.Add(
                new Loading.LoaderTask<int, int>(
                    Lang.Text("Minecraft.Download.Stage.InstallGame"),
                    _ => InstanceJsonMerge.Normalize(instanceFolder, instanceName))
                {
                    ProgressWeight = 1d
                });
        AddLoaderSpecificLoaders(
            loaders,
            request,
            instanceFolder,
            instanceName,
            minecraftName,
            tempFolder);
        AddForgelikeLoaders(
            loaders,
            request,
            instanceFolder,
            instanceName,
            minecraftName,
            tempFolder);
        AddApiModLoaders(loaders, request, instanceFolder, minecraftName);

        loaders.Add(
            new Loading.LoaderTask<int, int>(
                Lang.Text("Minecraft.Download.Stage.DeleteIgnoreFlag"),
                _ => DeleteIgnoreFlag(instanceFolder))
            {
                ProgressWeight = 0.5d
            });
        return loaders;
    }

    private static void AddVanillaLoaders(
        List<Loading.LoaderBase> loaders,
        McInstallRequest request,
        string instanceFolder,
        string instanceName,
        string minecraftName)
    {
        var instanceJsonPath = McInstallRecovery.GetJsonPath(instanceFolder, instanceName);
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
        loaders.AddRange(
            GameDownload.DlClientFix(
                new McInstance(instanceFolder),
                false,
                GameDownload.AssetsIndexExistsBehaviour.DontDownload));
    }

    private static void AddLoaderSpecificLoaders(
        List<Loading.LoaderBase> loaders,
        McInstallRequest request,
        string instanceFolder,
        string instanceName,
        string minecraftName,
        string tempFolder)
    {
        var isLegacyFabric = request.legacyFabricVersion is not null;
        if (request.fabricVersion is null && !isLegacyFabric)
            return;

        var profileLoader =
            LoaderProfileInstall.CreateProfileLoader(request, tempFolder, isLegacyFabric);
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
                    InstanceJsonMerge.Merge(instanceFolder, instanceName, loaderJsonPath);
                })
            {
                ProgressWeight = 1d
            });

        loaders.AddRange(
            GameDownload.DlClientFix(
                new McInstance(instanceFolder),
                false,
                GameDownload.AssetsIndexExistsBehaviour.DontDownload));
        LauncherCommon.Log(
            $"[Install] 已为 {instanceName} 追加 {
                (isLegacyFabric ? "Legacy Fabric" : "Fabric")} 安装步骤，原版版本：{minecraftName}",
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

    #region 官方安装器型加载器（Forge / NeoForge / Cleanroom）

    private static bool IsForgelikeRequested(McInstallRequest request)
    {
        return request.forgeEntry is not null
            || request.forgeVersion is not null
            || request.neoForgeEntry is not null
            || request.neoForgeVersion is not null
            || request.cleanroomEntry is not null
            || request.cleanroomVersion is not null;
    }

    private static void AddForgelikeLoaders(
        List<Loading.LoaderBase> loaders,
        McInstallRequest request,
        string instanceFolder,
        string instanceName,
        string minecraftName,
        string tempFolder)
    {
        if (!TryCreateForgelikeInstaller(
                request,
                minecraftName,
                out var loaderName,
                out var loaderVersion,
                out var inheritVersion,
                out var installerUrls,
                out var installerHash))
            return;

        var runFolder = Path.Combine(tempFolder, "installer-run");
        Directory.CreateDirectory(runFolder);
        var installerPath = LoaderInstallerInstall.GetInstallerPath(tempFolder, loaderName);
        loaders.Add(
            LoaderInstallerInstall.CreateInstallerDownloader(
                loaderName,
                installerUrls,
                installerPath,
                installerHash));
        loaders.Add(
            LoaderInstallerInstall.CreateLibraryAnalyzeLoader(
                loaderName,
                loaderVersion,
                inheritVersion,
                installerPath,
                runFolder,
                instanceFolder,
                instanceName,
                request.minecraftJson));
        loaders.Add(
            new LoaderDownload(
                Lang.Text("Minecraft.Download.Stage.DownloadLoaderLibraries", loaderName),
                [])
            {
                ProgressWeight = 8d
            });
        var runLoader = LoaderInstallerInstall.CreateInstallerRunLoader(
            loaderName,
            installerPath,
            runFolder,
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
                            Lang.Text("Minecraft.Download.Error.LoaderInstallFailed", loaderName));
                    InstanceJsonMerge.Merge(instanceFolder, instanceName, loaderJsonPath);
                })
            {
                ProgressWeight = 2d
            });
        loaders.AddRange(
            GameDownload.DlClientFix(
                new McInstance(instanceFolder),
                false,
                GameDownload.AssetsIndexExistsBehaviour.DontDownload));
        LauncherCommon.Log(
            $"[Install] 已为 {instanceName} 追加 {loaderName} {loaderVersion} 安装步骤，继承版本：{inheritVersion}");
    }

    private static bool TryCreateForgelikeInstaller(
        McInstallRequest request,
        string minecraftName,
        out string loaderName,
        out string loaderVersion,
        out string inheritVersion,
        out List<string> urls,
        out string? hash)
    {
        loaderVersion = "";
        inheritVersion = minecraftName;
        urls = [];
        hash = null;

        if (request.forgeEntry is not null || request.forgeVersion is not null)
        {
            loaderName = "Forge";
            var entry = request.forgeEntry;
            loaderVersion = entry?.VersionName ?? request.forgeVersion ?? "";
            if (string.IsNullOrEmpty(loaderVersion))
                return false;
            inheritVersion = entry?.Inherit ?? minecraftName;
            var inheritName = inheritVersion.Replace("-", "_");
            var fileVersion = entry?.FileVersion ?? loaderVersion;
            var category = entry?.Category ?? "installer";
            var extension = entry?.FileExtension ?? "jar";
            var fileName =
                $"{inheritName}-{fileVersion}/forge-{inheritName}-{fileVersion}-{category}.{extension}";
            urls =
            [
                $"https://bmclapi2.bangbang93.com/maven/net/minecraftforge/forge/{fileName}",
                $"https://files.minecraftforge.net/maven/net/minecraftforge/forge/{fileName}"
            ];
            hash = entry?.Hash;
            return true;
        }

        if (request.neoForgeEntry is not null || request.neoForgeVersion is not null)
        {
            loaderName = "NeoForge";
            var entry = request.neoForgeEntry
                ?? new GameDownload.DlNeoForgeListEntry(
                    request.neoForgeVersion
                    ?? throw new InvalidOperationException("NeoForge 版本为空"));
            loaderVersion = entry.VersionName;
            inheritVersion = entry.Inherit;
            var url = $"{entry.UrlBase}-installer.jar";
            urls =
            [
                url.Replace(
                    "maven.neoforged.net/releases",
                    "bmclapi2.bangbang93.com/maven"),
                url
            ];
            return true;
        }

        if (request.cleanroomEntry is not null || request.cleanroomVersion is not null)
        {
            loaderName = "Cleanroom";
            var entry = request.cleanroomEntry
                ?? new GameDownload.DlCleanroomListEntry(
                    request.cleanroomVersion
                    ?? throw new InvalidOperationException("Cleanroom 版本为空"));
            loaderVersion = entry.VersionName;
            inheritVersion = entry.Inherit;
            urls = [$"{entry.UrlBase}-installer.jar"];
            return true;
        }

        loaderName = "";
        return false;
    }

    #endregion

    #region 模组型加载器附属文件（Fabric API / Legacy Fabric API）

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
                    var modsFolder = Path.Combine(new McInstance(instanceFolder).PathIndie, "mods");
                    Directory.CreateDirectory(modsFolder);
                    task.output =
                    [
                        file.ToNetFile(
                            modsFolder,
                            Comp.DownloadReason.Dependency,
                            minecraftName,
                            loaderType)
                    ];
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