using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
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
    public const int PageSize = PageCompViewModel.PageSize;

    private IReadOnlyList<CompCategoryTag> _categoryTags = [];
    private bool _initialised;

    public PageComp()
    {
        InitializeComponent();

        PanSearchBox.Search += (_, _) => StartNewSearch();
        PanSearchBox.TextBox.KeyDown += OnSearchBoxKeyDown;
        BtnSearchReset.Click += (_, _) => ResetFilter();
        BtnSearchInstallModPack.Click += (_, _) => ViewModel.InstallModPackCommand.Execute(null);
        BtnPageFirst.Click += (_, _) =>
        {
            SyncFiltersToViewModel();
            ViewModel.FirstPageCommand.Execute(null);
        };
        BtnPageLeft.Click += (_, _) =>
        {
            SyncFiltersToViewModel();
            ViewModel.PreviousPageCommand.Execute(null);
        };
        BtnPageRight.Click += (_, _) =>
        {
            SyncFiltersToViewModel();
            ViewModel.NextPageCommand.Execute(null);
        };
        Load.Click += (_, _) =>
        {
            SyncFiltersToViewModel();
            ViewModel.RetryCommand.Execute(null);
        };

        ViewModel.StateChanged += ApplyState;
        ViewModel.ProjectsChanged += RenderResults;
        ViewModel.CategoryOptionsChanged += ApplyCategoryTags;
        ViewModel.ScrollToTopRequested += () => PanBack.ScrollToHome();
        ViewModel.Initialize();
        Load.State = ViewModel.Loader!;

        BuildVersionCombo();
        ApplyStaticToolTips();
        Grid.SetIsSharedSizeScope(PanProjects, true);

        ComboSearchSource.SelectedIndex = 0;
        ComboSearchSort.SelectedIndex = 0;
        ComboSearchLoader.SelectedIndex = 0;
        ComboSearchShaderLoader.SelectedIndex = 0;

        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(
            InitOnce,
            DispatcherPriority.Background);
    }

    public PageCompViewModel ViewModel { get; } = new();

    public Comp.CompType PageType
    {
        get => ViewModel.PageType;
        set
        {
            if (ViewModel.PageType == value) return;
            ViewModel.PageType = value;
            if (_initialised) ApplyPageTypeUi();
        }
    }

    public string? TargetGameVersion => ViewModel.TargetGameVersion;

    public Comp.CompLoaderType TargetModLoader => ViewModel.TargetModLoader;

    public bool SupportCurseForge
    {
        get => ViewModel.SupportCurseForge;
        set
        {
            if (ViewModel.SupportCurseForge == value) return;
            ViewModel.SupportCurseForge = value;
            if (_initialised) ApplySourceVisibility();
        }
    }

    public bool SupportModrinth
    {
        get => ViewModel.SupportModrinth;
        set
        {
            if (ViewModel.SupportModrinth == value) return;
            ViewModel.SupportModrinth = value;
            if (_initialised) ApplySourceVisibility();
        }
    }

    public MyScrollViewer ScrollViewer => PanBack;

    public string? SelectedGameVersion => ViewModel.SelectedGameVersion;

    public Comp.CompLoaderType SelectedModLoader => ViewModel.SelectedModLoader;

    public event Action<CompDetailParameter>? CompDetailRequested;

    public void SetCategoryTags(IEnumerable<CompCategoryTag> tags)
    {
        _categoryTags = tags as IReadOnlyList<CompCategoryTag> ?? [.. tags];
        ViewModel.SetCategoryTags(
            _categoryTags.Select(tag => new CompCategoryFilterTag(
                tag.TextKey,
                tag.Tag,
                tag.IsIndented)));
        if (_initialised) ApplyCategoryTags();
    }

    public void ApplySearchContext(string? gameVersion, Comp.CompLoaderType modLoader)
    {
        ViewModel.ApplySearchContext(gameVersion, modLoader);
        if (!_initialised) return;
        TextSearchVersion.Text = ViewModel.SearchVersionText;
        ComboSearchLoader.SelectedIndex = ViewModel.SearchLoaderIndex;
        ApplySourceVisibility();
        StartNewSearch();
    }

    public void RefreshSearch()
    {
        StartNewSearch();
    }

    public void RefreshFavoriteStatuses()
    {
        ViewModel.RefreshAllFavoriteStatus();
        foreach (var child in PanProjects.Children)
            if (child is MyCompItem item)
                item.RefreshFavoriteStatus();
    }

    private void InitOnce()
    {
        if (_initialised) return;
        _initialised = true;

        LocalizationService.LanguageChanged += OnLanguageChanged;
        ApplyPageTypeUi();
        ApplyCategoryTags();
        InsertHighestDropVersion();

        if (!string.IsNullOrEmpty(ViewModel.TargetGameVersion))
            TextSearchVersion.Text = ViewModel.TargetGameVersion;
        if (ViewModel.TargetModLoader != Comp.CompLoaderType.Any
            && PageType is Comp.CompType.Mod or Comp.CompType.ModPack)
            SelectComboLoader(ViewModel.TargetModLoader);

        StartNewSearch();
    }

    private void InsertHighestDropVersion()
    {
        var highestVersion = ViewModel.GetHighestDropVersion();
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

    private void OnSearchBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        StartNewSearch();
        e.Handled = true;
    }

    private void ApplyPageTypeUi()
    {
        PanSearchBox.HintText = ViewModel.SearchHintText;
        Load.Text = ViewModel.LoadText;
        LabLoader.IsVisible = ViewModel.IsLoaderLabelVisible;
        ComboSearchLoader.IsVisible = ViewModel.IsLoaderComboVisible;
        ComboSearchShaderLoader.IsVisible = ViewModel.IsShaderLoaderComboVisible;
        BtnSearchInstallModPack.IsVisible = ViewModel.IsInstallModPackVisible;
        ApplySourceVisibility();
    }

    private void ApplySourceVisibility()
    {
        ItemSourceCurseForge.IsVisible = ViewModel.SupportCurseForge;
        ItemSourceModrinth.IsVisible = ViewModel.SupportModrinth;
        if (!ViewModel.SupportModrinth && ComboSearchSource.SelectedIndex == 2)
            ComboSearchSource.SelectedIndex = ViewModel.SupportCurseForge ? 1 : 0;
        if (!ViewModel.SupportCurseForge && ComboSearchSource.SelectedIndex == 1)
            ComboSearchSource.SelectedIndex = ViewModel.SupportModrinth ? 2 : 0;
    }

    private void ApplyState()
    {
        PanAlways.IsVisible = ViewModel.IsFilterVisible;
        PanContent.IsVisible = ViewModel.IsContentVisible;
        PanLoad.IsVisible = ViewModel.IsLoadingVisible;
    }

    private void OnLanguageChanged()
    {
        if (!_initialised) return;
        ViewModel.RefreshLocalization();
        ApplyPageTypeUi();
        ApplyCategoryTags();
        ApplyStaticToolTips();
        var versionIndex = TextSearchVersion.SelectedIndex;
        var versionText = TextSearchVersion.Text;
        BuildVersionCombo();
        TextSearchVersion.SelectedIndex = versionIndex >= 0 ? versionIndex : 0;
        TextSearchVersion.Text = versionIndex > 0
            ? versionText ?? string.Empty
            : Lang.Text("Download.Comp.Filter.Version.AllInputAvailable");
        SyncFiltersToViewModel();
        ApplySourceVisibility();
    }

    private void RenderResults()
    {
        try
        {
            PanProjects.Children.Clear();
            foreach (var project in ViewModel.Projects)
            {
                var item = new MyVirtualizingElement<MyCompItem>(() =>
                {
                    var compItem = new MyCompItem(project);
                    compItem.Click += OnCompItemClick;
                    return compItem;
                })
                {
                    Height = 66d
                };
                PanProjects.Children.Add(item);
            }

            if (ViewModel.Projects.Count == 0)
                PanProjects.Children.Add(
                    new TextBlock
                    {
                        Text = Lang.Text("Download.Comp.List.NoResults"),
                        Margin = new Thickness(0, 6, 0, 6)
                    });

            HintError.IsVisible = ViewModel.IsErrorVisible;
            HintError.Text = ViewModel.ErrorMessage;
            CardPages.IsVisible = ViewModel.IsPagerVisible || ViewModel.CurrentPage > 0;
            BtnPageFirst.IsEnabled = ViewModel.CanGoFirst;
            BtnPageLeft.IsEnabled = ViewModel.CanGoLeft;
            BtnPageRight.IsEnabled = ViewModel.CanGoRight;
            BtnPageFirst.Opacity = ViewModel.FirstOpacity;
            BtnPageLeft.Opacity = ViewModel.LeftOpacity;
            BtnPageRight.Opacity = ViewModel.RightOpacity;
            LabPage.Text = ViewModel.PageText;
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
        SyncFiltersToViewModel();
        var context = new CompDetailParameter(
            item.Project,
            PageType,
            ViewModel.SelectedGameVersion,
            ViewModel.SelectedModLoader);
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
        SyncFiltersToViewModel();
        ViewModel.StartNewSearch();
    }

    private void SyncFiltersToViewModel()
    {
        ViewModel.SearchText = PanSearchBox.Text;
        ViewModel.SearchVersionText = TextSearchVersion.Text ?? string.Empty;
        ViewModel.SearchSourceIndex = ComboSearchSource.SelectedIndex;
        ViewModel.SearchTagIndex = ComboSearchTag.SelectedIndex;
        ViewModel.SearchSortIndex = ComboSearchSort.SelectedIndex;
        ViewModel.SearchLoaderIndex = ComboSearchLoader.SelectedIndex;
        ViewModel.SearchShaderLoaderIndex = ComboSearchShaderLoader.SelectedIndex;
    }

    private void BuildVersionCombo()
    {
        TextSearchVersion.Items.Clear();
        TextSearchVersion.Items.Add(
            new MyComboBoxItem
            {
                Content = Lang.Text("Download.Comp.Filter.Version.Any")
            });
        foreach (var version in PageCompViewModel.CommonGameVersions)
            TextSearchVersion.Items.Add(
                new MyComboBoxItem
                {
                    Content = version
                });
        TextSearchVersion.SelectedIndex = 0;
        TextSearchVersion.Text = Lang.Text("Download.Comp.Filter.Version.AllInputAvailable");
    }

    private void ApplyCategoryTags()
    {
        var selectedIndex = ComboSearchTag.SelectedIndex;
        ComboSearchTag.Items.Clear();
        foreach (var option in ViewModel.CategoryOptions)
        {
            var item = new MyComboBoxItem
            {
                Content = option.Text,
                Tag = option.Tag
            };
            if (!option.IsEnabled)
                item.IsEnabled = false;
            else if (option.IsIndented)
                item.Margin = new Thickness(15d, 0d, 0d, 0d);
            ComboSearchTag.Items.Add(item);
        }

        ComboSearchTag.SelectedIndex =
            selectedIndex >= 0 && selectedIndex < ComboSearchTag.ItemCount
                ? selectedIndex
                : 0;
    }

    private void ApplyStaticToolTips()
    {
        ToolTip.SetTip(BtnSearchReset, Lang.Text("Download.Comp.Filter.Reset"));
        ToolTip.SetTip(
            BtnSearchInstallModPack,
            Lang.Text("Download.Comp.Filter.InstallModpack.ToolTip"));
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