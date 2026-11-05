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
using PCL.Network;
using PCL.Network.Loaders;
using PCL.Views.Pages.Instance;
using PCL.Views.Shell;

namespace PCL;

/// <summary>实例资源管理页（模组、资源包、光影、结构文件）的视图模型。</summary>
public partial class PageInstanceCompResourceViewModel : ViewModelBase
{
    public enum SortMethod
    {
        FileName,
        CompName,
        TagNums,
        CreateTime,
        ModFileSize
    }

    private readonly HashSet<string> _selected = new(StringComparer.OrdinalIgnoreCase);

    [ObservableProperty] public partial Comp.CompType CompType { get; set; } = Comp.CompType.Mod;

    [ObservableProperty] public partial string CurrentFolderPath { get; set; } = string.Empty;

    [ObservableProperty] public partial string RootFolderPath { get; set; } = string.Empty;

    [ObservableProperty] public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty] public partial int FilterIndex { get; set; }

    [ObservableProperty] public partial int SortIndex { get; set; } = 1;

    [ObservableProperty] public partial bool IsLoading { get; set; }

    [ObservableProperty] public partial bool HasError { get; set; }

    [ObservableProperty] public partial string FilterAllText { get; set; } = string.Empty;

    [ObservableProperty] public partial string FilterEnabledText { get; set; } = string.Empty;

    [ObservableProperty] public partial string FilterDisabledText { get; set; } = string.Empty;

    [ObservableProperty] public partial string FilterUpdatableText { get; set; } = string.Empty;

    [ObservableProperty] public partial string FilterErrorText { get; set; } = string.Empty;

    [ObservableProperty] public partial string FilterDuplicateText { get; set; } = string.Empty;

    [ObservableProperty] public partial bool ShowFilterEnabled { get; set; }

    [ObservableProperty] public partial bool ShowFilterDisabled { get; set; }

    [ObservableProperty] public partial bool ShowFilterUpdatable { get; set; }

    [ObservableProperty] public partial bool ShowFilterError { get; set; }

    [ObservableProperty] public partial bool ShowFilterDuplicate { get; set; }

    [ObservableProperty] public partial string SortText { get; set; } = string.Empty;

    [ObservableProperty] public partial string SelectedText { get; set; } = string.Empty;

    [ObservableProperty] public partial bool IsSelectionVisible { get; set; }

    [ObservableProperty] public partial bool CanEnableSelected { get; set; }

    [ObservableProperty] public partial bool CanDisableSelected { get; set; }

    [ObservableProperty] public partial bool CanUpdateSelected { get; set; }

    [ObservableProperty] public partial bool CanFavoriteAndShare { get; set; }

    [ObservableProperty] public partial bool ShowManageBack { get; set; }

    [ObservableProperty] public partial bool ShowManageDownload { get; set; } = true;

    [ObservableProperty] public partial bool ShowJarInJar { get; set; }

    [ObservableProperty] public partial bool IsEmpty { get; set; }

    [ObservableProperty] public partial bool IsSchematicEmpty { get; set; }

    [ObservableProperty] public partial string EmptyTitle { get; set; } = string.Empty;

    [ObservableProperty] public partial string EmptyDescription { get; set; } = string.Empty;

    [ObservableProperty] public partial bool ShowHintBack { get; set; }

    [ObservableProperty] public partial bool ShowHintDownload { get; set; } = true;

    public List<LocalComp.LocalCompFile> Entries { get; } = [];

    public List<LocalComp.LocalCompFile> VisibleEntries { get; } = [];

    public IReadOnlyCollection<string> SelectedPaths => _selected;

    public string CompNameKey =>
        CompType switch
        {
            Comp.CompType.ResourcePack => "Instance.Left.ResourcePacks",
            Comp.CompType.Shader => "Instance.Left.Shaders",
            Comp.CompType.Schematic => "Instance.Left.Schematics",
            _ => "Instance.Left.Mod"
        };

    public bool IsFolderView => !string.IsNullOrEmpty(CurrentFolderPath);

    public bool HasEntriesInCurrentFolder => GetFolderEntries().Any();

    public event Action? ReloadRequested;

    public void Configure(Comp.CompType compType, string rootFolder)
    {
        CompType = compType;
        RootFolderPath = rootFolder;
        ShowManageDownload = compType != Comp.CompType.Schematic
            && (HiddenFeatureState.ForceShow
                || !Config.Preference.Hide.PageDownload);
        ShowHintDownload = compType != Comp.CompType.Schematic;
        IsSchematicEmpty = false;
        UpdateFilterState();
        UpdateSortText();
    }

    public void SetCurrentFolder(string path)
    {
        CurrentFolderPath = path;
        ShowManageBack = !string.IsNullOrEmpty(path);
    }

    public void ApplyEmptyState()
    {
        IsEmpty = true;
        IsSchematicEmpty = false;
        var inSubFolder = !string.IsNullOrEmpty(CurrentFolderPath);
        EmptyTitle = Lang.Text(
            inSubFolder ? "Instance.Resource.EmptyFolder.Title" : "Instance.Resource.Empty.Title");
        EmptyDescription = Lang.Text(
            inSubFolder
                ? "Instance.Resource.EmptyFolder.Description"
                : "Instance.Resource.Empty.DescriptionWithDownload");
        ShowHintBack = inSubFolder;
        ShowManageBack = inSubFolder;
        ShowHintDownload = CompType != Comp.CompType.Schematic;
    }

    public void ApplySchematicEmptyState()
    {
        IsEmpty = false;
        IsSchematicEmpty = true;
    }

    partial void OnSearchTextChanged(string value)
    {
        RebuildVisibleEntries();
    }

    partial void OnFilterIndexChanged(int value)
    {
        RebuildVisibleEntries();
    }

    partial void OnSortIndexChanged(int value)
    {
        UpdateSortText();
        RebuildVisibleEntries();
    }

    public void ApplyFilter(int index)
    {
        FilterIndex = index;
    }

    public void ApplySort(int index)
    {
        SortIndex = index;
    }

    public void SetSearch(string text)
    {
        SearchText = text;
    }

    public void SetEntries(IEnumerable<LocalComp.LocalCompFile> entries)
    {
        Entries.Clear();
        Entries.AddRange(entries);
        RebuildVisibleEntries();
    }

    public void RebuildVisibleEntries()
    {
        var available = GetFolderEntries()
            .Select(entry => entry.RawPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _selected.RemoveWhere(path => !available.Contains(path));

        var keyword = SearchText.Trim().ToLowerInvariant();
        var filtered = GetFolderEntries()
            .Where(MatchFilter)
            .Where(entry =>
                keyword.Length == 0
                || (entry.Name ?? string.Empty).Contains(keyword, StringComparison.InvariantCulture)
                || (entry.Description ?? string.Empty).Contains(
                    keyword,
                    StringComparison.InvariantCulture)
                || (entry.FileName ?? string.Empty).Contains(
                    keyword,
                    StringComparison.InvariantCulture))
            .ToList();
        SortEntries(filtered);

        VisibleEntries.Clear();
        VisibleEntries.AddRange(filtered);
        IsEmpty = VisibleEntries.Count == 0;
        UpdateFilterState();
        UpdateSortText();
        UpdateSelectionState();
    }

    private IEnumerable<LocalComp.LocalCompFile> GetFolderEntries()
    {
        if (string.IsNullOrEmpty(RootFolderPath))
            return [];
        if (string.IsNullOrEmpty(CurrentFolderPath))
        {
            var root = Path.GetFullPath(RootFolderPath.TrimEnd(Path.DirectorySeparatorChar));
            return Entries.Where(entry =>
                string.Equals(
                    Directory.GetParent(GetEntryPath(entry))?.FullName,
                    root,
                    StringComparison.OrdinalIgnoreCase));
        }

        return Entries.Where(entry =>
            string.Equals(
                Directory.GetParent(GetEntryPath(entry))?.FullName,
                CurrentFolderPath,
                StringComparison.OrdinalIgnoreCase));
    }

    private static string GetEntryPath(LocalComp.LocalCompFile entry)
    {
        return entry.IsFolder ? entry.ActualPath : entry.path;
    }

    private bool MatchFilter(LocalComp.LocalCompFile entry)
    {
        return FilterIndex switch
        {
            1 => entry.State == LocalComp.LocalCompFile.LocalFileStatus.Fine,
            2 => entry.State == LocalComp.LocalCompFile.LocalFileStatus.Disabled,
            3 => entry.CanUpdate,
            4 => entry.State == LocalComp.LocalCompFile.LocalFileStatus.Unavailable,
            5 => IsDuplicate(entry),
            _ => true
        };
    }

    private bool IsDuplicate(LocalComp.LocalCompFile entry)
    {
        var name = entry.Name;
        if (string.IsNullOrEmpty(name))
            return false;
        return Entries.Any(other =>
            !ReferenceEquals(other, entry)
            && string.Equals(other.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private void SortEntries(List<LocalComp.LocalCompFile> list)
    {
        list.Sort((a, b) =>
        {
            if (a.IsFolder != b.IsFolder)
                return a.IsFolder ? -1 : 1;
            var nameCompare = string.Compare(
                a.Name ?? a.FileName,
                b.Name ?? b.FileName,
                StringComparison.OrdinalIgnoreCase);
            switch ((SortMethod)SortIndex)
            {
                case SortMethod.FileName:
                    return string.Compare(
                        a.FileName,
                        b.FileName,
                        StringComparison.OrdinalIgnoreCase);
                case SortMethod.TagNums:
                    if (a.IsFolder && b.IsFolder)
                        return nameCompare;
                    return b.Tags.Count.CompareTo(a.Tags.Count);
                case SortMethod.CreateTime:
                {
                    var aTime = SafeCreationTime(a);
                    var bTime = SafeCreationTime(b);
                    return aTime == bTime ? nameCompare : bTime.CompareTo(aTime);
                }
                case SortMethod.ModFileSize:
                {
                    if (a.IsFolder && b.IsFolder)
                        return nameCompare;
                    var aSize = SafeFileSize(a);
                    var bSize = SafeFileSize(b);
                    return aSize == bSize ? nameCompare : bSize.CompareTo(aSize);
                }
                case SortMethod.CompName:
                default:
                    return nameCompare;
            }
        });
    }

    private void UpdateFilterState()
    {
        var all = Entries.Count;
        var enabled = Entries.Count(entry =>
            entry.State == LocalComp.LocalCompFile.LocalFileStatus.Fine);
        var disabled = Entries.Count(entry =>
            entry.State == LocalComp.LocalCompFile.LocalFileStatus.Disabled);
        var updatable = Entries.Count(entry => entry.CanUpdate);
        var error = Entries.Count(entry =>
            entry.State == LocalComp.LocalCompFile.LocalFileStatus.Unavailable);
        var duplicate = Entries.Count(IsDuplicate);

        FilterAllText = FilterIndex == 0 && string.IsNullOrWhiteSpace(SearchText)
            ? Lang.Text("Instance.Resource.Filter.AllWithCount", all)
            : Lang.Text("Instance.Resource.Filter.SearchResult");
        FilterEnabledText = Lang.Text("Instance.Resource.Filter.EnabledWithCount", enabled);
        FilterDisabledText = Lang.Text("Instance.Resource.Filter.DisabledWithCount", disabled);
        FilterUpdatableText = Lang.Text("Instance.Resource.Filter.UpdatableWithCount", updatable);
        FilterErrorText = Lang.Text("Instance.Resource.Filter.ErrorWithCount", error);
        FilterDuplicateText = Lang.Text("Instance.Resource.Filter.DuplicateWithCount", duplicate);
        ShowFilterEnabled = enabled > 0 && enabled < all;
        ShowFilterDisabled = disabled > 0;
        ShowFilterUpdatable = updatable > 0;
        ShowFilterError = error > 0;
        ShowFilterDuplicate = duplicate > 0;
    }

    private void UpdateSortText()
    {
        var key = (SortMethod)SortIndex switch
        {
            SortMethod.CompName => "Instance.Resource.Sort.ResourceName",
            SortMethod.TagNums => "Instance.Resource.Sort.TagCount",
            SortMethod.CreateTime => "Instance.Resource.Sort.AddTime",
            SortMethod.ModFileSize => "Instance.Resource.Sort.FileSize",
            _ => "Instance.Resource.Sort.FileName"
        };
        SortText = Lang.Text("Instance.Resource.Sort.Text", Lang.Text(key));
    }

    public static IReadOnlyList<string> GetSortNames()
    {
        return
        [
            Lang.Text("Instance.Resource.Sort.FileName"),
            Lang.Text("Instance.Resource.Sort.ResourceName"),
            Lang.Text("Instance.Resource.Sort.TagCount"),
            Lang.Text("Instance.Resource.Sort.AddTime"),
            Lang.Text("Instance.Resource.Sort.FileSize")
        ];
    }

    public void SetSelected(LocalComp.LocalCompFile entry, bool selected)
    {
        if (selected)
            _selected.Add(entry.RawPath);
        else
            _selected.Remove(entry.RawPath);
        UpdateSelectionState();
    }

    public bool IsSelected(LocalComp.LocalCompFile entry)
    {
        return _selected.Contains(entry.RawPath);
    }

    public void ClearSelection()
    {
        _selected.Clear();
        UpdateSelectionState();
    }

    [RelayCommand]
    public void ToggleSelectAll()
    {
        SetAllSelected(null);
    }

    public void SetAllSelected(bool? select)
    {
        var selectable = VisibleEntries.Where(entry => !entry.IsFolder).ToList();
        if (selectable.Count == 0)
            return;
        var allSelected = selectable.All(entry => _selected.Contains(entry.RawPath));
        var target = select ?? !allSelected;
        foreach (var entry in selectable)
            if (target)
                _selected.Add(entry.RawPath);
            else
                _selected.Remove(entry.RawPath);
        RebuildVisibleEntries();
    }

    public void SetSelectedEnabled(bool enabled)
    {
        var targets = Entries
            .Where(entry => _selected.Contains(entry.RawPath))
            .Where(entry => enabled
                ? entry.State == LocalComp.LocalCompFile.LocalFileStatus.Disabled
                : entry.State == LocalComp.LocalCompFile.LocalFileStatus.Fine)
            .ToList();
        if (targets.Count == 0)
            return;
        foreach (var entry in targets)
            ToggleEntry(entry, false);
        _selected.Clear();
        ReloadRequested?.Invoke();
    }

    [RelayCommand]
    public void ToggleEntry(LocalComp.LocalCompFile entry)
    {
        ToggleEntry(entry, true);
    }

    public void ToggleEntry(LocalComp.LocalCompFile entry, bool requestReload)
    {
        try
        {
            var path = entry.path;
            switch (entry.State)
            {
                case LocalComp.LocalCompFile.LocalFileStatus.Fine:
                    MoveWithRegistry(path, $"{path}.disabled");
                    break;
                case LocalComp.LocalCompFile.LocalFileStatus.Disabled:
                {
                    var enabledPath = path.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)
                        ? path[..^".disabled".Length]
                        : path[..^".old".Length];
                    MoveWithRegistry(path, enabledPath);
                    break;
                }
                case LocalComp.LocalCompFile.LocalFileStatus.Unavailable:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Instance.Resource.Error.OperationFailed"),
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Instance.Resource.Error.OperationFailed"));
        }

        if (!requestReload)
            return;
        if (entry.State is LocalComp.LocalCompFile.LocalFileStatus.Fine
            or LocalComp.LocalCompFile.LocalFileStatus.Disabled)
            _selected.Remove(entry.RawPath);
        ReloadRequested?.Invoke();
    }

    private void MoveWithRegistry(string source, string target)
    {
        var folder = Path.GetDirectoryName(source) ?? RootFolderPath;
        CompLocalUpdateRegistry.Begin(folder);
        try
        {
            File.Move(source, target);
        }
        finally
        {
            CompLocalUpdateRegistry.End(folder);
        }
    }

    [RelayCommand]
    public async Task DeleteSelectedAsync()
    {
        var targets = Entries.Where(entry => _selected.Contains(entry.RawPath)).ToList();
        if (targets.Count > 0)
            await DeleteEntriesAsync(targets);
    }

    public async Task DeleteEntriesAsync(IReadOnlyList<LocalComp.LocalCompFile> targets)
    {
        try
        {
            var dialog = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Common.Dialog.Warning"),
                    Content = Lang.Text(
                        "Event.Safety.ConfirmMessage",
                        $"{Lang.Text("Common.Action.Delete")} {targets.Count}"),
                    Buttons = DialogButtons.OkCancel,
                    IsWarn = true
                });
            if (dialog.Button != 1)
                return;

            var folder = string.Empty;
            foreach (var entry in targets)
                try
                {
                    folder = Path.GetDirectoryName(GetEntryPath(entry)) ?? RootFolderPath;
                    DeleteEntryCore(entry);
                }
                catch (Exception ex)
                {
                    LauncherCommon.Log(ex, $"删除资源失败：{entry.path}", LauncherLogLevel.Feedback);
                }

            if (!string.IsNullOrEmpty(folder))
                CompLocalUpdateRegistry.End(folder);
            _selected.Clear();
            ReloadRequested?.Invoke();
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Instance.Resource.Error.OperationFailed"),
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Instance.Resource.Error.OperationFailed"));
        }
    }

    public async Task DeleteEntryAsync(LocalComp.LocalCompFile entry)
    {
        try
        {
            var dialog = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Common.Dialog.Warning"),
                    Content = Lang.Text(
                        "Event.Safety.ConfirmMessage",
                        $"{Lang.Text("Common.Action.Delete")} {entry.Name}"),
                    Buttons = DialogButtons.OkCancel,
                    IsWarn = true
                });
            if (dialog.Button != 1)
                return;

            LauncherCommon.RunInNewThread(
                () =>
                {
                    try
                    {
                        DeleteEntryCore(entry);
                    }
                    catch (Exception ex)
                    {
                        LauncherCommon.Log(
                            ex,
                            Lang.Text("Instance.Resource.Error.OperationFailed"),
                            LauncherLogLevel.Feedback,
                            userSummary: Lang.Text("Instance.Resource.Error.OperationFailed"));
                    }
                    finally
                    {
                        LauncherCommon.RunInUi(() => ReloadRequested?.Invoke());
                    }
                },
                $"Comp Delete {LauncherCommon.GetUuid()}",
                ThreadPriority.BelowNormal);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Instance.Resource.Error.OperationFailed"),
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Instance.Resource.Error.OperationFailed"));
        }
    }

    private static void DeleteEntryCore(LocalComp.LocalCompFile entry)
    {
        var folder = Path.GetDirectoryName(entry.IsFolder ? entry.ActualPath : entry.path);
        if (!string.IsNullOrEmpty(folder))
            CompLocalUpdateRegistry.Begin(folder);
        try
        {
            if (entry.IsFolder || Directory.Exists(entry.ActualPath))
            {
                LauncherCommon.DeleteDirectory(entry.ActualPath);
                return;
            }

            var recycle = RecycleBin.IsSupported(entry.path);
            var moved = false;
            if (recycle)
            {
                var outcome = RecycleBin.Move(entry.path, out _);
                moved = outcome == RecycleBin.Outcome.Moved;
            }

            if (!moved)
                File.Delete(entry.path);
        }
        finally
        {
            if (!string.IsNullOrEmpty(folder))
                CompLocalUpdateRegistry.End(folder);
        }
    }

    [RelayCommand]
    public Task UpdateSelectedAsync()
    {
        var targets = Entries
            .Where(entry => _selected.Contains(entry.RawPath)
                && entry is { CanUpdate: true, UpdateFile: not null })
            .ToList();
        return UpdateEntriesAsync(targets);
    }

    public async Task UpdateEntriesAsync(IReadOnlyList<LocalComp.LocalCompFile> entries)
    {
        var targets = entries
            .Where(entry => entry is { CanUpdate: true, UpdateFile: not null })
            .ToList();
        if (targets.Count == 0)
            return;

        try
        {
            var warn = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Instance.Resource.Update.Warning.Title"),
                    Content = Lang.Text("Instance.Resource.Update.Warning.Message"),
                    Buttons = DialogButtons.Create(
                        "Instance.Resource.Update.Warning.Confirm",
                        "Common.Action.Cancel"),
                    IsWarn = true
                });
            if (!warn.IsConfirmed)
                return;

            var downloads = new List<DownloadFile>();
            var replacements = new List<(LocalComp.LocalCompFile Entry, string NewPath)>();
            foreach (var entry in targets)
            {
                var updateFile = entry.UpdateFile!;
                if (!updateFile.Available)
                    continue;
                var folder = Path.GetDirectoryName(entry.path) ?? RootFolderPath;
                var targetPath = Path.Combine(folder, updateFile.FileName);
                var checker = string.IsNullOrEmpty(updateFile.Hash)
                    ? new FileChecker(1024L)
                    : new FileChecker(hash: updateFile.Hash);
                downloads.Add(new DownloadFile(updateFile.DownloadUrls, targetPath, checker));
                replacements.Add((entry, targetPath));
            }

            if (downloads.Count == 0)
            {
                HintService.Show(
                    Hints.Error(Lang.Text("Instance.Resource.Update.Failed", string.Empty)));
                return;
            }

            var loader = new Loading.LoaderCombo<string>(
                Lang.Text("Instance.Resource.Update.Task.Title", CompType.ToString()),
                [
                    new LoaderDownload(
                        Lang.Text("Instance.Resource.Update.Task.DownloadFiles"),
                        downloads)
                    {
                        ProgressWeight = 1d
                    }
                ])
            {
                OnStateChanged = changed =>
                {
                    switch (changed.State)
                    {
                        case LoadState.Finished:
                            try
                            {
                                foreach (var (entry, newPath) in replacements)
                                {
                                    var oldPath = entry.path;
                                    if (string.Equals(
                                            oldPath,
                                            newPath,
                                            StringComparison.OrdinalIgnoreCase))
                                        continue;
                                    foreach (var variant in new[]
                                             {
                                                 oldPath,
                                                 $"{oldPath}.disabled",
                                                 $"{oldPath}.old"
                                             })
                                        if (File.Exists(variant)
                                            && !string.Equals(
                                                variant,
                                                newPath,
                                                StringComparison.OrdinalIgnoreCase))
                                            File.Delete(variant);
                                }

                                HintService.Show(
                                    Hints.Success(
                                        Lang.Text(
                                            "Instance.Resource.Update.SuccessMultiple",
                                            replacements.Count)));
                            }
                            catch (Exception ex)
                            {
                                LauncherCommon.Log(ex, "替换资源文件失败", LauncherLogLevel.Feedback);
                                HintService.Show(
                                    Hints.Error(
                                        Lang.Text("Instance.Resource.Update.Failed", ex.Message)));
                            }

                            _selected.Clear();
                            LauncherCommon.RunInUi(() => ReloadRequested?.Invoke());
                            break;

                        case LoadState.Failed:
                            HintService.Show(
                                Hints.Error(
                                    Lang.Text(
                                        "Instance.Resource.Update.Failed",
                                        changed.Error?.Message ?? string.Empty)));
                            break;

                        case LoadState.Aborted:
                            HintService.Show(
                                Hints.Info(Lang.Text("Instance.Resource.Update.Aborted")));
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
            LauncherCommon.Log(
                ex,
                Lang.Text("Instance.Resource.Error.OperationFailed"),
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Instance.Resource.Error.OperationFailed"));
        }
    }

    public static void ShowUpdateLog(LocalComp.LocalCompFile entry)
    {
        try
        {
            if (entry.Comp is not null)
            {
                var isNumeric = long.TryParse(entry.Comp.Id, out _);
                var url = isNumeric
                    ? entry.ChangelogUrls.FirstOrDefault(item => item.Contains(
                        "curseforge.com",
                        StringComparison.OrdinalIgnoreCase))
                    : entry.ChangelogUrls.FirstOrDefault(item => item.Contains(
                        "modrinth.com",
                        StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(url))
                {
                    Basics.OpenPath(url);
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "打开更新日志失败", LauncherLogLevel.Developer);
        }

        HintService.Show(Hints.Info(Lang.Text("Instance.Resource.Item.OpenChangelogFailed")));
    }

    public Comp.CompProject? GetFavoriteProject()
    {
        return Entries
            .Where(entry => _selected.Contains(entry.RawPath))
            .Select(entry => entry.Comp)
            .FirstOrDefault(comp => comp is not null);
    }

    public string GetShareCode()
    {
        var ids = Entries
            .Where(entry => _selected.Contains(entry.RawPath) && entry.Comp is not null)
            .Select(entry => entry.Comp!.Id)
            .ToHashSet();
        return Comp.CompFavorites.GetShareCode(ids);
    }

    public IReadOnlyList<string> GetShareIds()
    {
        return
        [
            .. Entries
                .Where(entry => _selected.Contains(entry.RawPath) && entry.Comp is not null)
                .Select(entry => entry.Comp!.Id)
        ];
    }

    public static IReadOnlyList<string> BuildInfoLines(LocalComp.LocalCompFile entry)
    {
        var lines = new List<string>();
        if (entry.IsFolder)
        {
            lines.Add(Lang.Text("Instance.Resource.Item.Info.Path", entry.ActualPath));
            return lines;
        }

        if (!string.IsNullOrWhiteSpace(entry.Authors))
            lines.Add(Lang.Text("Instance.Resource.Item.Info.Author", entry.Authors));
        lines.Add(
            Lang.Text(
                "Instance.Resource.Item.Info.File",
                entry.FileName,
                InstancePageHelper.FormatSize(SafeFileSize(entry))));
        if (!string.IsNullOrWhiteSpace(entry.Version))
            lines.Add(Lang.Text("Instance.Resource.Item.Info.Version", entry.Version));
        if (!string.IsNullOrWhiteSpace(entry.Description))
            lines.Add(entry.Description);
        if (!string.IsNullOrWhiteSpace(entry.ModId))
            lines.Add(Lang.Text("Instance.Resource.Item.Info.ModId", entry.ModId));
        var dependencies = entry
            .Dependencies
            .Where(pair => !DependencyIds.IsPlatform(pair.Key))
            .ToList();
        if (dependencies.Count > 0)
        {
            lines.Add(Lang.Text("Instance.Resource.Item.Info.Dependency"));
            lines.AddRange(
                dependencies.Select(dependency => $" - {(dependency.Value is null
                    ? dependency.Key
                    : Lang.Text(
                        "Instance.Resource.Item.Info.DependencyVersion",
                        dependency.Key,
                        dependency.Value))}"));
        }

        if (lines.Count == 0)
            lines.Add(entry.Name);
        return lines;
    }

    [RelayCommand]
    public void EnterFolder(LocalComp.LocalCompFile entry)
    {
        if (!entry.IsFolder)
            return;
        SetCurrentFolder(entry.ActualPath);
        ReloadRequested?.Invoke();
    }

    [RelayCommand]
    public void GoBackToParentFolder()
    {
        if (string.IsNullOrEmpty(CurrentFolderPath))
            return;
        var parent = Directory.GetParent(CurrentFolderPath)?.FullName;
        SetCurrentFolder(parent ?? string.Empty);
        ReloadRequested?.Invoke();
    }

    public async Task InstallFilesAsync(IReadOnlyList<string> files)
    {
        if (files.Count == 0)
            return;
        try
        {
            var targetFolder = string.IsNullOrEmpty(CurrentFolderPath)
                ? RootFolderPath
                : CurrentFolderPath;
            Directory.CreateDirectory(targetFolder);
            CompLocalUpdateRegistry.Begin(targetFolder);
            await Task.Run(() =>
            {
                foreach (var file in files)
                {
                    var name = Path.GetFileName(file);
                    var target = Path.Combine(targetFolder, name);
                    File.Copy(file, target, true);
                }
            });
            var successText = files.Count > 1
                ? Lang.Text(
                    "Instance.Resource.Install.SuccessMultiple",
                    files.Count,
                    Lang.Text(CompNameKey))
                : Lang.Text(
                    "Instance.Resource.Install.SuccessSingle",
                    Path.GetFileName(files[0]));
            HintService.Show(Hints.Success(successText));
            LauncherCommon.RunInUi(() => ReloadRequested?.Invoke());
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Instance.Resource.Error.OperationFailed"),
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Instance.Resource.Error.OperationFailed"));
        }
        finally
        {
            CompLocalUpdateRegistry.End(
                string.IsNullOrEmpty(CurrentFolderPath) ? RootFolderPath : CurrentFolderPath);
        }
    }

    public bool AcceptDrop(IReadOnlyList<string> paths, out List<string> accepted)
    {
        accepted =
        [
            .. paths
                .Where(path => CompType switch
                {
                    Comp.CompType.Mod => LocalComp.LocalCompFile.IsModFile(path),
                    Comp.CompType.ResourcePack or Comp.CompType.Shader =>
                        path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase),
                    _ => LocalComp.LocalCompFile.IsCompFile(path, CompType)
                })
        ];
        return accepted.Count > 0;
    }

    [RelayCommand]
    public async Task ExportInfoAsync()
    {
        try
        {
            var mode = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Instance.Resource.Export.Mode.Title"),
                    Content = Lang.Text("Instance.Resource.Export.Mode.Message"),
                    Buttons = DialogButtons.Create(
                        "Instance.Resource.Export.Mode.Txt",
                        "Instance.Resource.Export.Mode.Csv",
                        "Common.Action.Cancel")
                });
            if (!mode.IsConfirmed && mode.Button != 2 && mode.Button != 3)
                return;

            var isCsv = mode.Button == 2;
            var savePath = SystemDialogs.SelectSaveFile(
                Lang.Text("Instance.Resource.Export.SelectSaveLocation"),
                isCsv ? "resource.csv" : "resource.txt",
                Lang.Text("Instance.Resource.Export.FilesFilter"));
            if (string.IsNullOrWhiteSpace(savePath))
                return;

            var builder = new StringBuilder();
            if (isCsv)
                builder.AppendLine(
                    "文件名,资源名称,资源版本,此版本更新时间,Mod ID,对应平台工程 ID,文件大小（字节）,文件路径,内嵌模组");
            foreach (var entry in Entries)
                if (isCsv)
                {
                    var fields = new[]
                    {
                        entry.FileName,
                        entry.Comp?.TranslatedName ?? entry.Name ?? string.Empty,
                        entry.Version ?? string.Empty,
                        entry.CompFile?.ReleaseDate.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty,
                        entry.ModId ?? string.Empty,
                        entry.Comp?.Id ?? string.Empty,
                        SafeFileSize(entry).ToString(),
                        entry.path,
                        string.Join(";", FlattenEmbeddedNames(entry.EmbeddedMods))
                    };
                    builder.AppendLine(string.Join(',', fields.Select(CsvEscape)));
                }
                else
                {
                    builder.AppendLine(entry.FileName);
                    AppendEmbeddedForExport(builder, entry.EmbeddedMods, 1);
                }

            await File.WriteAllTextAsync(savePath, builder.ToString(), Encoding.UTF8);
            InstancePageHelper.OpenFolder(savePath);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Instance.Resource.Error.OperationFailed"),
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Instance.Resource.Error.OperationFailed"));
        }
    }

    private static void AppendEmbeddedForExport(
        StringBuilder builder,
        List<LocalComp.LocalCompFile>? embedded,
        int depth)
    {
        if (embedded is null)
            return;
        foreach (var entry in embedded)
        {
            builder.AppendLine(new string(' ', depth * 2) + entry.FileName);
            AppendEmbeddedForExport(builder, entry.EmbeddedMods, depth + 1);
        }
    }

    private static IEnumerable<string> FlattenEmbeddedNames(List<LocalComp.LocalCompFile>? embedded)
    {
        if (embedded is null)
            yield break;
        foreach (var entry in embedded)
        {
            yield return entry.Name ?? entry.ModId ?? entry.FileName;
            foreach (var child in FlattenEmbeddedNames(entry.EmbeddedMods))
                yield return child;
        }
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

    private static DateTime SafeCreationTime(LocalComp.LocalCompFile entry)
    {
        try
        {
            return File.GetCreationTime(entry.path);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    private static long SafeFileSize(LocalComp.LocalCompFile entry)
    {
        try
        {
            return new FileInfo(entry.path).Length;
        }
        catch
        {
            return 0L;
        }
    }

    private void UpdateSelectionState()
    {
        var count = _selected.Count;
        IsSelectionVisible = count > 0;
        if (count == 0)
        {
            CanUpdateSelected = false;
            CanEnableSelected = false;
            CanDisableSelected = false;
            CanFavoriteAndShare = false;
            return;
        }

        SelectedText = Lang.Text("Instance.Resource.SelectedCount", count);
        var selectedEntries = Entries.Where(entry => _selected.Contains(entry.RawPath)).ToList();
        CanUpdateSelected = selectedEntries.Any(entry => entry.CanUpdate);
        CanEnableSelected = selectedEntries.Any(entry =>
            entry.State == LocalComp.LocalCompFile.LocalFileStatus.Disabled);
        CanDisableSelected = selectedEntries.Any(entry =>
            entry.State == LocalComp.LocalCompFile.LocalFileStatus.Fine);
        CanFavoriteAndShare = selectedEntries.Any(entry => entry.Comp is not null);
    }
}