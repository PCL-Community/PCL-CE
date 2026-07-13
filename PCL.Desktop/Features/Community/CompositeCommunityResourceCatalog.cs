// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Desktop.Features.Community;

public sealed class CompositeCommunityResourceCatalog : ICommunityResourceCatalog, IDisposable
{
    private readonly ICommunityResourceCatalog _modrinth;
    private readonly ICommunityResourceCatalog _curseForge;
    private readonly bool _ownsCatalogs;

    public CompositeCommunityResourceCatalog()
        : this(new ModrinthCommunityResourceCatalog(), new CurseForgeCommunityResourceCatalog(), ownsCatalogs: true)
    {
    }

    public CompositeCommunityResourceCatalog(
        ICommunityResourceCatalog modrinth,
        ICommunityResourceCatalog curseForge,
        bool ownsCatalogs = false)
    {
        _modrinth = modrinth ?? throw new ArgumentNullException(nameof(modrinth));
        _curseForge = curseForge ?? throw new ArgumentNullException(nameof(curseForge));
        _ownsCatalogs = ownsCatalogs;
    }

    public async Task<IReadOnlyList<CommunityResourceEntry>> SearchAsync(
        CommunityResourceCategory category,
        string query,
        CommunitySearchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new CommunitySearchOptions();
        if (options.Source == CommunityResourceSource.Modrinth)
            return await _modrinth.SearchAsync(category, query, options, cancellationToken).ConfigureAwait(false);
        if (options.Source == CommunityResourceSource.CurseForge)
            return await _curseForge.SearchAsync(category, query, options, cancellationToken).ConfigureAwait(false);

        Task<IReadOnlyList<CommunityResourceEntry>> modrinthTask =
            TrySearchAsync(_modrinth, category, query, options, cancellationToken);
        Task<IReadOnlyList<CommunityResourceEntry>> curseForgeTask =
            TrySearchAsync(_curseForge, category, query, options, cancellationToken);
        await Task.WhenAll(modrinthTask, curseForgeTask).ConfigureAwait(false);
        IReadOnlyList<CommunityResourceEntry> modrinth = await modrinthTask.ConfigureAwait(false);
        IReadOnlyList<CommunityResourceEntry> curseForge = await curseForgeTask.ConfigureAwait(false);
        if (modrinth.Count == 0 && curseForge.Count == 0)
        {
            // Re-run the preferred source to surface a useful network/configuration error.
            return await _modrinth.SearchAsync(category, query, options, cancellationToken).ConfigureAwait(false);
        }

        List<CommunityResourceEntry> combined = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (CommunityResourceEntry entry in modrinth.Concat(curseForge))
        {
            string key = string.IsNullOrWhiteSpace(entry.Slug) ? entry.Title : entry.Slug;
            if (seen.Add(key))
                combined.Add(entry);
        }
        return combined;
    }

    public Task<CommunityResourceDownloadFile?> ResolveDownloadAsync(
        CommunityResourceEntry entry,
        CommunitySearchOptions? options = null,
        CancellationToken cancellationToken = default) =>
        Select(entry).ResolveDownloadAsync(entry, options, cancellationToken);

    public Task<IReadOnlyList<CommunityResourceVersion>> GetVersionsAsync(
        CommunityResourceEntry entry,
        CommunitySearchOptions? options = null,
        CancellationToken cancellationToken = default) =>
        Select(entry).GetVersionsAsync(entry, options, cancellationToken);

    public Task<CommunityResourceFileIdentity?> LookupFileBySha1Async(
        string sha1Hex,
        CancellationToken cancellationToken = default) =>
        _modrinth.LookupFileBySha1Async(sha1Hex, cancellationToken);

    public Task<CommunityResourceVersion?> GetLatestVersionAsync(
        string projectId,
        CommunitySearchOptions? options = null,
        CancellationToken cancellationToken = default) =>
        _modrinth.GetLatestVersionAsync(projectId, options, cancellationToken);

    public void Dispose()
    {
        if (!_ownsCatalogs)
            return;
        (_modrinth as IDisposable)?.Dispose();
        (_curseForge as IDisposable)?.Dispose();
    }

    private ICommunityResourceCatalog Select(CommunityResourceEntry entry) =>
        entry.Source == CommunityResourceSource.CurseForge ? _curseForge : _modrinth;

    private static async Task<IReadOnlyList<CommunityResourceEntry>> TrySearchAsync(
        ICommunityResourceCatalog catalog,
        CommunityResourceCategory category,
        string query,
        CommunitySearchOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            return await catalog.SearchAsync(category, query, options, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return [];
        }
    }
}
