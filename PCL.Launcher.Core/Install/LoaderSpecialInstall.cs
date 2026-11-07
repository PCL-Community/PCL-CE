using System.Diagnostics;
using System.Globalization;
using System.Text;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.IO.Net.Http;
using PCL.Core.Minecraft;
using PCL.Core.Utils;
using PCL.Network;
using PCL.Network.Loaders;

namespace PCL;

/// <summary>
///     OptiFine / LiteLoader / LabyMod 的特殊安装链路。
///     OptiFine 在存在模组加载器时作为模组放入 mods；否则运行官方安装器或生成 Legacy 版本 Json。
///     LiteLoader 按官方版本元数据生成加载器 Json；LabyMod 下载官方 manifest 并在合并阶段注入支持库。
/// </summary>
internal static class LoaderSpecialInstall
{
    #region LabyMod

    public static void AddLabyModLoaders(
        List<Loading.LoaderBase> loaders,
        McInstallRequest request,
        string instanceFolder,
        string instanceName,
        string minecraftName,
        string tempFolder,
        InstallJsonSourceCollection sources)
    {
        if (request.labyModCommitRef is null)
            return;
        var channel = string.IsNullOrEmpty(request.labyModChannel)
            ? "production"
            : request.labyModChannel;
        var safeMinecraftName = minecraftName.Replace("∞", "infinite");
        var id = $"labymod-{request.labyModCommitRef}-{minecraftName}";
        var target = Path.Combine(tempFolder, "versions", id, $"{id}.json");
        var url =
            $"https://releases.r2.labymod.net/api/v1/download/manifest/labymod4/{channel}/{safeMinecraftName}/{request.labyModCommitRef}.json";

        loaders.Add(
            new Loading.LoaderTask<int, List<DownloadFile>>(
                Lang.Text("Minecraft.Download.Stage.ObtainLabyModClientUrl"),
                task =>
                {
                    task.output =
                    [
                        new DownloadFile(
                            [url],
                            target,
                            new FileChecker(isJson: true),
                            true)
                    ];
                    task.Progress = 0.5d;
                })
            {
                ProgressWeight = 2d
            });
        loaders.Add(
            new LoaderDownload(
                Lang.Text("Minecraft.Download.Stage.DownloadLoaderLibraries", "LabyMod"),
                [])
            {
                ProgressWeight = 10d
            });
        loaders.Add(
            new Loading.LoaderTask<int, int>(
                Lang.Text("Minecraft.Download.Stage.InstallGame"),
                _ => sources.Add(InstallJsonKind.LabyMod, target))
            {
                ProgressWeight = 0.1d,
                show = false
            });
        LauncherCommon.Log($"[Install] 已为 {instanceName} 追加 LabyMod {channel} 安装步骤");
    }

    #endregion

    #region OptiFine

    public static void AddOptiFineLoaders(
        List<Loading.LoaderBase> loaders,
        McInstallRequest request,
        string instanceFolder,
        string instanceName,
        string minecraftName,
        string tempFolder,
        InstallJsonSourceCollection sources)
    {
        var entry = request.optiFineEntry;
        if (entry is null)
            return;
        if (request.OptiFineAsMod)
        {
            AddOptiFineModLoaders(
                loaders,
                entry,
                request,
                instanceFolder,
                minecraftName,
                tempFolder);
            return;
        }

        var isNewVersion = entry.Inherit is { } inherit
            && (inherit.Contains('w') || McVersionComparer.CompareVersionGe(inherit, "1.14"));
        var target = isNewVersion
            ? Path.Combine(tempFolder, "OptiFine.jar")
            : GetLegacyOptiFineTarget(instanceFolder, entry);
        var javaVersion = entry.Inherit is { } inheritVersion
            && McVersionComparer.CompareVersionGe(inheritVersion, "26.1.2")
                ? new Version(21, 0, 0, 0)
                : new Version(1, 8, 0, 0);

        loaders.Add(
            new Loading.LoaderTask<int, List<DownloadFile>>(
                Lang.Text("Minecraft.Download.Stage.ObtainOptiFineUrl"),
                task =>
                {
                    var sourceList = GetOptiFineDownloadSources(entry, task);
                    task.output =
                    [
                        new DownloadFile(
                            sourceList,
                            target,
                            new FileChecker(isNewVersion ? 300 * 1024 : 64 * 1024))
                    ];
                })
            {
                ProgressWeight = 4d
            });
        loaders.Add(
            new LoaderDownload(
                Lang.Text("Minecraft.Download.Stage.DownloadOptiFineMainFile"),
                [])
            {
                ProgressWeight = 8d
            });

        if (isNewVersion)
            loaders.Add(
                new Loading.LoaderTask<int, int>(
                    Lang.Text("Minecraft.Download.Stage.InstallOptiFine.MethodA"),
                    task =>
                    {
                        var jsonPath = InstallNewOptiFine(
                            task,
                            entry,
                            instanceFolder,
                            instanceName,
                            tempFolder,
                            target,
                            javaVersion);
                        sources.Add(InstallJsonKind.OptiFine, jsonPath);
                    })
                {
                    ProgressWeight = 10d
                });
        else
            loaders.Add(
                new Loading.LoaderTask<int, int>(
                    Lang.Text("Minecraft.Download.Stage.InstallOptiFine.MethodB"),
                    task =>
                    {
                        var jsonPath = InstallLegacyOptiFine(
                            instanceFolder,
                            instanceName,
                            minecraftName,
                            entry,
                            tempFolder);
                        sources.Add(InstallJsonKind.OptiFine, jsonPath);
                    })
                {
                    ProgressWeight = 1d
                });
    }

    private static void AddOptiFineModLoaders(
        List<Loading.LoaderBase> loaders,
        GameDownload.DlOptiFineListEntry entry,
        McInstallRequest request,
        string instanceFolder,
        string minecraftName,
        string tempFolder)
    {
        var fileName = entry.NameFile
            ?? throw new InvalidOperationException("OptiFine 版本缺少文件名");
        var modsFolder = Path.Combine(new McInstance(instanceFolder).PathIndie, "mods");
        // LiteLoader 要求 OptiFine 位于 mods/<MC 版本> 下，其余加载器直接放在 mods 下。
        if (request.liteLoaderEntry is not null)
            modsFolder = Path.Combine(modsFolder, minecraftName);
        var target = Path.Combine(modsFolder, fileName);

        loaders.Add(
            new Loading.LoaderTask<int, List<DownloadFile>>(
                Lang.Text("Minecraft.Download.Stage.ObtainOptiFineDownloadUrl"),
                task =>
                {
                    var existed = File.Exists(target);
                    task.output =
                    [
                        new DownloadFile(
                            GetOptiFineDownloadSources(entry, task),
                            target,
                            new FileChecker(64 * 1024))
                    ];
                    if (!existed)
                        McInstallRecovery.RegisterCreatedFile(target);
                    task.Progress = 0.9d;
                })
            {
                ProgressWeight = 6d
            });
        loaders.Add(
            new LoaderDownload(
                Lang.Text("Minecraft.Download.Stage.DownloadOptiFineMainFile"),
                [])
            {
                ProgressWeight = 10d
            });

        LauncherCommon.Log($"[Install] OptiFine 将作为模组下载到 {target}");
    }

    private static string GetLegacyOptiFineTarget(
        string instanceFolder,
        GameDownload.DlOptiFineListEntry entry)
    {
        var nameFile = entry.NameFile
            ?? throw new InvalidOperationException("OptiFine 版本缺少文件名");
        var mcFolder = GetMcFolder(instanceFolder);
        var version = nameFile
            .Replace("OptiFine_", "")
            .Replace(".jar", "")
            .Replace("preview_", "");
        return Path.Combine(
            mcFolder,
            "libraries",
            "optifine",
            "OptiFine",
            version,
            nameFile.Replace("OptiFine_", "OptiFine-").Replace("preview_", ""));
    }

    private static string InstallNewOptiFine(
        Loading.LoaderTask<int, int> task,
        GameDownload.DlOptiFineListEntry entry,
        string instanceFolder,
        string instanceName,
        string tempFolder,
        string target,
        Version javaVersion)
    {
        var inherit = entry.Inherit
            ?? throw new InvalidOperationException("OptiFine 版本缺少对应的 Minecraft 版本");
        var baseHome = Path.Combine(tempFolder, "optifine-home");
        var baseMcFolder = Path.Combine(baseHome, ".minecraft");
        var inheritedVersionFolder = Path.Combine(baseMcFolder, "versions", inherit);
        Directory.CreateDirectory(inheritedVersionFolder);
        GameFolders.McFolderLauncherProfilesJsonCreate(baseMcFolder);
        LauncherCommon.CopyFile(
            Path.Combine(instanceFolder, $"{instanceName}.json"),
            Path.Combine(inheritedVersionFolder, $"{inherit}.json"));
        LauncherCommon.CopyFile(
            Path.Combine(instanceFolder, $"{instanceName}.jar"),
            Path.Combine(inheritedVersionFolder, $"{inherit}.jar"));

        var versionRoot = Path.Combine(baseMcFolder, "versions");
        var before = Directory.Exists(versionRoot)
            ? Directory
                .GetDirectories(versionRoot)
                .Select(Path.GetFileName)
                .Where(name => name is not null)
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        RunOptiFineInstaller(task, baseHome, target, javaVersion);
        task.Progress = 0.9d;

        var generated = Directory.Exists(versionRoot)
            ? Directory
                .GetDirectories(versionRoot)
                .Where(folder => !before.Contains(Path.GetFileName(folder)))
                .OrderByDescending(folder => folder)
                .ToList()
            : [];
        var generatedJson = generated
            .SelectMany(folder => Directory.GetFiles(folder, "*.json"))
            .FirstOrDefault();
        if (generatedJson is null)
            throw new Exception(
                Lang.Text("Minecraft.Download.Error.OptiFineInstallerRunFailed"));

        // 老版本安装器会把 OptiFine 的 jar 放进 libraries，复制到全局 libraries 后再合并 Json。
        if (Directory.Exists(Path.Combine(baseMcFolder, "libraries")))
            LauncherCommon.CopyDirectory(
                Path.Combine(baseMcFolder, "libraries"),
                Path.Combine(GetMcFolder(instanceFolder), "libraries"));

        var outputFolder = Path.Combine(tempFolder, "optifine-generated");
        Directory.CreateDirectory(outputFolder);
        var outputPath = Path.Combine(outputFolder, $"{entry.NameVersion ?? "OptiFine"}.json");
        LauncherCommon.CopyFile(generatedJson, outputPath);
        LauncherCommon.Log($"[Install] OptiFine 安装器生成版本 Json：{generatedJson}");
        return outputPath;
    }

    private static void RunOptiFineInstaller(
        Loading.LoaderTask<int, int> task,
        string baseHome,
        string target,
        Version javaVersion)
    {
        var useWrapper = OperatingSystem.IsWindows()
            && LauncherCommon.IsUtf8CodePage()
            && !Config.Launch.DisableJlw;
        while (true)
            try
            {
                RunOptiFineInstallerOnce(task, baseHome, target, javaVersion, useWrapper);
                return;
            }
            catch (Exception ex) when (ex is not CancelledException)
            {
                if (useWrapper)
                    throw new Exception(
                        Lang.Text("Minecraft.Download.Error.OptiFineInstallerRunFailed"),
                        ex);
                LauncherCommon.Log(ex, "不使用 JavaWrapper 安装 OptiFine 失败，将使用 JavaWrapper 并重试");
                useWrapper = true;
            }
    }

    private static void RunOptiFineInstallerOnce(
        Loading.LoaderTask<int, int> task,
        string baseHome,
        string target,
        Version javaVersion,
        bool useWrapper)
    {
        var java = SelectJava(task, javaVersion, true);
        var args = new StringBuilder();
        if (useWrapper)
            args.Append(
                $"-Doolloo.jlw.tmpdir=\"{LauncherPaths.PureAsciiDirectory.TrimEnd(Path.DirectorySeparatorChar)}\" ");
        args.Append($"-Duser.home=\"{baseHome.TrimEnd(Path.DirectorySeparatorChar)}\" ");
        if (useWrapper)
            args.Append(
                $"-cp \"{target}\" -jar \"{GameLaunch.ExtractJavaWrapper()}\" optifine.Installer");
        else
            args.Append($"-cp \"{target}\" optifine.Installer");
        if (java.Installation.MajorVersion >= 9)
            args.Insert(
                0,
                "--add-exports cpw.mods.bootstraplauncher/cpw.mods.bootstraplauncher=ALL-UNNAMED ");

        var startInfo = new ProcessStartInfo
        {
            FileName = java.Installation.JavaExePath,
            Arguments = args.ToString(),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = LauncherCommon.ShortenPath(baseHome),
            Environment =
            {
                ["appdata"] = baseHome
            }
        };
        var lines = RunInstallerProcess(
            task,
            startInfo,
            "OptiFine",
            Path.Combine(baseHome, "optifine-install.log"),
            20);
        if (lines.Any(line =>
                line.Contains("Exception", StringComparison.OrdinalIgnoreCase)
                || line.TrimStart().StartsWith("at ", StringComparison.Ordinal)))
            throw new Exception(
                Lang.Text(
                    "Minecraft.Download.Error.InstallerFailedLastLine",
                    GetLastLines(lines)));
    }

    private static List<string> GetOptiFineDownloadSources(
        GameDownload.DlOptiFineListEntry entry,
        Loading.LoaderBase task)
    {
        var inherit = entry.Inherit ?? "";
        var displayName = entry.DisplayName ?? "";
        var bmclapiInherit = inherit is "1.8" or "1.9" ? $"{inherit}.0" : inherit;
        var sources = new List<string>
        {
            entry.IsPreview
                ? $"https://bmclapi2.bangbang93.com/optifine/{bmclapiInherit}/HD_U_{displayName.Replace($"{inherit} ", "").Replace(" ", "/")}"
                : $"https://bmclapi2.bangbang93.com/optifine/{bmclapiInherit}/HD_U/{displayName.Replace($"{inherit} ", "")}"
        };

        try
        {
            using var response = HttpRequest
                .Create($"https://optifine.net/adloadx?f={entry.NameFile}")
                .WithHeader("Accept", "text/html")
                .WithHeader("Accept-Language", "en-US,en;q=0.5")
                .WithHeader("X-Requested-With", "XMLHttpRequest")
                .SendAsync()
                .GetAwaiter()
                .GetResult();
            response.EnsureSuccessStatusCode();
            var page = response.AsString();
            var link = page.RegexSeek(@"downloadx\?f=[^""']+");
            if (!string.IsNullOrEmpty(link))
            {
                sources.Add($"https://optifine.net/{link}");
                task.Progress = 0.8d;
            }
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, $"获取 OptiFine {entry.DisplayName} 官方下载地址失败");
        }

        return [.. sources.Distinct()];
    }

    private static string InstallLegacyOptiFine(
        string instanceFolder,
        string instanceName,
        string minecraftName,
        GameDownload.DlOptiFineListEntry entry,
        string tempFolder)
    {
        var inherit = entry.Inherit ?? minecraftName;
        var nameFile = entry.NameFile
            ?? throw new InvalidOperationException("OptiFine 版本缺少文件名");
        var version = nameFile
            .Replace("OptiFine_", "")
            .Replace(".jar", "")
            .Replace("preview_", "");
        var instanceJson = ReadJson(Path.Combine(instanceFolder, $"{instanceName}.json"));
        var releaseDate = (entry.ReleaseTime
            ?? instanceJson["releaseTime"]?.ToObject<DateTime>()
            ?? DateTime.Now).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var isOldJson = instanceJson["minecraftArguments"] is not null;
        var jsonId = entry.NameVersion ?? $"{inherit}-OptiFine_{version}";

        var json = new JsonObject
        {
            ["id"] = jsonId,
            ["inheritsFrom"] = inherit,
            ["time"] = $"{releaseDate}T23:33:33+08:00",
            ["releaseTime"] = $"{releaseDate}T23:33:33+08:00",
            ["type"] = "release",
            ["libraries"] = new JsonArray
            {
                new JsonObject
                {
                    ["name"] = $"optifine:OptiFine:{version}"
                },
                new JsonObject
                {
                    ["name"] = "net.minecraft:launchwrapper:1.12"
                }
            },
            ["mainClass"] = "net.minecraft.launchwrapper.Launch"
        };
        if (isOldJson)
        {
            json["minimumLauncherVersion"] = 18;
            json["minecraftArguments"] =
                $"{instanceJson["minecraftArguments"]} --tweakClass optifine.OptiFineTweaker";
        }
        else
        {
            json["minimumLauncherVersion"] = "21";
            json["arguments"] = new JsonObject
            {
                ["game"] = new JsonArray
                {
                    (JsonNode)"--tweakClass",
                    (JsonNode)"optifine.OptiFineTweaker"
                }
            };
        }

        var outputFolder = Path.Combine(tempFolder, "optifine-legacy");
        Directory.CreateDirectory(outputFolder);
        var outputPath = Path.Combine(outputFolder, $"{jsonId}.json");
        LauncherCommon.WriteFile(outputPath, json.ToString(), new UTF8Encoding(false));
        LauncherCommon.Log($"[Install] 已生成 Legacy OptiFine 版本 Json：{outputPath}");
        return outputPath;
    }

    private static JsonObject ReadJson(string path)
    {
        var text = LauncherCommon.ReadFile(path).TrimStart('\uFEFF');
        if (!text.TrimStart().StartsWith('{'))
            throw new Exception($"版本 Json 无效：{path}");
        return (JsonObject)LauncherCommon.GetJson(text);
    }

    #endregion

    #region LiteLoader

    public static void AddLiteLoaderLoaders(
        List<Loading.LoaderBase> loaders,
        McInstallRequest request,
        string instanceFolder,
        string instanceName,
        string minecraftName,
        string tempFolder,
        InstallJsonSourceCollection sources)
    {
        if (request.liteLoaderEntry is null)
            return;

        loaders.Add(
            new Loading.LoaderTask<int, int>(
                Lang.Text("Minecraft.Download.Stage.InstallLiteLoader"),
                _ =>
                {
                    var entry = ResolveLiteLoaderEntry(request.liteLoaderEntry, minecraftName);
                    var jsonPath = CreateLiteLoaderJson(
                        entry,
                        minecraftName,
                        tempFolder);
                    sources.Add(InstallJsonKind.LiteLoader, jsonPath);
                })
            {
                ProgressWeight = 1d
            });
        LauncherCommon.Log($"[Install] 已为 {instanceName} 追加 LiteLoader 安装步骤");
    }

    private static GameDownload.DlLiteLoaderListEntry ResolveLiteLoaderEntry(
        GameDownload.DlLiteLoaderListEntry entry,
        string minecraftName)
    {
        if (entry.jsonToken is not null && !string.IsNullOrEmpty(entry.Inherit))
            return entry;

        var inherit = entry.Inherit ?? minecraftName;
        JsonObject? versions = null;
        foreach (var url in new[]
                 {
                     "https://dl.liteloader.com/versions/versions.json",
                     "https://bmclapi2.bangbang93.com/maven/com/mumfrey/liteloader/versions.json"
                 })
            try
            {
                versions = (JsonObject)Requester.FetchJson(url, RequestParam.WithRetry);
                if (versions["versions"] is JsonObject)
                    break;
            }
            catch (Exception ex)
            {
                LauncherCommon.Log(ex, $"[Install] 获取 LiteLoader 版本元数据失败：{url}");
            }

        var root = versions?["versions"] as JsonObject
            ?? throw new Exception(Lang.Text("Minecraft.Download.Error.LiteLoaderInstallFailed"));
        var token = root[inherit]
            ?? root[minecraftName]
            ?? root.FirstOrDefault(pair =>
                    pair.Key.StartsWithF(inherit) || inherit.StartsWithF(pair.Key))
                .Value;
        var realEntry = (token?["artefacts"] ?? token?["snapshots"])?[
                "com.mumfrey:liteloader"]?["latest"]
            ?? throw new Exception(
                Lang.Text(
                    "Minecraft.Download.Error.UnknownMinecraftVersion",
                    inherit));
        return new GameDownload.DlLiteLoaderListEntry
        {
            Inherit = inherit,
            IsLegacy = inherit.Split('.').Length > 1
                && double.TryParse(inherit.Split('.')[1], out var minor)
                && minor < 8d,
            IsPreview = realEntry["stream"]?.ToString()?.ToLower() == "snapshot",
            FileName = $"liteloader-installer-{inherit}-00-SNAPSHOT.jar",
            Md5 = (string?)realEntry["md5"],
            ReleaseTime = DateTime.Now,
            jsonToken = realEntry
        };
    }

    private static string CreateLiteLoaderJson(
        GameDownload.DlLiteLoaderListEntry entry,
        string minecraftName,
        string tempFolder)
    {
        var inherit = entry.Inherit ?? minecraftName;
        var versionName = $"{minecraftName}-LiteLoader";
        var versionFolder = Path.Combine(tempFolder, "versions", versionName);
        Directory.CreateDirectory(versionFolder);

        var libraries = entry.jsonToken?["libraries"]?.DeepClone() as JsonArray ?? new JsonArray();
        libraries.Add(
            new JsonObject
            {
                ["name"] = $"com.mumfrey:liteloader:{entry.jsonToken?["version"]}",
                ["url"] = "https://dl.liteloader.com/versions/"
            });
        var releaseDate = entry.ReleaseTime ?? DateTime.Now;
        var json = new JsonObject
        {
            ["id"] = versionName,
            ["time"] = releaseDate.ToString("O", CultureInfo.InvariantCulture),
            ["releaseTime"] = releaseDate.ToString("O", CultureInfo.InvariantCulture),
            ["type"] = "release",
            ["arguments"] = new JsonObject
            {
                ["game"] = new JsonArray
                {
                    (JsonNode)"--tweakClass",
                    (JsonNode)(entry.jsonToken?["tweakClass"]?.ToString()
                        ?? "com.mumfrey.liteloader.launch.LiteLoaderTweaker")
                }
            },
            ["libraries"] = libraries,
            ["mainClass"] = "net.minecraft.launchwrapper.Launch",
            ["minimumLauncherVersion"] = 18,
            ["inheritsFrom"] = inherit,
            ["jar"] = inherit
        };
        var jsonPath = Path.Combine(versionFolder, $"{versionName}.json");
        LauncherCommon.WriteFile(jsonPath, json.ToString(), new UTF8Encoding(false));
        LauncherCommon.Log($"[Install] 已生成 LiteLoader 版本 Json：{jsonPath}");
        return jsonPath;
    }

    #endregion

    #region 公共

    internal static JavaEntry SelectJava(
        Loading.LoaderBase task,
        Version javaVersion,
        bool enforceVersionRange)
    {
        lock (JavaRuntime.JavaLock)
        {
            var java = JavaRuntime.JavaSelect(
                Lang.Text("Minecraft.Download.Error.InstallationCanceled"),
                javaVersion,
                enforceVersionRange: enforceVersionRange);
            if (java is not null)
                return java;

            if (!JavaRuntime.JavaDownloadConfirm(
                    Lang.Text("Minecraft.Download.Error.JavaVersionRequired")))
                throw new Exception(
                    Lang.Text("Minecraft.Download.Error.JavaNotFoundInstallCanceled"));

            var downloadMajor = javaVersion.Major >= 9 ? javaVersion.Major : 8;
            var javaLoader = JavaRuntime.GetJavaDownloadLoader();
            try
            {
                javaLoader.Start(downloadMajor, true);
                while (javaLoader.State == LoadState.Loading
                       && task.State != LoadState.Aborted)
                    Thread.Sleep(10);
            }
            finally
            {
                javaLoader.Abort();
            }

            if (task.State == LoadState.Aborted)
                throw new CancelledException();
            java = JavaRuntime.JavaSelect(
                Lang.Text("Minecraft.Download.Error.InstallationCanceled"),
                javaVersion,
                enforceVersionRange: enforceVersionRange);
            return java
                ?? throw new Exception(
                    Lang.Text("Minecraft.Download.Error.JavaNotFoundInstallCanceled"));
        }
    }

    internal static List<string> RunInstallerProcess(
        Loading.LoaderBase task,
        ProcessStartInfo startInfo,
        string loaderName,
        string logPath,
        int timeoutMinutes)
    {
        var lines = new List<string>();
        using var process = new Process
        {
            StartInfo = startInfo
        };
        process.OutputDataReceived += (_, e) => AppendInstallerLine(lines, e.Data);
        process.ErrorDataReceived += (_, e) => AppendInstallerLine(lines, e.Data);
        LauncherCommon.Log(
            $"[Install] 开始运行 {loaderName} 安装器：{startInfo.FileName} {startInfo.Arguments}");
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        var deadline = DateTime.UtcNow.AddMinutes(timeoutMinutes);
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
                WriteLog(logPath, lines);
                throw new TimeoutException(
                    Lang.Text("Minecraft.Download.Error.LoaderInstallerRunFailed", loaderName));
            }

            Thread.Sleep(100);
        }

        process.WaitForExit();
        WriteLog(logPath, lines);
        if (process.ExitCode != 0)
            throw new Exception(
                Lang.Text(
                    "Minecraft.Download.Error.LoaderInstallerFailedLastLine",
                    loaderName,
                    GetLastLines(lines)));
        return lines;
    }

    private static void AppendInstallerLine(List<string> lines, string? line)
    {
        if (line is null)
            return;
        lock (lines)
        {
            lines.Add(line);
            if (lines.Count > 500)
                lines.RemoveAt(0);
        }

        LauncherCommon.Log("[Installer] " + line);
    }

    internal static string GetLastLines(IEnumerable<string> lines, int count = 20)
    {
        var text = string.Join("\r\n", lines.TakeLast(count));
        return string.IsNullOrWhiteSpace(text) ? "（安装器没有输出）" : text;
    }

    internal static void WriteLog(string logPath, IEnumerable<string> lines)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath) ?? ".");
            LauncherCommon.WriteFile(
                logPath,
                string.Join(Environment.NewLine, lines),
                new UTF8Encoding(false));
            LauncherCommon.Log($"[Install] 安装器日志：{logPath}");
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "写入安装器日志失败");
        }
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

    internal static string GetMcFolder(string instanceFolder)
    {
        var folder = Path.TrimEndingDirectorySeparator(instanceFolder);
        var versionsFolder = Path.GetDirectoryName(folder);
        return Path.GetDirectoryName(versionsFolder) ?? GameFolders.mcFolderSelected ?? "";
    }

    #endregion
}