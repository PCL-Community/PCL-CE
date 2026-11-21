using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Platform;
using PCL.Core.UI;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.Core.ViewModel;
using PCL.Navigation;
using PCL.Network;
using PCL.Network.Loaders;
using PCL.Views.Pages.Download;
using PCL.Views.Pages.Instance;

namespace PCL;

/// <summary>存档数据包管理页的视图模型。</summary>
public partial class PageInstanceSavesDatapackViewModel : ViewModelBase
{
    public enum DatapackFilterType
    {
        All = 0,
        Enabled = 1,
        Disabled = 2,
        CanUpdate = 3,
        Unavailable = 4
    }

    public enum DatapackSortMethod
    {
        FileName,
        CompName,
        CreateTime,
        DatapackFileSize
    }

    private readonly LocalCompSelectionState _selection = new();

    private int _filterAllCount;
    private int _filterDisabledCount;
    private int _filterEnabledCount;
    private int _filterErrorCount;
    private int _filterUpdatableCount;

    private IReadOnlyList<LocalComp.LocalCompFile> _searchScope = [];

    [ObservableProperty]
    public partial DatapackFilterType Filter { get; set; } = DatapackFilterType.All;

    [ObservableProperty]
    public partial DatapackSortMethod SortMethod { get; set; } = DatapackSortMethod.CompName;

    public string SortText
    {
        get
        {
            var key = SortMethod switch
            {
                DatapackSortMethod.CompName => "Instance.Resource.Sort.ResourceName",
                DatapackSortMethod.CreateTime => "Instance.Resource.Sort.AddTime",
                DatapackSortMethod.DatapackFileSize => "Instance.Resource.Sort.FileSize",
                _ => "Instance.Resource.Sort.FileName"
            };
            return Lang.Text("Instance.Resource.Sort.Text", Lang.Text(key));
        }
    }

    [ObservableProperty] public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty] public partial string SaveFolder { get; set; } = string.Empty;

    [ObservableProperty] public partial bool IsLoading { get; set; }

    [ObservableProperty] public partial bool HasError { get; set; }

    [ObservableProperty] public partial bool ShowContent { get; set; }

    [ObservableProperty] public partial bool ShowEmpty { get; set; }

    [ObservableProperty] public partial bool ShowSelection { get; set; }

    [ObservableProperty] public partial bool ShowFilterEnabled { get; set; }

    [ObservableProperty] public partial bool ShowFilterDisabled { get; set; }

    [ObservableProperty] public partial bool ShowFilterCanUpdate { get; set; }

    [ObservableProperty] public partial bool ShowFilterError { get; set; }

    public string FilterAllText =>
        IsSearching
            ? Lang.Text("Instance.Resource.Filter.SearchResult")
            : Lang.Text("Instance.Resource.Filter.AllWithCount", _filterAllCount);

    public string FilterEnabledText =>
        Lang.Text("Instance.Resource.Filter.EnabledWithCount", _filterEnabledCount);

    public string FilterDisabledText =>
        Lang.Text("Instance.Resource.Filter.DisabledWithCount", _filterDisabledCount);

    public string FilterCanUpdateText =>
        Lang.Text("Instance.Resource.Filter.UpdatableWithCount", _filterUpdatableCount);

    public string FilterErrorText =>
        Lang.Text("Instance.Resource.Filter.ErrorWithCount", _filterErrorCount);

    public string SelectedText => Lang.Text("Instance.Resource.SelectedCount", _selection.Count);

    [ObservableProperty] public partial bool CanEnable { get; set; }

    [ObservableProperty] public partial bool CanDisable { get; set; }

    [ObservableProperty] public partial bool CanUpdate { get; set; }

    [ObservableProperty] public partial bool CanFavoriteAndShare { get; set; }

    [ObservableProperty] public partial bool CanDelete { get; set; }

    public List<LocalComp.LocalCompFile> Entries { get; } = [];

    public List<LocalComp.LocalCompFile> VisibleEntries { get; } = [];

    public IReadOnlyCollection<string> SelectedKeys => _selection.Keys;

    public McInstance? Instance { get; private set; }

    public object? Page { get; private set; }

    public string? DatapacksPath =>
        string.IsNullOrEmpty(SaveFolder) ? null : Path.Combine(SaveFolder, "datapacks");

    public bool IsSearching => !string.IsNullOrWhiteSpace(SearchText);

    public event Action? ReloadRequested;

    public void SetInstance(McInstance? instance)
    {
        Instance = instance;
    }

    public void SetPage(object? page)
    {
        Page = page;
    }

    public void SetSaveFolder(string saveFolder)
    {
        SaveFolder = saveFolder;
    }

    /// <summary>重置搜索、筛选与选择；用于进入页面或刷新。</summary>
    public void ResetListViewState()
    {
        SearchText = string.Empty;
        Filter = DatapackFilterType.All;
        SortMethod = DatapackSortMethod.CompName;
        _selection.Clear();
        _searchScope = [];
        ShowSelection = false;
        UpdateSelectionState();
    }

    public void SetSearch(string search)
    {
        SearchText = search;
    }

    partial void OnSearchTextChanged(string value)
    {
        BuildList();
    }

    partial void OnFilterChanged(DatapackFilterType value)
    {
        BuildList();
    }

    partial void OnSortMethodChanged(DatapackSortMethod value)
    {
        OnPropertyChanged(nameof(SortText));
    }

    public void SetFilter(int index)
    {
        Filter = (DatapackFilterType)Math.Clamp(index, 0, 4);
    }

    public void SetSortMethod(DatapackSortMethod method)
    {
        SortMethod = method;
        OnPropertyChanged(nameof(SortText));
        BuildList();
    }

    public static IReadOnlyList<string> GetSortNames()
    {
        return
        [
            Lang.Text("Instance.Resource.Sort.FileName"),
            Lang.Text("Instance.Resource.Sort.ResourceName"),
            Lang.Text("Instance.Resource.Sort.AddTime"),
            Lang.Text("Instance.Resource.Sort.FileSize")
        ];
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (string.IsNullOrEmpty(SaveFolder))
        {
            Entries.Clear();
            VisibleEntries.Clear();
            BuildList();
            return;
        }

        IsLoading = true;
        Error = null;
        OnPropertyChanged(nameof(HasError));
        ShowContent = false;
        ShowEmpty = false;
        try
        {
            var entries = await RunAsync(_ => LoadEntriesAsync(SaveFolder, Instance));
            if (entries is null)
                return;
            Entries.Clear();
            Entries.AddRange(entries);
            BuildList();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsLoading = false;
            ShowContent = Entries.Count > 0;
            ShowEmpty = !IsLoading && Entries.Count == 0;
            HasError = !string.IsNullOrEmpty(Error);
            OnPropertyChanged(nameof(HasError));
        }
    }

    private async Task<List<LocalComp.LocalCompFile>> LoadEntriesAsync(
        string saveFolder,
        McInstance? instance)
    {
        var path = Path.Combine(saveFolder, "datapacks");
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);

        if (instance is null)
            return LoadEntriesDirectory(path);
        if (!instance.IsLoaded)
            instance.Load();

        return await Task.Run(() =>
        {
            try
            {
                var data = new LocalComp.CompLocalLoaderData
                {
                    gameVersion = instance,
                    frm = Page,
                    loaders = [Comp.CompLoaderType.Minecraft],
                    compPath = path,
                    compType = Comp.CompType.DataPack
                };
                Loading.LoaderFolderRun(
                    LocalComp.CompResourceListLoader,
                    path,
                    Loading.LoaderFolderRunType.ForceRun,
                    loaderInput: data,
                    waitForExit: true);
                var output = LocalComp.CompResourceListLoader.output;
                return output is null
                    ? LoadEntriesDirectory(path)
                    : [.. output.OrderBy(entry => entry.path, StringComparer.OrdinalIgnoreCase)];
            }
            catch
            {
                return LoadEntriesDirectory(path);
            }
        });
    }

    private static List<LocalComp.LocalCompFile> LoadEntriesDirectory(string path)
    {
        var result = Directory
            .EnumerateDirectories(path)
            .Select(directory => new LocalComp.LocalCompFile(Path.Combine(directory, "__FOLDER__")))
            .ToList();
        foreach (var file in Directory.EnumerateFiles(path))
            if (LocalComp.LocalCompFile.IsCompFile(file, Comp.CompType.DataPack))
                try
                {
                    var entry = new LocalComp.LocalCompFile(file);
                    entry.LoadBasicInfo();
                    result.Add(entry);
                }
                catch (Exception ex)
                {
                    LauncherCommon.Log(
                        ex,
                        $"读取数据包元数据失败：{file}",
                        LauncherLogLevel.Developer);
                }

        result.Sort((a, b) =>
            string.Compare(a.path, b.path, StringComparison.OrdinalIgnoreCase));
        return result;
    }

    [RelayCommand]
    public Task RetryAsync()
    {
        return LoadAsync();
    }

    public void Rebuild()
    {
        BuildList();
    }

    private void BuildList()
    {
        var available = Entries
            .Select(entry => entry.path)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _selection.RemoveWhere(key => !available.Contains(key));

        var scope = SearchEntries(Entries);
        _searchScope = scope;
        var filtered = scope.Where(MatchFilter).ToList();

        var sorted = (SortMethod switch
        {
            DatapackSortMethod.CompName => filtered.OrderBy(
                entry => entry.IsFolder ? string.Empty : entry.Name,
                StringComparer.OrdinalIgnoreCase),
            DatapackSortMethod.CreateTime => filtered.OrderByDescending(
                LocalCompFileOperations.GetSafeWriteTime),
            DatapackSortMethod.DatapackFileSize => filtered.OrderByDescending(
                LocalCompFileOperations.GetSafeSize),
            _ => filtered.OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
        }).ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase);
        filtered = [.. sorted.OrderBy(entry => entry.IsFolder ? 0 : 1)];

        _selection.RemoveWhere(key =>
            !filtered.Any(entry => string.Equals(
                entry.path,
                key,
                StringComparison.OrdinalIgnoreCase)));
        VisibleEntries.Clear();
        VisibleEntries.AddRange(filtered);
        ShowContent = Entries.Count > 0;
        ShowEmpty = !IsLoading && Entries.Count == 0;
        ShowSelection = _selection.Count > 0;
        UpdateFilterState(scope);
        OnPropertyChanged(nameof(SortText));
        UpdateSelectionState();
    }

    private IReadOnlyList<LocalComp.LocalCompFile> SearchEntries(
        IReadOnlyList<LocalComp.LocalCompFile> entries)
    {
        var keyword = SearchText.Trim();
        if (keyword.Length == 0)
            return [.. entries];

        var query = new List<SearchService.SearchEntry<LocalComp.LocalCompFile>>(entries.Count);
        foreach (var entry in entries)
        {
            var sources = new List<SearchService.SearchSource>
            {
                new(entry.Name)
            };
            if (!string.IsNullOrWhiteSpace(entry.Version))
                sources.Add(new SearchService.SearchSource(entry.Version, 0.2d));
            if (!string.IsNullOrWhiteSpace(entry.Description))
                sources.Add(new SearchService.SearchSource(entry.Description, 0.4d));
            if (entry.Comp is { } comp)
            {
                if (!string.IsNullOrEmpty(comp.RawName))
                    sources.Add(new SearchService.SearchSource(comp.RawName));
                if (!string.IsNullOrEmpty(comp.TranslatedName))
                    sources.Add(new SearchService.SearchSource(comp.TranslatedName));
                if (!string.IsNullOrEmpty(comp.Description))
                    sources.Add(new SearchService.SearchSource(comp.Description, 0.4d));
                if (comp.Tags is { Count: > 0 } tags)
                    sources.Add(new SearchService.SearchSource(string.Join("", tags), 0.2d));
            }

            query.Add(
                new SearchService.SearchEntry<LocalComp.LocalCompFile>
                {
                    item = entry,
                    searchSource = sources
                });
        }

        return
        [
            .. SearchService
                .Search(query, keyword, SearchService.MaxLocalSearchDepth, 0.35d)
                .Select(result => result.item)
                .OfType<LocalComp.LocalCompFile>()
        ];
    }

    private bool MatchFilter(LocalComp.LocalCompFile entry)
    {
        return Filter switch
        {
            DatapackFilterType.Enabled => entry.State
                == LocalComp.LocalCompFile.LocalFileStatus.Fine,
            DatapackFilterType.Disabled => entry.State
                == LocalComp.LocalCompFile.LocalFileStatus.Disabled,
            DatapackFilterType.CanUpdate => entry.CanUpdate,
            DatapackFilterType.Unavailable => entry.State
                == LocalComp.LocalCompFile.LocalFileStatus.Unavailable,
            _ => true
        };
    }

    private void UpdateFilterState(IReadOnlyList<LocalComp.LocalCompFile>? source = null)
    {
        var scope = source ?? _searchScope;
        if (scope.Count == 0 && source is null)
            scope = Entries;
        var updatable = scope.Count(entry => entry.CanUpdate);
        var unavailable = scope.Count(entry =>
            entry.State == LocalComp.LocalCompFile.LocalFileStatus.Unavailable);
        var enabled =
            scope.Count(entry => entry.State == LocalComp.LocalCompFile.LocalFileStatus.Fine);
        var disabled = scope.Count(entry =>
            entry.State == LocalComp.LocalCompFile.LocalFileStatus.Disabled);
        _filterAllCount = scope.Count;
        _filterEnabledCount = enabled;
        _filterDisabledCount = disabled;
        _filterUpdatableCount = updatable;
        _filterErrorCount = unavailable;
        OnPropertyChanged(nameof(FilterAllText));
        OnPropertyChanged(nameof(FilterEnabledText));
        OnPropertyChanged(nameof(FilterDisabledText));
        OnPropertyChanged(nameof(FilterCanUpdateText));
        OnPropertyChanged(nameof(FilterErrorText));
        ShowFilterCanUpdate = Filter == DatapackFilterType.CanUpdate || updatable > 0;
        ShowFilterError = Filter == DatapackFilterType.Unavailable || unavailable > 0;
        ShowFilterEnabled = Filter == DatapackFilterType.Enabled
            || (enabled > 0 && enabled < scope.Count);
        ShowFilterDisabled = Filter == DatapackFilterType.Disabled || disabled > 0;
    }

    public void SetSelected(LocalComp.LocalCompFile entry, bool selected)
    {
        if (selected)
            _selection.Add(entry.path);
        else
            _selection.Remove(entry.path);
        UpdateSelectionState();
    }

    public bool IsSelected(LocalComp.LocalCompFile entry)
    {
        return _selection.Contains(entry.path);
    }

    public void ClearSelection()
    {
        _selection.Clear();
        UpdateSelectionState();
        ShowSelection = false;
    }

    [RelayCommand]
    public void ToggleSelectAll()
    {
        if (VisibleEntries.Count == 0)
            return;
        var allSelected = VisibleEntries.All(entry => _selection.Contains(entry.path));
        foreach (var entry in VisibleEntries)
            if (allSelected)
                _selection.Remove(entry.path);
            else
                _selection.Add(entry.path);
        BuildList();
    }

    private void UpdateSelectionState()
    {
        ShowSelection = _selection.Count > 0;
        if (_selection.Count == 0)
        {
            CanEnable = false;
            CanDisable = false;
            CanUpdate = false;
            CanFavoriteAndShare = false;
            CanDelete = false;
            return;
        }

        OnPropertyChanged(nameof(SelectedText));
        var selected = Entries.Where(entry => _selection.Contains(entry.path)).ToList();
        CanEnable = selected.Any(entry =>
            entry.State == LocalComp.LocalCompFile.LocalFileStatus.Disabled);
        CanDisable =
            selected.Any(entry => entry.State == LocalComp.LocalCompFile.LocalFileStatus.Fine);
        CanUpdate = selected.Any(entry => entry.CanUpdate);
        CanFavoriteAndShare = selected.Count == _selection.Count
            && selected.All(entry => entry.Comp is not null);
        CanDelete = selected.Count > 0;
    }

    [RelayCommand]
    public void OpenFolder()
    {
        var path = DatapacksPath;
        if (path is null)
            return;
        Directory.CreateDirectory(path);
        Basics.OpenPath(path);
    }

    [RelayCommand]
    public void DownloadNew()
    {
        if (!string.IsNullOrEmpty(SaveFolder))
        {
            var datapacks = Path.Combine(SaveFolder, "datapacks");
            Directory.CreateDirectory(datapacks);
            PageDownloadCompDetail.SetCachedFolder(Comp.CompType.DataPack, datapacks);
        }

        var instance = PageInstanceLeft.ResolveInstance();
        if (instance is not null)
            PageRouter.Navigate(
                PageIds.Download,
                SubPageIds.DownloadDataPack,
                new CompSearchContext(instance.Info.VanillaName, Comp.CompLoaderType.Any));
        else
            PageRouter.Navigate(PageIds.Download, SubPageIds.DownloadDataPack);
    }

    public async Task InstallFilesAsync(IReadOnlyList<string> files)
    {
        try
        {
            var path = DatapacksPath;
            if (path is null || files.Count == 0)
                return;
            if (files.Any(file => file.Contains(
                    "$RECYCLE.BIN",
                    StringComparison.OrdinalIgnoreCase)))
            {
                HintService.Show(
                    Hints.Error(Lang.Text("Instance.Resource.Install.RestoreFromRecycleBin")));
                return;
            }

            var accepted = files
                .Where(file => file.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                    || file.EndsWith(".zip.disabled", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (accepted.Count == 0)
            {
                var extension = Path.GetExtension(files[0]).TrimStart('.');
                HintService.Show(
                    Hints.Error(
                        Lang.Text(
                            "Instance.Resource.Install.UnsupportedFormat",
                            extension,
                            Lang.Text("Download.Comp.Type.DataPack"),
                            "zip")));
                return;
            }

            var confirm = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Instance.Saves.Datapack.Install.Title"),
                    Content = Lang.Text("Instance.Saves.Datapack.Install.Message"),
                    Buttons = DialogButtons.OkCancel
                });
            if (!confirm.IsConfirmed)
                return;

            Directory.CreateDirectory(path);
            var installed = 0;
            foreach (var file in accepted)
            {
                var target = Path.Combine(path, Path.GetFileName(file));
                if (string.Equals(file, target, StringComparison.OrdinalIgnoreCase))
                {
                    installed++;
                    continue;
                }

                if (File.Exists(target))
                {
                    var overwrite = await DialogService.Instance.ShowAsync(
                        new TextDialogRequest
                        {
                            Title = Lang.Text("Instance.Resource.Install.OverwriteConfirm.Title"),
                            Content = Lang.Text(
                                "Instance.Resource.Install.OverwriteConfirm.Message",
                                Path.GetFileName(file)),
                            Buttons = DialogButtons.From(
                                DialogButton.Create(Lang.Text("Common.Action.Overwrite")),
                                DialogButton.Create(Lang.Text("Common.Action.Cancel")))
                        });
                    if (!overwrite.IsConfirmed)
                        continue;
                }

                await Task.Run(() => File.Copy(file, target, true));
                installed++;
            }

            if (installed == 0)
                return;
            HintService.Show(
                installed == 1
                    ? Hints.Success(
                        Lang.Text(
                            "Instance.Resource.Install.SuccessSingle",
                            Path.GetFileName(accepted[0])))
                    : Hints.Success(
                        Lang.Text(
                            "Instance.Resource.Install.SuccessMultiple",
                            installed,
                            Lang.Text("Instance.Saves.Left.Datapack"))));
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ReportError("Instance.Resource.Error.OperationFailed", ex);
        }
    }

    public async Task ExportInfoAsync()
    {
        try
        {
            var mode = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Instance.Resource.Export.Mode.Title"),
                    Content = Lang.Text("Instance.Saves.Datapack.Export.Mode.Message"),
                    Buttons = DialogButtons.Create(
                        "Instance.Resource.Export.Mode.Txt",
                        "Instance.Resource.Export.Mode.Csv",
                        "Common.Action.Cancel")
                });
            if (mode.Button is not (1 or 2))
                return;

            var isCsv = mode.Button == 2;
            var savePath = await SystemDialogs.SelectSaveFileAsync(
                Lang.Text("Instance.Resource.Export.SelectSaveLocation"),
                isCsv ? "datapacks.csv" : "datapacks.txt",
                Lang.Text("Instance.Resource.Export.FilesFilter"));
            if (string.IsNullOrWhiteSpace(savePath))
                return;

            var builder = new StringBuilder();
            if (isCsv)
                builder.AppendLine("FileName,Enabled,Folder,Size,Version,Description");
            foreach (var entry in Entries.OrderBy(
                         entry => entry.Name,
                         StringComparer.OrdinalIgnoreCase))
                if (isCsv)
                    builder.AppendLine(
                        string.Join(
                            ',',
                            new[]
                            {
                                entry.Name,
                                (entry.State != LocalComp.LocalCompFile.LocalFileStatus.Disabled)
                                .ToString(),
                                entry.IsFolder.ToString(),
                                LocalCompFileOperations.GetSafeSize(entry).ToString(),
                                entry.Version,
                                entry.Description ?? string.Empty
                            }.Select(LocalCompFileOperations.CsvEscape)));
                else
                    builder.AppendLine(entry.Name);

            await File.WriteAllTextAsync(savePath, builder.ToString(), Encoding.UTF8);
            Basics.OpenPath(savePath);
        }
        catch (Exception ex)
        {
            ReportError("Instance.Resource.Error.OperationFailed", ex);
        }
    }

    public Task UpdateEntryAsync(LocalComp.LocalCompFile entry)
    {
        return UpdateEntriesAsync([entry]);
    }

    public async Task UpdateEntriesAsync(IReadOnlyList<LocalComp.LocalCompFile> entries)
    {
        var targets = entries
            .Where(entry => entry is { UpdateFile: not null, CanUpdate: true })
            .ToList();
        if (targets.Count == 0)
            return;

        var registeredFolders = targets
            .Select(entry => Path.GetDirectoryName(LocalCompFileOperations.GetEntryPath(entry))
                ?? DatapacksPath)
            .Where(folder => !string.IsNullOrEmpty(folder))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (var folder in registeredFolders)
            CompLocalUpdateRegistry.Begin(folder!);
        var released = false;

        try
        {
            if (!States.Hint.FunctionDatapackUpdate || targets.Count >= 15)
            {
                var warning = await DialogService.Instance.ShowAsync(
                    new TextDialogRequest
                    {
                        Title = Lang.Text("Instance.Saves.Datapack.Update.Warning.Title"),
                        Content = Lang.Text("Instance.Saves.Datapack.Update.Warning.Message"),
                        Buttons = DialogButtons.Create(
                            "Instance.Saves.Datapack.Update.Warning.Confirm",
                            "Common.Action.Cancel"),
                        IsWarn = true
                    });
                if (!warning.IsConfirmed)
                {
                    Release();
                    return;
                }

                States.Hint.FunctionDatapackUpdate = true;
            }

            var downloads = new List<DownloadFile>();
            var replacements =
                new List<(LocalComp.LocalCompFile Entry, string DownloadPath, string TargetPath)>();
            var skippedUnsafe = 0;
            foreach (var entry in targets)
            {
                var update = entry.UpdateFile;
                if (!update.Available || update.DownloadUrls.Count == 0)
                    continue;

                var fileName = Path.GetFileName(update.FileName);
                if (string.IsNullOrWhiteSpace(fileName)
                    || !string.Equals(fileName, update.FileName, StringComparison.Ordinal))
                {
                    skippedUnsafe++;
                    continue;
                }

                var folder = Path.GetFullPath(
                    Path.GetDirectoryName(LocalCompFileOperations.GetEntryPath(entry))
                    ?? DatapacksPath ?? string.Empty);
                if (string.IsNullOrEmpty(folder))
                    continue;
                var targetPath = Path.GetFullPath(
                    Path.Combine(
                        folder,
                        fileName
                        + (entry.State == LocalComp.LocalCompFile.LocalFileStatus.Disabled
                            ? ".disabled"
                            : string.Empty)));
                if (!targetPath.StartsWith(
                        folder + Path.DirectorySeparatorChar,
                        StringComparison.Ordinal))
                {
                    skippedUnsafe++;
                    continue;
                }

                var downloadPath = Path.Combine(
                    LauncherPaths.Temp,
                    "DownloadedComp",
                    $"{Guid.NewGuid():N}-{Path.GetFileName(targetPath)}");
                var checker = string.IsNullOrEmpty(update.Hash)
                    ? new FileChecker(1024L)
                    : new FileChecker(hash: update.Hash);
                downloads.Add(new DownloadFile(update.DownloadUrls, downloadPath, checker));
                replacements.Add((entry, downloadPath, targetPath));
            }

            if (skippedUnsafe > 0)
                HintService.Show(
                    Hints.Error(
                        Lang.Text(
                            "Instance.Saves.Datapack.Update.UnsafeFilesSkipped",
                            skippedUnsafe)));

            if (downloads.Count == 0)
            {
                Release();
                return;
            }

            var replaced = 0;
            var saveName = Path.GetFileName(
                (SaveFolder ?? string.Empty).TrimEnd(Path.DirectorySeparatorChar));
            var loader = new Loading.LoaderCombo<int>(
                Lang.Text("Instance.Saves.Datapack.Update.Task.Title", saveName),
                [
                    new LoaderDownload(
                        Lang.Text("Instance.Saves.Datapack.Update.Task.DownloadFiles"),
                        downloads)
                    {
                        ProgressWeight = targets.Count * 1.5d
                    },
                    new Loading.LoaderTask<int, int>(
                        Lang.Text("Instance.Saves.Datapack.Update.Task.ReplaceFiles"),
                        _ => replaced = ReplaceDatapackFiles(replacements))
                ])
            {
                OnStateChanged = changed =>
                {
                    switch (changed.State)
                    {
                        case LoadState.Finished:
                        {
                            switch (replaced)
                            {
                                case 1:
                                    HintService.Show(
                                        Hints.Success(
                                            Lang.Text(
                                                "Instance.Resource.Update.SuccessSingle",
                                                replacements[0].Entry.Name)));
                                    break;

                                case > 1:
                                    HintService.Show(
                                        Hints.Success(
                                            Lang.Text(
                                                "Instance.Resource.Update.SuccessMultiple",
                                                replaced)));
                                    break;
                            }

                            foreach (var replacement in replacements)
                                LocalCompFileOperations.TryDeleteFile(replacement.DownloadPath);
                            _selection.Clear();
                            LauncherCommon.RunInUi(() => ReloadRequested?.Invoke());
                            Release();
                            break;
                        }

                        case LoadState.Failed:
                        {
                            foreach (var replacement in replacements)
                                LocalCompFileOperations.TryDeleteFile(replacement.DownloadPath);
                            HintService.Show(
                                Hints.Error(
                                    Lang.Text(
                                        "Instance.Resource.Update.Failed",
                                        changed.Error?.Message ?? string.Empty)));
                            Release();
                            break;
                        }

                        case LoadState.Aborted:
                        {
                            foreach (var replacement in replacements)
                                LocalCompFileOperations.TryDeleteFile(replacement.DownloadPath);
                            HintService.Show(
                                Hints.Info(Lang.Text("Instance.Resource.Update.Aborted")));
                            Release();
                            break;
                        }

                        case LoadState.Waiting:
                        case LoadState.Loading:
                            break;

                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            };
            loader.Start();
            Loading.LoaderTaskbarAdd(loader);
            LoadingUi.Current?.RefreshDownloadButton();
        }
        catch (Exception ex)
        {
            Release();
            ReportError("Instance.Resource.Update.Failed", ex);
        }

        return;

        void Release()
        {
            if (released)
                return;
            released = true;
            foreach (var folder in registeredFolders)
                CompLocalUpdateRegistry.End(folder!);
        }
    }

    private static int ReplaceDatapackFiles(
        IReadOnlyList<(LocalComp.LocalCompFile Entry, string DownloadPath, string TargetPath)>
            replacements)
    {
        var replaced = 0;
        foreach (var (entry, downloadPath, targetPath) in replacements)
        {
            if (!File.Exists(downloadPath))
                continue;
            if (File.Exists(targetPath))
                LocalCompFileOperations.DeleteFileToRecycleBin(targetPath);
            var directory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.Move(downloadPath, targetPath, true);
            replaced++;

            foreach (var variant in new[]
                     {
                         entry.path,
                         $"{entry.path}.disabled",
                         $"{entry.path}.old"
                     })
                if (File.Exists(variant)
                    && !string.Equals(
                        variant,
                        targetPath,
                        StringComparison.OrdinalIgnoreCase))
                    LocalCompFileOperations.DeleteFileToRecycleBin(variant);
        }

        return replaced;
    }

    [RelayCommand]
    public void Toggle(LocalComp.LocalCompFile entry)
    {
        Toggle(entry, true);
    }

    public void Toggle(LocalComp.LocalCompFile entry, bool requestReload)
    {
        try
        {
            var source = LocalCompFileOperations.GetEntryPath(entry);
            var disabled = entry.State == LocalComp.LocalCompFile.LocalFileStatus.Disabled;
            var target = disabled
                ? source.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)
                    ? source[..^".disabled".Length]
                    : source[..^".old".Length]
                : $"{source}.disabled";
            if (File.Exists(target) || Directory.Exists(target))
            {
                HintService.Show(
                    Hints.Error(
                        Lang.Text(
                            "Instance.Saves.Datapack.Replace.FileNameConflict",
                            Path.GetFileName(target))));
                return;
            }

            if (entry.IsFolder)
                Directory.Move(source, target);
            else
                File.Move(source, target);
            if (requestReload)
                ReloadRequested?.Invoke();
        }
        catch (Exception ex)
        {
            HintService.Show(Hints.Error(Lang.Text("Instance.Saves.Datapack.ToggleWarning")));
            LauncherCommon.Log(ex, "切换数据包状态失败", LauncherLogLevel.Feedback);
        }
    }

    public void SetSelectedEnabled(bool enabled)
    {
        var targets = Entries
            .Where(entry => _selection.Contains(entry.path))
            .Where(entry => enabled
                ? entry.State == LocalComp.LocalCompFile.LocalFileStatus.Disabled
                : entry.State == LocalComp.LocalCompFile.LocalFileStatus.Fine)
            .ToList();
        foreach (var entry in targets)
            Toggle(entry, false);
        _selection.Clear();
        ReloadRequested?.Invoke();
    }

    [RelayCommand]
    public Task UpdateSelectedAsync()
    {
        var targets = Entries
            .Where(entry => _selection.Contains(entry.path) && entry.CanUpdate)
            .ToList();
        return UpdateEntriesAsync(targets);
    }

    public List<Comp.CompProject> GetFavoriteProjects()
    {
        return LocalCompFileOperations.GetSelectedProjects(
            Entries,
            _selection.Keys,
            entry => entry.path);
    }

    public IReadOnlyList<string> GetShareIds()
    {
        return LocalCompFileOperations.GetSelectedShareIds(
            Entries,
            _selection.Keys,
            entry => entry.path);
    }

    [RelayCommand]
    public async Task DeleteSelectedAsync()
    {
        var targets = Entries.Where(entry => _selection.Contains(entry.path)).ToList();
        if (targets.Count > 0)
            await DeleteAsync(targets);
    }

    public async Task DeleteAsync(
        IReadOnlyList<LocalComp.LocalCompFile> targets,
        bool permanentDelete = false)
    {
        try
        {
            var names = string.Join(", ", targets.Select(entry => entry.Name));
            var canRecycle = !permanentDelete
                && targets.All(entry =>
                    ApplicationRuntime.Current.Platform.RecycleBin.IsSupported(
                        LocalCompFileOperations.GetEntryPath(entry)));
            var result = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Instance.Saves.Datapack.Delete.ConfirmTitle"),
                    Content = Lang.Text(
                        canRecycle
                            ? "Instance.Saves.Datapack.Delete.ConfirmMessage"
                            : "Instance.Saves.Datapack.Delete.ConfirmPermanent",
                        names),
                    Buttons = DialogButtons.OkCancel,
                    IsWarn = true
                });
            if (!result.IsConfirmed)
                return;

            var moved = 0;
            var permanent = 0;
            foreach (var entry in targets)
            {
                var useRecycle = !permanentDelete
                    && ApplicationRuntime.Current.Platform.RecycleBin.IsSupported(
                        LocalCompFileOperations.GetEntryPath(entry));
                var outcome = useRecycle
                    ? ApplicationRuntime.Current.Platform.RecycleBin.Move(
                        LocalCompFileOperations.GetEntryPath(entry),
                        out _)
                    : RecycleBinOutcome.Unsupported;
                switch (outcome)
                {
                    case RecycleBinOutcome.Moved:
                        moved++;
                        break;

                    case RecycleBinOutcome.Unsupported:
                        if (entry.IsFolder)
                            Directory.Delete(LocalCompFileOperations.GetEntryPath(entry), true);
                        else
                            File.Delete(LocalCompFileOperations.GetEntryPath(entry));
                        permanent++;
                        break;

                    case RecycleBinOutcome.Failed:
                    default:
                        throw new IOException(Lang.Text("Instance.Saves.DeleteFailed"));
                }
            }

            if (moved > 0)
                HintService.Show(
                    Hints.Success(
                        targets.Count == 1
                            ? Lang.Text(
                                "Instance.Saves.Datapack.Delete.RecycleSingle",
                                targets[0].Name)
                            : Lang.Text("Instance.Saves.Datapack.Delete.RecycleMultiple", moved)));
            if (permanent > 0)
                HintService.Show(
                    Hints.Success(
                        targets.Count == 1
                            ? Lang.Text(
                                "Instance.Saves.Datapack.Delete.PermanentSingle",
                                targets[0].Name)
                            : Lang.Text(
                                "Instance.Saves.Datapack.Delete.PermanentMultiple",
                                permanent)));
            _selection.Clear();
            ReloadRequested?.Invoke();
        }
        catch (Exception ex)
        {
            ReportError("Instance.Resource.Delete.Failed", ex);
        }
    }

    private static void ReportError(string titleKey, Exception ex)
    {
        LauncherCommon.Log(
            ex,
            Lang.Text(titleKey),
            LauncherLogLevel.Feedback,
            userSummary: Lang.Text(titleKey));
        HintService.Show(Hints.Error(Lang.Text(titleKey)));
    }
}