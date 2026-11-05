using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using PCL.Core.App.Localization;
using PCL.Navigation;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Download;

/// <summary>
///     资源工程搜索页：筛选 + 搜索结果列表 + 分页，点击条目进入资源详情页。
///     供下载页的各资源子页（Mod/整合包/资源包/光影/数据包/世界）作为内容宿主复用，宿主通过
///     <see cref="PageType" /> 指定资源种类，并通过 <see cref="SetCategoryTags" /> 注入分类筛选项。
/// </summary>
public partial class PageComp : UserControl
{
    public const int PageSize = 40;

    private static readonly string[] CommonGameVersions =
    [
        "1.21.11", "1.21.1", "1.20.6", "1.20.1", "1.19.4", "1.19.2",
        "1.18.2", "1.16.5", "1.12.2", "1.10.2", "1.8.9", "1.7.10"
    ];

    private bool _autoRetried;
    private IReadOnlyList<CompCategoryTag> _categoryTags = [];
    private bool _inited;
    private Loading.LoaderTask<Comp.CompProjectRequest, int>? _loader;
    private int _page;
    private Comp.CompType _pageType = Comp.CompType.Mod;

    private Comp.CompProjectStorage _storage = new();

    public PageComp()
    {
        InitializeComponent();

        PanSearchBox.Search += (_, _) => StartNewSearch();
        PanSearchBox.TextBox.KeyDown += OnSearchBoxKeyDown;
        BtnSearchReset.Click += (_, _) => ResetFilter();
        BtnSearchInstallModPack.Click += (_, _) => Modpack.ModpackInstall();
        BtnPageFirst.Click += (_, _) => ChangePage(0);
        BtnPageLeft.Click += (_, _) => ChangePage(_page - 1);
        BtnPageRight.Click += (_, _) => ChangePage(_page + 1);
        Load.Click += (_, _) =>
        {
            if (_loader is { State: LoadState.Failed })
                StartLoad(true);
        };

        BuildVersionCombo();
        ApplyStaticToolTips();
        Grid.SetIsSharedSizeScope(PanProjects, true);

        ComboSearchSource.SelectedIndex = 0;
        ComboSearchSort.SelectedIndex = 0;
        ComboSearchLoader.SelectedIndex = 0;
        ComboSearchShaderLoader.SelectedIndex = 0;

        AttachedToVisualTree += (_, _) => InitOnce();
    }

    public Comp.CompType PageType
    {
        get => _pageType;
        set
        {
            if (_pageType == value) return;
            _pageType = value;
            if (_inited) ApplyPageTypeUi();
        }
    }

    public string? TargetGameVersion { get; private set; }

    public Comp.CompLoaderType TargetModLoader { get; private set; } = Comp.CompLoaderType.Any;

    public bool SupportCurseForge { get; set; } = true;

    public bool SupportModrinth { get; set; } = true;

    public MyScrollViewer ScrollViewer => PanBack;

    public string? SelectedGameVersion => GetGameVersionFilter();

    public Comp.CompLoaderType SelectedModLoader => GetSelectedModLoader();

    public event Action<CompDetailParameter>? CompDetailRequested;

    public void SetCategoryTags(IEnumerable<CompCategoryTag> tags)
    {
        _categoryTags = tags as IReadOnlyList<CompCategoryTag> ?? [.. tags];
        ApplyCategoryTags();
    }

    public void ApplySearchContext(string? gameVersion, Comp.CompLoaderType modLoader)
    {
        TargetGameVersion = gameVersion;
        TargetModLoader = modLoader;
        if (!_inited) return;
        TextSearchVersion.Text = string.IsNullOrEmpty(gameVersion)
            ? Lang.Text("Download.Comp.Filter.Version.AllInputAvailable")
            : gameVersion;
        SelectComboLoader(modLoader);
        ApplySourceVisibility();
        StartNewSearch();
    }

    public void RefreshSearch()
    {
        StartNewSearch();
    }

    public void RefreshFavoriteStatuses()
    {
        foreach (var child in PanProjects.Children)
            if (child is MyCompItem item)
                item.RefreshFavoriteStatus();
    }

    private void BuildVersionCombo()
    {
        TextSearchVersion.Items.Add(
            new MyComboBoxItem
            {
                Content = Lang.Text("Download.Comp.Filter.Version.Any")
            });
        foreach (var version in CommonGameVersions)
            TextSearchVersion.Items.Add(
                new MyComboBoxItem
                {
                    Content = version
                });
        TextSearchVersion.SelectedIndex = 0;
        TextSearchVersion.Text = Lang.Text("Download.Comp.Filter.Version.AllInputAvailable");
    }

    private void ApplyStaticToolTips()
    {
        ToolTip.SetTip(BtnSearchReset, Lang.Text("Download.Comp.Filter.Reset"));
        ToolTip.SetTip(
            BtnSearchInstallModPack,
            Lang.Text("Download.Comp.Filter.InstallModpack.ToolTip"));
    }

    private void ApplyCategoryTags()
    {
        var selectedIndex = ComboSearchTag.SelectedIndex;
        ComboSearchTag.Items.Clear();
        ComboSearchTag.Items.Add(
            new MyComboBoxItem
            {
                Content = Lang.Text("Common.Option.All"),
                Tag = ""
            });
        foreach (var tag in _categoryTags)
        {
            var item = new MyComboBoxItem
            {
                Content = Lang.Text(tag.TextKey),
                Tag = tag.Tag ?? ""
            };
            if (tag.IsGroupHeader)
                item.IsEnabled = false;
            else if (tag.IsIndented)
                item.Margin = new Thickness(15d, 0d, 0d, 0d);
            ComboSearchTag.Items.Add(item);
        }

        ComboSearchTag.SelectedIndex =
            selectedIndex >= 0 && selectedIndex < ComboSearchTag.ItemCount
                ? selectedIndex
                : 0;
    }

    private void InitOnce()
    {
        if (_inited) return;
        _inited = true;

        _loader = new Loading.LoaderTask<Comp.CompProjectRequest, int>(
            Lang.Text("Download.Comp.List.Source.ResourceFetch", Comp.GetCompTypeName(PageType)),
            Comp.CompProjectsGet)
        {
            reloadTimeout = 60 * 1000
        };
        _loader.OnStateChangedUi += OnLoaderStateChanged;
        Load.State = _loader;

        LocalizationService.LanguageChanged += OnLanguageChanged;
        ApplyPageTypeUi();
        InsertHighestDropVersion();

        if (!string.IsNullOrEmpty(TargetGameVersion))
            TextSearchVersion.Text = TargetGameVersion;
        if (TargetModLoader != Comp.CompLoaderType.Any
            && PageType is Comp.CompType.Mod or Comp.CompType.ModPack)
            SelectComboLoader(TargetModLoader);

        StartNewSearch();
    }

    private void InsertHighestDropVersion()
    {
        try
        {
            var drops = GameDownload.AllDrops;
            if (drops is null || drops.Count == 0 || drops.First() <= 250)
                return;
            var highestVersion = McInstanceInfo.DropToVersion(drops.First());
            if (string.IsNullOrEmpty(highestVersion))
                return;
            if (TextSearchVersion.ItemCount > 1
                && (TextSearchVersion.Items[1] as MyComboBoxItem)?.Content?.ToString()
                == highestVersion)
                return;
            TextSearchVersion.Items.Insert(
                1,
                new MyComboBoxItem
                {
                    Content = highestVersion
                });
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "插入最高版本筛选项失败");
        }
    }

    private void OnSearchBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        StartNewSearch();
        e.Handled = true;
    }

    private void ApplyPageTypeUi()
    {
        PanSearchBox.HintText = Comp.GetCompSearchName(PageType);
        Load.Text = Comp.GetCompLoadingName(PageType);
        _loader?.name = Lang.Text(
            "Download.Comp.List.Source.ResourceFetch",
            Comp.GetCompTypeName(PageType));

        var isModLike = PageType is Comp.CompType.Mod or Comp.CompType.ModPack;
        var isShader = PageType == Comp.CompType.Shader;
        LabLoader.IsVisible = isModLike || isShader;
        ComboSearchLoader.IsVisible = isModLike;
        ComboSearchShaderLoader.IsVisible = isShader;
        BtnSearchInstallModPack.IsVisible = PageType == Comp.CompType.ModPack;
        ApplySourceVisibility();
    }

    private void ApplySourceVisibility()
    {
        ItemSourceCurseForge.IsVisible = SupportCurseForge;
        ItemSourceModrinth.IsVisible = SupportModrinth;
        if (!SupportModrinth && ComboSearchSource.SelectedIndex == 2)
            ComboSearchSource.SelectedIndex = SupportCurseForge ? 1 : 0;
        if (!SupportCurseForge && ComboSearchSource.SelectedIndex == 1)
            ComboSearchSource.SelectedIndex = SupportModrinth ? 2 : 0;
    }

    private void OnLanguageChanged()
    {
        if (!_inited) return;
        ApplyPageTypeUi();
        ApplyCategoryTags();
        ApplyStaticToolTips();
        var versionIndex = TextSearchVersion.SelectedIndex;
        var versionText = TextSearchVersion.Text;
        TextSearchVersion.Items.Clear();
        TextSearchVersion.Items.Add(
            new MyComboBoxItem
            {
                Content = Lang.Text("Download.Comp.Filter.Version.Any")
            });
        foreach (var version in CommonGameVersions)
            TextSearchVersion.Items.Add(
                new MyComboBoxItem
                {
                    Content = version
                });
        TextSearchVersion.SelectedIndex = versionIndex >= 0 ? versionIndex : 0;
        TextSearchVersion.Text = versionIndex > 0
            ? versionText ?? string.Empty
            : Lang.Text("Download.Comp.Filter.Version.AllInputAvailable");
        if (_loader is { State: LoadState.Finished })
            RenderResults();
    }

    private void OnLoaderStateChanged(
        Loading.LoaderBase sender,
        LoadState newState,
        LoadState oldState)
    {
        switch (newState)
        {
            case LoadState.Loading:
                PanAlways.IsVisible = false;
                PanContent.IsVisible = false;
                PanLoad.IsVisible = true;
                break;

            case LoadState.Finished:
                PanLoad.IsVisible = false;
                PanAlways.IsVisible = true;
                PanContent.IsVisible = true;
                RenderResults();
                break;

            case LoadState.Failed:
                PanLoad.IsVisible = true;
                PanAlways.IsVisible = false;
                PanContent.IsVisible = false;
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
    }

    private void RenderResults()
    {
        try
        {
            PanProjects.Children.Clear();
            var results = _storage.results;
            var index = Math.Min(_page * PageSize, results.Count);
            var pageResults = results.Skip(index).Take(PageSize).ToList();
            var showQuickDownload =
                PageType is not (Comp.CompType.ModPack or Comp.CompType.DataPack);
            foreach (var project in pageResults)
            {
                var item = new MyVirtualizingElement<MyCompItem>(() =>
                {
                    var compItem = new MyCompItem(
                        project,
                        GetGameVersionFilter() is null,
                        GetSelectedModLoader() == Comp.CompLoaderType.Any
                        && PageType is Comp.CompType.Mod or Comp.CompType.ModPack,
                        showQuickDownload);
                    compItem.Click += OnCompItemClick;
                    return compItem;
                })
                {
                    Height = 66d
                };
                PanProjects.Children.Add(item);
            }

            if (pageResults.Count == 0)
                PanProjects.Children.Add(
                    new TextBlock
                    {
                        Text = Lang.Text("Download.Comp.List.NoResults"),
                        Margin = new Thickness(0, 6, 0, 6)
                    });

            var errorMessage = _storage.errorMessage;
            HintError.IsVisible = !string.IsNullOrEmpty(errorMessage);
            HintError.Text = errorMessage ?? string.Empty;

            var hasMore = results.Count > PageSize * (_page + 1)
                || _storage.curseForgeOffset < _storage.curseForgeTotal
                || _storage.modrinthOffset < _storage.modrinthTotal;
            CardPages.IsVisible = results.Count > PageSize || _page > 0 || hasMore;
            BtnPageFirst.IsEnabled = _page > 0;
            BtnPageLeft.IsEnabled = _page > 0;
            BtnPageRight.IsEnabled = hasMore;
            BtnPageFirst.Opacity = _page > 0 ? 1d : 0.2d;
            BtnPageLeft.Opacity = _page > 0 ? 1d : 0.2d;
            BtnPageRight.Opacity = hasMore ? 1d : 0.2d;
            LabPage.Text = Lang.Number(_page + 1, "N0");
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

    private void OnCompItemClick(MyCompItem item)
    {
        var context = new CompDetailParameter(
            item.Project,
            PageType,
            GetGameVersionFilter(),
            GetSelectedModLoader());
        var handler = CompDetailRequested;
        if (handler is not null)
        {
            handler(context);
            return;
        }

        PageRouter.Navigate(
            Navigation.PageType.CompDetail,
            PageSubType.Default,
            context);
    }

    private void StartNewSearch()
    {
        _page = 0;
        _autoRetried = false;
        _storage = new Comp.CompProjectStorage();
        StartLoad(true);
    }

    private void StartLoad(bool force)
    {
        if (_loader is null) return;
        PanAlways.IsVisible = false;
        PanContent.IsVisible = false;
        PanLoad.IsVisible = true;
        _loader.Start(BuildRequest(), force);
    }

    private void ChangePage(int newPage)
    {
        if (newPage < 0 || _loader is null) return;
        _page = newPage;
        PanBack.ScrollToHome();
        StartLoad(true);
    }

    private Comp.CompProjectRequest BuildRequest()
    {
        var request = new Comp.CompProjectRequest(PageType, _storage, (_page + 1) * PageSize)
        {
            searchText = NormalizeSearchText(PanSearchBox.Text),
            gameVersion = GetGameVersionFilter(),
            sort = (Comp.CompSortType)GetComboTagInt(ComboSearchSort, 1),
            source = GetSelectedSource()
        };

        var selectedTag = (ComboSearchTag.SelectedItem as MyComboBoxItem)?.Tag?.ToString() ?? "";
        if (PageType == Comp.CompType.Shader)
        {
            var loaderTag =
                (ComboSearchShaderLoader.SelectedItem as MyComboBoxItem)?.Tag?.ToString() ?? "";
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

    private string? GetGameVersionFilter()
    {
        var text = TextSearchVersion.Text?.Trim();
        if (string.IsNullOrEmpty(text)) return null;
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
        var loaderType = (Comp.CompLoaderType)GetComboTagInt(ComboSearchLoader, 0);
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
        var source = (Comp.CompSourceType)GetComboTagInt(ComboSearchSource, 3);
        if (!SupportCurseForge) source &= ~Comp.CompSourceType.CurseForge;
        if (!SupportModrinth) source &= ~Comp.CompSourceType.Modrinth;
        return source == 0 ? Comp.CompSourceType.CurseForge : source;
    }

    private static int GetComboTagInt(ComboBox combo, int fallback)
    {
        return combo.SelectedItem is MyComboBoxItem { Tag: string tag }
            && int.TryParse(tag, out var value)
                ? value
                : fallback;
    }

    private void SelectComboLoader(Comp.CompLoaderType loaderType)
    {
        var target = ((int)loaderType).ToString();
        for (var i = 0; i < ComboSearchLoader.ItemCount; i++)
            if (ComboSearchLoader.Items[i] is MyComboBoxItem { Tag: string tag } && tag == target)
            {
                ComboSearchLoader.SelectedIndex = i;
                return;
            }
    }

    private void ResetFilter()
    {
        PanSearchBox.Text = "";
        TextSearchVersion.SelectedIndex = 0;
        TextSearchVersion.Text = Lang.Text("Download.Comp.Filter.Version.AllInputAvailable");
        ComboSearchSource.SelectedIndex = 0;
        ComboSearchTag.SelectedIndex = 0;
        ComboSearchLoader.SelectedIndex = 0;
        ComboSearchShaderLoader.SelectedIndex = 0;
        ComboSearchSort.SelectedIndex = 0;
        StartNewSearch();
    }
}

/// <summary>打开资源详情页时携带的筛选上下文。</summary>
/// <param name="Project">资源工程。</param>
/// <param name="TargetType">目标资源种类。</param>
/// <param name="TargetGameVersion">目标 Minecraft 版本；为空表示不限制。</param>
/// <param name="TargetModLoader">目标模组加载器。</param>
public sealed record CompDetailParameter(
    Comp.CompProject Project,
    Comp.CompType TargetType,
    string? TargetGameVersion,
    Comp.CompLoaderType TargetModLoader);