using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Minecraft.ResourceProject.Comp.Models;
using PCL.Core.Minecraft.ResourceProject.Comp.Models.Enums;

namespace PCL.Core.Minecraft.ResourceProject.Comp.Abstractions;

public interface ICompClient
{
    Task<CompSearchResult> SearchProjects(CompSearchFilter filter, CancellationToken ct = default);
    Task<CompProject> GetProject(string projectId, CancellationToken ct = default);
    Task<List<CompProject>> GetProjects(IEnumerable<string> projectIds, CancellationToken ct = default);
    Task<List<CompProject>> GetFeaturedProjects(string? gameVersion = null, CancellationToken ct = default);
    Task<string> GetProjectDescription(string projectId, CancellationToken ct = default);

    Task<List<CompFile>> GetProjectFiles(string projectId, CompSearchFilter? filter = null, CancellationToken ct = default);
    Task<CompFile> GetFile(string fileId, CancellationToken ct = default);
    Task<string> GetFileDownloadUrl(string fileId, CancellationToken ct = default);
    Task<string> GetFileChangelog(string fileId, CancellationToken ct = default);

    Task<Dictionary<string, List<CompFile>>> MatchFingerprints(
        IEnumerable<string> hashes, HashAlgorithm algo, CancellationToken ct = default);
    Task<CompFile?> CheckForUpdates(
        string fileHash, HashAlgorithm algo, CompSearchFilter? filter = null, CancellationToken ct = default);

    Task<List<CompGameVersion>> GetGameVersions(CancellationToken ct = default);
    Task<List<CompLoader>> GetLoaders(CancellationToken ct = default);
    Task<List<CompCategory>> GetCategories(CancellationToken ct = default);
}
