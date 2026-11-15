using PCL.Core.App.Localization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace PCL;

[ContentProperty("SearchTags")]
public partial class PageComp
{
    /// <summary>
    ///     每页展示的结果数量。
    /// </summary>
    public const int PageSize = 40;

    public int page;

    public Comp.CompProjectStorage storage = new();

    public PageCompViewModel ViewModel { get; } = new();

    /// <summary>
    ///     列表区域的滚动控件。外层页面通过 MyPageRight.PanScroll 绑定它，以支持「返回顶部」。
    /// </summary>
    public MyScrollViewer ListScroll => this;

    private void Load_OnFinish()
    {
        try
        {
            Common.Log($"[Comp] 开始可视化{TypeNameSpaced}列表，已储藏 {storage.results.Count} 个结果，当前在第 {page + 1} 页");

            var startIndex = page * PageSize;
            var count = startIndex >= storage.results.Count
                ? 0
                : Math.Min(storage.results.Count - startIndex, PageSize);
            var items = new List<MyCompItemViewModel>(count);
            if (count > 0)
            {
                var showMcVersionDesc = loader.input!.gameVersion is null;
                var showLoaderDesc = loader.input.modLoader == Comp.CompLoaderType.Any &&
                                     (PageType == Comp.CompType.Mod || PageType == Comp.CompType.ModPack);
                foreach (var result in storage.results.GetRange(startIndex, count))
                {
                    var showQuickDownload = result.Type != Comp.CompType.ModPack &&
                                            result.Type != Comp.CompType.DataPack;
                    items.Add(result.ToCompItemViewModel(showMcVersionDesc, showLoaderDesc, showQuickDownload));
                }
            }

            ViewModel.ReplaceProjects(items);
            // 页码
            ViewModel.ApplyPagerState(page, storage);
            // 错误信息
            ViewModel.ApplyErrorState(storage);

            // 强制返回顶部
            ScrollToTop();
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                $"可视化{TypeNameSpaced}列表出错",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Download.Comp.Error.OperationFailed"));
        }
    }

    private void Load_State(object sender, LoadingState state, LoadingState oldState)
    {
        switch (loader.State)
        {
            case LoadState.Failed:
                {
                    var errorMessage = "";
                    // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
                    // this maybe a wrong, Exception will be null
                    if (loader.Error is not null)
                        errorMessage = loader.Error.Message;
                    if (errorMessage.Contains(Lang.Text("Common.Error.InvalidJson")))
                    {
                        Common.Log($"[Download] 下载的{TypeNameSpaced}列表 json 文件损坏，已自动重试", Common.LogLevel.Debug);
                        ((MyPageRight)Parent).PageLoaderRestart();
                    }

                    break;
                }
        }
    }

    // 切换页码
    private void BtnPageFirst_Click(object sender, EventArgs e)
    {
        ChangePage(0);
    }

    private void BtnPageLeft_Click(object sender, EventArgs e)
    {
        ChangePage(page - 1);
    }

    private void BtnPageRight_Click(object sender, EventArgs e)
    {
        ChangePage(page + 1);
    }

    private async void ChangePage(int newPage)
    {
        CardPages.IsEnabled = false;
        page = newPage;
        Launcher.frmMain!.BackToTop();
        Common.Log($"[Download] {TypeName}：切换到第 {page + 1} 页");
        // 等待返回顶部的平滑滚动动画结束，避免加载完成后滚动位置错乱
        await Task.Delay(150).ConfigureAwait(true);
        CardPages.IsEnabled = true;
        loader.Start();
    }

    /// <summary>
    ///     刷新所有已显示项目的收藏状态
    /// </summary>
    public void RefreshAllFavoriteStatus()
    {
        try
        {
            ViewModel.RefreshAllFavoriteStatus();
        }
        catch (Exception ex)
        {
            Common.Log(ex, "刷新收藏状态时出错");
        }
    }

    #region 属性

    /// <summary>
    ///     用于 XAML 快速设置的 Tag 下拉框列表。
    /// </summary>
    public ItemCollection SearchTags => ComboSearchTag.Items;

    public static readonly DependencyProperty SupportCurseForgeProperty =
        DependencyProperty.Register(nameof(SupportCurseForge), typeof(bool), typeof(PageComp),
            new PropertyMetadata(true, OnSupportCurseForgeChanged));

    public bool SupportCurseForge
    {
        get => (bool)GetValue(SupportCurseForgeProperty);
        set => SetValue(SupportCurseForgeProperty, value);
    }

    public static readonly DependencyProperty SupportModrinthProperty =
        DependencyProperty.Register(nameof(SupportModrinth), typeof(bool), typeof(PageComp),
            new PropertyMetadata(true, OnSupportModrinthChanged));

    public bool SupportModrinth
    {
        get => (bool)GetValue(SupportModrinthProperty);
        set => SetValue(SupportModrinthProperty, value);
    }

    private static void OnSupportCurseForgeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((PageComp)d).ViewModel.SupportCurseForge = (bool)e.NewValue;

    private static void OnSupportModrinthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((PageComp)d).ViewModel.SupportModrinth = (bool)e.NewValue;

    /// <summary>
    ///     英文前后不含空格的可读资源类型名，例如 "Mod"、"整合包"。
    /// </summary>
    public string TypeName => Comp.GetCompTypeName(PageType);

    /// <summary>
    ///     英文前后含一个空格的可读资源类型名，例如 " Mod "、"整合包"。
    /// </summary>
    public string TypeNameSpaced => TypeName;

    /// <summary>
    ///     该页面对应的资源类型。
    /// </summary>
    public Comp.CompType PageType
    {
        get;
        set
        {
            if (field == value)
                return;
            field = value;
            ViewModel.IsInstallModPackVisible = value == Comp.CompType.ModPack;
            loader.name = Lang.Text("Download.Comp.List.Source.ResourceFetch", TypeName);
            PanSearchBox.HintText = Comp.GetCompSearchName(value);
            ViewModel.LoadingText = Comp.GetCompLoadingName(value);
        }
    } = (Comp.CompType)(-1);

    #endregion

    #region 加载

    /// <summary>
    ///     在切换到页面时，应自动将筛选项设置为与该目标 MC 版本和加载器相同。
    /// </summary>
    public static McInstance? targetVersion;

    public Loading.LoaderTask<Comp.CompProjectRequest, int> loader;

    private bool _isLoaderInited;

    public PageComp()
    {
        loader = new Loading.LoaderTask<Comp.CompProjectRequest, int>(Lang.Text("Download.Comp.List.Source.ResourceFetch", "XXX"), Comp.CompProjectsGet,
            inputDelegate: LoaderInput)
        { reloadTimeout = 60 * 1000 };
        Loaded += PageCompControls_Inited;
        IsVisibleChanged += PageComp_IsVisibleChanged;
        InitializeComponent();
        DataContext = ViewModel;
    }

    private void PanSearchBox_Search(object sender, EventArgs e) => StartNewSearch();

    private void PageCompControls_Inited(object sender, EventArgs e)
    {
        // 不知道从 Initialized 改成 Loaded 会不会有问题，但用 Initialized 会导致初始的筛选器修改被覆盖回默认值
        if (targetVersion is not null)
        {
            // 设置目标
            ResetFilter(); // 重置筛选器
            TextSearchVersion.Text = targetVersion.Info.VanillaName;

            MyComboBoxItem GetTargetItemByName(string name)
            {
                foreach (MyComboBoxItem item in ComboSearchLoader.Items)
                    if (string.Equals(item.Content?.ToString(), name, StringComparison.OrdinalIgnoreCase))
                        return item;
                return (MyComboBoxItem)ComboSearchLoader.Items[0]!;
            }

            ;
            if (targetVersion.Info.HasForge)
                ComboSearchLoader.SelectedItem = GetTargetItemByName("Forge");
            else if (targetVersion.Info.HasFabric)
                ComboSearchLoader.SelectedItem = GetTargetItemByName("Fabric");
            else if (targetVersion.Info.HasNeoForge)
                ComboSearchLoader.SelectedItem = GetTargetItemByName("NeoForge");
            else if (targetVersion.Info.HasQuilt) ComboSearchLoader.SelectedItem = GetTargetItemByName("Quilt");
            targetVersion = null;
            // 如果已经完成请求，则重新开始
            if (_isLoaderInited)
                StartNewSearch();
            ScrollToHome();
        }

        // 加载器初始化
        if (_isLoaderInited)
            return;
        _isLoaderInited = true;
        ((MyPageRight)Parent).PageLoaderInit(Load, PanLoad, PanContent, PanAlways, loader, _ => Load_OnFinish(),
            LoaderInput);

        if (GameDownload.AllDrops is not null && GameDownload.AllDrops.Count != 0 && GameDownload.AllDrops[0] > 250)
        {
            var highestVersion = McInstanceInfo.DropToVersion(GameDownload.AllDrops[0]);
            if ((((MyComboBoxItem)TextSearchVersion.Items[1]!).Content.ToString() ?? "") !=
                (highestVersion ?? "")) // 0 是全部
                TextSearchVersion.Items.Insert(1, new MyComboBoxItem { Content = highestVersion });
        }

        if (PageType == Comp.CompType.Shader)
        {
            ViewModel.IsLoaderLabelVisible = true;
            ViewModel.IsLoaderComboVisible = false;
            ViewModel.IsShaderLoaderComboVisible = true;
        }
        else if (PageType is Comp.CompType.Mod or Comp.CompType.ModPack)
        {
            ViewModel.IsLoaderLabelVisible = true;
            ViewModel.IsLoaderComboVisible = true;
            ViewModel.IsShaderLoaderComboVisible = false;
        }
        else
        {
            ViewModel.IsLoaderLabelVisible = false;
            ViewModel.IsLoaderComboVisible = false;
            ViewModel.IsShaderLoaderComboVisible = false;
        }
    }

    private void PageComp_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible) RefreshAllFavoriteStatus();
    }

    private Comp.CompProjectRequest LoaderInput()
    {
        var request = new Comp.CompProjectRequest(PageType, storage, (page + 1) * PageSize);
        var gameVersion = TextSearchVersion.Text == Lang.Text("Download.Comp.Filter.Version.AllInputAvailable") ? null :
            TextSearchVersion.Text.Contains('.') || TextSearchVersion.Text.Contains('w') ? TextSearchVersion.Text :
            null;

        request.searchText = PanSearchBox.Text.Trim();
        if (request.searchText.RegexCheck(@"\|.*[\u4e00-\u9fff]"))
            request.searchText = request.searchText.BeforeFirst("|").Trim();
        else if (request.searchText.RegexCheck(@"[\u4e00-\u9fff].*\|"))
            request.searchText = request.searchText.AfterFirst("|").Trim();
        request.gameVersion = gameVersion;
        var selectedTag = (ComboSearchTag.SelectedItem as FrameworkElement)?.Tag?.ToString();
        var loaderTag = (ComboSearchShaderLoader.SelectedItem as FrameworkElement)?.Tag?.ToString();

        request.tag = (PageType == Comp.CompType.Shader
            ? string.IsNullOrEmpty(loaderTag)
                ? selectedTag
                : selectedTag + loaderTag
            : selectedTag) ?? string.Empty;
        request.modLoader = PageType is Comp.CompType.Mod or Comp.CompType.ModPack
            ? (Comp.CompLoaderType)Common.Val(((MyComboBoxItem)ComboSearchLoader.SelectedItem).Tag)
            : Comp.CompLoaderType.Any;
        request.source = (Comp.CompSourceType)Common.Val(((MyComboBoxItem)ComboSearchSource.SelectedItem).Tag);
        request.sort = (Comp.CompSortType)Common.Val(((MyComboBoxItem)ComboSearchSort.SelectedItem).Tag);
        return request;
    }

    #endregion

    #region 搜索

    // 搜索按钮
    private void StartNewSearch()
    {
        page = 0;
        object? argInput = LoaderInput();
        if (loader.ShouldStart(ref argInput))
            storage = new Comp.CompProjectStorage(); // 避免连续搜索两次使得 CompProjectStorage 引用丢失（#1311）
        loader.Start();
    }

    private void EnterTrigger(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            StartNewSearch();
    }

    private void ResetFilter()
    {
        PanSearchBox.Text = "";
        TextSearchVersion.Text = Lang.Text("Download.Comp.Filter.Version.AllInputAvailable");
        TextSearchVersion.SelectedIndex = 0;
        ComboSearchSource.SelectedIndex = 0;
        ComboSearchTag.SelectedIndex = 0;
        ComboSearchLoader.SelectedIndex = 0;
        ComboSearchShaderLoader.SelectedIndex = 0;
        ComboSearchSort.SelectedIndex = 0;
        loader.lastFinishedTime = 0L;
    }

    private void BtnSearchReset_Click(object sender, EventArgs e)
    {
        ResetFilter();
    }

    #endregion
}