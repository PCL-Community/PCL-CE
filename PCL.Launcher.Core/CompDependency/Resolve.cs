using PCL.Core.Minecraft.ResourceProject;
using PCL.Network;

namespace PCL;

public static partial class CompDependency
{
    public static List<(string Filename, DownloadFile File)> BuildDependencyDownloads(
        ModDependencyResolutionResult result,
        string targetModsFolder)
    {
        ArgumentNullException.ThrowIfNull(result);

        var downloads = new List<(string, DownloadFile)>();
        foreach (var install in result.ToInstall.AsEnumerable().Reverse())
        {
            if (!Comp.compProjectCache.TryGetValue(
                    install.ProjectId,
                    out var depProject))
                continue;

            var fromCurseForge = string.Equals(
                install.Source,
                "CurseForge",
                StringComparison.OrdinalIgnoreCase);
            if (depProject.FromCurseForge != fromCurseForge)
                continue;

            var depCompFile = Comp
                .CompFilesGet(install.ProjectId, fromCurseForge)
                .FirstOrDefault(file => string.Equals(
                    file.Id,
                    install.File.Id,
                    StringComparison.OrdinalIgnoreCase));
            if (depCompFile is null)
                continue;

            var depFileName = Comp.CompFileNameGet(depProject, depCompFile);
            var targetPath = Path.Combine(
                targetModsFolder ?? string.Empty,
                depFileName);
            downloads.Add(
                (
                    depFileName,
                    depCompFile.ToNetFile(targetPath, Comp.DownloadReason.Dependency)));
        }

        return downloads;
    }
}