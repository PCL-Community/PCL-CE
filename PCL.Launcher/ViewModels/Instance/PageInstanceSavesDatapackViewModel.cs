using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.IO;
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

    private readonly HashSet<string> _selected = new(StringComparer.OrdinalIgnoreCase);

    public PageInstanceSavesDatapackViewModel()
    {
        UpdateSortText();
    }

    [ObservableProperty]
    public partial DatapackFilterType Filter { get; set; } = DatapackFilterType.All;

    [ObservableProperty]
    public partial DatapackSortMethod SortMethod { get; set; } = DatapackSortMethod.CompName;

    [ObservableProperty] public partial string SortText { get; set; } = string.Empty;

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

    [ObservableProperty] public partial string FilterAllText { get; set; } = string.Empty;

    [ObservableProperty] public partial string FilterEnabledText { get; set; } = string.Empty;

    [ObservableProperty] public partial string FilterDisabledText { get; set; } = string.Empty;

    [ObservableProperty] public partial string FilterCanUpdateText { get; set; } = string.Empty;

    [ObservableProperty] public partial string FilterErrorText { get; set; } = string.Empty;

    [ObservableProperty] public partial string SelectedText { get; set; } = string.Empty;

    [ObservableProperty] public partial bool CanEnable { get; set; }

    [ObservableProperty] public partial bool CanDisable { get; set; }

    [ObservableProperty] public partial bool CanUpdate { get; set; }

    [ObservableProperty] public partial bool CanFavoriteAndShare { get; set; }

    [ObservableProperty] public partial bool CanDelete { get; set; }

    public List<DatapackEntry> Entries { get; } = [];

    public List<DatapackEntry> VisibleEntries { get; } = [];

    public IReadOnlyCollection<string> SelectedKeys => _selected;

    public static List<string> UpdatingVersions { get; } = [];

    public string? DatapacksPath =>
        string.IsNullOrEmpty(SaveFolder) ? null : Path.Combine(SaveFolder, "datapacks");

    public bool IsSearching => !string.IsNullOrWhiteSpace(SearchText);

    public event Action? ReloadRequested;

    public void SetSaveFolder(string saveFolder)
    {
        SaveFolder = saveFolder;
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

    public void SetFilter(int index)
    {
        Filter = (DatapackFilterType)Math.Clamp(index, 0, 4);
    }

    public void SetSortMethod(DatapackSortMethod method)
    {
        SortMethod = method;
        UpdateSortText();
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

    private void UpdateSortText()
    {
        var key = SortMethod switch
        {
            DatapackSortMethod.CompName => "Instance.Resource.Sort.ResourceName",
            DatapackSortMethod.CreateTime => "Instance.Resource.Sort.AddTime",
            DatapackSortMethod.DatapackFileSize => "Instance.Resource.Sort.FileSize",
            _ => "Instance.Resource.Sort.FileName"
        };
        SortText = Lang.Text("Instance.Resource.Sort.Text", Lang.Text(key));
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
            var entries = await RunAsync(_ => LoadEntriesAsync(SaveFolder));
            if (entries is null)
                return;
            Entries.Clear();
            Entries.AddRange(entries);
            BuildList();
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

    private static async Task<List<DatapackEntry>> LoadEntriesAsync(string saveFolder)
    {
        var path = Path.Combine(saveFolder, "datapacks");
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);

        var instance = InstanceList.McMcInstanceSelected;
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
                    frm = null,
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
                if (output is null)
                    return LoadEntriesDirectory(path);
                return output
                    .Select(file => new DatapackEntry
                    {
                        Path = file.path,
                        Name = Path.GetFileName(file.path),
                        IsDirectory = file.IsFolder,
                        File = file
                    })
                    .OrderBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch
            {
                return LoadEntriesDirectory(path);
            }
        });
    }

    private static List<DatapackEntry> LoadEntriesDirectory(string path)
    {
        return
        [
            .. Directory
                .EnumerateFileSystemEntries(path)
                .OrderBy(entry => entry, StringComparer.OrdinalIgnoreCase)
                .Select(CreateEntry)
        ];
    }

    private static DatapackEntry CreateEntry(string path)
    {
        var name = Path.GetFileName(path);
        var isDirectory = Directory.Exists(path);
        LocalComp.LocalCompFile? file = null;
        if (!isDirectory)
            try
            {
                file = new LocalComp.LocalCompFile(path);
                file.Load();
            }
            catch (Exception ex)
            {
                LauncherCommon.Log(ex, $"读取数据包元数据失败：{path}", LauncherLogLevel.Developer);
            }

        return new DatapackEntry
        {
            Path = path,
            Name = name,
            IsDirectory = isDirectory,
            File = file
        };
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
        var keyword = SearchText.Trim();
        var available = Entries
            .Select(entry => entry.RawKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _selected.RemoveWhere(key => !available.Contains(key));

        var filtered = Entries
            .Where(MatchFilter)
            .Where(entry =>
                keyword.Length == 0
                || entry.DisplayName.Contains(keyword, StringComparison.OrdinalIgnoreCase));

        filtered = (SortMethod switch
        {
            DatapackSortMethod.CompName => filtered.OrderBy(
                entry => entry.IsDirectory ? string.Empty : entry.File?.Name ?? entry.DisplayName,
                StringComparer.OrdinalIgnoreCase),
            DatapackSortMethod.CreateTime => filtered.OrderByDescending(SafeWriteTime),
            DatapackSortMethod.DatapackFileSize => filtered.OrderByDescending(SafeSize),
            _ => filtered.OrderBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
        }).ThenBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase);
        filtered = filtered.OrderBy(entry => entry.IsDirectory ? 0 : 1);

        VisibleEntries.Clear();
        VisibleEntries.AddRange(filtered);
        ShowContent = Entries.Count > 0;
        ShowEmpty = !IsLoading && Entries.Count == 0;
        ShowSelection = _selected.Count > 0;
        UpdateFilterState();
        UpdateSortText();
        UpdateSelectionState();
    }

    private bool MatchFilter(DatapackEntry entry)
    {
        return Filter switch
        {
            DatapackFilterType.Enabled => entry is { Disabled: false, Unavailable: false },
            DatapackFilterType.Disabled => entry.Disabled,
            DatapackFilterType.CanUpdate => entry.CanUpdate,
            DatapackFilterType.Unavailable => entry.Unavailable,
            _ => true
        };
    }

    private void UpdateFilterState()
    {
        var updatable = Entries.Count(entry => entry.CanUpdate);
        var unavailable = Entries.Count(entry => entry.Unavailable);
        var enabled = Entries.Count(entry => entry is { Disabled: false, Unavailable: false });
        var disabled = Entries.Count(entry => entry.Disabled);
        FilterAllText = IsSearching
            ? Lang.Text("Instance.Resource.Filter.SearchResult")
            : Lang.Text("Instance.Resource.Filter.AllWithCount", Entries.Count);
        FilterEnabledText = Lang.Text("Instance.Resource.Filter.EnabledWithCount", enabled);
        FilterDisabledText = Lang.Text("Instance.Resource.Filter.DisabledWithCount", disabled);
        FilterCanUpdateText = Lang.Text("Instance.Resource.Filter.UpdatableWithCount", updatable);
        FilterErrorText = Lang.Text("Instance.Resource.Filter.ErrorWithCount", unavailable);
        ShowFilterCanUpdate = Filter == DatapackFilterType.CanUpdate || updatable > 0;
        ShowFilterError = Filter == DatapackFilterType.Unavailable || unavailable > 0;
        ShowFilterEnabled = Filter == DatapackFilterType.Enabled
            || (enabled > 0 && enabled < Entries.Count);
        ShowFilterDisabled = Filter == DatapackFilterType.Disabled || disabled > 0;
    }

    public void SetSelected(DatapackEntry entry, bool selected)
    {
        if (selected)
            _selected.Add(entry.RawKey);
        else
            _selected.Remove(entry.RawKey);
        UpdateSelectionState();
    }

    public bool IsSelected(DatapackEntry entry)
    {
        return _selected.Contains(entry.RawKey);
    }

    public void ClearSelection()
    {
        _selected.Clear();
        UpdateSelectionState();
        ShowSelection = false;
    }

    [RelayCommand]
    public void ToggleSelectAll()
    {
        if (VisibleEntries.Count == 0)
            return;
        var allSelected = VisibleEntries.All(entry => _selected.Contains(entry.RawKey));
        foreach (var entry in VisibleEntries)
            if (allSelected)
                _selected.Remove(entry.RawKey);
            else
                _selected.Add(entry.RawKey);
        BuildList();
    }

    private void UpdateSelectionState()
    {
        ShowSelection = _selected.Count > 0;
        if (_selected.Count == 0)
        {
            CanEnable = false;
            CanDisable = false;
            CanUpdate = false;
            CanFavoriteAndShare = false;
            CanDelete = false;
            return;
        }

        SelectedText = Lang.Text("Instance.Resource.SelectedCount", _selected.Count);
        var selected = Entries.Where(entry => _selected.Contains(entry.RawKey)).ToList();
        CanEnable = selected.Any(entry => entry.Disabled);
        CanDisable = selected.Any(entry => !entry.Disabled);
        CanUpdate = selected.Any(entry => entry.CanUpdate);
        CanFavoriteAndShare = selected.Any(entry => entry.File?.Comp is not null);
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
                PageType.Download,
                PageSubType.DownloadDataPack,
                new CompSearchContext(instance.Info.VanillaName, Comp.CompLoaderType.Any));
        else
            PageRouter.Navigate(PageType.Download, PageSubType.DownloadDataPack);
    }

    public async Task InstallFilesAsync(IReadOnlyList<string> files)
    {
        try
        {
            var path = DatapacksPath;
            if (path is null || files.Count == 0)
                return;

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
            var overwriteAll = false;
            var installed = 0;
            foreach (var file in accepted)
            {
                var target = Path.Combine(path, Path.GetFileName(file));
                if (string.Equals(file, target, StringComparison.OrdinalIgnoreCase))
                {
                    installed++;
                    continue;
                }

                if (File.Exists(target) && !overwriteAll)
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
                    overwriteAll = true;
                }

                File.Copy(file, target, true);
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
            var savePath = SystemDialogs.SelectSaveFile(
                Lang.Text("Instance.Resource.Export.SelectSaveLocation"),
                isCsv ? "datapacks.csv" : "datapacks.txt",
                Lang.Text("Instance.Resource.Export.FilesFilter"));
            if (string.IsNullOrWhiteSpace(savePath))
                return;

            var builder = new StringBuilder();
            if (isCsv)
                builder.AppendLine("FileName,Enabled,Folder,Size,Version,Description");
            foreach (var entry in Entries.OrderBy(
                         entry => entry.DisplayName,
                         StringComparer.OrdinalIgnoreCase))
                if (isCsv)
                    builder.AppendLine(
                        string.Join(
                            ',',
                            new[]
                            {
                                entry.DisplayName,
                                (!entry.Disabled).ToString(),
                                entry.IsDirectory.ToString(),
                                SafeSize(entry).ToString(),
                                entry.Version,
                                entry.Description ?? string.Empty
                            }.Select(CsvEscape)));
                else
                    builder.AppendLine(entry.DisplayName);

            await File.WriteAllTextAsync(savePath, builder.ToString(), Encoding.UTF8);
            Basics.OpenPath(savePath);
        }
        catch (Exception ex)
        {
            ReportError("Instance.Resource.Error.OperationFailed", ex);
        }
    }

    public async Task UpdateEntryAsync(DatapackEntry entry)
    {
        if (entry.File is not { UpdateFile: not null, CanUpdate: true } file)
            return;
        try
        {
            var folder = Path.GetFullPath(Path.GetDirectoryName(entry.Path) ?? DatapacksPath);
            if (string.IsNullOrEmpty(folder))
                return;
            var update = file.UpdateFile;
            if (!update.Available || update.DownloadUrls.Count == 0)
                return;

            var fileName = Path.GetFileName(update.FileName);
            if (string.IsNullOrWhiteSpace(fileName)
                || !string.Equals(fileName, update.FileName, StringComparison.Ordinal))
                return;

            var targetPath = Path.GetFullPath(
                Path.Combine(
                    folder,
                    fileName + (entry.Disabled ? ".disabled" : string.Empty)));
            if (!targetPath.StartsWith(
                    folder + Path.DirectorySeparatorChar,
                    StringComparison.Ordinal))
                return;

            var downloadPath = Path.Combine(
                LauncherPaths.Temp,
                "DownloadedComp",
                Path.GetFileName(targetPath));
            var checker = string.IsNullOrEmpty(update.Hash)
                ? new FileChecker(1024L)
                : new FileChecker(hash: update.Hash);
            var loader = new Loading.LoaderCombo<int>(
                Lang.Text("Instance.Resource.Update.Task.DownloadFiles"),
                [
                    new LoaderDownload(
                        Lang.Text("Instance.Resource.Update.Task.DownloadFiles"),
                        [new DownloadFile(update.DownloadUrls, downloadPath, checker)])
                ])
            {
                OnStateChanged = changed =>
                {
                    switch (changed.State)
                    {
                        case LoadState.Finished:
                            if (!File.Exists(downloadPath))
                                break;
                            if (File.Exists(targetPath))
                                DeleteFileToRecycleBin(targetPath);
                            File.Move(downloadPath, targetPath, true);
                            foreach (var variant in new[]
                                     {
                                         entry.Path,
                                         $"{entry.Path}.disabled",
                                         $"{entry.Path}.old"
                                     })
                                if (File.Exists(variant)
                                    && !string.Equals(
                                        variant,
                                        targetPath,
                                        StringComparison.OrdinalIgnoreCase))
                                    DeleteFileToRecycleBin(variant);
                            HintService.Show(
                                Hints.Success(
                                    Lang.Text(
                                        "Instance.Resource.Update.SuccessSingle",
                                        entry.DisplayName)));
                            LauncherCommon.RunInUi(() => ReloadRequested?.Invoke());
                            break;

                        case LoadState.Failed:
                            TryDeleteFile(downloadPath);
                            HintService.Show(
                                Hints.Error(
                                    Lang.Text(
                                        "Instance.Resource.Update.Failed",
                                        changed.Error?.Message ?? string.Empty)));
                            break;

                        case LoadState.Aborted:
                            TryDeleteFile(downloadPath);
                            break;

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
            ReportError("Instance.Resource.Update.Failed", ex);
        }
    }

    [RelayCommand]
    public void Toggle(DatapackEntry entry)
    {
        Toggle(entry, true);
    }

    public void Toggle(DatapackEntry entry, bool requestReload)
    {
        try
        {
            var target = entry.Disabled
                ? entry.Path[..^".disabled".Length]
                : $"{entry.Path}.disabled";
            if (entry.IsDirectory)
                Directory.Move(entry.Path, target);
            else
                File.Move(entry.Path, target);
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
            .Where(entry => _selected.Contains(entry.RawKey))
            .Where(entry => enabled ? entry.Disabled : !entry.Disabled)
            .ToList();
        foreach (var entry in targets)
            Toggle(entry, false);
        _selected.Clear();
        ReloadRequested?.Invoke();
    }

    [RelayCommand]
    public async Task UpdateSelectedAsync()
    {
        var targets = Entries
            .Where(entry => _selected.Contains(entry.RawKey) && entry.CanUpdate)
            .ToList();
        foreach (var entry in targets)
            await UpdateEntryAsync(entry);
        _selected.Clear();
        BuildList();
    }

    public List<Comp.CompProject> GetFavoriteProjects()
    {
        return
        [
            .. Entries
                .Where(entry => _selected.Contains(entry.RawKey) && entry.File?.Comp is not null)
                .Select(entry => entry.File!.Comp!)
        ];
    }

    public IReadOnlyList<string> GetShareIds()
    {
        return
        [
            .. Entries
                .Where(entry => _selected.Contains(entry.RawKey) && entry.File?.Comp is not null)
                .Select(entry => entry.File!.Comp!.Id)
        ];
    }

    [RelayCommand]
    public async Task DeleteSelectedAsync()
    {
        var targets = Entries.Where(entry => _selected.Contains(entry.RawKey)).ToList();
        if (targets.Count > 0)
            await DeleteAsync(targets);
    }

    public async Task DeleteAsync(IReadOnlyList<DatapackEntry> targets)
    {
        try
        {
            var names = string.Join(", ", targets.Select(entry => entry.DisplayName));
            var canRecycle = targets.All(entry => RecycleBin.IsSupported(entry.Path));
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
                var outcome = canRecycle
                    ? RecycleBin.Move(entry.Path, out _)
                    : RecycleBin.Outcome.Unsupported;
                switch (outcome)
                {
                    case RecycleBin.Outcome.Moved:
                        moved++;
                        break;

                    case RecycleBin.Outcome.Unsupported:
                        if (entry.IsDirectory)
                            Directory.Delete(entry.Path, true);
                        else
                            File.Delete(entry.Path);
                        permanent++;
                        break;

                    case RecycleBin.Outcome.Failed:
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
                                targets[0].DisplayName)
                            : Lang.Text("Instance.Saves.Datapack.Delete.RecycleMultiple", moved)));
            if (permanent > 0)
                HintService.Show(
                    Hints.Success(
                        targets.Count == 1
                            ? Lang.Text(
                                "Instance.Saves.Datapack.Delete.PermanentSingle",
                                targets[0].DisplayName)
                            : Lang.Text(
                                "Instance.Saves.Datapack.Delete.PermanentMultiple",
                                permanent)));
            _selected.Clear();
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

    private static string CsvEscape(string? value)
    {
        value ??= string.Empty;
        return value.Contains(',')
            || value.Contains('"')
            || value.Contains('\r')
            || value.Contains('\n')
                ? $"\"{value.Replace("\"", "\"\"")}\""
                : value;
    }

    private static DateTime SafeWriteTime(DatapackEntry entry)
    {
        try
        {
            return entry.IsDirectory
                ? Directory.GetLastWriteTime(entry.Path)
                : File.GetLastWriteTime(entry.Path);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    private static long SafeSize(DatapackEntry entry)
    {
        try
        {
            return entry.IsDirectory ? 0L : new FileInfo(entry.Path).Length;
        }
        catch
        {
            return 0L;
        }
    }

    private static void DeleteFileToRecycleBin(string path)
    {
        if (!File.Exists(path))
            return;
        if (RecycleBin.IsSupported(path)
            && RecycleBin.Move(path, out _) == RecycleBin.Outcome.Moved)
            return;
        File.Delete(path);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // ignored
        }
    }
}

/// <summary>存档数据包列表中的一条数据。</summary>
public sealed class DatapackEntry
{
    public required string Path { get; init; }

    public required string Name { get; init; }

    public bool IsDirectory { get; init; }

    public LocalComp.LocalCompFile? File { get; init; }

    public bool Disabled => Path.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase);

    public string DisplayName => Disabled ? Name[..^".disabled".Length] : Name;

    public string RawKey => Path;

    public string Version => File?.Version ?? string.Empty;

    public string? Description => File?.Description;

    public IReadOnlyList<string> Tags => File?.Tags ?? [];

    public bool CanUpdate => File is { CanUpdate: true, UpdateFile: not null };

    public bool Unavailable => File?.State == LocalComp.LocalCompFile.LocalFileStatus.Unavailable;
}