using System;
using System.Collections.Generic;
using System.Linq;

namespace PCL.Core.Minecraft.ResourceProject;

public sealed record class ModDependencyReference
{
    public string ProjectId { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public bool IsRequired { get; init; } = true;
}

public sealed record class ModDependencyRequest
{
    public string TargetMinecraftVersion { get; init; } = string.Empty;
    public List<string> TargetLoaders { get; init; } = [];
    public List<ModDependencyReference> RequiredDependencies { get; init; } = [];
    public List<InstalledModIdentity> InstalledMods { get; init; } = [];
    public Func<string, string, ModDependencyProject?> ProjectResolver { get; init; } = (_, _) => null;
}

public sealed record class ModDependencyProject
{
    public string ProjectId { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string? ProjectName { get; init; }
    public List<ModDependencyFile> Files { get; init; } = [];
}

public sealed record class ModDependencyFile
{
    public string Id { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? Version { get; init; }
    public List<string> GameVersions { get; init; } = [];
    public List<string> Loaders { get; init; } = [];
    public int ReleaseType { get; init; }
    public DateTime ReleaseDate { get; init; }
    public List<ModDependencyReference> RequiredDependencies { get; init; } = [];
}

public sealed record class InstalledModIdentity
{
    public string? SourceProjectId { get; init; }
    public string? Source { get; init; }
    public string? ModId { get; init; }
    public List<string> GameVersions { get; init; } = [];
    public List<string> Loaders { get; init; } = [];
}

public sealed record class ModDependencyResolutionResult
{
    public List<ResolvedDependencyInstall> ToInstall { get; } = [];
    public List<UnresolvedDependency> Unresolved { get; } = [];
    public List<IgnoredDependency> Satisfied { get; } = [];
}

public sealed record class ResolvedDependencyInstall
{
    public string ProjectId { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string? ProjectName { get; init; }
    public ModDependencyFile File { get; init; } = new();
}

public sealed record class UnresolvedDependency
{
    public string ProjectId { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
}

public sealed record class IgnoredDependency
{
    public string ProjectId { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
}

public sealed class ModDependencyResolver
{
    private const int MaxDepth = 32;
    private static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;

    public ModDependencyResolutionResult Resolve(ModDependencyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.ProjectResolver);

        var context = new ResolutionContext(request);
        foreach (var dependency in request.RequiredDependencies)
        {
            ResolveDependency(context, dependency, 0);
        }

        return context.Result;
    }

    private static void ResolveDependency(ResolutionContext context, ModDependencyReference dependency, int depth)
    {
        if (string.IsNullOrWhiteSpace(dependency.ProjectId) || string.IsNullOrWhiteSpace(dependency.Source))
        {
            return;
        }

        if (!dependency.IsRequired)
        {
            context.AddSatisfied(dependency.ProjectId, dependency.Source, "Optional dependency ignored.");
            return;
        }

        if (depth > MaxDepth)
        {
            context.AddUnresolved(dependency.ProjectId, dependency.Source, "Maximum dependency depth exceeded.");
            return;
        }

        var visitedKey = context.GetVisitedKey(dependency.ProjectId, dependency.Source);
        if (!context.Visited.Add(visitedKey))
        {
            return;
        }

        if (context.IsInstalledCompatible(dependency.ProjectId, dependency.Source))
        {
            context.AddSatisfied(dependency.ProjectId, dependency.Source, "Already installed and compatible.");
            return;
        }

        var project = context.Request.ProjectResolver(dependency.Source, dependency.ProjectId);
        if (project is null)
        {
            context.AddUnresolved(dependency.ProjectId, dependency.Source, "Dependency project was not found.");
            return;
        }

        var selectedFile = SelectBestFile(project.Files, context.TargetMinecraftVersion, context.TargetLoaders);
        if (selectedFile is null)
        {
            context.AddUnresolved(project.ProjectId, project.Source, "No compatible file was found.");
            return;
        }

        context.AddInstall(project, selectedFile);

        foreach (var nestedDependency in selectedFile.RequiredDependencies)
        {
            ResolveDependency(context, nestedDependency, depth + 1);
        }
    }

    private static ModDependencyFile? SelectBestFile(
        IEnumerable<ModDependencyFile> files,
        string targetMinecraftVersion,
        HashSet<string> targetLoaders)
    {
        return files
            .Where(file => IsCompatibleFile(file, targetMinecraftVersion, targetLoaders))
            .OrderByDescending(file => HasExactGameVersionMatch(file, targetMinecraftVersion))
            .ThenByDescending(file => HasLoaderMatch(file, targetLoaders))
            .ThenBy(file => NormalizeReleaseType(file.ReleaseType))
            .ThenByDescending(file => file.ReleaseDate)
            .FirstOrDefault();
    }

    private static bool IsCompatibleFile(ModDependencyFile file, string targetMinecraftVersion, HashSet<string> targetLoaders)
    {
        if (!HasExactGameVersionMatch(file, targetMinecraftVersion))
        {
            return false;
        }

        if (targetLoaders.Count == 0)
        {
            return true;
        }

        if (file.Loaders.Count == 0)
        {
            return true;
        }

        return file.Loaders.Any(loader => targetLoaders.Contains(loader));
    }

    private static bool HasExactGameVersionMatch(ModDependencyFile file, string targetMinecraftVersion)
    {
        return file.GameVersions.Any(version => Comparer.Equals(version, targetMinecraftVersion));
    }

    private static bool HasLoaderMatch(ModDependencyFile file, HashSet<string> targetLoaders)
    {
        if (targetLoaders.Count == 0)
        {
            return true;
        }

        return file.Loaders.Any(loader => targetLoaders.Contains(loader));
    }

    private static int NormalizeReleaseType(int releaseType)
    {
        return releaseType switch
        {
            1 => 1,
            2 => 2,
            3 => 3,
            _ => int.MaxValue,
        };
    }

    private sealed class ResolutionContext
    {
        private readonly HashSet<string> _installDedupe = new(Comparer);
        private readonly HashSet<string> _unresolvedDedupe = new(Comparer);
        private readonly HashSet<string> _satisfiedDedupe = new(Comparer);

        public ResolutionContext(ModDependencyRequest request)
        {
            Request = request;
            Result = new ModDependencyResolutionResult();
            Visited = new HashSet<string>(Comparer);
            TargetMinecraftVersion = request.TargetMinecraftVersion ?? string.Empty;
            TargetLoaders = new HashSet<string>(
                request.TargetLoaders.Where(static loader => !string.IsNullOrWhiteSpace(loader)),
                Comparer);
            LoaderSetKey = string.Join(",", TargetLoaders.OrderBy(static loader => loader, Comparer));
        }

        public ModDependencyRequest Request { get; }
        public ModDependencyResolutionResult Result { get; }
        public HashSet<string> Visited { get; }
        public string TargetMinecraftVersion { get; }
        public HashSet<string> TargetLoaders { get; }
        private string LoaderSetKey { get; }

        public string GetVisitedKey(string projectId, string source)
        {
            return $"{source}:{projectId}:{TargetMinecraftVersion}:{LoaderSetKey}";
        }

        public bool IsInstalledCompatible(string projectId, string source)
        {
            return Request.InstalledMods.Any(installed =>
                Comparer.Equals(installed.SourceProjectId, projectId)
                && Comparer.Equals(installed.Source, source)
                && installed.GameVersions.Any(version => Comparer.Equals(version, TargetMinecraftVersion))
                && LoadersCompatible(installed.Loaders));
        }

        public void AddInstall(ModDependencyProject project, ModDependencyFile file)
        {
            var dedupeKey = GetProjectKey(project.ProjectId, project.Source);
            if (!_installDedupe.Add(dedupeKey))
            {
                return;
            }

            Result.ToInstall.Add(new ResolvedDependencyInstall
            {
                ProjectId = project.ProjectId,
                Source = project.Source,
                ProjectName = project.ProjectName,
                File = file,
            });
        }

        public void AddUnresolved(string projectId, string source, string reason)
        {
            var dedupeKey = GetProjectKey(projectId, source);
            if (!_unresolvedDedupe.Add(dedupeKey))
            {
                return;
            }

            Result.Unresolved.Add(new UnresolvedDependency
            {
                ProjectId = projectId,
                Source = source,
                Reason = reason,
            });
        }

        public void AddSatisfied(string projectId, string source, string reason)
        {
            var dedupeKey = GetProjectKey(projectId, source);
            if (!_satisfiedDedupe.Add(dedupeKey))
            {
                return;
            }

            Result.Satisfied.Add(new IgnoredDependency
            {
                ProjectId = projectId,
                Source = source,
                Reason = reason,
            });
        }

        private bool LoadersCompatible(List<string> installedLoaders)
        {
            if (TargetLoaders.Count == 0 || installedLoaders.Count == 0)
            {
                return true;
            }

            return installedLoaders.Any(loader => TargetLoaders.Contains(loader));
        }

        private static string GetProjectKey(string projectId, string source)
        {
            return $"{source}:{projectId}";
        }
    }
}
