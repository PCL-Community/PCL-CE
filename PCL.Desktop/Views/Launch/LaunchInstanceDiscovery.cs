// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Desktop.Views.Launch;

public sealed record LaunchInstanceInfo(string Name, string VersionJsonPath, string InstanceDirectory);

public static class LaunchInstanceDiscovery
{
    public static Task<IReadOnlyList<LaunchInstanceInfo>> DiscoverAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => Discover(GetCandidateRoots(), cancellationToken), cancellationToken);

    public static IReadOnlyList<LaunchInstanceInfo> Discover(
        IEnumerable<string> candidateRoots,
        CancellationToken cancellationToken = default)
    {
        List<LaunchInstanceInfo> result = [];
        foreach (string root in candidateRoots.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string versionsRoot = Path.Combine(root, "versions");
            if (!Directory.Exists(versionsRoot))
                continue;

            foreach (string versionDirectory in Directory.EnumerateDirectories(versionsRoot))
            {
                cancellationToken.ThrowIfCancellationRequested();
                string name = Path.GetFileName(versionDirectory);
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                string jsonPath = Path.Combine(versionDirectory, name + ".json");
                if (!File.Exists(jsonPath))
                    continue;

                result.Add(new LaunchInstanceInfo(name, jsonPath, versionDirectory));
            }
        }

        return result
            .OrderByDescending(instance => Directory.GetLastWriteTimeUtc(instance.InstanceDirectory))
            .ThenBy(instance => instance.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<string> GetCandidateRoots()
    {
        List<string> roots = [];
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
