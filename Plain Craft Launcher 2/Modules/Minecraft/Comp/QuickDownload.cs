using System.IO;
using System.Text;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.Core.UI;
using PCL.Core.UI.Dialogs;
using PCL.Network.Loaders;

namespace PCL;

public static partial class Comp
{
    /// <summary>
    ///     资源卡片的快速下载入口：按 <see cref="Config.Download.Comp.QuickDownloadBehavior" /> 指定的行为，
    ///     下载该资源最新（优先 Release、其次最新发布）的兼容版本到目标实例或文件夹。
    ///     由 <see cref="MyCompItem" /> 上的快速下载按钮调用。快速下载不会自动安装前置。
    /// </summary>
    public static void QuickDownload(CompProject project)
    {
        Common.RunInNewThread(
            () =>
            {
                try
                {
                    HintService.Show(Hints.Info(Lang.Text("Download.Comp.QuickDownload.Hint.Loading")));
                    var files = FilterFilesByType(
                        [
                            .. CompFilesGet(project.Id, project.FromCurseForge)
                                .Where(f => f.Available)
                        ],
                        project.Type);
                    if (files.Count == 0)
                    {
                        HintService.Show(Hints.Info(Lang.Text("Download.Comp.QuickDownload.Hint.NoFile")));
                        return;
                    }

                    var behavior = Config.Download.Comp.QuickDownloadBehavior;
                    if (behavior == 0)
                    {
                        var choice = Common.RunInUiWait(() =>
                        {
                            var options = new List<IMyRadio>
                            {
                                new MyRadioBox
                                {
                                    Text = Lang.Text(
                                        "Download.Comp.QuickDownload.ChooseMethod.CurrentInstance")
                                },
                                new MyRadioBox
                                {
                                    Text = Lang.Text(
                                        "Download.Comp.QuickDownload.ChooseMethod.AskInstance")
                                },
                                new MyRadioBox
                                {
                                    Text = Lang.Text(
                                        "Download.Comp.QuickDownload.ChooseMethod.AskPath")
                                }
                            };
                            return DialogService.Instance.Show(new SelectDialogRequest
                            {
                                Title = Lang.Text("Download.Comp.QuickDownload.ChooseMethod.Title"),
                                Options = PCL.DialogOptions.From(options),
                                Buttons = DialogButtons.From(
                                    DialogButton.Create(Lang.Text("Common.Action.Continue")),
                                    DialogButton.Create(Lang.Text("Common.Action.Cancel"))),
                                HighLight = false,
                                ForceWait = true
                            }).Result?.SelectedIndex;
                        });
                        if (choice is null) return;
                        behavior = choice.Value + 1;
                    }

                    switch (behavior)
                    {
                        case 1:
                            _QuickDownloadToInstance(
                                project,
                                files,
                                InstanceList.McMcInstanceSelected);
                            break;
                        case 2:
                        {
                            var instances = _QuickDownloadPickInstances(project, files);
                            if (instances is null) return;
                            var startedTargets =
                                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            foreach (var instance in instances)
                                _QuickDownloadToInstance(project, files, instance, startedTargets);
                            break;
                        }
                        case 3:
                            _QuickDownloadToFolder(project, files);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Common.Log(
                        ex,
                        "[Comp] 快速下载失败",
                        Common.LogLevel.Feedback,
                        userSummary: Lang.Text("Minecraft.Comp.Error.OperationFailed"));
                }
            },
            "Comp QuickDownload");
    }

    /// <summary>下载到指定实例的最新兼容版本。</summary>
    private static void _QuickDownloadToInstance(
        CompProject project,
        List<CompFile> files,
        McInstance? instance,
        ISet<string>? startedTargets = null)
    {
        if (instance is null)
        {
            HintService.Show(Hints.Info(Lang.Text("Download.Comp.QuickDownload.Hint.NoInstance")));
            return;
        }

        if (!instance.IsLoaded) instance.Load();
        var compatible = files
            .Where(f => IsInstanceSuitableForFile(instance, f, _ResolveLoaders(f, project)))
            .ToList();
        var file = _PickLatestFile(compatible);
        if (file is null)
        {
            HintService.Show(Hints.Info(Lang.Text("Download.Comp.QuickDownload.Hint.NoCompatibleFile")));
            return;
        }

        var folder = instance.PathIndie + _GetSubFolder(project.Type);
        Directory.CreateDirectory(folder);
        var target = Path.Combine(folder, CompFileNameGet(project, file));
        if (startedTargets is not null && !startedTargets.Add(target))
            return;
        _StartQuickDownload(file, target);
        HintService.Show(Hints.Success(Lang.Text("Download.Comp.QuickDownload.Hint.DownloadStarted", project.RawName)));
    }

    /// <summary>弹实例列表让用户选择，返回选中的实例（兼容者优先、当前选中实例居首）；取消或无兼容实例返回 null。</summary>
    private static List<McInstance>? _QuickDownloadPickInstances(
        CompProject project,
        List<CompFile> files)
    {
        var needLoad = InstanceList.mcInstanceListLoader.State != Common.LoadState.Finished;
        if (needLoad)
        {
            if (string.IsNullOrEmpty(GameFolders.mcFolderSelected)) return null;
            HintService.Show(Hints.Info(Lang.Text("Download.Comp.QuickDownload.Hint.Loading")));
            Loading.LoaderFolderRun(
                InstanceList.mcInstanceListLoader,
                GameFolders.mcFolderSelected,
                Loading.LoaderFolderRunType.ForceRun,
                1,
                "versions\\",
                true);
        }

        var compatible = InstanceList
            .mcInstanceList.Values
            .SelectMany(l => l)
            .Where(v =>
                v is not null
                && files.Any(f => IsInstanceSuitableForFile(
                    v,
                    f,
                    _ResolveLoaders(f, project))))
            .ToList();
        if (compatible.Count == 0)
        {
            HintService.Show(Hints.Info(Lang.Text("Download.Comp.QuickDownload.Hint.NoCompatibleInstance")));
            return null;
        }

        var current = InstanceList.McMcInstanceSelected;
        if (current is not null)
            compatible =
            [
                .. compatible
                    .OrderBy(v => v == current ? 0 : 1)
                    .ThenBy(v => v.Name)
            ];
        var indices = Common.RunInUiWait(() =>
        {
            var options = compatible
                .Select(v => new MyListItem
                {
                    Title = v.Name
                })
                .ToList();
            return DialogService.Instance.Show(new SelectDialogRequest
            {
                Title = Lang.Text("Download.Comp.QuickDownload.ChooseInstance.Title"),
                Options = PCL.DialogOptions.From(options),
                MultiSelect = true,
                Buttons = DialogButtons.From(
                    DialogButton.Create(Lang.Text("Common.Action.Continue")),
                    DialogButton.Create(Lang.Text("Common.Action.Cancel"))),
                HighLight = false,
                ForceWait = true
            }).Result?.SelectedIndices?.ToList();
        });
        if (indices is null) return null;
        return [.. indices.Select(i => compatible[i])];
    }

    /// <summary>下载最新版本到用户选择的文件夹。</summary>
    private static void _QuickDownloadToFolder(CompProject project, List<CompFile> files)
    {
        var file = _PickLatestFile(files);
        if (file is null)
        {
            HintService.Show(Hints.Info(Lang.Text("Download.Comp.QuickDownload.Hint.NoFile")));
            return;
        }

        var saveFolder = Common.RunInUiWait(() =>
            SystemDialogs.SelectFolder(Lang.Text("Download.Comp.QuickDownload.Hint.SelectFolder")));
        if (string.IsNullOrWhiteSpace(saveFolder)) return;
        var target = Path.Combine(saveFolder, CompFileNameGet(project, file));
        _StartQuickDownload(file, target);
        HintService.Show(Hints.Success(Lang.Text("Download.Comp.QuickDownload.Hint.DownloadStarted", project.RawName)));
    }

    /// <summary>构造并启动单文件下载任务（与详情页 Save_Click 末段一致）。</summary>
    private static void _StartQuickDownload(CompFile file, string target)
    {
        var desc = file.Type switch
        {
            CompType.Mod => Lang.Text("Download.Comp.Type.Mod"),
            CompType.ResourcePack => Lang.Text("Download.Comp.Type.ResourcePack"),
            CompType.Shader => Lang.Text("Download.Comp.Type.Shader"),
            CompType.DataPack => Lang.Text("Download.Comp.Type.DataPack"),
            CompType.World => Lang.Text("Download.Comp.Type.World"),
            _ => Lang.Text("Download.Comp.Type.Mod")
        };
        var loaderName = Lang.Text(
            "Download.Comp.Detail.DownloadResource",
            desc,
            Path.GetFileNameWithoutExtension(target));
        var loaders = new List<Loading.LoaderBase>
        {
            new LoaderDownload(
                Lang.Text("Download.Comp.Detail.DownloadFile"),
                [
                    file.Type == CompType.Mod
                        ? file.ToNetFile(target)
                        : file.ToNetFile(target, DownloadReason.Standalone, null)
                ])
            {
                ProgressWeight = 6,
                block = true
            }
        };
        if (file.Type == CompType.World)
        {
            var extractDir = Path.GetDirectoryName(target);
            if (extractDir is not null)
                loaders.Add(
                    new Loading.LoaderTask<int, int>(
                        Lang.Text("Download.Comp.Detail.InstallWorld"),
                        _ => Common.ExtractFile(target, extractDir, Encoding.UTF8))
                    {
                        ProgressWeight = 0.1d,
                        block = true
                    });
            loaders.Add(
                new Loading.LoaderTask<int, int>(
                    Lang.Text("Download.Comp.Detail.CleanCache"),
                    _ => File.Delete(target)));
        }

        var loader = new Loading.LoaderCombo<int>(loaderName, loaders)
        {
            OnStateChanged = DownloadUi.LoaderStateChangedHintOnly
        };
        loader.Start(1);
        Loading.LoaderTaskbarAdd(loader);
        Launcher.frmMain!.BtnExtraDownload.ShowRefresh();
        Launcher.frmMain.BtnExtraDownload.Ribble();
    }

    /// <summary>根据资源类型返回实例内的目标子文件夹（与 Save_Click 一致）。</summary>
    private static string _GetSubFolder(CompType type)
    {
        return type switch
        {
            CompType.Mod => "mods\\",
            CompType.ResourcePack => "resourcepacks\\",
            CompType.Shader => "shaderpacks\\",
            CompType.World => "saves\\",
            _ => ""
        };
    }

    /// <summary>取文件自身声明的加载器，缺失时回退到工程的加载器。</summary>
    private static List<CompLoaderType> _ResolveLoaders(CompFile file, CompProject project)
    {
        return file.ModLoaders.Count > 0 ? file.ModLoaders : project.ModLoaders;
    }

    /// <summary>
    ///     按资源类型筛选文件，与详情页 GetResults 一致：Modrinth 会返回 Mod / 服务端插件 / 数据包混合的列表，
    ///     需过滤回当前类型，避免快速下载到另一种产物。光影与资源包不筛（原版光影以资源包格式发布）。
    /// </summary>
    private static List<CompFile> FilterFilesByType(List<CompFile> files, CompType type)
    {
        return type is CompType.Shader or CompType.ResourcePack
            ? files
            : ([.. files.Where(f => f.Type == type)]);
    }

    /// <summary>判断某实例是否兼容该文件（基于 Save_Click 的 isVersionSuitable，补全了 Quilt 判定）。</summary>
    public static bool IsInstanceSuitableForFile(
        McInstance? version,
        CompFile file,
        List<CompLoaderType> allowedLoaders)
    {
        if (version is null) return false;
        if (!version.IsLoaded) version.Load();

        if (file.Type is CompType.Mod or CompType.DataPack)
            if (file.GameVersions.Any(v => v.Contains('.'))
                && !file.GameVersions.Any(v => v.Contains('.') && v == version.Info.VanillaName))
                return false;

        return allowedLoaders.Count == 0
            || (allowedLoaders.Contains(CompLoaderType.Forge) && version.Info.HasForge)
            || (allowedLoaders.Contains(CompLoaderType.Forge) && version.Info.HasCleanroom)
            || (allowedLoaders.Contains(CompLoaderType.Fabric)
                && (version.Info.HasFabric || version.Info.HasLegacyFabric))
            || (allowedLoaders.Contains(CompLoaderType.NeoForge) && version.Info.HasNeoForge)
            || (allowedLoaders.Contains(CompLoaderType.Quilt) && version.Info.HasQuilt)
            || (allowedLoaders.Contains(CompLoaderType.LiteLoader) && version.Info.HasLiteLoader);
    }

    /// <summary>挑选最新文件：优先 Release，其次按发布日期最新。compatFilter 为空时不做兼容过滤。</summary>
    private static CompFile? _PickLatestFile(
        List<CompFile> files,
        Func<CompFile, bool>? compatFilter = null)
    {
        var candidates = (compatFilter is null
            ? files
            : files.Where(compatFilter)).ToList();
        if (candidates.Count == 0) return null;
        return candidates
            .OrderByDescending(f => f.Status == CompFileStatus.Release)
            .ThenByDescending(f => f.ReleaseDate)
            .First();
    }
}
