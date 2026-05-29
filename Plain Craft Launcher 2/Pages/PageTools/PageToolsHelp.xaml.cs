using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageToolsHelp : IRefreshable
{
    public PageToolsHelp()
    {
        Initialized += PageOther_Inited;
        Loaded += PageOther_Loaded;
        InitializeComponent();
    }

    public void Refresh()
    {
        PageToolsLeft.RefreshHelp();
    }

    /// <summary>
    ///     将帮助列表对象实例化为主页 UI。
    /// </summary>
    private void HelpListLoad(ModLoader.LoaderTask<int, List<ModMain.HelpEntry>> Loader)
    {
        try
        {
            // 初始化
            PanList.Children.Clear();
            PanBack.ScrollToHome();
            var HelpItems = Loader.Output;
            // 获取全部分类
            var Types = new List<string>();
            foreach (var Item in HelpItems)
            foreach (var Type in Item.Types)
                if (!Types.Contains(Type))
                    Types.Add(Type);

            // 将指南页面置顶
            if (Types.Contains("指南"))
            {
                Types.Remove("指南");
                Types.Insert(0, "指南");
            }

            // 转化为 UI
            foreach (var Type in Types)
            {
                // 确认所属该分类的项目
                var TypeItems = new List<ModMain.HelpEntry>();
                foreach (var Item in HelpItems)
                    if (Item.Types.Contains(Type))
                        TypeItems.Add(Item);
                // 增加卡片
                var NewCard = new MyCard { Title = Type, Margin = new Thickness(0d, 0d, 0d, 15d) };
                var NewStack = new StackPanel
                {
                    Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d),
                    VerticalAlignment = VerticalAlignment.Top, RenderTransform = new TranslateTransform(0d, 0d),
                    Tag = TypeItems
                };
                NewCard.Children.Add(NewStack);
                NewCard.SwapControl = NewStack;

                void PutMethod(StackPanel Stack)
                {
                    foreach (var item in (IEnumerable)Stack.Tag)
                        Stack.Children.Add(((ModMain.HelpEntry)item).ToListItem());
                }

                ;
                NewCard.InstallMethod = PutMethod;
                if (Type == "指南")
                    MyCard.StackInstall(ref NewStack, PutMethod);
                else
                    NewCard.IsSwapped = true;
                PanList.Children.Add(NewCard);
            }
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, "加载帮助列表 UI 失败", ModBase.LogLevel.Feedback);
        }
    }

    /// <summary>
    ///     帮助项目的点击事件。
    /// </summary>
    public static void OnItemClick(ModMain.HelpEntry Entry)
    {
        try
        {
            if (Entry.IsEvent)
                CustomEvent.Raise(Enum.Parse<CustomEvent.EventType>(Entry.EventType), Entry.EventData);
            else
                EnterHelpPage(Entry);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "处理帮助项目点击时发生意外错误", ModBase.LogLevel.Feedback);
        }
    }

    public static void EnterHelpPage(string Location)
    {
        ModBase.RunInThread(() =>
        {
            if (ModMain.HelpLoader.State != ModBase.LoadState.Finished)
                ModMain.HelpLoader.WaitForExit(ModBase.GetUuid());
            var Entry = new ModMain.HelpEntry(Location);
            ModBase.RunInUi(() =>
            {
                var FrmHelpDetail = new PageOtherHelpDetail();
                if (FrmHelpDetail.Init(Entry))
                    ModMain.FrmMain.PageChange(new FormMain.PageStackData
                        { Page = FormMain.PageType.HelpDetail, Additional = (null, null, null, ModComp.CompLoaderType.Any, ModComp.CompType.Any, Entry, FrmHelpDetail, null) });
                else
                    ModBase.Log("[Help] 已取消进入帮助项目，这一般是由于 xaml 初始化失败，且用户在弹窗中手动放弃", ModBase.LogLevel.Debug);
            });
        });
    }

    public static void EnterHelpPage(ModMain.HelpEntry Entry)
    {
        ModBase.RunInThread(() =>
        {
            if (ModMain.HelpLoader.State != ModBase.LoadState.Finished)
                ModMain.HelpLoader.WaitForExit(ModBase.GetUuid());
            ModBase.RunInUi(() =>
            {
                var FrmHelpDetail = new PageOtherHelpDetail();
                if (FrmHelpDetail.Init(Entry))
                    ModMain.FrmMain.PageChange(new FormMain.PageStackData
                        { Page = FormMain.PageType.HelpDetail, Additional = (null, null, null, ModComp.CompLoaderType.Any, ModComp.CompType.Any, Entry, FrmHelpDetail, null) });
                else
                    ModBase.Log("[Help] 已取消进入帮助项目，这一般是由于 xaml 初始化失败，且用户在弹窗中手动放弃", ModBase.LogLevel.Debug);
            });
        });
    }

    public static PageOtherHelpDetail GetHelpPage(string Location)
    {
        if (ModMain.HelpLoader.State != ModBase.LoadState.Finished)
            ModMain.HelpLoader.WaitForExit(ModBase.GetUuid());
        var FrmHelpDetail = new PageOtherHelpDetail();
        if (FrmHelpDetail.Init(new ModMain.HelpEntry(Location))) return FrmHelpDetail;

        throw new Exception("已取消进入帮助项目，这一般是由于 xaml 初始化失败，且用户在弹窗中手动放弃");
    }

    /// <summary>
    ///     搜索帮助。
    /// </summary>
    public void SearchRun(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SearchBox.Text))
        {
            // 隐藏
            ModAnimation.AniStart(new[]
            {
                ModAnimation.AaOpacity(PanSearch, -PanSearch.Opacity, 100),
                ModAnimation.AaCode(() =>
                {
                    PanSearch.Height = 0d;
                    PanSearch.Visibility = Visibility.Collapsed;
                    PanList.Visibility = Visibility.Visible;
                }, After: true),
                ModAnimation.AaOpacity(PanList, 1d - PanList.Opacity, 150, 30)
            }, "FrmOtherHelp Search Switch");
        }
        else
        {
            // 构造请求
            var QueryList = new List<ModBase.SearchEntry<ModMain.HelpEntry>>();
            foreach (var Entry in ModMain.HelpLoader.Output)
            {
                if (!Entry.ShowInSearch || (ModBase.Val(ModBase.VersionBranchCode) == 50d && !Entry.ShowInPublic))
                    continue;
                if (!Entry.ShowInSearch || (ModBase.Val(ModBase.VersionBranchCode) != 50d && !Entry.ShowInSnapshot))
                    continue;
                QueryList.Add(new ModBase.SearchEntry<ModMain.HelpEntry>
                {
                    Item = Entry,
                    SearchSource = new List<ModBase.SearchSource>
                        { new(Entry.Title, 1d), new(Entry.Desc, 0.5d), new(Entry.Search, 1.5d) }
                });
                // New KeyValuePair(Of String, Double)(If(Entry.IsEvent, If(Entry.EventData, ""), Entry.XamlContent), 0.2)
            }

            // 进行搜索，构造列表
            var SearchResult = ModBase.Search(QueryList, SearchBox.Text, 5, 0.08d);
            PanSearchList.Children.Clear();
            if (!SearchResult.Any())
            {
                PanSearch.Title = "无搜索结果";
                PanSearchList.Visibility = Visibility.Collapsed;
            }
            else
            {
                PanSearch.Title = "搜索结果";
                foreach (var Result in SearchResult)
                {
                    var Item = Result.Item.ToListItem();
                    if (ModBase.ModeDebug)
                        Item.Info = (Result.AbsoluteRight ? "完全匹配，" : "") + "相似度：" +
                                    Lang.Number(Math.Round(Result.Similarity, 3), "N3") + "，" + Item.Info;
                    PanSearchList.Children.Add(Item);
                }

                PanSearchList.Visibility = Visibility.Visible;
            }

            // 显示
            ModAnimation.AniStart(new[]
            {
                ModAnimation.AaOpacity(PanList, -PanList.Opacity, 100),
                ModAnimation.AaCode(() =>
                {
                    PanList.Visibility = Visibility.Collapsed;
                    PanSearch.Visibility = Visibility.Visible;
                    PanSearch.TriggerForceResize();
                }, After: true),
                ModAnimation.AaOpacity(PanSearch, 1d - PanSearch.Opacity, 150, 30)
            }, "FrmOtherHelp Search Switch");
        }
    }

    #region 初始化

    // 滚动条
    private void PageOther_Loaded(object sender, RoutedEventArgs e)
    {
        PanBack.ScrollToHome();
    }

    // 初始化加载器信息
    private void PageOther_Inited(object sender, EventArgs e)
    {
        PageLoaderInit(Load, PanLoad, PanBack, null, ModMain.HelpLoader,
            a => this.HelpListLoad((ModLoader.LoaderTask<int, List<ModMain.HelpEntry>>)a));
    }

    #endregion
}
