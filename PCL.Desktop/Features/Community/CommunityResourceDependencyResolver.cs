// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Desktop.Features.Community;

public sealed record CommunityResourceDownloadPlanItem(
    CommunityResourceEntry Entry,
    CommunityResourceVersion Version,
    CommunityResourceDownloadFile File,
    bool IsDependency);

public static class CommunityResourceDependencyResolver
{
    public static async Task<IReadOnlyList<CommunityResourceVersion>> EnrichNamesAsync(
        ICommunityResourceCatalog catalog,
        IReadOnlyList<CommunityResourceVersion> versions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        Dictionary<(CommunityResourceSource Source, string ProjectId), string> titles = [];
        IEnumerable<(CommunityResourceSource Source, string ProjectId)> keys = versions
            .SelectMany(static version => version.Dependencies)
            .Where(static dependency => !string.IsNullOrWhiteSpace(dependency.ProjectId))
            .Select(static dependency => (dependency.Source, dependency.ProjectId))
            .Distinct()
            .Take(40);

        using SemaphoreSlim gate = new(4, 4);
        await Task.WhenAll(keys.Select(async key =>
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                CommunityResourceEntry? project = await TryGetProjectAsync(
                        catalog,
                        key.Source,
                        key.ProjectId,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (project is not null)
                {
                    lock (titles)
                        titles[key] = project.Title;
                }
            }
            finally
            {
                gate.Release();
            }
        })).ConfigureAwait(false);

        return versions.Select(version => version with
        {
            Dependencies = version.Dependencies.Select(dependency => dependency with
            {
                ProjectTitle = titles.GetValueOrDefault((dependency.Source, dependency.ProjectId))
            }).ToArray()
        }).ToArray();
    }

    public static async Task<IReadOnlyList<CommunityResourceDownloadPlanItem>> ResolveRequiredDownloadsAsync(
        ICommunityResourceCatalog catalog,
        CommunityResourceEntry rootEntry,
        CommunityResourceVersion rootVersion,
        CommunityResourceDownloadFile rootFile,
        CommunitySearchOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(rootEntry);
        ArgumentNullException.ThrowIfNull(rootVersion);
        ArgumentNullException.ThrowIfNull(rootFile);
        ArgumentNullException.ThrowIfNull(options);

        List<CommunityResourceDownloadPlanItem> result = [];
        HashSet<(CommunityResourceSource Source, string ProjectId)> visited = [];
        await ResolveAsync(rootEntry, rootVersion, rootFile, isDependency: false).ConfigureAwait(false);
        return result;

        async Task ResolveAsync(
            CommunityResourceEntry entry,
            CommunityResourceVersion version,
            CommunityResourceDownloadFile file,
            bool isDependency)
        {
            cancellationToken.ThrowIfCancellationRequested();
            (CommunityResourceSource Source, string ProjectId) key = (entry.Source, entry.ProjectId);
            if (!visited.Add(key))
                return;

            foreach (CommunityResourceDependency dependency in version.Dependencies
                         .Where(static dependency => dependency.Type == CommunityResourceDependencyType.Required))
            {
                if (string.IsNullOrWhiteSpace(dependency.ProjectId))
                {
                    throw new InvalidOperationException(
                        $"前置 {dependency.DisplayName} 缺少项目标识，无法自动下载。");
                }

                CommunityResourceEntry? dependencyEntry = await TryGetProjectAsync(
                        catalog,
                        dependency.Source,
                        dependency.ProjectId,
                        cancellationToken)
                    .ConfigureAwait(false);
                dependencyEntry ??= new CommunityResourceEntry(
                    dependency.ProjectId,
                    dependency.ProjectId,
                    dependency.DisplayName,
                    string.Empty,
                    "mod",
                    null,
                    0,
                    null)
                {
                    Source = dependency.Source
                };

                CommunitySearchOptions dependencyOptions = options with { Source = dependency.Source };
                IReadOnlyList<CommunityResourceVersion> candidates = await catalog.GetVersionsAsync(
                        dependencyEntry,
                        dependencyOptions,
                        cancellationToken)
                    .ConfigureAwait(false);
                CommunityResourceVersion? dependencyVersion = SelectVersion(candidates, dependency.VersionId);
                if (dependencyVersion is null && !string.IsNullOrWhiteSpace(dependency.VersionId))
                {
                    candidates = await catalog.GetVersionsAsync(
                            dependencyEntry,
                            dependencyOptions with { GameVersion = null, Loader = null },
                            cancellationToken)
                        .ConfigureAwait(false);
                    dependencyVersion = SelectVersion(candidates, dependency.VersionId);
                }

                CommunityResourceDownloadFile? dependencyFile = dependencyVersion is { Files.Count: > 0 }
                    ? dependencyVersion.Files[0]
                    : null;
                if (dependencyVersion is null || dependencyFile is null)
                {
                    throw new InvalidOperationException(
                        $"未找到前置 {dependency.DisplayName} 的兼容下载文件。");
                }

                await ResolveAsync(
                        dependencyEntry,
                        dependencyVersion,
                        dependencyFile,
                        isDependency: true)
                    .ConfigureAwait(false);
            }

            result.Add(new CommunityResourceDownloadPlanItem(entry, version, file, isDependency));
        }
    }

    private static CommunityResourceVersion? SelectVersion(
        IReadOnlyList<CommunityResourceVersion> candidates,
        string? requiredVersionId)
    {
        if (!string.IsNullOrWhiteSpace(requiredVersionId))
        {
            return candidates.FirstOrDefault(version =>
                string.Equals(version.VersionId, requiredVersionId, StringComparison.OrdinalIgnoreCase));
        }

        return candidates
            .OrderByDescending(static version => version.PublishedAt ?? DateTimeOffset.MinValue)
            .FirstOrDefault();
    }

    private static async Task<CommunityResourceEntry?> TryGetProjectAsync(
        ICommunityResourceCatalog catalog,
        CommunityResourceSource source,
        string projectId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await catalog.GetProjectAsync(source, projectId, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or IOException)
        {
            return null;
        }
    }
}
