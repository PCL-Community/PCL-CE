using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using PCL.Core.UI;
using PCL.Network;
using PCL.Network.Loaders;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageDownloadCompFavorites
{
    private readonly List<MyListItem> CompItemList = new();
    private List<MyListItem> SelectedItemList = new();

    public PageDownloadCompFavorites()
    {
        Loader = new ModLoader.LoaderTask<List<string>, List<ModComp.CompProject>>("CompProject Favorites",
            CompFavoritesGet, LoaderInput);
        Initialized += PageDownloadCompFavorites_Inited;
        Loaded += PageDownloadCompFavorites_Loaded;
        KeyDown += Page_KeyDown;
        InitializeComponent();
        {
            PanSearchBox.HintText = Lang.Text("Download.Comp.Favorites.Search.Hint");
            Load.Text = Lang.Text("Download.Comp.Favorites.Loading");
            // 这是选择收藏夹旁边那个图标按钮
            // 实在不想把布局写动态代码里，但是奈何龙猫的石山没办法在 XAML 里定义 Logo 属性为已有常量值
            // 还有一个很扯淡的点，同样自定义的 MyButton 能在 XAML 直接设置 Click 事件
            // 到 MyIconButton 就不行了，死活跑不了，也不知道是不是漏了什么依赖属性没写
            Btn_ManageTargetFav.Logo = Icon.IconButtonSetup;
            Btn_ManageTargetFav.Click += Manage_Click;
        }
        // Handles
        Load.StateChanged += Load_State;
        Btn_FavoritesCancel.Click += Btn_FavoritesCancel_Clicked;
        Btn_SelectCancel.Click += Btn_SelectCancel_Clicked;
        Btn_FavoritesShare.Click += Btn_FavoritesShare_Clicked;
        Btn_FavoritesDownload.Click += Btn_FavoritesDownload_Clicked;
        ComboTargetFav.SelectionChanged += ComboTargetFav_Selected;
        HintGetFail.MouseLeftButtonDown += HintGetFail_MouseLeftButtonDown;
        PanSearchBox.TextChanged += SearchRun;
    }

    private ModComp.CompFavorites.FavData CurrentFavTarget
    {
        get
        {
            var SelectedItem = (MyComboBoxItem)ComboTargetFav.SelectedItem;
            if (SelectedItem is null)
            {
                ModBase.Log("[Favorites] 异常：未选择收藏夹");
                SelectedItem = (MyComboBoxItem)ComboTargetFav.Items.GetItemAt(0);
            }

            return ModComp.CompFavorites.FavoritesList
                .First(e => string.Equals(e.Id, SelectedItem.Tag?.ToString(), StringComparison.OrdinalIgnoreCase));
        }
    }

    #region 加载器信息

    // 加载器信息
    public ModLoader.LoaderTask<List<string>, List<ModComp.CompProject>> Loader;

    private void PageDownloadCompFavorites_Inited(object sender, EventArgs e)
    {
        RefreshFavTargets();
        PageLoaderInit(Load, PanLoad, PanContent, null, Loader, _ => Load_OnFinish(), LoaderInput);
    }

    private void PageDownloadCompFavorites_Loaded(object sender, EventArgs e)
    {
        Items_SetSelectAll(false);
        RefreshBar();
        if (Loader.Input is not null && !Loader.Input.Count.Equals(CurrentFavTarget.Favs.Count)) RefreshFavTargets();
    }

    private List<string> LoaderInput()
    {
        List<string> TargetList = null;
        try
        {
            TargetList = CurrentFavTarget.Favs.Distinct().ToList();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[Favorites] 加载收藏夹列表时出错");
        }

        return (List<string>)TargetList.Clone(); // 复制而不是直接引用！
    }

    private void CompFavoritesGet(ModLoader.LoaderTask<List<string>, List<ModComp.CompProject>> Task)
    {
        Task.Output = ModComp.CompRequest.GetCompProjectsByIds(Task.Input);
    }

    #endregion

    #region UI 化 - 自适应卡片

    public class CompListItemContainer // 用来存储自动依据类型生成的卡片及其相关信息
    {
        public MyCard Card { get; set; }
        public StackPanel ContentList { get; set; }
        public string Title { get; set; }
        public int CompType { get; set; }
    }

    private readonly List<CompListItemContainer> ItemList = new();

    /// <summary>
    ///     刷新收藏夹列表
    /// </summary>
    private void RefreshFavTargets()
    {
        ComboTargetFav.Items.Clear();
        foreach (var Target in ModComp.CompFavorites.FavoritesList)
        {
            var Item = new MyComboBoxItem
            {
                Content = Target.Name,
                Tag = Target.Id
            };
            ComboTargetFav.Items.Add(Item);
        }

        if (ComboTargetFav.SelectedIndex == -1) ComboTargetFav.SelectedIndex = 0; // 默认选择第一个
    }

    /// <summary>
    ///     返回适合当前工程项目的卡片记录
    /// </summary>
    /// <param name="Type">工程项目类型</param>
    /// <returns></returns>
    private CompListItemContainer GetSuitListContainer(int Type)
    {
        if (ItemList.Any(e => e.CompType.Equals(Type))) return ItemList.First(e => e.CompType.Equals(Type));

        var NewItem = new CompListItemContainer
        {
            Card = new MyCard
            {
                CanSwap = true,
                Margin = new Thickness(0d, 0d, 0d, 15d)
            },
            ContentList = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(12d, 38d, 12d, 12d)
            },
            CompType = Type
        };
        switch (Type)
        {
            case -1:
            {
                NewItem.Title = Lang.Text("Download.Comp.Favorites.SearchResults.Title");
                break;
            }
            case (int)ModComp.CompType.Mod:
            {
                NewItem.Title = "Mod ({0})";
                break;
            }
            case (int)ModComp.CompType.ModPack:
            {
                NewItem.Title = $"{Lang.Text("Download.Comp.Type.Modpack")} ({{0}})";
                break;
            }
            case (int)ModComp.CompType.ResourcePack:
            {
                NewItem.Title = $"{Lang.Text("Download.Comp.Type.ResourcePack")} ({{0}})";
                break;
            }
            case (int)ModComp.CompType.Shader:
            {
                NewItem.Title = $"{Lang.Text("Download.Comp.Type.Shader")} ({{0}})";
                break;
            }
            case (int)ModComp.CompType.DataPack:
            {
                NewItem.Title = $"{Lang.Text("Download.Comp.Type.DataPack")} ({{0}})";
                break;
            }
            case (int)ModComp.CompType.Plugin:
            {
                NewItem.Title = $"{Lang.Text("Download.Comp.Type.Plugin")} ({{0}})";
                break;
            }
            case (int)ModComp.CompType.World:
            {
                NewItem.Title = $"{Lang.Text("Download.Comp.Type.World")} ({{0}})";
                break;
            }

            default:
            {
                NewItem.Title = $"{Lang.Text("Download.Comp.Favorites.UnknownType")} ({{0}})";
                break;
            }
        }

        NewItem.Card.Title = string.Format(NewItem.Title, 0);
        NewItem.Card.Children.Add(NewItem.ContentList);
        ItemList.Add(NewItem);
        return NewItem;
    }

    private void RefreshContent()
    {
        foreach (var item in ItemList) // 清除逻辑父子关系
            item.ContentList.Children.Clear();
        PanContentList.Children.Clear();
        var DataSource = IsSearching ? SearchResult : CompItemList;
        foreach (var item in DataSource)
            GetSuitListContainer(IsSearching ? -1 : (int)((ModComp.CompProject)item.Tag).Type).ContentList.Children
                .Add(item);
        foreach (var item in ItemList)
        {
            if (item.ContentList.Children.Count == 0)
                continue;
            PanContentList.Children.Add(item.Card);
        }
    }

    private void RefreshCardTitle()
    {
        foreach (var item in ItemList)
            item.Card.Title = string.Format(item.Title,
                CompItemList.Where(e => (int)((ModComp.CompProject)e.Tag).Type == item.CompType).Count());
        if (!ItemList.Any(e => e.CompType.Equals(-1)))
            return;
        var SearchItem = ItemList.First(e => e.CompType.Equals(-1));
        if (SearchItem is not null) SearchItem.Card.Title = string.Format(SearchItem.Title, SearchResult.Count);
    }

    #endregion

    #region UI 化 - 加载主逻辑

    // 结果 UI 化
    private void Load_OnFinish()
    {
        ItemList.Clear();
        try
        {
            AllowSearch = false;
            PanSearchBox.Text = string.Empty;
            AllowSearch = true;
            CompItemList.Clear();
            var SomeGetFail = Loader.Input.Count != Loader.Output.Count;
            HintGetFail.Visibility = SomeGetFail ? Visibility.Visible : Visibility.Collapsed;
            foreach (var item in Loader.Output)
            {
                var CompItem = item.ToListItem();
                ListItemBuild(CompItem);
                CompItemList.Add(CompItem);
            }

            if (CompItemList.Any()) // 有收藏
            {
                if (!IsSearching)
                {
                    PanSearchBox.Visibility = Visibility.Visible;
                    PanContentList.Visibility = Visibility.Visible;
                    CardNoContent.Visibility = Visibility.Collapsed;
                }
            }
            else // 没有收藏
            {
                PanSearchBox.Visibility = Visibility.Collapsed;
                PanContentList.Visibility = Visibility.Collapsed;
                CardNoContent.Visibility = Visibility.Visible;
            }

            RefreshContent();
            RefreshCardTitle();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "可视化收藏夹列表出错", ModBase.LogLevel.Feedback);
        }
    }

    private void ListItemBuild(MyListItem CompItem)
    {
        CompItem.Type = MyListItem.CheckType.CheckBox;
        var CompId = ((ModComp.CompProject)CompItem.Tag).Id;
        // ----备注----
        var Notes = "";
        CurrentFavTarget.Notes.TryGetValue(CompId, out Notes);
        var NoteItem = new Run { Foreground = new SolidColorBrush(Color.FromRgb(0, 184, 148)) };
        if (!string.IsNullOrWhiteSpace(Notes)) NoteItem.Text = $" ({Notes})";
        CompItem.LabTitle.Inlines.Add(NoteItem);
        // ----添加按钮----
        // 修改备注按钮
        var Btn_EditNote = new MyIconButton();
        Btn_EditNote.Logo = Icon.IconButtonEdit;
        Btn_EditNote.ToolTip = Lang.Text("Download.Comp.Favorites.EditNote");
        ToolTipService.SetPlacement(Btn_EditNote, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(Btn_EditNote, 30d);
        ToolTipService.SetHorizontalOffset(Btn_EditNote, 2d);
        Btn_EditNote.Click += (sender, e) =>
        {
            CurrentFavTarget.Notes.TryGetValue(CompId, out Notes);
            var DesiredNote = ModMain.MyMsgBoxInput(Lang.Text("Download.Comp.Favorites.EditNote"), DefaultInput: Notes);
            // 只有在用户确认时才更新备注，避免取消时清空原有备注
            if (DesiredNote is not null)
            {
                CurrentFavTarget.Notes[CompId] = DesiredNote;
                NoteItem.Text = string.IsNullOrWhiteSpace(DesiredNote) ? "" : $" ({DesiredNote})";
                ModComp.CompFavorites.Save();
            }
        };
        // 删除按钮
        var Btn_Delete = new MyIconButton();
        Btn_Delete.Logo = Icon.IconButtonLikeFill;
        Btn_Delete.ToolTip = Lang.Text("Download.Comp.Favorites.Action.Unfavorite");
        ToolTipService.SetPlacement(Btn_Delete, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(Btn_Delete, 30d);
        ToolTipService.SetHorizontalOffset(Btn_Delete, 2d);
        Btn_Delete.Click += (sender, e) =>
        {
            Items_CancelFavorites(CompItem);
            RefreshContent();
            RefreshCardTitle();
            RefreshBar();
        };
        CompItem.Buttons = new[] { Btn_EditNote, Btn_Delete };
        // ---操作逻辑---
        // 右键查看详细信息界面
        if (CompItem.Tag is ModComp.CompProject)
            CompItem.MouseRightButtonUp += (_, _) => ModMain.FrmMain.PageChange(
                new FormMain.PageStackData
                {
                    Page = FormMain.PageType.CompDetail,
                    Additional = ((ModComp.CompProject)CompItem.Tag, new List<string>(), string.Empty, ModComp.CompLoaderType.Any,
                        ((ModComp.CompProject)CompItem.Tag).Type, null, null, null)
                });
        // ---其它事件---
        CompItem.Changed += ItemCheckStatusChanged;
    }

    #endregion

    #region UI 化 - 选择操作

    private int BottomBarShownCount;

    private void RefreshBar()
    {
        var NewCount = SelectedItemList.Count;
        var Selected = NewCount > 0;
        if (Selected)
            LabSelect.Text = Lang.Text("Download.Comp.Favorites.Hint.SelectedCount", NewCount); // 取消所有选择时不更新数字
        // 更新显示状态
        if (ModAnimation.AniControlEnabled == 0)
        {
            PanContentList.Margin = new Thickness(0d, 0d, 0d, Selected ? 80 : 0);
            if (Selected)
            {
                // 仅在数量增加时播放出现/跳跃动画
                if (BottomBarShownCount >= NewCount)
                {
                    BottomBarShownCount = NewCount;
                    return;
                }

                BottomBarShownCount = NewCount;
                // 出现/跳跃动画
                CardSelect.Visibility = Visibility.Visible;
                ModAnimation.AniStart(
                    new[]
                    {
                        ModAnimation.AaOpacity(CardSelect, 1d - CardSelect.Opacity, 60),
                        ModAnimation.AaTranslateY(CardSelect, -27 - TransSelect.Y, 120,
                            Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)),
                        ModAnimation.AaTranslateY(CardSelect, 3d, 150, 120,
                            new ModAnimation.AniEaseInoutFluent(ModAnimation.AniEasePower.Weak)),
                        ModAnimation.AaTranslateY(CardSelect, -1, 90, 270,
                            new ModAnimation.AniEaseInoutFluent(ModAnimation.AniEasePower.Weak))
                    }, "CompFavorites Sidebar");
            }
            else
            {
                // 不重复播放隐藏动画
                if (BottomBarShownCount == 0)
                    return;
                BottomBarShownCount = 0;
                // 隐藏动画
                ModAnimation.AniStart(
                    new[]
                    {
                        ModAnimation.AaOpacity(CardSelect, -CardSelect.Opacity, 90),
                        ModAnimation.AaTranslateY(CardSelect, -10 - TransSelect.Y, 90,
                            Ease: new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)),
                        ModAnimation.AaCode(() => CardSelect.Visibility = Visibility.Collapsed, After: true)
                    }, "CompFavorites Sidebar");
            }
        }
        else
        {
            ModAnimation.AniStop("CompFavorites Sidebar");
            BottomBarShownCount = NewCount;
            if (Selected)
            {
                CardSelect.Visibility = Visibility.Visible;
                CardSelect.Opacity = 1d;
                TransSelect.Y = -25;
            }
            else
            {
                CardSelect.Visibility = Visibility.Collapsed;
                CardSelect.Opacity = 0d;
                TransSelect.Y = -10;
            }
        }
    }

    #endregion

    #region 事件

    // 选中状态改变
    private void ItemCheckStatusChanged(object sender, ModBase.RouteEventArgs e)
    {
        var SenderItem = (MyListItem)sender;
        if (SelectedItemList.Contains(SenderItem))
            SelectedItemList.Remove(SenderItem);
        if (SenderItem.Checked)
            SelectedItemList.Add(SenderItem);
        RefreshBar();
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
                if (ErrorMessage.Contains(Lang.Text("Common.Error.InvalidJson")))
                {
                    ModBase.Log("[Download] 下载的工程列表 JSON 文件损坏，已自动重试", ModBase.LogLevel.Debug);
                    PageLoaderRestart();
                }

                break;
            }
        }
    }

    private void Btn_FavoritesCancel_Clicked(object sender, ModBase.RouteEventArgs e)
    {
        foreach (var Items in SelectedItemList.Clone())
            Items_CancelFavorites(Items);
        if (CompItemList.Any())
        {
            RefreshContent();
            RefreshCardTitle();
        }
        else
        {
            Loader.Start();
        }

        RefreshBar();
    }

    private void Btn_SelectCancel_Clicked(object sender, ModBase.RouteEventArgs e)
    {
        Items_SetSelectAll(false);
    }

    private void Btn_FavoritesShare_Clicked(object sender, ModBase.RouteEventArgs e)
    {
        try
        {
            ModBase.ClipboardSet(
                ModComp.CompFavorites.GetShareCode(SelectedItemList.Select(i => ((ModComp.CompProject)i.Tag).Id)
                    .ToHashSet()));
            Items_SetSelectAll(false);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[CompFavourites] 分享收藏时发生错误", ModBase.LogLevel.Hint);
        }
    }

    private void Btn_FavoritesDownload_Clicked(object sender, ModBase.RouteEventArgs e)
    {
        try
        {
            if (1 != ModMain.MyMsgBox(
                    Lang.Text("Download.Comp.Favorites.Dialog.BulkDownload.Content"),
                    Lang.Text("Download.Comp.Favorites.Dialog.BulkDownload.Title"),
                    Lang.Text("Common.Action.Continue"),
                    Lang.Text("Common.Action.Cancel"), IsWarn: true
                )) return;
            var SupportedModLoader = new List<ModComp.CompLoaderType>();
            var LoaderFirstSet = true;
            var HasMod = false;
            foreach (var Item in SelectedItemList) // 获取共同支持的 ModLoader
            {
                var Proj = (ModComp.CompProject)Item.Tag;
                if (Proj.Type == ModComp.CompType.Mod)
                {
                    HasMod = true;
                    if (LoaderFirstSet)
                    {
                        LoaderFirstSet = false;
                        SupportedModLoader = Proj.ModLoaders;
                    }
                    else
                    {
                        SupportedModLoader = SupportedModLoader.Intersect(Proj.ModLoaders).ToList();
                    }
                }
            }

            // 检查是否有共同支持的 ModLoader
            if (HasMod && SupportedModLoader.Count == 0)
            {
                ModMain.Hint(Lang.Text("Download.Comp.Favorites.Hint.SelectLoader"), ModMain.HintType.Critical);
                return;
            }

            // 要求选择版本
            var DesiredModLoader = ModComp.CompLoaderType.Any;
            if (HasMod && SupportedModLoader.Count > 0)
                if (SupportedModLoader.Count > 0)
                {
                    var MSelection = new List<IMyRadio>();
                    foreach (var i in SupportedModLoader)
                        MSelection.Add(new MyRadioBox { Text = i.ToString() });
                    var SelectedModLoaderStr = ModMain.MyMsgBoxSelect(MSelection, Lang.Text("Download.Comp.Favorites.Dialog.SelectLoader.Title"), Button2: Lang.Text("Common.Action.Cancel"));
                    if (SelectedModLoaderStr is null)
                        return;
                    DesiredModLoader = SupportedModLoader[(int)SelectedModLoaderStr];
                }

            ModMain.Hint(Lang.Text("Download.Comp.Favorites.Hint.LoadingVersions"));
            // 输入 Ids，输出合适版本
            var GetInfoAndDownloadLoader = new List<ModLoader.LoaderBase>();
            GetInfoAndDownloadLoader.Add(new ModLoader.LoaderTask<List<string>, List<DownloadFile>>(
                Lang.Text("Download.Comp.Favorites.LoaderName.QueryInfo"), Ts =>
            {
                List<List<ModComp.CompFile>> AllFiles = [];
                List<string> SuitVersion = [];
                var VersionFirstSet = true;
                // 工程支持的全部版本获取
                Func<List<List<string>>, List<string>> GetAllVersionList = ls =>
                {
                    var allVersionList = new List<string>();
                    foreach (var i in ls) allVersionList.AddRange(i);

                    return allVersionList.Distinct().ToList();
                };
                // 获取多个工程之间支持的版本的交集
                var FinishedTasks = 0;
                foreach (var Item in Ts.Input)
                    ModBase.RunInNewThread(() =>
                    {
                        try
                        {
                            AllFiles.Add(ModComp.CompFilesGet(Item, ModComp.CompRequest.IsFromCurseForge(Item))
                                .Where(i => i.Type != ModComp.CompType.Mod || i.ModLoaders.Contains(DesiredModLoader))
                                .ToList());
                        }
                        catch (Exception ex)
                        {
                            ModBase.Log(ex, $"获取 {Item} 的下载信息失败", ModBase.LogLevel.Hint);
                        }
                        finally
                        {
                            FinishedTasks += 1;
                        }
                    });
                while (FinishedTasks != Ts.Input.Count)
                    Thread.Sleep(200);
                // 求取共同的版本
                foreach (var Item in AllFiles)
                {
                    var Current = GetAllVersionList(Item.Select(i => i.GameVersions).ToList());
                    if (VersionFirstSet)
                    {
                        VersionFirstSet = false;
                        SuitVersion = Current;
                    }
                    else
                    {
                        SuitVersion = SuitVersion.Intersect(Current).ToList();
                    }

                    // Log(SuitVersion.Join(","))
                    if (SuitVersion.Count == 0)
                    {
                        ModMain.Hint(Lang.Text("Download.Comp.Favorites.Hint.NoResource"), ModMain.HintType.Critical);
                        Ts.Abort();
                        return;
                    }
                    // 要求用户选择希望下载的版本
                }

                int? SelectedVersion = 0;
                ModBase.RunInUiWait(() =>
                {
                    List<IMyRadio> Selection = [];
                    foreach (var i in SuitVersion)
                        Selection.Add(new MyRadioBox { Text = i });
                    SelectedVersion = ModMain.MyMsgBoxSelect(Selection, Lang.Text("Download.Comp.Favorites.Dialog.SelectVersion.Title"), Button2: Lang.Text("Common.Action.Cancel"));
                    if (SelectedVersion is null) Ts.Abort();
                });
                string SelectedVersionStr = SuitVersion[(int)SelectedVersion];
                ModMain.Hint(Lang.Text("Download.Comp.Favorites.Hint.SelectSaveLocation", SelectedVersionStr));
                var SaveFolder = SystemDialogs.SelectFolder();
                if (string.IsNullOrWhiteSpace(SaveFolder))
                {
                    Ts.Abort();
                    return;
                }

                ;
                // 获取有期望版本号的文件
                List<DownloadFile> Res = [];
                foreach (var Target in AllFiles)
                {
                    // 按照发布日期排序
                    var FinalChoices = Target.Where(i => i.GameVersions.Contains(SelectedVersionStr)).ToList();
                    FinalChoices.Sort((a, b) => a.ReleaseDate > b.ReleaseDate);
                    // 获取文件名
                    var TargetProject = ModComp.CompProjectCache[FinalChoices.First().ProjectId];
                    var FileName = ModComp.CompFileNameGet(TargetProject, FinalChoices.First());
                    // 选择最新版本进行下载
                    Res.Add(FinalChoices.First().ToNetFile(System.IO.Path.Combine(SaveFolder, FileName)));
                }

                Ts.Output = Res;
            })
            {
                ProgressWeight = 2d
            });

            GetInfoAndDownloadLoader.Add(
                new LoaderDownload(
                    Lang.Text("Download.Comp.Favorites.LoaderName.BatchDownloadSuitable"),
                    []
                )
                {
                    ProgressWeight = 8d
                }
            );
            var checkLoader = new ModLoader.LoaderCombo<List<string>>(
                Lang.Text("Download.Comp.Favorites.LoaderName.BatchDownload", ModBase.GetUuid()),
                GetInfoAndDownloadLoader
            )
            {
                OnStateChanged = ModDownloadLib.LoaderStateChangedHintOnly
            };

            checkLoader.Start(SelectedItemList.Select(i => ((ModComp.CompProject)i.Tag).Id).ToList());
            ModLoader.LoaderTaskbarAdd(checkLoader);
            ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
            ModMain.FrmMain.BtnExtraDownload.Ribble();
            Items_SetSelectAll(false);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "批量下载收藏时发生错误", ModBase.LogLevel.Hint);
        }
    }

    private void Items_SetSelectAll(bool TargetStatus)
    {
        if (IsSearching)
            foreach (var Item in SearchResult)
                Item.Checked = TargetStatus;
        else
            foreach (var Item in CompItemList)
                Item.Checked = TargetStatus;
        SelectedItemList = CompItemList.Where(e => e.Checked).ToList();
    }

    private void Items_CancelFavorites(MyListItem Item)
    {
        try
        {
            CompItemList.Remove(Item);
            if (SelectedItemList.Contains(Item))
                SelectedItemList.Remove(Item);
            if (SearchResult.Contains(Item))
                SearchResult.Remove(Item);
            CurrentFavTarget.Favs.Remove(((ModComp.CompProject)Item.Tag).Id);
            ModComp.CompFavorites.Save();
            if (!CompItemList.Any())
                ModMain.FrmDownloadCompFavorites.PageLoaderRestart();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[CompFavourites] 移除收藏时发生错误");
        }
    }

    private void Page_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.A && (e.KeyboardDevice.IsKeyDown(Key.LeftCtrl) || e.KeyboardDevice.IsKeyDown(Key.RightCtrl)))
            Items_SetSelectAll(true);
    }

    private void Manage_Click(object sender, EventArgs _)
    {
        var Body = new ContextMenu();
        var NewItem = new MyMenuItem
        {
            Header = Lang.Text("Download.Comp.Favorites.Menu.Share"),
            Icon = Icon.IconButtonShare
        };
        NewItem.Click += (_, _) =>
        {
            try
            {
                if (CurrentFavTarget.Favs.Count == 0)
                {
                    HintWrapper.Show(Lang.Text("Download.Comp.Favorites.Hint.NothingShared"));
                    return;
                }

                ModBase.ClipboardSet(ModComp.CompFavorites.GetShareCode(CurrentFavTarget.Favs));
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "[Favourites] 分享收藏时发生错误", ModBase.LogLevel.Hint);
            }
        };
        Body.Items.Add(NewItem);
        NewItem = new MyMenuItem
        {
            Header = Lang.Text("Download.Comp.Favorites.Menu.Import"),
            Icon = Icon.IconButtonAdd
        };
        NewItem.Click += (_, _) =>
        {
            try
            {
                var ClipData = ModMain.MyMsgBoxInput(Lang.Text("Download.Comp.Favorites.Dialog.Import.Input"), HintText: Lang.Text("Download.Comp.Favorites.Dialog.Import.Hint"));
                if (string.IsNullOrWhiteSpace(ClipData)) return;
                var NewFavs = ModComp.CompFavorites.GetIdsByShareCode(ClipData);
                if (NewFavs.Count == 0)
                {
                    ModMain.Hint(Lang.Text("Download.Comp.Favorites.Hint.NothingShared"));
                    return;
                }

                var UserWant = ModMain.MyMsgBox(Lang.Text("Download.Comp.Favorites.Dialog.Import.Type"), Button1: Lang.Text("Download.Comp.Favorites.Dialog.Import.NewFolder"), Button2: Lang.Text("Download.Comp.Favorites.Dialog.Import.CurrentFolder"));
                switch (UserWant)
                {
                    case 1:
                    {
                        var NewFavName = ModMain.MyMsgBoxInput(Lang.Text("Download.Comp.Favorites.Dialog.Import.New"), Lang.Text("Download.Comp.Favorites.Dialog.NewPrompt"));
                        if (string.IsNullOrWhiteSpace(NewFavName)) return;
                        ModComp.CompFavorites.FavoritesList.Add(ModComp.CompFavorites.GetNewFav(NewFavName, NewFavs));
                        ModComp.CompFavorites.Save();
                        RefreshFavTargets();
                        ComboTargetFav.SelectedIndex = ComboTargetFav.Items.Count - 1;
                        break;
                    }
                    case 2:
                    {
                        NewFavs.ToList().ForEach(x => CurrentFavTarget.Favs.Add(x));
                        ModComp.CompFavorites.Save();
                        Loader.Start(IsForceRestart: true);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "解析分享数据失败", ModBase.LogLevel.Hint);
            }
        };
        Body.Items.Add(NewItem);
        NewItem = new MyMenuItem
        {
            Header = Lang.Text("Download.Comp.Favorites.Menu.New"),
            Icon = Icon.IconButtonCreate
        };
        NewItem.Click += (_, _) =>
        {
            var NewFavName = ModMain.MyMsgBoxInput(Lang.Text("Download.Comp.Favorites.Menu.New"), Lang.Text("Download.Comp.Favorites.Dialog.NewPrompt"));
            if (string.IsNullOrWhiteSpace(NewFavName))
                return;
            ModComp.CompFavorites.FavoritesList.Add(ModComp.CompFavorites.GetNewFav(NewFavName, null));
            ModComp.CompFavorites.Save();
            RefreshFavTargets();
            ComboTargetFav.SelectedIndex = ComboTargetFav.Items.Count - 1;
        };
        Body.Items.Add(NewItem);
        NewItem = new MyMenuItem
        {
            Header = Lang.Text("Download.Comp.Favorites.Menu.Rename"),
            Icon = Icon.IconButtonEdit
        };
        NewItem.Click += (_, _) =>
        {
            var newName = ModMain.MyMsgBoxInput(Lang.Text("Download.Comp.Favorites.Dialog.Rename.Title"), DefaultInput: CurrentFavTarget.Name);
            if (string.IsNullOrWhiteSpace(newName) || (CurrentFavTarget.Name ?? "") == (newName ?? ""))
                return;
            CurrentFavTarget.Name = newName;
            ModComp.CompFavorites.Save();
            RefreshFavTargets();
        };
        Body.Items.Add(NewItem);
        NewItem = new MyMenuItem
        {
            Header = Lang.Text("Download.Comp.Favorites.Menu.Delete"),
            Icon = Icon.IconButtonDelete
        };
        NewItem.Click += (_, _) =>
        {
            if (ModComp.CompFavorites.FavoritesList.Count == 1)
            {
                ModMain.Hint(Lang.Text("Download.Comp.Favorites.Hint.LastCollection"));
                return;
            }

            var content = Lang.Text("Download.Comp.Favorites.Dialog.Delete.Confirm", CurrentFavTarget.Name, CurrentFavTarget.Favs.Count, CurrentFavTarget.Id);
            var res = ModMain.MyMsgBox(content, Lang.Text("Download.Comp.Favorites.Dialog.Delete.Title"), IsWarn: true, Button1: Lang.Text("Common.Action.No"), Button2: Lang.Text("Common.Action.Yes"), Button3: Lang.Text("Common.Action.No"));
            if (res == 2)
            {
                ModComp.CompFavorites.FavoritesList.Remove(CurrentFavTarget);
                ModComp.CompFavorites.Save();
                ModMain.Hint(Lang.Text("Download.Comp.Favorites.Hint.Deleted"), ModMain.HintType.Finish);
                RefreshFavTargets();
                ComboTargetFav.SelectedIndex = 0;
            }
        };
        Body.Items.Add(NewItem);
        Body.PlacementTarget = (UIElement)sender;
        Body.Placement = PlacementMode.Bottom;
        Body.IsOpen = true;
    }

    private void ComboTargetFav_Selected(object sender, RoutedEventArgs e)
    {
        if (ComboTargetFav.SelectedItem is null)
            return;
        Items_SetSelectAll(false);
        Loader.Start(IsForceRestart: true);
    }

    private void HintGetFail_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var Content = Lang.Text("Download.Comp.Favorites.Dialog.GetFailed.Content") + "\r\n" + "\r\n";
        var FailIds = Loader.Input.Except(Loader.Output.Select(i => i.Id).ToList()).ToList();
        foreach (var Id in FailIds)
            Content += $" - {Id}" + "\r\n";
        ModMain.MyMsgBox(Content, Lang.Text("Download.Comp.Favorites.Dialog.GetFailed.Title"), Button2: Lang.Text("Download.Comp.Favorites.Dialog.GetFailed.CopyIds"), Button3: Lang.Text("Download.Comp.Favorites.Dialog.GetFailed.Remove"),
            Button2Action: () => ModBase.ClipboardSet(FailIds.Join("\r\n")), Button3Action: () =>
            {
                foreach (var Id in FailIds)
                    CurrentFavTarget.Favs.Remove(Id);
                ModComp.CompFavorites.Save();
                ModMain.Hint(Lang.Text("Download.Comp.Favorites.Hint.Removed"), ModMain.HintType.Finish);
            });
    }

    #endregion

    #region 搜索

    private bool IsSearching => !string.IsNullOrWhiteSpace(PanSearchBox.Text);

    private bool AllowSearch = true;
    private List<MyListItem> SearchResult = new();

    public void SearchRun(object sender, EventArgs e)
    {
        if (!AllowSearch)
            return;
        if (IsSearching)
        {
            // 构造请求
            var QueryList = new List<ModBase.SearchEntry<MyListItem>>();
            foreach (var Item in CompItemList)
            {
                if (!(Item.Tag is ModComp.CompProject))
                    continue;
                var Entry = (ModComp.CompProject)Item.Tag;
                var SearchSource = new List<ModBase.SearchSource>();
                SearchSource.Add(new ModBase.SearchSource(Entry.RawName, 1d));
                if (Entry.Description is not null && !string.IsNullOrEmpty(Entry.Description))
                    SearchSource.Add(new ModBase.SearchSource(Entry.Description, 0.4d));
                if ((Entry.TranslatedName ?? "") != (Entry.RawName ?? ""))
                    SearchSource.Add(new ModBase.SearchSource(Entry.TranslatedName, 1d));
                SearchSource.Add(new ModBase.SearchSource(string.Join("", Entry.Tags), 0.2d));
                QueryList.Add(new ModBase.SearchEntry<MyListItem> { Item = Item, SearchSource = SearchSource });
            }

            // 进行搜索
            SearchResult = ModBase.Search(QueryList, PanSearchBox.Text, 6, 0.35d).Select(r => r.Item).ToList();
        }

        RefreshContent();
        RefreshCardTitle();
    }

    #endregion
}
