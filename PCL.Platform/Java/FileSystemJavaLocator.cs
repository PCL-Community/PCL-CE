// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Frozen;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;
using PCL.Domain.Minecraft.Java;
using PCL.Platform.Abstractions.Java;

namespace PCL.Platform.Java;

public sealed class FileSystemJavaLocator : IJavaLocator
{
    private static readonly FrozenDictionary<string, JavaBrand> BrandMap =
        new Dictionary<string, JavaBrand>(StringComparer.OrdinalIgnoreCase)
        {
            ["adoptium"] = JavaBrand.EclipseTemurin,
            ["eclipse"] = JavaBrand.EclipseTemurin,
            ["temurin"] = JavaBrand.EclipseTemurin,
            ["bellsoft"] = JavaBrand.Liberica,
            ["liberica"] = JavaBrand.Liberica,
            ["azul"] = JavaBrand.Zulu,
            ["zulu"] = JavaBrand.Zulu,
            ["amazon"] = JavaBrand.Corretto,
            ["corretto"] = JavaBrand.Corretto,
            ["microsoft"] = JavaBrand.Microsoft,
            ["ibm"] = JavaBrand.IBMSemeru,
            ["semeru"] = JavaBrand.IBMSemeru,
            ["oracle"] = JavaBrand.Oracle,
            ["alibaba"] = JavaBrand.Dragonwell,
            ["dragonwell"] = JavaBrand.Dragonwell,
            ["tencent"] = JavaBrand.TencentKona,
            ["kona"] = JavaBrand.TencentKona,
            ["openjdk"] = JavaBrand.OpenJDK,
            ["graalvm"] = JavaBrand.GraalVmCommunity,
            ["jetbrains"] = JavaBrand.JetBrains
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private readonly IReadOnlyList<string>? _searchRoots;

    public FileSystemJavaLocator(IEnumerable<string>? searchRoots = null)
    {
        _searchRoots = searchRoots?.Where(static root => !string.IsNullOrWhiteSpace(root)).ToArray();
    }

    public ValueTask<IReadOnlyList<JavaRuntimeCandidate>> FindAllAsync(CancellationToken cancellationToken)
    {
        Dictionary<string, JavaRuntimeCandidate> candidates = new(GetPathComparer());

        foreach (string javaHome in EnumerateJavaHomes(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            JavaRuntimeCandidate? candidate = TryCreateCandidate(javaHome);
            if (candidate is null)
                continue;

            // Prefer a real JDK home over a PATH/javapath shim for the same major version path key.
            string key = candidate.Installation.JavaExecutablePath;
            if (candidates.TryGetValue(key, out JavaRuntimeCandidate? existing))
            {
                if (IsBetterCandidate(candidate, existing))
                    candidates[key] = candidate;
                continue;
            }

            candidates[key] = candidate;
        }

        // Also index by java home so shim + real home don't both win confusingly later.
        return ValueTask.FromResult<IReadOnlyList<JavaRuntimeCandidate>>(
            candidates.Values
                .GroupBy(static c => Path.GetFullPath(c.Installation.JavaHome), GetPathComparer())
                .Select(static group => group.OrderByDescending(static c => c.Installation.MajorVersion)
                    .ThenBy(static c => c.Installation.IsJre)
                    .First())
                .ToArray());
    }

    private static bool IsBetterCandidate(JavaRuntimeCandidate candidate, JavaRuntimeCandidate existing)
    {
        if (candidate.Installation.MajorVersion != existing.Installation.MajorVersion)
            return candidate.Installation.MajorVersion > existing.Installation.MajorVersion;
        if (candidate.Installation.IsJre != existing.Installation.IsJre)
            return !candidate.Installation.IsJre && existing.Installation.IsJre;
        return candidate.Installation.Version.CompareTo(existing.Installation.Version) > 0;
    }

    private IEnumerable<string> EnumerateJavaHomes(CancellationToken cancellationToken)
    {
        IEnumerable<string> roots = _searchRoots ?? EnumerateDefaultSearchRoots();
        foreach (string root in roots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (string javaHome in ExpandRoot(root))
                yield return javaHome;
        }
    }

    private static IEnumerable<string> EnumerateDefaultSearchRoots()
    {
        string? javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrWhiteSpace(javaHome))
            yield return javaHome;

        foreach (string pathEntry in EnumeratePathEntries())
            yield return pathEntry;

        // PCL-managed Mojang runtimes downloaded by the launcher.
        foreach (string runtimeRoot in EnumerateManagedRuntimeRoots())
            yield return runtimeRoot;

        if (OperatingSystem.IsWindows())
        {
            foreach (string registryHome in EnumerateWindowsRegistryJavaHomes())
                yield return registryHome;

            foreach (string root in EnumerateExisting(
                         Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                         Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                         @"D:\Program Files",
                         @"D:\Program Files (x86)",
                         @"C:\Program Files",
                         @"C:\Program Files (x86)"))
            {
                yield return Path.Combine(root, "Java");
                yield return Path.Combine(root, "Eclipse Adoptium");
                yield return Path.Combine(root, "Microsoft");
                yield return Path.Combine(root, "Zulu");
                yield return Path.Combine(root, "BellSoft");
                yield return Path.Combine(root, "Amazon Corretto");
                yield return Path.Combine(root, "Common Files", "Oracle", "Java");
            }
        }
        else if (OperatingSystem.IsMacOS())
        {
            yield return "/Library/Java/JavaVirtualMachines";
            yield return Path.Combine(GetHomeDirectory(), "Library", "Java", "JavaVirtualMachines");
            yield return "/opt/homebrew/opt/openjdk";
            yield return "/usr/local/opt/openjdk";
        }
        else
        {
            yield return "/usr/lib/jvm";
            yield return "/usr/java";
            yield return "/opt/java";
            yield return "/opt/jdk";
            yield return Path.Combine(GetHomeDirectory(), ".sdkman", "candidates", "java");
        }
    }

    [SupportedOSPlatform("windows")]
    private static IEnumerable<string> EnumerateWindowsRegistryJavaHomes()
    {
        List<string> homes = [];
        foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using RegistryKey machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                CollectRegistryJavaHomes(machine, @"SOFTWARE\JavaSoft", homes, depth: 3);
                CollectRegistryJavaHomes(machine, @"SOFTWARE\Eclipse Adoptium", homes, depth: 5);
                CollectRegistryJavaHomes(machine, @"SOFTWARE\Microsoft\JDK", homes, depth: 3);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or global::System.Security.SecurityException or IOException)
            {
                // A locked vendor key must not prevent the remaining discovery sources from running.
            }
        }

        return homes.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    [SupportedOSPlatform("windows")]
    private static void CollectRegistryJavaHomes(
        RegistryKey parent,
        string subKeyPath,
        List<string> homes,
        int depth)
    {
        using RegistryKey? key = parent.OpenSubKey(subKeyPath);
        if (key is null)
            return;

        CollectRegistryJavaHomes(key, homes, depth);
    }

    [SupportedOSPlatform("windows")]
    private static void CollectRegistryJavaHomes(RegistryKey key, List<string> homes, int depth)
    {
        if (key.GetValue("JavaHome") is string javaHome && !string.IsNullOrWhiteSpace(javaHome))
            homes.Add(javaHome);
        if (depth <= 0)
            return;

        foreach (string subKeyName in key.GetSubKeyNames())
        {
            try
            {
                using RegistryKey? child = key.OpenSubKey(subKeyName);
                if (child is not null)
                    CollectRegistryJavaHomes(child, homes, depth - 1);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or global::System.Security.SecurityException or IOException)
            {
                // Continue with other vendor/version keys.
            }
        }
    }

    private static IEnumerable<string> ExpandRoot(string root)
    {
        string normalizedRoot;
        try
        {
            normalizedRoot = Path.GetFullPath(root);
        }
        catch (Exception) when (IsPathException())
        {
            yield break;
        }

        string? directHome = ResolveJavaHome(normalizedRoot);
        if (directHome is not null)
            yield return directHome;

        if (!Directory.Exists(normalizedRoot))
            yield break;

        foreach (string child in SafeEnumerateDirectories(normalizedRoot))
        {
            string? childHome = ResolveJavaHome(child);
            if (childHome is not null)
                yield return childHome;

            // One extra level for vendor trees like Program Files\Java\jdk-21.0.10
            // and Program Files\Eclipse Adoptium\jdk-17.0.x-hotspot.
            foreach (string grandChild in SafeEnumerateDirectories(child))
            {
                string? grandChildHome = ResolveJavaHome(grandChild);
                if (grandChildHome is not null)
                    yield return grandChildHome;
            }
        }
    }

    private static string? ResolveJavaHome(string path)
    {
        if (File.Exists(path) && IsJavaExecutableName(Path.GetFileName(path)))
        {
            string? directory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory))
                return null;

            // .../bin/java(.exe)
            if (string.Equals(Path.GetFileName(directory), "bin", StringComparison.OrdinalIgnoreCase))
            {
                string? home = Directory.GetParent(directory)?.FullName;
                if (!string.IsNullOrWhiteSpace(home) && File.Exists(GetJavaExecutablePath(home)))
                    return Path.GetFullPath(home);
            }

            // Oracle javapath shim directory: treat as non-home; TryCreateCandidate will probe -version.
            return Path.GetFullPath(directory);
        }

        if (Directory.Exists(path))
        {
            string macBundleHome = Path.Combine(path, "Contents", "Home");
            if (Directory.Exists(macBundleHome) && File.Exists(GetJavaExecutablePath(macBundleHome)))
                return Path.GetFullPath(macBundleHome);

            if (File.Exists(GetJavaExecutablePath(path)))
                return Path.GetFullPath(path);

            // PATH entry pointing at bin/
            string parent = Directory.GetParent(path)?.FullName ?? string.Empty;
            if (string.Equals(Path.GetFileName(path), "bin", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(parent) &&
                File.Exists(Path.Combine(path, GetJavaExecutableName())))
            {
                return Path.GetFullPath(parent);
            }

            // Directory that directly contains java.exe (javapath).
            if (File.Exists(Path.Combine(path, GetJavaExecutableName())))
                return Path.GetFullPath(path);
        }

        return null;
    }

    private static JavaRuntimeCandidate? TryCreateCandidate(string javaHome)
    {
        string javaExecutable = File.Exists(GetJavaExecutablePath(javaHome))
            ? GetJavaExecutablePath(javaHome)
            : Path.Combine(javaHome, GetJavaExecutableName());
        if (!File.Exists(javaExecutable))
            return null;

        Dictionary<string, string> release = ReadReleaseFile(Path.Combine(javaHome, "release"));
        // Mojang runtime layout: .../runtime/<component>/<platform>/<component>
        if (release.Count == 0)
        {
            string nestedRelease = Path.Combine(javaHome, Path.GetFileName(javaHome), "release");
            if (File.Exists(nestedRelease))
                release = ReadReleaseFile(nestedRelease);
        }

        Version parsedVersion;
        if (!TryParseVersion(GetReleaseValue(release, "JAVA_VERSION"), out Version? version) || version is null)
        {
            if (!TryProbeJavaVersion(javaExecutable, out parsedVersion))
                parsedVersion = new Version(0, 0, 0, 0);
        }
        else
        {
            parsedVersion = version;
        }

        // Skip unusable zero-version shims that could not be probed.
        if (parsedVersion.Major == 0 && parsedVersion.Minor == 0)
            return null;

        JavaArchitecture architecture = ParseArchitecture(GetReleaseValue(release, "OS_ARCH"));
        bool isJre = !File.Exists(Path.Combine(javaHome, "bin", GetJavacExecutableName())) &&
                     !File.Exists(Path.Combine(javaHome, GetJavacExecutableName()));
        string resolvedHome = File.Exists(GetJavaExecutablePath(javaHome))
            ? Path.GetFullPath(javaHome)
            : Path.GetFullPath(javaHome);
        string resolvedExecutable = Path.GetFullPath(javaExecutable);
        string? windowed = OperatingSystem.IsWindows()
            ? (GetWindowedJavaExecutablePath(resolvedHome) ??
               (File.Exists(Path.Combine(resolvedHome, "javaw.exe"))
                   ? Path.GetFullPath(Path.Combine(resolvedHome, "javaw.exe"))
                   : null))
            : null;

        JavaInstallation installation = new(
            resolvedHome,
            resolvedExecutable,
            windowed,
            parsedVersion,
            ParseBrand(GetReleaseValue(release, "IMPLEMENTOR") ?? resolvedHome),
            architecture,
            architecture is JavaArchitecture.X64 or JavaArchitecture.Arm64 or JavaArchitecture.Unknown,
            isJre);

        return new JavaRuntimeCandidate(installation, Source: JavaSource.AutoScanned);
    }

    private static bool TryProbeJavaVersion(string javaExecutable, out Version version)
    {
        version = new Version(0, 0, 0, 0);
        try
        {
            using Process process = new()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = javaExecutable,
                    Arguments = "-version",
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            if (!process.Start())
                return false;

            string stderr = process.StandardError.ReadToEnd();
            string stdout = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(4000))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
                return false;
            }

            string text = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
            // openjdk version "21.0.2"  OR  java version "1.8.0_202"
            foreach (string line in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                int firstQuote = line.IndexOf('"');
                int secondQuote = firstQuote >= 0 ? line.IndexOf('"', firstQuote + 1) : -1;
                if (firstQuote < 0 || secondQuote <= firstQuote)
                    continue;

                string token = line[(firstQuote + 1)..secondQuote];
                if (TryParseVersion(token, out Version? parsed) && parsed is not null)
                {
                    version = parsed;
                    return version.Major > 0 || version.Minor > 0;
                }
            }
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException or InvalidOperationException or
            global::System.ComponentModel.Win32Exception)
        {
            return false;
        }

        return false;
    }

    private static Dictionary<string, string> ReadReleaseFile(string releaseFile)
    {
        Dictionary<string, string> values = new(StringComparer.Ordinal);
        if (!File.Exists(releaseFile))
            return values;

        foreach (string line in File.ReadLines(releaseFile))
        {
            int equalsIndex = line.IndexOf('=');
            if (equalsIndex <= 0)
                continue;

            string key = line[..equalsIndex].Trim();
            string value = line[(equalsIndex + 1)..].Trim().Trim('"');
            values[key] = value;
        }

        return values;
    }

    private static string? GetReleaseValue(Dictionary<string, string> values, string key) =>
        values.TryGetValue(key, out string? value) ? value : null;

    private static JavaBrand ParseBrand(string? implementor)
    {
        if (string.IsNullOrWhiteSpace(implementor))
            return JavaBrand.Unknown;

        foreach ((string token, JavaBrand brand) in BrandMap)
        {
            if (implementor.Contains(token, StringComparison.OrdinalIgnoreCase))
                return brand;
        }

        return JavaBrand.Unknown;
    }

    private static JavaArchitecture ParseArchitecture(string? architecture)
    {
        if (string.IsNullOrWhiteSpace(architecture))
            return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X86 => JavaArchitecture.X86,
                Architecture.X64 => JavaArchitecture.X64,
                Architecture.Arm => JavaArchitecture.Arm,
                Architecture.Arm64 => JavaArchitecture.Arm64,
                _ => JavaArchitecture.Unknown
            };

        return architecture.ToLowerInvariant() switch
        {
            "x86" or "i386" or "i686" => JavaArchitecture.X86,
            "x86_64" or "amd64" => JavaArchitecture.X64,
            "arm" => JavaArchitecture.Arm,
            "aarch64" or "arm64" => JavaArchitecture.Arm64,
            _ => JavaArchitecture.Unknown
        };
    }

    private static bool TryParseVersion(string? value, out Version? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        ReadOnlySpan<char> span = value.AsSpan().Trim();
        int suffixIndex = span.IndexOfAny('+', '-');
        if (suffixIndex >= 0)
            span = span[..suffixIndex];

        int updateIndex = span.IndexOf('_');
        int update = 0;
        if (updateIndex >= 0)
        {
            _ = int.TryParse(span[(updateIndex + 1)..], out update);
            span = span[..updateIndex];
        }

        Span<int> parts = stackalloc int[4];
        int partCount = 0;
        while (!span.IsEmpty && partCount < parts.Length)
        {
            int dotIndex = span.IndexOf('.');
            ReadOnlySpan<char> segment = dotIndex >= 0 ? span[..dotIndex] : span;
            if (!int.TryParse(segment, out parts[partCount]))
                return false;

            partCount++;
            if (dotIndex < 0)
                break;

            span = span[(dotIndex + 1)..];
        }

        if (partCount == 0)
            return false;

        while (partCount < 4)
            parts[partCount++] = 0;

        if (update > 0)
            parts[3] = update;

        version = new Version(parts[0], parts[1], parts[2], parts[3]);
        return true;
    }

    private static IEnumerable<string> EnumerateManagedRuntimeRoots()
    {
        List<string> roots = [];
        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrWhiteSpace(appData))
                roots.Add(Path.Combine(appData, ".minecraft", "runtime"));
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrWhiteSpace(localAppData))
                roots.Add(Path.Combine(localAppData, "PCL-N", "runtime"));
        }
        catch (Exception)
        {
            // Environment folder lookup can fail in restricted hosts; skip managed roots.
        }

        return roots;
    }

    private static IEnumerable<string> EnumeratePathEntries()
    {
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
            yield break;

        foreach (string entry in path.Split(Path.PathSeparator))
        {
            if (!string.IsNullOrWhiteSpace(entry))
                yield return entry;
        }
    }

    private static IEnumerable<string> EnumerateExisting(params string[] paths)
    {
        foreach (string path in paths)
        {
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                yield return path;
        }
    }

    private static IEnumerable<string> SafeEnumerateDirectories(string path)
    {
        try
        {
            return Directory.EnumerateDirectories(path);
        }
        catch (Exception) when (IsIoException())
        {
            return [];
        }
    }

    private static string? GetWindowedJavaExecutablePath(string javaHome)
    {
        string javaw = Path.Combine(javaHome, "bin", "javaw.exe");
        return File.Exists(javaw) ? Path.GetFullPath(javaw) : null;
    }

    private static string GetJavaExecutablePath(string javaHome) =>
        Path.Combine(javaHome, "bin", GetJavaExecutableName());

    private static string GetJavaExecutableName() => OperatingSystem.IsWindows() ? "java.exe" : "java";

    private static string GetJavacExecutableName() => OperatingSystem.IsWindows() ? "javac.exe" : "javac";

    private static bool IsJavaExecutableName(string fileName) => OperatingSystem.IsWindows()
        ? string.Equals(fileName, "java.exe", StringComparison.OrdinalIgnoreCase) ||
          string.Equals(fileName, "javaw.exe", StringComparison.OrdinalIgnoreCase)
        : string.Equals(fileName, "java", StringComparison.Ordinal);

    private static StringComparer GetPathComparer() =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static string GetHomeDirectory()
    {
        string? home = Environment.GetEnvironmentVariable("HOME");
        if (!string.IsNullOrWhiteSpace(home))
            return Path.GetFullPath(home);

        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return !string.IsNullOrWhiteSpace(profile)
            ? Path.GetFullPath(profile)
            : Path.GetFullPath(Path.GetTempPath());
    }

    private static bool IsPathException() => true;

    private static bool IsIoException() => true;
}
