using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Utils;
using PCL.Network;
using PCL.Network.Loaders;

namespace PCL;

/// <summary>Forgelike 安装器产物类型。</summary>
internal enum ForgelikeArtifact
{
    Installer,
    LegacyUniversal,
    LegacyClient
}

/// <summary>一次 Forgelike 安装所需的全部信息。</summary>
internal sealed class ForgelikeInstallPlan
{
    public ForgelikeArtifact Artifact;
    public string? Hash;
    public required string InheritVersion;
    public required string InstallerPath;
    public required string LoaderName;
    public required string LoaderVersion;
    public required string RunFolder;
    public List<string> Urls = [];

    /// <summary>是否使用现代安装器流程；旧版 Forge 即使下载的是 installer 文件也需走 Legacy 解包。</summary>
    public bool UseModernInstaller;

    public string ArtifactExtension => Artifact == ForgelikeArtifact.Installer ? "jar" : "zip";

    public bool IsInstaller => UseModernInstaller;
}

internal sealed class ForgelikePlanHolder
{
    public ForgelikeInstallPlan? Plan;
}

/// <summary>
///     通过官方安装器（Forge / NeoForge / Cleanroom）安装加载器，并合并安装器产出的版本 Json 与支持库。
///     对旧版 Forge 的 universal / client 压缩包，按 WPF 的 Legacy 方式直接解包并生成版本 Json。
/// </summary>
internal static class LoaderInstallerInstall
{
    private const string WrapperResource = "Resources/forge-installer.jar";
    private const string WrapperClassName = "com.bangbang93.ForgeInstaller";

    public static Loading.LoaderBase CreatePlanLoader(
        McInstallRequest request,
        string minecraftName,
        string tempFolder,
        ForgelikePlanHolder holder)
    {
        return new Loading.LoaderTask<int, int>(
            Lang.Text("Minecraft.Download.Stage.ObtainLoaderDetails", GetLoaderName(request)),
            _ => holder.Plan = ResolvePlan(request, minecraftName, tempFolder))
        {
            ProgressWeight = 1d,
            show = false
        };
    }

    public static Loading.LoaderBase CreateInstallerDownloader(ForgelikePlanHolder holder)
    {
        return new Loading.LoaderTask<int, List<DownloadFile>>(
            Lang.Text(
                "Minecraft.Download.Stage.DownloadLoaderMainFile",
                holder.Plan?.LoaderName ?? "Forgelike"),
            task =>
            {
                var plan = holder.Plan
                    ?? throw new InvalidOperationException("未获取到加载器安装信息");
                task.output =
                [
                    new DownloadFile(
                        plan.Urls,
                        plan.InstallerPath,
                        new FileChecker(
                            64L * 1024,
                            hash: string.IsNullOrEmpty(plan.Hash) ? null : plan.Hash))
                ];
            })
        {
            ProgressWeight = 8d,
            show = false
        };
    }

    public static Loading.LoaderBase CreateDownloadLoader(string displayName)
    {
        return new LoaderDownload(
            Lang.Text("Minecraft.Download.Stage.DownloadLoaderMainFile", displayName),
            [])
        {
            ProgressWeight = 8d
        };
    }

    public static Loading.LoaderBase CreateLibraryAnalyzeLoader(
        ForgelikePlanHolder holder,
        string instanceFolder,
        string instanceName,
        string? minecraftJsonUrl)
    {
        return new Loading.LoaderTask<int, List<DownloadFile>>(
            Lang.Text(
                "Minecraft.Download.Stage.AnalyzeLoaderLibraries",
                holder.Plan?.LoaderName ?? "Forgelike"),
            task => task.output = AnalyzeLibraries(
                task,
                holder.Plan ?? throw new InvalidOperationException("未获取到加载器安装信息"),
                instanceFolder,
                instanceName,
                minecraftJsonUrl))
        {
            ProgressWeight = 2d,
            show = false
        };
    }

    public static Loading.LoaderTask<List<DownloadFile>, string> CreateInstallerRunLoader(
        ForgelikePlanHolder holder,
        string instanceFolder,
        string instanceName)
    {
        var loaderName = holder.Plan?.LoaderName ?? "Forgelike";
        return new Loading.LoaderTask<List<DownloadFile>, string>(
            Lang.Text(
                holder.Plan?.IsInstaller == true
                    ? "Minecraft.Download.Stage.InstallForgeType"
                    : "Minecraft.Download.Stage.InstallForge.MethodB",
                loaderName),
            task => task.output = RunInstaller(
                task,
                holder.Plan ?? throw new InvalidOperationException("未获取到加载器安装信息"),
                instanceFolder,
                instanceName))
        {
            ProgressWeight = 12d
        };
    }

    private static string GetLoaderName(McInstallRequest request)
    {
        if (request.cleanroomEntry is not null || request.cleanroomVersion is not null)
            return "Cleanroom";
        if (request.neoForgeEntry is not null || request.neoForgeVersion is not null)
            return "NeoForge";
        return "Forge";
    }

    private static ForgelikeInstallPlan ResolvePlan(
        McInstallRequest request,
        string minecraftName,
        string tempFolder)
    {
        if (request.cleanroomEntry is not null || request.cleanroomVersion is not null)
        {
            var entry = request.cleanroomEntry
                ?? new GameDownload.DlCleanroomListEntry(
                    request.cleanroomVersion
                    ?? throw new InvalidOperationException("Cleanroom 版本为空"));
            if (!string.Equals(entry.Inherit, "1.12.2", StringComparison.Ordinal))
                throw new Exception(Lang.Text("Download.Install.State.NoVersion"));
            return new ForgelikeInstallPlan
            {
                Artifact = ForgelikeArtifact.Installer,
                InheritVersion = "1.12.2",
                LoaderName = "Cleanroom",
                LoaderVersion = entry.VersionName,
                InstallerPath = GetInstallerPath(tempFolder, "Cleanroom", "jar"),
                RunFolder = Path.Combine(tempFolder, "installer-run-cleanroom"),
                Urls = [$"{entry.UrlBase}-installer.jar"],
                UseModernInstaller = true
            };
        }

        if (request.neoForgeEntry is not null || request.neoForgeVersion is not null)
        {
            var rawVersion = request.neoForgeVersion ?? "";
            var entry = request.neoForgeEntry
                ?? new GameDownload.DlNeoForgeListEntry(
                    minecraftName == "1.20.1" && !rawVersion.StartsWith("1.20.1-")
                        ? $"1.20.1-{rawVersion}"
                        : rawVersion);
            var url = $"{entry.UrlBase}-installer.jar";
            return new ForgelikeInstallPlan
            {
                Artifact = ForgelikeArtifact.Installer,
                InheritVersion = string.IsNullOrEmpty(entry.Inherit)
                    ? minecraftName
                    : entry.Inherit,
                LoaderName = "NeoForge",
                LoaderVersion = entry.VersionName,
                InstallerPath = GetInstallerPath(tempFolder, "NeoForge", "jar"),
                RunFolder = Path.Combine(tempFolder, "installer-run-neoforge"),
                Urls =
                [
                    url.Replace(
                        "maven.neoforged.net/releases",
                        "bmclapi2.bangbang93.com/maven"),
                    url
                ],
                UseModernInstaller = true
            };
        }

        var (forgeVersion, forgeInherit) = NormalizeForgeVersion(
            request.forgeVersion ?? request.forgeEntry?.VersionName ?? "",
            request.forgeEntry?.Inherit ?? minecraftName);
        var forgeEntry = request.forgeEntry;
        if (forgeEntry is null)
        {
            var resolver =
                new Loading.LoaderTask<string, List<GameDownload.DlForgeVersionEntry>>(
                    $"DlForgeVersion {forgeInherit}",
                    GameDownload.DlForgeVersionMain);
            resolver.WaitForExit(forgeInherit);
            forgeEntry = (resolver.output ?? [])
                .FirstOrDefault(entry =>
                    string.Equals(entry.VersionName, forgeVersion, StringComparison.Ordinal)
                    || entry.version.ToString() == forgeVersion
                    || McVersionComparer.CompareVersion(entry.VersionName, forgeVersion) == 0);
            if (forgeEntry is null)
                throw new Exception(
                    Lang.Text(
                        "Minecraft.Download.Error.LoaderDetailsNotFound",
                        "Forge",
                        forgeInherit,
                        forgeVersion));
        }

        forgeVersion = forgeEntry.VersionName;
        forgeInherit = string.IsNullOrEmpty(forgeEntry.Inherit)
            ? forgeInherit
            : forgeEntry.Inherit;
        var artifact = forgeEntry.Category switch
        {
            "universal" => ForgelikeArtifact.LegacyUniversal,
            "client" => ForgelikeArtifact.LegacyClient,
            _ => ForgelikeArtifact.Installer
        };
        var useModernInstaller = forgeEntry.version.Major >= 20;
        var extension = artifact == ForgelikeArtifact.Installer ? "jar" : "zip";
        var category = artifact switch
        {
            ForgelikeArtifact.LegacyUniversal => "universal",
            ForgelikeArtifact.LegacyClient => "client",
            _ => "installer"
        };
        var inheritName = forgeInherit.Replace("-", "_");
        var fileVersion = string.IsNullOrEmpty(forgeEntry.FileVersion)
            ? forgeVersion
            : forgeEntry.FileVersion;
        var relativePath =
            $"net/minecraftforge/forge/{inheritName}-{fileVersion}/forge-{inheritName}-{fileVersion}-{category}.{extension}";
        return new ForgelikeInstallPlan
        {
            Artifact = artifact,
            Hash = forgeEntry.Hash,
            InheritVersion = forgeInherit,
            LoaderName = "Forge",
            LoaderVersion = forgeVersion,
            InstallerPath = GetInstallerPath(tempFolder, "Forge", extension),
            RunFolder = Path.Combine(tempFolder, "installer-run-forge"),
            Urls =
            [
                $"https://bmclapi2.bangbang93.com/maven/{relativePath}",
                $"https://files.minecraftforge.net/maven/{relativePath}"
            ],
            UseModernInstaller = useModernInstaller
        };
    }

    private static (string Version, string Inherit) NormalizeForgeVersion(
        string rawVersion,
        string minecraftName)
    {
        var version = rawVersion.Trim();
        var dashIndex = version.IndexOf('-');
        if (dashIndex > 0)
        {
            var inherit = version[..dashIndex];
            var rest = version[(dashIndex + 1)..];
            if (inherit.StartsWith("1.", StringComparison.Ordinal)
                && McVersionComparer.CompareVersion(inherit, minecraftName) == 0)
                return (rest, inherit);
        }

        return (version, minecraftName);
    }

    private static string GetInstallerPath(string tempFolder, string loaderName, string extension)
    {
        return Path.Combine(tempFolder, $"{loaderName}-installer.{extension}");
    }

    private static List<DownloadFile> AnalyzeLibraries(
        Loading.LoaderTask<int, List<DownloadFile>> task,
        ForgelikeInstallPlan plan,
        string instanceFolder,
        string instanceName,
        string? minecraftJsonUrl)
    {
        if (!File.Exists(plan.InstallerPath))
            throw new Exception(
                Lang.Text(
                    "Minecraft.Download.Error.LoaderInstallerRunFailed",
                    plan.LoaderName));
        // Legacy universal / client 压缩包由 InstallLegacyArtifact 直接解包，不需要提前分析支持库。
        if (!plan.IsInstaller)
            return [];

        Directory.CreateDirectory(Path.Combine(plan.RunFolder, "versions"));
        GameFolders.McFolderLauncherProfilesJsonCreate(plan.RunFolder);
        SeedInheritedVanilla(plan.RunFolder, plan.InheritVersion, instanceFolder, instanceName);

        using var archive = ZipFile.OpenRead(plan.InstallerPath);
        var installProfile = ReadEntryJson(archive, "install_profile.json")
            ?? ReadEntryJson(archive, "version.json")
            ?? throw new InvalidOperationException("安装器缺少 install_profile.json");
        var versionJson = ReadEntryJson(archive, "version.json");
        if (versionJson is not null)
            installProfile.Merge(versionJson);
        if (plan.InheritVersion == "1.16.5" && Config.Download.FixAuthLib)
            installProfile = FixLegacyAuthLib(installProfile);

        var libraries = Libraries.McLibListGetWithJson(
            installProfile,
            true,
            plan.RunFolder);
        if (installProfile["data"]?["MOJMAPS"] is not null)
        {
            task.Progress = 0.4d;
            var clientListUrl = minecraftJsonUrl;
            if (string.IsNullOrEmpty(clientListUrl))
                clientListUrl = GameDownload.DlClientListGet(plan.InheritVersion)?.ToString();
            if (string.IsNullOrEmpty(clientListUrl))
                throw new InvalidOperationException("未获取到 Minecraft Json 地址");

            var rawJson = (JsonObject)LauncherCommon.GetJson(
                NetFacade.NetGetCodeByLoader(
                    GameDownload.DlSourceLauncherOrMetaGet(clientListUrl),
                    isJson: true));
            var mappingsClientNode = installProfile["data"]?["MOJMAPS"]?["client"]
                ?? throw new InvalidOperationException("未找到 MOJMAPS 客户端映射");
            var mappingsClient = mappingsClientNode.ToString().Trim('[', ']');
            var originalName = mappingsClient.BeforeFirst("@");
            var address = Libraries
                .McLibGet(originalName, customMcFolder: plan.RunFolder)
                .Replace(".jar", $"-mappings.{mappingsClient.Split("@")[1]}");
            var clientMappings = rawJson["downloads"]?["client_mappings"]
                ?? throw new InvalidOperationException("未找到客户端映射下载信息");
            libraries.Add(
                new Libraries.McLibToken
                {
                    IsNatives = false,
                    LocalPath = address,
                    OriginalName = originalName,
                    Url = clientMappings["url"]?.ToString() ?? "",
                    size = (long?)clientMappings["size"] ?? -1L,
                    Sha1 = clientMappings["sha1"]?.ToString() ?? ""
                });
        }

        task.Progress = 0.8d;
        var loaderFilePrefix =
            $"{plan.LoaderName.ToLower()}-{plan.InheritVersion}-{plan.LoaderVersion}";
        for (var i = libraries.Count - 1; i >= 0; i--)
            if (libraries[i]
                    .LocalPath.EndsWith(
                        loaderFilePrefix + ".jar",
                        StringComparison.OrdinalIgnoreCase)
                || libraries[i]
                    .LocalPath.EndsWith(
                        loaderFilePrefix + "-client.jar",
                        StringComparison.OrdinalIgnoreCase))
                libraries.RemoveAt(i);

        return Libraries.McLibNetFilesFromTokens(libraries, plan.RunFolder);
    }

    private static JsonObject FixLegacyAuthLib(JsonObject json)
    {
        return (JsonObject)LauncherCommon.GetJson(
            json
                .ToString()
                .Replace("2.1.28/authlib-2.1.28.jar", "2.3.31/authlib-2.3.31.jar")
                .Replace("com.mojang:authlib:2.1.28", "com.mojang:authlib:2.3.31")
                .Replace(
                    "ad54da276bf59983d02d5ed16fc14541354c71fd",
                    "bbd00ca33b052f73a6312254780fc580d2da3535")
                .Replace("76328", "87662"));
    }

    private static void SeedInheritedVanilla(
        string runFolder,
        string inheritVersion,
        string instanceFolder,
        string instanceName)
    {
        try
        {
            var inheritedFolder = Path.Combine(runFolder, "versions", inheritVersion);
            Directory.CreateDirectory(inheritedFolder);
            var instanceJson = Path.Combine(instanceFolder, $"{instanceName}.json");
            var instanceJar = Path.Combine(instanceFolder, $"{instanceName}.jar");
            var inheritedJson = Path.Combine(inheritedFolder, $"{inheritVersion}.json");
            var inheritedJar = Path.Combine(inheritedFolder, $"{inheritVersion}.jar");
            if (File.Exists(instanceJson) && !File.Exists(inheritedJson))
                File.Copy(instanceJson, inheritedJson);
            if (File.Exists(instanceJar) && !File.Exists(inheritedJar))
                File.Copy(instanceJar, inheritedJar);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "准备安装器继承版本文件失败");
        }
    }

    private static JsonObject? ReadEntryJson(ZipArchive archive, string entryName)
    {
        var entry = archive.GetEntry(entryName);
        return entry is null
            ? null
            : (JsonObject)LauncherCommon.GetJson(LauncherCommon.ReadFile(entry.Open()));
    }

    private static string RunInstaller(
        Loading.LoaderTask<List<DownloadFile>, string> task,
        ForgelikeInstallPlan plan,
        string instanceFolder,
        string instanceName)
    {
        if (!plan.IsInstaller)
            return InstallLegacyArtifact(task, plan, instanceFolder, instanceName);

        var versionsFolder = Path.Combine(plan.RunFolder, "versions");
        Directory.CreateDirectory(versionsFolder);
        var existingVersionSet = Directory
            .GetDirectories(versionsFolder)
            .Select(Path.GetFileName)
            .Where(name => name is not null)
            .Select(name => name!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var wrapperPath = ExtractWrapper(plan.RunFolder);
        var minJava = McVersionComparer.CompareVersionGe(plan.InheritVersion, "1.20.5")
            ? new Version(21, 0, 0, 0)
            : McVersionComparer.CompareVersionGe(plan.InheritVersion, "1.18")
                ? new Version(17, 0, 0, 0)
                : new Version(1, 8, 0, 60);
        var java = LoaderSpecialInstall.SelectJava(
            task,
            minJava,
            true);
        task.Progress = 0.1d;

        var classPath = $"{wrapperPath}{Path.PathSeparator}{plan.InstallerPath}";
        var args = java.Installation.MajorVersion >= 9
            ? $"--add-exports cpw.mods.bootstraplauncher/cpw.mods.bootstraplauncher=ALL-UNNAMED -cp \"{classPath}\" {WrapperClassName} \"{plan.RunFolder}\""
            : $"-cp \"{classPath}\" {WrapperClassName} \"{plan.RunFolder}\"";
        var startInfo = new ProcessStartInfo
        {
            FileName = java.Installation.JavaExePath,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        var lines = LoaderSpecialInstall.RunInstallerProcess(
            task,
            startInfo,
            plan.LoaderName,
            Path.Combine(plan.RunFolder, "installer.log"),
            20);
        if (!lines.Any(line => line.Trim().Equals("true", StringComparison.OrdinalIgnoreCase)))
            throw new Exception(
                Lang.Text(
                    "Minecraft.Download.Error.LoaderInstallerFailedLastLine",
                    plan.LoaderName,
                    LoaderSpecialInstall.GetLastLines(lines)));
        task.Progress = 0.9d;

        var generatedFolder = FindGeneratedVersionFolder(
            versionsFolder,
            existingVersionSet,
            plan.LoaderName);
        if (generatedFolder is null)
            throw new Exception(
                Lang.Text(
                    "Minecraft.Download.Error.LoaderInstallerFailedLastLine",
                    plan.LoaderName,
                    LoaderSpecialInstall.GetLastLines(lines)));
        var loaderJson = Directory.GetFiles(generatedFolder, "*.json").FirstOrDefault()
            ?? throw new Exception(
                Lang.Text(
                    "Minecraft.Download.Error.LoaderInstallerFailedLastLine",
                    plan.LoaderName,
                    LoaderSpecialInstall.GetLastLines(lines)));

        CopyGeneratedVersionArtifacts(generatedFolder, instanceFolder);
        CopyGeneratedLibraries(plan.RunFolder, instanceFolder);
        LauncherCommon.Log($"[Install] {plan.LoaderName} 安装器已生成版本 Json：{loaderJson}");
        return loaderJson;
    }

    private static string? FindGeneratedVersionFolder(
        string versionsFolder,
        IReadOnlySet<string> existingVersionSet,
        string loaderName)
    {
        var generated = Directory
            .GetDirectories(versionsFolder)
            .Where(folder => !existingVersionSet.Contains(Path.GetFileName(folder)))
            .Where(folder => Directory.GetFiles(folder, "*.json").Length > 0)
            .ToList();
        if (generated.Count <= 1)
            return generated.FirstOrDefault();

        var filtered = generated
            .Where(folder =>
                Path.GetFileName(folder).Contains(loaderName, StringComparison.OrdinalIgnoreCase)
                || Path.GetFileName(folder).Contains("forge", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (filtered.Count == 1)
            return filtered[0];

        LauncherCommon.Log(
            $"[Install] 安装器生成了多个疑似版本文件夹，无法确定：{string.Join(";", generated.Select(Path.GetFileName))}");
        return null;
    }

    private static void CopyGeneratedVersionArtifacts(
        string generatedFolder,
        string instanceFolder)
    {
        foreach (var file in Directory.GetFiles(generatedFolder, "*", SearchOption.AllDirectories))
        {
            var extension = Path.GetExtension(file);
            if (extension.Equals(".json", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".jar", StringComparison.OrdinalIgnoreCase))
                continue;
            var target = Path.Combine(
                instanceFolder,
                Path.GetRelativePath(generatedFolder, file));
            LauncherCommon.CopyFile(file, target);
        }
    }

    private static string InstallLegacyArtifact(
        Loading.LoaderTask<List<DownloadFile>, string> task,
        ForgelikeInstallPlan plan,
        string instanceFolder,
        string instanceName)
    {
        try
        {
            ZipArchive? installer = null;
            try
            {
                installer = ZipFile.OpenRead(plan.InstallerPath);
                var installProfile = ReadEntryJson(installer, "install_profile.json");
                var versionJson = ReadEntryJson(installer, "version.json");

                JsonObject? outputJson = null;
                if (installProfile?["install"] is not null)
                {
                    var installInfo = installProfile["install"]!;
                    var jarAddress = Libraries.McLibGet(
                        installInfo["path"]?.ToString()
                        ?? throw new InvalidOperationException("安装器缺少安装路径"),
                        customMcFolder: LoaderSpecialInstall.GetMcFolder(instanceFolder));
                    using (var installStream = (installer.GetEntry(
                                   installInfo["filePath"]?.ToString()
                                   ?? throw new InvalidOperationException("安装器缺少安装文件名"))
                               ?? throw new InvalidOperationException("安装器缺少安装文件")).Open())
                    {
                        LauncherCommon.WriteFile(jarAddress, installStream);
                    }

                    outputJson = (JsonObject)(installProfile["versionInfo"]?.DeepClone()
                        ?? throw new InvalidOperationException(
                            "安装器缺少 versionInfo 信息"));
                    if (outputJson["inheritsFrom"] is null)
                        outputJson["inheritsFrom"] = plan.InheritVersion;
                }
                else if (installProfile?["json"] is not null)
                {
                    var entryName = installProfile["json"]!.ToString().TrimStart('/');
                    var entry = installer.GetEntry(entryName)
                        ?? throw new InvalidOperationException("安装器缺少版本 Json 文件");
                    outputJson = (JsonObject)LauncherCommon.GetJson(
                        LauncherCommon.ReadFile(entry.Open()));
                }
                else if (versionJson is not null)
                {
                    outputJson = versionJson;
                }
                else
                {
                    var entry = installer.Entries
                            .FirstOrDefault(candidate => candidate.FullName.EndsWith(
                                "version.json",
                                StringComparison.OrdinalIgnoreCase))
                        ?? throw new InvalidOperationException("安装器缺少版本 Json 文件");
                    outputJson = (JsonObject)LauncherCommon.GetJson(
                        LauncherCommon.ReadFile(entry.Open()));
                }

                ExtractLegacyLibraries(installer, instanceFolder);
                task.Progress = 0.8d;
                if (outputJson is null)
                    throw new InvalidOperationException("安装器缺少版本 Json 文件");
                outputJson["id"] = $"{plan.LoaderName.ToLower()}-{plan.LoaderVersion}";
                var outputFolder = Path.Combine(plan.RunFolder, "legacy-generated");
                Directory.CreateDirectory(outputFolder);
                var outputPath = Path.Combine(
                    outputFolder,
                    $"{plan.LoaderName}-{plan.LoaderVersion}.json");
                LauncherCommon.WriteFile(
                    outputPath,
                    outputJson.ToString(),
                    new UTF8Encoding(false));
                LauncherCommon.Log(
                    $"[Install] 已按 Legacy 方式生成 {plan.LoaderName} 版本 Json：{outputPath}");
                return outputPath;
            }
            finally
            {
                installer?.Dispose();
            }
        }
        catch (Exception ex) when (ex is not CancelledException)
        {
            throw new Exception(
                Lang.Text("Minecraft.Download.Error.ForgeOldInstallFailed"),
                ex);
        }
    }

    private static void ExtractLegacyLibraries(ZipArchive archive, string instanceFolder)
    {
        var mcFolder = LoaderSpecialInstall.GetMcFolder(instanceFolder);
        foreach (var entry in archive.Entries)
        {
            if (!entry.FullName.StartsWith("maven/", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrEmpty(entry.Name))
                continue;
            var relative =
                entry.FullName["maven/".Length..].Replace('/', Path.DirectorySeparatorChar);
            var target = Path.Combine(mcFolder, "libraries", relative);
            using var stream = entry.Open();
            LauncherCommon.WriteFile(target, stream);
        }
    }

    private static void CopyGeneratedLibraries(string runFolder, string instanceFolder)
    {
        var tempLibraries = Path.Combine(runFolder, "libraries");
        if (!Directory.Exists(tempLibraries))
            return;
        try
        {
            LauncherCommon.CopyDirectory(
                tempLibraries,
                Path.Combine(LoaderSpecialInstall.GetMcFolder(instanceFolder), "libraries"));
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "复制安装器生成的支持库失败");
        }
    }

    private static string ExtractWrapper(string runFolder)
    {
        var wrapperPath = Path.Combine(runFolder, "forge-installer.jar");
        using var stream = LauncherCommon.GetResourceStream(WrapperResource)
            ?? throw new Exception("缺少 Forge 安装器包装资源");
        using var file = File.Create(wrapperPath);
        stream.CopyTo(file);
        return wrapperPath;
    }
}