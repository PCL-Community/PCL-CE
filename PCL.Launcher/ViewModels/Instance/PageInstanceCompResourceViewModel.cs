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

    private readonly Dictionary<string, (DateTime CreationTime, long Length)> _fileInfoCache =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> _selected = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<LocalComp.LocalCompFile> _searchScope = [];

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

    [ObservableProperty] public partial bool ShowList { get; set; }

    [ObservableProperty] public partial bool ShowEmpty { get; set; }

    [ObservableProperty] public partial bool ShowSchematicEmpty { get; set; }

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

    public bool IsSearching => !string.IsNullOrWhiteSpace(SearchText);

    public bool HasEntriesInCurrentFolder =>
        IsSearching ? VisibleEntries.Count > 0 : GetFolderEntries().Any();

    public event Action? ReloadRequested;

    public void Configure(Comp.CompType compType, string rootFolder)
    {
        CompType = compType;
        RootFolderPath = rootFolder;
        ShowManageDownload = compType != Comp.CompType.Schematic
            && (HiddenFeatureState.ForceShow
                || !Config.Preference.Hide.PageDownload);
        ShowHintDownload = compType != Comp.CompType.Schematic;
        UpdateFilterState();
        UpdateSortText();
    }

    public void SetCurrentFolder(string path)
    {
        CurrentFolderPath = path;
        ShowManageBack = !string.IsNullOrEmpty(path);
    }

    /// <summary>开始新一轮加载前收起旧列表与空状态，避免旧内容在加载期间闪现。</summary>
    public void BeginLoading()
    {
        ShowList = false;
        ShowEmpty = false;
        ShowSchematicEmpty = false;
    }

    /// <summary>显示资源列表，隐藏两种空状态。</summary>
    public void ShowListPanel()
    {
        ShowList = true;
        ShowEmpty = false;
        ShowSchematicEmpty = false;
    }

    /// <summary>显示“尚未安装资源”空状态。</summary>
    public void ShowEmptyPanel()
    {
        ShowList = false;
        ShowEmpty = true;
        ShowSchematicEmpty = false;
    }

    /// <summary>显示原理图目录不可用空状态。</summary>
    public void ShowSchematicEmptyPanel()
    {
        ShowList = false;
        ShowEmpty = false;
        ShowSchematicEmpty = true;
    }

    public void ApplyEmptyState()
    {
        ShowEmptyPanel();
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
        ShowSchematicEmptyPanel();
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

    /// <summary>重置搜索、筛选、选择与滚动相关状态；用于重新进入页面或强制刷新。</summary>
    public void ResetListViewState()
    {
        SearchText = string.Empty;
        FilterIndex = 0;
        _selected.Clear();
        _searchScope = [];
        UpdateSortText();
        UpdateSelectionState();
    }

    public void SetEntries(IEnumerable<LocalComp.LocalCompFile> entries)
    {
        Entries.Clear();
        Entries.AddRange(entries);
        _fileInfoCache.Clear();
        RebuildVisibleEntries();
    }

    public void RebuildVisibleEntries()
    {
        // 非搜索状态只展示当前目录；搜索状态展示整个资源根目录的命中项，
        // 这样根目录下不会因为模组放在子文件夹里而“消失”。
        var source = GetListSource();
        var available = Entries
            .Select(entry => entry.RawPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _selected.RemoveWhere(path => !available.Contains(path));

        var scope = FilterBySearch(source);
        _searchScope = scope;
        var filtered = scope.Where(MatchFilter).ToList();
        SortEntries(filtered);
        _selected.RemoveWhere(path =>
            !filtered.Any(entry => string.Equals(
                entry.RawPath,
                path,
                StringComparison.OrdinalIgnoreCase)));

        VisibleEntries.Clear();
        VisibleEntries.AddRange(filtered);
        UpdateFilterState(scope);
        UpdateSortText();
        UpdateSelectionState();
    }

    /// <summary>当前列表的数据源：搜索时为全部条目，否则为当前目录条目。</summary>
    public IReadOnlyList<LocalComp.LocalCompFile> GetListSource()
    {
        return IsSearching ? [.. Entries] : [.. GetFolderEntries()];
    }

    /// <summary>按当前搜索文本过滤资源；独立于筛选和文件状态，便于测试与复用。</summary>
    public IReadOnlyList<LocalComp.LocalCompFile> FilterBySearch(
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
                new(entry.Name ?? entry.FileName ?? string.Empty),
                new(entry.FileName ?? string.Empty)
            };
            if (!string.IsNullOrWhiteSpace(entry.Version))
                sources.Add(new SearchService.SearchSource(entry.Version, 0.2d));
            if (!string.IsNullOrWhiteSpace(entry.Description))
                sources.Add(new SearchService.SearchSource(entry.Description, 0.4d));
            if (entry.Comp is not null)
            {
                if (!string.Equals(entry.Comp.RawName, entry.Name, StringComparison.Ordinal))
                    sources.Add(new SearchService.SearchSource(entry.Comp.RawName ?? string.Empty));
                if (!string.Equals(
                        entry.Comp.TranslatedName,
                        entry.Comp.RawName,
                        StringComparison.Ordinal))
                    sources.Add(
                        new SearchService.SearchSource(entry.Comp.TranslatedName ?? string.Empty));
                if (!string.Equals(
                        entry.Comp.Description,
                        entry.Description,
                        StringComparison.Ordinal))
                    sources.Add(
                        new SearchService.SearchSource(
                            entry.Comp.Description ?? string.Empty,
                            0.4d));
                if (entry.Comp.Tags is { Count: > 0 } tags)
                    sources.Add(
                        new SearchService.SearchSource(string.Join("", tags), 0.2d));
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

    private bool IsDuplicate(
        LocalComp.LocalCompFile entry,
        IReadOnlyList<LocalComp.LocalCompFile>? source = null)
    {
        return entry.Comp is not null
            && (source ?? _searchScope).Any(other =>
                !ReferenceEquals(other, entry)
                && other.Comp is not null
                && string.Equals(other.Comp.Id, entry.Comp.Id, StringComparison.OrdinalIgnoreCase));
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
                {
                    return string.Compare(
                        a.FileName,
                        b.FileName,
                        StringComparison.OrdinalIgnoreCase);
                }

                case SortMethod.TagNums:
                {
                    return a.IsFolder && b.IsFolder
                        ? nameCompare
                        : b.Tags.Count.CompareTo(a.Tags.Count);
                }

                case SortMethod.CreateTime:
                {
                    var aTime = GetFileInfo(a).CreationTime;
                    var bTime = GetFileInfo(b).CreationTime;
                    return aTime == bTime ? nameCompare : bTime.CompareTo(aTime);
                }

                case SortMethod.ModFileSize:
                {
                    if (a.IsFolder && b.IsFolder)
                        return nameCompare;

                    var aSize = GetFileInfo(a).Length;
                    var bSize = GetFileInfo(b).Length;
                    return aSize == bSize ? nameCompare : bSize.CompareTo(aSize);
                }

                case SortMethod.CompName:
                default:
                    return nameCompare;
            }
        });
    }

    private void UpdateFilterState(IReadOnlyList<LocalComp.LocalCompFile>? source = null)
    {
        var scope = source ?? _searchScope;
        if (scope.Count == 0 && source is null)
            scope = [.. GetFolderEntries()];

        var all = scope.Count;
        var enabled = scope.Count(entry =>
            entry.State == LocalComp.LocalCompFile.LocalFileStatus.Fine);
        var disabled = scope.Count(entry =>
            entry.State == LocalComp.LocalCompFile.LocalFileStatus.Disabled);
        var updatable = scope.Count(entry => entry.CanUpdate);
        var error = scope.Count(entry =>
            entry.State == LocalComp.LocalCompFile.LocalFileStatus.Unavailable);
        var duplicate = scope.Count(entry => IsDuplicate(entry, scope));

        FilterAllText = string.IsNullOrWhiteSpace(SearchText)
            ? Lang.Text("Instance.Resource.Filter.AllWithCount", all)
            : Lang.Text("Instance.Resource.Filter.SearchResult");
        FilterEnabledText = Lang.Text("Instance.Resource.Filter.EnabledWithCount", enabled);
        FilterDisabledText = Lang.Text("Instance.Resource.Filter.DisabledWithCount", disabled);
        FilterUpdatableText = Lang.Text("Instance.Resource.Filter.UpdatableWithCount", updatable);
        FilterErrorText = Lang.Text("Instance.Resource.Filter.ErrorWithCount", error);
        FilterDuplicateText = Lang.Text("Instance.Resource.Filter.DuplicateWithCount", duplicate);
        ShowFilterEnabled = FilterIndex == 1 || (enabled > 0 && enabled < all);
        ShowFilterDisabled = FilterIndex == 2 || disabled > 0;
        ShowFilterUpdatable = FilterIndex == 3 || updatable > 0;
        ShowFilterError = FilterIndex == 4 || error > 0;
        ShowFilterDuplicate = FilterIndex == 5 || duplicate > 0;
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
        var selectable = VisibleEntries.ToList();
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
            .Where(entry => !entry.IsFolder && _selected.Contains(entry.RawPath))
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
        var toggled = false;
        try
        {
            var path = entry.path;
            switch (entry.State)
            {
                case LocalComp.LocalCompFile.LocalFileStatus.Fine:
                {
                    var disabledPath = File.Exists($"{path}.old")
                        ? $"{path}.old"
                        : $"{path}.disabled";
                    toggled = TryMoveWithConflict(path, disabledPath);
                    break;
                }

                case LocalComp.LocalCompFile.LocalFileStatus.Disabled:
                {
                    var enabledPath = path.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)
                        ? path[..^".disabled".Length]
                        : path[..^".old".Length];
                    toggled = TryMoveWithConflict(path, enabledPath);
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
        if (toggled
            && entry.State is LocalComp.LocalCompFile.LocalFileStatus.Fine
                or LocalComp.LocalCompFile.LocalFileStatus.Disabled)
            _selected.Remove(entry.RawPath);
        ReloadRequested?.Invoke();
    }

    /// <summary>移动资源文件；若目标已存在则按 WPF 规则处理同名文件冲突。</summary>
    private bool TryMoveWithConflict(string source, string target)
    {
        if (!PathExists(target))
        {
            MoveWithRegistry(source, target);
            return true;
        }

        if (!PathExists(source))
            return true;

        if (File.Exists(source)
            && File.Exists(target)
            && !string.Equals(
                LauncherCommon.GetFileMd5(source),
                LauncherCommon.GetFileMd5(target),
                StringComparison.OrdinalIgnoreCase))
        {
            DialogService.Instance.Show(
                new TextDialogRequest
                {
                    Title = Lang.Text("Instance.Resource.Ed.FileConflict"),
                    Content =
                        Lang.Text("Instance.Resource.Ed.FileConflict.Message", target, source),
                    Buttons = DialogButtons.Ok
                });
            return false;
        }

        if (Directory.Exists(target))
            LauncherCommon.DeleteDirectory(target);
        else
            File.Delete(target);
        MoveWithRegistry(source, target);
        return true;
    }

    private static bool PathExists(string path)
    {
        return File.Exists(path) || Directory.Exists(path);
    }

    private void MoveWithRegistry(string source, string target)
    {
        var folder = Path.GetDirectoryName(source) ?? RootFolderPath;
        CompLocalUpdateRegistry.Begin(folder);
        try
        {
            if (Directory.Exists(source))
                Directory.Move(source, target);
            else
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

    public async Task DeleteEntriesAsync(
        IReadOnlyList<LocalComp.LocalCompFile> targets,
        bool permanentDelete = false)
    {
        if (targets.Count == 0)
            return;
        try
        {
            var canRecycle = !permanentDelete
                && targets.All(entry =>
                    PlatformRuntime.Current.RecycleBin.IsSupported(GetEntryPath(entry)));
            var content = Lang.Text(
                "Event.Safety.ConfirmMessage",
                $"{Lang.Text("Common.Action.Delete")} {targets.Count}");
            if (!canRecycle)
                content += Environment.NewLine
                    + Lang.Text("Select.Instance.Delete.RecycleBinUnavailable");
            var dialog = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Common.Dialog.Warning"),
                    Content = content,
                    Buttons = DialogButtons.OkCancel,
                    IsWarn = true
                });
            if (!dialog.IsConfirmed)
                return;

            var moved = 0;
            var permanent = 0;
            foreach (var entry in targets)
                try
                {
                    if (DeleteEntryCore(entry, permanentDelete))
                        moved++;
                    else
                        permanent++;
                }
                catch (Exception ex)
                {
                    LauncherCommon.Log(ex, $"删除资源失败：{entry.path}", LauncherLogLevel.Feedback);
                }

            _selected.Clear();
            ShowDeleteResult(targets, moved, permanent);
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

    public async Task DeleteEntryAsync(
        LocalComp.LocalCompFile entry,
        bool permanentDelete = false)
    {
        try
        {
            var canRecycle = !permanentDelete
                && PlatformRuntime.Current.RecycleBin.IsSupported(GetEntryPath(entry));
            var content = Lang.Text(
                "Event.Safety.ConfirmMessage",
                $"{Lang.Text("Common.Action.Delete")} {GetEntryDisplayName(entry)}");
            if (!canRecycle)
                content += Environment.NewLine
                    + Lang.Text("Select.Instance.Delete.RecycleBinUnavailable");
            var dialog = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Common.Dialog.Warning"),
                    Content = content,
                    Buttons = DialogButtons.OkCancel,
                    IsWarn = true
                });
            if (!dialog.IsConfirmed)
                return;

            LauncherCommon.RunInNewThread(
                () =>
                {
                    var moved = 0;
                    var permanent = 0;
                    try
                    {
                        if (DeleteEntryCore(entry, permanentDelete))
                            moved++;
                        else
                            permanent++;
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
                        LauncherCommon.RunInUi(() =>
                        {
                            ShowDeleteResult([entry], moved, permanent);
                            ReloadRequested?.Invoke();
                        });
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

    /// <summary>删除单个条目；返回 true 表示已移入回收站，false 表示已永久删除。</summary>
    private static bool DeleteEntryCore(
        LocalComp.LocalCompFile entry,
        bool permanentDelete = false)
    {
        var targetPath = GetEntryPath(entry);
        var folder = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(folder))
            CompLocalUpdateRegistry.Begin(folder);
        try
        {
            var isDirectory = entry.IsFolder || Directory.Exists(targetPath);
            if (!permanentDelete && PlatformRuntime.Current.RecycleBin.IsSupported(targetPath))
            {
                var outcome = PlatformRuntime.Current.RecycleBin.Move(targetPath, out var error);
                switch (outcome)
                {
                    case RecycleBinOutcome.Moved:
                        return true;

                    case RecycleBinOutcome.Failed:
                        throw new IOException(
                            error ?? Lang.Text("Instance.Resource.Delete.Failed"));

                    case RecycleBinOutcome.Unsupported:
                        break;

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            if (isDirectory)
                LauncherCommon.DeleteDirectory(targetPath);
            else
                File.Delete(targetPath);
            return false;
        }
        finally
        {
            if (!string.IsNullOrEmpty(folder))
                CompLocalUpdateRegistry.End(folder);
        }
    }

    private static string GetEntryDisplayName(LocalComp.LocalCompFile entry)
    {
        return string.IsNullOrWhiteSpace(entry.FileName)
            ? entry.Name ?? string.Empty
            : entry.FileName;
    }

    private static void ShowDeleteResult(
        IReadOnlyList<LocalComp.LocalCompFile> targets,
        int moved,
        int permanent)
    {
        if (moved > 0)
            HintService.Show(
                Hints.Success(
                    moved == 1
                        ? Lang.Text(
                            "Instance.Resource.Delete.RecycleSingle",
                            GetEntryDisplayName(targets[0]))
                        : Lang.Text("Instance.Resource.Delete.RecycleMultiple", moved)));
        if (permanent > 0)
            HintService.Show(
                Hints.Success(
                    permanent == 1
                        ? Lang.Text(
                            "Instance.Resource.Delete.PermanentSingle",
                            GetEntryDisplayName(targets[0]))
                        : Lang.Text("Instance.Resource.Delete.PermanentMultiple", permanent)));
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

        var registeredFolders = targets
            .Select(entry => Path.GetDirectoryName(entry.path) ?? RootFolderPath)
            .Where(folder => !string.IsNullOrEmpty(folder))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (var folder in registeredFolders)
            CompLocalUpdateRegistry.Begin(folder);
        var released = false;

        try
        {
            if (CompType == Comp.CompType.Mod
                && (!States.Hint.UpdateMod || targets.Count >= 15))
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
                {
                    Release();
                    return;
                }

                States.Hint.UpdateMod = true;
            }

            var downloads = new List<DownloadFile>();
            var replacements =
                new List<(LocalComp.LocalCompFile Entry, string DownloadPath, string TargetPath)>();
            foreach (var entry in targets)
            {
                var updateFile = entry.UpdateFile!;
                if (!updateFile.Available)
                    continue;
                var fileName = Path.GetFileName(updateFile.FileName);
                if (string.IsNullOrWhiteSpace(fileName)
                    || !string.Equals(fileName, updateFile.FileName, StringComparison.Ordinal))
                    continue;

                var folder = Path.GetFullPath(
                    Path.GetDirectoryName(entry.path) ?? RootFolderPath);
                var suffix = entry.path.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)
                    ? ".disabled"
                    : entry.path.EndsWith(".old", StringComparison.OrdinalIgnoreCase)
                        ? ".old"
                        : string.Empty;
                var targetPath = Path.GetFullPath(Path.Combine(folder, fileName + suffix));
                if (!targetPath.StartsWith(
                        folder + Path.DirectorySeparatorChar,
                        StringComparison.Ordinal))
                    continue;

                var downloadPath = Path.Combine(
                    LauncherPaths.Temp,
                    "DownloadedComp",
                    fileName + suffix);
                var checker = string.IsNullOrEmpty(updateFile.Hash)
                    ? new FileChecker(1024L)
                    : new FileChecker(hash: updateFile.Hash);
                downloads.Add(new DownloadFile(updateFile.DownloadUrls, downloadPath, checker));
                replacements.Add((entry, downloadPath, targetPath));
            }

            if (downloads.Count == 0)
            {
                HintService.Show(
                    Hints.Error(Lang.Text("Instance.Resource.Update.Failed", string.Empty)));
                Release();
                return;
            }

            var instanceName = PageInstanceLeft.ResolveInstance()?.Name ?? string.Empty;
            var loader = new Loading.LoaderCombo<string>(
                Lang.Text("Instance.Resource.Update.Task.Title", instanceName),
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
                                var replaced = 0;
                                foreach (var (entry, downloadPath, newPath) in replacements)
                                {
                                    if (!File.Exists(downloadPath))
                                        continue;

                                    if (File.Exists(newPath))
                                        DeleteFileToRecycleBin(newPath);
                                    var directory = Path.GetDirectoryName(newPath);
                                    if (!string.IsNullOrEmpty(directory))
                                        Directory.CreateDirectory(directory);
                                    File.Move(downloadPath, newPath, true);
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
                                                newPath,
                                                StringComparison.OrdinalIgnoreCase))
                                            DeleteFileToRecycleBin(variant);
                                }

                                HintService.Show(
                                    Hints.Success(
                                        Lang.Text(
                                            "Instance.Resource.Update.SuccessMultiple",
                                            replaced)));
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
                            Release();
                            break;

                        case LoadState.Failed:
                            HintService.Show(
                                Hints.Error(
                                    Lang.Text(
                                        "Instance.Resource.Update.Failed",
                                        changed.Error?.Message ?? string.Empty)));
                            Release();
                            break;

                        case LoadState.Aborted:
                            HintService.Show(
                                Hints.Info(Lang.Text("Instance.Resource.Update.Aborted")));
                            Release();
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
            Release();
            LauncherCommon.Log(
                ex,
                Lang.Text("Instance.Resource.Error.OperationFailed"),
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Instance.Resource.Error.OperationFailed"));
        }

        return;

        void Release()
        {
            if (released)
                return;
            released = true;
            foreach (var folder in registeredFolders)
                CompLocalUpdateRegistry.End(folder);
        }
    }

    private static void DeleteFileToRecycleBin(string path)
    {
        if (!File.Exists(path))
            return;
        if (PlatformRuntime.Current.RecycleBin.IsSupported(path)
            && PlatformRuntime.Current.RecycleBin.Move(path, out _) == RecycleBinOutcome.Moved)
            return;
        File.Delete(path);
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

    public List<Comp.CompProject> GetFavoriteProjects()
    {
        return
        [
            .. Entries
                .Where(entry => _selected.Contains(entry.RawPath) && entry.Comp is not null)
                .Select(entry => entry.Comp!)
        ];
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

        if (files.Any(file => file.Contains("$RECYCLE.BIN", StringComparison.OrdinalIgnoreCase)))
        {
            HintService.Show(
                Hints.Error(Lang.Text("Instance.Resource.Install.RestoreFromRecycleBin")));
            return;
        }

        if (CompType == Comp.CompType.Mod)
        {
            var instance = PageInstanceLeft.ResolveInstance();
            if (instance is not { Modable: true })
            {
                HintService.Show(
                    Hints.Info(Lang.Text("Instance.Resource.Install.SelectModableInstance")));
                return;
            }
        }

        try
        {
            var targetFolder = string.IsNullOrEmpty(CurrentFolderPath)
                ? RootFolderPath
                : CurrentFolderPath;
            Directory.CreateDirectory(targetFolder);
            CompLocalUpdateRegistry.Begin(targetFolder);
            var installed = 0;
            foreach (var file in files)
            {
                if (CompType == Comp.CompType.Mod && !IsInstallableMod(file))
                    continue;

                var name = Path.GetFileName(file);
                if (CompType == Comp.CompType.Mod)
                {
                    name = name
                        .Replace(".disabled", "", StringComparison.OrdinalIgnoreCase)
                        .Replace(".old", "", StringComparison.OrdinalIgnoreCase);
                    if (!name.Contains('.'))
                        name += ".jar";
                }

                var target = Path.Combine(targetFolder, name);
                if (File.Exists(target))
                {
                    var overwrite = await DialogService.Instance.ShowAsync(
                        new TextDialogRequest
                        {
                            Title = Lang.Text("Instance.Resource.Install.OverwriteConfirm.Title"),
                            Content = Lang.Text(
                                "Instance.Resource.Install.OverwriteConfirm.Message",
                                name),
                            Buttons = DialogButtons.Create(
                                "Common.Action.Overwrite",
                                "Common.Action.Cancel")
                        });
                    if (!overwrite.IsConfirmed)
                        continue;
                }

                await Task.Run(() => File.Copy(file, target, true));
                installed++;
            }

            if (installed == 0)
                return;

            var successText = installed > 1
                ? Lang.Text(
                    "Instance.Resource.Install.SuccessMultiple",
                    installed,
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

    private static bool IsInstallableMod(string path)
    {
        return path.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".litemod", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".old", StringComparison.OrdinalIgnoreCase);
    }

    public bool AcceptDrop(IReadOnlyList<string> paths, out List<string> accepted)
    {
        accepted =
        [
            .. paths
                .Where(path => CompType switch
                {
                    Comp.CompType.Mod => IsInstallableMod(path),
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
            if (mode.Button is not (1 or 2))
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

    private (DateTime CreationTime, long Length) GetFileInfo(LocalComp.LocalCompFile entry)
    {
        var path = GetEntryPath(entry);
        if (_fileInfoCache.TryGetValue(path, out var cached))
            return cached;

        DateTime creationTime;
        long length;
        try
        {
            creationTime = entry.IsFolder
                ? Directory.GetCreationTime(path)
                : File.GetCreationTime(path);
            length = entry.IsFolder ? 0L : new FileInfo(path).Length;
        }
        catch
        {
            creationTime = DateTime.MinValue;
            length = 0L;
        }

        var info = (creationTime, length);
        _fileInfoCache[path] = info;
        return info;
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
        CanFavoriteAndShare = selectedEntries.Count == count
            && selectedEntries.All(entry => entry.Comp is not null);
    }
}