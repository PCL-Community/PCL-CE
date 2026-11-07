using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using PCL.Core.Logging;
using PCL.Core.Utils;

namespace PCL.Core.Minecraft.Java.Parser;

/// <summary>Linux/macOS Java 解析：release 文件、ELF/Mach-O 与 java -version 探测。</summary>
public sealed class PosixJavaParser : JavaParserBase
{
    protected override JavaInstallation? ParseCore(string javaExePath)
    {
        var resolvedPath = _ResolveLinkTarget(javaExePath);
        var javaFolder = Path.GetDirectoryName(resolvedPath)!;
        var release = _ReadReleaseFile(javaFolder);

        string? probeOutput = null;
        var version = ParseVersion(_GetReleaseValue(release, "JAVA_VERSION"))
            ?? ParseVersion(_GetReleaseValue(release, "JAVA_RUNTIME_VERSION"));
        if (version is null)
        {
            probeOutput = _TryProbeVersionOutput(resolvedPath);
            version = _ParseVersionFromOutput(probeOutput);
        }

        version ??= _ParseVersionFromPath(javaFolder);
        if (version is null)
            return null;

        var brand = DetermineBrand(
            _GetReleaseValue(release, "IMPLEMENTOR")
            ?? _GetReleaseValue(release, "IMPLEMENTOR_VERSION")
            ?? _GetReleaseValue(release, "JAVA_VENDOR")
            ?? "");
        if (brand is JavaBrandType.Unknown)
        {
            probeOutput ??= _TryProbeVersionOutput(resolvedPath);
            if (probeOutput is not null)
                brand = DetermineBrand(probeOutput);
        }

        var (architecture, is64Bit) = _ReadArchitecture(resolvedPath, release);
        var isJre = !File.Exists(Path.Combine(javaFolder, "javac"));

        return new JavaInstallation(javaFolder, version, brand, architecture, is64Bit, isJre);
    }

    private static string _ResolveLinkTarget(string path)
    {
        try
        {
            return File.ResolveLinkTarget(path, true)?.FullName ?? path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return path;
        }
    }

    private static Dictionary<string, string>? _ReadReleaseFile(string javaFolder)
    {
        var javaHome = Directory.GetParent(javaFolder)?.FullName;
        if (javaHome is null)
            return null;

        var releasePath = Path.Combine(javaHome, "release");
        if (!File.Exists(releasePath))
            return null;

        try
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var line in File.ReadLines(releasePath))
            {
                var separator = line.IndexOf('=');
                if (separator <= 0)
                    continue;
                values[line[..separator].Trim()] = line[(separator + 1)..].Trim().Trim('"');
            }

            return values;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogWrapper.Debug("Java", $"读取 release 文件失败：{ex.Message}");
            return null;
        }
    }

    private static string? _GetReleaseValue(
        IReadOnlyDictionary<string, string>? release,
        string key)
    {
        return release is not null
            && release.TryGetValue(key, out var value)
            && !string.IsNullOrWhiteSpace(value)
                ? value
                : null;
    }

    private static (MachineType Architecture, bool Is64Bit) _ReadArchitecture(
        string javaExePath,
        IReadOnlyDictionary<string, string>? release)
    {
        if (ElfHeaderReader.TryRead(javaExePath) is { } elf)
            return elf;

        var architecture = _ParseArchitecture(_GetReleaseValue(release, "OS_ARCH"));
        return architecture is null
            ? (MachineType.Unknown, false)
            : (architecture.Value, PEHeaderReader.IsMachine64Bit(architecture.Value));
    }

    private static MachineType? _ParseArchitecture(string? arch)
    {
        return arch?.ToLowerInvariant() switch
        {
            "x86_64" or "amd64" or "x64" => MachineType.AMD64,
            "x86" or "i386" or "i486" or "i586" or "i686" => MachineType.I386,
            "aarch64" or "arm64" => MachineType.ARM64,
            "arm" or "arm32" or "armhf" => MachineType.ARM,
            _ => null
        };
    }

    private static string? _TryProbeVersionOutput(string javaExePath)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = javaExePath,
                Arguments = "-version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
                return null;

            if (process.WaitForExit(5000))
                return $"{process.StandardError.ReadToEnd()}\n{process.StandardOutput.ReadToEnd()}";

            process.Kill(true);
            return null;
        }
        catch (Exception ex)
        {
            LogWrapper.Debug("Java", $"执行 java -version 失败：{ex.Message}");
            return null;
        }
    }

    private static Version? _ParseVersionFromOutput(string? output)
    {
        if (string.IsNullOrEmpty(output))
            return null;

        const string marker = "version \"";
        var index = output.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0)
            return null;

        var start = index + marker.Length;
        var end = output.IndexOf('"', start);
        return end > start ? ParseVersion(output[start..end]) : null;
    }

    private static Version? _ParseVersionFromPath(string javaFolder)
    {
        var candidates = new[]
        {
            javaFolder,
            Directory.GetParent(javaFolder)?.FullName
        };
        foreach (var candidate in candidates)
        {
            if (candidate is null)
                continue;

            var folderName = Path.GetFileName(candidate.TrimEnd(Path.DirectorySeparatorChar));
            var match = VersionPattern().Match(folderName);
            if (match.Success && ParseVersion(match.Value) is { } version)
                return version;
        }

        return null;
    }
}