using System.IO;
using CompFile = PCL.ModComp.CompFile;
using CompFileStatus = PCL.ModComp.CompFileStatus;
using CompLoaderType = PCL.ModComp.CompLoaderType;
using CompProject = PCL.ModComp.CompProject;
using LocalCompFile = PCL.ModLocalComp.LocalCompFile;
using PCL.Core.Minecraft.ResourceProject;
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

        var source = GetSource(project.fromCurseForge);
        var dependencies = file.dependencies
            .Where(static dependencyId => !string.IsNullOrWhiteSpace(dependencyId))
            .Select(dependencyId => new ModDependencyReference
            {
                ProjectId = dependencyId,
                Source = source,
                IsRequired = true,
            })
            .Concat(file.optionalDependencies
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

            var source = localFile.Comp is null ? null : GetSource(localFile.Comp.fromCurseForge);
            var gameVersions = localFile.compFile?.gameVersions?.Where(static version => !string.IsNullOrWhiteSpace(version)).ToList()
                               ?? new List<string>();
            var loaders = ToLoaderNames(localFile.compFile?.modLoaders);

            if (!string.IsNullOrWhiteSpace(localFile.Comp?.id) && !string.IsNullOrWhiteSpace(source))
            {
                result.Add(new InstalledModIdentity
                {
                    SourceProjectId = localFile.Comp.id,
                    Source = source,
                    ModId = localFile.ModId,
                    GameVersions = gameVersions,
                    Loaders = loaders,
                });
                continue;
            }

            if (!string.IsNullOrWhiteSpace(localFile.compFile?.projectId))
            {
                var fileSource = GetSource(localFile.compFile.fromCurseForge);
                result.Add(new InstalledModIdentity
                {
                    SourceProjectId = localFile.compFile.projectId,
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

        if (compProject.fromCurseForge != fromCurseForge)
        {
            return null;
        }

        return new ModDependencyProject
        {
            ProjectId = compProject.id,
            Source = source,
            ProjectName = compProject.TranslatedName ?? compProject.rawName,
            RequiredDependencies = files
                .SelectMany(static compFile => compFile.dependencies)
                .Where(static dependencyId => !string.IsNullOrWhiteSpace(dependencyId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(dependencyId => new ModDependencyReference
                {
                    ProjectId = dependencyId,
                    Source = source,
                    IsRequired = true,
                })
                .ToList(),
            Files = files.Select(compFile => new ModDependencyFile
            {
                Id = compFile.id,
                DisplayName = compFile.displayName,
                Version = compFile.version,
                GameVersions = compFile.gameVersions?.Where(static version => !string.IsNullOrWhiteSpace(version)).ToList()
                               ?? new List<string>(),
                Loaders = ToLoaderNames(compFile.modLoaders),
                ReleaseType = MapReleaseType(compFile.status),
                ReleaseDate = compFile.releaseDate,
                RequiredDependencies = compFile.dependencies
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

    public static List<DownloadFile> BuildDependencyDownloads(
        ModDependencyResolutionResult result,
        string targetModsFolder)
    {
        ArgumentNullException.ThrowIfNull(result);

        var downloads = new List<DownloadFile>();
        foreach (var install in result.ToInstall.AsEnumerable().Reverse())
        {
            if (!ModComp.compProjectCache.TryGetValue(install.ProjectId, out var depProject))
            {
                continue;
            }

            var fromCurseForge = string.Equals(install.Source, "CurseForge", StringComparison.OrdinalIgnoreCase);
            if (depProject.fromCurseForge != fromCurseForge)
            {
                continue;
            }

            var depCompFile = ModComp.CompFilesGet(install.ProjectId, fromCurseForge)
                .FirstOrDefault(file => string.Equals(file.id, install.File.Id, StringComparison.OrdinalIgnoreCase));
            if (depCompFile is null)
            {
                continue;
            }

            var targetPath = Path.Combine(targetModsFolder ?? string.Empty, ModComp.CompFileNameGet(depProject, depCompFile));
            downloads.Add(depCompFile.ToNetFile(targetPath));
        }

        return downloads;
    }

    /// <summary>
    ///     Shows confirmation dialog for required dependency installs.
    ///     Returns true if user confirms, false if user cancels or there are unresolved required deps.
    /// </summary>
    public static bool ConfirmDependencyInstall(ModDependencyResolutionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Unresolved is { Count: > 0 })
        {
            ModBase.Log($"[CompDeps] 无法解析: {result.Unresolved.Count} 个必需前置");
            var message = "以下必需前置无法解析：\n\n" +
                          string.Join("\n", result.Unresolved
                              .Select(dep => $"- {dep.Source} {dep.ProjectId}: {dep.Reason}"));
            ModMain.MyMsgBox(message, "无法安装必需前置", Button1: "确定", IsWarn: true, ForceWait: true);
            return false;
        }

        if (result.ToInstall is { Count: > 0 })
        {
            var message = "此 Mod 需要以下必需前置：\n\n" +
                          string.Join("\n", result.ToInstall
                              .Select(install =>
                                  $"- {install.ProjectName} ({install.Source}) - {install.File.DisplayName} v{install.File.Version}"));
            var dialogResult = ModMain.MyMsgBox(message, "安装 Mod 前置确认",
                Button1: "安装 Mod 与必需前置", Button2: "取消安装", ForceWait: true);
            if (dialogResult != 1)
            {
                ModBase.Log("[CompDeps] 用户取消，已中止安装");
            }
            return dialogResult == 1;
        }

        return true;
    }

    /// <summary>
    ///     Shows abort message when dependency resolution was cancelled by user or failed.
    /// </summary>
    public static void ShowDependencyAbortMessage(string reason)
    {
        ModMain.MyMsgBox(reason, "安装已中止", Button1: "确定", IsWarn: false, ForceWait: true);
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
