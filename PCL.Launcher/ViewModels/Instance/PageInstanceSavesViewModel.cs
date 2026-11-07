using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.IO;
using PCL.Core.Platform;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.Core.ViewModel;
using PCL.Navigation;
using PCL.Views.Pages.Download;
using PCL.Views.Pages.Instance;

namespace PCL;

/// <summary>存档列表页的视图模型。</summary>
public partial class PageInstanceSavesViewModel : ViewModelBase
{
    private readonly List<SaveEntry> _allSaves = [];
    private readonly List<string> _saveFolders = [];

    private FileSystemWatcher? _watcher;

    public PageInstanceSavesViewModel()
    {
        SortText = Lang.Text("Instance.Saves.SortBy", GetSortName(SaveSortMethod.FileName));
    }

    [ObservableProperty] public partial string ListTitle { get; set; } = string.Empty;

    [ObservableProperty] public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty] public partial string SortText { get; set; } = string.Empty;

    [ObservableProperty] public partial bool ShowContent { get; set; }

    [ObservableProperty] public partial bool ShowNoWorld { get; set; } = true;

    [ObservableProperty] public partial bool IsLoading { get; set; }

    [ObservableProperty] public partial bool HasError { get; set; }

    [ObservableProperty] public partial bool IsSearching { get; set; }

    [ObservableProperty] public partial bool QuickPlayEnabled { get; set; }

    [ObservableProperty] public partial string? DeletingFolder { get; set; }

    public McInstance? Instance { get; private set; }

    public ObservableCollection<SaveEntry> Saves { get; } = [];

    public IReadOnlyList<SaveEntry> VisibleSaves => Saves;

    public string WorldPath =>
        string.IsNullOrEmpty(Instance?.PathIndie)
            ? string.Empty
            : Path.Combine(Instance.PathIndie, "saves");

    public SaveSortMethod SortMethod { get; private set; } = SaveSortMethod.FileName;

    /// <summary>由页面注入的文件拖放剪贴板写入器，避免 ViewModel 引用具体窗口。</summary>
    public Func<string, Task>? FileClipboardWriter { get; set; }

    public event Action? Updated;

    partial void OnSearchTextChanged(string value)
    {
        IsSearching = !string.IsNullOrWhiteSpace(value);
        ApplySearchAndSort();
    }

    public void SetInstance(McInstance? instance)
    {
        Instance = instance;
        CheckQuickPlay();
        SortText = Lang.Text("Instance.Saves.SortBy", GetSortName(SortMethod));
    }

    public void CheckQuickPlay()
    {
        try
        {
            QuickPlayEnabled = Instance is not null
                && Equals(
                    new GameLaunch.LaunchArgument(Instance).HasArguments(
                        "--quickPlaySingleplayer"),
                    true);
        }
        catch (Exception ex)
        {
            QuickPlayEnabled = false;
            LauncherCommon.Log(
                ex,
                "检查存档快捷启动失败",
                LauncherLogLevel.Hint,
                userSummary: Lang.Text("Instance.Saves.Error.OperationFailed"));
        }
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (string.IsNullOrEmpty(WorldPath))
        {
            _allSaves.Clear();
            Saves.Clear();
            UpdateEmptyState();
            return;
        }

        IsLoading = true;
        Error = null;
        OnPropertyChanged(nameof(HasError));
        try
        {
            InstancePageHelper.EnsureDirectory(WorldPath);
            var loaded = await RunAsync(ct => Task.Run(
                () =>
                {
                    var folders = Directory
                        .EnumerateDirectories(WorldPath)
                        .ToList();
                    return (Folders: folders, Saves: folders.Select(BuildEntry).ToList());
                },
                ct));
            if (loaded.Folders is null)
                return;

            _saveFolders.Clear();
            _saveFolders.AddRange(loaded.Folders);
            _allSaves.Clear();
            _allSaves.AddRange(loaded.Saves);
            ApplySearchAndSort();
        }
        finally
        {
            IsLoading = false;
            HasError = !string.IsNullOrEmpty(Error);
            OnPropertyChanged(nameof(HasError));
            UpdateEmptyState();
        }
    }

    [RelayCommand]
    public Task RetryAsync()
    {
        return LoadAsync();
    }

    public void SetSortMethod(SaveSortMethod method)
    {
        SortMethod = method;
        SortText = Lang.Text("Instance.Saves.SortBy", GetSortName(method));
        ApplySearchAndSort();
    }

    public void RefreshLocalization()
    {
        _allSaves.Clear();
        foreach (var folder in _saveFolders)
            _allSaves.Add(BuildEntry(folder));
        SortText = Lang.Text("Instance.Saves.SortBy", GetSortName(SortMethod));
        ApplySearchAndSort();
    }

    public static string GetSortName(SaveSortMethod method)
    {
        return Lang.Text(
            method switch
            {
                SaveSortMethod.CreateTime => "Instance.Saves.SortCreateTime",
                SaveSortMethod.ModifyTime => "Instance.Saves.SortModifyTime",
                _ => "Instance.Saves.SortFileName"
            });
    }

    public IReadOnlyList<string> GetSortNames()
    {
        return
        [
            GetSortName(SaveSortMethod.FileName),
            GetSortName(SaveSortMethod.CreateTime),
            GetSortName(SaveSortMethod.ModifyTime)
        ];
    }

    private void ApplySearchAndSort()
    {
        var keyword = SearchText.Trim();
        IsSearching = keyword.Length > 0;
        List<SaveEntry> filtered;
        if (IsSearching)
            filtered = SearchService
                .Search(
                    [
                        .. _allSaves.Select(entry =>
                            new SearchService.SearchEntry<SaveEntry>
                            {
                                item = entry,
                                searchSource = [new SearchService.SearchSource(entry.Title)]
                            })
                    ],
                    keyword,
                    SearchService.MaxLocalSearchDepth,
                    0.35d)
                .Select(result => result.item)
                .OfType<SaveEntry>()
                .ToList();
        else
            filtered = [.. _allSaves];

        filtered.Sort(CompareEntries);

        Saves.Clear();
        foreach (var entry in filtered)
            Saves.Add(entry);

        ListTitle = IsSearching
            ? Lang.Text("Instance.Saves.SearchResultTitle", filtered.Count)
            : Lang.Text("Instance.Saves.SaveListTitle", _saveFolders.Count);
        UpdateEmptyState();
        Updated?.Invoke();
    }

    private int CompareEntries(SaveEntry left, SaveEntry right)
    {
        return SortMethod switch
        {
            SaveSortMethod.CreateTime => SafeCreationTime(right.FolderPath)
                .CompareTo(SafeCreationTime(left.FolderPath)),
            SaveSortMethod.ModifyTime => SafeWriteTime(right.FolderPath)
                .CompareTo(SafeWriteTime(left.FolderPath)),
            _ => string.Compare(
                left.Title,
                right.Title,
                StringComparison.OrdinalIgnoreCase)
        };
    }

    private void UpdateEmptyState()
    {
        ShowNoWorld = _saveFolders.Count == 0;
        ShowContent = _saveFolders.Count != 0;
    }

    private SaveEntry BuildEntry(string folder)
    {
        var logo = GetSaveLogo(folder);
        var info = Lang.Text(
            "Instance.Saves.CreationTime",
            Lang.Date(SafeCreationTime(folder), "d"),
            Lang.Date(SafeWriteTime(folder), "d"));
        return new SaveEntry(folder, Path.GetFileName(folder), info, logo);
    }

    private string GetSaveLogo(string folder)
    {
        var iconPath = Path.Combine(folder, "icon.png");
        if (!File.Exists(iconPath))
            return $"{LauncherPaths.ImageRoot}Icons/NoIcon.png";
        try
        {
            var instancePath = Instance?.PathInstance;
            if (string.IsNullOrEmpty(instancePath))
                return $"{LauncherPaths.ImageRoot}Icons/NoIcon.png";
            var cacheFolder = Path.Combine(instancePath, "PCL", "ImgCache");
            Directory.CreateDirectory(cacheFolder);
            var target = Path.Combine(cacheFolder, $"{LauncherCommon.GetFileMd5(iconPath)}.png");
            if (!File.Exists(target))
                File.Copy(iconPath, target, true);
            return target;
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "缓存存档图标失败", LauncherLogLevel.Developer);
            return $"{LauncherPaths.ImageRoot}Icons/NoIcon.png";
        }
    }

    [RelayCommand]
    private void OpenFolder()
    {
        if (string.IsNullOrEmpty(WorldPath))
            return;
        InstancePageHelper.EnsureDirectory(WorldPath);
        Basics.OpenPath(WorldPath);
    }

    [RelayCommand]
    private static void OpenSaveFolder(string? folder)
    {
        if (!string.IsNullOrEmpty(folder))
            Basics.OpenPath(folder);
    }

    [RelayCommand]
    private static void OpenDetails(string? folder)
    {
        if (string.IsNullOrEmpty(folder))
            return;
        PageInstanceSavesLeft.CurrentSave = folder;
        PageRouter.Navigate(
            new PageStackData(
                PageType.VersionSaves,
                PageSubType.VersionSavesInfo,
                folder));
    }

    [RelayCommand]
    private void DownloadNew()
    {
        if (Instance is not null)
            PageRouter.Navigate(
                PageType.Download,
                PageSubType.DownloadWorld,
                new CompSearchContext(Instance.Info.VanillaName, Comp.CompLoaderType.Any));
        else
            PageRouter.Navigate(PageType.Download, PageSubType.DownloadWorld);
    }

    [RelayCommand]
    private void LaunchSave(string? folder)
    {
        if (string.IsNullOrEmpty(folder) || Instance is null)
            return;
        try
        {
            if (!GameLaunch.McLaunchStart(
                    new GameLaunch.McLaunchOptions
                    {
                        WorldName = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar)),
                        instance = Instance
                    }))
                return;
            PageRouter.Navigate(PageType.Launch);
        }
        catch (Exception ex)
        {
            ReportError("Instance.Saves.Error.OperationFailed", ex);
        }
    }

    [RelayCommand]
    public async Task CopySaveAsync(string? folder)
    {
        if (string.IsNullOrEmpty(folder))
            return;
        if (!Directory.Exists(folder))
        {
            HintService.Show(Hints.Info(Lang.Text("Instance.Saves.FolderNotFound")));
            return;
        }

        try
        {
            if (FileClipboardWriter is not null)
                await FileClipboardWriter(folder);
            HintService.Show(Hints.Info(Lang.Text("Instance.Saves.CopiedToClipboard")));
            HintService.Show(Hints.Info(Lang.Text("Instance.Saves.CopyPasteWarning")));
        }
        catch (Exception ex)
        {
            ReportError("Instance.Saves.CopyFailed", ex);
        }
    }

    [RelayCommand]
    private async Task DeleteSave(string? folder)
    {
        await DeleteSaveAsync(folder);
    }

    public async Task<bool> DeleteSaveAsync(string? folder)
    {
        if (string.IsNullOrEmpty(folder))
            return false;

        var entry = Saves.FirstOrDefault(save => save.FolderPath == folder);
        try
        {
            DeletingFolder = folder;
            var saveName = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar));
            var canRecycle = RecycleBin.IsSupported(folder);
            var result = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Instance.Saves.Delete.ConfirmTitle"),
                    Content = Lang.Text(
                        canRecycle
                            ? "Instance.Saves.Delete.ConfirmMessage"
                            : "Instance.Saves.Delete.ConfirmPermanent",
                        saveName),
                    Buttons = DialogButtons.OkCancel,
                    IsWarn = true
                });
            if (!result.IsConfirmed)
                return false;

            if (entry is not null)
            {
                entry.IsDeleting = true;
                entry.Info = Lang.Text("Instance.Saves.Deleting");
            }

            var (outcome, error) = await Task.Run(() =>
            {
                var moved = RecycleBin.Move(folder, out var reason);
                return (moved, reason);
            });
            switch (outcome)
            {
                case RecycleBin.Outcome.Moved:
                    HintService.Show(
                        Hints.Success(Lang.Text("Instance.Saves.DeletedToRecycleBin")));
                    break;

                case RecycleBin.Outcome.Unsupported:
                    await Task.Run(() => Directory.Delete(folder, true));
                    HintService.Show(
                        Hints.Success(Lang.Text("Instance.Saves.DeletedPermanent", saveName)));
                    break;

                case RecycleBin.Outcome.Failed:
                default:
                    throw new IOException(error ?? Lang.Text("Instance.Saves.DeleteFailed"));
            }

            await LoadAsync();
            return true;
        }
        catch (Exception ex)
        {
            ReportError("Instance.Saves.DeleteFailed", ex);
            await LoadAsync();
            return false;
        }
        finally
        {
            DeletingFolder = null;
        }
    }

    public async Task DiscussPasteAsync()
    {
        if (string.IsNullOrEmpty(WorldPath))
            return;
        InstancePageHelper.EnsureDirectory(WorldPath);
        var paths = PlatformServices.Clipboard?.GetFileDropList();
        if (paths is not null)
            foreach (var path in paths)
            {
                if (!Directory.Exists(path)) continue;
                var folderName = new DirectoryInfo(path).Name;
                if (Directory.Exists(Path.Combine(WorldPath, folderName)))
                    HintService.Show(
                        Hints.Info(Lang.Text("Instance.Saves.DuplicateFolder", folderName)));
            }

        var copied = await Files.PasteFromClipboardAsync(WorldPath, false, true, true);
        if (copied == 0)
        {
            HintService.Show(Hints.Warning(Lang.Text("Instance.Saves.Empty.Message")));
            return;
        }

        HintService.Show(Hints.Success(Lang.Text("Instance.Saves.PastedCount", copied)));
        await LoadAsync();
    }

    [RelayCommand]
    public async Task PasteAsync()
    {
        try
        {
            await DiscussPasteAsync();
        }
        catch (Exception ex)
        {
            ReportError("Instance.Saves.CopyFailed", ex);
        }
    }

    /// <summary>启动对存档目录的监听；回调由页面负责调度到 UI 线程并做防抖。</summary>
    public void StartWatcher(Action onChanged)
    {
        StopWatcher();
        if (string.IsNullOrEmpty(WorldPath) || !Directory.Exists(WorldPath))
            return;
        _watcher = new FileSystemWatcher
        {
            Path = WorldPath,
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.LastWrite
        };
        _watcher.Created += (_, _) => onChanged();
        _watcher.Deleted += (_, _) => onChanged();
        _watcher.Renamed += (_, _) => onChanged();
        _watcher.EnableRaisingEvents = true;
    }

    public void StopWatcher()
    {
        if (_watcher is null)
            return;
        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
        _watcher = null;
    }

    private static DateTime SafeCreationTime(string path)
    {
        try
        {
            return Directory.GetCreationTime(path);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    private static DateTime SafeWriteTime(string path)
    {
        try
        {
            return Directory.GetLastWriteTime(path);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    private static void ReportError(string titleKey, Exception ex)
    {
        LauncherCommon.Log(
            ex,
            Lang.Text(titleKey),
            LauncherLogLevel.Hint,
            userSummary: Lang.Text(titleKey));
        HintService.Show(Hints.Error(Lang.Text(titleKey)));
    }
}

/// <summary>存档列表中的一条存档。</summary>
public partial class SaveEntry(
    string folderPath,
    string title,
    string info,
    string logo)
    : ObservableObject
{
    public string FolderPath { get; } = folderPath;

    public string Title { get; } = title;

    [ObservableProperty] public partial string Info { get; set; } = info;

    public string Logo { get; } = logo;

    [ObservableProperty] public partial bool IsDeleting { get; set; }
}

/// <summary>存档列表的排序方式。</summary>
public enum SaveSortMethod
{
    FileName,
    CreateTime,
    ModifyTime
}