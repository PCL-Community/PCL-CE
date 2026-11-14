using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Core.App.Localization;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>
///     资源工程搜索页的视图模型：持有筛选条件、分页、加载状态与当前页条目，
///     页面只负责把这里的集合/状态映射到具体控件。
/// </summary>
public partial class PageCompViewModel : ViewModelBase
{
    public const int PageSize = 40;

    public static readonly IReadOnlyList<string> CommonGameVersions =
    [
        "1.21.11", "1.21.1", "1.20.6", "1.20.1", "1.19.4", "1.19.2",
        "1.18.2", "1.16.5", "1.12.2", "1.10.2", "1.8.9", "1.7.10"
    ];

    private static readonly Comp.CompLoaderType[] ModLoaderTypes =
    [
        Comp.CompLoaderType.Any,
        Comp.CompLoaderType.Forge,
        Comp.CompLoaderType.NeoForge,
        Comp.CompLoaderType.Fabric,
        Comp.CompLoaderType.Quilt
    ];

    private static readonly string[] ShaderLoaderTags =
    [
        "",
        "/vanilla",
        "/iris",
        "/optifine"
    ];

    private readonly List<CompCategoryFilterTag> _categoryTags = [];
    private bool _autoRetried;

    private int _page;

    private Comp.CompProjectStorage _storage = new();

    [ObservableProperty] public partial bool CanGoFirst { get; set; }

    [ObservableProperty] public partial bool CanGoLeft { get; set; }

    [ObservableProperty] public partial bool CanGoRight { get; set; } = true;

    [ObservableProperty] public partial int CurrentPage { get; set; }

    [ObservableProperty] public partial string ErrorMessage { get; set; } = "";

    [ObservableProperty] public partial double FirstOpacity { get; set; } = 0.2d;

    [ObservableProperty] public partial bool IsContentVisible { get; set; }

    [ObservableProperty] public partial bool IsErrorVisible { get; set; }

    [ObservableProperty] public partial bool IsFilterVisible { get; set; }

    [ObservableProperty] public partial bool IsInstallModPackVisible { get; set; }

    [ObservableProperty] public partial bool IsLoaderComboVisible { get; set; } = true;

    [ObservableProperty] public partial bool IsLoaderLabelVisible { get; set; } = true;

    [ObservableProperty] public partial bool IsLoadingVisible { get; set; } = true;

    [ObservableProperty] public partial bool IsPagerVisible { get; set; } = true;

    [ObservableProperty] public partial bool IsShaderLoaderComboVisible { get; set; }

    [ObservableProperty] public partial double LeftOpacity { get; set; } = 0.2d;

    [ObservableProperty] public partial string LoadText { get; set; } = "";

    [ObservableProperty]
    public partial string LoadingText { get; set; } =
        Lang.Text("Application.Control.Loading.Default");

    [ObservableProperty] public partial string PageText { get; set; } = "1";

    [ObservableProperty] public partial Comp.CompType PageType { get; set; } = Comp.CompType.Mod;

    [ObservableProperty]
    public partial ObservableCollection<MyCompItemViewModel> Projects { get; set; } = [];

    [ObservableProperty] public partial double RightOpacity { get; set; } = 1d;

    [ObservableProperty] public partial string SearchHintText { get; set; } = "";

    [ObservableProperty] public partial int SearchLoaderIndex { get; set; }

    [ObservableProperty] public partial int SearchShaderLoaderIndex { get; set; }

    [ObservableProperty] public partial int SearchSortIndex { get; set; }

    [ObservableProperty] public partial int SearchSourceIndex { get; set; }

    [ObservableProperty] public partial int SearchTagIndex { get; set; }

    [ObservableProperty] public partial string SearchText { get; set; } = "";

    [ObservableProperty]
    public partial string SearchVersionText { get; set; } =
        Lang.Text("Download.Comp.Filter.Version.AllInputAvailable");

    [ObservableProperty] public partial bool SupportCurseForge { get; set; } = true;

    [ObservableProperty] public partial bool SupportModrinth { get; set; } = true;

    [ObservableProperty] public partial string? TargetGameVersion { get; set; }

    [ObservableProperty]
    public partial Comp.CompLoaderType TargetModLoader { get; set; } = Comp.CompLoaderType.Any;

    /// <summary>分类下拉项，已包含“全部”和缩进/分组禁用信息。</summary>
    public ObservableCollection<CompCategoryFilterOption> CategoryOptions { get; } = [];

    public Loading.LoaderTask<Comp.CompProjectRequest, int>? Loader { get; private set; }

    public string? SelectedGameVersion => GetGameVersionFilter();

    public Comp.CompLoaderType SelectedModLoader => GetSelectedModLoader();

    public event Action? StateChanged;

    public event Action? ProjectsChanged;

    public event Action? CategoryOptionsChanged;

    /// <summary>请求页面把列表滚动回顶部。</summary>
    public event Action? ScrollToTopRequested;

    partial void OnPageTypeChanged(Comp.CompType value)
    {
        ApplyPageTypeUi();
    }

    public void Initialize()
    {
        if (Loader is not null)
            return;
        Loader = new Loading.LoaderTask<Comp.CompProjectRequest, int>(
            Lang.Text("Download.Comp.List.Source.ResourceFetch", Comp.GetCompTypeName(PageType)),
            Comp.CompProjectsGet)
        {
            reloadTimeout = 60 * 1000
        };
        Loader.OnStateChangedUi += OnLoaderStateChanged;
        ApplyPageTypeUi();
    }

    public void SetCategoryTags(IEnumerable<CompCategoryFilterTag> tags)
    {
        _categoryTags.Clear();
        _categoryTags.AddRange(tags);
        RefreshCategoryOptions();
    }

    public void RefreshCategoryOptions()
    {
        var selectedIndex = SearchTagIndex;
        CategoryOptions.Clear();
        CategoryOptions.Add(
            new CompCategoryFilterOption(
                Lang.Text("Common.Option.All"),
                ""));
        foreach (var tag in _categoryTags)
            CategoryOptions.Add(
                new CompCategoryFilterOption(
                    Lang.Text(tag.TextKey),
                    tag.Tag ?? "",
                    !tag.IsGroupHeader,
                    tag.IsIndented));
        SearchTagIndex = selectedIndex >= 0 && selectedIndex < CategoryOptions.Count
            ? selectedIndex
            : 0;
        CategoryOptionsChanged?.Invoke();
    }

    public void ApplyPageTypeUi()
    {
        SearchHintText = Comp.GetCompSearchName(PageType);
        LoadText = Comp.GetCompLoadingName(PageType);
        LoadingText = LoadText;
        IsInstallModPackVisible = PageType == Comp.CompType.ModPack;
        var isModLike = PageType is Comp.CompType.Mod or Comp.CompType.ModPack;
        var isShader = PageType == Comp.CompType.Shader;
        IsLoaderLabelVisible = isModLike || isShader;
        IsLoaderComboVisible = isModLike;
        IsShaderLoaderComboVisible = isShader;
        Loader?.name = Lang.Text(
            "Download.Comp.List.Source.ResourceFetch",
            Comp.GetCompTypeName(PageType));
    }

    public void ApplySearchContext(string? gameVersion, Comp.CompLoaderType modLoader)
    {
        TargetGameVersion = gameVersion;
        TargetModLoader = modLoader;
        SearchVersionText = string.IsNullOrEmpty(gameVersion)
            ? Lang.Text("Download.Comp.Filter.Version.AllInputAvailable")
            : gameVersion;
        SearchLoaderIndex = FindLoaderIndex(modLoader);
    }

    public void StartNewSearch()
    {
        _page = 0;
        CurrentPage = 0;
        _autoRetried = false;
        _storage = new Comp.CompProjectStorage();
        StartLoad(true);
    }

    public void StartLoad(bool force)
    {
        if (Loader is null)
            return;
        IsFilterVisible = false;
        IsContentVisible = false;
        IsLoadingVisible = true;
        IsErrorVisible = false;
        ErrorMessage = string.Empty;
        StateChanged?.Invoke();
        Loader.Start(BuildRequest(), force);
    }

    public void ChangePage(int newPage)
    {
        if (newPage < 0 || Loader is null)
            return;
        _page = newPage;
        CurrentPage = newPage;
        ScrollToTopRequested?.Invoke();
        StartLoad(true);
    }

    public void RefreshLocalization()
    {
        ApplyPageTypeUi();
        RefreshCategoryOptions();
        if (Loader is { State: LoadState.Finished })
        {
            RenderCurrentPage();
            ProjectsChanged?.Invoke();
            StateChanged?.Invoke();
        }
    }

    /// <summary>刷新当前所有条目的收藏图标。</summary>
    public void RefreshAllFavoriteStatus()
    {
        foreach (var project in Projects)
            project.RefreshFavoriteStatus();
    }

    /// <summary>整体替换当前页条目，供外部在已持有视图模型时复用。</summary>
    public void ReplaceProjects(IReadOnlyList<MyCompItemViewModel> projects)
    {
        Projects = [.. projects];
    }

    /// <summary>按给定页码与存储状态刷新分页控件状态。</summary>
    public void ApplyPagerState(int currentPage, Comp.CompProjectStorage storage)
    {
        _page = currentPage;
        _storage = storage;
        ApplyPagerState();
    }

    /// <summary>按给定存储状态刷新错误提示。</summary>
    public void ApplyErrorState(Comp.CompProjectStorage storage)
    {
        _storage = storage;
        ApplyErrorState();
    }

    /// <summary>返回下载页筛选器应插入的最高版本；没有时返回 null。</summary>
    public string? GetHighestDropVersion()
    {
        try
        {
            var drops = GameDownload.AllDrops;
            if (drops is null || drops.Count == 0 || drops.First() <= 250)
                return null;
            var highestVersion = McInstanceInfo.DropToVersion(drops.First());
            return string.IsNullOrEmpty(highestVersion) ? null : highestVersion;
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "插入最高版本筛选项失败");
            return null;
        }
    }

    [RelayCommand]
    private void FirstPage()
    {
        ChangePage(0);
    }

    [RelayCommand]
    private void PreviousPage()
    {
        ChangePage(_page - 1);
    }

    [RelayCommand]
    private void NextPage()
    {
        ChangePage(_page + 1);
    }

    [RelayCommand]
    private void Retry()
    {
        if (Loader is { State: LoadState.Failed })
            StartLoad(true);
    }

    [RelayCommand]
    public static void InstallModPack()
    {
        Modpack.ModpackInstall();
    }

    private void OnLoaderStateChanged(
        Loading.LoaderBase sender,
        LoadState newState,
        LoadState oldState)
    {
        switch (newState)
        {
            case LoadState.Loading:
                IsFilterVisible = false;
                IsContentVisible = false;
                IsLoadingVisible = true;
                break;

            case LoadState.Finished:
                RenderCurrentPage();
                IsLoadingVisible = false;
                IsFilterVisible = true;
                IsContentVisible = true;
                break;

            case LoadState.Failed:
                IsFilterVisible = false;
                IsContentVisible = false;
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
                    StartLoad(true);
                }

                break;

            case LoadState.Waiting:
            case LoadState.Aborted:
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(newState), newState, null);
        }

        StateChanged?.Invoke();
        if (newState == LoadState.Finished)
            ProjectsChanged?.Invoke();
    }

    private void RenderCurrentPage()
    {
        try
        {
            var results = _storage.results;
            var index = Math.Min(_page * PageSize, results.Count);
            var pageResults = results.Skip(index).Take(PageSize).ToList();
            var showMcVersionDesc = GetGameVersionFilter() is null;
            var showLoaderDesc = GetSelectedModLoader() == Comp.CompLoaderType.Any
                && PageType is Comp.CompType.Mod or Comp.CompType.ModPack;
            var showQuickDownload =
                PageType is not (Comp.CompType.ModPack or Comp.CompType.DataPack);
            var projects = new List<MyCompItemViewModel>(pageResults.Count);
            projects.AddRange(
                pageResults.Select(project => MyCompItemViewModel.Create(
                    project,
                    showMcVersionDesc,
                    showLoaderDesc,
                    showQuickDownload)));
            Projects = new ObservableCollection<MyCompItemViewModel>(projects);
            ApplyPagerState();
            ApplyErrorState();
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "可视化资源列表失败",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Download.Comp.Error.OperationFailed"));
        }
    }

    private void ApplyPagerState()
    {
        PageText = Lang.Number(_page + 1, "N0");
        IsPagerVisible = _storage.results.Count > PageSize
            || _storage.curseForgeOffset < _storage.curseForgeTotal
            || _storage.modrinthOffset < _storage.modrinthTotal;
        CanGoFirst = _page > 0;
        FirstOpacity = _page > 0 ? 1d : 0.2d;
        CanGoLeft = _page > 0;
        LeftOpacity = _page > 0 ? 1d : 0.2d;
        CanGoRight = _storage.results.Count > PageSize * (_page + 1)
            || _storage.curseForgeOffset < _storage.curseForgeTotal
            || _storage.modrinthOffset < _storage.modrinthTotal;
        RightOpacity = CanGoRight ? 1d : 0.2d;
        CurrentPage = _page;
    }

    private void ApplyErrorState()
    {
        IsErrorVisible = !string.IsNullOrEmpty(_storage.errorMessage);
        ErrorMessage = _storage.errorMessage ?? string.Empty;
    }

    private Comp.CompProjectRequest BuildRequest()
    {
        var request = new Comp.CompProjectRequest(PageType, _storage, (_page + 1) * PageSize)
        {
            searchText = NormalizeSearchText(SearchText),
            gameVersion = GetGameVersionFilter(),
            sort = (Comp.CompSortType)Math.Clamp(SearchSortIndex + 1, 1, 6),
            source = GetSelectedSource()
        };

        var selectedTag = SearchTagIndex >= 0 && SearchTagIndex < CategoryOptions.Count
            ? CategoryOptions[SearchTagIndex].Tag
            : "";
        if (PageType == Comp.CompType.Shader)
        {
            var loaderTag = SearchShaderLoaderIndex >= 0
                && SearchShaderLoaderIndex < ShaderLoaderTags.Length
                    ? ShaderLoaderTags[SearchShaderLoaderIndex]
                    : "";
            request.tag = string.IsNullOrEmpty(loaderTag) ? selectedTag : selectedTag + loaderTag;
            request.modLoader = Comp.CompLoaderType.Any;
        }
        else
        {
            request.tag = selectedTag;
            request.modLoader = GetSelectedModLoader();
        }

        return request;
    }

    private string? GetGameVersionFilter()
    {
        var text = SearchVersionText?.Trim();
        if (string.IsNullOrEmpty(text))
            return null;
        if (string.Equals(
                text,
                Lang.Text("Download.Comp.Filter.Version.Any"),
                StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                text,
                Lang.Text("Download.Comp.Filter.Version.AllInputAvailable"),
                StringComparison.OrdinalIgnoreCase))
            return null;
        return text.Contains('.') || text.Contains('w') ? text : null;
    }

    private Comp.CompLoaderType GetSelectedModLoader()
    {
        if (PageType is not (Comp.CompType.Mod or Comp.CompType.ModPack))
            return Comp.CompLoaderType.Any;
        var index = Math.Clamp(SearchLoaderIndex, 0, ModLoaderTypes.Length - 1);
        var loaderType = ModLoaderTypes[index];
        if (loaderType == Comp.CompLoaderType.Forge
            && GetGameVersionFilter() is { } version
            && version.Contains('.')
            && int.TryParse(version.Split('.')[1], out var minor)
            && minor < 14)
            return Comp.CompLoaderType.Any;
        return loaderType;
    }

    private Comp.CompSourceType GetSelectedSource()
    {
        var source = SearchSourceIndex switch
        {
            1 => Comp.CompSourceType.CurseForge,
            2 => Comp.CompSourceType.Modrinth,
            _ => Comp.CompSourceType.Any
        };
        if (!SupportCurseForge) source &= ~Comp.CompSourceType.CurseForge;
        if (!SupportModrinth) source &= ~Comp.CompSourceType.Modrinth;
        return source == 0 ? Comp.CompSourceType.CurseForge : source;
    }

    private static int FindLoaderIndex(Comp.CompLoaderType modLoader)
    {
        for (var i = 0; i < ModLoaderTypes.Length; i++)
            if (ModLoaderTypes[i] == modLoader)
                return i;
        return 0;
    }

    private static string NormalizeSearchText(string text)
    {
        var trimmed = text.Trim();
        var pipe = trimmed.IndexOf('|');
        if (pipe < 0) return trimmed;
        var before = trimmed[..pipe];
        var after = trimmed[(pipe + 1)..];
        if (ContainsChinese(after)) return before.Trim();
        if (ContainsChinese(before)) return after.Trim();
        return trimmed;
    }

    private static bool ContainsChinese(string text)
    {
        return text.Any(c => c is >= '\u4e00' and <= '\u9fff');
    }
}

/// <summary>分类筛选的原始项；文本保持资源键，由视图模型按当前语言求值。</summary>
public sealed record CompCategoryFilterTag(
    string TextKey,
    string? Tag,
    bool IsIndented = false)
{
    public bool IsGroupHeader => Tag is null;
}

/// <summary>分类下拉中的一个可显示项。</summary>
public sealed record CompCategoryFilterOption(
    string Text,
    string Tag,
    bool IsEnabled = true,
    bool IsIndented = false);