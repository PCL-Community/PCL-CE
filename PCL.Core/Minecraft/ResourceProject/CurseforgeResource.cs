using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CurseForge.APIClient;
using PCL.Core.App;
using PCL.Core.Minecraft.ResourceProject.Model;

namespace PCL.Core.Minecraft.ResourceProject;

public class CurseforgeResource : IResourceGetter
{
    public static CurseforgeResource Instance { get; } = new(Secrets.CurseForgeAPIKey);

    private readonly ApiClient _client;
    private readonly int _gameId;

    public CurseforgeResource(string apiKey, int gameId = 432)
    {
        _client = new ApiClient(apiKey, "ce.pclc.cc");
        _gameId = gameId;
    }

    public async Task<List<ModProject>> SearchModProjects(string query)
    {
        var response = await _client.SearchModsAsync(_gameId, searchFilter: query);
        return response.Data.Select(hit => new ModProject
        {
            Name = hit.Name,
            Id = hit.Id.ToString(),
            Slug = hit.Slug,
            Summary = hit.Summary,
            PublishedAt = hit.DateCreated.DateTime,
            UpdatedAt = hit.DateModified.DateTime,
            DownloadCount = Convert.ToInt64(hit.DownloadCount),
            FavoriteCount = hit.ThumbsUpCount,
            Categories = hit.Categories.Select(c => c.Name)
                .ToArray(),
            AuthorNames = hit.Authors.Select(a => a.Name)
                .ToArray(),
            IconUrl = hit.Logo.ThumbnailUrl,
            ScreenshotUrls = hit.Screenshots.Select(x => x.ThumbnailUrl).ToArray(),
            DirectLink = hit.Links.WebsiteUrl,
            VersionIds = hit.LatestFiles.Select(x => new ModFile
            {
                Id = x.Id.ToString(),
                ProjectId = x.ModId.ToString(),
                DisplayName = x.DisplayName,
                VersionNumber = x.FileName,
                FileName = x.FileName,
                PublishedAt = x.FileDate.DateTime,
                DownloadCount = x.DownloadCount,
                FileSizeBytes = x.FileLength,
                GameVersions = x.GameVersions.ToArray(),
                Loaders = [],
                ReleaseType = x.ReleaseType.ToString(),
                Status = x.FileStatus.ToString(),
                IsAvailable = x.IsAvailable,
                DownloadUrl = x.DownloadUrl,
                IsPrimary = x.IsEarlyAccessContent ?? false,
                Hashes = x.Modules.Select(k => new FileHash()
                {
                    Algorithm = "int",
                    Value = k.Fingerprint.ToString()
                }).ToArray(),
                Dependencies = x.Dependencies.Select(k => new ModDependency()
                {
                    ProjectId = k.ModId.ToString(),
                    RelationType = k.RelationType.ToString(),
                    VersionId = null
                }).ToArray(),
                Changelog = string.Empty
            }).ToArray(),
        }).ToList();
    }

    public async Task<ModProject> GetProject(string modId)
    {
        var id = int.Parse(modId);
        var response = await _client.GetModAsync(id);
        return new ModProject
        {
            Id = response.Data.Id.ToString(),
            Name = response.Data.Name,
            Slug = response.Data.Slug,
            Summary = response.Data.Summary,
            PublishedAt = response.Data.DateCreated.DateTime,
            UpdatedAt = response.Data.DateModified.DateTime,
            DownloadCount = Convert.ToInt32(response.Data.DownloadCount),
            FavoriteCount = response.Data.ThumbsUpCount,
            Status = response.Data.Status.ToString(),
            Categories = response.Data.Categories
                .Where(x => x.ClassId != null)
                .Select(x => x.ClassId!.ToString()).ToArray()!,
            AuthorNames = response.Data.Authors.Select(x => x.Name).ToArray(),
            IconUrl = response.Data.Logo.ThumbnailUrl,
            ScreenshotUrls = response.Data.Screenshots.Select(x => x.ThumbnailUrl).ToArray(),
            DirectLink = response.Data.Links.WebsiteUrl,
            VersionIds = response.Data.LatestFiles.Select(x => new ModFile
            {
                Id = x.Id.ToString(),
                ProjectId = x.ModId.ToString(),
                DisplayName = x.DisplayName,
                VersionNumber = x.FileName,
                FileName = x.FileName,
                PublishedAt = x.FileDate.DateTime,
                DownloadCount = x.DownloadCount,
                FileSizeBytes = x.FileLength,
                GameVersions = x.GameVersions.ToArray(),
                Loaders = [],
                ReleaseType = x.ReleaseType.ToString(),
                Status = x.FileStatus.ToString(),
                IsAvailable = x.IsAvailable,
                DownloadUrl = x.DownloadUrl,
                IsPrimary = x.IsEarlyAccessContent ?? false,
                Hashes = x.Hashes.Select(x => new FileHash(){
                    Algorithm = x.Algo.ToString().ToLower(),
                    Value = x.Value
                }).ToArray(),
                Dependencies = x.Dependencies.Select(x => new ModDependency()
                {
                    ProjectId = x.ModId.ToString(),
                    RelationType = x.RelationType.ToString(),
                    VersionId = null
                }).ToArray(),
                Changelog = null
            }).ToArray()
        };
    }

    public async Task<ModFile> GetProjectFiles(string modId, string fileId)
    {
        throw new System.NotImplementedException();
    }
}