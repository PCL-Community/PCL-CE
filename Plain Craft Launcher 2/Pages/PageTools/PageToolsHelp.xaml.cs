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
    private void HelpListLoad(ModLoader.LoaderTask<int, List<ModMain.HelpEntry>> loader)
    {
        try
        {
            // 初始化
            PanList.Children.Clear();
            PanBack.ScrollToHome();
            var helpItems = loader.output;
            // 获取全部分类
            var types = new List<string>();
            foreach (var Item in helpItems)
            foreach (var Type in Item.types)
                if (!types.Contains(Type))
                    types.Add(Type);

            // 将指南页面置顶
            if (types.Contains("指南"))
            {
                types.Remove("指南");
                types.Insert(0, "指南");
            }

            // 转化为 UI
            foreach (var Type in types)
            {
                // 确认所属该分类的项目
                var typeItems = new List<ModMain.HelpEntry>();
                foreach (var Item in helpItems)
                    if (Item.types.Contains(Type))
                        typeItems.Add(Item);
                // 增加卡片
                var newCard = new MyCard { Title = Type, Margin = new Thickness(0d, 0d, 0d, 15d) };
                var newStack = new StackPanel
                {
                    Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d),
                    VerticalAlignment = VerticalAlignment.Top, RenderTransform = new TranslateTransform(0d, 0d),
                    Tag = typeItems
                };
                newCard.Children.Add(newStack);
                newCard.SwapControl = newStack;

                void PutMethod(StackPanel stack)
                {
                    foreach (var item in (IEnumerable)stack.Tag)
                        stack.Children.Add(((ModMain.HelpEntry)item).ToListItem());
                }

                ;
                newCard.InstallMethod = PutMethod;
                if (Type == "指南")
                    MyCard.StackInstall(ref newStack, PutMethod);
                else
                    newCard.IsSwapped = true;
                PanList.Children.Add(newCard);
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
    public static void OnItemClick(ModMain.HelpEntry entry)
    {
        try
        {
            if (entry.isEvent)
                CustomEvent.Raise(Enum.Parse<CustomEvent.EventType>(entry.eventType), entry.eventData);
            else
                EnterHelpPage(entry);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "处理帮助项目点击时发生意外错误", ModBase.LogLevel.Feedback);
        }
    }

    public static void EnterHelpPage(string location)
    {
        ModBase.RunInThread(() =>
        {
            if (ModMain.helpLoader.State != ModBase.LoadState.Finished)
                ModMain.helpLoader.WaitForExit(ModBase.GetUuid());
            var entry = new ModMain.HelpEntry(location);
            ModBase.RunInUi(() =>
            {
                var frmHelpDetail = new PageOtherHelpDetail();
                if (frmHelpDetail.Init(entry))
                    ModMain.frmMain.PageChange(new FormMain.PageStackData
                        { page = FormMain.PageType.HelpDetail, additional = (null, null, null, ModComp.CompLoaderType.Any, ModComp.CompType.Any, entry, frmHelpDetail, null) });
                else
                    ModBase.Log("[Help] 已取消进入帮助项目，这一般是由于 xaml 初始化失败，且用户在弹窗中手动放弃", ModBase.LogLevel.Debug);
            });
        });
    }

    public static void EnterHelpPage(ModMain.HelpEntry entry)
    {
        ModBase.RunInThread(() =>
        {
            if (ModMain.helpLoader.State != ModBase.LoadState.Finished)
                ModMain.helpLoader.WaitForExit(ModBase.GetUuid());
            ModBase.RunInUi(() =>
            {
                var frmHelpDetail = new PageOtherHelpDetail();
                if (frmHelpDetail.Init(entry))
                    ModMain.frmMain.PageChange(new FormMain.PageStackData
                        { page = FormMain.PageType.HelpDetail, additional = (null, null, null, ModComp.CompLoaderType.Any, ModComp.CompType.Any, entry, frmHelpDetail, null) });
                else
                    ModBase.Log("[Help] 已取消进入帮助项目，这一般是由于 xaml 初始化失败，且用户在弹窗中手动放弃", ModBase.LogLevel.Debug);
            });
        });
    }

    public static PageOtherHelpDetail GetHelpPage(string location)
    {
        if (ModMain.helpLoader.State != ModBase.LoadState.Finished)
            ModMain.helpLoader.WaitForExit(ModBase.GetUuid());
        var frmHelpDetail = new PageOtherHelpDetail();
        if (frmHelpDetail.Init(new ModMain.HelpEntry(location))) return frmHelpDetail;

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
                }, after: true),
                ModAnimation.AaOpacity(PanList, 1d - PanList.Opacity, 150, 30)
            }, "FrmOtherHelp Search Switch");
        }
        else
        {
            // 构造请求
            var queryList = new List<ModBase.SearchEntry<ModMain.HelpEntry>>();
            foreach (var Entry in ModMain.helpLoader.output)
            {
                if (!Entry.showInSearch || (ModBase.Val(ModBase.versionBranchCode) == 50d && !Entry.showInPublic))
                    continue;
                if (!Entry.showInSearch || (ModBase.Val(ModBase.versionBranchCode) != 50d && !Entry.showInSnapshot))
                    continue;
                queryList.Add(new ModBase.SearchEntry<ModMain.HelpEntry>
                {
                    item = Entry,
                    searchSource = new List<ModBase.SearchSource>
                        { new(Entry.title, 1d), new(Entry.desc, 0.5d), new(Entry.search, 1.5d) }
                });
                // New KeyValuePair(Of String, Double)(If(Entry.IsEvent, If(Entry.EventData, ""), Entry.XamlContent), 0.2)
            }

            // 进行搜索，构造列表
            var searchResult = ModBase.Search(queryList, SearchBox.Text, 5, 0.08d);
            PanSearchList.Children.Clear();
            if (!searchResult.Any())
            {
                PanSearch.Title = "无搜索结果";
                PanSearchList.Visibility = Visibility.Collapsed;
            }
            else
            {
                PanSearch.Title = "搜索结果";
                foreach (var Result in searchResult)
                {
                    var item = Result.item.ToListItem();
                    if (ModBase.modeDebug)
                        item.Info = (Result.absoluteRight ? "完全匹配，" : "") + "相似度：" +
                                    Lang.Number(Math.Round(Result.similarity, 3), "N3") + "，" + item.Info;
                    PanSearchList.Children.Add(item);
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
                }, after: true),
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
        PageLoaderInit(Load, PanLoad, PanBack, null, ModMain.helpLoader,
            a => this.HelpListLoad((ModLoader.LoaderTask<int, List<ModMain.HelpEntry>>)a));
    }

    #endregion
}
