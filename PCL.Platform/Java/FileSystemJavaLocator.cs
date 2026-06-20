// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Frozen;
using System.Runtime.InteropServices;
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

            candidates.TryAdd(candidate.Installation.JavaExecutablePath, candidate);
        }

        return ValueTask.FromResult<IReadOnlyList<JavaRuntimeCandidate>>(candidates.Values.ToArray());
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

        if (OperatingSystem.IsWindows())
        {
            foreach (string root in EnumerateExisting(
                         Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                         Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)))
            {
                yield return Path.Combine(root, "Java");
                yield return Path.Combine(root, "Eclipse Adoptium");
                yield return Path.Combine(root, "Microsoft");
                yield return Path.Combine(root, "Zulu");
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
        }
    }

    private static string? ResolveJavaHome(string path)
    {
        if (File.Exists(path) && IsJavaExecutableName(Path.GetFileName(path)))
            return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, ".."));

        if (Directory.Exists(path))
        {
            string macBundleHome = Path.Combine(path, "Contents", "Home");
            if (Directory.Exists(macBundleHome) && File.Exists(GetJavaExecutablePath(macBundleHome)))
                return Path.GetFullPath(macBundleHome);

            if (File.Exists(GetJavaExecutablePath(path)))
                return Path.GetFullPath(path);

            string parent = Directory.GetParent(path)?.FullName ?? string.Empty;
            if (string.Equals(Path.GetFileName(path), "bin", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(parent) &&
                File.Exists(Path.Combine(path, GetJavaExecutableName())))
            {
                return Path.GetFullPath(parent);
            }
        }

        return null;
    }

    private static JavaRuntimeCandidate? TryCreateCandidate(string javaHome)
    {
        string javaExecutable = GetJavaExecutablePath(javaHome);
        if (!File.Exists(javaExecutable))
            return null;

        Dictionary<string, string> release = ReadReleaseFile(Path.Combine(javaHome, "release"));
        Version parsedVersion;
        if (!TryParseVersion(GetReleaseValue(release, "JAVA_VERSION"), out Version? version) || version is null)
            parsedVersion = new Version(0, 0, 0, 0);
        else
            parsedVersion = version;

        JavaArchitecture architecture = ParseArchitecture(GetReleaseValue(release, "OS_ARCH"));
        bool isJre = !File.Exists(Path.Combine(javaHome, "bin", GetJavacExecutableName()));
        JavaInstallation installation = new(
            Path.GetFullPath(javaHome),
            Path.GetFullPath(javaExecutable),
            OperatingSystem.IsWindows() ? GetWindowedJavaExecutablePath(javaHome) : null,
            parsedVersion,
            ParseBrand(GetReleaseValue(release, "IMPLEMENTOR")),
            architecture,
            architecture is JavaArchitecture.X64 or JavaArchitecture.Arm64,
            isJre);

        return new JavaRuntimeCandidate(installation, Source: JavaSource.AutoScanned);
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

    private static bool IsJavaExecutableName(string fileName) =>
        string.Equals(fileName, "java", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(fileName, "java.exe", StringComparison.OrdinalIgnoreCase);

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
