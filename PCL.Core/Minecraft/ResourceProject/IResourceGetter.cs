using System.Collections.Generic;
using System.Threading.Tasks;
using PCL.Core.Minecraft.ResourceProject.Model;

namespace PCL.Core.Minecraft.ResourceProject;

public interface IResourceGetter
{
    public Task<List<ModProject>> SearchModProjects(string query);
    public Task<ModProject> GetProject(string modId);
    public Task<ModFile> GetProjectFiles(string modId, string fileId);
}