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

public partial class PageSelectRight
{
    private const int normalDelay = 75; // 正常输入延迟0.075秒
    private const int quickDelay = 50; // 清空搜索框延迟0.05秒
    private bool isRefreshing;

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
        PanVerSearchBox.HintText = Lang.Text("Select.Instance.Search.Hint");
        Loaded += PageSelectRight_Loaded;
        Unloaded += PageSelectRight_Unloaded;
        LoaderInit();
    }

    // 窗口基础
    private void PageSelectRight_Loaded(object sender, RoutedEventArgs e)
    {
        ModLoader.LoaderFolderRun(ModMinecraft.mcInstanceListLoader, ModMinecraft.mcFolderSelected,
            ModLoader.LoaderFolderRunType.RunOnUpdated, 1, @"versions\");
        PanBack.ScrollToHome();
        PanVerSearchBox.TextChanged += (a, b) => PanVerSearchBox_TextChanged(a, (TextChangedEventArgs)b);

        reloadTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(normalDelay) };
        reloadTimer.Tick += ReloadTimer_Tick;
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

        if (elapsed >= currentDelay && ModMinecraft.mcInstanceListLoader.State == ModBase.LoadState.Finished &&
            !isRefreshing)
        {
            isRefreshing = true;

            // 确保在UI线程执行刷新
            Dispatcher.BeginInvoke(new Action(() =>
            {
                McInstanceListUI(ModMinecraft.mcInstanceListLoader);
                isRefreshing = false;
            }));
            reloadTimer.Stop();
        }
    }

    private void PageSelectRight_Unloaded(object sender, RoutedEventArgs e)
    {
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
        PageLoaderInit(Load, PanLoad, PanAllBack, null, ModMinecraft.mcInstanceListLoader,
            a => this.McInstanceListUI((ModLoader.LoaderTask<string, int>)a),
            AutoRun: false);
    }

    private void Load_Click(object sender, MouseButtonEventArgs e)
    {
        if (ModMinecraft.mcInstanceListLoader.State == ModBase.LoadState.Failed)
            ModLoader.LoaderFolderRun(ModMinecraft.mcInstanceListLoader, ModMinecraft.mcFolderSelected,
                ModLoader.LoaderFolderRunType.ForceRun, 1, @"versions\");
    }

    #region 结果 UI 化

    private void McInstanceListUI(ModLoader.LoaderTask<string, int> Loader)
    {
        try
        {
            var path = Loader.input;
            // 加载 UI
            PanMain.Children.Clear();

            var hasVisibleFolders = false;
            var searchText = PanVerSearchBox.Text.Trim().ToLower(); // 获取搜索框文本
            var hasAnyResults = false;
            var originalHasInstances = ModMinecraft.mcInstanceList.ToArray().Any(c => c.Value.Count > 0);

            // 搜索无结果时显示 PanEmptySearch
            PanEmptySearch.Visibility = Visibility.Collapsed; // 默认隐藏

            foreach (var Card in ModMinecraft.mcInstanceList.ToArray())
            {
                if ((Card.Key == ModMinecraft.McInstanceCardType.Hidden) ^ showHidden)
                    continue;
                var filteredInstances = Card.Value.Where(v =>
                {
                    if (string.IsNullOrEmpty(searchText))
                        return true;
                    return v.Name.ToLower().Contains(searchText) ||
                           (v.desc is not null && v.desc.ToLower().Contains(searchText)) || v.GetDefaultDescription()
                               .Replace(",", "").ToLower().Trim().Contains(searchText);
                }).ToList();
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
                    case ModMinecraft.McInstanceCardType.OriginalLike:
                    {
                        cardName = Lang.Text("Select.Instance.Card.Regular");
                        break;
                    }
                    case ModMinecraft.McInstanceCardType.API:
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
                            if (!instance.isLoaded)
                                instance.Load();
                            if (instance.Info.hasFabric)
                                isFabricExists = true;
                            if (instance.Info.hasQuilt)
                                isQuiltExists = true;
                            if (instance.Info.hasLiteLoader)
                                isLiteExists = true;
                            if (instance.Info.hasForge)
                                isForgeExists = true;
                            if (instance.Info.hasNeoForge)
                                isNeoForgeExists = true;
                            if (instance.Info.hasCleanroom)
                                isCleanroomExists = true;
                            if (instance.Info.hasLabyMod)
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
                    case ModMinecraft.McInstanceCardType.Error:
                    {
                        cardName = Lang.Text("Select.Instance.Card.Error");
                        break;
                    }
                    case ModMinecraft.McInstanceCardType.Hidden:
                    {
                        cardName = Lang.Text("Select.Instance.Card.Hidden");
                        break;
                    }
                    case ModMinecraft.McInstanceCardType.Rubbish:
                    {
                        cardName = Lang.Text("Select.Instance.Card.LessUsed");
                        break;
                    }
                    case ModMinecraft.McInstanceCardType.Star:
                    {
                        cardName = Lang.Text("Select.Instance.Card.Favorites");
                        break;
                    }
                    case ModMinecraft.McInstanceCardType.Fool:
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
                var cardTitle = $"{cardName}{(Card.Key == ModMinecraft.McInstanceCardType.Star ? "" : $" ({Lang.Number(filteredInstances.Count, "N0")})")}";
                var newCard = new MyCard { Title = cardTitle, Margin = new Thickness(0d, 0d, 0d, 15d) };
                var newStack = new StackPanel
                {
                    Margin = new Thickness(20d, MyCard.swapedHeight, 18d, 0d),
                    VerticalAlignment = VerticalAlignment.Top, RenderTransform = new TranslateTransform(0d, 0d),
                    Tag = filteredInstances
                };
                newCard.Children.Add(newStack);
                newCard.swapControl = newStack;
                PanMain.Children.Add(newCard);

                // 确定卡片是否展开
                void PutMethod(StackPanel Stack)
                {
                    foreach (var item in (IEnumerable)Stack.Tag)
                        Stack.Children.Add(McVersionListItem((ModMinecraft.McInstance)item));
                }

                ;
                if (Card.Key == ModMinecraft.McInstanceCardType.Rubbish ||
                    Card.Key == ModMinecraft.McInstanceCardType.Error ||
                    Card.Key == ModMinecraft.McInstanceCardType.Fool)
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

            PanVerSearchBox.Visibility = hasVisibleFolders ? Visibility.Visible : Visibility.Collapsed;

            // 判断应该显示哪一个页面
            if (!hasAnyResults)
            {
                if (!originalHasInstances)
                {
                    // 完全没有实例的情况
                    PanEmpty.Visibility = Visibility.Visible;
                    PanBack.Visibility = Visibility.Collapsed;
                    if (showHidden)
                    {
                        LabEmptyTitle.Text = Lang.Text("Select.Instance.Hidden.EmptyTitle");
                        LabEmptyContent.Text = Lang.Text("Select.Instance.Hidden.EmptyMessage");
                        BtnEmptyDownload.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        LabEmptyTitle.Text = Lang.Text("Select.Instance.Empty.Title");
                        LabEmptyContent.Text = Lang.Text("Select.Instance.Empty.Message");
                        BtnEmptyDownload.Visibility =
                            Config.Preference.Hide.PageDownload && !PageSetupUI.HiddenForceShow
                                ? Visibility.Collapsed
                                : Visibility.Visible;
                    }
                }
                // 有实例但搜索无结果的情况
                else if (showHidden && ModMinecraft.mcInstanceList.ToArray().Any(c =>
                             c.Key == ModMinecraft.McInstanceCardType.Hidden && c.Value.Count > 0))
                {
                    // 有隐藏实例但搜索无结果 - 显示搜索无结果提示
                    PanVerSearchBox.Visibility = Visibility.Visible;
                    PanEmpty.Visibility = Visibility.Collapsed;
                    PanBack.Visibility = Visibility.Visible;
                    PanEmptySearch.Visibility = Visibility.Visible;
                    LabEmptySearchTitle.Text = Lang.Text("Select.Instance.Hidden.EmptySearchTitle");
                    LabEmptySearchContent.Text = string.IsNullOrWhiteSpace(searchText)
                        ? Lang.Text("Select.Instance.Search.EmptyInput")
                        : Lang.Text("Select.Instance.Search.NoHiddenResult", searchText);
                }
                else if (showHidden)
                {
                    // 无隐藏实例 - 显示"无隐藏实例"提示
                    PanEmpty.Visibility = Visibility.Visible;
                    PanBack.Visibility = Visibility.Collapsed;
                    LabEmptyTitle.Text = Lang.Text("Select.Instance.Hidden.EmptyTitle");
                    LabEmptyContent.Text = Lang.Text("Select.Instance.Hidden.EmptyMessage");
                    BtnEmptyDownload.Visibility = Visibility.Collapsed;
                    PanVerSearchBox.Visibility = Visibility.Collapsed;
                }
                else
                {
                    // 普通模式下的搜索无结果
                    PanVerSearchBox.Visibility = Visibility.Visible;
                    PanEmpty.Visibility = Visibility.Collapsed;
                    PanBack.Visibility = Visibility.Visible;
                    PanEmptySearch.Visibility = Visibility.Visible;
                    LabEmptySearchTitle.Text = Lang.Text("Select.Instance.EmptySearch.Title");
                    LabEmptySearchContent.Text = string.IsNullOrWhiteSpace(searchText)
                        ? Lang.Text("Select.Instance.Search.EmptyInput")
                        : Lang.Text("Select.Instance.Search.NoResult", searchText);
                }
            }
            else
            {
                PanBack.Visibility = Visibility.Visible;
                PanEmpty.Visibility = Visibility.Collapsed;
                PanEmptySearch.Visibility = Visibility.Collapsed;
            } // 有结果时隐藏
        }


        catch (Exception ex)
        {
            ModBase.Log(ex, Lang.Text("Select.Instance.Error.UiUpdate"), ModBase.LogLevel.Feedback);
        }
    }

    public static MyListItem McVersionListItem(ModMinecraft.McInstance instance)
    {
        var newItem = new MyListItem
        {
            Title = instance.Name, Info = instance.desc, Height = 42d, Tag = instance, SnapsToDevicePixels = true,
            Type = MyListItem.CheckType.Clickable
        };
        var instanceInfo = instance.Info;
        var tags = new List<string>();
        tags.Add(instanceInfo.vanillaName);
        if (instanceInfo.hasForge)
            tags.Add("Forge " + instanceInfo.forge);
        else if (instanceInfo.hasNeoForge)
            tags.Add("NeoForge " + instanceInfo.neoForge);
        else if (instanceInfo.hasCleanroom)
            tags.Add("Cleanroom " + instanceInfo.cleanroom);
        else if (instanceInfo.hasLabyMod)
            tags.Add("LabyMod " + instanceInfo.labyMod);
        else if (instanceInfo.hasQuilt)
            tags.Add("Quilt " + instanceInfo.quilt);
        else if (instanceInfo.hasFabric) tags.Add("Fabric " + instanceInfo.fabric);
        if (instanceInfo.hasLiteLoader)
            tags.Add("LiteLoader");
        if (instanceInfo.hasOptiFine)
            tags.Add("OptiFine " + instanceInfo.optiFine);
        newItem.Tags = tags;
        try
        {
            if (instance.logo.EndsWith(@"PCL\Logo.png"))
                newItem.Logo = instance.PathInstance + @"PCL\Logo.png"; // 修复老版本中，存储的自定义 Logo 使用完整路径，导致移动后无法加载的 Bug
            else
                newItem.Logo = instance.logo;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, Lang.Text("Select.Instance.Error.IconLoad"), ModBase.LogLevel.Hint);
            newItem.Logo = "pack://application:,,,/images/Blocks/RedstoneBlock.png";
        }

        newItem.ContentHandler = McVersionListContent;
        return newItem;
    }

    private static void McVersionListContent(MyListItem sender, EventArgs e)
    {
        var version = (ModMinecraft.McInstance)sender.Tag;
        // 注册点击事件
        sender.Click += (a, b) => Item_Click((MyListItem)a, b);
        // 图标按钮
        var btnStar = new MyIconButton();
        if (version.isStar)
        {
            btnStar.ToolTip = Lang.Text("Select.Instance.Unfavorite");
            ToolTipService.SetPlacement(btnStar, PlacementMode.Center);
            ToolTipService.SetVerticalOffset(btnStar, 30d);
            ToolTipService.SetHorizontalOffset(btnStar, 2d);
            btnStar.LogoScale = 1.1d;
            btnStar.Logo = Icon.IconButtonLikeFill;
        }
        else
        {
            btnStar.ToolTip = Lang.Text("Select.Instance.Favorite");
            ToolTipService.SetPlacement(btnStar, PlacementMode.Center);
            ToolTipService.SetVerticalOffset(btnStar, 30d);
            ToolTipService.SetHorizontalOffset(btnStar, 2d);
            btnStar.LogoScale = 1.1d;
            btnStar.Logo = Icon.IconButtonLikeLine;
        }

        btnStar.Click += (_, _) =>
        {
            States.Instance.Starred[version.PathInstance] = !version.isStar;
            ModMinecraft.mcInstanceListForceRefresh = true;
            ModLoader.LoaderFolderRun(ModMinecraft.mcInstanceListLoader, ModMinecraft.mcFolderSelected,
                ModLoader.LoaderFolderRunType.ForceRun, 1, @"versions\");
        };
        var btnOpenFolder = new MyIconButton { LogoScale = 1.1d, Logo = Icon.IconButtonOpen };
        btnOpenFolder.ToolTip = Lang.Text("Select.Instance.OpenFolder");
        ToolTipService.SetPlacement(btnOpenFolder, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnOpenFolder, 30d);
        ToolTipService.SetHorizontalOffset(btnOpenFolder, 2d);
        btnOpenFolder.Click += (_, _) => PageInstanceOverall.OpenVersionFolder(version);
        var btnDel = new MyIconButton { LogoScale = 1.1d, Logo = Icon.IconButtonDelete };
        btnDel.ToolTip = Lang.Text("Common.Action.Delete");
        ToolTipService.SetPlacement(btnDel, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnDel, 30d);
        ToolTipService.SetHorizontalOffset(btnDel, 2d);
        btnDel.Click += (_, _) => DeleteVersion(sender, version);
        if (version.state != ModMinecraft.McInstanceState.Error)
        {
            var btnCont = new MyIconButton { LogoScale = 1.1d, Logo = Icon.IconButtonSetup };
            btnCont.ToolTip = Lang.Text("Select.Instance.Settings");
            ToolTipService.SetPlacement(btnCont, PlacementMode.Center);
            ToolTipService.SetVerticalOffset(btnCont, 30d);
            ToolTipService.SetHorizontalOffset(btnCont, 2d);
            btnCont.Click += (_, _) =>
            {
                PageInstanceLeft.instance = version;
                ModMain.frmMain.PageChange(FormMain.PageType.InstanceSetup);
            };
            sender.MouseRightButtonUp += (_, _) =>
            {
                PageInstanceLeft.instance = version;
                ModMain.frmMain.PageChange(FormMain.PageType.InstanceSetup);
            };
            sender.Buttons = new[] { btnStar, btnOpenFolder, btnDel, btnCont };
        }
        else
        {
            var btnCont = new MyIconButton { LogoScale = 1.15d, Logo = Icon.IconButtonOpen };
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
        var instance = (ModMinecraft.McInstance)sender.Tag;
        if (new ModMinecraft.McInstance(instance.PathInstance).Check())
        {
            // 正常实例
            ModMinecraft.McInstanceSelected = instance;
            States.Game.SelectedInstance = ModMinecraft.McInstanceSelected.Name;
            ModMain.frmMain.PageBack();
        }
        else
        {
            // 错误实例
            PageInstanceOverall.OpenVersionFolder(instance);
        }
    }

    private void BtnDownload_Click(object sender, MouseButtonEventArgs e)
    {
        ModMain.frmMain.PageChange(FormMain.PageType.Download, FormMain.PageSubType.DownloadInstall);
    }

    // 修改此代码时，同时修改 PageInstanceOverall 中的代码
    public static void DeleteVersion(MyListItem item, ModMinecraft.McInstance instance)
    {
        try
        {
            var isShiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
            var isHintIndie = instance.state != ModMinecraft.McInstanceState.Error &&
                              (instance.PathIndie ?? "") != (ModMinecraft.mcFolderSelected ?? "");
            var confirmMsg = isShiftPressed
                ? Lang.Text("Select.Instance.Delete.ConfirmPermanentMessage", instance.Name)
                : Lang.Text("Select.Instance.Delete.ConfirmMessage", instance.Name);
            var confirmFullMsg = confirmMsg +
                                 (isHintIndie ? "\r\n" + Lang.Text("Select.Instance.Delete.IsolatedWarning") : "");
            switch (ModMain.MyMsgBox(confirmFullMsg, Lang.Text("Select.Instance.Delete.ConfirmTitle"),
                        Button2: Lang.Text("Common.Action.Cancel"), IsWarn: true))
            {
                case 1:
                {
                    ModBase.IniClearCache(Path.Combine(instance.PathIndie, "options.txt"));
                    ((DynamicCacheConfigStorage)ConfigService.GetProvider(ConfigSource.GameInstance)).InvalidateCache(
                        instance.PathInstance);
                    if (isShiftPressed)
                    {
                        ModBase.DeleteDirectory(instance.PathInstance);
                        ModMain.Hint(Lang.Text("Select.Instance.Delete.PermanentSuccess", instance.Name),
                            ModMain.HintType.Finish);
                    }
                    else
                    {
                        FileSystem.DeleteDirectory(instance.PathInstance, UIOption.AllDialogs,
                            RecycleOption.SendToRecycleBin);
                        ModMain.Hint(Lang.Text("Select.Instance.Delete.RecycleBinSuccess", instance.Name),
                            ModMain.HintType.Finish);
                    }

                    break;
                }
                case 2:
                {
                    return;
                }
            }

            // 从 UI 中移除
            if (instance.displayType == ModMinecraft.McInstanceCardType.Hidden || !instance.isStar)
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
                    if (ModMinecraft.McInstanceSelected is not null && (instance.PathInstance ?? "") ==
                        (ModMinecraft.McInstanceSelected.PathInstance ?? ""))
                        // 删除当前实例就更改选择
                        ModMinecraft.McInstanceSelected = (ModMinecraft.McInstance)((MyListItem)parent.Children[0]).Tag;
                    ModLoader.LoaderFolderRun(ModMinecraft.mcInstanceListLoader, ModMinecraft.mcFolderSelected,
                        ModLoader.LoaderFolderRunType.UpdateOnly, 1, @"versions\");
                }
                else
                {
                    // 删除后没剩了
                    ModLoader.LoaderFolderRun(ModMinecraft.mcInstanceListLoader, ModMinecraft.mcFolderSelected,
                        ModLoader.LoaderFolderRunType.ForceRun, 1, @"versions\");
                }
            }
            else
            {
                // 同时出现在当前卡片与收藏夹
                ModLoader.LoaderFolderRun(ModMinecraft.mcInstanceListLoader, ModMinecraft.mcFolderSelected,
                    ModLoader.LoaderFolderRunType.ForceRun, 1, @"versions\");
            }
        }
        catch (OperationCanceledException ex)
        {
            ModBase.Log(ex, $"删除实例 {instance.Name} 被主动取消");
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, Lang.Text("Select.Instance.Error.Delete", instance.Name), ModBase.LogLevel.Msgbox);
        }
    }

    public void BtnEmptyDownload_Loaded()
    {
        var newVisibility = (Config.Preference.Hide.PageDownload && !PageSetupUI.HiddenForceShow) || showHidden
            ? Visibility.Collapsed
            : Visibility.Visible;
        if (BtnEmptyDownload.Visibility != newVisibility)
        {
            BtnEmptyDownload.Visibility = newVisibility;
            PanLoad.TriggerForceResize();
        }
    }

    #endregion
}
