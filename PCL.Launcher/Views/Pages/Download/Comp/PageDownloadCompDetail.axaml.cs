using System.Text;
using Avalonia;
using Avalonia.Controls;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Platform;
using PCL.Core.UI;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.Core.Utils.Validate;
using PCL.Network.Loaders;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Download;

public partial class PageDownloadCompDetail : MyPageRight
{
    private List<Comp.CompFile> _allResults = [];
    private Loading.LoaderTask<int, List<Comp.CompFile>>? _fileLoader;
    private bool _groupedDrop;
    private bool _groupedOld;
    private string? _loaderFilter;
    private Comp.CompProject? _project;
    private string? _targetGameVersion;
    private Comp.CompLoaderType _targetModLoader = Comp.CompLoaderType.Any;
    private Comp.CompType _targetType = Comp.CompType.Any;
    private string? _versionFilter;

    public PageDownloadCompDetail()
    {
        InitializeComponent();
        PanScroll = PanBack;

        BtnOpenWebsite.Click += (_, _) =>
        {
            if (_project is { Website.Length: > 0 } project)
                Basics.OpenPath(project.Website);
        };
        BtnIntroWiki.Click += (_, _) =>
        {
            if (_project is { WikiId: > 0 } project)
                Basics.OpenPath("https://www.mcmod.cn/class/" + project.WikiId + ".html");
        };
        BtnCopyName.Click += (_, _) =>
        {
            if (_project is null) return;
            PlatformServices.Clipboard?.SetText(_project.TranslatedName);
            HintService.Show(Hints.Success(Lang.Text("Common.Status.Success"), false));
        };
        BtnCopyLink.Click += (_, _) =>
        {
            if (_project is null) return;
            var link = !string.IsNullOrEmpty(_project.Website)
                ? _project.Website
                : $"https://modrinth.com/project/{_project.Slug}";
            PlatformServices.Clipboard?.SetText(link);
            HintService.Show(Hints.Success(Lang.Text("Common.Status.Success"), false));
        };
        BtnTranslate.Click += async (_, _) => await TranslateDescriptionAsync();
        BtnFavorites.Click += (_, _) =>
        {
            if (_project is { } project)
                CompFavoritesMenu.Show(project, BtnFavorites, RefreshFavoriteButton);
        };

        LocalizationService.LanguageChanged += OnLanguageChanged;
        AttachedToVisualTree += (_, _) => EnsureLoader();
    }

    private void OnLanguageChanged()
    {
        if (_project is null) return;
        LabDownloadCount.Text = Lang.Text(
            "Common.Format.DownloadCount",
            Lang.CompactNumber(_project.DownloadCount));
        ApplyButtonLocalization();
        if (_fileLoader?.State == LoadState.Finished)
        {
            BuildFilters();
            BuildFileList();
        }
    }

    public override void OnNavigatedTo(object? parameter)
    {
        PanBack.ScrollToHome();
        if (!TryApplyParameter(parameter)) return;
        EnsureLoader();
        Refresh();
    }

    public override void OnNavigatedFrom()
    {
    }

    private void EnsureLoader()
    {
        if (_fileLoader is not null) return;
        _fileLoader = new Loading.LoaderTask<int, List<Comp.CompFile>>(
            Lang.Text("Download.Comp.Detail.FileList.VersionList"),
            task =>
            {
                if (_project is null) return;
                task.output = Comp.CompFilesGet(_project.Id, _project.FromCurseForge);
            })
        {
            reloadTimeout = 60 * 1000
        };
        _fileLoader.OnStateChangedUi += OnFileLoaderStateChanged;
        Load.State = _fileLoader;
    }

    private bool TryApplyParameter(object? parameter)
    {
        switch (parameter)
        {
            case Comp.CompProject project:
                _project = project;
                _targetType = project.Type;
                _targetGameVersion = null;
                _targetModLoader = Comp.CompLoaderType.Any;
                return true;
            case CompDetailParameter context:
                _project = context.Project;
                _targetType = context.TargetType;
                _targetGameVersion = context.TargetGameVersion;
                _targetModLoader = context.TargetModLoader;
                return true;
            default:
                _project = null;
                return false;
        }
    }

    private void Refresh()
    {
        if (_project is not { } project) return;

        LabTitle.Text = project.TranslatedName;
        var title = project.GetControlTitle(true);
        LabSubTitle.Text = title.Value;
        LabSubTitle.IsVisible = !string.IsNullOrEmpty(title.Value);
        LabDescription.Text = (project.Description ?? string.Empty).Replace("\r", string.Empty);
        LabDownloadCount.Text = Lang.Text(
            "Common.Format.DownloadCount",
            Lang.CompactNumber(project.DownloadCount));
        LabSource.Text = project.FromCurseForge ? "CurseForge" : "Modrinth";

        ImgLogo.Source = string.IsNullOrEmpty(project.LogoUrl)
            ? "avares://PCL.Launcher/Assets/Images/Icons/NoIcon.png"
            : project.LogoUrl;

        BtnIntroWiki.IsVisible = Lang.IsChineseMainland && project.WikiId > 0;
        BtnTranslate.IsVisible = Lang.IsChineseMainland;
        ApplyButtonLocalization();
        RefreshFavoriteButton();

        PanResults.Children.Clear();
        PanLoad.IsVisible = true;
        _fileLoader?.Start(0, true);
    }

    private void ApplyButtonLocalization()
    {
        BtnIntroWiki.Text = Lang.Text("Download.Comp.Detail.McMod");
        BtnTranslate.Text = Lang.Text("Download.Comp.Detail.TranslateDescription");
        BtnFavorites.Text = Lang.Text("Download.Comp.Detail.Favorite");
    }

    private void RefreshFavoriteButton()
    {
        if (_project is null) return;
        BtnFavorites.SvgIcon = Comp.CompFavorites.IsFavourite(_project.Id)
            ? "lucide/heart-filled"
            : "lucide/heart";
    }

    private async Task TranslateDescriptionAsync()
    {
        if (_project is null) return;
        try
        {
            BtnTranslate.IsEnabled = false;
            HintService.Show(
                Hints.Info(
                    Lang.Text(
                        "Download.Comp.Detail.DescriptionTranslating",
                        _project.TranslatedName)));
            var chineseDescription = await _project.ChineseDescription;
            if (chineseDescription is null) return;
            await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Common.Dialog.Title"),
                    Content = Lang.Text(
                        "Download.Comp.Detail.DescriptionTranslationResult",
                        _project.Description,
                        chineseDescription),
                    Buttons = DialogButtons.Ok
                });
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "翻译资源简介失败",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Download.Comp.Error.OperationFailed"));
        }
        finally
        {
            BtnTranslate.IsEnabled = true;
        }
    }

    private void OnFileLoaderStateChanged(
        Loading.LoaderBase sender,
        LoadState newState,
        LoadState oldState)
    {
        switch (newState)
        {
            case LoadState.Finished:
                PanLoad.IsVisible = false;
                _allResults = FilterResultsByType(_fileLoader?.output ?? []);
                BuildFilters();
                BuildFileList();
                break;

            case LoadState.Failed:
                PanLoad.IsVisible = false;
                HintService.Show(
                    Hints.Error(
                        sender.Error?.Message
                        ?? Lang.Text("Download.Comp.Error.OperationFailed")));
                break;

            case LoadState.Waiting:
            case LoadState.Loading:
            case LoadState.Aborted:
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(newState), newState, null);
        }
    }

    private List<Comp.CompFile> FilterResultsByType(List<Comp.CompFile> files)
    {
        var results = files.Where(file => file.Available).ToList();
        return _targetType switch
        {
            Comp.CompType.Any => [.. results.Where(file => file.Type != Comp.CompType.Plugin)],
            Comp.CompType.Shader or Comp.CompType.ResourcePack => results,
            _ => [.. results.Where(file => file.Type == _targetType)]
        };
    }

    #region 筛选器

    private void BuildFilters()
    {
        _groupedDrop = false;
        _groupedOld = false;
        var versionGroups = GetVersionGroups(false, false);
        if (versionGroups.Count >= 9)
        {
            _groupedDrop = true;
            versionGroups = GetVersionGroups(true, false);
            if (versionGroups.Count >= 9)
            {
                _groupedDrop = false;
                _groupedOld = true;
                versionGroups = GetVersionGroups(false, true);
                if (versionGroups.Count >= 9)
                {
                    _groupedDrop = true;
                    versionGroups = GetVersionGroups(true, true);
                }
            }
        }

        var loaderGroups = _allResults
            .SelectMany(file => file.ModLoaders)
            .Select(loader => loader.ToString())
            .Distinct()
            .OrderByDescending(name => name, StringComparer.Ordinal)
            .ToList();

        var showFilters = versionGroups.Count >= 2;
        CardFilter.IsVisible = showFilters;
        PanInstanceFilter.Children.Clear();
        PanModLoaderFilter.Children.Clear();
        PanModLoaderFilter.IsVisible = false;
        if (!showFilters)
        {
            _versionFilter = null;
            _loaderFilter = null;
            return;
        }

        var targetGroup = _targetGameVersion is null
            ? null
            : GetGroupedVersionName(_targetGameVersion, _groupedDrop, _groupedOld);
        if (targetGroup is not null && !versionGroups.Contains(targetGroup))
            targetGroup = null;
        _versionFilter = targetGroup;
        _loaderFilter = null;

        PanInstanceFilter.Children.Add(
            CreateFilterChip(
                "InstanceFilter",
                Lang.Text("Common.Option.All"),
                _versionFilter is null,
                () => _versionFilter = null));
        foreach (var group in versionGroups)
        {
            var name = group;
            PanInstanceFilter.Children.Add(
                CreateFilterChip(
                    "InstanceFilter",
                    name,
                    _versionFilter == name,
                    () => _versionFilter = name));
        }

        var isMod = _targetType == Comp.CompType.Mod;
        PanModLoaderFilter.IsVisible = isMod && loaderGroups.Count > 1;
        if (PanModLoaderFilter.IsVisible)
        {
            var targetLoaderName = _targetModLoader == Comp.CompLoaderType.Any
                ? null
                : _targetModLoader.ToString();
            if (targetLoaderName is not null && !loaderGroups.Contains(targetLoaderName))
                targetLoaderName = null;
            _loaderFilter = targetLoaderName;

            PanModLoaderFilter.Children.Add(
                CreateFilterChip(
                    "ModLoaderFilter",
                    Lang.Text("Common.Option.All"),
                    _loaderFilter is null,
                    () => _loaderFilter = null));
            foreach (var group in loaderGroups)
            {
                var name = group;
                PanModLoaderFilter.Children.Add(
                    CreateFilterChip(
                        "ModLoaderFilter",
                        name,
                        _loaderFilter == name,
                        () => _loaderFilter = name));
            }
        }
    }

    private MyRadioButton CreateFilterChip(
        string groupName,
        string text,
        bool isChecked,
        Action onChecked)
    {
        var chip = new MyRadioButton
        {
            Text = text,
            GroupName = groupName,
            ColorType = MyRadioButton.ColorState.Highlight,
            Checked = isChecked,
            Margin = new Thickness(2d, 0d, 2d, 4d)
        };
        chip.Check += (_, _) =>
        {
            onChecked();
            BuildFileList();
        };
        return chip;
    }

    private List<string> GetVersionGroups(bool groupedDrop, bool foldOld)
    {
        return
        [
            .. _allResults
                .SelectMany(file => file.GameVersions)
                .Select(version => GetGroupedVersionName(version, groupedDrop, foldOld))
                .Distinct()
                .OrderByDescending(name => name, VersionGroupComparer.Instance)
        ];
    }

    private static string GetGroupedVersionName(string? name, bool groupedByDrop, bool foldOld)
    {
        if (name is null)
            return Lang.Text("Download.Comp.Detail.VersionGroup.Other");
        if (name.Contains('w'))
            return Lang.Text("Download.Comp.Detail.VersionGroup.Snapshot");
        if (foldOld && McInstanceInfo.VersionToDrop(name, true) < 120)
            return Lang.Text("Download.Comp.Detail.VersionGroup.Old");
        if (groupedByDrop)
        {
            var drop = McInstanceInfo.VersionToDrop(name, true);
            if (drop >= 0)
                return McInstanceInfo.DropToVersion(drop);
        }

        return name;
    }

    /// <summary>分组名排序：目标版本卡置顶，其余按版本号倒序，「其他/快照」等特殊项排在后。</summary>
    private sealed class VersionGroupComparer : IComparer<string>
    {
        public static readonly VersionGroupComparer Instance = new();

        public int Compare(string? x, string? y)
        {
            var sx = x ?? string.Empty;
            var sy = y ?? string.Empty;
            if (sx == sy) return 0;
            var isXSpecial = !sx.Contains('.');
            var isYSpecial = !sy.Contains('.');
            if (isXSpecial && isYSpecial)
                return string.Compare(sx, sy, StringComparison.Ordinal);
            if (isXSpecial) return 1;
            if (isYSpecial) return -1;
            return -McVersionComparer.CompareVersion(sx, sy);
        }
    }

    #endregion

    #region 文件列表

    private void BuildFileList()
    {
        try
        {
            PanResults.Children.Clear();
            var filtered = FilterFiles(_allResults);
            if (filtered.Count == 0)
            {
                PanResults.Children.Add(
                    new TextBlock
                    {
                        Text = Lang.Text("Download.Comp.List.NoResults"),
                        Margin = new Thickness(4, 6, 4, 6)
                    });
                return;
            }

            var groups =
                new SortedDictionary<string, List<Comp.CompFile>>(VersionGroupComparer.Instance);
            foreach (var file in filtered)
            {
                var versions = file.GameVersions.Count == 0
                    ? [string.Empty]
                    : file.GameVersions.ToArray();
                foreach (var version in versions)
                {
                    var key = version.Length == 0
                        ? Lang.Text("Download.Comp.Detail.VersionGroup.Other")
                        : GetGroupedVersionName(version, _groupedDrop, _groupedOld);
                    if (!groups.TryGetValue(key, out var list))
                    {
                        list = [];
                        groups[key] = list;
                    }

                    if (!list.Contains(file))
                        list.Add(file);
                }
            }

            foreach (var (key, files) in groups)
            {
                var card = new MyCard
                {
                    Title = key,
                    Margin = new Thickness(0, 0, 0, 15)
                };
                var stack = new StackPanel
                {
                    Margin = new Thickness(20, MyCard.SwapedHeight, 18, 0),
                    Tag = files
                };
                card.Children.Add(stack);
                card.SwapControl = stack;
                card.InstallMethod = target =>
                {
                    if (target.Tag is not List<Comp.CompFile> list) return;
                    target.Children.Clear();
                    foreach (var file in list)
                        target.Children.Add(CreateFileRow(file));
                };
                if (groups.Count == 1)
                {
                    card.InstallMethod(stack);
                    card.IsSwapped = false;
                }
                else
                {
                    card.IsSwapped = true;
                }

                if (key == Lang.Text("Download.Comp.Detail.VersionGroup.Other"))
                    stack.Children.Add(
                        new MyHint
                        {
                            Text = Lang.Text("Download.Comp.Detail.VersionRecognitionDelayHint"),
                            Theme = MyHint.Themes.Yellow,
                            Margin = new Thickness(5, 0, 0, 8)
                        });
                PanResults.Children.Add(card);
            }
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "可视化资源文件列表失败",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Download.Comp.Error.OperationFailed"));
        }
    }

    /// <summary>文件行的点击动作。</summary>
    private enum FileClickAction
    {
        Save,
        InstallModpack,
        InstallWorld
    }

    private Control CreateFileRow(Comp.CompFile file)
    {
        return new MyVirtualizingElement<MyListItem>(() => BuildFileRow(file))
        {
            Height = 42d
        };
    }

    private MyListItem BuildFileRow(Comp.CompFile file)
    {
        var action = GetFileAction(file);
        var item = new MyListItem
        {
            Type = MyListItem.CheckType.Clickable,
            Height = 42d,
            Title = string.IsNullOrEmpty(file.DisplayName) ? file.FileName : file.DisplayName,
            Info = BuildFileInfo(file),
            Logo = GetStatusLogo(file),
            tag = file
        };

        var saveButton = new MyIconButton
        {
            SvgIcon = "lucide/save"
        };
        ToolTip.SetTip(saveButton, Lang.Text("Download.Version.SaveAs"));
        saveButton.Click += async (_, _) => await SaveFileAsync(file);

        if (action == FileClickAction.Save)
        {
            item.Buttons = [saveButton];
            item.Click += async (_, _) => await SaveFileAsync(file);
            return item;
        }

        var installButton = new MyIconButton
        {
            SvgIcon = "lucide/download"
        };
        ToolTip.SetTip(installButton, Lang.Text("Common.Action.Install"));
        installButton.Click += async (_, _) => await RunInstallAsync(action, file);
        item.Buttons = [installButton, saveButton];
        item.Click += async (_, _) => await RunInstallAsync(action, file);
        return item;
    }

    private FileClickAction GetFileAction(Comp.CompFile file)
    {
        var type = _project?.Type ?? file.Type;
        return type switch
        {
            Comp.CompType.ModPack => FileClickAction.InstallModpack,
            Comp.CompType.World => FileClickAction.InstallWorld,
            _ => FileClickAction.Save
        };
    }

    private Task RunInstallAsync(FileClickAction action, Comp.CompFile file)
    {
        return action switch
        {
            FileClickAction.InstallModpack => InstallModpackAsync(file),
            FileClickAction.InstallWorld => InstallWorldAsync(file),
            _ => SaveFileAsync(file)
        };
    }

    private static string GetStatusLogo(Comp.CompFile file)
    {
        var icon = file.Status switch
        {
            Comp.CompFileStatus.Release => "R.png",
            Comp.CompFileStatus.Beta => "B.png",
            _ => "A.png"
        };
        return LauncherPaths.ImageRoot + "Icons/" + icon;
    }

    private List<Comp.CompFile> FilterFiles(List<Comp.CompFile> files)
    {
        var result = files.ToList();
        if (_versionFilter is not null)
        {
            var byVersion = result
                .Where(file => file.GameVersions
                    .Any(version => GetGroupedVersionName(version, _groupedDrop, _groupedOld)
                        == _versionFilter))
                .ToList();
            if (byVersion.Count > 0)
                result = byVersion;
        }

        if (_loaderFilter is not null)
            result =
            [
                .. result
                    .Where(file =>
                        file.ModLoaders.Any(loader => loader.ToString() == _loaderFilter))
            ];

        return
        [
            .. result
                .OrderByDescending(file => file.ReleaseDate)
        ];
    }

    private static string BuildFileInfo(Comp.CompFile file)
    {
        var info = new List<string>();
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file.FileName);
        var title = string.IsNullOrEmpty(file.DisplayName) ? file.FileName : file.DisplayName;
        if (!string.IsNullOrEmpty(fileNameWithoutExtension) && title != fileNameWithoutExtension)
            info.Add(fileNameWithoutExtension);
        if (file.Dependencies.Count != 0)
            info.Add(
                Lang.Text(
                    "Download.Comp.Detail.FileList.DependencyCount",
                    file.Dependencies.Count));
        if (file.GameVersions.Count > 0)
            info.Add(
                Lang.Text(
                    "Download.Comp.Detail.FileList.GameVersion",
                    string.Join("、", file.GameVersions.Select(Comp.FormatGameVersion))));
        if (file.DownloadCount > 0)
            info.Add(
                Lang.Text("Common.Format.DownloadCount", Lang.CompactNumber(file.DownloadCount)));
        info.Add(
            Lang.Text(
                "Download.Comp.Detail.FileList.Updated",
                Lang.TimeSpan(file.ReleaseDate - DateTime.Now)));
        if (file.Status != Comp.CompFileStatus.Release)
            info.Add(file.StatusDescription);
        return string.Join("  |  ", info);
    }

    private async Task SaveFileAsync(Comp.CompFile file)
    {
        try
        {
            var folder =
                SystemDialogs.SelectFolder(Lang.Text("Download.Comp.Detail.SelectSaveLocation"));
            if (string.IsNullOrEmpty(folder)) return;
            var target = folder.EndsWith(Path.DirectorySeparatorChar.ToString())
                ? folder
                : folder + Path.DirectorySeparatorChar;
            var downloadFile = file.ToNetFile(target);
            var loader = new LoaderDownload(
                Lang.Text("Download.Comp.Detail.DownloadFile"),
                [downloadFile]);
            loader.OnStateChangedUi += (sender, newState, _) =>
            {
                switch (newState)
                {
                    case LoadState.Finished:
                        HintService.Show(Hints.Success(Lang.Text("Common.Status.Success")));
                        break;

                    case LoadState.Failed:
                        HintService.Show(
                            Hints.Error(
                                sender.Error?.Message
                                ?? Lang.Text("Download.Comp.Error.OperationFailed")));
                        break;

                    case LoadState.Waiting:
                    case LoadState.Loading:
                    case LoadState.Aborted:
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(newState), newState, null);
                }
            };
            loader.Start();
            HintService.Show(
                Hints.Info(
                    Lang.Text(
                        "Download.Comp.QuickDownload.Hint.DownloadStarted",
                        Path.GetFileName(downloadFile.LocalPath)),
                    false));
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "下载资源文件失败",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Download.Comp.Error.OperationFailed"));
        }

        await Task.CompletedTask;
    }

    #region 安装到实例

    private readonly HashSet<string> _installingFiles = new(StringComparer.OrdinalIgnoreCase);

    private async Task InstallModpackAsync(Comp.CompFile file)
    {
        if (_project is not { } project) return;
        var folder = GameFolders.mcFolderSelected;
        if (string.IsNullOrEmpty(folder))
        {
            HintService.Show(Hints.Error(Lang.Text("Download.Install.NoFolder")));
            return;
        }

        if (!_installingFiles.Add(file.FileName))
            return;
        var loaderStarted = false;
        try
        {
            var versionsFolder = Path.Combine(folder, "versions");
            var validator = new FolderNameValidator(versionsFolder);
            var defaultName = SanitizeInstanceName(project.TranslatedName);
            if (!(await validator.ValidateAsync(defaultName)).IsValid)
                defaultName = "";
            var nameResult = await DialogService.Instance.ShowAsync(
                new InputDialogRequest
                {
                    Title = Lang.Text("Download.Comp.Detail.InputInstanceName"),
                    DefaultValue = defaultName,
                    ValidateRules = [validator],
                    Buttons = DialogButtons.OkCancel,
                    HighLight = false
                });
            var instanceName = nameResult.IsConfirmed ? nameResult.InputText?.Trim() : null;
            if (string.IsNullOrEmpty(instanceName))
                return;

            var instanceFolder = Path.Combine(versionsFolder, instanceName);
            if (File.Exists(Path.Combine(instanceFolder, $"{instanceName}.json")))
            {
                HintService.Show(
                    Hints.Error(
                        Lang.Text(
                            "Minecraft.Download.Error.InstanceAlreadyExists",
                            instanceName,
                            "")));
                return;
            }

            // 上次失败留下的半成品目录：先按安装恢复服务清理可重试
            if (!McInstallRecovery.TryClearIncompleteInstanceFolder(instanceFolder, instanceName))
            {
                HintService.Show(
                    Hints.Error(
                        Lang.Text(
                            "Minecraft.Download.Error.InstanceAlreadyExists",
                            instanceName,
                            "")));
                return;
            }

            Directory.CreateDirectory(instanceFolder);
            var packPath = Path.Combine(
                instanceFolder,
                $"原始整合包.{(project.FromCurseForge ? "zip" : "mrpack")}");
            var fileName = file.FileName;
            var loaders = new List<Loading.LoaderBase>
            {
                new LoaderDownload(
                    Lang.Text("Download.Comp.Detail.DownloadModpackFile"),
                    [file.ToNetFile(packPath, Comp.DownloadReason.ModPack)])
                {
                    ProgressWeight = 10d,
                    block = true
                },
                new Loading.LoaderTask<int, int>(
                    Lang.Text("Download.Comp.Detail.PrepareModpackInstall"),
                    _ =>
                    {
                        var modpackLoader =
                            Modpack.ModpackInstall(packPath, instanceName, null, project.Id, true);
                        // 整合包安装继续在任务栏执行，安装结束前保持去重标记，避免同名实例被并发安装。
                        modpackLoader.OnStateChangedUi += (_, innerState, _) =>
                        {
                            if (innerState is not (LoadState.Waiting or LoadState.Loading))
                                _installingFiles.Remove(fileName);
                        };
                    })
                {
                    ProgressWeight = 0.1d
                }
            };

            var identifier = $"CompDetail:Modpack:{instanceName}";
            if (Loading.loaderTaskbar.Any(task => task.Identifier == identifier))
            {
                HintService.Show(
                    Hints.Error(Lang.Text("Minecraft.Download.Error.InstanceDownloading")));
                return;
            }

            var loaderName =
                $"{Lang.Text("Download.Comp.Detail.ModpackDownload")}：{project.TranslatedName}";
            var loader = new Loading.LoaderCombo<int>(loaderName, loaders)
            {
                Identifier = identifier
            };
            loader.OnStateChangedUi += (sender, newState, _) =>
                OnModpackInstallTerminal(sender, newState, fileName, instanceFolder, packPath);
            loader.Start();
            Loading.LoaderTaskbarAdd(loader);
            loaderStarted = true;
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "开始整合包安装失败",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Download.Comp.Error.OperationFailed"));
        }
        finally
        {
            // 未真正启动安装（用户取消、输入不合法等）时立即解除去重标记，允许再次点击。
            if (!loaderStarted)
                _installingFiles.Remove(file.FileName);
        }
    }

    private void OnModpackInstallTerminal(
        Loading.LoaderBase loader,
        LoadState newState,
        string fileName,
        string instanceFolder,
        string packPath)
    {
        if (newState is LoadState.Waiting or LoadState.Loading)
            return;

        if (newState == LoadState.Finished)
        {
            LauncherCommon.Log(
                $"[Comp] 已开始安装整合包：{Path.GetFileName(instanceFolder)}",
                LauncherLogLevel.Developer);
            return;
        }

        _installingFiles.Remove(fileName);
        HintService.Show(
            newState == LoadState.Aborted
                ? Hints.Info(Lang.Text("Download.Comp.Detail.Task.Cancelled", loader.name))
                : Hints.Error(
                    Lang.Text(
                        "Download.Comp.Detail.Task.Failed",
                        loader.name,
                        loader.Error?.Message
                        ?? Lang.Text("Download.Comp.Error.OperationFailed"))));
        CleanupModpackDownload(instanceFolder, packPath);
    }

    private static void CleanupModpackDownload(string instanceFolder, string packPath)
    {
        try
        {
            McInstallRecovery.UnregisterSession(instanceFolder);
            if (File.Exists(packPath))
                File.Delete(packPath);
            if (Directory.Exists(instanceFolder)
                && !Directory.EnumerateFileSystemEntries(instanceFolder).Any())
                Directory.Delete(instanceFolder);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "清理整合包下载残留失败");
        }
    }

    private async Task InstallWorldAsync(Comp.CompFile file)
    {
        try
        {
            var defaultFolder = GetWorldDefaultFolder();
            var target = SystemDialogs.SelectSaveFile(
                Lang.Text("Download.Comp.Detail.SelectWorldInstallLocation"),
                file.FileName ?? "",
                Lang.Text("Download.Comp.Detail.WorldFile.Filter"),
                defaultFolder);
            if (string.IsNullOrEmpty(target))
                return;
            var targetFolder = Path.GetDirectoryName(target);
            if (string.IsNullOrEmpty(targetFolder))
                return;
            Directory.CreateDirectory(targetFolder);

            var loaders = new List<Loading.LoaderBase>
            {
                new LoaderDownload(
                    Lang.Text("Download.Comp.Detail.DownloadWorldFile"),
                    [
                        file.ToNetFile(
                            target,
                            Comp.DownloadReason.Standalone,
                            file.RawGameVersions.FirstOrDefault())
                    ])
                {
                    ProgressWeight = 10d,
                    block = true
                },
                new Loading.LoaderTask<int, int>(
                    Lang.Text("Download.Comp.Detail.InstallWorld"),
                    _ => LauncherCommon.ExtractFile(target, targetFolder, Encoding.UTF8))
                {
                    ProgressWeight = 0.1d,
                    block = true
                },
                new Loading.LoaderTask<int, int>(
                    Lang.Text("Download.Comp.Detail.CleanCache"),
                    _ => File.Delete(target))
            };

            var loaderName =
                $"{(_project?.FromCurseForge == true ? "CurseForge" : "Modrinth")} {Lang.Text("Download.Comp.Detail.WorldDownload")}：{_project?.TranslatedName}";
            var loader = new Loading.LoaderCombo<int>(loaderName, loaders);
            loader.OnStateChangedUi += (sender, newState, _) =>
            {
                switch (newState)
                {
                    case LoadState.Finished:
                        HintService.Show(Hints.Success(Lang.Text("Common.Status.Success")));
                        break;
                    case LoadState.Failed:
                        HintService.Show(
                            Hints.Error(
                                Lang.Text(
                                    "Download.Comp.Detail.Task.Failed",
                                    sender.name,
                                    sender.Error?.Message
                                    ?? Lang.Text("Download.Comp.Error.OperationFailed"))));
                        TryDeleteFile(target);
                        break;
                    case LoadState.Aborted:
                        HintService.Show(
                            Hints.Info(
                                Lang.Text("Download.Comp.Detail.Task.Cancelled", sender.name)));
                        TryDeleteFile(target);
                        break;
                }
            };
            loader.Start();
            Loading.LoaderTaskbarAdd(loader);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "下载世界资源失败",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Download.Comp.Error.OperationFailed"));
        }

        await Task.CompletedTask;
    }

    private static string GetWorldDefaultFolder()
    {
        try
        {
            var current = InstanceList.McMcInstanceSelected;
            if (current is not null)
            {
                var saves = Path.Combine(current.PathIndie, "saves");
                Directory.CreateDirectory(saves);
                return saves;
            }

            var folder = GameFolders.mcFolderSelected;
            if (!string.IsNullOrEmpty(folder))
            {
                var saves = Path.Combine(folder, "saves");
                Directory.CreateDirectory(saves);
                return saves;
            }
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "获取世界资源默认保存目录失败");
        }

        return GameFolders.mcFolderSelected ?? "";
    }

    private static string SanitizeInstanceName(string name)
    {
        return name
            .Replace(".zip", "", StringComparison.OrdinalIgnoreCase)
            .Replace(".rar", "", StringComparison.OrdinalIgnoreCase)
            .Replace(".mrpack", "", StringComparison.OrdinalIgnoreCase)
            .Replace("\\", "＼")
            .Replace("/", "／")
            .Replace("|", "｜")
            .Replace(":", "：")
            .Replace("<", "＜")
            .Replace(">", "＞")
            .Replace("*", "＊")
            .Replace("?", "？")
            .Replace("\"", "")
            .Trim();
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "删除世界资源临时文件失败");
        }
    }

    #endregion

    #endregion
}