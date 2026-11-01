using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using PCL.Core.Logging;
using PCL.Core.Utils;

namespace PCL.Core.Minecraft.Java.Parser;

public partial class PeHeaderParser : IJavaParser
{
    private static readonly Dictionary<string, JavaBrandType> _BrandMap = new()
    {
        ["Eclipse"] = JavaBrandType.EclipseTemurin,
        ["Temurin"] = JavaBrandType.EclipseTemurin,
        ["Bellsoft"] = JavaBrandType.Liberica,
        ["Microsoft"] = JavaBrandType.Microsoft,
        ["Amazon"] = JavaBrandType.Corretto,
        ["Azul"] = JavaBrandType.Zulu,
        ["IBM"] = JavaBrandType.IBMSemeru,
        ["Oracle"] = JavaBrandType.Oracle,
        ["Tencent"] = JavaBrandType.TencentKona,
        ["OpenJDK"] = JavaBrandType.OpenJDK,
        ["Alibaba"] = JavaBrandType.Dragonwell,
        ["GraalVM"] = JavaBrandType.GraalVmCommunity,
        ["JetBrains"] = JavaBrandType.JetBrains
    };

    [GeneratedRegex(@"\d+(?:[._]\d+)*")]
    private static partial Regex _VersionPattern();

    public JavaInstallation? Parse(string javaExePath)
    {
        try
        {
            if (!File.Exists(javaExePath))
                return null;

            LogWrapper.Info("Java", $"解析 {javaExePath} 的 Java 程序信息");

            return OperatingSystem.IsWindows()
                ? _ParseWindows(javaExePath)
                : _ParseUnixLike(javaExePath);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, $"[Java] 解析 {javaExePath} 时出错");
            return null;
        }
    }

    private static JavaInstallation _ParseWindows(string javaExePath)
    {
        var versionInfo = FileVersionInfo.GetVersionInfo(javaExePath);
        var fileVersion = Version.Parse(versionInfo.FileVersion ?? "0.0.0.0");
        var companyName = _NormalizeCompanyName(versionInfo);
        var brand = _DetermineBrand(companyName);

        var javaFolder = Path.GetDirectoryName(javaExePath)!;
        var isJre = !File.Exists(Path.Combine(javaFolder, "javac.exe"));

        var peData = PEHeaderReader.ReadPEHeader(javaExePath);
        var arch = peData.Machine;
        var is64Bit = PEHeaderReader.IsMachine64Bit(arch);

        return new JavaInstallation(
            javaFolder,
            fileVersion,
            brand,
            arch,
            is64Bit,
            isJre
        );
    }

    private static JavaInstallation? _ParseUnixLike(string javaExePath)
    {
        // 解析符号链接，让 /usr/bin/java 这类入口与真实安装目录得到相同的 JavaFolder，避免重复条目
        var resolvedPath = _ResolveLinkTarget(javaExePath);
        var javaFolder = Path.GetDirectoryName(resolvedPath)!;
        var release = _ReadReleaseFile(javaFolder);

        string? probeOutput = null;
        var version = _ParseVersion(_GetReleaseValue(release, "JAVA_VERSION"))
            ?? _ParseVersion(_GetReleaseValue(release, "JAVA_RUNTIME_VERSION"));
        if (version is null)
        {
            probeOutput = _TryProbeVersionOutput(resolvedPath);
            version = _ParseVersionFromOutput(probeOutput);
        }

        version ??= _ParseVersionFromPath(javaFolder);
        if (version is null)
            return null;

        var brand = _DetermineBrand(
            _GetReleaseValue(release, "IMPLEMENTOR")
            ?? _GetReleaseValue(release, "IMPLEMENTOR_VERSION")
            ?? _GetReleaseValue(release, "JAVA_VENDOR")
            ?? "");
        if (brand is JavaBrandType.Unknown)
        {
            probeOutput ??= _TryProbeVersionOutput(resolvedPath);
            if (probeOutput is not null)
                brand = _DetermineBrand(probeOutput);
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

    /// <summary>读取 javaHome/release，键值形如 JAVA_VERSION="17.0.9"。</summary>
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

        // macOS 的 java 是 Mach-O，读不到 ELF，退回 release 中的 OS_ARCH
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

    /// <summary>解析 java -version 输出中的带引号版本号，例如 openjdk version "17.0.9"。</summary>
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
        return end > start ? _ParseVersion(output[start..end]) : null;
    }

    /// <summary>从 /usr/lib/jvm/java-17-openjdk-amd64 这类目录名推断版本。</summary>
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
            var match = _VersionPattern().Match(folderName);
            if (match.Success && _ParseVersion(match.Value) is { } version)
                return version;
        }

        return null;
    }

    private static Version? _ParseVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        // 兼容 1.8.0_382（把下划线视作分段）与 17.0.9+11 这类带构建号的写法
        var main = value.Trim().Trim('"').Split('+', '-')[0].Replace('_', '.');
        var numbers = new List<int>(4);
        foreach (var part in main.Split('.'))
        {
            if (numbers.Count == 4 || !int.TryParse(part, out var number))
                break;
            numbers.Add(number);
        }

        if (numbers.Count == 0)
            return null;

        return new Version(
            numbers[0],
            numbers.Count > 1 ? numbers[1] : 0,
            numbers.Count > 2 ? numbers[2] : 0,
            numbers.Count > 3 ? numbers[3] : 0);
    }

    private static string _NormalizeCompanyName(FileVersionInfo info)
    {
        var name = info.CompanyName ?? info.FileDescription ?? info.ProductName ?? string.Empty;

        // 修复 Oracle/OpenJDK 混淆问题
        if (!name.Contains("Oracle", StringComparison.OrdinalIgnoreCase) && name != "N/A")
            return name;

        if ((info.FileDescription?.Contains("Java(TM)", StringComparison.OrdinalIgnoreCase)
                ?? false)
            || (info.ProductName?.Contains("Java(TM)", StringComparison.OrdinalIgnoreCase)
                ?? false))
            return "Oracle";
        return "OpenJDK";
    }

    private static JavaBrandType _DetermineBrand(string output)
    {
        var match = _BrandMap.Keys
            .FirstOrDefault(k => output.Contains(k, StringComparison.OrdinalIgnoreCase));
        return match is not null ? _BrandMap[match] : JavaBrandType.Unknown;
    }
}