// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using PCL.Core.App;
using PCL.Core.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace PCL.Core.Minecraft.Java.Scanner;

public class DefaultPathsScanner : IJavaScanner
{
    private const int DefaultSearchDepth = 8;
    private const int TargetedSearchDepth = 10;
    private static readonly string[] _JavaExecutableNames =
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ["java.exe", "java"] : ["java", "java.exe"];
    private readonly IReadOnlyList<SearchRoot>? _configuredRoots;

    public DefaultPathsScanner()
    {
    }

    internal DefaultPathsScanner(IEnumerable<string> roots)
    {
        _configuredRoots = roots
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => new SearchRoot(path, TargetedSearchDepth, false))
            .ToArray();
    }

    public void Scan(ICollection<string> results)
    {
        try
        {
            var searchRoots = _configuredRoots ?? _GetSearchRoots();
            var found = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
            var stopwatch = Stopwatch.StartNew();

            LogWrapper.Info(
                $"[Java] 开始并行扫描 {searchRoots.Count} 个候选目录:{Environment.NewLine}" +
                string.Join(Environment.NewLine, searchRoots.Select(root => root.Path)));

            Parallel.ForEach(searchRoots,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = Math.Min(Math.Max(Environment.ProcessorCount, 2), 6)
                },
                root => _SearchRoot(root, found));

            foreach (var path in found.Keys)
                results.Add(path);

            LogWrapper.Info($"[Java] 候选目录扫描完成，找到 {found.Count} 个 Java，耗时 {stopwatch.ElapsedMilliseconds} ms");
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Java", "默认路径扫描失败");
        }
    }

    private static IReadOnlyList<SearchRoot> _GetSearchRoots()
    {
        var roots = new Dictionary<string, SearchRoot>(StringComparer.OrdinalIgnoreCase);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        // 常见工具管理的 JDK 不一定包含 java/jdk 关键词，必须直接扫描。
        AddCombinedRoot(userProfile, [".jdks"], TargetedSearchDepth, false);
        AddCombinedRoot(userProfile, [".gradle", "jdks"], TargetedSearchDepth, false);
        AddCombinedRoot(userProfile, [".minecraft", "runtime"], TargetedSearchDepth, false);
        AddCombinedRoot(userProfile, [".sdkman", "candidates", "java"], TargetedSearchDepth, false);
        AddCombinedRoot(userProfile, [".asdf", "installs", "java"], TargetedSearchDepth, false);
        AddCombinedRoot(userProfile, ["Library", "Java", "JavaVirtualMachines"], TargetedSearchDepth, false);
        AddCombinedRoot(local, ["JetBrains", "Toolbox", "apps"], TargetedSearchDepth, false);
        AddCombinedRoot(local, ["Programs", "Eclipse Adoptium"], TargetedSearchDepth, false);
        AddCombinedRoot(local, ["Programs", "GraalVM"], TargetedSearchDepth, false);
        AddCombinedRoot(local, ["Programs", "Microsoft"], TargetedSearchDepth, false);
        AddCombinedRoot(local, ["Programs", "Zulu"], TargetedSearchDepth, false);
        AddCombinedRoot(roaming, ["PrismLauncher", "java"], TargetedSearchDepth, false);
        AddCombinedRoot(roaming, ["PolyMC", "java"], TargetedSearchDepth, false);
        AddCombinedRoot(programFiles, ["Eclipse Adoptium"], TargetedSearchDepth, false);
        AddCombinedRoot(programFiles, ["GraalVM"], TargetedSearchDepth, false);
        AddCombinedRoot(programFiles, ["Java"], TargetedSearchDepth, false);
        AddCombinedRoot(programFiles, ["Zulu"], TargetedSearchDepth, false);
        AddCombinedRoot(programFilesX86, ["Minecraft Launcher", "runtime"], TargetedSearchDepth, false);
        AddRoot(Path.Combine(Basics.ExecutableDirectory, "PCL"), TargetedSearchDepth, false);
        AddRoot("/Library/Java/JavaVirtualMachines", TargetedSearchDepth, false);
        AddRoot("/opt/homebrew/Cellar", DefaultSearchDepth, true);
        AddRoot("/opt/homebrew/opt", TargetedSearchDepth, true);
        AddRoot("/usr/lib/jvm", TargetedSearchDepth, false);
        AddRoot("/usr/java", TargetedSearchDepth, false);
        AddRoot("/usr/local/Cellar", DefaultSearchDepth, true);
        AddRoot("/usr/local/opt", TargetedSearchDepth, true);
        AddRoot("/opt/java", TargetedSearchDepth, false);
        AddRoot("/opt/jdk", TargetedSearchDepth, false);

        // 这些目录可能很大，仅扫描第一层名称符合 Java 关键词的分支。
        AddRoot(userProfile, DefaultSearchDepth, true);
        AddRoot(roaming, DefaultSearchDepth, true);
        AddRoot(local, DefaultSearchDepth, true);
        AddCombinedRoot(local, ["Programs"], DefaultSearchDepth, true);
        AddRoot(programFiles, DefaultSearchDepth, true);
        AddRoot(programFilesX86, DefaultSearchDepth, true);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            foreach (var drive in DriveInfo.GetDrives()
                         .Where(drive => drive.DriveType == DriveType.Fixed && drive.IsReady))
                try
                {
                    foreach (var directory in Directory.EnumerateDirectories(drive.Name, "*", _EnumerationOptions)
                                 .Where(_ShouldScanDirectory))
                        AddRoot(directory, DefaultSearchDepth, false);
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                {
                    LogWrapper.Debug($"[Java] 跳过磁盘根目录 {drive.Name}: {ex.Message}");
                }

        return roots.Values.ToArray();

        void AddRoot(string path, int maxDepth, bool filterFirstLevel)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                return;

            try
            {
                path = Path.GetFullPath(path);
            }
            catch
            {
                return;
            }

            var candidate = new SearchRoot(path, maxDepth, filterFirstLevel);
            if (!roots.TryGetValue(path, out var existing) ||
                existing.FilterFirstLevel && !filterFirstLevel ||
                existing.MaxDepth < maxDepth)
                roots[path] = candidate;
        }

        void AddCombinedRoot(string basePath, string[] parts, int maxDepth, bool filterFirstLevel)
        {
            if (string.IsNullOrWhiteSpace(basePath))
                return;
            AddRoot(Path.Combine([basePath, .. parts]), maxDepth, filterFirstLevel);
        }
    }

    private static void _SearchRoot(SearchRoot root, ConcurrentDictionary<string, byte> results)
    {
        if (!Directory.Exists(root.Path))
            return;

        var queue = new Queue<(string Path, int Depth)>();
        queue.Enqueue((root.Path, 0));

        while (queue.Count > 0)
        {
            var (current, depth) = queue.Dequeue();
            if (depth > root.MaxDepth)
                continue;

            try
            {
                var foundCurrent = false;
                foreach (var executableName in _JavaExecutableNames)
                {
                    var directJava = Path.Combine(current, executableName);
                    if (File.Exists(directJava))
                    {
                        results.TryAdd(directJava, 0);
                        foundCurrent = true;
                    }

                    var binJava = Path.Combine(current, "bin", executableName);
                    if (File.Exists(binJava))
                    {
                        results.TryAdd(binJava, 0);
                        foundCurrent = true;
                    }
                }

                if (foundCurrent)
                    continue;

                if (depth == root.MaxDepth)
                    continue;

                foreach (var directory in Directory.EnumerateDirectories(current, "*", _EnumerationOptions))
                {
                    var name = Path.GetFileName(directory);
                    if (_ShouldExcludeDirectory(name))
                        continue;
                    if (depth == 0 && root.FilterFirstLevel && !_ShouldScanDirectory(directory))
                        continue;

                    queue.Enqueue((directory, depth + 1));
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or DirectoryNotFoundException)
            {
                LogWrapper.Debug($"[Java] 跳过目录 {current}: {ex.Message}");
            }
            catch (Exception ex)
            {
                LogWrapper.Error(ex, "Java", $"搜索目录 {current} 时出错");
            }
        }
    }

    private static bool _ShouldScanDirectory(string path)
    {
        var name = Path.GetFileName(path);
        return !_ShouldExcludeDirectory(name) &&
               JavaConsts.AllKeyworkds.Any(keyword =>
                   name.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static bool _ShouldExcludeDirectory(string name) =>
        JavaConsts.ExcludeFolderNames.Any(excluded =>
            name.Contains(excluded, StringComparison.OrdinalIgnoreCase));

    private static readonly EnumerationOptions _EnumerationOptions = new()
    {
        IgnoreInaccessible = true,
        RecurseSubdirectories = false,
        ReturnSpecialDirectories = false,
        AttributesToSkip = FileAttributes.ReparsePoint
    };

    private sealed record SearchRoot(string Path, int MaxDepth, bool FilterFirstLevel);
}
