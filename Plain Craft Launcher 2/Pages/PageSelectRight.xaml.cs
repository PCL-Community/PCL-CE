using System.Collections;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.FileIO;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.Configuration.Storage;
using FileSystem = Microsoft.VisualBasic.FileIO.FileSystem;
using PCL.Core.App.Localization;
using PCL.Core.UI;

namespace PCL;

public partial class PageSelectRight : IPageView
{
    private readonly PageSelectRightViewModel _vm = new();
    private const int normalDelay = 75; // 正常输入延迟0.075秒
    private const int quickDelay = 50; // 清空搜索框延迟0.05秒
    private bool isRefreshing;
    private bool _refreshed;

    private DateTime lastInputTime = DateTime.MinValue;
    private DispatcherTimer reloadTimer;

    // 窗口属性
    /// <summary>
    ///     是否显示隐藏的 Minecraft 实例。
    /// </summary>
    public bool showHidden = false;

    public PageSelectRight()
    {
        InitializeComponent();
        DataContext = _vm;
        PanVerSearchBox.HintText = Lang.Text("Select.Instance.Search.Hint");
        Loaded += PageSelectRight_Loaded;
        Unloaded += PageSelectRight_Unloaded;
        LoaderInit();
    }

    // 窗口基础
    private void PageSelectRight_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshOnNavigate();
        PanVerSearchBox.TextChanged += (a, b) => PanVerSearchBox_TextChanged(a, (TextChangedEventArgs)b);

        reloadTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(normalDelay) };
        reloadTimer.Tick += ReloadTimer_Tick;
    }

    public void OnNavigatedTo(object? parameter)
    {
        RefreshOnNavigate();
    }

    public void OnNavigatedFrom()
    {
    }

    private void RefreshOnNavigate()
    {
        if (_refreshed)
            return;
        _refreshed = true;
        Loading.LoaderFolderRun(InstanceList.mcInstanceListLoader, GameFolders.mcFolderSelected,
            Loading.LoaderFolderRunType.RunOnUpdated, 1, @"versions\");
        PanBack.ScrollToHome();
    }

    private void PanVerSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        // 记录最后一次输入时间
        lastInputTime = DateTime.Now;

        isRefreshing = false;

        // 动态调整延迟时间
        if (string.IsNullOrWhiteSpace(PanVerSearchBox.Text))
        {
            if (reloadTimer.Interval.TotalMilliseconds != quickDelay)
                reloadTimer.Interval = TimeSpan.FromMilliseconds(quickDelay);
        }
        else if (reloadTimer.Interval.TotalMilliseconds != normalDelay)
        {
            reloadTimer.Interval = TimeSpan.FromMilliseconds(normalDelay);
        }


        if (!reloadTimer.IsEnabled) reloadTimer.Start();
    }

    private void ReloadTimer_Tick(object sender, EventArgs e)
    {
        // 检查是否超过当前设定的延迟时间没有新输入
        var elapsed = (DateTime.Now - lastInputTime).TotalMilliseconds;
        var currentDelay = reloadTimer.Interval.TotalMilliseconds;

        if (elapsed >= currentDelay && InstanceList.mcInstanceListLoader.State == Common.LoadState.Finished &&
            !isRefreshing)
        {
            isRefreshing = true;

            // 确保在UI线程执行刷新
            Dispatcher.BeginInvoke(new Action(() =>
            {
                McInstanceListUI(InstanceList.mcInstanceListLoader);
                isRefreshing = false;
            }));
            reloadTimer.Stop();
        }
    }

    private void PageSelectRight_Unloaded(object sender, RoutedEventArgs e)
    {
        _refreshed = false;
        // 清理计时器
        if (reloadTimer is not null)
        {
            reloadTimer.Stop();
            reloadTimer.Tick -= ReloadTimer_Tick;
            reloadTimer = null;
        }
    }

    private void LoaderInit()
    {
        PageLoaderInit(Load, PanLoad, PanAllBack, null, InstanceList.mcInstanceListLoader,
            a => this.McInstanceListUI((Loading.LoaderTask<string, int>)a),
            autoRun: false);
    }

    #region 结果 UI 化

    private void McInstanceListUI(Loading.LoaderTask<string, int> loader)
    {
        try
        {
            var path = loader.input;
            // 加载 UI
            PanMain.Children.Clear();

            var hasVisibleFolders = false;
            var searchText = PanVerSearchBox.Text.Trim().ToLower(); // 获取搜索框文本
            var hasAnyResults = false;
            var originalHasInstances = InstanceList.mcInstanceList.ToArray().Any(c => c.Value.Count > 0);

            // 搜索无结果时显示 PanEmptySearch
            _vm.EmptySearchPanelVisibility = Visibility.Collapsed; // 默认隐藏

            foreach (var Card in InstanceList.mcInstanceList.ToArray())
            {
                if ((Card.Key == McInstanceCardType.Hidden) ^ showHidden)
                    continue;
                var filteredInstances = Card.Value.Where(v => _vm.MatchesSearch(v, searchText)).ToList();
                if (filteredInstances.Count == 0)
                    continue;

                hasVisibleFolders = true;
                hasAnyResults = true;
                if (filteredInstances.Count == 0)
                    continue;
                hasVisibleFolders = true;

                #region 确认卡片名称

                var cardName = "";
                switch (Card.Key)
                {
                    case McInstanceCardType.OriginalLike:
                    {
                        cardName = Lang.Text("Select.Instance.Card.Regular");
                        break;
                    }
                    case McInstanceCardType.API:
                    {
                        var isForgeExists = false;
                        var isNeoForgeExists = false;
                        var isFabricExists = false;
                        var isQuiltExists = false;
                        var isLiteExists = false;
                        var isCleanroomExists = false;
                        var isLabyModExists = false;
                        foreach (var instance in Card.Value)
                        {
                            if (!instance.IsLoaded)
                                instance.Load();
                            if (instance.Info.HasFabric)
                                isFabricExists = true;
                            if (instance.Info.HasQuilt)
                                isQuiltExists = true;
                            if (instance.Info.HasLiteLoader)
                                isLiteExists = true;
                            if (instance.Info.HasForge)
                                isForgeExists = true;
                            if (instance.Info.HasNeoForge)
                                isNeoForgeExists = true;
                            if (instance.Info.HasCleanroom)
                                isCleanroomExists = true;
                            if (instance.Info.HasLabyMod)
                                isLabyModExists = true;
                        }

                        if ((isLiteExists ? 1 : 0) + (isForgeExists ? 1 : 0) + (isFabricExists ? 1 : 0) +
                            (isNeoForgeExists ? 1 : 0) + (isQuiltExists ? 1 : 0) + (isCleanroomExists ? 1 : 0) +
                            (isLabyModExists ? 1 : 0) > 1)
                            cardName = Lang.Text("Select.Instance.Card.Modable");
                        else if (isForgeExists)
                            cardName = Lang.Text("Select.Instance.Card.Forge");
                        else if (isNeoForgeExists)
                            cardName = Lang.Text("Select.Instance.Card.NeoForge");
                        else if (isCleanroomExists)
                            cardName = Lang.Text("Select.Instance.Card.Cleanroom");
                        else if (isLabyModExists)
                            cardName = Lang.Text("Select.Instance.Card.LabyMod");
                        else if (isLiteExists)
                            cardName = Lang.Text("Select.Instance.Card.LiteLoader");
                        else if (isQuiltExists)
                            cardName = Lang.Text("Select.Instance.Card.Quilt");
                        else
                            cardName = Lang.Text("Select.Instance.Card.Fabric");

                        break;
                    }
                    case McInstanceCardType.Error:
                    {
                        cardName = Lang.Text("Select.Instance.Card.Error");
                        break;
                    }
                    case McInstanceCardType.Hidden:
                    {
                        cardName = Lang.Text("Select.Instance.Card.Hidden");
                        break;
                    }
                    case McInstanceCardType.Rubbish:
                    {
                        cardName = Lang.Text("Select.Instance.Card.LessUsed");
                        break;
                    }
                    case McInstanceCardType.Star:
                    {
                        cardName = Lang.Text("Select.Instance.Card.Favorites");
                        break;
                    }
                    case McInstanceCardType.Fool:
                    {
                        cardName = Lang.Text("Select.Instance.Card.AprilFools");
                        break;
                    }

                    default:
                    {
                        throw new ArgumentException($"未知的卡片种类（{(int)Card.Key}）");
                    }
                }

                #endregion

                // 建立控件
                var cardTitle = $"{cardName}{(Card.Key == McInstanceCardType.Star ? "" : $" ({Lang.Number(filteredInstances.Count, "N0")})")}";
                var newCard = new MyCard { Title = cardTitle, Margin = new Thickness(0d, 0d, 0d, 15d) };
                var newStack = new StackPanel
                {
                    Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d),
                    VerticalAlignment = VerticalAlignment.Top, RenderTransform = new TranslateTransform(0d, 0d),
                    Tag = filteredInstances
                };
                newCard.Children.Add(newStack);
                newCard.SwapControl = newStack;
                PanMain.Children.Add(newCard);

                // 确定卡片是否展开
                void PutMethod(StackPanel stack)
                {
                    foreach (var item in (IEnumerable)stack.Tag)
                        stack.Children.Add(McVersionListItem((McInstance)item));
                }

                ;
                if (Card.Key == McInstanceCardType.Rubbish ||
                    Card.Key == McInstanceCardType.Error ||
                    Card.Key == McInstanceCardType.Fool)
                {
                    newCard.IsSwapped = true;
                    newCard.InstallMethod = PutMethod;
                }
                else
                {
                    MyCard.StackInstall(ref newStack, PutMethod);
                }
            }

            // 若只有一个卡片，则强制展开
            if (PanMain.Children.Count == 1 && ((MyCard)PanMain.Children[0]).IsSwapped)
                ((MyCard)PanMain.Children[0]).IsSwapped = false;

            _vm.SearchBoxVisibility = hasVisibleFolders ? Visibility.Visible : Visibility.Collapsed;

            // 判断应该显示哪一个页面
            if (!hasAnyResults)
            {
                if (!originalHasInstances)
                {
                    // 完全没有实例的情况
                    _vm.EmptyPanelVisibility = Visibility.Visible;
                    _vm.BackVisibility = Visibility.Collapsed;
                    if (showHidden)
                    {
                        _vm.EmptyTitle = Lang.Text("Select.Instance.Hidden.EmptyTitle");
                        _vm.EmptyContent = Lang.Text("Select.Instance.Hidden.EmptyMessage");
                        _vm.EmptyDownloadVisibility = Visibility.Collapsed;
                    }
                    else
                    {
                        _vm.EmptyTitle = Lang.Text("Select.Instance.Empty.Title");
                        _vm.EmptyContent = Lang.Text("Select.Instance.Empty.Message");
                        _vm.EmptyDownloadVisibility =
                            Config.Preference.Hide.PageDownload && !PageSetupUI.HiddenForceShow
                                ? Visibility.Collapsed
                                : Visibility.Visible;
                    }
                }
                // 有实例但搜索无结果的情况
                else if (showHidden && InstanceList.mcInstanceList.ToArray().Any(c =>
                             c.Key == McInstanceCardType.Hidden && c.Value.Count > 0))
                {
                    // 有隐藏实例但搜索无结果 - 显示搜索无结果提示
                    _vm.SearchBoxVisibility = Visibility.Visible;
                    _vm.EmptyPanelVisibility = Visibility.Collapsed;
                    _vm.BackVisibility = Visibility.Visible;
                    _vm.EmptySearchPanelVisibility = Visibility.Visible;
                    _vm.EmptySearchTitle = Lang.Text("Select.Instance.Hidden.EmptySearchTitle");
                    _vm.EmptySearchContent = string.IsNullOrWhiteSpace(searchText)
                        ? Lang.Text("Select.Instance.Search.EmptyInput")
                        : Lang.Text("Select.Instance.Search.NoHiddenResult", searchText);
                }
                else if (showHidden)
                {
                    // 无隐藏实例 - 显示"无隐藏实例"提示
                    _vm.EmptyPanelVisibility = Visibility.Visible;
                    _vm.BackVisibility = Visibility.Collapsed;
                    _vm.EmptyTitle = Lang.Text("Select.Instance.Hidden.EmptyTitle");
                    _vm.EmptyContent = Lang.Text("Select.Instance.Hidden.EmptyMessage");
                    _vm.EmptyDownloadVisibility = Visibility.Collapsed;
                    _vm.SearchBoxVisibility = Visibility.Collapsed;
                }
                else
                {
                    // 普通模式下的搜索无结果
                    _vm.SearchBoxVisibility = Visibility.Visible;
                    _vm.EmptyPanelVisibility = Visibility.Collapsed;
                    _vm.BackVisibility = Visibility.Visible;
                    _vm.EmptySearchPanelVisibility = Visibility.Visible;
                    _vm.EmptySearchTitle = Lang.Text("Select.Instance.EmptySearch.Title");
                    _vm.EmptySearchContent = string.IsNullOrWhiteSpace(searchText)
                        ? Lang.Text("Select.Instance.Search.EmptyInput")
                        : Lang.Text("Select.Instance.Search.NoResult", searchText);
                }
            }
            else
            {
                _vm.BackVisibility = Visibility.Visible;
                _vm.EmptyPanelVisibility = Visibility.Collapsed;
                _vm.EmptySearchPanelVisibility = Visibility.Collapsed;
            } // 有结果时隐藏
        }


        catch (Exception ex)
        {
            Common.Log(
                ex,
                Lang.Text("Select.Instance.Error.UiUpdate"),
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Select.Instance.Error.UiUpdate"));
        }
    }

    public static MyListItem McVersionListItem(McInstance mcInstance)
    {
        var newItem = new MyListItem
        {
            Title = mcInstance.Name, Info = mcInstance.Desc, Height = 42d, Tag = mcInstance, SnapsToDevicePixels = true,
            Type = MyListItem.CheckType.Clickable
        };
        var instanceInfo = mcInstance.Info;
        var tags = new List<string>();
        tags.Add(instanceInfo.VanillaName);
        if (instanceInfo.HasForge)
            tags.Add("Forge " + instanceInfo.Forge);
        else if (instanceInfo.HasNeoForge)
            tags.Add("NeoForge " + instanceInfo.NeoForge);
        else if (instanceInfo.HasCleanroom)
            tags.Add("Cleanroom " + instanceInfo.Cleanroom);
        else if (instanceInfo.HasLabyMod)
            tags.Add("LabyMod " + instanceInfo.LabyMod);
        else if (instanceInfo.HasQuilt)
            tags.Add("Quilt " + instanceInfo.Quilt);
        else if (instanceInfo.HasFabric) tags.Add("Fabric " + instanceInfo.Fabric);
        if (instanceInfo.HasLiteLoader)
            tags.Add("LiteLoader");
        if (instanceInfo.HasOptiFine)
            tags.Add("OptiFine " + instanceInfo.OptiFine);
        newItem.Tags = tags;
        try
        {
            if (mcInstance.Logo.EndsWith(@"PCL\Logo.png"))
                newItem.Logo = mcInstance.PathInstance + @"PCL\Logo.png"; // 修复老版本中，存储的自定义 Logo 使用完整路径，导致移动后无法加载的 Bug
            else
                newItem.Logo = mcInstance.Logo;
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                Lang.Text("Select.Instance.Error.IconLoad"),
                Common.LogLevel.Hint,
                userSummary: Lang.Text("Select.Instance.Error.IconLoad"));
            newItem.Logo = "pack://application:,,,/images/Blocks/RedstoneBlock.png";
        }

        newItem.ContentHandler = McVersionListContent;
        return newItem;
    }

    private static void McVersionListContent(MyListItem sender, EventArgs e)
    {
        var version = (McInstance)sender.Tag;
        // 注册点击事件
        sender.Click += (a, b) => Item_Click((MyListItem)a, b);
        // 图标按钮
        var btnStar = new MyIconButton();
        if (version.IsStar)
        {
            btnStar.ToolTip = Lang.Text("Select.Instance.Unfavorite");
            ToolTipService.SetPlacement(btnStar, PlacementMode.Center);
            ToolTipService.SetVerticalOffset(btnStar, 30d);
            ToolTipService.SetHorizontalOffset(btnStar, 2d);
            btnStar.LogoScale = 1.1d;
            btnStar.SvgIcon = "lucide/heart-filled";
        }
        else
        {
            btnStar.ToolTip = Lang.Text("Select.Instance.Favorite");
            ToolTipService.SetPlacement(btnStar, PlacementMode.Center);
            ToolTipService.SetVerticalOffset(btnStar, 30d);
            ToolTipService.SetHorizontalOffset(btnStar, 2d);
            btnStar.LogoScale = 1.1d;
            btnStar.SvgIcon = "lucide/heart";
        }

        btnStar.Click += (_, _) =>
        {
            States.Instance.Starred[version.PathInstance] = !version.IsStar;
            InstanceList.mcInstanceListForceRefresh = true;
            Loading.LoaderFolderRun(InstanceList.mcInstanceListLoader, GameFolders.mcFolderSelected,
                Loading.LoaderFolderRunType.ForceRun, 1, @"versions\");
        };
        var btnOpenFolder = new MyIconButton { LogoScale = 1.1d, SvgIcon = "lucide/folder-open" };
        btnOpenFolder.ToolTip = Lang.Text("Select.Instance.OpenFolder");
        ToolTipService.SetPlacement(btnOpenFolder, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnOpenFolder, 30d);
        ToolTipService.SetHorizontalOffset(btnOpenFolder, 2d);
        btnOpenFolder.Click += (_, _) => PageInstanceOverall.OpenVersionFolder(version);
        var btnDel = new MyIconButton { LogoScale = 1.1d, SvgIcon = "lucide/trash-2" };
        btnDel.ToolTip = Lang.Text("Common.Action.Delete");
        ToolTipService.SetPlacement(btnDel, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnDel, 30d);
        ToolTipService.SetHorizontalOffset(btnDel, 2d);
        btnDel.Click += (_, _) => DeleteVersion(sender, version);
        if (version.state != McInstanceState.Error)
        {
            var btnCont = new MyIconButton { LogoScale = 1.1d, SvgIcon = "lucide/settings" };
            btnCont.ToolTip = Lang.Text("Select.Instance.Settings");
            ToolTipService.SetPlacement(btnCont, PlacementMode.Center);
            ToolTipService.SetVerticalOffset(btnCont, 30d);
            ToolTipService.SetHorizontalOffset(btnCont, 2d);
            btnCont.Click += (_, _) =>
            {
                PageInstanceLeft.McInstance = version;
                Launcher.frmMain.PageChange(FormMain.PageType.InstanceSetup);
            };
            sender.MouseRightButtonUp += (_, _) =>
            {
                PageInstanceLeft.McInstance = version;
                Launcher.frmMain.PageChange(FormMain.PageType.InstanceSetup);
            };
            sender.Buttons = new[] { btnStar, btnOpenFolder, btnDel, btnCont };
        }
        else
        {
            var btnCont = new MyIconButton { LogoScale = 1.15d, SvgIcon = "lucide/folder-open" };
            btnCont.ToolTip = Lang.Text("Common.Action.OpenFolder");
            ToolTipService.SetPlacement(btnCont, PlacementMode.Center);
            ToolTipService.SetVerticalOffset(btnCont, 30d);
            ToolTipService.SetHorizontalOffset(btnCont, 2d);
            btnCont.Click += (_, _) => PageInstanceOverall.OpenVersionFolder(version);
            sender.MouseRightButtonUp += (_, _) => PageInstanceOverall.OpenVersionFolder(version);
            sender.Buttons = new[] { btnStar, btnOpenFolder, btnDel, btnCont };
        }
    }

    #endregion

    #region 页面事件

    // 点击选项
    public static void Item_Click(MyListItem sender, EventArgs e)
    {
        var instance = (McInstance)sender.Tag;
        if (new McInstance(instance.PathInstance).Check())
        {
            // 正常实例
            InstanceList.McMcInstanceSelected = instance;
            States.Game.SelectedInstance = InstanceList.McMcInstanceSelected.Name;
            Launcher.frmMain.PageBack();
        }
        else
        {
            // 错误实例
            PageInstanceOverall.OpenVersionFolder(instance);
        }
    }

    // 修改此代码时，同时修改 PageInstanceOverall 中的代码
    public static void DeleteVersion(MyListItem item, McInstance mcInstance)
    {
        try
        {
            var isShiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
            var isHintIndie = mcInstance.state != McInstanceState.Error &&
                              (mcInstance.PathIndie ?? "") != (GameFolders.mcFolderSelected ?? "");
            var confirmMsg = isShiftPressed
                ? Lang.Text("Select.Instance.Delete.ConfirmPermanentMessage", mcInstance.Name)
                : Lang.Text("Select.Instance.Delete.ConfirmMessage", mcInstance.Name);
            var confirmFullMsg = confirmMsg +
                                 (isHintIndie ? "\r\n" + Lang.Text("Select.Instance.Delete.IsolatedWarning") : "");
            switch (Launcher.MyMsgBox(confirmFullMsg, Lang.Text("Select.Instance.Delete.ConfirmTitle"),
                        button2: Lang.Text("Common.Action.Cancel"), isWarn: true))
            {
                case 1:
                {
                    Common.IniClearCache(Path.Combine(mcInstance.PathIndie, "options.txt"));
                    ((DynamicCacheConfigStorage)ConfigService.GetProvider(ConfigSource.GameInstance)).InvalidateCache(
                        mcInstance.PathInstance);
                    if (isShiftPressed)
                    {
                        Common.DeleteDirectory(mcInstance.PathInstance);
                        HintService.Hint(Lang.Text("Select.Instance.Delete.PermanentSuccess", mcInstance.Name),
                            HintType.Success);
                    }
                    else
                    {
                        FileSystem.DeleteDirectory(mcInstance.PathInstance, UIOption.AllDialogs,
                            RecycleOption.SendToRecycleBin);
                        HintService.Hint(Lang.Text("Select.Instance.Delete.RecycleBinSuccess", mcInstance.Name),
                            HintType.Success);
                    }

                    break;
                }
                case 2:
                {
                    return;
                }
            }

            // 从 UI 中移除
            if (mcInstance.displayType == McInstanceCardType.Hidden || !mcInstance.IsStar)
            {
                // 仅出现在当前卡片
                var parent = (StackPanel)item.Parent;
                if (parent.Children.Count > 2) // 当前的项目与一个占位符
                {
                    // 删除后还有剩
                    var card = (MyCard)parent.Parent;
                    card.Title = card.Title.Replace(Lang.Number(parent.Children.Count - 1, "N0"),
                        Lang.Number(parent.Children.Count - 2, "N0")); // 有一个占位符
                    parent.Children.Remove(item);
                    if (InstanceList.McMcInstanceSelected is not null && (mcInstance.PathInstance ?? "") ==
                        (InstanceList.McMcInstanceSelected.PathInstance ?? ""))
                        // 删除当前实例就更改选择
                        InstanceList.McMcInstanceSelected = (McInstance)((MyListItem)parent.Children[0]).Tag;
                    Loading.LoaderFolderRun(InstanceList.mcInstanceListLoader, GameFolders.mcFolderSelected,
                        Loading.LoaderFolderRunType.UpdateOnly, 1, @"versions\");
                }
                else
                {
                    // 删除后没剩了
                    Loading.LoaderFolderRun(InstanceList.mcInstanceListLoader, GameFolders.mcFolderSelected,
                        Loading.LoaderFolderRunType.ForceRun, 1, @"versions\");
                }
            }
            else
            {
                // 同时出现在当前卡片与收藏夹
                Loading.LoaderFolderRun(InstanceList.mcInstanceListLoader, GameFolders.mcFolderSelected,
                    Loading.LoaderFolderRunType.ForceRun, 1, @"versions\");
            }
        }
        catch (OperationCanceledException ex)
        {
            Common.Log(ex, $"删除实例 {mcInstance.Name} 被主动取消");
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                Lang.Text("Select.Instance.Error.Delete", mcInstance.Name),
                Common.LogLevel.Msgbox,
                userSummary: Lang.Text("Select.Instance.Error.Delete", mcInstance.Name));
        }
    }

    public void BtnEmptyDownload_Loaded()
    {
        var oldVisibility = _vm.EmptyDownloadVisibility;
        _vm.RefreshEmptyDownloadVisibility(showHidden);
        if (_vm.EmptyDownloadVisibility != oldVisibility)
            PanLoad.TriggerForceResize();
    }

    #endregion
}
