// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Desktop.Features.Launching.Views;

public sealed record LaunchInstanceInfo(string Name, string VersionJsonPath, string InstanceDirectory);

public sealed record LaunchInstanceDiscoveryProgress(
    string Stage,
    int Current,
    int Total,
    int Found,
    string? RootDirectory = null);

public static class LaunchInstanceDiscovery
{
    public static Task<IReadOnlyList<LaunchInstanceInfo>> DiscoverAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => Discover(GetCandidateRoots(), cancellationToken), cancellationToken);

    public static Task<IReadOnlyList<LaunchInstanceInfo>> DiscoverAsync(
        IEnumerable<string> candidateRoots,
        CancellationToken cancellationToken = default)
        => DiscoverAsync(candidateRoots, progress: null, cancellationToken);

    public static Task<IReadOnlyList<LaunchInstanceInfo>> DiscoverAsync(
        IEnumerable<string> candidateRoots,
        IProgress<LaunchInstanceDiscoveryProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidateRoots);
        string[] roots = candidateRoots.ToArray();
        return Task.Run(() => Discover(roots, progress, cancellationToken), cancellationToken);
    }

    public static IReadOnlyList<LaunchInstanceInfo> Discover(
        IEnumerable<string> candidateRoots,
        CancellationToken cancellationToken = default)
        => Discover(candidateRoots, progress: null, cancellationToken);

    public static IReadOnlyList<LaunchInstanceInfo> Discover(
        IEnumerable<string> candidateRoots,
        IProgress<LaunchInstanceDiscoveryProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        string[] roots = candidateRoots
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        List<(LaunchInstanceInfo Instance, DateTime LastWriteTimeUtc)> result = [];
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string root = roots[rootIndex];
            progress?.Report(new LaunchInstanceDiscoveryProgress(
                "正在扫描游戏文件夹",
                rootIndex,
                roots.Length,
                result.Count,
                root));
            string versionsRoot = Path.Combine(root, "versions");
            if (!Directory.Exists(versionsRoot))
                continue;

            DirectoryInfo[] versionDirectories;
            try
            {
                versionDirectories = new DirectoryInfo(versionsRoot).GetDirectories();
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            progress?.Report(new LaunchInstanceDiscoveryProgress(
                "正在检查游戏版本",
                0,
                versionDirectories.Length,
                result.Count,
                root));
            for (int versionIndex = 0; versionIndex < versionDirectories.Length; versionIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                DirectoryInfo versionDirectory = versionDirectories[versionIndex];
                string name = versionDirectory.Name;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    string jsonPath = Path.Combine(versionDirectory.FullName, name + ".json");
                    if (File.Exists(jsonPath))
                    {
                        result.Add((
                            new LaunchInstanceInfo(name, jsonPath, versionDirectory.FullName),
                            versionDirectory.LastWriteTimeUtc));
                    }
                }

                progress?.Report(new LaunchInstanceDiscoveryProgress(
                    "正在检查游戏版本",
                    versionIndex + 1,
                    versionDirectories.Length,
                    result.Count,
                    root));
            }
        }

        progress?.Report(new LaunchInstanceDiscoveryProgress(
            "游戏版本检查完成",
            roots.Length,
            roots.Length,
            result.Count));
        return result
            .OrderByDescending(entry => entry.LastWriteTimeUtc)
            .ThenBy(entry => entry.Instance.Name, StringComparer.OrdinalIgnoreCase)
            .Select(entry => entry.Instance)
            .ToArray();
    }

    public static IReadOnlyList<string> GetCandidateRoots()
    {
        List<string> roots = [];
        string? configuredRoots = Environment.GetEnvironmentVariable("PCLN_MINECRAFT_ROOTS");
        if (!string.IsNullOrWhiteSpace(configuredRoots))
        {
            foreach (string root in configuredRoots.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                AddIfUsable(roots, root);
        }

        AddIfUsable(roots, Path.Combine(AppContext.BaseDirectory, ".minecraft"));

        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            AddIfUsable(roots, Path.Combine(userProfile, ".minecraft"));
            AddIfUsable(roots, Path.Combine(userProfile, "Library", "Application Support", "minecraft"));
        }

        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrWhiteSpace(appData))
            AddIfUsable(roots, Path.Combine(appData, ".minecraft"));

        return roots;
    }

    private static void AddIfUsable(List<string> roots, string path)
    {
        if (!string.IsNullOrWhiteSpace(path))
            roots.Add(path);
    }
}
