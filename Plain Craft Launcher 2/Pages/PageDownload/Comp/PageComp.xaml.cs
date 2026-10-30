using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using PCL.Core.App.Localization;

namespace PCL;

[ContentProperty("SearchTags")]
public partial class PageComp
{
    /// <summary>
    ///     每页展示的结果数量。
    /// </summary>
    public const int pageSize = 40;

    public int page;

    public Comp.CompProjectStorage storage = new();

    private readonly PageCompViewModel _vm = new();

    public PageCompViewModel ViewModel => _vm;

    // 结果 UI 化
    private void Load_OnFinish()
    {
        try
        {
            Common.Log($"[Comp] 开始可视化{TypeNameSpaced}列表，已储藏 {storage.results.Count} 个结果，当前在第 {page + 1} 页");
            // 列表项
            PanProjects.Children.Clear();
            var index = Math.Min(page * pageSize, storage.results.Count - 1);
            foreach (var result in storage.results.GetRange(index, Math.Min(storage.results.Count - index, pageSize)))
            {
                var showQuickDownload = result.Type != Comp.CompType.ModPack &&
                                        result.Type != Comp.CompType.DataPack;
                PanProjects.Children.Add(result.ToCompItem(loader.input.gameVersion is null,
                    loader.input.modLoader == Comp.CompLoaderType.Any &&
                    (PageType == Comp.CompType.Mod || PageType == Comp.CompType.ModPack),
                    showQuickDownload));
            }
            // 页码
            _vm.ApplyPagerState(page, storage);
            // 错误信息
            _vm.ApplyErrorState(storage);

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

    // 自动重试
    private void Load_State(object sender, MyLoading.MyLoadingState state, MyLoading.MyLoadingState oldState)
    {
        switch (loader.State)
        {
            case Common.LoadState.Failed:
            {
                var errorMessage = "";
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

    private void ChangePage(int newPage)
    {
        CardPages.IsEnabled = false;
        page = newPage;
        Launcher.frmMain.BackToTop();
        Common.Log($"[Download] {TypeName}：切换到第 {page + 1} 页");
        Common.RunInThread(() =>
        {
            Thread.Sleep(100); // 等待向上滚的动画结束
            Common.RunInUi(() => CardPages.IsEnabled = true);
            loader.Start();
        });
    }

    // 安装已有整合包按钮
    private void BtnSearchInstallModPack_Click(object sender, EventArgs e)
    {
        Modpack.ModpackInstall();
    }

    /// <summary>
    ///     刷新所有已显示项目的收藏状态
    /// </summary>
    public void RefreshAllFavoriteStatus()
    {
        try
        {
            foreach (var item in PanProjects.Children)
                if (item is MyCompItem)
                    ((MyCompItem)item).RefreshFavoriteStatus();
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
        DependencyProperty.Register("SupportCurseForge", typeof(bool), typeof(PageComp),
            new PropertyMetadata(true, OnSupportCurseForgeChanged));

    public bool SupportCurseForge
    {
        get => (bool)GetValue(SupportCurseForgeProperty);
        set => SetValue(SupportCurseForgeProperty, value);
    }

    public static readonly DependencyProperty SupportModrinthProperty =
        DependencyProperty.Register("SupportModrinth", typeof(bool), typeof(PageComp),
            new PropertyMetadata(true, OnSupportModrinthChanged));

    public bool SupportModrinth
    {
        get => (bool)GetValue(SupportModrinthProperty);
        set => SetValue(SupportModrinthProperty, value);
    }

    private static void OnSupportCurseForgeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((PageComp)d)._vm.SupportCurseForge = (bool)e.NewValue;

    private static void OnSupportModrinthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((PageComp)d)._vm.SupportModrinth = (bool)e.NewValue;

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
        get => field;
        set
        {
            if (field == value)
                return;
            field = value;
            _vm.IsInstallModPackVisible = value == Comp.CompType.ModPack;
            loader.name = Lang.Text("Download.Comp.List.Source.ResourceFetch", TypeName);
            PanSearchBox.HintText = Comp.GetCompSearchName(value);
            Load.Text = Comp.GetCompLoadingName(value);
        }
    } = (Comp.CompType)(-1);

    #endregion

    #region 加载

    /// <summary>
    ///     在切换到页面时，应自动将筛选项设置为与该目标 MC 版本和加载器相同。
    /// </summary>
    public static McInstance targetVersion;

    // 在点击 MyCompItem 时会获取 Loader 的输入，以使资源详情页面可以应用相同的筛选项
    public Loading.LoaderTask<Comp.CompProjectRequest, int> loader;

    private bool isLoaderInited;

    public PageComp()
    {
        loader = new Loading.LoaderTask<Comp.CompProjectRequest, int>(Lang.Text("Download.Comp.List.Source.ResourceFetch", "XXX"), Comp.CompProjectsGet,
            inputDelegate: LoaderInput) { reloadTimeout = 60 * 1000 };
        Loaded += PageCompControls_Inited;
        IsVisibleChanged += PageComp_IsVisibleChanged;
        InitializeComponent();
        DataContext = _vm;
        Load.StateChanged += Load_State;
        BtnPageFirst.Click += BtnPageFirst_Click;
        BtnPageLeft.Click += BtnPageLeft_Click;
        BtnPageRight.Click += BtnPageRight_Click;
        PanSearchBox.Search += (_, _) => StartNewSearch();
        PanSearchBox.KeyDown += EnterTrigger;
        TextSearchVersion.KeyDown += EnterTrigger;
        BtnSearchReset.Click += (_, _) => ResetFilter();
        BtnSearchInstallModPack.Click += BtnSearchInstallModPack_Click;
    }

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
                foreach (MyComboBoxItem Item in ComboSearchLoader.Items)
                    if (string.Equals(Item.Content?.ToString(), name, StringComparison.OrdinalIgnoreCase))
                        return Item;
                return (MyComboBoxItem)ComboSearchLoader.Items[0];
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
            if (isLoaderInited)
                StartNewSearch();
            ScrollToHome();
        }

        // 加载器初始化
        if (isLoaderInited)
            return;
        isLoaderInited = true;
        ((MyPageRight)Parent).PageLoaderInit(Load, PanLoad, PanContent, PanAlways, loader, _ => Load_OnFinish(),
            LoaderInput);
        // 将最高 Drop 加入筛选
        if (GameDownload.AllDrops is not null && GameDownload.AllDrops.Count != 0 && GameDownload.AllDrops.First() > 250)
        {
            var highestVersion = McInstanceInfo.DropToVersion(GameDownload.AllDrops.First());
            if ((((MyComboBoxItem)TextSearchVersion.Items[1]).Content.ToString() ?? "") !=
                (highestVersion ?? "")) // 0 是全部
                TextSearchVersion.Items.Insert(1, new MyComboBoxItem { Content = highestVersion });
        }

        // 根据页面类型控制加载器选择的显示
        if (PageType == Comp.CompType.Shader)
        {
            LabLoader.Visibility = Visibility.Visible;
            ComboSearchLoader.Visibility = Visibility.Collapsed;
            ComboSearchShaderLoader.Visibility = Visibility.Visible;
        }
        else if (PageType == Comp.CompType.Mod || PageType == Comp.CompType.ModPack)
        {
            LabLoader.Visibility = Visibility.Visible;
            ComboSearchLoader.Visibility = Visibility.Visible;
            ComboSearchShaderLoader.Visibility = Visibility.Collapsed;
        }
        else
        {
            LabLoader.Visibility = Visibility.Collapsed;
            ComboSearchLoader.Visibility = Visibility.Collapsed;
            ComboSearchShaderLoader.Visibility = Visibility.Collapsed;
        }
    }

    private void PageComp_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // 当页面变为可见时刷新收藏按钮状态
        if (IsVisible) RefreshAllFavoriteStatus();
    }

    private Comp.CompProjectRequest LoaderInput()
    {
        var request = new Comp.CompProjectRequest(PageType, storage, (page + 1) * pageSize);
        var gameVersion = TextSearchVersion.Text == Lang.Text("Download.Comp.Filter.Version.AllInputAvailable") ? null :
            TextSearchVersion.Text.Contains(".") || TextSearchVersion.Text.Contains("w") ? TextSearchVersion.Text :
            null;
        var modLoader = Comp.CompLoaderType.Any;
        if (PageType == Comp.CompType.Mod || PageType == Comp.CompType.ModPack) // 只有 Mod 考虑加载器
        {
            modLoader = (Comp.CompLoaderType)Common.Val(((MyComboBoxItem)ComboSearchLoader.SelectedItem).Tag);
            if (gameVersion is not null && gameVersion.Contains(".") && Common.Val(gameVersion.Split(".")[1]) < 14d &&
                modLoader == Comp.CompLoaderType.Forge) // 1.14-
                                                           // 选择了 Forge
                modLoader = Comp.CompLoaderType.Any; // 此时，视作没有筛选 Mod Loader（因为部分老 Mod 没有设置自己支持的加载器）
        }

        request.searchText = PanSearchBox.Text;
        request.gameVersion = gameVersion;
        var selectedTag = (ComboSearchTag.SelectedItem as FrameworkElement)?.Tag?.ToString();
        var loaderTag = (ComboSearchShaderLoader.SelectedItem as FrameworkElement)?.Tag?.ToString();

        request.tag = PageType == Comp.CompType.Shader
            ? string.IsNullOrEmpty(loaderTag)
                ? selectedTag
                : selectedTag + loaderTag
            : selectedTag;
        request.modLoader =
            (Comp.CompLoaderType)(PageType == Comp.CompType.Mod || PageType == Comp.CompType.ModPack
                ? Common.Val(((MyComboBoxItem)ComboSearchLoader.SelectedItem).Tag)
                : (double)Comp.CompLoaderType.Any);
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
        object argInput = LoaderInput();
        if (loader.ShouldStart(ref argInput))
            storage = new Comp.CompProjectStorage(); // 避免连续搜索两次使得 CompProjectStorage 引用丢失（#1311）
        loader.Start();
    }

    private void EnterTrigger(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            StartNewSearch();
    }

    // 重置按钮
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
        loader.lastFinishedTime = 0L; // 要求强制重新开始
    }

    private void BtnSearchReset_Click(object sender, EventArgs e)
    {
        ResetFilter();
    }

    #endregion
}