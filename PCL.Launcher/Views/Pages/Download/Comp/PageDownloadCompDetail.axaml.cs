using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.Minecraft.ResourceProject;
using PCL.Core.Platform;
using PCL.Core.UI;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.Core.Utils.Validate;
using PCL.Navigation;
using PCL.Network.Loaders;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Download;

public partial class PageDownloadCompDetail : MyPageRight
{
    private MyCompItem? _compItem;
    private DispatcherTimer? _refreshTimer;

    public PageDownloadCompDetail()
    {
        InitializeComponent();
        PanScroll = PanBack;
        Load.State = ViewModel.EnsureLoader();
        ViewModel.StateChanged += ApplyLoadState;
        ViewModel.FiltersChanged += BuildFilters;
        ViewModel.FileGroupsChanged += BuildFileList;
        ViewModel.IntroChanged += ApplyIntroState;

        BtnOpenWebsite.Click += (_, _) =>
        {
            if (ViewModel.Project is { Website.Length: > 0 } project)
                Basics.OpenPath(project.Website);
        };
        BtnIntroWiki.Click += (_, _) =>
        {
            if (ViewModel.Project is { WikiId: > 0 } project)
                Basics.OpenPath($"https://www.mcmod.cn/class/{project.WikiId}.html");
        };
        BtnCopyName.Click += (_, _) =>
        {
            if (ViewModel.Project is not { } project) return;
            var title = project.GetControlTitle(true);
            PlatformServices.Clipboard?.SetText(title.Key + title.Value);
            HintService.Show(Hints.Success(Lang.Text("Common.Status.Success"), false));
        };
        BtnCopyLink.Click += (_, _) =>
        {
            if (ViewModel.Project is not { } project) return;
            PlatformServices.Clipboard?.SetText(project.Website);
            HintService.Show(Hints.Success(Lang.Text("Common.Status.Success"), false));
        };
        BtnTranslate.Click += async (_, _) => await TranslateDescriptionAsync();
        BtnFavorites.Click += (_, _) =>
        {
            if (ViewModel.Project is { } project)
                CompFavoritesMenu.Show(project, BtnFavorites, RefreshFavoriteButton);
        };
        Load.Click += (_, _) => ViewModel.RetryCommand.Execute(null);
        Grid.SetIsSharedSizeScope(PanResults, true);

        LocalizationService.LanguageChanged += OnLanguageChanged;
    }

    public PageDownloadCompDetailViewModel ViewModel { get; } = new();

    private void OnLanguageChanged()
    {
        if (ViewModel.Project is null) return;
        ApplyButtonLocalization();
        ViewModel.RefreshLocalization();
        RefreshIntro();
    }

    public override void OnNavigatedTo(object? parameter)
    {
        PanBack.ScrollToHome();
        var applied = parameter switch
        {
            Comp.CompProject project => ViewModel.ApplyContext(
                project,
                project.Type,
                null,
                Comp.CompLoaderType.Any),
            CompDetailParameter context => ViewModel.ApplyContext(
                context.Project,
                context.TargetType,
                context.TargetGameVersion,
                context.TargetModLoader),
            _ => ViewModel.ApplyContext(
                null,
                Comp.CompType.Any,
                null,
                Comp.CompLoaderType.Any)
        };
        if (!applied) return;

        _refreshTimer?.Stop();
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(180d)
        };
        _refreshTimer.Tick += (_, _) =>
        {
            _refreshTimer?.Stop();
            _refreshTimer = null;
            if (ReferenceEquals(PageRouter.CurrentPage, this))
                Refresh();
        };
        _refreshTimer.Start();
    }

    public override void OnNavigatedFrom()
    {
        _refreshTimer?.Stop();
        _refreshTimer = null;
    }

    private void Refresh()
    {
        if (ViewModel.Project is not { } project) return;

        RefreshIntro();

        BtnIntroWiki.IsVisible = Lang.IsChineseMainland && project.WikiId > 0;
        BtnTranslate.IsVisible = Lang.IsChineseMainland;
        ApplyButtonLocalization();
        RefreshFavoriteButton();

        PanResults.Children.Clear();
        CardFilter.IsVisible = false;
        PanLoad.IsVisible = true;
        ViewModel.StartLoad();
    }

    private void ApplyLoadState()
    {
        PanLoad.IsVisible = ViewModel.IsLoadingVisible || ViewModel.IsErrorVisible;
        CardFilter.IsVisible = ViewModel.FilterVisible;
    }

    private void ApplyIntroState()
    {
        BtnIntroWiki.IsVisible = ViewModel.IntroWikiVisible;
        BtnTranslate.IsVisible = ViewModel.TranslateVisible;
        RefreshIntro();
    }

    private void RefreshIntro()
    {
        if (ViewModel.IntroItem is not { } introItem)
        {
            if (_compItem is not null)
            {
                PanIntro.Children.Remove(_compItem);
                _compItem = null;
            }

            return;
        }

        if (_compItem is not null)
            PanIntro.Children.Remove(_compItem);

        _compItem = new MyCompItem(introItem)
        {
            CanInteraction = false,
            ShowFavoriteBtn = false,
            ShowDownloadBtn = false,
            Margin = new Thickness(-7d, -7d, 0d, 8d)
        };
        PanIntro.Children.Insert(0, _compItem);
    }

    private void ApplyButtonLocalization()
    {
        BtnOpenWebsite.Text = ViewModel.IntroWebText;
        BtnIntroWiki.Text = Lang.Text("Download.Comp.Detail.McMod");
        BtnTranslate.Text = Lang.Text("Download.Comp.Detail.TranslateDescription");
        BtnFavorites.Text = Lang.Text("Download.Comp.Detail.Favorite");
    }

    private void RefreshFavoriteButton()
    {
        ViewModel.RefreshFavorite();
        BtnFavorites.SvgIcon = ViewModel.IsFavorite
            ? "lucide/heart-filled"
            : "lucide/heart";
        _compItem?.RefreshFavoriteStatus();
    }

    private async Task TranslateDescriptionAsync()
    {
        if (ViewModel.Project is not { } project) return;
        try
        {
            BtnTranslate.IsEnabled = false;
            HintService.Show(
                Hints.Info(
                    Lang.Text(
                        "Download.Comp.Detail.DescriptionTranslating",
                        project.TranslatedName)));
            var chineseDescription = await project.ChineseDescription;
            if (chineseDescription is null) return;
            await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Common.Dialog.Title"),
                    Content = Lang.Text(
                        "Download.Comp.Detail.DescriptionTranslationResult",
                        project.Description,
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

    #region 筛选器

    private void BuildFilters()
    {
        try
        {
            CardFilter.IsVisible = ViewModel.FilterVisible;
            PanInstanceFilter.Children.Clear();
            PanModLoaderFilter.Children.Clear();
            PanModLoaderFilter.IsVisible = false;
            if (!ViewModel.FilterVisible)
                return;

            foreach (var chip in ViewModel.InstanceFilterLabelChips)
                PanInstanceFilter.Children.Add(CreateFilterLabel(chip.Text, true));
            foreach (var option in ViewModel.InstanceFilterOptions)
                PanInstanceFilter.Children.Add(
                    CreateFilterChip(
                        "InstanceFilter",
                        option,
                        selected => ViewModel.SelectInstanceFilterCommand.Execute(selected)));

            PanModLoaderFilter.IsVisible = ViewModel is
            {
                TargetType: Comp.CompType.Mod,
                ModLoaderFilterOptions.Count: > 0
            };
            if (!PanModLoaderFilter.IsVisible)
                return;

            foreach (var chip in ViewModel.ModLoaderFilterLabelChips)
                PanModLoaderFilter.Children.Add(CreateFilterLabel(chip.Text, false));
            foreach (var option in ViewModel.ModLoaderFilterOptions)
                PanModLoaderFilter.Children.Add(
                    CreateFilterChip(
                        "ModLoaderFilter",
                        option,
                        selected => ViewModel.SelectModLoaderFilterCommand.Execute(selected)));
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "构建资源筛选器失败",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Download.Comp.Error.OperationFailed"));
        }
    }

    private static TextBlock CreateFilterLabel(string text, bool withBottomMargin)
    {
        return new TextBlock
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2d, 0d, 8d, withBottomMargin ? 4d : 0d)
        };
    }

    private static MyRadioButton CreateFilterChip(
        string groupName,
        CompFilterOption option,
        Action<CompFilterOption> onChecked)
    {
        var chip = new MyRadioButton
        {
            Text = option.Text,
            GroupName = groupName,
            ColorType = MyRadioButton.ColorState.Highlight,
            Checked = option.IsSelected,
            Margin = new Thickness(2d, 0d, 2d, 4d)
        };
        chip.Check += (_, _) => onChecked(option);
        return chip;
    }

    #endregion

    #region 文件列表

    private void BuildFileList()
    {
        try
        {
            PanResults.Children.Clear();
            if (ViewModel.FileGroups.Count == 0)
            {
                PanResults.Children.Add(
                    new TextBlock
                    {
                        Text = Lang.Text("Download.Comp.List.NoResults"),
                        Margin = new Thickness(4, 6, 4, 6)
                    });
                return;
            }

            foreach (var group in ViewModel.FileGroups)
            {
                var card = new MyCard
                {
                    Title = group.Title,
                    Margin = new Thickness(0, 0, 0, 15),
                    Tag = group
                };
                var stack = new StackPanel
                {
                    Margin = new Thickness(20, MyCard.SwapedHeight, 18, 0),
                    Tag = group
                };
                card.Children.Add(stack);
                card.SwapControl = stack;
                card.InstallMethod = target =>
                {
                    target.Children.Clear();
                    foreach (var file in group.Items)
                        target.Children.Add(CreateFileRow(file));
                    if (group.ShowRecognitionHint)
                        target.Children.Add(
                            new MyHint
                            {
                                Text = Lang.Text(
                                    "Download.Comp.Detail.VersionRecognitionDelayHint"),
                                Theme = MyHint.Themes.Yellow,
                                Margin = new Thickness(5, 0, 0, 8)
                            });
                };

                PanResults.Children.Add(card);
                if (!group.IsSwapped)
                    card.StackInstall();
                else
                    card.IsSwapped = true;
                card.Swap += (_, _) =>
                    ViewModel.SetGroupExpanded(group.Title, !card.IsSwapped);
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

    private Control CreateFileRow(CompFileItemViewModel file)
    {
        return new MyVirtualizingElement<MyListItem>(() => BuildFileRow(file))
        {
            Height = 42d
        };
    }

    private MyListItem BuildFileRow(CompFileItemViewModel fileItem)
    {
        var file = fileItem.File;
        var action = fileItem.Action;
        var item = new MyListItem
        {
            Type = MyListItem.CheckType.Clickable,
            Height = 42d,
            Title = fileItem.TitleText,
            Info = fileItem.InfoText,
            Logo = fileItem.Logo,
            Tag = file
        };

        var saveButton = new MyIconButton
        {
            SvgIcon = "lucide/save"
        };
        ToolTip.SetTip(saveButton, Lang.Text("Download.Version.SaveAs"));
        saveButton.Click += async (_, _) => await SaveFileAsync(file);

        if (action == CompFileAction.Save)
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

    private Task RunInstallAsync(CompFileAction action, Comp.CompFile file)
    {
        return action switch
        {
            CompFileAction.InstallModpack => InstallModpackAsync(file),
            CompFileAction.InstallWorld => InstallWorldAsync(file),
            CompFileAction.InstallToInstance => InstallToInstanceAsync(file),
            _ => SaveFileAsync(file)
        };
    }

    #region 另存为

    /// <summary>按资源类型记录上一次另存为的目录，与 WPF 的 cachedFolder 行为一致。</summary>
    public static Dictionary<Comp.CompType, string> CachedFolder { get; } = [];

    public static void SetCachedFolder(Comp.CompType type, string? folder)
    {
        if (!string.IsNullOrWhiteSpace(folder))
            CachedFolder[type] = folder;
    }

    private async Task SaveFileAsync(Comp.CompFile file)
    {
        try
        {
            if (ViewModel.Project is not { } project)
                return;

            var desc = Comp.GetCompTypeName(file.Type);
            var subFolder = GetSubFolder(file.Type);
            var isModpack = file.Type == Comp.CompType.ModPack;
            var defaultFolder = isModpack
                ? null
                : GetDefaultSaveFolder(file, project, subFolder);
            var target = SystemDialogs.SelectSaveFile(
                Lang.Text("Download.Comp.Detail.SelectSaveLocation"),
                Comp.CompFileNameGet(project, file),
                BuildSaveFilter(file, desc),
                defaultFolder);
            if (string.IsNullOrEmpty(target))
                return;

            var targetDir = Path.GetDirectoryName(target);
            if (string.IsNullOrEmpty(targetDir))
                return;
            if (string.IsNullOrEmpty(defaultFolder)
                || !PathsEqual(targetDir, defaultFolder))
                CachedFolder[file.Type] = targetDir;

            var targetInstance = FindInstanceByIndiePath(targetDir);
            if (targetInstance is { IsLoaded: false })
                targetInstance.Load();
            if (file.Type == Comp.CompType.Mod
                && Config.Download.Comp.AutoInstallDependencies
                && file.Dependencies.Count > 0)
            {
                var proceed = await ResolveAndStartDependenciesAsync(
                    file,
                    project,
                    targetDir,
                    targetInstance,
                    GetAllowedLoaders(file, project));
                if (!proceed)
                    return;
            }

            StartDownloadTask(
                file,
                target,
                desc,
                $"CompDetail:Save:{target}");
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "下载资源文件失败",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Download.Comp.Error.OperationFailed"));
        }
    }

    private static string BuildSaveFilter(Comp.CompFile file, string desc)
    {
        var pattern = file.Type switch
        {
            Comp.CompType.Mod => file.FileName?.EndsWith(
                    ".litemod",
                    StringComparison.OrdinalIgnoreCase)
                == true
                    ? "*.litemod"
                    : "*.jar",
            Comp.CompType.ModPack =>
                file.FileName?.EndsWith(".mrpack", StringComparison.OrdinalIgnoreCase) == true
                || (string.IsNullOrWhiteSpace(file.FileName) && !file.FromCurseForge)
                    ? "*.mrpack"
                    : "*.zip",
            _ => "*.zip"
        };
        return $"{Lang.Text("Download.Comp.Detail.ResourceFile.Filter", desc)}|{pattern}";
    }

    private static string GetSubFolder(Comp.CompType type)
    {
        return type switch
        {
            Comp.CompType.Mod => $"mods{Path.DirectorySeparatorChar}",
            Comp.CompType.ResourcePack => $"resourcepacks{Path.DirectorySeparatorChar}",
            Comp.CompType.Shader => $"shaderpacks{Path.DirectorySeparatorChar}",
            Comp.CompType.World => $"saves{Path.DirectorySeparatorChar}",
            Comp.CompType.Schematic => $"schematics{Path.DirectorySeparatorChar}",
            _ => string.Empty
        };
    }

    private static McInstance? FindApplicableInstance(
        Comp.CompFile file,
        Comp.CompProject project)
    {
        var allowedLoaders = GetAllowedLoaders(file, project);
        var current = InstanceList.McMcInstanceSelected;
        if (IsFileSuitableForInstance(current, file, allowedLoaders))
            return current;

        var needLoad = InstanceList.mcInstanceListLoader.State != LoadState.Finished;
        if (needLoad && !string.IsNullOrEmpty(GameFolders.mcFolderSelected))
        {
            HintService.Show(
                Hints.Info(Lang.Text("Download.Comp.Detail.FindingApplicableInstance")));
            Loading.LoaderFolderRun(
                InstanceList.mcInstanceListLoader,
                GameFolders.mcFolderSelected,
                Loading.LoaderFolderRunType.ForceRun,
                1,
                $"versions{Path.DirectorySeparatorChar}",
                true);
        }

        return InstanceList
            .mcInstanceList.Values
            .SelectMany(instances => instances)
            .FirstOrDefault(instance => IsFileSuitableForInstance(instance, file, allowedLoaders));
    }

    /// <summary>获取另存为的默认目录：上次目录 → 当前实例 → 其他适合的实例 → 当前 MC 文件夹。</summary>
    private static string? GetDefaultSaveFolder(
        Comp.CompFile file,
        Comp.CompProject project,
        string subFolder)
    {
        if (CachedFolder.TryGetValue(file.Type, out var cached)
            && !string.IsNullOrWhiteSpace(cached))
        {
            Directory.CreateDirectory(cached);
            return cached;
        }

        var allowedLoaders = GetAllowedLoaders(file, project);
        var current = InstanceList.McMcInstanceSelected;
        if (IsFileSuitableForInstance(current, file, allowedLoaders))
        {
            var folder = Path.Combine(current!.PathIndie, subFolder);
            Directory.CreateDirectory(folder);
            return folder;
        }

        var needLoad = InstanceList.mcInstanceListLoader.State != LoadState.Finished;
        if (needLoad && !string.IsNullOrEmpty(GameFolders.mcFolderSelected))
        {
            HintService.Show(
                Hints.Info(Lang.Text("Download.Comp.Detail.FindingApplicableInstance")));
            Loading.LoaderFolderRun(
                InstanceList.mcInstanceListLoader,
                GameFolders.mcFolderSelected,
                Loading.LoaderFolderRunType.ForceRun,
                1,
                $"versions{Path.DirectorySeparatorChar}",
                true);
        }

        var suitableFolders = InstanceList
            .mcInstanceList.Values
            .SelectMany(instances => instances)
            .Where(instance => IsFileSuitableForInstance(instance, file, allowedLoaders))
            .Select(instance => new DirectoryInfo(Path.Combine(instance.PathIndie, subFolder)))
            .ToList();
        if (suitableFolders.Count > 0)
        {
            var selected = suitableFolders
                .OrderByDescending(folder =>
                    folder.Exists ? folder.LastWriteTimeUtc : DateTime.MinValue)
                .ThenByDescending(folder => folder.Exists ? folder.GetFiles().Length : -1)
                .First();
            Directory.CreateDirectory(selected.FullName);
            return selected.FullName;
        }

        if (needLoad)
            HintService.Show(Hints.Info(Lang.Text("Download.Comp.Detail.NoApplicableInstance")));
        return string.IsNullOrEmpty(GameFolders.mcFolderSelected)
            ? null
            : GameFolders.mcFolderSelected;
    }

    private static List<Comp.CompLoaderType> GetAllowedLoaders(
        Comp.CompFile file,
        Comp.CompProject project)
    {
        var loaders = file.ModLoaders.Count > 0 ? file.ModLoaders : project.ModLoaders;
        return [.. loaders.Where(loader => loader != Comp.CompLoaderType.Any).Distinct()];
    }

    /// <summary>
    ///     判断文件是否适合指定实例。Mod/数据包需要版本与加载器都匹配；
    ///     资源包、光影、原理图与世界只要求有可用实例，由用户自行决定是否安装。
    /// </summary>
    private static bool IsFileSuitableForInstance(
        McInstance? instance,
        Comp.CompFile file,
        List<Comp.CompLoaderType> allowedLoaders)
    {
        if (instance is null)
            return false;
        if (!instance.IsLoaded)
            instance.Load();
        return file.Type is not (Comp.CompType.Mod or Comp.CompType.DataPack)
            || Comp.IsInstanceSuitableForFile(instance, file, allowedLoaders);
    }

    private static bool PathsEqual(string left, string right)
    {
        try
        {
            return string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static McInstance? FindInstanceByIndiePath(string targetDir)
    {
        try
        {
            var instances = new List<McInstance>();
            if (InstanceList.McMcInstanceSelected is { } current)
                instances.Add(current);
            instances.AddRange(
                InstanceList
                    .mcInstanceList.Values
                    .SelectMany(list => list)
                    .Where(instance => instance is not null));
            var normalizedTarget = Path.TrimEndingDirectorySeparator(Path.GetFullPath(targetDir));
            foreach (var instance in instances.Distinct())
            {
                var pathIndie = Path.TrimEndingDirectorySeparator(
                    Path.GetFullPath(instance.PathIndie));
                if (string.Equals(normalizedTarget, pathIndie, StringComparison.OrdinalIgnoreCase)
                    || normalizedTarget.StartsWith(
                        pathIndie + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase))
                    return instance;
            }
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "查找资源对应的实例失败", LauncherLogLevel.Developer);
        }

        return null;
    }

    #endregion

    #region 下载任务

    private static void StartDownloadTask(
        Comp.CompFile file,
        string targetPath,
        string desc,
        string identifier,
        bool deleteOnFailureIfMissing = true,
        Action? onTerminal = null)
    {
        var existed = File.Exists(targetPath);
        var loaderName = Lang.Text(
            "Download.Comp.Detail.DownloadResource",
            desc,
            Path.GetFileNameWithoutExtension(targetPath));
        var loaders = new List<Loading.LoaderBase>
        {
            new LoaderDownload(
                Lang.Text("Download.Comp.Detail.DownloadFile"),
                [
                    file.Type == Comp.CompType.Mod
                        ? file.ToNetFile(targetPath)
                        : file.ToNetFile(
                            targetPath,
                            Comp.DownloadReason.Standalone,
                            file.RawGameVersions.FirstOrDefault())
                ])
            {
                ProgressWeight = 6d,
                block = true
            }
        };
        var loader = new Loading.LoaderCombo<int>(loaderName, loaders)
        {
            Identifier = identifier
        };
        loader.OnStateChangedUi += (sender, newState, _) =>
        {
            switch (newState)
            {
                case LoadState.Finished:
                    HintService.Show(Hints.Success(Lang.Text("Common.Status.Success")));
                    onTerminal?.Invoke();
                    break;

                case LoadState.Failed:
                    HintService.Show(
                        Hints.Error(
                            sender.Error?.Message
                            ?? Lang.Text("Download.Comp.Error.OperationFailed")));
                    if (deleteOnFailureIfMissing && !existed)
                        TryDeleteFile(targetPath);
                    onTerminal?.Invoke();
                    break;

                case LoadState.Aborted:
                    HintService.Show(Hints.Info(Lang.Text("Common.Status.Cancelled")));
                    if (deleteOnFailureIfMissing && !existed)
                        TryDeleteFile(targetPath);
                    onTerminal?.Invoke();
                    break;

                case LoadState.Waiting:
                case LoadState.Loading:
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(newState), newState, null);
            }
        };
        loader.Start(1);
        Loading.LoaderTaskbarAdd(loader);
        LoadingUi.Current?.RefreshDownloadButton();
    }

    private static void StartDependencyDownloads(
        ModDependencyResolutionResult result,
        string targetDir,
        string desc)
    {
        var downloads = CompDependency.BuildDependencyDownloads(result, targetDir);
        if (downloads.Count == 0)
        {
            LauncherCommon.Log("[CompDeps] 前置解析完成，但没有可下载的前置文件", LauncherLogLevel.Debug);
            return;
        }

        foreach (var (filename, downloadFile) in downloads)
        {
            var targetPath = downloadFile.LocalPath;
            var existed = File.Exists(targetPath);
            var loaderName = Lang.Text(
                "Download.Comp.Detail.DownloadResource",
                desc,
                Path.GetFileNameWithoutExtension(filename));
            var loader = new Loading.LoaderCombo<int>(
                loaderName,
                [
                    new LoaderDownload(
                        Lang.Text("Download.Comp.Detail.DownloadFile"),
                        [downloadFile])
                    {
                        ProgressWeight = 6d,
                        block = true
                    }
                ])
            {
                Identifier = $"CompDetail:Dependency:{targetPath}"
            };
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
                        if (!existed)
                            TryDeleteFile(targetPath);
                        break;

                    case LoadState.Aborted:
                        HintService.Show(Hints.Info(Lang.Text("Common.Status.Cancelled")));
                        if (!existed)
                            TryDeleteFile(targetPath);
                        break;

                    case LoadState.Waiting:
                    case LoadState.Loading:
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(newState), newState, null);
                }
            };
            loader.Start(1);
            Loading.LoaderTaskbarAdd(loader);
        }

        LoadingUi.Current?.RefreshDownloadButton();
    }

    /// <summary>
    ///     解析并启动必需前置下载；返回 false 表示用户取消，调用方不得继续下载本体。
    /// </summary>
    private static async Task<bool> ResolveAndStartDependenciesAsync(
        Comp.CompFile file,
        Comp.CompProject project,
        string targetDir,
        McInstance? targetInstance,
        List<Comp.CompLoaderType> fallbackLoaders)
    {
        try
        {
            var mcVersion = targetInstance?.Info.VanillaName
                ?? file.GameVersions.FirstOrDefault(version => version.Contains('.'))
                ?? string.Empty;
            var loaders = targetInstance is null
                ? fallbackLoaders
                : GetInstanceLoaders(targetInstance);
            if (loaders.Count == 0)
                loaders = fallbackLoaders;

            LauncherCommon.Log($"[CompDeps] 开始解析必需前置: {file.Dependencies.Count} 个依赖");
            var request = CompDependency.BuildRequest(
                file,
                project,
                mcVersion,
                loaders,
                targetDir);
            var result = await Task.Run(() => new ModDependencyResolver().Resolve(request));

            if (result.Unresolved.Count == 0 && result.ToInstall.Count == 0)
            {
                LauncherCommon.Log("[CompDeps] 所有必需前置已安装");
                return true;
            }

            var choice = CompDependency.ConfirmDependencyInstall(result);
            switch (choice)
            {
                case Comp.CompDepsInstallTypes.WithDeps:
                    StartDependencyDownloads(
                        result,
                        targetDir,
                        Comp.GetCompTypeName(file.Type));
                    return true;

                case Comp.CompDepsInstallTypes.Unresolved:
                    LauncherCommon.Log("[CompDeps] 发现无法解析的前置，将继续下载已解析的前置");
                    StartDependencyDownloads(
                        result,
                        targetDir,
                        Comp.GetCompTypeName(file.Type));
                    return true;

                case Comp.CompDepsInstallTypes.WithoutDeps:
                    LauncherCommon.Log("[CompDeps] 用户选择仅下载资源本体");
                    return true;

                case Comp.CompDepsInstallTypes.Cancel:
                default:
                    LauncherCommon.Log("[CompDeps] 用户取消安装");
                    return false;
            }
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "[CompDeps] 依赖解析失败，继续下载资源本体",
                LauncherLogLevel.Feedback);
            await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Download.Comp.Dependency.ResolveFailed.Title"),
                    Content = ExceptionDetails.Compose(
                        Lang.Text("Download.Comp.Dependency.ResolveFailed.Message"),
                        ex),
                    Buttons = DialogButtons.Create(
                        "Download.Comp.Dependency.ResolveFailed.Continue"),
                    IsWarn = true,
                    ForceWait = true
                });
            return true;
        }
    }

    private static List<Comp.CompLoaderType> GetInstanceLoaders(McInstance instance)
    {
        var loaders = new List<Comp.CompLoaderType>();
        if (instance.Info.HasForge || instance.Info.HasCleanroom)
            loaders.Add(Comp.CompLoaderType.Forge);
        if (instance.Info.HasFabric || instance.Info.HasLegacyFabric)
            loaders.Add(Comp.CompLoaderType.Fabric);
        if (instance.Info.HasQuilt)
            loaders.Add(Comp.CompLoaderType.Quilt);
        if (instance.Info.HasNeoForge)
            loaders.Add(Comp.CompLoaderType.NeoForge);
        if (instance.Info.HasLiteLoader)
            loaders.Add(Comp.CompLoaderType.LiteLoader);
        return loaders;
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
            LauncherCommon.Log(ex, $"删除未完成的资源文件失败：{path}", LauncherLogLevel.Developer);
        }
    }

    #endregion

    #region 安装到实例

    private readonly HashSet<string> _installingFiles = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>把 Mod / 资源包 / 光影 / 投影原理图直接安装到当前实例目录。</summary>
    private async Task InstallToInstanceAsync(Comp.CompFile file)
    {
        if (ViewModel.Project is not { } project)
            return;
        var instance = FindApplicableInstance(file, project);
        if (instance is null)
        {
            HintService.Show(
                Hints.Error(Lang.Text("Download.Comp.Detail.NoApplicableInstance")));
            return;
        }

        try
        {
            if (!instance.IsLoaded)
                instance.Load();
            var allowedLoaders = GetAllowedLoaders(file, project);

            var subFolder = LocalComp.GetPathNameByCompType(file.Type);
            if (string.IsNullOrEmpty(subFolder) || subFolder == "Nothing")
            {
                HintService.Show(Hints.Error(Lang.Text("Download.Comp.Error.OperationFailed")));
                return;
            }

            var baseFolder = instance.PathIndie;
            if (instance.Info.HasLabyMod && file.Type == Comp.CompType.Mod)
                baseFolder = Path.Combine(
                    baseFolder,
                    "labymod-neo",
                    "fabric",
                    instance.Info.VanillaName);
            var folder = Path.Combine(baseFolder, subFolder);
            Directory.CreateDirectory(folder);
            var targetPath = Path.Combine(folder, Comp.CompFileNameGet(project, file));
            if (!_installingFiles.Add(file.FileName))
                return;

            var started = false;
            try
            {
                if (file.Type == Comp.CompType.Mod
                    && Config.Download.Comp.AutoInstallDependencies
                    && file.Dependencies.Count > 0)
                {
                    var proceed = await ResolveAndStartDependenciesAsync(
                        file,
                        project,
                        folder,
                        instance,
                        allowedLoaders);
                    if (!proceed)
                        return;
                }

                StartDownloadTask(
                    file,
                    targetPath,
                    Comp.GetCompTypeName(file.Type),
                    $"CompDetail:Install:{targetPath}",
                    onTerminal: () => _installingFiles.Remove(file.FileName));
                started = true;
            }
            finally
            {
                if (!started)
                    _installingFiles.Remove(file.FileName);
            }
        }
        catch (Exception ex)
        {
            _installingFiles.Remove(file.FileName);
            LauncherCommon.Log(
                ex,
                "安装资源到实例失败",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Download.Comp.Error.OperationFailed"));
        }
    }

    private async Task InstallModpackAsync(Comp.CompFile file)
    {
        if (!RestrictedFeatures.Check("online-modpack-install"))
            return;
        if (ViewModel.Project is not { } project) return;
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
        switch (newState)
        {
            case LoadState.Waiting or LoadState.Loading:
                return;
            case LoadState.Finished:
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

            var tempPath = Path.Combine(
                LauncherPaths.Temp,
                "DownloadedWorld",
                $"{Guid.NewGuid():N}{Path.GetExtension(target)}");
            Directory.CreateDirectory(Path.GetDirectoryName(tempPath)!);

            var loaders = new List<Loading.LoaderBase>
            {
                new LoaderDownload(
                    Lang.Text("Download.Comp.Detail.DownloadWorldFile"),
                    [
                        file.ToNetFile(
                            tempPath,
                            Comp.DownloadReason.Standalone,
                            file.RawGameVersions.FirstOrDefault())
                    ])
                {
                    ProgressWeight = 10d,
                    block = true
                },
                new Loading.LoaderTask<int, int>(
                    Lang.Text("Download.Comp.Detail.InstallWorld"),
                    _ => LauncherCommon.ExtractFile(tempPath, targetFolder, Encoding.UTF8))
                {
                    ProgressWeight = 0.1d,
                    block = true
                },
                new Loading.LoaderTask<int, int>(
                    Lang.Text("Download.Comp.Detail.CleanCache"),
                    _ => File.Delete(tempPath))
            };

            var loaderName =
                $"{(ViewModel.Project?.FromCurseForge == true ? "CurseForge" : "Modrinth")} {Lang.Text("Download.Comp.Detail.WorldDownload")}：{ViewModel.Project?.TranslatedName}";
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
                        TryDeleteFile(tempPath);
                        break;
                    case LoadState.Aborted:
                        HintService.Show(
                            Hints.Info(
                                Lang.Text("Download.Comp.Detail.Task.Cancelled", sender.name)));
                        TryDeleteFile(tempPath);
                        break;
                    case LoadState.Waiting:
                    case LoadState.Loading:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(newState), newState, null);
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

    #endregion

    #endregion
}