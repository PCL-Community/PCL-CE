using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Modrinth;
using PCL.Core.App;
using PCL.Core.Minecraft.ResourceProject.Model;
using PCL.Core.Net;

namespace PCL.Core.Minecraft.ResourceProject;

public class ModrinthResource : IResourceGetter
{
    public static ModrinthResource Instance { get; } = new();
    private ModrinthClient _client;

    private ModrinthResource()
    {
        _client = new ModrinthClient(new ModrinthClientConfig(), NetworkService.GetClient());
    }

    public async Task<List<ModProject>> SearchModProjects(string query)
    {
        var response = await _client.Project.SearchAsync(query);
        return response.Hits.Select(hit => new ModProject
        {
            Name = hit.Title ?? string.Empty,
            Id = hit.ProjectId,
            Slug = hit.Slug,
            Summary = hit.Description ?? string.Empty,
            PublishedAt = hit.DateCreated,
            UpdatedAt = hit.DateModified,
            DownloadCount = hit.Downloads,
            FavoriteCount = hit.Followers,
            Categories = hit.Categories,
            AuthorNames = [hit.Author],
            IconUrl = hit.IconUrl,
            ScreenshotUrls = hit.Gallery,
            DirectLink = hit.Url,
            VersionIds = [],
        }).ToList();
    }

    public async Task<ModProject> GetProject(string modId)
    {
        throw new System.NotImplementedException();
    }

    public async Task<ModFile> GetProjectFiles(string modId, string fileId)
    {
        throw new System.NotImplementedException();
    }
}