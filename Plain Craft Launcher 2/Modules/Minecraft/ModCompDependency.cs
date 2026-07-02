using System.IO;
using CompFile = PCL.ModComp.CompFile;
using CompFileStatus = PCL.ModComp.CompFileStatus;
using CompLoaderType = PCL.ModComp.CompLoaderType;
using CompProject = PCL.ModComp.CompProject;
using LocalCompFile = PCL.ModLocalComp.LocalCompFile;
using PCL.Core.Minecraft.ResourceProject;
using PCL.Core.App.Localization;
using PCL.Network;

namespace PCL;

public static class ModCompDependency
{
    public static ModDependencyRequest BuildRequest(
        CompFile file,
        CompProject project,
        string targetMinecraftVersion,
        List<CompLoaderType> targetLoaders,
        string targetModsFolder)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(project);
        targetLoaders ??= new List<CompLoaderType>();

        var source = GetSource(project.FromCurseForge);
        var dependencies = file.Dependencies
            .Where(static dependencyId => !string.IsNullOrWhiteSpace(dependencyId))
            .Select(dependencyId => new ModDependencyReference
            {
                ProjectId = dependencyId,
                Source = source,
                IsRequired = true,
            })
            .Concat(file.OptionalDependencies
                .Where(static dependencyId => !string.IsNullOrWhiteSpace(dependencyId))
                .Select(dependencyId => new ModDependencyReference
                {
                    ProjectId = dependencyId,
                    Source = source,
                    IsRequired = false,
                }))
            .ToList();

        return new ModDependencyRequest
        {
            TargetMinecraftVersion = targetMinecraftVersion ?? string.Empty,
            TargetLoaders = ToLoaderNames(targetLoaders),
            RequiredDependencies = dependencies,
            InstalledMods = ScanInstalledMods(targetModsFolder),
            ProjectResolver = ResolveProjectFiles,
        };
    }

    public static List<InstalledModIdentity> ScanInstalledMods(string targetModsFolder)
    {
        var result = new List<InstalledModIdentity>();
        if (string.IsNullOrWhiteSpace(targetModsFolder) || !Directory.Exists(targetModsFolder))
        {
            return result;
        }

        foreach (var path in Directory.GetFiles(targetModsFolder))
        {
            if (!LocalCompFile.IsModFile(path))
            {
                continue;
            }

            var localFile = new LocalCompFile(path);
            localFile.Load();

            var source = localFile.Comp is null ? null : GetSource(localFile.Comp.FromCurseForge);
            var gameVersions = localFile.compFile?.GameVersions?.Where(static version => !string.IsNullOrWhiteSpace(version)).ToList()
                               ?? new List<string>();
            var loaders = ToLoaderNames(localFile.compFile?.ModLoaders);

            if (!string.IsNullOrWhiteSpace(localFile.Comp?.Id) && !string.IsNullOrWhiteSpace(source))
            {
                result.Add(new InstalledModIdentity
                {
                    SourceProjectId = localFile.Comp.Id,
                    Source = source,
                    ModId = localFile.ModId,
                    GameVersions = gameVersions,
                    Loaders = loaders,
                });
                continue;
            }

            if (!string.IsNullOrWhiteSpace(localFile.compFile?.ProjectId))
            {
                var fileSource = GetSource(localFile.compFile.FromCurseForge);
                result.Add(new InstalledModIdentity
                {
                    SourceProjectId = localFile.compFile.ProjectId,
                    Source = fileSource,
                    ModId = localFile.ModId,
                    GameVersions = gameVersions,
                    Loaders = loaders,
                });
                continue;
            }

            result.Add(new InstalledModIdentity
            {
                SourceProjectId = null,
                Source = null,
                ModId = localFile.ModId,
                GameVersions = gameVersions,
                Loaders = loaders,
            });
        }

        return result;
    }

    public static ModDependencyProject? ResolveProjectFiles(string source, string projectId)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(projectId))
        {
            return null;
        }

        var fromCurseForge = string.Equals(source, "CurseForge", StringComparison.OrdinalIgnoreCase);
        var files = ModComp.CompFilesGet(projectId, fromCurseForge);
        if (!ModComp.compProjectCache.TryGetValue(projectId, out var compProject))
        {
            return null;
        }

        if (compProject.FromCurseForge != fromCurseForge)
        {
            return null;
        }

        return new ModDependencyProject
        {
            ProjectId = compProject.Id,
            Source = source,
            ProjectName = compProject.TranslatedName ?? compProject.RawName,
            Files = files.Select(compFile => new ModDependencyFile
            {
                Id = compFile.Id,
                DisplayName = compFile.DisplayName,
                Version = compFile.Version,
                GameVersions = compFile.GameVersions?.Where(static version => !string.IsNullOrWhiteSpace(version)).ToList()
                               ?? new List<string>(),
                Loaders = ToLoaderNames(compFile.ModLoaders),
                ReleaseType = MapReleaseType(compFile.Status),
                ReleaseDate = compFile.ReleaseDate,
                RequiredDependencies = compFile.Dependencies
                    .Where(static dependencyId => !string.IsNullOrWhiteSpace(dependencyId))
                    .Select(dependencyId => new ModDependencyReference
                    {
                        ProjectId = dependencyId,
                        Source = source,
                        IsRequired = true,
                    })
                    .ToList(),
            }).ToList(),
        };
    }

    public static ModDependencyFile? SelectCompatibleDependencyFile(
        ModDependencyResolutionResult result,
        string projectId,
        string source)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.ToInstall
            .FirstOrDefault(install =>
                string.Equals(install.ProjectId, projectId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(install.Source, source, StringComparison.OrdinalIgnoreCase))
            ?.File;
    }

    public static List<(string Filename, DownloadFile File)> BuildDependencyDownloads(
        ModDependencyResolutionResult result,
        string targetModsFolder)
    {
        ArgumentNullException.ThrowIfNull(result);

        var downloads = new List<(string, DownloadFile)>();
        foreach (var install in result.ToInstall.AsEnumerable().Reverse())
        {
            if (!ModComp.compProjectCache.TryGetValue(install.ProjectId, out var depProject))
            {
                continue;
            }

            var fromCurseForge = string.Equals(install.Source, "CurseForge", StringComparison.OrdinalIgnoreCase);
            if (depProject.FromCurseForge != fromCurseForge)
            {
                continue;
            }

            var depCompFile = ModComp.CompFilesGet(install.ProjectId, fromCurseForge)
                .FirstOrDefault(file => string.Equals(file.Id, install.File.Id, StringComparison.OrdinalIgnoreCase));
            if (depCompFile is null)
            {
                continue;
            }

            var targetPath = Path.Combine(targetModsFolder ?? string.Empty, ModComp.CompFileNameGet(depProject, depCompFile));
            downloads.Add((depCompFile.FileName, depCompFile.ToNetFile(targetPath)));
        }

        return downloads;
    }

    /// <summary>
    ///     Shows confirmation dialog for required dependency installs.
    ///     Returns: CompDepsInstallTypes.WithDeps if user chooses to install with deps, 
    ///              CompDepsInstallTypes.WithoutDeps if user chooses to install without deps, 
    ///              CompDepsInstallTypes.Cancel if user cancels, 
    ///              CompDepsInstallTypes.Unresolved if there are unresolved required deps.
    /// </summary>
    public static ModComp.CompDepsInstallTypes ConfirmDependencyInstall(ModDependencyResolutionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Unresolved is { Count: > 0 })
        {
            ModBase.Log($"[CompDeps] 无法解析: {result.Unresolved.Count} 个必需前置");
            var dependencies = string.Join(
                Environment.NewLine,
                result.Unresolved.Select(dep => Lang.Text(
                    "Download.Comp.Dependency.Unresolved.ListItem",
                    dep.Source,
                    dep.ProjectId,
                    dep.Reason)));
            var message = Lang.Text("Download.Comp.Dependency.Unresolved.Message", dependencies);
            var selectedButton = ModMain.MyMsgBox(
                message,
                Lang.Text("Download.Comp.Dependency.Unresolved.Title"),
                Lang.Text("Download.Comp.Dependency.Unresolved.ContinueWithoutDependencies"),
                Lang.Text("Common.Action.Cancel"),
                isWarn: true,
                forceWait: true);

            return selectedButton == 1
                ? ModComp.CompDepsInstallTypes.Unresolved
                : ModComp.CompDepsInstallTypes.Cancel;
        }

        if (result.ToInstall is { Count: > 0 })
        {
            var dependencies = string.Join(
                Environment.NewLine,
                result.ToInstall.Select(install => Lang.Text(
                    "Download.Comp.Dependency.Install.ListItem",
                    install.ProjectName,
                    install.Source,
                    install.File.DisplayName,
                    install.File.Version)));
            var message = Lang.Text("Download.Comp.Dependency.Install.Message", dependencies);
            var dialogResult = ModMain.MyMsgBox(
                message,
                Lang.Text("Download.Comp.Dependency.Install.Title"),
                Lang.Text("Download.Comp.Dependency.Install.WithDependencies"),
                Lang.Text("Download.Comp.Dependency.Install.WithoutDependencies"),
                Lang.Text("Download.Comp.Dependency.Install.Cancel"),
                forceWait: true);

            return dialogResult switch
            {
                1 => ModComp.CompDepsInstallTypes.WithDeps,
                2 => ModComp.CompDepsInstallTypes.WithoutDeps,
                3 => ModComp.CompDepsInstallTypes.Cancel,
                _ => ModComp.CompDepsInstallTypes.Cancel
            };
        }

        return ModComp.CompDepsInstallTypes.WithDeps;
    }

    /// <summary>
    ///     Shows abort message when dependency resolution was cancelled by user or failed.
    /// </summary>
    public static void ShowDependencyAbortMessage(string reason)
    {
        ModMain.MyMsgBox(
            Lang.Text("Download.Comp.Dependency.Abort.Message", reason),
            Lang.Text("Download.Comp.Dependency.Abort.Title"),
            Lang.Text("Common.Action.Confirm"),
            isWarn: false,
            forceWait: true);
    }

    private static string GetSource(bool fromCurseForge)
    {
        return fromCurseForge ? "CurseForge" : "Modrinth";
    }

    private static List<string> ToLoaderNames(IEnumerable<CompLoaderType>? loaders)
    {
        if (loaders is null)
        {
            return new List<string>();
        }

        return loaders
            .Where(static loader => loader != CompLoaderType.Any)
            .Select(static loader => loader.ToString())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int MapReleaseType(CompFileStatus status)
    {
        return status switch
        {
            CompFileStatus.Release => 1,
            CompFileStatus.Beta => 2,
            CompFileStatus.Alpha => 3,
            _ => 1,
        };
    }
}
