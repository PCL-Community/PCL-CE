using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using PCL.Core.Logging;

namespace PCL.Core.Minecraft.Java.Scanner;

/// <summary>macOS Java 扫描：JAVA_HOME、JDK bundle 与 command -v java。</summary>
public sealed class MacOsJavaScanner : IJavaScanner
{
    private const string JvmRoot = "/Library/Java/JavaVirtualMachines";
    private const string SystemJvmRoot = "/System/Library/Java/JavaVirtualMachines";

    public void Scan(ICollection<string> results)
    {
        try
        {
            _AddJavaHome(results);
            _AddBundles(JvmRoot, results);
            _AddBundles(SystemJvmRoot, results);
            _AddFromCommandV(results);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Java", "macOS Java 扫描失败");
        }
    }

    private static void _AddJavaHome(ICollection<string> results)
    {
        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (string.IsNullOrWhiteSpace(javaHome)) return;
        _TryAdd(Path.Combine(javaHome, "bin"), results);
    }

    private static void _AddBundles(string root, ICollection<string> results)
    {
        if (!Directory.Exists(root)) return;
        try
        {
            foreach (var bundle in Directory.EnumerateDirectories(root))
                _TryAdd(Path.Combine(bundle, "Contents", "Home", "bin"), results);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            LogWrapper.Debug($"跳过 macOS JVM 目录 {root}: {ex.Message}");
        }
    }

    private static void _AddFromCommandV(ICollection<string> results)
    {
        try
        {
            var info = new ProcessStartInfo
            {
                FileName = "/bin/sh",
                Arguments = "-c \"command -v java\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var process = Process.Start(info);
            if (process is null) return;
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0) return;
            var path = output
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .FirstOrDefault(p => p.Length > 0);
            if (path is not null) results.Add(_ResolveLink(path));
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Java", "command -v java 扫描失败");
        }
    }

    private static string _ResolveLink(string path)
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

    private static void _TryAdd(string javaFolder, ICollection<string> results)
    {
        var exe = JavaExecutableLayout.GetJavaExePath(javaFolder);
        if (File.Exists(exe)) results.Add(exe);
    }
}