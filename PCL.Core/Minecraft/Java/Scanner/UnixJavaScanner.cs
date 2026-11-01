using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using PCL.Core.Logging;

namespace PCL.Core.Minecraft.Java.Scanner;

/// <summary>
///     非 Windows 平台的 Java 扫描：JAVA_HOME、发行版常见 JVM 目录，以及 command -v java（解析符号链接）。
/// </summary>
public class UnixJavaScanner : IJavaScanner
{
    private static readonly string[] _JvmRoots =
    [
        "/usr/lib/jvm",
        "/usr/lib64/jvm",
        "/usr/java",
        "/usr/local/java",
        "/usr/local/lib/jvm",
        "/opt/java",
        "/opt/jdk"
    ];

    private const string MacJvmRoot = "/Library/Java/JavaVirtualMachines";

    public void Scan(ICollection<string> results)
    {
        try
        {
            _AddJavaHome(results);
            foreach (var root in _JvmRoots)
                _AddJvmHomeChildren(root, results);
            _AddMacJdkBundles(results);
            _AddFromCommandV(results);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Java", "非 Windows 平台 Java 扫描失败");
        }
    }

    private static void _AddJavaHome(ICollection<string> results)
    {
        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (string.IsNullOrWhiteSpace(javaHome)) return;

        _TryAddJavaExecutable(Path.Combine(javaHome, "bin"), results);
    }

    private static void _AddJvmHomeChildren(string root, ICollection<string> results)
    {
        if (!Directory.Exists(root)) return;

        try
        {
            foreach (var home in Directory.EnumerateDirectories(root))
                _TryAddJavaExecutable(Path.Combine(home, "bin"), results);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            LogWrapper.Debug($"跳过 JVM 目录 {root}: {ex.Message}");
        }
    }

    private static void _AddMacJdkBundles(ICollection<string> results)
    {
        if (!OperatingSystem.IsMacOS() || !Directory.Exists(MacJvmRoot)) return;

        try
        {
            foreach (var bundle in Directory.EnumerateDirectories(MacJvmRoot))
                _TryAddJavaExecutable(Path.Combine(bundle, "Contents", "Home", "bin"), results);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            LogWrapper.Debug($"跳过 macOS JVM 目录 {MacJvmRoot}: {ex.Message}");
        }
    }

    private static void _AddFromCommandV(ICollection<string> results)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "/bin/sh",
                Arguments = "-c \"command -v java\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc is null) return;

            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();

            if (proc.ExitCode != 0) return;

            var path = output
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .FirstOrDefault(p => p.Length > 0);
            if (path is null) return;

            results.Add(_ResolveLinkTarget(path));
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Java", "command -v java 扫描失败");
        }
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

    private static void _TryAddJavaExecutable(string javaFolder, ICollection<string> results)
    {
        var javaExe = JavaExecutableLayout.GetJavaExePath(javaFolder);
        if (File.Exists(javaExe)) results.Add(javaExe);
    }
}