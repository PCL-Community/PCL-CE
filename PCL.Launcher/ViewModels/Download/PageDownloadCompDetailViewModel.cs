using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Utils;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>资源详情页的视图模型。</summary>
public partial class PageDownloadCompDetailViewModel : ViewModelBase
{
    private const string OtherGroupKey = "\u0001other";
    private const string SnapshotGroupKey = "\u0001snapshot";
    private const string OldGroupKey = "\u0001old";
    private const string TargetGroupKey = "\u0001target";

    private readonly HashSet<string> _expandedTitles = [];
    private List<Comp.CompFile> _allResults = [];
    private bool _autoRetried;
    private bool _groupedDrop;
    private bool _groupedOld;
    private string? _loadedContextKey;
    private string? _loaderFilter;
    private string? _versionFilter;

    [ObservableProperty] public partial Comp.CompProject? Project { get; set; }

    [ObservableProperty] public partial string? TargetGameVersion { get; set; }

    [ObservableProperty]
    public partial Comp.CompLoaderType TargetModLoader { get; set; } = Comp.CompLoaderType.Any;

    [ObservableProperty] public partial Comp.CompType TargetType { get; set; } = Comp.CompType.Any;

    [ObservableProperty]
    public partial ObservableCollection<CompFileGroupViewModel> FileGroups { get; set; } = [];

    [ObservableProperty] public partial MyCompItemViewModel? IntroItem { get; set; }

    /// <summary>资源详情页简介卡片的来源站点名称。</summary>
    public string IntroWebText =>
        Project is null
            ? string.Empty
            : Project.FromCurseForge
                ? "CurseForge"
                : "Modrinth";

    public bool IntroWikiVisible => Lang.IsChineseMainland && Project is { WikiId: > 0 };

    public bool TranslateVisible => Lang.IsChineseMainland;

    [ObservableProperty] public partial bool IsFavorite { get; set; }

    [ObservableProperty] public partial bool FilterVisible { get; set; }

    [ObservableProperty] public partial bool IsLoadingVisible { get; set; }

    [ObservableProperty] public partial bool IsErrorVisible { get; set; }

    [ObservableProperty] public partial string ErrorMessage { get; set; } = "";

    [LocalizedProperty("Download.Comp.Detail.FileList.VersionList")]
    public partial string LoadText { get; }

    public ObservableCollection<CompFilterChip> InstanceFilterLabelChips { get; } = [];

    public ObservableCollection<CompFilterChip> ModLoaderFilterLabelChips { get; } = [];

    public ObservableCollection<CompFilterOption> InstanceFilterOptions { get; } = [];

    public ObservableCollection<CompFilterOption> ModLoaderFilterOptions { get; } = [];

    public CompFilterOption? SelectedInstanceFilter =>
        InstanceFilterOptions.FirstOrDefault(option => option.IsSelected);

    public CompFilterOption? SelectedModLoaderFilter =>
        ModLoaderFilterOptions.FirstOrDefault(option => option.IsSelected);

    public string? InstanceFilterValue =>
        SelectedInstanceFilter is null || SelectedInstanceFilter.IsAll
            ? null
            : SelectedInstanceFilter.Text;

    public string? ModLoaderFilterValue =>
        SelectedModLoaderFilter is null || SelectedModLoaderFilter.IsAll
            ? null
            : SelectedModLoaderFilter.Text;

    public Loading.LoaderTask<int, List<Comp.CompFile>>? Loader { get; private set; }

    /// <summary>当前加载结果是否仍属于同一导航上下文且未超过加载器缓存时限。</summary>
    public bool HasFreshResults =>
        Project is not null
        && Loader is { State: LoadState.Finished }
        && string.Equals(_loadedContextKey, BuildContextKey(), StringComparison.Ordinal)
        && (Loader.reloadTimeout < 0
            || Loader.lastFinishedTime == 0L
            || TimeUtils.GetTimeTick() - Loader.lastFinishedTime < Loader.reloadTimeout);

    public event Action? StateChanged;

    public event Action? FiltersChanged;

    public event Action? FileGroupsChanged;

    public event Action? IntroChanged;

    public Loading.LoaderTask<int, List<Comp.CompFile>> EnsureLoader()
    {
        if (Loader is not null)
            return Loader;

        Loader = new Loading.LoaderTask<int, List<Comp.CompFile>>(
            Lang.Text("Download.Comp.Detail.FileList.VersionList"),
            task =>
            {
                if (Project is null)
                    return;
                task.output = Comp.CompFilesGet(Project.Id, Project.FromCurseForge);
            })
        {
            reloadTimeout = 60 * 1000
        };
        Loader.OnStateChangedUi += OnFileLoaderStateChanged;
        return Loader;
    }

    /// <summary>应用导航上下文；工程为空时清空当前详情。</summary>
    public bool ApplyContext(
        Comp.CompProject? project,
        Comp.CompType targetType,
        string? targetGameVersion,
        Comp.CompLoaderType targetModLoader)
    {
        var oldContextKey = BuildContextKey();
        var oldProjectId = Project?.Id;
        if (project is null)
        {
            Project = null;
            IntroItem = null;
            OnPropertyChanged(nameof(IntroWebText));
            OnPropertyChanged(nameof(IntroWikiVisible));
            OnPropertyChanged(nameof(TranslateVisible));
            IntroChanged?.Invoke();
            ClearResults();
            return false;
        }

        Project = project;
        TargetType = targetType;
        TargetGameVersion = targetGameVersion;
        TargetModLoader = targetModLoader;
        if (project.Id != oldProjectId)
            _expandedTitles.Clear();
        ApplyProjectState(project);

        var newContextKey = BuildContextKey();
        if (!string.IsNullOrEmpty(oldContextKey)
            && !string.Equals(oldContextKey, newContextKey, StringComparison.Ordinal))
            ClearResults();
        return true;
    }

    private string BuildContextKey()
    {
        return string.Join(
            '\u001f',
            Project?.FromCurseForge == true ? "cf" : "mr",
            Project?.Id ?? string.Empty,
            ((int)TargetType).ToString(),
            TargetGameVersion ?? string.Empty,
            ((int)TargetModLoader).ToString());
    }

    private void ClearResults()
    {
        _allResults = [];
        FileGroups = [];
        FilterVisible = false;
        IsLoadingVisible = true;
        IsErrorVisible = false;
        ErrorMessage = string.Empty;
        InstanceFilterLabelChips.Clear();
        ModLoaderFilterLabelChips.Clear();
        InstanceFilterOptions.Clear();
        ModLoaderFilterOptions.Clear();
        FiltersChanged?.Invoke();
        FileGroupsChanged?.Invoke();
    }

    public void ApplyProjectState(Comp.CompProject project)
    {
        OnPropertyChanged(nameof(IntroWebText));
        OnPropertyChanged(nameof(IntroWikiVisible));
        OnPropertyChanged(nameof(TranslateVisible));
        IntroItem = MyCompItemViewModel.Create(project);
        RefreshFavorite();
        IntroChanged?.Invoke();
    }

    public void SetFilterVisibility(bool visible)
    {
        FilterVisible = visible;
    }

    public void RefreshFavorite()
    {
        IsFavorite = Project is not null && Comp.CompFavorites.IsFavourite(Project.Id);
    }

    /// <summary>开始一次详情文件加载。</summary>
    public void StartLoad()
    {
        if (Project is null || Loader is null)
            return;
        _loadedContextKey = null;
        _allResults = [];
        FileGroups = [];
        FilterVisible = false;
        IsLoadingVisible = true;
        IsErrorVisible = false;
        ErrorMessage = string.Empty;
        _autoRetried = false;
        Loader.Start(0, true);
        StateChanged?.Invoke();
    }

    public void SetGroupExpanded(string key, bool expanded)
    {
        if (expanded)
            _expandedTitles.Add(key);
        else
            _expandedTitles.Remove(key);
    }

    protected override void OnLanguageChanged()
    {
        base.OnLanguageChanged();
        if (Loader is not null)
            Loader.name = Lang.Text("Download.Comp.Detail.FileList.VersionList");
    }

    [RelayCommand]
    private void Retry()
    {
        if (Loader is null)
            return;
        if (Loader.State == LoadState.Failed)
            Loader.Start(0, true);
        else
            StartLoad();
    }

    [RelayCommand]
    private void SelectInstanceFilter(CompFilterOption? option)
    {
        SetInstanceFilter(option);
        RebuildFileGroups();
    }

    [RelayCommand]
    private void SelectModLoaderFilter(CompFilterOption? option)
    {
        SetModLoaderFilter(option);
        RebuildFileGroups();
    }

    public void SetInstanceFilter(CompFilterOption? option)
    {
        foreach (var item in InstanceFilterOptions)
            item.IsSelected = ReferenceEquals(item, option);
        _versionFilter = option is null || option.IsAll ? null : option.Value;
        OnPropertyChanged(nameof(SelectedInstanceFilter));
        OnPropertyChanged(nameof(InstanceFilterValue));
    }

    public void SetModLoaderFilter(CompFilterOption? option)
    {
        foreach (var item in ModLoaderFilterOptions)
            item.IsSelected = ReferenceEquals(item, option);
        _loaderFilter = option is null || option.IsAll ? null : option.Value;
        OnPropertyChanged(nameof(SelectedModLoaderFilter));
        OnPropertyChanged(nameof(ModLoaderFilterValue));
    }

    /// <summary>筛选类型相同的结果（Modrinth 会返回 Mod、服务端插件、数据包混合的列表）。</summary>
    public static List<Comp.CompFile> FilterResults(
        List<Comp.CompFile> results,
        Comp.CompType pageType)
    {
        switch (pageType)
        {
            case Comp.CompType.Any:
                results = [.. results.Where(r => r.Type != Comp.CompType.Plugin)];
                break;

            case Comp.CompType.Shader or Comp.CompType.ResourcePack:
                break;

            default:
                results = [.. results.Where(r => r.Type == pageType)];
                break;
        }

        return results;
    }

    private void OnFileLoaderStateChanged(
        Loading.LoaderBase sender,
        LoadState newState,
        LoadState oldState)
    {
        switch (newState)
        {
            case LoadState.Loading:
                IsLoadingVisible = true;
                IsErrorVisible = false;
                break;

            case LoadState.Finished:
                IsLoadingVisible = false;
                IsErrorVisible = false;
                ProcessLoadedResults();
                break;

            case LoadState.Failed:
                IsLoadingVisible = true;
                IsErrorVisible = true;
                ErrorMessage = sender.Error?.Message ?? string.Empty;
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

    private void ProcessLoadedResults()
    {
        _allResults = FilterResults(
            [.. (Loader?.output ?? []).Where(file => file.Available)],
            TargetType);
        BuildFilters();
        RebuildFileGroups();
        _loadedContextKey = BuildContextKey();
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
        FilterVisible = showFilters;
        InstanceFilterOptions.Clear();
        ModLoaderFilterOptions.Clear();
        InstanceFilterLabelChips.Clear();
        ModLoaderFilterLabelChips.Clear();
        if (!showFilters)
        {
            _versionFilter = null;
            _loaderFilter = null;
            FiltersChanged?.Invoke();
            return;
        }

        var targetGroup = TargetGameVersion is null
            ? null
            : GetGroupedVersionName(TargetGameVersion, _groupedDrop, _groupedOld);
        if (targetGroup is not null && !versionGroups.Contains(targetGroup))
            targetGroup = null;
        _versionFilter = targetGroup;
        _loaderFilter = null;

        if (TargetType == Comp.CompType.Mod)
        {
            InstanceFilterLabelChips.Add(
                new CompFilterChip("Download.Comp.Detail.InstanceFilter"));
            ModLoaderFilterLabelChips.Add(
                new CompFilterChip("Download.Comp.Detail.LoaderFilter"));
        }

        InstanceFilterOptions.Add(new CompFilterOption("Common.Option.All", true));
        foreach (var version in versionGroups)
            InstanceFilterOptions.Add(
                new CompFilterOption(
                    version,
                    displayKey: GetVersionGroupDisplayKey(version)));

        var isMod = TargetType == Comp.CompType.Mod;
        if (isMod)
        {
            var targetLoaderName = TargetModLoader == Comp.CompLoaderType.Any
                ? null
                : TargetModLoader.ToString();
            if (targetLoaderName is not null && !loaderGroups.Contains(targetLoaderName))
                targetLoaderName = null;
            _loaderFilter = targetLoaderName;
            ModLoaderFilterOptions.Add(new CompFilterOption("Common.Option.All", true));
            foreach (var loader in loaderGroups)
                ModLoaderFilterOptions.Add(new CompFilterOption(loader));
        }

        var instanceToCheck = _versionFilter is null
            ? InstanceFilterOptions.FirstOrDefault()
            : InstanceFilterOptions.FirstOrDefault(option =>
                !option.IsAll && option.Value == _versionFilter);
        instanceToCheck ??= InstanceFilterOptions.FirstOrDefault();
        SetInstanceFilter(instanceToCheck);
        if (isMod)
        {
            var modLoaderToCheck = _loaderFilter is null
                ? ModLoaderFilterOptions.FirstOrDefault()
                : ModLoaderFilterOptions.FirstOrDefault(option =>
                    !option.IsAll && option.Value == _loaderFilter);
            modLoaderToCheck ??= ModLoaderFilterOptions.FirstOrDefault();
            SetModLoaderFilter(modLoaderToCheck);
        }
        else
        {
            SetModLoaderFilter(null);
        }

        FiltersChanged?.Invoke();
    }

    private List<string> GetVersionGroups(bool groupedDrop, bool foldOld)
    {
        return
        [
            .. _allResults
                .SelectMany(file => file.GameVersions)
                .Select(version => GetGroupedVersionName(version, groupedDrop, foldOld))
                .Distinct()
                .OrderBy(name => name, VersionGroupComparer.Instance)
        ];
    }

    private static string GetGroupedVersionName(string? name, bool groupedByDrop, bool foldOld)
    {
        if (name is null)
            return OtherGroupKey;
        if (name.Contains('w'))
            return SnapshotGroupKey;
        if (foldOld && McInstanceInfo.VersionToDrop(name, true) < 120)
            return OldGroupKey;
        if (groupedByDrop)
        {
            var drop = McInstanceInfo.VersionToDrop(name, true);
            if (drop >= 0)
                return McInstanceInfo.DropToVersion(drop);
        }

        return name;
    }

    /// <summary>把稳定的分组键转换为当前语言下的显示文本。</summary>
    private static string GetVersionGroupDisplayName(string key)
    {
        if (key.Contains(OtherGroupKey, StringComparison.Ordinal))
            return key.Replace(
                OtherGroupKey,
                Lang.Text("Download.Comp.Detail.VersionGroup.Other"),
                StringComparison.Ordinal);
        if (key.Contains(SnapshotGroupKey, StringComparison.Ordinal))
            return key.Replace(
                SnapshotGroupKey,
                Lang.Text("Download.Comp.Detail.VersionGroup.Snapshot"),
                StringComparison.Ordinal);
        if (key.Contains(OldGroupKey, StringComparison.Ordinal))
            return key.Replace(
                OldGroupKey,
                Lang.Text("Download.Comp.Detail.VersionGroup.Old"),
                StringComparison.Ordinal);
        return key;
    }

    private static string? GetVersionGroupDisplayKey(string key)
    {
        return key switch
        {
            OtherGroupKey => "Download.Comp.Detail.VersionGroup.Other",
            SnapshotGroupKey => "Download.Comp.Detail.VersionGroup.Snapshot",
            OldGroupKey => "Download.Comp.Detail.VersionGroup.Old",
            _ => null
        };
    }

    /// <summary>卡片排序：所选版本卡置顶，其余按版本号倒序，「其他/快照」等特殊项排在后。</summary>
    public sealed class CardSorter(string topmost = "") : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            var sx = x ?? string.Empty;
            var sy = y ?? string.Empty;

            if (sx == sy) return 0;
            if (sx == topmost) return -1;
            if (sy == topmost) return 1;

            var isXSpecial = !sx.Contains('.');
            var isYSpecial = !sy.Contains('.');

            if (isXSpecial && isYSpecial)
                return string.Compare(sx, sy, StringComparison.Ordinal);
            if (isXSpecial) return 1;
            if (isYSpecial) return -1;

            var versionCodeSort = -McVersionComparer.CompareVersion(
                StripLoaderPrefix(sx),
                StripLoaderPrefix(sy));
            return versionCodeSort != 0
                ? versionCodeSort
                : -McVersionComparer.CompareVersion(sx, sy);
        }

        private static string StripLoaderPrefix(string value)
        {
            var space = value.IndexOf(' ');
            return space >= 0 ? value[(space + 1)..] : value;
        }
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

    private void RebuildFileGroups()
    {
        try
        {
            var targetGroupKey = GetTargetGroupKey();
            var groups = new SortedDictionary<string, List<Comp.CompFile>>(
                new CardSorter(targetGroupKey))
            {
                {
                    OtherGroupKey, []
                }
            };
            var duplicates = new Dictionary<string, HashSet<Comp.CompFile>>();

            var hasMultipleLoaders = (Project?.ModLoaders.Count ?? 0) > 1;
            var ignoreQuilt = Config.Download.Comp.IgnoreQuilt;

            foreach (var file in _allResults)
            {
                foreach (var gameVersion in file.GameVersions)
                {
                    if (_versionFilter is not null
                        && GetGroupedVersionName(gameVersion, _groupedDrop, _groupedOld)
                        != _versionFilter)
                        continue;

                    var exactVersion = GetGroupedVersionName(gameVersion, false, false);
                    var prefixes = new List<string>();
                    if (hasMultipleLoaders
                        && file.Type == Comp.CompType.Mod
                        && McInstanceInfo.IsFormatFit(exactVersion))
                    {
                        prefixes.AddRange(
                            from loader in file.ModLoaders
                            where loader != Comp.CompLoaderType.Quilt || !ignoreQuilt
                            where _loaderFilter is null || loader.ToString() == _loaderFilter
                            select $"{loader} ");

                        if (prefixes.Count == 0 && _loaderFilter is not null)
                            continue;
                    }

                    if (prefixes.Count == 0)
                        prefixes.Add(string.Empty);

                    foreach (var prefix in prefixes)
                        AddFileToGroup(
                            groups,
                            duplicates,
                            prefix + exactVersion,
                            file);
                }

                if (string.IsNullOrEmpty(targetGroupKey)
                    || string.IsNullOrEmpty(TargetGameVersion)
                    || !file.GameVersions.Contains(TargetGameVersion)
                    || (TargetModLoader != Comp.CompLoaderType.Any
                        && !file.ModLoaders.Contains(TargetModLoader)))
                    continue;
                if (_versionFilter is not null
                    && file.GameVersions.All(version =>
                        GetGroupedVersionName(version, _groupedDrop, _groupedOld)
                        != _versionFilter))
                    continue;
                AddFileToGroup(groups, duplicates, targetGroupKey, file);
            }

            var cardCount = groups.Count(pair => pair.Value.Count > 0);
            var result = new List<CompFileGroupViewModel>();
            foreach (var (key, files) in groups)
            {
                if (files.Count == 0)
                    continue;

                var isTarget = key == targetGroupKey || _expandedTitles.Contains(key);
                var isSwapped = !(isTarget || cardCount == 1);
                var showRecognitionHint = key == OtherGroupKey;
                result.Add(
                    CompFileGroupViewModel.Create(
                        key,
                        key == targetGroupKey
                            ? GetTargetCardTitle
                            : () => GetVersionGroupDisplayName(key),
                        files,
                        Project?.Type ?? TargetType,
                        isSwapped,
                        showRecognitionHint));
            }

            FileGroups = new ObservableCollection<CompFileGroupViewModel>(result);
            FileGroupsChanged?.Invoke();
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

    private static void AddFileToGroup(
        IDictionary<string, List<Comp.CompFile>> groups,
        IDictionary<string, HashSet<Comp.CompFile>> duplicates,
        string key,
        Comp.CompFile file)
    {
        if (!groups.TryGetValue(key, out var list))
        {
            list = [];
            groups[key] = list;
            duplicates[key] = [];
        }

        if (duplicates[key].Add(file))
            list.Add(file);
    }

    private string GetTargetGroupKey()
    {
        if (string.IsNullOrEmpty(TargetGameVersion)
            && TargetModLoader == Comp.CompLoaderType.Any)
            return string.Empty;
        return TargetGroupKey;
    }

    private string GetTargetCardTitle()
    {
        var loaderText = TargetModLoader != Comp.CompLoaderType.Any
            ? $"{TargetModLoader} "
            : string.Empty;
        return Lang.Text(
            "Download.Comp.Detail.SelectedVersion",
            loaderText,
            TargetGameVersion ?? string.Empty);
    }

    #endregion
}

/// <summary>详情页筛选条标签的数据项；文本按资源键实时求值。</summary>
public sealed record CompFilterChip(string TextKey);

/// <summary>资源详情页筛选器中的单个选项。</summary>
public sealed partial class CompFilterOption(
    string value,
    bool isAll = false,
    string? displayKey = null) : ObservableObject
{
    /// <summary>筛选使用的稳定值，不随语言变化。</summary>
    public string Value { get; } = value;

    public bool IsAll { get; } = isAll;

    public string Text =>
        IsAll
            ? Lang.Text("Common.Option.All")
            : displayKey is not null
                ? Lang.Text(displayKey)
                : Value;

    [ObservableProperty] public partial bool IsSelected { get; set; }
}

/// <summary>文件行点击后的动作。</summary>
public enum CompFileAction
{
    Save,
    InstallToInstance,
    InstallModpack,
    InstallWorld
}

/// <summary>资源详情页文件列表中的一个文件条目。</summary>
public sealed class CompFileItemViewModel(
    Comp.CompFile file,
    Comp.CompType projectType,
    bool badDisplayName)
{
    public Comp.CompFile File { get; } = file;

    public bool BadDisplayName { get; } = badDisplayName;

    public string TitleText { get; } = badDisplayName
        ? file.FileName
        : string.IsNullOrEmpty(file.DisplayName)
            ? file.FileName
            : file.DisplayName;

    public string InfoText => BuildInfo(File);

    public string Logo { get; } = GetStatusLogo(file);

    public CompFileAction Action { get; } = projectType switch
    {
        Comp.CompType.ModPack => CompFileAction.InstallModpack,
        Comp.CompType.World => CompFileAction.InstallWorld,
        _ => CompFileAction.Save
    };

    private static string GetStatusLogo(Comp.CompFile file)
    {
        var icon = file.Status switch
        {
            Comp.CompFileStatus.Release => "R.png",
            Comp.CompFileStatus.Beta => "B.png",
            _ => "A.png"
        };
        return $"{LauncherPaths.ImageRoot}Icons/{icon}";
    }

    private static string BuildInfo(Comp.CompFile file)
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
}

/// <summary>资源详情页中的一个版本分组，对应界面上的一个 MyCard。</summary>
public sealed partial class CompFileGroupViewModel : ObservableObject
{
    private readonly Func<string> _titleFactory;

    private CompFileGroupViewModel(
        string key,
        Func<string> titleFactory,
        List<Comp.CompFile> files,
        ObservableCollection<CompFileItemViewModel> items)
    {
        Key = key;
        _titleFactory = titleFactory;
        Files = files;
        Items = items;
    }

    /// <summary>版本分组的稳定键，用于筛选与展开状态，不随语言变化。</summary>
    public string Key { get; }

    /// <summary>卡片标题，即版本分组的当前语言显示名称。</summary>
    public string Title => _titleFactory();

    /// <summary>分组内的文件，已按发布时间倒序排列（供依赖栏使用）。</summary>
    public List<Comp.CompFile> Files { get; }

    /// <summary>卡片是否处于折叠状态。</summary>
    [ObservableProperty]
    public partial bool IsSwapped { get; set; }

    /// <summary>是否显示「版本识别可能延迟」提示，仅 Other 分组可见。</summary>
    [ObservableProperty]
    public partial bool ShowRecognitionHint { get; set; }

    public ObservableCollection<CompFileItemViewModel> Items { get; }

    public static CompFileGroupViewModel Create(
        string key,
        Func<string> titleFactory,
        List<Comp.CompFile> files,
        Comp.CompType type,
        bool isSwapped,
        bool showRecognitionHint)
    {
        files.Sort((a, b) => b.ReleaseDate.CompareTo(a.ReleaseDate));
        var distinctCount = files
            .Select(file =>
                string.IsNullOrEmpty(file.DisplayName) ? file.FileName : file.DisplayName)
            .Distinct()
            .Count();
        var badDisplayName = distinctCount != files.Count;
        var items = new ObservableCollection<CompFileItemViewModel>(
            files.Select(file => new CompFileItemViewModel(file, type, badDisplayName)));
        return new CompFileGroupViewModel(key, titleFactory, files, items)
        {
            IsSwapped = isSwapped,
            ShowRecognitionHint = showRecognitionHint
        };
    }
}