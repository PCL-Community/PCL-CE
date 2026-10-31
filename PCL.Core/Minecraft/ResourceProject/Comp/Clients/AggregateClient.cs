using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Minecraft.ResourceProject.Comp.Abstractions;
using PCL.Core.Minecraft.ResourceProject.Comp.Models;
using PCL.Core.Minecraft.ResourceProject.Comp.Models.Enums;

namespace PCL.Core.Minecraft.ResourceProject.Comp.Clients;

public sealed class AggregateClient : ICompClient
{
    private readonly IReadOnlyList<ICompClient> _clients;

    public AggregateClient(params ICompClient[] clients)
    {
        _clients = clients ?? throw new ArgumentNullException(nameof(clients));
    }

    public AggregateClient(IEnumerable<ICompClient> clients)
    {
        _clients = clients?.ToList() ?? throw new ArgumentNullException(nameof(clients));
    }

    public async Task<CompSearchResult> SearchProjects(CompSearchFilter filter, CancellationToken ct = default)
    {
        var tasks = _clients.Select(c => c.SearchProjects(filter, ct)).ToArray();
        await Task.WhenAll(tasks).ConfigureAwait(false);

        var allHits = new List<CompProject>();
        var totalCount = 0;

        foreach (var task in tasks)
        {
            try
            {
                var result = await task.ConfigureAwait(false);
                allHits.AddRange(result.Hits);
                totalCount += result.TotalCount;
            }
            catch
            {
            }
        }

        return new CompSearchResult(allHits, totalCount, filter.Offset, filter.Limit);
    }

    public async Task<CompProject> GetProject(string projectId, CancellationToken ct = default)
    {
        var exceptions = new List<Exception>();

        foreach (var client in _clients)
        {
            try
            {
                return await client.GetProject(projectId, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        }

        throw new AggregateException(
            $"All providers failed to get project {projectId}.", exceptions);
    }

    public async Task<List<CompProject>> GetProjects(IEnumerable<string> projectIds, CancellationToken ct = default)
    {
        var ids = projectIds.ToList();
        if (ids.Count == 0) return [];

        var tasks = _clients.Select(c => _TryGetProjects(c, ids, ct)).ToArray();
        await Task.WhenAll(tasks).ConfigureAwait(false);

        return tasks
            .Where(t => t.IsCompletedSuccessfully && t.Result is not null)
            .SelectMany(t => t.Result!)
            .DistinctBy(p => $"{p.Provider}:{p.Id}")
            .ToList();
    }

    public async Task<List<CompProject>> GetFeaturedProjects(string? gameVersion = null, CancellationToken ct = default)
    {
        var tasks = _clients.Select(c => _TryGetFeatured(c, gameVersion, ct)).ToArray();
        await Task.WhenAll(tasks).ConfigureAwait(false);

        return tasks
            .Where(t => t.IsCompletedSuccessfully && t.Result is not null)
            .SelectMany(t => t.Result!)
            .DistinctBy(p => $"{p.Provider}:{p.Id}")
            .ToList();
    }

    public async Task<string> GetProjectDescription(string projectId, CancellationToken ct = default)
    {
        foreach (var client in _clients)
        {
            try
            {
                return await client.GetProjectDescription(projectId, ct).ConfigureAwait(false);
            }
            catch
            {
            }
        }
        return "";
    }

    public async Task<List<CompFile>> GetProjectFiles(string projectId, CompSearchFilter? filter = null, CancellationToken ct = default)
    {
        var tasks = _clients.Select(c => _TryGetFiles(c, projectId, filter, ct)).ToArray();
        await Task.WhenAll(tasks).ConfigureAwait(false);

        return tasks
            .Where(t => t.IsCompletedSuccessfully && t.Result is not null)
            .SelectMany(t => t.Result!)
            .DistinctBy(f => $"{f.Id}")
            .ToList();
    }

    public async Task<CompFile> GetFile(string fileId, CancellationToken ct = default)
    {
        foreach (var client in _clients)
        {
            try
            {
                return await client.GetFile(fileId, ct).ConfigureAwait(false);
            }
            catch
            {
            }
        }
        throw new InvalidOperationException($"All providers failed to get file {fileId}.");
    }

    public async Task<string> GetFileDownloadUrl(string fileId, CancellationToken ct = default)
    {
        foreach (var client in _clients)
        {
            try
            {
                return await client.GetFileDownloadUrl(fileId, ct).ConfigureAwait(false);
            }
            catch
            {
            }
        }
        return "";
    }

    public async Task<string> GetFileChangelog(string fileId, CancellationToken ct = default)
    {
        foreach (var client in _clients)
        {
            try
            {
                return await client.GetFileChangelog(fileId, ct).ConfigureAwait(false);
            }
            catch
            {
            }
        }
        return "";
    }

    public async Task<Dictionary<string, List<CompFile>>> MatchFingerprints(
        IEnumerable<string> hashes, HashAlgorithm algo, CancellationToken ct = default)
    {
        var result = new Dictionary<string, List<CompFile>>();

        foreach (var client in _clients)
        {
            try
            {
                var clientResult = await client.MatchFingerprints(hashes, algo, ct).ConfigureAwait(false);
                foreach (var (key, files) in clientResult)
                {
                    if (!result.ContainsKey(key))
                        result[key] = files;
                }
            }
            catch
            {
            }
        }

        return result;
    }

    public async Task<CompFile?> CheckForUpdates(
        string fileHash, HashAlgorithm algo, CompSearchFilter? filter = null, CancellationToken ct = default)
    {
        foreach (var client in _clients)
        {
            try
            {
                var result = await client.CheckForUpdates(fileHash, algo, filter, ct).ConfigureAwait(false);
                if (result is not null) return result;
            }
            catch
            {
            }
        }
        return null;
    }

    public async Task<List<CompGameVersion>> GetGameVersions(CancellationToken ct = default)
    {
        var tasks = _clients.Select(c => _TryGetGameVersions(c, ct)).ToArray();
        await Task.WhenAll(tasks).ConfigureAwait(false);

        return tasks
            .Where(t => t.IsCompletedSuccessfully && t.Result is not null)
            .SelectMany(t => t.Result!)
            .DistinctBy(gv => gv.Version)
            .ToList();
    }

    public async Task<List<CompLoader>> GetLoaders(CancellationToken ct = default)
    {
        var tasks = _clients.Select(c => _TryGetLoaders(c, ct)).ToArray();
        await Task.WhenAll(tasks).ConfigureAwait(false);

        return tasks
            .Where(t => t.IsCompletedSuccessfully && t.Result is not null)
            .SelectMany(t => t.Result!)
            .DistinctBy(l => l.Name)
            .ToList();
    }

    public async Task<List<CompCategory>> GetCategories(CancellationToken ct = default)
    {
        var tasks = _clients.Select(c => _TryGetCategories(c, ct)).ToArray();
        await Task.WhenAll(tasks).ConfigureAwait(false);

        return tasks
            .Where(t => t.IsCompletedSuccessfully && t.Result is not null)
            .SelectMany(t => t.Result!)
            .DistinctBy(c => c.Slug)
            .ToList();
    }

    private static async Task<List<CompProject>?> _TryGetProjects(ICompClient client, List<string> ids, CancellationToken ct)
    {
        try { return await client.GetProjects(ids, ct).ConfigureAwait(false); }
        catch { return null; }
    }

    private static async Task<List<CompProject>?> _TryGetFeatured(ICompClient client, string? gameVersion, CancellationToken ct)
    {
        try { return await client.GetFeaturedProjects(gameVersion, ct).ConfigureAwait(false); }
        catch { return null; }
    }

    private static async Task<List<CompFile>?> _TryGetFiles(ICompClient client, string projectId, CompSearchFilter? filter, CancellationToken ct)
    {
        try { return await client.GetProjectFiles(projectId, filter, ct).ConfigureAwait(false); }
        catch { return null; }
    }

    private static async Task<List<CompGameVersion>?> _TryGetGameVersions(ICompClient client, CancellationToken ct)
    {
        try { return await client.GetGameVersions(ct).ConfigureAwait(false); }
        catch { return null; }
    }

    private static async Task<List<CompLoader>?> _TryGetLoaders(ICompClient client, CancellationToken ct)
    {
        try { return await client.GetLoaders(ct).ConfigureAwait(false); }
        catch { return null; }
    }

    private static async Task<List<CompCategory>?> _TryGetCategories(ICompClient client, CancellationToken ct)
    {
        try { return await client.GetCategories(ct).ConfigureAwait(false); }
        catch { return null; }
    }
}
