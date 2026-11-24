using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using PCL.Core.App.Localization;
using PCL.Navigation;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Download;

/// <summary>
/// 资源工程搜索页：筛选 + 搜索结果列表 + 分页，点击条目进入资源详情页。
/// 供下载页的各资源子页（Mod/整合包/资源包/光影/数据包/世界）作为内容宿主复用，宿主通过 <see cref="PageIds" /> 指定资源种类，并通过
/// <see cref="SetCategoryTags" /> 注入分类筛选项。
/// </summary>
public partial class PageComp : UserControl
{
    public const int PageSize = PageCompViewModel.PageSize;

    private IReadOnlyList<CompCategoryTag> _categoryTags = [];

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
        ViewModel.ProjectsChanged += RenderResults;
        ViewModel.CategoryOptionsChanged += ApplyCategoryTags;
        ViewModel.ScrollToTopRequested += () => PanBack.ScrollToHome();
        ViewModel.Initialize();
        Load.State = ViewModel.Loader!;

        BuildVersionCombo();
        ApplyStaticToolTips();
        Grid.SetIsSharedSizeScope(PanProjects, true);

        PanSearchBox.Bind(
            MySearchBox.HintTextProperty,
            new Binding(nameof(PageCompViewModel.SearchHintText))
            {
                Source = ViewModel
            });
        Load.Bind(
            MyLoading.TextProperty,
            new Binding(nameof(PageCompViewModel.LoadText))
            {
                Source = ViewModel
            });

        ComboSearchSource.SelectedIndex = 0;
        ComboSearchSort.SelectedIndex = 0;
        ComboSearchLoader.SelectedIndex = 0;
        ComboSearchShaderLoader.SelectedIndex = 0;
    }

    public PageCompViewModel ViewModel { get; } = new();

    public Comp.CompType PageIds
    {
        get => ViewModel.PageIds;
        set
        {
            if (ViewModel.PageIds == value) return;
            ViewModel.PageIds = value;
            if (IsInitialized) ApplyPageIdsUi();
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
            if (IsInitialized) ApplySourceVisibility();
        }
    }

    public bool SupportModrinth
    {
        get => ViewModel.SupportModrinth;
        set
        {
            if (ViewModel.SupportModrinth == value) return;
            ViewModel.SupportModrinth = value;
            if (IsInitialized) ApplySourceVisibility();
        }
    }

    public MyScrollViewer ScrollViewer => PanBack;

    internal MyLoading LoadingIndicator => Load;

    internal MyCard LoadingCard => PanLoad;

    internal StackPanel ContentPanel => PanContent;

    internal StackPanel AlwaysPanel => PanAlways;

    internal Loading.LoaderBase PageLoader => ViewModel.Loader!;

    internal bool IsInitialized { get; private set; }

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
        if (IsInitialized) ApplyCategoryTags();
    }

    public void ApplySearchContext(string? gameVersion, Comp.CompLoaderType modLoader)
    {
        ViewModel.ApplySearchContext(gameVersion, modLoader);
        if (!IsInitialized) return;
        TextSearchVersion.Text = ViewModel.SearchVersionText;
        TextSearchVersion.SelectedIndex = string.IsNullOrEmpty(ViewModel.SearchVersionText)
            ? -1
            : TextSearchVersion.SelectedIndex;
        ComboSearchLoader.SelectedIndex = ViewModel.SearchLoaderIndex;
        ApplySourceVisibility();
        StartNewSearch();
    }

    public void RefreshSearch()
    {
        StartNewSearch();
    }

    internal Comp.CompProjectRequest BuildLoaderInput()
    {
        SyncFiltersToViewModel();
        return ViewModel.BuildRequest();
    }

    public void RefreshFavoriteStatuses()
    {
        ViewModel.RefreshAllFavoriteStatus();
        foreach (var child in PanProjects.Children)
            if (child is MyCompItem item)
                item.RefreshFavoriteStatus();
    }

    internal void InitializeForLoad(CompSearchContext? context = null)
    {
        if (context is not null)
            ApplySearchContext(context.TargetGameVersion, context.TargetModLoader);

        if (IsInitialized) return;
        IsInitialized = true;

        ApplyPageIdsUi();
        ApplyCategoryTags();
        InsertHighestDropVersion();

        if (!string.IsNullOrEmpty(ViewModel.TargetGameVersion))
            TextSearchVersion.Text = ViewModel.TargetGameVersion;
        if (ViewModel.TargetModLoader != Comp.CompLoaderType.Any
            && PageIds is Comp.CompType.Mod or Comp.CompType.ModPack)
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

    private void ApplyPageIdsUi()
    {
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
            {
                var empty = new TextBlock
                {
                    Margin = new Thickness(0, 6, 0, 6)
                };
                empty.SetLocalizedValue(
                    TextBlock.TextProperty,
                    "Download.Comp.List.NoResults");
                PanProjects.Children.Add(empty);
            }

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
            PageIds,
            ViewModel.SelectedGameVersion,
            ViewModel.SelectedModLoader);
        var handler = CompDetailRequested;
        if (handler is not null)
        {
            handler(context);
            return;
        }

        PageRouter.Navigate(
            Navigation.PageIds.CompDetail,
            null,
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
        var anyItem = new MyComboBoxItem();
        anyItem.SetLocalizedValue(
            ContentProperty,
            "Download.Comp.Filter.Version.Any");
        TextSearchVersion.Items.Add(anyItem);
        foreach (var version in PageCompViewModel.CommonGameVersions)
            TextSearchVersion.Items.Add(
                new MyComboBoxItem
                {
                    Content = version
                });
        TextSearchVersion.SelectedIndex = -1;
        TextSearchVersion.Text = string.Empty;
    }

    private void ApplyCategoryTags()
    {
        var selectedIndex = ComboSearchTag.SelectedIndex;
        ComboSearchTag.Items.Clear();
        foreach (var option in ViewModel.CategoryOptions)
        {
            var item = new MyComboBoxItem
            {
                Tag = option.Tag
            };
            LocalizedText.Bind(
                item,
                ContentProperty,
                () => option.Text);
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
        BtnSearchReset.SetLocalizedValue(
            ToolTip.TipProperty,
            "Download.Comp.Filter.Reset");
        BtnSearchInstallModPack.SetLocalizedValue(
            ToolTip.TipProperty,
            "Download.Comp.Filter.InstallModpack.ToolTip");
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
        TextSearchVersion.SelectedIndex = -1;
        TextSearchVersion.Text = "";
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