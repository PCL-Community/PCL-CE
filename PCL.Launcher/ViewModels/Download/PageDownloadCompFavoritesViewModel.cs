using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Core.App.Localization;
using PCL.Core.Platform;
using PCL.Core.UI;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.Core.ViewModel;
using PCL.Network;
using PCL.Network.Loaders;

namespace PCL;

/// <summary>下载页收藏夹列表页的视图模型。</summary>
public partial class PageDownloadCompFavoritesViewModel : ViewModelBase
{
    private readonly List<FavoriteItemViewModel> _compItems = [];
    private readonly HashSet<FavoriteItemViewModel> _selectedItems = [];
    private bool _autoRetried;

    [ObservableProperty]
    public partial ObservableCollection<FavoriteFolderOption> Folders { get; set; } = [];

    [ObservableProperty] public partial int SelectedFolderIndex { get; set; } = -1;

    [ObservableProperty]
    public partial ObservableCollection<FavoriteGroupViewModel> Groups { get; set; } = [];

    [ObservableProperty] public partial string SelectedText { get; set; } = "";

    [ObservableProperty] public partial string SearchText { get; set; } = "";

    [ObservableProperty] public partial bool ShowHintGetFail { get; set; }

    [ObservableProperty] public partial bool ShowSearchBox { get; set; } = true;

    [ObservableProperty] public partial bool ShowContentList { get; set; } = true;

    [ObservableProperty] public partial bool ShowNoContent { get; set; } = true;

    [ObservableProperty] public partial bool ShowNoResults { get; set; }

    [ObservableProperty] public partial bool HasSelection { get; set; }

    [ObservableProperty] public partial bool IsLoading { get; set; }

    [ObservableProperty] public partial bool IsLoadFailed { get; set; }

    [ObservableProperty] public partial bool IsBulkDownloadEnabled { get; set; } = true;

    /// <summary>列表底部给选择条让出的像素高度。</summary>
    [ObservableProperty]
    public partial double ContentListBottomInset { get; set; }

    public Loading.LoaderTask<int, List<Comp.CompProject>>? Loader { get; private set; }

    public IReadOnlyCollection<FavoriteItemViewModel> SelectedItems => _selectedItems;

    public Comp.CompFavorites.FavData? SelectedFolder
    {
        get
        {
            var groups = Comp.CompFavorites.FavoritesList;
            if (groups is null || groups.Count == 0) return null;
            return SelectedFolderIndex >= 0 && SelectedFolderIndex < groups.Count
                ? groups[SelectedFolderIndex]
                : groups[0];
        }
    }

    public event Action? ContentChanged;

    public event Action? StateChanged;

    public event Action? FoldersChanged;

    public event Action? SelectionChanged;

    public event Action? SearchReset;

    partial void OnIsBulkDownloadEnabledChanged(bool value)
    {
        SelectionChanged?.Invoke();
    }

    public Loading.LoaderTask<int, List<Comp.CompProject>> EnsureLoader()
    {
        if (Loader is not null)
            return Loader;

        Loader = new Loading.LoaderTask<int, List<Comp.CompProject>>(
            Lang.Text("Download.Comp.Favorites.Loading"),
            task =>
            {
                var group = SelectedFolder;
                if (group is null || group.Favs.Count == 0)
                {
                    task.output = [];
                    return;
                }

                task.output = Comp.CompRequest.GetCompProjectsByIds([.. group.Favs]);
            },
            inputDelegate: () => 0)
        {
            reloadTimeout = 60 * 1000
        };
        Loader.OnStateChangedUi += OnLoaderStateChanged;
        return Loader;
    }

    public void RefreshFolders(string? preferredId = null)
    {
        var groups = Comp.CompFavorites.FavoritesList;
        var selectedId = preferredId ?? SelectedFolder?.Id;
        Folders =
        [
            .. (groups ?? []).Select(group => new FavoriteFolderOption(group.Id, group.Name))
        ];
        var index = -1;
        if (selectedId is not null)
            index = Folders.ToList().FindIndex(option => option.Id == selectedId);
        SelectedFolderIndex = index >= 0 ? index : 0;
        FoldersChanged?.Invoke();
    }

    public void SelectFolder(int index)
    {
        if (index < 0 || SelectedFolderIndex == index)
            return;
        SelectedFolderIndex = index;
        ClearSelection();
        Reload();
    }

    public void Reload()
    {
        if (Loader is null)
            return;
        SearchText = string.Empty;
        ClearSelection();
        _compItems.Clear();
        Groups = [];
        ShowHintGetFail = false;
        ShowNoContent = true;
        ShowNoResults = false;
        ShowSearchBox = true;
        _autoRetried = false;
        IsLoading = true;
        IsLoadFailed = false;
        SearchReset?.Invoke();
        ContentChanged?.Invoke();
        StateChanged?.Invoke();
        Loader.Start(0, true);
    }

    public void CompleteLoad()
    {
        var group = SelectedFolder;
        var output = Loader?.output ?? [];
        ShowHintGetFail = group is not null && output.Count < group.Favs.Count;
        _compItems.Clear();
        _selectedItems.Clear();
        foreach (var project in output)
        {
            var note = group is not null && group.Notes.TryGetValue(project.Id, out var value)
                ? value
                : null;
            var item = new FavoriteItemViewModel(project, note);
            item.PropertyChanged += OnItemPropertyChanged;
            _compItems.Add(item);
        }

        RebuildGroups();
    }

    public void RebuildGroups()
    {
        var searchText = SearchText.Trim();
        var isSearching = searchText.Length > 0;
        IEnumerable<FavoriteItemViewModel> dataSource = _compItems;
        if (isSearching)
        {
            var entries = new List<SearchService.SearchEntry<FavoriteItemViewModel>>();
            foreach (var item in _compItems)
            {
                var project = item.Project;
                var sources = new List<SearchService.SearchSource>
                {
                    new(project.RawName)
                };
                if (!string.IsNullOrEmpty(project.Description))
                    sources.Add(new SearchService.SearchSource(project.Description, 0.4d));
                if ((project.TranslatedName ?? "") != (project.RawName ?? ""))
                    sources.Add(new SearchService.SearchSource(project.TranslatedName));
                sources.Add(
                    new SearchService.SearchSource(
                        string.Join("", project.Tags ?? []),
                        0.2d));
                entries.Add(
                    new SearchService.SearchEntry<FavoriteItemViewModel>
                    {
                        item = item,
                        searchSource = sources
                    });
            }

            dataSource = SearchService
                .Search(entries, searchText, SearchService.MaxLocalSearchDepth, 0.35d)
                .Select(result => result.item)
                .OfType<FavoriteItemViewModel>();
        }

        var order = new List<int>();
        var grouped = new Dictionary<int, List<FavoriteItemViewModel>>();
        foreach (var item in dataSource)
        {
            var type = isSearching ? -1 : item.Type;
            if (!grouped.TryGetValue(type, out var list))
            {
                grouped[type] = list = [];
                order.Add(type);
            }

            list.Add(item);
        }

        var groups = new List<FavoriteGroupViewModel>();
        foreach (var type in order)
        {
            var group = new FavoriteGroupViewModel(type);
            group.SetItems(grouped[type]);
            if (group.IsVisible)
                groups.Add(group);
        }

        Groups = new ObservableCollection<FavoriteGroupViewModel>(groups);
        ShowSearchBox = _compItems.Count > 0 || isSearching;
        ShowContentList = _compItems.Count > 0;
        ShowNoContent = _compItems.Count == 0 && !isSearching;
        ShowNoResults = isSearching && groups.Count == 0;
        RefreshSelectionBar();
        ContentChanged?.Invoke();
    }

    public void SelectAll()
    {
        foreach (var item in Groups.SelectMany(group => group.Items))
            item.IsChecked = true;
        RefreshSelectionBar();
    }

    public void ClearSelection()
    {
        foreach (var item in _compItems)
            item.IsChecked = false;
        _selectedItems.Clear();
        RefreshSelectionBar();
    }

    public void RemoveFavorite(FavoriteItemViewModel item)
    {
        try
        {
            var group = SelectedFolder;
            if (group is null) return;
            _compItems.Remove(item);
            _selectedItems.Remove(item);
            group.Favs.Remove(item.Project.Id);
            Comp.CompFavorites.Save();
            RebuildGroups();
            RefreshAfterRemoval();
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "[CompFavorites] 移除收藏时发生错误");
        }
    }

    public void RefreshAfterRemoval()
    {
        var group = SelectedFolder;
        if (group is null || group.Favs.Count == 0)
            Reload();
        else
            Loader?.Start(0, true);
    }

    [RelayCommand]
    private void SelectAllItems()
    {
        SelectAll();
    }

    [RelayCommand]
    private void ClearSelectedItems()
    {
        ClearSelection();
    }

    [RelayCommand]
    private async Task ShareSelectedAsync()
    {
        try
        {
            var ids = _selectedItems
                .Select(item => item.Project.Id)
                .ToHashSet();
            if (ids.Count == 0) return;
            PlatformServices.Clipboard?.SetText(Comp.CompFavorites.GetShareCode(ids));
            ClearSelection();
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "[CompFavorites] 分享收藏时发生错误",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Download.Comp.Favorites.Error.OperationFailed"));
        }

        await Task.CompletedTask;
    }

    [RelayCommand]
    private void CancelSelected()
    {
        try
        {
            var group = SelectedFolder;
            if (group is null) return;
            foreach (var item in _selectedItems.ToList())
            {
                _compItems.Remove(item);
                group.Favs.Remove(item.Project.Id);
            }

            _selectedItems.Clear();
            Comp.CompFavorites.Save();
            RebuildGroups();
            RefreshAfterRemoval();
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "[CompFavorites] 取消收藏时发生错误",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Download.Comp.Favorites.Error.OperationFailed"));
        }
    }

    [RelayCommand]
    private async Task BatchDownloadAsync()
    {
        if (!IsBulkDownloadEnabled) return;
        var projects = _selectedItems
            .Select(item => item.Project)
            .ToList();
        if (projects.Count == 0) return;

        IsBulkDownloadEnabled = false;
        try
        {
            var confirm = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Download.Comp.Favorites.Dialog.BulkDownload.Title"),
                    Content = Lang.Text("Download.Comp.Favorites.Dialog.BulkDownload.Content"),
                    Buttons = DialogButtons.ContinueCancel,
                    IsWarn = true
                });
            if (!confirm.IsConfirmed) return;

            var hasMod = projects.Any(project => project.Type == Comp.CompType.Mod);
            var supportedLoaders = new List<Comp.CompLoaderType>();
            var first = true;
            foreach (var project in projects.Where(project => project.Type == Comp.CompType.Mod))
                if (first)
                {
                    supportedLoaders = [.. project.ModLoaders];
                    first = false;
                }
                else
                {
                    supportedLoaders = [.. supportedLoaders.Intersect(project.ModLoaders)];
                }

            if (hasMod && supportedLoaders.Count == 0)
            {
                HintService.Show(
                    Hints.Error(Lang.Text("Download.Comp.Favorites.Hint.SelectLoader")));
                return;
            }

            var desiredLoader = Comp.CompLoaderType.Any;
            if (hasMod && supportedLoaders.Count > 0)
            {
                var loaderChoice = await DialogService.Instance.ShowAsync(
                    new SelectDialogRequest
                    {
                        Title = Lang.Text("Download.Comp.Favorites.Dialog.SelectLoader.Title"),
                        Options =
                        [
                            .. supportedLoaders
                                .Select(loader => new DialogOption
                                {
                                    Text = loader.ToString()
                                })
                        ],
                        Buttons = DialogButtons.OkCancel,
                        HighLight = false
                    });
                if (!loaderChoice.IsConfirmed || loaderChoice.SelectedIndex is null) return;
                desiredLoader = supportedLoaders[loaderChoice.SelectedIndex.Value];
            }

            HintService.Show(Hints.Info(Lang.Text("Download.Comp.Favorites.Hint.LoadingVersions")));
            var loader = new Loading.LoaderCombo<List<string>>(
                Lang.Text(
                    "Download.Comp.Favorites.LoaderName.BatchDownload",
                    LauncherCommon.GetUuid()),
                [
                    new Loading.LoaderTask<List<string>, List<DownloadFile>>(
                        Lang.Text("Download.Comp.Favorites.LoaderName.QueryInfo"),
                        task => task.output = BuildBatchDownloadFiles(
                            task,
                            projects,
                            desiredLoader))
                    {
                        ProgressWeight = 2d
                    },
                    new LoaderDownload(
                        Lang.Text("Download.Comp.Favorites.LoaderName.BatchDownloadSuitable"),
                        [])
                    {
                        ProgressWeight = 8d
                    }
                ]);
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
                                ?? Lang.Text("Download.Comp.Favorites.Error.OperationFailed")));
                        break;

                    case LoadState.Aborted:
                        HintService.Show(Hints.Info(Lang.Text("Common.Status.Cancelled")));
                        break;

                    case LoadState.Waiting:
                    case LoadState.Loading:
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(newState), newState, null);
                }
            };
            loader.Start(new List<string>(projects.Select(project => project.Id)));
            Loading.LoaderTaskbarAdd(loader);
            ClearSelection();
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "批量下载收藏时发生错误",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Download.Comp.Favorites.Error.OperationFailed"));
        }
        finally
        {
            IsBulkDownloadEnabled = true;
            RefreshSelectionBar();
        }
    }

    [RelayCommand]
    private async Task ShowGetFailedAsync()
    {
        var group = SelectedFolder;
        if (group is null) return;
        var loadedIds = (Loader?.output ?? [])
            .Select(project => project.Id)
            .ToHashSet();
        var failedIds = group.Favs.Where(id => !loadedIds.Contains(id)).Distinct().ToList();
        if (failedIds.Count == 0) return;

        var content = Lang.Text("Download.Comp.Favorites.Dialog.GetFailed.Content")
            + Environment.NewLine
            + Environment.NewLine
            + string.Join(Environment.NewLine, failedIds.Select(id => $" - {id}"));
        await DialogService.Instance.ShowAsync(
            new TextDialogRequest
            {
                Title = Lang.Text("Download.Comp.Favorites.Dialog.GetFailed.Title"),
                Content = content,
                Buttons = DialogButtons.From(
                    DialogButton.Create(Lang.Text("Common.Action.Confirm")),
                    DialogButton.Create(
                        Lang.Text("Download.Comp.Favorites.Dialog.GetFailed.CopyIds"),
                        () => PlatformServices.Clipboard?.SetText(
                            string.Join(Environment.NewLine, failedIds))),
                    DialogButton.Create(
                        Lang.Text("Download.Comp.Favorites.Dialog.GetFailed.Remove"),
                        () =>
                        {
                            foreach (var id in failedIds)
                                group.Favs.Remove(id);
                            Comp.CompFavorites.Save();
                            HintService.Show(
                                Hints.Success(
                                    Lang.Text("Download.Comp.Favorites.Hint.Removed")));
                            Reload();
                        })),
                ForceWait = true
            });
    }

    [RelayCommand]
    private async Task EditNoteAsync(Comp.CompProject? project)
    {
        if (project is null) return;
        var group = SelectedFolder;
        if (group is null) return;
        try
        {
            group.Notes.TryGetValue(project.Id, out var note);
            var result = await DialogService.Instance.ShowAsync(
                new InputDialogRequest
                {
                    Title = Lang.Text("Download.Comp.Favorites.EditNote"),
                    DefaultValue = note ?? ""
                });
            if (!result.IsConfirmed) return;
            group.Notes[project.Id] = result.InputText ?? "";
            var item = _compItems.FirstOrDefault(candidate => candidate.Project.Id == project.Id);
            item?.SetNote(result.InputText);
            Comp.CompFavorites.Save();
            RebuildGroups();
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "[CompFavorites] 修改备注时发生错误",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Download.Comp.Favorites.Error.OperationFailed"));
        }
    }

    [RelayCommand]
    private void ShareCurrentFolder()
    {
        try
        {
            var group = SelectedFolder;
            if (group is null || group.Favs.Count == 0)
            {
                HintService.Show(
                    Hints.Info(Lang.Text("Download.Comp.Favorites.Hint.NothingShared")));
                return;
            }

            PlatformServices.Clipboard?.SetText(Comp.CompFavorites.GetShareCode(group.Favs));
            HintService.Show(Hints.Success(Lang.Text("Common.Status.Success"), false));
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "[CompFavorites] 分享收藏时发生错误",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Download.Comp.Favorites.Error.OperationFailed"));
        }
    }

    [RelayCommand]
    private async Task NewFolderAsync()
    {
        try
        {
            var result = await DialogService.Instance.ShowAsync(
                new InputDialogRequest
                {
                    Title = Lang.Text("Download.Comp.Favorites.Menu.New"),
                    Content = Lang.Text("Download.Comp.Favorites.Dialog.NewPrompt"),
                    HighLight = false
                });
            var name = result.IsConfirmed ? result.InputText?.Trim() : null;
            if (string.IsNullOrEmpty(name)) return;
            var newFolder = Comp.CompFavorites.GetNewFav(name, null);
            Comp.CompFavorites.FavoritesList?.Add(newFolder);
            Comp.CompFavorites.Save();
            RefreshFolders(newFolder.Id);
            Reload();
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "新建收藏夹时发生错误",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Download.Comp.Favorites.Error.OperationFailed"));
        }
    }

    [RelayCommand]
    private async Task RenameFolderAsync()
    {
        var group = SelectedFolder;
        if (group is null) return;
        try
        {
            var result = await DialogService.Instance.ShowAsync(
                new InputDialogRequest
                {
                    Title = Lang.Text("Download.Comp.Favorites.Dialog.Rename.Title"),
                    DefaultValue = group.Name,
                    HighLight = false
                });
            var newName = result.IsConfirmed ? result.InputText?.Trim() : null;
            if (string.IsNullOrEmpty(newName) || newName == group.Name) return;
            group.Name = newName;
            Comp.CompFavorites.Save();
            RefreshFolders(group.Id);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "重命名收藏夹时发生错误",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Download.Comp.Favorites.Error.OperationFailed"));
        }
    }

    [RelayCommand]
    private async Task DeleteFolderAsync()
    {
        var group = SelectedFolder;
        var groups = Comp.CompFavorites.FavoritesList;
        if (group is null || groups is null) return;
        try
        {
            if (groups.Count == 1)
            {
                HintService.Show(
                    Hints.Info(Lang.Text("Download.Comp.Favorites.Hint.LastCollection")));
                return;
            }

            var result = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Download.Comp.Favorites.Dialog.Delete.Title"),
                    Content = Lang.Text(
                        "Download.Comp.Favorites.Dialog.Delete.Confirm",
                        group.Name,
                        group.Favs.Count,
                        group.Id),
                    Buttons = DialogButtons.From(
                        DialogButton.Create(Lang.Text("Common.Option.No")),
                        DialogButton.Create(Lang.Text("Common.Option.Yes")),
                        DialogButton.Create(Lang.Text("Common.Option.No"))),
                    IsWarn = true
                });
            if (result.Button != 2) return;
            groups.Remove(group);
            Comp.CompFavorites.Save();
            HintService.Show(Hints.Success(Lang.Text("Download.Comp.Favorites.Hint.Deleted")));
            RefreshFolders();
            Reload();
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "删除收藏夹时发生错误",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Download.Comp.Favorites.Error.OperationFailed"));
        }
    }

    [RelayCommand]
    private async Task ImportFavoritesAsync()
    {
        try
        {
            var codeResult = await DialogService.Instance.ShowAsync(
                new InputDialogRequest
                {
                    Title = Lang.Text("Download.Comp.Favorites.Dialog.Import.Input"),
                    HintText = Lang.Text("Download.Comp.Favorites.Dialog.Import.Hint"),
                    HighLight = false
                });
            var code = codeResult.IsConfirmed ? codeResult.InputText : null;
            if (string.IsNullOrWhiteSpace(code)) return;
            var ids = Comp.CompFavorites.GetIdsByShareCode(code);
            if (ids.Count == 0)
            {
                HintService.Show(
                    Hints.Info(Lang.Text("Download.Comp.Favorites.Hint.NothingShared")));
                return;
            }

            var typeResult = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Common.Dialog.Title"),
                    Content = Lang.Text("Download.Comp.Favorites.Dialog.Import.Type"),
                    Buttons = DialogButtons.From(
                        DialogButton.Create(
                            Lang.Text("Download.Comp.Favorites.Dialog.Import.NewFolder")),
                        DialogButton.Create(
                            Lang.Text("Download.Comp.Favorites.Dialog.Import.CurrentFolder")))
                });
            switch (typeResult.Button)
            {
                case 1:
                {
                    var nameResult = await DialogService.Instance.ShowAsync(
                        new InputDialogRequest
                        {
                            Title = Lang.Text("Download.Comp.Favorites.Dialog.Import.New"),
                            Content = Lang.Text("Download.Comp.Favorites.Dialog.NewPrompt"),
                            HighLight = false
                        });
                    var name = nameResult.IsConfirmed ? nameResult.InputText?.Trim() : null;
                    if (string.IsNullOrEmpty(name)) return;
                    var newFolder = Comp.CompFavorites.GetNewFav(name, ids);
                    Comp.CompFavorites.FavoritesList?.Add(newFolder);
                    Comp.CompFavorites.Save();
                    RefreshFolders(newFolder.Id);
                    Reload();
                    break;
                }
                case 2:
                {
                    var group = SelectedFolder;
                    if (group is null) return;
                    foreach (var id in ids)
                        group.Favs.Add(id);
                    Comp.CompFavorites.Save();
                    Reload();
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "解析分享数据失败",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Download.Comp.Favorites.Error.OperationFailed"));
        }
    }

    private void OnLoaderStateChanged(
        Loading.LoaderBase sender,
        LoadState newState,
        LoadState oldState)
    {
        switch (newState)
        {
            case LoadState.Loading:
                IsLoading = true;
                IsLoadFailed = false;
                break;

            case LoadState.Finished:
                IsLoading = false;
                IsLoadFailed = false;
                CompleteLoad();
                break;

            case LoadState.Failed:
                IsLoading = false;
                IsLoadFailed = true;
                if (!_autoRetried
                    && sender.Error?.Message.Contains(
                        Lang.Text("Common.Error.InvalidJson"),
                        StringComparison.OrdinalIgnoreCase)
                    == true)
                {
                    _autoRetried = true;
                    Loader?.Start(0, true);
                }

                break;

            case LoadState.Waiting:
            case LoadState.Aborted:
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(newState), newState, null);
        }

        StateChanged?.Invoke();
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FavoriteItemViewModel.IsChecked)
            || sender is not FavoriteItemViewModel item)
            return;
        if (item.IsChecked)
            _selectedItems.Add(item);
        else
            _selectedItems.Remove(item);
        RefreshSelectionBar();
    }

    private void RefreshSelectionBar()
    {
        var count = _selectedItems.Count;
        HasSelection = count > 0;
        ContentListBottomInset = count > 0 ? 80d : 0d;
        if (count > 0)
            SelectedText = Lang.Text("Download.Comp.Favorites.Hint.SelectedCount", count);
        SelectionChanged?.Invoke();
    }

    private static List<DownloadFile> BuildBatchDownloadFiles(
        Loading.LoaderTask<List<string>, List<DownloadFile>> task,
        List<Comp.CompProject> projects,
        Comp.CompLoaderType desiredLoader)
    {
        var projectFiles = new Dictionary<Comp.CompProject, List<Comp.CompFile>>();
        List<string>? commonVersions = null;
        foreach (var project in projects)
        {
            List<Comp.CompFile> files;
            try
            {
                files = Comp.CompFilesGet(project.Id, project.FromCurseForge);
            }
            catch (Exception ex)
            {
                LauncherCommon.Log(
                    ex,
                    $"获取 {project.TranslatedName} 的下载信息失败",
                    LauncherLogLevel.Hint,
                    userSummary: Lang.Text("Download.Comp.Favorites.Error.OperationFailed"));
                continue;
            }

            files = project.Type == Comp.CompType.Mod
                ?
                [
                    .. files.Where(file => desiredLoader == Comp.CompLoaderType.Any
                        || file.ModLoaders.Contains(desiredLoader))
                ]
                : files;
            projectFiles[project] = files;

            var versions = files.SelectMany(file => file.GameVersions).Distinct().ToList();
            commonVersions = commonVersions is null
                ? versions
                : [.. commonVersions.Intersect(versions)];
            if (commonVersions.Count == 0)
            {
                HintService.Show(Hints.Error(Lang.Text("Download.Comp.Favorites.Hint.NoResource")));
                task.Abort();
                return [];
            }
        }

        if (commonVersions is null || commonVersions.Count == 0 || projectFiles.Count == 0)
        {
            HintService.Show(Hints.Error(Lang.Text("Download.Comp.Favorites.Hint.NoResource")));
            task.Abort();
            return [];
        }

        var versionResult = LauncherCommon.RunInUiWait(() =>
            DialogService
                .Instance.ShowAsync(
                    new SelectDialogRequest
                    {
                        Title = Lang.Text("Download.Comp.Favorites.Dialog.SelectVersion.Title"),
                        Options =
                        [
                            .. commonVersions.Select(version => new DialogOption
                            {
                                Text = version
                            })
                        ],
                        Buttons = DialogButtons.OkCancel,
                        HighLight = false
                    })
                .GetAwaiter()
                .GetResult());
        if (versionResult is null
            || !versionResult.IsConfirmed
            || versionResult.SelectedIndex is null)
        {
            task.Abort();
            return [];
        }

        var selectedVersion = commonVersions[versionResult.SelectedIndex.Value];
        var saveFolder = LauncherCommon.RunInUiWait(() => SystemDialogs.SelectFolder());
        if (string.IsNullOrWhiteSpace(saveFolder))
        {
            task.Abort();
            return [];
        }

        var result = new List<DownloadFile>();
        foreach (var (project, files) in projectFiles)
        {
            var file = files
                .Where(candidate => candidate.GameVersions.Contains(selectedVersion))
                .OrderByDescending(candidate => candidate.ReleaseDate)
                .FirstOrDefault();
            if (file is null) continue;
            var fileName = Comp.CompFileNameGet(project, file);
            result.Add(file.ToNetFile(Path.Combine(saveFolder, fileName)));
        }

        return result;
    }
}

/// <summary>收藏夹下拉中的一个文件夹选项。</summary>
public sealed record FavoriteFolderOption(string Id, string Name);

/// <summary>收藏夹列表中的一个分组（按工程类型划分）。</summary>
public sealed partial class FavoriteGroupViewModel : ObservableObject
{
    private readonly List<FavoriteItemViewModel> _items = [];

    public FavoriteGroupViewModel(int compType)
    {
        CompType = compType;
        RefreshTitle();
    }

    /// <summary>分组对应的工程类型；-1 表示搜索结果分组。</summary>
    public int CompType { get; }

    [ObservableProperty] public partial string Title { get; set; } = "";

    [ObservableProperty] public partial bool IsVisible { get; set; } = true;

    public IReadOnlyList<FavoriteItemViewModel> Items => _items;

    public void SetItems(IReadOnlyList<FavoriteItemViewModel> items)
    {
        _items.Clear();
        _items.AddRange(items);
        IsVisible = items.Count > 0;
        RefreshTitle();
    }

    public void RefreshTitle()
    {
        Title = string.Format(
            PageDownloadCompFavoritesViewModel.GetCategoryTitleFormat(CompType),
            _items.Count);
    }
}

/// <summary>收藏夹列表中的一个条目。</summary>
public sealed partial class FavoriteItemViewModel : ObservableObject
{
    public FavoriteItemViewModel(Comp.CompProject project, string? note)
    {
        Project = project;
        SetNote(note);
    }

    public Comp.CompProject Project { get; }

    public int Type => (int)Project.Type;

    public string Title => Project.TranslatedName;

    public string Info =>
        (Project.Description ?? string.Empty)
        .Replace("\r", string.Empty)
        .Replace("\n", string.Empty);

    public string Logo =>
        string.IsNullOrEmpty(Project.LogoUrl)
            ? $"{LauncherPaths.ImageRoot}Icons/NoIcon.png"
            : Project.LogoUrl;

    public IEnumerable<string> Tags => Project.Tags;

    /// <summary>备注原文（不含括号与空格），用于编辑对话框的默认值。</summary>
    public string Note { get; private set; } = "";

    [ObservableProperty] public partial string NoteText { get; set; } = "";

    [ObservableProperty] public partial bool IsChecked { get; set; }

    public void SetNote(string? note)
    {
        Note = note ?? "";
        NoteText = string.IsNullOrWhiteSpace(Note) ? "" : $" ({Note})";
    }
}

public partial class PageDownloadCompFavoritesViewModel
{
    public static string GetCategoryTitleFormat(int type)
    {
        return type switch
        {
            -1 => Lang.Text("Download.Comp.Favorites.SearchResults.Title"),
            (int)Comp.CompType.Mod => Lang.Text("Download.Comp.Favorites.Category.Mod"),
            (int)Comp.CompType.ModPack => $"{Lang.Text("Download.Comp.Type.Modpack")} ({{0}})",
            (int)Comp.CompType.ResourcePack =>
                $"{Lang.Text("Download.Comp.Type.ResourcePack")} ({{0}})",
            (int)Comp.CompType.Shader => $"{Lang.Text("Download.Comp.Type.Shader")} ({{0}})",
            (int)Comp.CompType.DataPack => $"{Lang.Text("Download.Comp.Type.DataPack")} ({{0}})",
            (int)Comp.CompType.Plugin => $"{Lang.Text("Download.Comp.Type.Plugin")} ({{0}})",
            (int)Comp.CompType.World => $"{Lang.Text("Download.Comp.Type.World")} ({{0}})",
            _ => $"{Lang.Text("Download.Comp.Favorites.UnknownType")} ({{0}})"
        };
    }
}