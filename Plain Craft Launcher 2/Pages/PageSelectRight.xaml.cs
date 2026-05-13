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
                        CardName = "常规实例";
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
                            CardName = "可安装 Mod";
                        else if (IsForgeExists)
                            CardName = "Forge 实例";
                        else if (IsNeoForgeExists)
                            CardName = "NeoForge 实例";
                        else if (IsCleanroomExists)
                            CardName = "Cleanroom 实例";
                        else if (IsLabyModExists)
                            CardName = "LabyMod 实例";
                        else if (IsLiteExists)
                            CardName = "LiteLoader 实例";
                        else if (IsQuiltExists)
                            CardName = "Quilt 实例";
                        else
                            CardName = "Fabric 实例";

                        break;
                    }
                    case ModMinecraft.McInstanceCardType.Error:
                    {
                        CardName = "错误的实例";
                        break;
                    }
                    case ModMinecraft.McInstanceCardType.Hidden:
                    {
                        CardName = "隐藏的实例";
                        break;
                    }
                    case ModMinecraft.McInstanceCardType.Rubbish:
                    {
                        CardName = "不常用实例";
                        break;
                    }
                    case ModMinecraft.McInstanceCardType.Star:
                    {
                        CardName = "收藏夹";
                        break;
                    }
                    case ModMinecraft.McInstanceCardType.Fool:
                    {
                        CardName = "愚人节版本";
                        break;
                    }

                    default:
                    {
                        throw new ArgumentException($"未知的卡片种类（{(int)Card.Key}）");
                    }
                }

                #endregion

                // 建立控件
                var CardTitle = $"{CardName}{(CardName == "收藏夹" ? "" : $" ({filteredInstances.Count})")}";
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
                        LabEmptyTitle.Text = "无隐藏实例";
                        LabEmptyContent.Text = """
                                               没有实例被隐藏，你可以在实例设置的实例分类选项中隐藏实例。
                                               再次按下 F11 即可退出隐藏实例查看模式。
                                               """;
                        BtnEmptyDownload.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        LabEmptyTitle.Text = "无可用实例";
                        LabEmptyContent.Text = """
                                               未找到任何游戏实例，请先下载一个游戏实例。
                                               若有已存在的实例，请在左边的列表中选择添加文件夹，选择 .minecraft 文件夹将其导入。
                                               """;
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
                    LabEmptySearchTitle.Text = "无匹配的隐藏实例";
                    LabEmptySearchContent.Text = string.IsNullOrWhiteSpace(searchText)
                        ? "请输入搜索内容"
                        : $"没有找到与 '{searchText}' 匹配的隐藏实例";
                }
                else if (ShowHidden)
                {
                    // 无隐藏实例 - 显示"无隐藏实例"提示
                    PanEmpty.Visibility = Visibility.Visible;
                    PanBack.Visibility = Visibility.Collapsed;
                    LabEmptyTitle.Text = "无隐藏实例";
                    LabEmptyContent.Text =
                        """
                        没有实例被隐藏，你可以在实例设置的实例分类选项中隐藏实例。
                        再次按下 F11 即可退出隐藏实例查看模式。
                        """;
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
                    LabEmptySearchTitle.Text = "无匹配的游戏实例";
                    LabEmptySearchContent.Text = string.IsNullOrWhiteSpace(searchText)
                        ? "请输入搜索内容"
                        : $"没有找到与 '{searchText}' 匹配的实例";
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
            ModBase.Log(ex, "将实例列表转换显示时失败", ModBase.LogLevel.Feedback);
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
            ModBase.Log(ex, "加载实例图标失败", ModBase.LogLevel.Hint);
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
            BtnStar.ToolTip = "取消收藏";
            ToolTipService.SetPlacement(BtnStar, PlacementMode.Center);
            ToolTipService.SetVerticalOffset(BtnStar, 30d);
            ToolTipService.SetHorizontalOffset(BtnStar, 2d);
            BtnStar.LogoScale = 1.1d;
            BtnStar.Logo = ModBase.Logo.IconButtonLikeFill;
        }
        else
        {
            BtnStar.ToolTip = "收藏";
            ToolTipService.SetPlacement(BtnStar, PlacementMode.Center);
            ToolTipService.SetVerticalOffset(BtnStar, 30d);
            ToolTipService.SetHorizontalOffset(BtnStar, 2d);
            BtnStar.LogoScale = 1.1d;
            BtnStar.Logo = ModBase.Logo.IconButtonLikeLine;
        }

        BtnStar.Click += (_, _) =>
        {
            States.Instance.Starred[Version.PathInstance] = !Version.IsStar;
            ModMinecraft.McInstanceListForceRefresh = true;
            ModLoader.LoaderFolderRun(ModMinecraft.McInstanceListLoader, ModMinecraft.McFolderSelected,
                ModLoader.LoaderFolderRunType.ForceRun, 1, @"versions\");
        };
        var BtnOpenFolder = new MyIconButton { LogoScale = 1.1d, Logo = ModBase.Logo.IconButtonOpen };
        BtnOpenFolder.ToolTip = "打开实例目录";
        ToolTipService.SetPlacement(BtnOpenFolder, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(BtnOpenFolder, 30d);
        ToolTipService.SetHorizontalOffset(BtnOpenFolder, 2d);
        BtnOpenFolder.Click += (_, _) => PageInstanceOverall.OpenVersionFolder(Version);
        var BtnDel = new MyIconButton { LogoScale = 1.1d, Logo = ModBase.Logo.IconButtonDelete };
        BtnDel.ToolTip = "删除";
        ToolTipService.SetPlacement(BtnDel, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(BtnDel, 30d);
        ToolTipService.SetHorizontalOffset(BtnDel, 2d);
        BtnDel.Click += (_, _) => DeleteVersion(sender, Version);
        if (Version.State != ModMinecraft.McInstanceState.Error)
        {
            var BtnCont = new MyIconButton { LogoScale = 1.1d, Logo = ModBase.Logo.IconButtonSetup };
            BtnCont.ToolTip = "设置";
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
            var BtnCont = new MyIconButton { LogoScale = 1.15d, Logo = ModBase.Logo.IconButtonOpen };
            BtnCont.ToolTip = "打开文件夹";
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
            switch (ModMain.MyMsgBox(
                        $"""
                         你确定要{(IsShiftPressed ? "永久" : "")}删除实例 {instance.Name} 吗？{(IsHintIndie
                             ? "\r\n由于该实例开启了版本隔离，删除时该实例对应的存档、资源包、Mod 等文件也将被一并删除！"
                             : "")}
                         """, "实例删除确认", Button2: "取消", IsWarn: true))
            {
                case 1:
                {
                    ModBase.IniClearCache(Path.Combine(instance.PathIndie, "options.txt"));
                    ((DynamicCacheConfigStorage)ConfigService.GetProvider(ConfigSource.GameInstance)).InvalidateCache(
                        instance.PathInstance);
                    if (IsShiftPressed)
                    {
                        ModBase.DeleteDirectory(instance.PathInstance);
                        ModMain.Hint($"实例 {instance.Name} 已永久删除！", ModMain.HintType.Finish);
                    }
                    else
                    {
                        FileSystem.DeleteDirectory(instance.PathInstance, UIOption.AllDialogs,
                            RecycleOption.SendToRecycleBin);
                        ModMain.Hint($"实例 {instance.Name} 已删除到回收站！", ModMain.HintType.Finish);
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
                    Card.Title = Card.Title.Replace((Parent.Children.Count - 1).ToString(),
                        (Parent.Children.Count - 2).ToString()); // 有一个占位符
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
            ModBase.Log(ex, $"删除实例 {instance.Name} 失败", ModBase.LogLevel.Msgbox);
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
