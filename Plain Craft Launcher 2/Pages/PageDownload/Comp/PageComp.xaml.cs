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
    public const int PageSize = 40;

    public int Page;

    public ModComp.CompProjectStorage Storage = new();

    // 结果 UI 化
    private void Load_OnFinish()
    {
        try
        {
            ModBase.Log($"[Comp] 开始可视化{TypeNameSpaced}列表，已储藏 {Storage.Results.Count} 个结果，当前在第 {Page + 1} 页");
            // 列表项
            PanProjects.Children.Clear();
            var index = Math.Min(Page * PageSize, Storage.Results.Count - 1);
            foreach (var result in Storage.Results.GetRange(index, Math.Min(Storage.Results.Count - index, PageSize)))
                PanProjects.Children.Add(result.ToCompItem(Loader.Input.GameVersion is null,
                    Loader.Input.ModLoader == ModComp.CompLoaderType.Any &&
                    (PageType == ModComp.CompType.Mod || PageType == ModComp.CompType.ModPack)));
            // 页码
            CardPages.Visibility =
                Storage.Results.Count > 40 || Storage.CurseForgeOffset < Storage.CurseForgeTotal ||
                Storage.ModrinthOffset < Storage.ModrinthTotal
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            LabPage.Text = Lang.Number(Page + 1, "N0");
            BtnPageFirst.IsEnabled = Page > 1;
            BtnPageFirst.Opacity = Page > 1 ? 1d : 0.2d;
            BtnPageLeft.IsEnabled = Page > 0;
            BtnPageLeft.Opacity = Page > 0 ? 1d : 0.2d;
            var IsRightEnabled = Storage.Results.Count > PageSize * (Page + 1) ||
                                 Storage.CurseForgeOffset < Storage.CurseForgeTotal ||
                                 Storage.ModrinthOffset <
                                 Storage.ModrinthTotal; // 由于 WPF 的未知 bug，读取到的 IsEnabled 可能是错误的值（#3319）
            BtnPageRight.IsEnabled = IsRightEnabled;
            BtnPageRight.Opacity = IsRightEnabled ? 1d : 0.2d;
            // 错误信息
            if (Storage.ErrorMessage is null)
            {
                HintError.Visibility = Visibility.Collapsed;
            }
            else
            {
                HintError.Visibility = Visibility.Visible;
                HintError.Text = Storage.ErrorMessage;
            }

            // 强制返回顶部
            ScrollToTop();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, $"可视化{TypeNameSpaced}列表出错", ModBase.LogLevel.Feedback);
        }
    }

    // 自动重试
    private void Load_State(object sender, MyLoading.MyLoadingState state, MyLoading.MyLoadingState oldState)
    {
        switch (Loader.State)
        {
            case ModBase.LoadState.Failed:
            {
                var ErrorMessage = "";
                if (Loader.Error is not null)
                    ErrorMessage = Loader.Error.Message;
                if (ErrorMessage.Contains("不是有效的 json 文件"))
                {
                    ModBase.Log($"[Download] 下载的{TypeNameSpaced}列表 json 文件损坏，已自动重试", ModBase.LogLevel.Debug);
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
        ChangePage(Page - 1);
    }

    private void BtnPageRight_Click(object sender, EventArgs e)
    {
        ChangePage(Page + 1);
    }

    private void ChangePage(int NewPage)
    {
        CardPages.IsEnabled = false;
        Page = NewPage;
        ModMain.FrmMain.BackToTop();
        ModBase.Log($"[Download] {TypeName}：切换到第 {Page + 1} 页");
        ModBase.RunInThread(() =>
        {
            Thread.Sleep(100); // 等待向上滚的动画结束
            ModBase.RunInUi(() => CardPages.IsEnabled = true);
            Loader.Start();
        });
    }

    // 安装已有整合包按钮
    private void BtnSearchInstallModPack_Click(object sender, EventArgs e)
    {
        ModModpack.ModpackInstall();
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
            ModBase.Log(ex, "刷新收藏状态时出错");
        }
    }

    #region 属性

    /// <summary>
    ///     用于 XAML 快速设置的 Tag 下拉框列表。
    /// </summary>
    public ItemCollection SearchTags => ComboSearchTag.Items;

    public static readonly DependencyProperty SupportCurseForgeProperty =
        DependencyProperty.Register("SupportCurseForge", typeof(bool), typeof(PageComp), new PropertyMetadata(true));

    public bool SupportCurseForge
    {
        get => (bool)GetValue(SupportCurseForgeProperty);
        set => SetValue(SupportCurseForgeProperty, value);
    }

    public static readonly DependencyProperty SupportModrinthProperty =
        DependencyProperty.Register("SupportModrinth", typeof(bool), typeof(PageComp), new PropertyMetadata(true));

    public bool SupportModrinth
    {
        get => (bool)GetValue(SupportModrinthProperty);
        set => SetValue(SupportModrinthProperty, value);
    }

    /// <summary>
    ///     英文前后不含空格的可读资源类型名，例如 "Mod"、"整合包"。
    /// </summary>
    public string TypeName
    {
        get => _TypeName;
        set
        {
            if ((_TypeName ?? "") == (value ?? ""))
                return;
            _TypeName = value;
            Loader.Name = $"社区资源获取：{value}";
        }
    }

    private string _TypeName = "";

    /// <summary>
    ///     英文前后含一个空格的可读资源类型名，例如 " Mod "、"整合包"。
    /// </summary>
    public string TypeNameSpaced
    {
        get => _TypeNameSpaced;
        set
        {
            if ((_TypeNameSpaced ?? "") == (value ?? ""))
                return;
            _TypeNameSpaced = value;
            PanSearchBox.HintText = $"搜索{value}";
            Load.Text = $"正在获取{value}列表";
        }
    }

    private string _TypeNameSpaced = "";

    /// <summary>
    ///     该页面对应的资源类型。
    /// </summary>
    public ModComp.CompType PageType
    {
        get => _Type;
        set
        {
            if (_Type == value)
                return;
            _Type = value;
            BtnSearchInstallModPack.Visibility =
                value == ModComp.CompType.ModPack ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private ModComp.CompType _Type = (ModComp.CompType)(-1);

    #endregion

    #region 加载

    /// <summary>
    ///     在切换到页面时，应自动将筛选项设置为与该目标 MC 版本和加载器相同。
    /// </summary>
    public static ModMinecraft.McInstance TargetVersion;

    // 在点击 MyCompItem 时会获取 Loader 的输入，以使资源详情页面可以应用相同的筛选项
    public ModLoader.LoaderTask<ModComp.CompProjectRequest, int> Loader;

    private bool IsLoaderInited;

    public PageComp()
    {
        Loader = new ModLoader.LoaderTask<ModComp.CompProjectRequest, int>("社区资源获取：XXX", ModComp.CompProjectsGet,
            LoaderInput) { ReloadTimeout = 60 * 1000 };
        Loaded += PageCompControls_Inited;
        IsVisibleChanged += PageComp_IsVisibleChanged;
        InitializeComponent();
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
        if (TargetVersion is not null)
        {
            // 设置目标
            ResetFilter(); // 重置筛选器
            TextSearchVersion.Text = TargetVersion.Info.VanillaName;

            MyComboBoxItem GetTargetItemByName(string Name)
            {
                foreach (MyComboBoxItem Item in ComboSearchLoader.Items)
                    if (string.Equals(Item.Content?.ToString(), Name, StringComparison.OrdinalIgnoreCase))
                        return Item;
                return (MyComboBoxItem)ComboSearchLoader.Items[0];
            }

            ;
            if (TargetVersion.Info.HasForge)
                ComboSearchLoader.SelectedItem = GetTargetItemByName("Forge");
            else if (TargetVersion.Info.HasFabric)
                ComboSearchLoader.SelectedItem = GetTargetItemByName("Fabric");
            else if (TargetVersion.Info.HasNeoForge)
                ComboSearchLoader.SelectedItem = GetTargetItemByName("NeoForge");
            else if (TargetVersion.Info.HasQuilt) ComboSearchLoader.SelectedItem = GetTargetItemByName("Quilt");
            TargetVersion = null;
            // 如果已经完成请求，则重新开始
            if (IsLoaderInited)
                StartNewSearch();
            ScrollToHome();
        }

        // 加载器初始化
        if (IsLoaderInited)
            return;
        IsLoaderInited = true;
        ((MyPageRight)Parent).PageLoaderInit(Load, PanLoad, PanContent, PanAlways, Loader, _ => Load_OnFinish(),
            LoaderInput);
        // 将最高 Drop 加入筛选
        if (ModDownload.AllDrops is not null && ModDownload.AllDrops.Count != 0 && ModDownload.AllDrops.First() > 250)
        {
            var HighestVersion = ModMinecraft.McInstanceInfo.DropToVersion(ModDownload.AllDrops.First());
            if ((((MyComboBoxItem)TextSearchVersion.Items[1]).Content.ToString() ?? "") !=
                (HighestVersion ?? "")) // 0 是全部
                TextSearchVersion.Items.Insert(1, new MyComboBoxItem { Content = HighestVersion });
        }

        // 根据页面类型控制加载器选择的显示
        if (PageType == ModComp.CompType.Shader)
        {
            LabLoader.Visibility = Visibility.Visible;
            ComboSearchLoader.Visibility = Visibility.Collapsed;
            ComboSearchShaderLoader.Visibility = Visibility.Visible;
        }
        else if (PageType == ModComp.CompType.Mod || PageType == ModComp.CompType.ModPack)
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

    private ModComp.CompProjectRequest LoaderInput()
    {
        var Request = new ModComp.CompProjectRequest(PageType, Storage, (Page + 1) * PageSize);
        var GameVersion = TextSearchVersion.Text == "全部 (也可自行输入)" ? null :
            TextSearchVersion.Text.Contains(".") || TextSearchVersion.Text.Contains("w") ? TextSearchVersion.Text :
            null;
        var ModLoader = ModComp.CompLoaderType.Any;
        if (PageType == ModComp.CompType.Mod || PageType == ModComp.CompType.ModPack) // 只有 Mod 考虑加载器
        {
            ModLoader = (ModComp.CompLoaderType)ModBase.Val(((MyComboBoxItem)ComboSearchLoader.SelectedItem).Tag);
            if (GameVersion is not null && GameVersion.Contains(".") && ModBase.Val(GameVersion.Split(".")[1]) < 14d &&
                ModLoader == ModComp.CompLoaderType.Forge) // 1.14-
                                                           // 选择了 Forge
                ModLoader = ModComp.CompLoaderType.Any; // 此时，视作没有筛选 Mod Loader（因为部分老 Mod 没有设置自己支持的加载器）
        }

        Request.SearchText = PanSearchBox.Text;
        Request.GameVersion = GameVersion;
        var selectedTag = (ComboSearchTag.SelectedItem as FrameworkElement)?.Tag?.ToString();
        var loaderTag = (ComboSearchShaderLoader.SelectedItem as FrameworkElement)?.Tag?.ToString();

        Request.Tag = PageType == ModComp.CompType.Shader
            ? string.IsNullOrEmpty(loaderTag)
                ? selectedTag
                : selectedTag + loaderTag
            : selectedTag;
        Request.ModLoader =
            (ModComp.CompLoaderType)(PageType == ModComp.CompType.Mod || PageType == ModComp.CompType.ModPack
                ? ModBase.Val(((MyComboBoxItem)ComboSearchLoader.SelectedItem).Tag)
                : (double)ModComp.CompLoaderType.Any);
        Request.Source = (ModComp.CompSourceType)ModBase.Val(((MyComboBoxItem)ComboSearchSource.SelectedItem).Tag);
        Request.Sort = (ModComp.CompSortType)ModBase.Val(((MyComboBoxItem)ComboSearchSort.SelectedItem).Tag);
        return Request;
    }

    #endregion

    #region 搜索

    // 搜索按钮
    private void StartNewSearch()
    {
        Page = 0;
        object argInput = LoaderInput();
        if (Loader.ShouldStart(ref argInput))
            Storage = new ModComp.CompProjectStorage(); // 避免连续搜索两次使得 CompProjectStorage 引用丢失（#1311）
        Loader.Start();
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
        TextSearchVersion.Text = "全部 (也可自行输入)";
        TextSearchVersion.SelectedIndex = 0;
        ComboSearchSource.SelectedIndex = 0;
        ComboSearchTag.SelectedIndex = 0;
        ComboSearchLoader.SelectedIndex = 0;
        ComboSearchShaderLoader.SelectedIndex = 0;
        ComboSearchSort.SelectedIndex = 0;
        Loader.LastFinishedTime = 0L; // 要求强制重新开始
    }

    private void BtnSearchReset_Click(object sender, EventArgs e)
    {
        ResetFilter();
    }

    #endregion
}