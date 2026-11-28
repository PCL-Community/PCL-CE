using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.InteropServices;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.IO;
using PCL.Core.Minecraft.Java;
using PCL.Core.Minecraft.Java.Models;
using PCL.Core.Utils;
using PCL.Core.Utils.OS;
using PCL.Network;
using PCL.Network.Loaders;

namespace PCL;

public static partial class JavaRuntime
{
    private const string FoojayApi = "https://api.foojay.io/disco/v3.0";
    private const string MojangRuntimeIndex =
        "https://piston-meta.mojang.com/v1/products/java-runtime/2ec0cc96c44e5a76b9c8b7c39df7210883d12871/all.json";

    private static readonly HashSet<string> IgnoreHash =
    [
        "12976a6c2b227cbac58969c1455444596c894656",
        "c80e4bab46e34d02826eab226a4441d0970f2aba",
        "84d2102ad171863db04e7ee22a259d1f6c5de4a5"
    ];

    private static readonly object InstallLock = new();

    private sealed class JavaInstallPlan
    {
        public required string FinalDirectory { get; init; }
        public required string StagingDirectory { get; init; }
        public required string ContentDirectory { get; init; }
        public required List<DownloadFile> DownloadFiles { get; init; }
        public string? ArchivePath { get; init; }
        public string? ExistingJavaPath { get; init; }
        public List<MojangLink> Links { get; } = [];
        public List<string> ExecutableFiles { get; } = [];
    }

    private sealed record MojangLink(string Path, string Target);

    /// <summary>Prompts before automatically downloading Java for a launch or install flow.</summary>
    public static bool JavaDownloadConfirm(
        string versionDescription,
        bool forcedManualDownload = false)
    {
        if (!forcedManualDownload)
            return LauncherUi.Interaction?.ShowConfirm(
                    Lang.Text("Minecraft.Launch.Java.AutoDownload.Message", versionDescription),
                    Lang.Text("Minecraft.Launch.Java.AutoDownload.Title"),
                    Lang.Text("Minecraft.Launch.Java.AutoDownload.Action"),
                    Lang.Text("Common.Action.Cancel"))
                ?? false;

        LauncherUi.Interaction?.ShowMessageBox(
            Lang.Text("Minecraft.Launch.Java.NotFound.Manual.Message", versionDescription),
            Lang.Text("Minecraft.Launch.Java.NotFound.Title"));
        return false;
    }

    /// <summary>Fetches the Foojay distributions supported by the Java download UI.</summary>
    public static List<FoojayDistributionModel> GetFoojayDistributions()
    {
        var root = (JsonObject)Requester.FetchJson($"{FoojayApi}/distributions", RequestParam.WithRetry);
        var results = root["result"] as JsonArray
            ?? throw new InvalidDataException("Foojay distribution response has no result array");

        return results
            .OfType<JsonObject>()
            .Select(item => new FoojayDistributionModel
            {
                Name = item["name"]?.ToString() ?? "",
                ApiParameter = item["api_parameter"]?.ToString() ?? "",
                Vendor = item["vendor"]?.ToString() ?? "",
                Available = bool.TryParse(item["available"]?.ToString(), out var available)
                    && available,
                Versions = item["versions"] is JsonArray versions
                    ? versions.Select(version => version?.ToString() ?? "").ToArray()
                    : []
            })
            .Where(item => item.Available
                && (item.ApiParameter.Equals("liberica", StringComparison.OrdinalIgnoreCase)
                    || item.ApiParameter.Equals("zulu", StringComparison.OrdinalIgnoreCase)))
            .OrderBy(item => item.ApiParameter, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Returns the newest available GA package for each LTS major version.</summary>
    public static List<FoojayVersionModel> GetFoojayLtsPackages(string distribution)
    {
        var (operatingSystem, architecture, archiveType) = GetFoojayPlatform();
        var parameters = new List<KeyValuePair<string, string>>
        {
            new("distro", distribution),
            new("package_type", "jdk"),
            new("operating_system", operatingSystem),
            new("architecture", architecture),
            new("archive_type", archiveType),
            new("release_status", "ga"),
            new("term_of_support", "lts"),
            new("directly_downloadable", "true"),
            new("javafx_bundled", "false"),
            new("latest", "available")
        };
        if (OperatingSystem.IsLinux())
            parameters.Add(new KeyValuePair<string, string>("lib_c_type", "glibc"));

        var query = string.Join(
            "&",
            parameters.Select(parameter =>
                $"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value)}"));
        var root = (JsonObject)Requester.FetchJson($"{FoojayApi}/packages?{query}", RequestParam.WithRetry);
        var packages = root["result"]?.Deserialize<FoojayVersionModel[]>(JsonCompat.SerializerOptions)
            ?? [];

        return packages
            .Where(package => package.MajorVersion >= 8
                && package.LatestBuildAvailable
                && package.DirectlyDownloadable
                && package.TermOfSupport.Equals("lts", StringComparison.OrdinalIgnoreCase)
                && package.ReleaseStatus.Equals("ga", StringComparison.OrdinalIgnoreCase))
            .GroupBy(package => package.MajorVersion)
            .Select(group => group.First())
            .OrderByDescending(package => package.MajorVersion)
            .ToList();
    }

    /// <summary>Lists the Mojang runtime components available for this operating system.</summary>
    public static List<MojangJavaVersionOption> GetMojangJavaVersions()
    {
        return GetJavaRuntimeComponents()
            .Select(component =>
            {
                var first = component.Value?.AsArray().FirstOrDefault();
                var versionName = first?["version"]?["name"]?.ToString();
                return versionName is null
                    ? null
                    : new MojangJavaVersionOption(
                        component.Key,
                        versionName,
                        ParseJavaMajorVersion(versionName));
            })
            .Where(option => option is not null && option.MajorVersion >= 8)
            .Cast<MojangJavaVersionOption>()
            .OrderByDescending(option => option.MajorVersion)
            .ThenByDescending(option => option.VersionName, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Creates a taskbar loader for a user-selected Mojang or Foojay Java package.</summary>
    public static Loading.LoaderCombo<JavaDownloadSelection> GetJavaInstallLoader(
        JavaDownloadSelection selection)
    {
        JavaInstallPlan? plan = null;
        var majorVersion = selection.Source switch
        {
            JavaDownloadSource.Foojay => selection.FoojayPackage?.MajorVersion,
            JavaDownloadSource.Mojang => selection.MojangVersion?.MajorVersion,
            _ => null
        } ?? throw new InvalidOperationException("The selected Java major version is missing");
        var distribution = selection.DistributionName
            ?? throw new InvalidOperationException("The selected Java distribution name is missing");

        return CreateJavaInstallLoader<JavaDownloadSelection>(
            Lang.Text("Setup.Java.Download.Task.Title", majorVersion, distribution),
            Lang.Text("Setup.Java.Download.Task.Prepare"),
            Lang.Text("Setup.Java.Download.Task.Download", distribution, majorVersion),
            Lang.Text("Setup.Java.Download.Task.Install"),
            task =>
            {
                plan = BuildJavaInstallPlan(
                    task.input ?? throw new InvalidOperationException("Java download selection is empty"));
                task.output = plan.DownloadFiles;
            },
            () => plan);
    }

    /// <summary>Gets the Java download loader used by launch and installation flows.</summary>
    public static Loading.LoaderCombo<string> GetJavaDownloadLoader()
    {
        JavaInstallPlan? plan = null;
        return CreateJavaInstallLoader<string>(
            Lang.Text("Minecraft.Launch.Java.Task.Download"),
            Lang.Text("Minecraft.Launch.Java.Task.GetDownloadInfo"),
            Lang.Text("Minecraft.Launch.Java.Task.DownloadFiles"),
            Lang.Text("Setup.Java.Download.Task.Install"),
            task =>
            {
                var requestedVersion = task.input?.ToString()
                    ?? throw new InvalidOperationException("Java version input is empty");
                plan = BuildMojangPlan(requestedVersion);
                task.output = plan.DownloadFiles;
            },
            () => plan);
    }

    private static Loading.LoaderCombo<InputType> CreateJavaInstallLoader<InputType>(
        string loaderName,
        string prepareName,
        string downloadName,
        string installName,
        Action<Loading.LoaderTask<InputType, List<DownloadFile>>> prepare,
        Func<JavaInstallPlan?> getPlan)
    {
        var prepareLoader = new Loading.LoaderTask<InputType, List<DownloadFile>>(
            prepareName,
            prepare)
        {
            ProgressWeight = 2d
        };
        var downloadLoader = new LoaderDownload(downloadName, [])
        {
            ProgressWeight = 10d
        };
        var installLoader = new Loading.LoaderTask<List<DownloadFile>, bool>(
            installName,
            task =>
            {
                InstallJavaPlan(
                    getPlan() ?? throw new InvalidOperationException("Java install plan is missing"));
                task.output = true;
            })
        {
            ProgressWeight = 2d
        };

        var loader = new Loading.LoaderCombo<InputType>(
            loaderName,
            [prepareLoader, downloadLoader, installLoader]);
        loader.OnStateChangedThread += (_, state, _) =>
        {
            if (state is LoadState.Failed or LoadState.Aborted && getPlan() is { } plan)
                DeleteStagingDirectory(plan.StagingDirectory);
        };
        loader.hasOnStateChangedThread = true;
        return loader;
    }

    private static JavaInstallPlan BuildJavaInstallPlan(JavaDownloadSelection selection)
    {
        return selection.Source switch
        {
            JavaDownloadSource.Foojay when selection.FoojayPackage is { } package
                => BuildFoojayPlan(package),
            JavaDownloadSource.Mojang when selection.MojangVersion is { } version
                => BuildMojangPlan(version.ComponentName),
            _ => throw new InvalidOperationException("The selected Java package is incomplete")
        };
    }

    private static JavaInstallPlan BuildFoojayPlan(FoojayVersionModel package)
    {
        var safeVersion = SafePathSegment(package.JavaVersion);
        var safeArchitecture = SafePathSegment(package.Architecture);
        var finalDirectory = Path.Combine(
            GetJavaInstallRoot(),
            $"{SafePathSegment(package.Distribution)}-{package.MajorVersion}-{safeVersion}-{safeArchitecture}");
        if (FindJavaExecutable(finalDirectory) is { } existingJava)
            return CreateExistingPlan(finalDirectory, existingJava);
        if (Directory.Exists(finalDirectory))
            throw new IOException($"Java installation folder already exists but is incomplete: {finalDirectory}");

        var infoRoot = (JsonObject)Requester.FetchJson(package.Links.Info, RequestParam.WithRetry);
        var infoNode = (infoRoot["result"] as JsonArray)?.FirstOrDefault()
            ?? infoRoot["result"]
            ?? infoRoot;
        var packageInfo = infoNode.Deserialize<FoojayPackageInfoModel>(JsonCompat.SerializerOptions);
        var downloadUrl = packageInfo?.DirectDownloadUri;
        if (string.IsNullOrWhiteSpace(downloadUrl))
            downloadUrl = package.Links.DownloadRedirect;
        if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var downloadUri)
            || !downloadUri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Foojay returned an invalid Java download URL");
        var checksum = packageInfo?.Checksum;
        if (string.IsNullOrWhiteSpace(checksum)
            || checksum.Length != 64
            || !checksum.All(Uri.IsHexDigit)
            || !string.Equals(packageInfo?.ChecksumType, "sha256", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Foojay did not provide a valid SHA-256 checksum for Java");

        var stagingDirectory = CreateStagingDirectory();
        try
        {
            var archiveName = SafePathSegment(package.Filename);
            var archivePath = Path.Combine(stagingDirectory, archiveName);
            var downloadFile = new DownloadFile(
                [downloadUrl],
                archivePath,
                new FileChecker(actualSize: package.Size, hash: checksum));

            return new JavaInstallPlan
            {
                FinalDirectory = finalDirectory,
                StagingDirectory = stagingDirectory,
                ContentDirectory = Path.Combine(stagingDirectory, "content"),
                ArchivePath = archivePath,
                DownloadFiles = [downloadFile]
            };
        }
        catch
        {
            DeleteStagingDirectory(stagingDirectory);
            throw;
        }
    }

    private static JavaInstallPlan BuildMojangPlan(string requestedVersion)
    {
        var components = GetJavaRuntimeComponents();
        var (targetName, targetComponent) = SelectJavaRuntimeComponent(components, requestedVersion);
        var versionName = targetComponent["version"]?["name"]?.ToString() ?? targetName;
        var finalDirectory = Path.Combine(
            GetJavaInstallRoot(),
            $"mojang-{SafePathSegment(targetName)}-{SafePathSegment(versionName)}-{GetMojangRuntimePlatformKey()}");
        if (FindJavaExecutable(finalDirectory) is { } existingJava)
            return CreateExistingPlan(finalDirectory, existingJava);
        if (Directory.Exists(finalDirectory))
            throw new IOException($"Java installation folder already exists but is incomplete: {finalDirectory}");

        var address = targetComponent["manifest"]?["url"]?.ToString()
            ?? throw new InvalidDataException($"Java {targetName} is missing its manifest URL");
        var manifest = FetchJavaRuntimeManifest(address);
        var stagingDirectory = CreateStagingDirectory();
        try
        {
            var contentDirectory = Path.Combine(stagingDirectory, "content");
            var plan = new JavaInstallPlan
            {
                FinalDirectory = finalDirectory,
                StagingDirectory = stagingDirectory,
                ContentDirectory = contentDirectory,
                DownloadFiles = []
            };
            plan.DownloadFiles.AddRange(CreateMojangDownloadFiles(manifest, contentDirectory, plan));
            return plan;
        }
        catch
        {
            DeleteStagingDirectory(stagingDirectory);
            throw;
        }
    }

    private static JavaInstallPlan CreateExistingPlan(string finalDirectory, string javaPath)
    {
        return new JavaInstallPlan
        {
            FinalDirectory = finalDirectory,
            StagingDirectory = "",
            ContentDirectory = finalDirectory,
            ExistingJavaPath = javaPath,
            DownloadFiles = []
        };
    }

    private static void InstallJavaPlan(JavaInstallPlan plan)
    {
        lock (InstallLock)
        {
            if (plan.ExistingJavaPath is { } existingJava)
            {
                RegisterInstalledJava(existingJava);
                return;
            }

            var movedToFinalDirectory = false;
            try
            {
                if (Directory.Exists(plan.FinalDirectory))
                {
                    if (FindJavaExecutable(plan.FinalDirectory) is { } concurrentJava)
                    {
                        RegisterInstalledJava(concurrentJava);
                        return;
                    }

                    throw new IOException(
                        $"Java installation folder already exists but is incomplete: {plan.FinalDirectory}");
                }

                if (plan.ArchivePath is { } archivePath)
                {
                    Directory.CreateDirectory(plan.ContentDirectory);
                    ExtractFoojayArchive(archivePath, plan.ContentDirectory);
                    SetFoojayUnixExecutables(plan.ContentDirectory);
                }
                else
                {
                    CreateMojangLinks(plan);
                    SetMojangUnixExecutables(plan);
                }

                var javaPath = FindJavaExecutable(plan.ContentDirectory)
                    ?? throw new InvalidDataException("The downloaded package does not contain a Java executable");
                EnsureUnixExecutable(javaPath);
                var relativeJavaPath = Path.GetRelativePath(plan.ContentDirectory, javaPath);

                Directory.CreateDirectory(Path.GetDirectoryName(plan.FinalDirectory)!);
                Directory.Move(plan.ContentDirectory, plan.FinalDirectory);
                movedToFinalDirectory = true;

                var installedJava = Path.Combine(plan.FinalDirectory, relativeJavaPath);
                if (FindJavaExecutable(plan.FinalDirectory) is null)
                    throw new InvalidDataException("The Java executable is missing after installation");
                RegisterInstalledJava(installedJava);
            }
            catch
            {
                if (movedToFinalDirectory)
                    DeleteStagingDirectory(plan.FinalDirectory);
                throw;
            }
            finally
            {
                DeleteStagingDirectory(plan.StagingDirectory);
            }
        }
    }

    private static void RegisterInstalledJava(string javaPath)
    {
        var entry = Javas.AddOrGet(javaPath)
            ?? throw new InvalidDataException("Java could not be parsed after installation");
        entry.Source = JavaSource.AutoInstalled;
        Javas.SaveConfig();
    }

    private static List<DownloadFile> CreateMojangDownloadFiles(
        JsonObject manifest,
        string contentDirectory,
        JavaInstallPlan plan)
    {
        var manifestFiles = manifest["files"]?.AsObject()
            ?? throw new InvalidDataException("Java runtime manifest has no files object");
        var results = new List<DownloadFile>(manifestFiles.Count);

        foreach (var file in manifestFiles)
        {
            var data = file.Value?.AsObject();
            if (data is null) continue;

            var localPath = GetSafeJavaPath(contentDirectory, file.Key);
            if (data["type"]?.ToString().Equals("link", StringComparison.OrdinalIgnoreCase) == true)
            {
                var target = data["target"]?.ToString()
                    ?? throw new InvalidDataException($"Mojang runtime link {file.Key} has no target");
                plan.Links.Add(new MojangLink(localPath, target));
                continue;
            }

            var rawDownloads = data["downloads"]?["raw"];
            if (rawDownloads is null) continue;

            var info = rawDownloads.AsObject();
            var sha1 = info["sha1"]?.ToString()
                ?? throw new InvalidDataException($"Java runtime file {file.Key} has no SHA-1");
            if (IgnoreHash.Contains(sha1)) continue;

            var size = info["size"]?.GetValue<long>()
                ?? throw new InvalidDataException($"Java runtime file {file.Key} has no size");
            var url = info["url"]?.ToString()
                ?? throw new InvalidDataException($"Java runtime file {file.Key} has no URL");

            if (bool.TryParse(data["executable"]?.ToString(), out var executable) && executable)
                plan.ExecutableFiles.Add(localPath);

            results.Add(new DownloadFile(
                GameDownload.DlSourceOrder(
                    [url],
                    [url.Replace("piston-data.mojang.com", "bmclapi2.bangbang93.com")]),
                localPath,
                new FileChecker(actualSize: size, hash: sha1)));
        }

        return results;
    }

    private static void CreateMojangLinks(JavaInstallPlan plan)
    {
        foreach (var link in plan.Links)
        {
            if (Path.IsPathRooted(link.Target))
                throw new InvalidDataException($"Mojang runtime link target is absolute: {link.Target}");

            var parent = Path.GetDirectoryName(link.Path)
                ?? throw new InvalidDataException($"Mojang runtime link has no parent: {link.Path}");
            var resolvedTarget = Path.GetFullPath(Path.Combine(parent, link.Target));
            if (!Files.IsPathWithinDirectory(resolvedTarget, plan.ContentDirectory))
                throw new InvalidDataException($"Mojang runtime link target escapes its install folder: {link.Target}");

            Directory.CreateDirectory(parent);
            if (File.Exists(link.Path) || Directory.Exists(link.Path))
                continue;

            var targetIsDirectory = Directory.Exists(resolvedTarget);
            if (OperatingSystem.IsWindows() && targetIsDirectory)
                Directory.CreateSymbolicLink(link.Path, link.Target);
            else
                File.CreateSymbolicLink(link.Path, link.Target);
        }
    }

    private static void SetMojangUnixExecutables(JavaInstallPlan plan)
    {
        foreach (var file in plan.ExecutableFiles)
            EnsureUnixExecutable(file);
    }

    private static void ExtractFoojayArchive(string archivePath, string destinationDirectory)
    {
        if (archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            ZipFile.ExtractToDirectory(archivePath, destinationDirectory, true);
            return;
        }

        if (archivePath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase)
            || archivePath.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
        {
            using var archive = File.OpenRead(archivePath);
            using var gzip = new GZipStream(archive, CompressionMode.Decompress);
            TarFile.ExtractToDirectory(gzip, destinationDirectory, true);
            return;
        }

        throw new NotSupportedException($"Unsupported Java archive format: {archivePath}");
    }

    private static void SetFoojayUnixExecutables(string contentDirectory)
    {
        if (OperatingSystem.IsWindows()) return;

        foreach (var file in Directory.EnumerateFiles(contentDirectory, "*", SearchOption.AllDirectories))
        {
            var parentName = Path.GetFileName(Path.GetDirectoryName(file) ?? "");
            var fileName = Path.GetFileName(file);
            if ((parentName.Equals("bin", StringComparison.OrdinalIgnoreCase)
                    && string.IsNullOrEmpty(Path.GetExtension(fileName)))
                || fileName.Equals("jspawnhelper", StringComparison.OrdinalIgnoreCase))
                EnsureUnixExecutable(file);
        }
    }

    private static void EnsureUnixExecutable(string path)
    {
        if (OperatingSystem.IsWindows() || !File.Exists(path)) return;
        try
        {
            var mode = File.GetUnixFileMode(path);
            File.SetUnixFileMode(
                path,
                mode | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
        }
        catch (IOException)
        {
            // Symlinks and mounted filesystems may not expose changeable mode bits.
        }
        catch (UnauthorizedAccessException)
        {
            // Java will report a useful launch error if its executable cannot be changed.
        }
    }

    private static string? FindJavaExecutable(string directory)
    {
        if (!Directory.Exists(directory)) return null;
        var executableName = JavaExecutableLayout.JavaExecutableName;
        try
        {
            return Directory
                .EnumerateFiles(directory, executableName, SearchOption.AllDirectories)
                .FirstOrDefault(path => Path.GetFileName(Path.GetDirectoryName(path) ?? "")
                    .Equals("bin", StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string GetJavaInstallRoot()
    {
        var root = Path.Combine(Paths.SharedData, "java");
        Directory.CreateDirectory(root);
        return root;
    }

    private static string CreateStagingDirectory()
    {
        var stagingDirectory = Path.Combine(GetJavaInstallRoot(), ".staging", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingDirectory);
        return stagingDirectory;
    }

    private static void DeleteStagingDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return;
        try
        {
            Directories.DeleteDirectory(directory);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "清理 Java 暂存目录失败", LauncherLogLevel.Developer);
        }
    }

    private static string SafePathSegment(string value)
    {
        var safe = new string(value
            .Where(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.')
            .ToArray());
        safe = safe.Trim('.', ' ');
        return string.IsNullOrEmpty(safe) ? "java" : safe;
    }

    private static string GetSafeJavaPath(string baseDirectory, string relativePath)
    {
        var path = Path.GetFullPath(Path.Combine(baseDirectory, relativePath));
        if (!Files.IsPathWithinDirectory(path, baseDirectory))
            throw new InvalidDataException($"Java runtime path escapes its install folder: {relativePath}");
        return path;
    }

    private static (string OperatingSystem, string Architecture, string ArchiveType) GetFoojayPlatform()
    {
        var operatingSystem = OperatingSystem.IsWindows()
            ? "windows"
            : OperatingSystem.IsLinux()
                ? "linux"
                : OperatingSystem.IsMacOS()
                    ? "macos"
                    : throw new PlatformNotSupportedException("Foojay Java downloads are unavailable on this OS");

        var architecture = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "aarch64",
            Architecture.Arm => "arm",
            _ => throw new PlatformNotSupportedException("Foojay Java downloads are unavailable for this CPU architecture")
        };
        return (operatingSystem, architecture, OperatingSystem.IsWindows() ? "zip" : "tar.gz");
    }

    private static JsonObject GetJavaRuntimeComponents()
    {
        var indexFile = NetFacade.NetGetCodeByLoader(
            GameDownload.DlVersionListOrder(
                [MojangRuntimeIndex],
                [MojangRuntimeIndex.Replace(
                    "piston-meta.mojang.com",
                    "bmclapi2.bangbang93.com")]),
            isJson: true);
        var platformKey = GetMojangRuntimePlatformKey();
        return ((JsonObject)LauncherCommon.GetJson(indexFile))[platformKey] as JsonObject
            ?? throw new InvalidDataException($"Mojang does not provide Java runtimes for {platformKey}");
    }

    private static string GetMojangRuntimePlatformKey()
    {
        var architecture = RuntimeInformation.OSArchitecture;
        if (OperatingSystem.IsWindows())
            return architecture switch
            {
                Architecture.Arm64 => "windows-arm64",
                Architecture.X86 => "windows-x86",
                _ => "windows-x64"
            };

        if (OperatingSystem.IsLinux())
            return architecture switch
            {
                Architecture.Arm64 => "linux-arm64",
                Architecture.X86 => "linux-i386",
                _ => "linux"
            };

        if (OperatingSystem.IsMacOS())
            return architecture == Architecture.Arm64 ? "mac-os-arm64" : "mac-os";

        throw new PlatformNotSupportedException("Mojang Java downloads are unavailable on this OS");
    }

    private static (string Name, JsonNode Component) SelectJavaRuntimeComponent(
        JsonObject components,
        string requestedName)
    {
        if (components[requestedName] is { } exact)
        {
            var first = exact.AsArray().FirstOrDefault();
            return first is null
                ? throw new InvalidDataException($"Mojang has no Java {requestedName} runtime")
                : (requestedName, first);
        }

        var requestedMajor = int.TryParse(requestedName, out var major) ? major : (int?)null;
        var match = components.FirstOrDefault(component =>
        {
            var versionName = component.Value?.AsArray().FirstOrDefault()?[
                "version"]?["name"]?.ToString();
            if (versionName is null) return false;
            return requestedMajor is { } requested
                ? ParseJavaMajorVersion(versionName) == requested
                : versionName.StartsWith(requestedName, StringComparison.OrdinalIgnoreCase);
        });

        if (match.Value is null)
            throw new InvalidDataException($"Mojang does not provide Java {requestedName}");
        var target = match.Value.AsArray().FirstOrDefault()
            ?? throw new InvalidDataException($"Mojang has no Java {requestedName} runtime");
        return (match.Key, target);
    }

    private static JsonObject FetchJavaRuntimeManifest(string address)
    {
        var manifestUrl = GameDownload
            .DlSourceOrder(
                [address],
                [address.Replace("piston-meta.mojang.com", "bmclapi2.bangbang93.com")])
            .First();
        return (JsonObject)Requester.FetchJson(manifestUrl, RequestParam.WithRetry);
    }

    private static int ParseJavaMajorVersion(string version)
    {
        var parts = version.Split(['.', '+', '_', '-'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || !int.TryParse(parts[0], out var major)) return 0;
        if (major == 1 && parts.Length > 1 && int.TryParse(parts[1], out var legacyMajor))
            return legacyMajor;
        return major;
    }
}
