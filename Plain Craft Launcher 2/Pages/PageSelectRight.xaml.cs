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
    private const int NormalDelay = 75; // 正常输入延迟0.075秒
    private const int QuickDelay = 50; // 清空搜索框延迟0.05秒
    private bool IsRefreshing;

    private DateTime LastInputTime = DateTime.MinValue;
    private DispatcherTimer ReloadTimer;

    // 窗口属性
    /// <summary>
    ///     是否显示隐藏的 Minecraft 实例。
    /// </summary>
    public bool ShowHidden = false;

    public PageSelectRight()
    {
        InitializeComponent();
        Load.Text = Lang.Text("Select.Instance.Loading");
        PanVerSearchBox.HintText = Lang.Text("Select.Instance.Search.Hint");
        Loaded += PageSelectRight_Loaded;
        Unloaded += PageSelectRight_Unloaded;
        LoaderInit();
    }

    // 窗口基础
    private void PageSelectRight_Loaded(object sender, RoutedEventArgs e)
    {
        ModLoader.LoaderFolderRun(ModMinecraft.McInstanceListLoader, ModMinecraft.McFolderSelected,
            ModLoader.LoaderFolderRunType.RunOnUpdated, 1, @"versions\");
        PanBack.ScrollToHome();
        PanVerSearchBox.TextChanged += (a, b) => PanVerSearchBox_TextChanged(a, (TextChangedEventArgs)b);

        ReloadTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(NormalDelay) };
        ReloadTimer.Tick += ReloadTimer_Tick;
    }

    private void PanVerSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        // 记录最后一次输入时间
        LastInputTime = DateTime.Now;

        IsRefreshing = false;

        // 动态调整延迟时间
        if (string.IsNullOrWhiteSpace(PanVerSearchBox.Text))
        {
            if (ReloadTimer.Interval.TotalMilliseconds != QuickDelay)
                ReloadTimer.Interval = TimeSpan.FromMilliseconds(QuickDelay);
        }
        else if (ReloadTimer.Interval.TotalMilliseconds != NormalDelay)
        {
            ReloadTimer.Interval = TimeSpan.FromMilliseconds(NormalDelay);
        }


        if (!ReloadTimer.IsEnabled) ReloadTimer.Start();
    }

    private void ReloadTimer_Tick(object sender, EventArgs e)
    {
        // 检查是否超过当前设定的延迟时间没有新输入
        var elapsed = (DateTime.Now - LastInputTime).TotalMilliseconds;
        var currentDelay = ReloadTimer.Interval.TotalMilliseconds;

        if (elapsed >= currentDelay && ModMinecraft.McInstanceListLoader.State == ModBase.LoadState.Finished &&
            !IsRefreshing)
        {
            IsRefreshing = true;

            // 确保在UI线程执行刷新
            Dispatcher.BeginInvoke(new Action(() =>
            {
                McInstanceListUI(ModMinecraft.McInstanceListLoader);
                IsRefreshing = false;
            }));
            ReloadTimer.Stop();
        }
    }

    private void PageSelectRight_Unloaded(object sender, RoutedEventArgs e)
    {
        // 清理计时器
        if (ReloadTimer is not null)
        {
            ReloadTimer.Stop();
            ReloadTimer.Tick -= ReloadTimer_Tick;
            ReloadTimer = null;
        }
    }

    private void LoaderInit()
    {
        PageLoaderInit(Load, PanLoad, PanAllBack, null, ModMinecraft.McInstanceListLoader,
            a => this.McInstanceListUI((ModLoader.LoaderTask<string, int>)a),
            AutoRun: false);
    }

    private void Load_Click(object sender, MouseButtonEventArgs e)
    {
        if (ModMinecraft.McInstanceListLoader.State == ModBase.LoadState.Failed)
            ModLoader.LoaderFolderRun(ModMinecraft.McInstanceListLoader, ModMinecraft.McFolderSelected,
                ModLoader.LoaderFolderRunType.ForceRun, 1, @"versions\");
    }

    #region 结果 UI 化

    private void McInstanceListUI(ModLoader.LoaderTask<string, int> Loader)
    {
        try
        {
            var Path = Loader.Input;
            // 加载 UI
            PanMain.Children.Clear();

            var hasVisibleFolders = false;
            var searchText = PanVerSearchBox.Text.Trim().ToLower(); // 获取搜索框文本
            var hasAnyResults = false;
            var originalHasInstances = ModMinecraft.McInstanceList.ToArray().Any(c => c.Value.Count > 0);

            // 搜索无结果时显示 PanEmptySearch
            PanEmptySearch.Visibility = Visibility.Collapsed; // 默认隐藏

            foreach (var Card in ModMinecraft.McInstanceList.ToArray())
            {
                if ((Card.Key == ModMinecraft.McInstanceCardType.Hidden) ^ ShowHidden)
                    continue;
                var filteredInstances = Card.Value.Where(v =>
                {
                    if (string.IsNullOrEmpty(searchText))
                        return true;
                    return v.Name.ToLower().Contains(searchText) ||
                           (v.Desc is not null && v.Desc.ToLower().Contains(searchText)) || v.GetDefaultDescription()
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

                var CardName = "";
                switch (Card.Key)
                {
                    case ModMinecraft.McInstanceCardType.OriginalLike:
                    {
                        CardName = Lang.Text("Select.Instance.Card.Regular");
                        break;
                    }
                    case ModMinecraft.McInstanceCardType.API:
                    {
                        var IsForgeExists = false;
                        var IsNeoForgeExists = false;
                        var IsFabricExists = false;
                        var IsQuiltExists = false;
                        var IsLiteExists = false;
                        var IsCleanroomExists = false;
                        var IsLabyModExists = false;
                        foreach (var instance in Card.Value)
                        {
                            if (!instance.IsLoaded)
                                instance.Load();
                            if (instance.Info.HasFabric)
                                IsFabricExists = true;
                            if (instance.Info.HasQuilt)
                                IsQuiltExists = true;
                            if (instance.Info.HasLiteLoader)
                                IsLiteExists = true;
                            if (instance.Info.HasForge)
                                IsForgeExists = true;
                            if (instance.Info.HasNeoForge)
                                IsNeoForgeExists = true;
                            if (instance.Info.HasCleanroom)
                                IsCleanroomExists = true;
                            if (instance.Info.HasLabyMod)
                                IsLabyModExists = true;
                        }

                        if ((IsLiteExists ? 1 : 0) + (IsForgeExists ? 1 : 0) + (IsFabricExists ? 1 : 0) +
                            (IsNeoForgeExists ? 1 : 0) + (IsQuiltExists ? 1 : 0) + (IsCleanroomExists ? 1 : 0) +
                            (IsLabyModExists ? 1 : 0) > 1)
                            CardName = Lang.Text("Select.Instance.Card.Modable");
                        else if (IsForgeExists)
                            CardName = Lang.Text("Select.Instance.Card.Forge");
                        else if (IsNeoForgeExists)
                            CardName = Lang.Text("Select.Instance.Card.NeoForge");
                        else if (IsCleanroomExists)
                            CardName = Lang.Text("Select.Instance.Card.Cleanroom");
                        else if (IsLabyModExists)
                            CardName = Lang.Text("Select.Instance.Card.LabyMod");
                        else if (IsLiteExists)
                            CardName = Lang.Text("Select.Instance.Card.LiteLoader");
                        else if (IsQuiltExists)
                            CardName = Lang.Text("Select.Instance.Card.Quilt");
                        else
                            CardName = Lang.Text("Select.Instance.Card.Fabric");

                        break;
                    }
                    case ModMinecraft.McInstanceCardType.Error:
                    {
                        CardName = Lang.Text("Select.Instance.Card.Error");
                        break;
                    }
                    case ModMinecraft.McInstanceCardType.Hidden:
                    {
                        CardName = Lang.Text("Select.Instance.Card.Hidden");
                        break;
                    }
                    case ModMinecraft.McInstanceCardType.Rubbish:
                    {
                        CardName = Lang.Text("Select.Instance.Card.LessUsed");
                        break;
                    }
                    case ModMinecraft.McInstanceCardType.Star:
                    {
                        CardName = Lang.Text("Select.Instance.Card.Favorites");
                        break;
                    }
                    case ModMinecraft.McInstanceCardType.Fool:
                    {
                        CardName = Lang.Text("Select.Instance.Card.AprilFools");
                        break;
                    }

                    default:
                    {
                        throw new ArgumentException($"未知的卡片种类（{(int)Card.Key}）");
                    }
                }

                #endregion

                // 建立控件
                var CardTitle = $"{CardName}{(Card.Key == ModMinecraft.McInstanceCardType.Star ? "" : $" ({Lang.Number(filteredInstances.Count, "N0")})")}";
                var NewCard = new MyCard { Title = CardTitle, Margin = new Thickness(0d, 0d, 0d, 15d) };
                var NewStack = new StackPanel
                {
                    Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d),
                    VerticalAlignment = VerticalAlignment.Top, RenderTransform = new TranslateTransform(0d, 0d),
                    Tag = filteredInstances
                };
                NewCard.Children.Add(NewStack);
                NewCard.SwapControl = NewStack;
                PanMain.Children.Add(NewCard);

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
                    NewCard.IsSwapped = true;
                    NewCard.InstallMethod = PutMethod;
                }
                else
                {
                    MyCard.StackInstall(ref NewStack, PutMethod);
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
                    if (ShowHidden)
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
                else if (ShowHidden && ModMinecraft.McInstanceList.ToArray().Any(c =>
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
                else if (ShowHidden)
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
        var NewItem = new MyListItem
        {
            Title = instance.Name, Info = instance.Desc, Height = 42d, Tag = instance, SnapsToDevicePixels = true,
            Type = MyListItem.CheckType.Clickable
        };
        var instanceInfo = instance.Info;
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
        NewItem.Tags = tags;
        try
        {
            if (instance.Logo.EndsWith(@"PCL\Logo.png"))
                NewItem.Logo = instance.PathInstance + @"PCL\Logo.png"; // 修复老版本中，存储的自定义 Logo 使用完整路径，导致移动后无法加载的 Bug
            else
                NewItem.Logo = instance.Logo;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, Lang.Text("Select.Instance.Error.IconLoad"), ModBase.LogLevel.Hint);
            NewItem.Logo = "pack://application:,,,/images/Blocks/RedstoneBlock.png";
        }

        NewItem.ContentHandler = McVersionListContent;
        return NewItem;
    }

    private static void McVersionListContent(MyListItem sender, EventArgs e)
    {
        var Version = (ModMinecraft.McInstance)sender.Tag;
        // 注册点击事件
        sender.Click += (a, b) => Item_Click((MyListItem)a, b);
        // 图标按钮
        var BtnStar = new MyIconButton();
        if (Version.IsStar)
        {
            BtnStar.ToolTip = Lang.Text("Select.Instance.Unfavorite");
            ToolTipService.SetPlacement(BtnStar, PlacementMode.Center);
            ToolTipService.SetVerticalOffset(BtnStar, 30d);
            ToolTipService.SetHorizontalOffset(BtnStar, 2d);
            BtnStar.LogoScale = 1.1d;
            BtnStar.Logo = Icon.IconButtonLikeFill;
        }
        else
        {
            BtnStar.ToolTip = Lang.Text("Select.Instance.Favorite");
            ToolTipService.SetPlacement(BtnStar, PlacementMode.Center);
            ToolTipService.SetVerticalOffset(BtnStar, 30d);
            ToolTipService.SetHorizontalOffset(BtnStar, 2d);
            BtnStar.LogoScale = 1.1d;
            BtnStar.Logo = Icon.IconButtonLikeLine;
        }

        BtnStar.Click += (_, _) =>
        {
            States.Instance.Starred[Version.PathInstance] = !Version.IsStar;
            ModMinecraft.McInstanceListForceRefresh = true;
            ModLoader.LoaderFolderRun(ModMinecraft.McInstanceListLoader, ModMinecraft.McFolderSelected,
                ModLoader.LoaderFolderRunType.ForceRun, 1, @"versions\");
        };
        var BtnOpenFolder = new MyIconButton { LogoScale = 1.1d, Logo = Icon.IconButtonOpen };
        BtnOpenFolder.ToolTip = Lang.Text("Select.Instance.OpenFolder");
        ToolTipService.SetPlacement(BtnOpenFolder, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(BtnOpenFolder, 30d);
        ToolTipService.SetHorizontalOffset(BtnOpenFolder, 2d);
        BtnOpenFolder.Click += (_, _) => PageInstanceOverall.OpenVersionFolder(Version);
        var BtnDel = new MyIconButton { LogoScale = 1.1d, Logo = Icon.IconButtonDelete };
        BtnDel.ToolTip = Lang.Text("Common.Action.Delete");
        ToolTipService.SetPlacement(BtnDel, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(BtnDel, 30d);
        ToolTipService.SetHorizontalOffset(BtnDel, 2d);
        BtnDel.Click += (_, _) => DeleteVersion(sender, Version);
        if (Version.State != ModMinecraft.McInstanceState.Error)
        {
            var BtnCont = new MyIconButton { LogoScale = 1.1d, Logo = Icon.IconButtonSetup };
            BtnCont.ToolTip = Lang.Text("Select.Instance.Settings");
            ToolTipService.SetPlacement(BtnCont, PlacementMode.Center);
            ToolTipService.SetVerticalOffset(BtnCont, 30d);
            ToolTipService.SetHorizontalOffset(BtnCont, 2d);
            BtnCont.Click += (_, _) =>
            {
                PageInstanceLeft.Instance = Version;
                ModMain.FrmMain.PageChange(FormMain.PageType.InstanceSetup);
            };
            sender.MouseRightButtonUp += (_, _) =>
            {
                PageInstanceLeft.Instance = Version;
                ModMain.FrmMain.PageChange(FormMain.PageType.InstanceSetup);
            };
            sender.Buttons = new[] { BtnStar, BtnOpenFolder, BtnDel, BtnCont };
        }
        else
        {
            var BtnCont = new MyIconButton { LogoScale = 1.15d, Logo = Icon.IconButtonOpen };
            BtnCont.ToolTip = Lang.Text("Common.Action.OpenFolder");
            ToolTipService.SetPlacement(BtnCont, PlacementMode.Center);
            ToolTipService.SetVerticalOffset(BtnCont, 30d);
            ToolTipService.SetHorizontalOffset(BtnCont, 2d);
            BtnCont.Click += (_, _) => PageInstanceOverall.OpenVersionFolder(Version);
            sender.MouseRightButtonUp += (_, _) => PageInstanceOverall.OpenVersionFolder(Version);
            sender.Buttons = new[] { BtnStar, BtnOpenFolder, BtnDel, BtnCont };
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
            ModMain.FrmMain.PageBack();
        }
        else
        {
            // 错误实例
            PageInstanceOverall.OpenVersionFolder(instance);
        }
    }

    private void BtnDownload_Click(object sender, MouseButtonEventArgs e)
    {
        ModMain.FrmMain.PageChange(FormMain.PageType.Download, FormMain.PageSubType.DownloadInstall);
    }

    // 修改此代码时，同时修改 PageInstanceOverall 中的代码
    public static void DeleteVersion(MyListItem item, ModMinecraft.McInstance instance)
    {
        try
        {
            var IsShiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
            var IsHintIndie = instance.State != ModMinecraft.McInstanceState.Error &&
                              (instance.PathIndie ?? "") != (ModMinecraft.McFolderSelected ?? "");
            var confirmMsg = IsShiftPressed
                ? Lang.Text("Select.Instance.Delete.ConfirmPermanentMessage", instance.Name)
                : Lang.Text("Select.Instance.Delete.ConfirmMessage", instance.Name);
            var confirmFullMsg = confirmMsg +
                                 (IsHintIndie ? "\r\n" + Lang.Text("Select.Instance.Delete.IsolatedWarning") : "");
            switch (ModMain.MyMsgBox(confirmFullMsg, Lang.Text("Select.Instance.Delete.ConfirmTitle"),
                        Button2: Lang.Text("Common.Action.Cancel"), IsWarn: true))
            {
                case 1:
                {
                    ModBase.IniClearCache(Path.Combine(instance.PathIndie, "options.txt"));
                    ((DynamicCacheConfigStorage)ConfigService.GetProvider(ConfigSource.GameInstance)).InvalidateCache(
                        instance.PathInstance);
                    if (IsShiftPressed)
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
            if (instance.DisplayType == ModMinecraft.McInstanceCardType.Hidden || !instance.IsStar)
            {
                // 仅出现在当前卡片
                var Parent = (StackPanel)item.Parent;
                if (Parent.Children.Count > 2) // 当前的项目与一个占位符
                {
                    // 删除后还有剩
                    var Card = (MyCard)Parent.Parent;
                    Card.Title = Card.Title.Replace(Lang.Number(Parent.Children.Count - 1, "N0"),
                        Lang.Number(Parent.Children.Count - 2, "N0")); // 有一个占位符
                    Parent.Children.Remove(item);
                    if (ModMinecraft.McInstanceSelected is not null && (instance.PathInstance ?? "") ==
                        (ModMinecraft.McInstanceSelected.PathInstance ?? ""))
                        // 删除当前实例就更改选择
                        ModMinecraft.McInstanceSelected = (ModMinecraft.McInstance)((MyListItem)Parent.Children[0]).Tag;
                    ModLoader.LoaderFolderRun(ModMinecraft.McInstanceListLoader, ModMinecraft.McFolderSelected,
                        ModLoader.LoaderFolderRunType.UpdateOnly, 1, @"versions\");
                }
                else
                {
                    // 删除后没剩了
                    ModLoader.LoaderFolderRun(ModMinecraft.McInstanceListLoader, ModMinecraft.McFolderSelected,
                        ModLoader.LoaderFolderRunType.ForceRun, 1, @"versions\");
                }
            }
            else
            {
                // 同时出现在当前卡片与收藏夹
                ModLoader.LoaderFolderRun(ModMinecraft.McInstanceListLoader, ModMinecraft.McFolderSelected,
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
        var NewVisibility = (Config.Preference.Hide.PageDownload && !PageSetupUI.HiddenForceShow) || ShowHidden
            ? Visibility.Collapsed
            : Visibility.Visible;
        if (BtnEmptyDownload.Visibility != NewVisibility)
        {
            BtnEmptyDownload.Visibility = NewVisibility;
            PanLoad.TriggerForceResize();
        }
    }

    #endregion
}
