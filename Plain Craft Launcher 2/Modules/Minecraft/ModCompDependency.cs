using System.IO;
using CompFile = PCL.ModComp.CompFile;
using CompFileStatus = PCL.ModComp.CompFileStatus;
using CompLoaderType = PCL.ModComp.CompLoaderType;
using CompProject = PCL.ModComp.CompProject;
using LocalCompFile = PCL.ModLocalComp.LocalCompFile;
using PCL.Core.Minecraft.ResourceProject;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Network;
using PCL.Network.Loaders;

namespace PCL;

/// <summary>前置安装的处理结果：取消 / 无需处理 / 跳过 / 已开始下载。</summary>
public enum CompDependencyInstallResult
{
    Cancel,
    Nothing,
    Skip,
    Install
}

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

            var depFileName = ModComp.CompFileNameGet(depProject, depCompFile);
            var targetPath = Path.Combine(targetModsFolder ?? string.Empty, depFileName);
            downloads.Add((depFileName, depCompFile.ToNetFile(targetPath, ModComp.DownloadReason.Dependency)));
        }

        return downloads;
    }

    /// <summary>
    ///     解析必需前置并下载到目标文件夹；<paramref name="installDependencies" /> 为 null 时询问用户，
    ///     为 false 时跳过，为 true 时直接安装。可在工作线程调用。
    /// </summary>
    public static CompDependencyInstallResult InstallRequiredDependencies(
        CompFile file,
        CompProject project,
        McInstance? targetInstance,
        string targetModsFolder,
        bool? installDependencies)
    {
        if (file.Type != ModComp.CompType.Mod || !file.Dependencies.Any())
        {
            return CompDependencyInstallResult.Nothing;
        }

        if (installDependencies == false)
        {
            ModBase.Log("[CompDeps] 已关闭前置安装，跳过前置处理");
            return CompDependencyInstallResult.Skip;
        }

        try
        {
            if (targetInstance is not null && !targetInstance.IsLoaded)
            {
                targetInstance.Load();
            }

            var mcVersion = targetInstance?.Info?.VanillaName
                            ?? file.GameVersions.FirstOrDefault(static version => version.Contains('.'))
                            ?? string.Empty;
            var targetLoaders = new List<CompLoaderType>();
            if (targetInstance is not null)
            {
                if (targetInstance.Info.HasForge || targetInstance.Info.HasCleanroom)
                {
                    targetLoaders.Add(CompLoaderType.Forge);
                }

                if (targetInstance.Info.HasFabric || targetInstance.Info.HasLegacyFabric)
                {
                    targetLoaders.Add(CompLoaderType.Fabric);
                }

                if (targetInstance.Info.HasQuilt)
                {
                    targetLoaders.Add(CompLoaderType.Quilt);
                }

                if (targetInstance.Info.HasNeoForge)
                {
                    targetLoaders.Add(CompLoaderType.NeoForge);
                }

                if (targetInstance.Info.HasLiteLoader)
                {
                    targetLoaders.Add(CompLoaderType.LiteLoader);
                }
            }

            if (targetLoaders.Count == 0)
            {
                targetLoaders = (file.ModLoaders.Any() ? file.ModLoaders : project.ModLoaders).ToList();
            }

            ModBase.Log($"[CompDeps] 开始解析必需前置: {file.Dependencies.Count} 个依赖");
            var request = BuildRequest(file, project, mcVersion, targetLoaders, targetModsFolder);
            var resolver = new ModDependencyResolver();
            var result = resolver.Resolve(request);

            if (!result.Unresolved.Any() && !result.ToInstall.Any())
            {
                ModBase.Log("[CompDeps] 已满足: 所有必需前置已安装");
                return CompDependencyInstallResult.Nothing;
            }

            if (installDependencies is null)
            {
                var installChoice = ModBase.RunInUiWait(() => ConfirmDependencyInstall(result));
                switch (installChoice)
                {
                    case ModComp.CompDepsInstallTypes.Cancel:
                        ModBase.Log("[CompDeps] 用户取消下载");
                        return CompDependencyInstallResult.Cancel;

                    case ModComp.CompDepsInstallTypes.WithoutDeps:
                        ModBase.Log("[CompDeps] 用户选择仅下载 Mod 本体，跳过前置下载");
                        return CompDependencyInstallResult.Skip;
                }
            }

            if (!result.ToInstall.Any())
            {
                ModBase.Log("[CompDeps] 所有前置均无法解析，仅下载 Mod 本体");
                return CompDependencyInstallResult.Nothing;
            }

            DownloadDependencies(result, targetModsFolder);
            return CompDependencyInstallResult.Install;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[CompDeps] 依赖解析失败，跳过前置安装");
            var message = ExceptionDetails.Compose(
                Lang.Text("Download.Comp.Dependency.ResolveFailed.Message"),
                ex);
            ModBase.RunInUiWait(() => ModMain.MyMsgBox(
                message,
                Lang.Text("Download.Comp.Dependency.ResolveFailed.Title"),
                Lang.Text("Download.Comp.Dependency.ResolveFailed.Continue"),
                isWarn: true,
                forceWait: true));
            return CompDependencyInstallResult.Nothing;
        }
    }

    /// <summary>启动前置文件的下载任务。</summary>
    private static void DownloadDependencies(ModDependencyResolutionResult result, string targetModsFolder)
    {
        ModBase.Log($"[CompDeps] 准备下载: {result.ToInstall.Count} 个前置");
        var depDownloads = BuildDependencyDownloads(result, targetModsFolder);
        foreach (var (depFilename, downloadFile) in depDownloads)
        {
            var depLoaderName = Lang.Text("Download.Comp.Detail.DownloadResource",
                Lang.Text("Download.Comp.Type.Mod"),
                ModBase.GetFileNameWithoutExtentionFromPath(depFilename));
            var depLoaders = new List<ModLoader.LoaderBase>
            {
                new LoaderDownload(Lang.Text("Download.Comp.Detail.DownloadFile"),
                    new List<DownloadFile> { downloadFile })
                {
                    ProgressWeight = 6,
                    block = true
                }
            };

            var depLoader = new ModLoader.LoaderCombo<int>(depLoaderName, depLoaders);
            depLoader.OnStateChanged = ModDownloadLib.LoaderStateChangedHintOnly;
            depLoader.Start(1);
            ModLoader.LoaderTaskbarAdd(depLoader);
        }
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
            var dialogResult = ModMain.DependencyInstallMsgBox(result);

            return dialogResult switch
            {
                1 => ModComp.CompDepsInstallTypes.WithDeps,
                2 => ModComp.CompDepsInstallTypes.WithoutDeps,
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
