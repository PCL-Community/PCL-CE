using System.Diagnostics;
using System.IO.Compression;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft;
using PCL.Core.Utils;
using PCL.Network;
using PCL.Network.Loaders;

namespace PCL;

/// <summary>
///     通过官方安装器（Forge / NeoForge / Cleanroom）安装加载器，并合并安装器产出的版本 Json 与支持库。
/// </summary>
internal static class LoaderInstallerInstall
{
    private const string WrapperResource = "Resources/forge-installer.jar";
    private const string WrapperClassName = "com.bangbang93.ForgeInstaller";

    public static string GetInstallerPath(string tempFolder, string loaderName)
    {
        return Path.Combine(tempFolder, $"{loaderName}-installer.jar");
    }

    public static Loading.LoaderBase CreateInstallerDownloader(
        string displayName,
        IEnumerable<string> urls,
        string targetPath,
        string? hash = null)
    {
        return new LoaderDownload(
            Lang.Text("Minecraft.Download.Stage.DownloadLoaderMainFile", displayName),
            [
                new DownloadFile(
                    urls.Where(url => !string.IsNullOrWhiteSpace(url)).Distinct().ToList(),
                    targetPath,
                    new FileChecker(64L * 1024, hash: string.IsNullOrEmpty(hash) ? null : hash))
            ])
        {
            ProgressWeight = 8d
        };
    }

    public static Loading.LoaderTask<int, List<DownloadFile>> CreateLibraryAnalyzeLoader(
        string loaderName,
        string loaderVersion,
        string inheritVersion,
        string installerPath,
        string runFolder,
        string instanceFolder,
        string instanceName,
        string? minecraftJsonUrl)
    {
        return new Loading.LoaderTask<int, List<DownloadFile>>(
            Lang.Text("Minecraft.Download.Stage.AnalyzeLoaderLibraries", loaderName),
            task => task.output = AnalyzeLibraries(
                task,
                loaderName,
                loaderVersion,
                inheritVersion,
                installerPath,
                runFolder,
                instanceFolder,
                instanceName,
                minecraftJsonUrl))
        {
            ProgressWeight = 1d,
            show = false
        };
    }

    private static List<DownloadFile> AnalyzeLibraries(
        Loading.LoaderTask<int, List<DownloadFile>> task,
        string loaderName,
        string loaderVersion,
        string inheritVersion,
        string installerPath,
        string runFolder,
        string instanceFolder,
        string instanceName,
        string? minecraftJsonUrl)
    {
        if (!File.Exists(installerPath))
            throw new Exception(
                Lang.Text("Minecraft.Download.Error.LoaderInstallerRunFailed", loaderName));

        Directory.CreateDirectory(Path.Combine(runFolder, "versions"));
        GameFolders.McFolderLauncherProfilesJsonCreate(runFolder);
        SeedInheritedVanilla(runFolder, inheritVersion, instanceFolder, instanceName);

        using var archive = ZipFile.OpenRead(installerPath);
        var installProfile = ReadEntryJson(archive, "install_profile.json")
            ?? throw new InvalidOperationException("安装器缺少 install_profile.json");
        var versionJson = ReadEntryJson(archive, "version.json");
        if (versionJson is not null)
            installProfile.Merge(versionJson);

        var libraries = Libraries.McLibListGetWithJson(
            installProfile,
            true,
            runFolder);
        if (installProfile["data"]?["MOJMAPS"] is not null)
        {
            task.Progress = 0.4d;
            var clientListUrl = minecraftJsonUrl;
            if (string.IsNullOrEmpty(clientListUrl))
                clientListUrl = GameDownload.DlClientListGet(inheritVersion)?.ToString();
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
                .McLibGet(originalName, customMcFolder: runFolder)
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
        var loaderFilePrefix = $"{loaderName.ToLower()}-{inheritVersion}-{loaderVersion}";
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

        return Libraries.McLibNetFilesFromTokens(libraries, runFolder);
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

    public static Loading.LoaderTask<List<DownloadFile>, string> CreateInstallerRunLoader(
        string loaderName,
        string installerPath,
        string runFolder,
        string instanceFolder,
        string instanceName)
    {
        return new Loading.LoaderTask<List<DownloadFile>, string>(
            Lang.Text("Minecraft.Download.Stage.InstallForgeType", loaderName),
            task => task.output = RunInstaller(
                task,
                loaderName,
                installerPath,
                runFolder,
                instanceFolder,
                instanceName))
        {
            ProgressWeight = 12d
        };
    }

    private static string RunInstaller(
        Loading.LoaderTask<List<DownloadFile>, string> task,
        string loaderName,
        string installerPath,
        string runFolder,
        string instanceFolder,
        string instanceName)
    {
        var versionsFolder = Path.Combine(runFolder, "versions");
        Directory.CreateDirectory(versionsFolder);
        var existingVersionSet = Directory
            .GetDirectories(versionsFolder)
            .Select(Path.GetFileName)
            .Where(name => name is not null)
            .Select(name => name!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var wrapperPath = ExtractWrapper(runFolder);

        JavaEntry java;
        lock (JavaRuntime.JavaLock)
        {
            java = JavaRuntime.JavaSelect(
                    Lang.Text("Minecraft.Download.Error.InstallationCanceled"),
                    new Version(1, 8, 0, 60))
                ?? throw new Exception(
                    Lang.Text("Minecraft.Download.Error.JavaNotFoundInstallCanceled"));
        }

        var outputLines = RunJavaInstaller(java, wrapperPath, installerPath, runFolder, task);
        task.Progress = 0.9d;

        var loaderJson = Directory
            .GetDirectories(versionsFolder)
            .Where(folder => !existingVersionSet.Contains(Path.GetFileName(folder)))
            .SelectMany(folder => Directory.GetFiles(folder, "*.json"))
            .FirstOrDefault();
        if (loaderJson is null)
            throw new Exception(
                Lang.Text(
                    "Minecraft.Download.Error.LoaderInstallerFailedLastLine",
                    loaderName,
                    string.Join("\r\n", outputLines.TakeLast(5))));

        CopyGeneratedLibraries(runFolder);
        LauncherCommon.Log($"[Install] {loaderName} 安装器已生成版本 Json：{loaderJson}");
        return loaderJson;
    }

    private static void CopyGeneratedLibraries(string runFolder)
    {
        var mcFolder = GameFolders.mcFolderSelected;
        var tempLibraries = Path.Combine(runFolder, "libraries");
        if (string.IsNullOrEmpty(mcFolder) || !Directory.Exists(tempLibraries))
            return;
        try
        {
            LauncherCommon.CopyDirectory(tempLibraries, Path.Combine(mcFolder, "libraries"));
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

    private static List<string> RunJavaInstaller(
        JavaEntry java,
        string wrapperPath,
        string installerPath,
        string runFolder,
        Loading.LoaderBase task)
    {
        var classPath = $"{wrapperPath}{Path.PathSeparator}{installerPath}";
        var arguments = $"-cp \"{classPath}\" {WrapperClassName} \"{runFolder}\"";
        var lines = new List<string>();
        var startInfo = new ProcessStartInfo
        {
            FileName = java.Installation.JavaExePath,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        LauncherCommon.Log($"[Install] 开始运行安装器：{java.Installation.JavaExePath} {arguments}");
        using var process = new Process
        {
            StartInfo = startInfo
        };
        process.OutputDataReceived += (_, e) => OnInstallerLine(e.Data, lines, task);
        process.ErrorDataReceived += (_, e) => OnInstallerLine(e.Data, lines, task);
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        var deadline = DateTime.UtcNow.AddMinutes(20);
        while (!process.HasExited)
        {
            if (task.State == LoadState.Aborted)
            {
                KillQuietly(process);
                throw new CancelledException();
            }

            if (DateTime.UtcNow > deadline)
            {
                KillQuietly(process);
                throw new TimeoutException(
                    Lang.Text("Minecraft.Download.Error.LoaderInstallerRunFailed", "加载器"));
            }

            Thread.Sleep(100);
        }

        process.WaitForExit();
        if (lines.All(line => line != "true"))
            throw new Exception(
                Lang.Text(
                    "Minecraft.Download.Error.LoaderInstallerFailedLastLine",
                    "加载器",
                    string.Join("\r\n", lines.TakeLast(5))));
        return lines;
    }

    private static void KillQuietly(Process process)
    {
        try
        {
            process.Kill(true);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "中止安装器进程失败");
        }
    }

    private static void OnInstallerLine(string? line, List<string> lines, Loading.LoaderBase task)
    {
        if (line is null)
            return;
        lock (lines)
        {
            lines.Add(line);
            if (lines.Count > 200)
                lines.RemoveAt(0);
        }

        if (LauncherCommon.ModeDebug)
            LauncherCommon.Log("[Installer] " + line);
    }
}