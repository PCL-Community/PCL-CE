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
    private readonly List<MyListItem> compItemList = new();
    private List<MyListItem> selectedItemList = new();

    public PageDownloadCompFavorites()
    {
        loader = new ModLoader.LoaderTask<List<string>, List<ModComp.CompProject>>("CompProject Favorites",
            CompFavoritesGet, LoaderInput);
        Initialized += PageDownloadCompFavorites_Inited;
        Loaded += PageDownloadCompFavorites_Loaded;
        KeyDown += Page_KeyDown;
        InitializeComponent();
        {

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
            var selectedItem = (MyComboBoxItem)ComboTargetFav.SelectedItem;
            if (selectedItem is null)
            {
                ModBase.Log("[Favorites] 异常：未选择收藏夹");
                selectedItem = (MyComboBoxItem)ComboTargetFav.Items.GetItemAt(0);
            }

            return ModComp.CompFavorites.FavoritesList
                .First(e => string.Equals(e.Id, selectedItem.Tag?.ToString(), StringComparison.OrdinalIgnoreCase));
        }
    }

    #region 加载器信息

    // 加载器信息
    public ModLoader.LoaderTask<List<string>, List<ModComp.CompProject>> loader;

    private void PageDownloadCompFavorites_Inited(object sender, EventArgs e)
    {
        RefreshFavTargets();
        PageLoaderInit(Load, PanLoad, PanContent, null, loader, _ => Load_OnFinish(), LoaderInput);
    }

    private void PageDownloadCompFavorites_Loaded(object sender, EventArgs e)
    {
        Items_SetSelectAll(false);
        RefreshBar();
        if (loader.input is not null && !loader.input.Count.Equals(CurrentFavTarget.Favs.Count)) RefreshFavTargets();
    }

    private List<string> LoaderInput()
    {
        List<string> targetList = null;
        try
        {
            targetList = CurrentFavTarget.Favs.Distinct().ToList();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[Favorites] 加载收藏夹列表时出错");
        }

        return (List<string>)targetList.Clone(); // 复制而不是直接引用！
    }

    private void CompFavoritesGet(ModLoader.LoaderTask<List<string>, List<ModComp.CompProject>> task)
    {
        task.output = ModComp.CompRequest.GetCompProjectsByIds(task.input);
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

    private readonly List<CompListItemContainer> itemList = new();

    /// <summary>
    ///     刷新收藏夹列表
    /// </summary>
    private void RefreshFavTargets()
    {
        ComboTargetFav.Items.Clear();
        foreach (var Target in ModComp.CompFavorites.FavoritesList)
        {
            var item = new MyComboBoxItem
            {
                Content = Target.Name,
                Tag = Target.Id
            };
            ComboTargetFav.Items.Add(item);
        }

        if (ComboTargetFav.SelectedIndex == -1) ComboTargetFav.SelectedIndex = 0; // 默认选择第一个
    }

    /// <summary>
    ///     返回适合当前工程项目的卡片记录
    /// </summary>
    /// <param name="type">工程项目类型</param>
    /// <returns></returns>
    private CompListItemContainer GetSuitListContainer(int type)
    {
        if (itemList.Any(e => e.CompType.Equals(type))) return itemList.First(e => e.CompType.Equals(type));

        var newItem = new CompListItemContainer
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
            CompType = type
        };
        switch (type)
        {
            case -1:
            {
                newItem.Title = Lang.Text("Download.Comp.Favorites.SearchResults.Title");
                break;
            }
            case (int)ModComp.CompType.Mod:
            {
                newItem.Title = "Mod ({0})";
                break;
            }
            case (int)ModComp.CompType.ModPack:
            {
                newItem.Title = $"{Lang.Text("Download.Comp.Type.Modpack")} ({{0}})";
                break;
            }
            case (int)ModComp.CompType.ResourcePack:
            {
                newItem.Title = $"{Lang.Text("Download.Comp.Type.ResourcePack")} ({{0}})";
                break;
            }
            case (int)ModComp.CompType.Shader:
            {
                newItem.Title = $"{Lang.Text("Download.Comp.Type.Shader")} ({{0}})";
                break;
            }
            case (int)ModComp.CompType.DataPack:
            {
                newItem.Title = $"{Lang.Text("Download.Comp.Type.DataPack")} ({{0}})";
                break;
            }
            case (int)ModComp.CompType.Plugin:
            {
                newItem.Title = $"{Lang.Text("Download.Comp.Type.Plugin")} ({{0}})";
                break;
            }
            case (int)ModComp.CompType.World:
            {
                newItem.Title = $"{Lang.Text("Download.Comp.Type.World")} ({{0}})";
                break;
            }

            default:
            {
                newItem.Title = $"{Lang.Text("Download.Comp.Favorites.UnknownType")} ({{0}})";
                break;
            }
        }

        newItem.Card.Title = string.Format(newItem.Title, 0);
        newItem.Card.Children.Add(newItem.ContentList);
        itemList.Add(newItem);
        return newItem;
    }

    private void RefreshContent()
    {
        foreach (var item in itemList) // 清除逻辑父子关系
            item.ContentList.Children.Clear();
        PanContentList.Children.Clear();
        var dataSource = IsSearching ? searchResult : compItemList;
        foreach (var item in dataSource)
            GetSuitListContainer(IsSearching ? -1 : (int)((ModComp.CompProject)item.Tag).Type).ContentList.Children
                .Add(item);
        foreach (var item in itemList)
        {
            if (item.ContentList.Children.Count == 0)
                continue;
            PanContentList.Children.Add(item.Card);
        }
    }

    private void RefreshCardTitle()
    {
        foreach (var item in itemList)
            item.Card.Title = string.Format(item.Title,
                compItemList.Where(e => (int)((ModComp.CompProject)e.Tag).Type == item.CompType).Count());
        if (!itemList.Any(e => e.CompType.Equals(-1)))
            return;
        var searchItem = itemList.First(e => e.CompType.Equals(-1));
        if (searchItem is not null) searchItem.Card.Title = string.Format(searchItem.Title, searchResult.Count);
    }

    #endregion

    #region UI 化 - 加载主逻辑

    // 结果 UI 化
    private void Load_OnFinish()
    {
        itemList.Clear();
        try
        {
            allowSearch = false;
            PanSearchBox.Text = string.Empty;
            allowSearch = true;
            compItemList.Clear();
            var someGetFail = loader.input.Count != loader.output.Count;
            HintGetFail.Visibility = someGetFail ? Visibility.Visible : Visibility.Collapsed;
            foreach (var item in loader.output)
            {
                var compItem = item.ToListItem();
                ListItemBuild(compItem);
                compItemList.Add(compItem);
            }

            if (compItemList.Any()) // 有收藏
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

    private void ListItemBuild(MyListItem compItem)
    {
        compItem.Type = MyListItem.CheckType.CheckBox;
        var compId = ((ModComp.CompProject)compItem.Tag).Id;
        // ----备注----
        var notes = "";
        CurrentFavTarget.Notes.TryGetValue(compId, out notes);
        var noteItem = new Run { Foreground = new SolidColorBrush(Color.FromRgb(0, 184, 148)) };
        if (!string.IsNullOrWhiteSpace(notes)) noteItem.Text = $" ({notes})";
        compItem.LabTitle.Inlines.Add(noteItem);
        // ----添加按钮----
        // 修改备注按钮
        var btn_EditNote = new MyIconButton();
        btn_EditNote.Logo = Icon.IconButtonEdit;
        btn_EditNote.ToolTip = Lang.Text("Download.Comp.Favorites.EditNote");
        ToolTipService.SetPlacement(btn_EditNote, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btn_EditNote, 30d);
        ToolTipService.SetHorizontalOffset(btn_EditNote, 2d);
        btn_EditNote.Click += (sender, e) =>
        {
            CurrentFavTarget.Notes.TryGetValue(compId, out notes);
            var desiredNote = ModMain.MyMsgBoxInput(Lang.Text("Download.Comp.Favorites.EditNote"), defaultInput: notes);
            // 只有在用户确认时才更新备注，避免取消时清空原有备注
            if (desiredNote is not null)
            {
                CurrentFavTarget.Notes[compId] = desiredNote;
                noteItem.Text = string.IsNullOrWhiteSpace(desiredNote) ? "" : $" ({desiredNote})";
                ModComp.CompFavorites.Save();
            }
        };
        // 删除按钮
        var btn_Delete = new MyIconButton();
        btn_Delete.Logo = Icon.IconButtonLikeFill;
        btn_Delete.ToolTip = Lang.Text("Download.Comp.Favorites.Action.Unfavorite");
        ToolTipService.SetPlacement(btn_Delete, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btn_Delete, 30d);
        ToolTipService.SetHorizontalOffset(btn_Delete, 2d);
        btn_Delete.Click += (sender, e) =>
        {
            Items_CancelFavorites(compItem);
            RefreshContent();
            RefreshCardTitle();
            RefreshBar();
        };
        compItem.Buttons = new[] { btn_EditNote, btn_Delete };
        // ---操作逻辑---
        // 右键查看详细信息界面
        if (compItem.Tag is ModComp.CompProject)
            compItem.MouseRightButtonUp += (_, _) => ModMain.frmMain.PageChange(
                new FormMain.PageStackData
                {
                    page = FormMain.PageType.CompDetail,
                    additional = ((ModComp.CompProject)compItem.Tag, new List<string>(), string.Empty, ModComp.CompLoaderType.Any,
                        ((ModComp.CompProject)compItem.Tag).Type, null, null, null)
                });
        // ---其它事件---
        compItem.Changed += ItemCheckStatusChanged;
    }

    #endregion

    #region UI 化 - 选择操作

    private int bottomBarShownCount;

    private void RefreshBar()
    {
        var newCount = selectedItemList.Count;
        var selected = newCount > 0;
        if (selected)
            LabSelect.Text = Lang.Text("Download.Comp.Favorites.Hint.SelectedCount", newCount); // 取消所有选择时不更新数字
        // 更新显示状态
        if (ModAnimation.AniControlEnabled == 0)
        {
            PanContentList.Margin = new Thickness(0d, 0d, 0d, selected ? 80 : 0);
            if (selected)
            {
                // 仅在数量增加时播放出现/跳跃动画
                if (bottomBarShownCount >= newCount)
                {
                    bottomBarShownCount = newCount;
                    return;
                }

                bottomBarShownCount = newCount;
                // 出现/跳跃动画
                CardSelect.Visibility = Visibility.Visible;
                ModAnimation.AniStart(
                    new[]
                    {
                        ModAnimation.AaOpacity(CardSelect, 1d - CardSelect.Opacity, 60),
                        ModAnimation.AaTranslateY(CardSelect, -27 - TransSelect.Y, 120,
                            ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)),
                        ModAnimation.AaTranslateY(CardSelect, 3d, 150, 120,
                            new ModAnimation.AniEaseInoutFluent(ModAnimation.AniEasePower.Weak)),
                        ModAnimation.AaTranslateY(CardSelect, -1, 90, 270,
                            new ModAnimation.AniEaseInoutFluent(ModAnimation.AniEasePower.Weak))
                    }, "CompFavorites Sidebar");
            }
            else
            {
                // 不重复播放隐藏动画
                if (bottomBarShownCount == 0)
                    return;
                bottomBarShownCount = 0;
                // 隐藏动画
                ModAnimation.AniStart(
                    new[]
                    {
                        ModAnimation.AaOpacity(CardSelect, -CardSelect.Opacity, 90),
                        ModAnimation.AaTranslateY(CardSelect, -10 - TransSelect.Y, 90,
                            ease: new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)),
                        ModAnimation.AaCode(() => CardSelect.Visibility = Visibility.Collapsed, after: true)
                    }, "CompFavorites Sidebar");
            }
        }
        else
        {
            ModAnimation.AniStop("CompFavorites Sidebar");
            bottomBarShownCount = newCount;
            if (selected)
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
        var senderItem = (MyListItem)sender;
        if (selectedItemList.Contains(senderItem))
            selectedItemList.Remove(senderItem);
        if (senderItem.Checked)
            selectedItemList.Add(senderItem);
        RefreshBar();
    }

    // 自动重试
    private void Load_State(object sender, MyLoading.MyLoadingState state, MyLoading.MyLoadingState oldState)
    {
        switch (loader.State)
        {
            case ModBase.LoadState.Failed:
            {
                var errorMessage = "";
                if (loader.Error is not null)
                    errorMessage = loader.Error.Message;
                if (errorMessage.Contains(Lang.Text("Common.Error.InvalidJson")))
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
        foreach (var Items in selectedItemList.Clone())
            Items_CancelFavorites(Items);
        if (compItemList.Any())
        {
            RefreshContent();
            RefreshCardTitle();
        }
        else
        {
            loader.Start();
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
                ModComp.CompFavorites.GetShareCode(selectedItemList.Select(i => ((ModComp.CompProject)i.Tag).Id)
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
                    Lang.Text("Common.Action.Cancel"), isWarn: true
                )) return;
            var supportedModLoader = new List<ModComp.CompLoaderType>();
            var loaderFirstSet = true;
            var hasMod = false;
            foreach (var Item in selectedItemList) // 获取共同支持的 ModLoader
            {
                var proj = (ModComp.CompProject)Item.Tag;
                if (proj.Type == ModComp.CompType.Mod)
                {
                    hasMod = true;
                    if (loaderFirstSet)
                    {
                        loaderFirstSet = false;
                        supportedModLoader = proj.ModLoaders;
                    }
                    else
                    {
                        supportedModLoader = supportedModLoader.Intersect(proj.ModLoaders).ToList();
                    }
                }
            }

            // 检查是否有共同支持的 ModLoader
            if (hasMod && supportedModLoader.Count == 0)
            {
                ModMain.Hint(Lang.Text("Download.Comp.Favorites.Hint.SelectLoader"), ModMain.HintType.Critical);
                return;
            }

            // 要求选择版本
            var desiredModLoader = ModComp.CompLoaderType.Any;
            if (hasMod && supportedModLoader.Count > 0)
                if (supportedModLoader.Count > 0)
                {
                    var mSelection = new List<IMyRadio>();
                    foreach (var i in supportedModLoader)
                        mSelection.Add(new MyRadioBox { Text = i.ToString() });
                    var selectedModLoaderStr = ModMain.MyMsgBoxSelect(mSelection, Lang.Text("Download.Comp.Favorites.Dialog.SelectLoader.Title"), button2: Lang.Text("Common.Action.Cancel"));
                    if (selectedModLoaderStr is null)
                        return;
                    desiredModLoader = supportedModLoader[(int)selectedModLoaderStr];
                }

            ModMain.Hint(Lang.Text("Download.Comp.Favorites.Hint.LoadingVersions"));
            // 输入 Ids，输出合适版本
            var getInfoAndDownloadLoader = new List<ModLoader.LoaderBase>();
            getInfoAndDownloadLoader.Add(new ModLoader.LoaderTask<List<string>, List<DownloadFile>>(
                Lang.Text("Download.Comp.Favorites.LoaderName.QueryInfo"), ts =>
            {
                List<List<ModComp.CompFile>> allFiles = [];
                List<string> suitVersion = [];
                var versionFirstSet = true;
                // 工程支持的全部版本获取
                Func<List<List<string>>, List<string>> getAllVersionList = ls =>
                {
                    var allVersionList = new List<string>();
                    foreach (var i in ls) allVersionList.AddRange(i);

                    return allVersionList.Distinct().ToList();
                };
                // 获取多个工程之间支持的版本的交集
                var finishedTasks = 0;
                foreach (var Item in ts.input)
                    ModBase.RunInNewThread(() =>
                    {
                        try
                        {
                            allFiles.Add(ModComp.CompFilesGet(Item, ModComp.CompRequest.IsFromCurseForge(Item))
                                .Where(i => i.Type != ModComp.CompType.Mod || i.ModLoaders.Contains(desiredModLoader))
                                .ToList());
                        }
                        catch (Exception ex)
                        {
                            ModBase.Log(ex, $"获取 {Item} 的下载信息失败", ModBase.LogLevel.Hint);
                        }
                        finally
                        {
                            finishedTasks += 1;
                        }
                    });
                while (finishedTasks != ts.input.Count)
                    Thread.Sleep(200);
                // 求取共同的版本
                foreach (var Item in allFiles)
                {
                    var current = getAllVersionList(Item.Select(i => i.GameVersions).ToList());
                    if (versionFirstSet)
                    {
                        versionFirstSet = false;
                        suitVersion = current;
                    }
                    else
                    {
                        suitVersion = suitVersion.Intersect(current).ToList();
                    }

                    // Log(SuitVersion.Join(","))
                    if (suitVersion.Count == 0)
                    {
                        ModMain.Hint(Lang.Text("Download.Comp.Favorites.Hint.NoResource"), ModMain.HintType.Critical);
                        ts.Abort();
                        return;
                    }
                    // 要求用户选择希望下载的版本
                }

                int? selectedVersion = 0;
                ModBase.RunInUiWait(() =>
                {
                    List<IMyRadio> selection = [];
                    foreach (var i in suitVersion)
                        selection.Add(new MyRadioBox { Text = i });
                    selectedVersion = ModMain.MyMsgBoxSelect(selection, Lang.Text("Download.Comp.Favorites.Dialog.SelectVersion.Title"), button2: Lang.Text("Common.Action.Cancel"));
                    if (selectedVersion is null) ts.Abort();
                });
                string selectedVersionStr = suitVersion[(int)selectedVersion];
                ModMain.Hint(Lang.Text("Download.Comp.Favorites.Hint.SelectSaveLocation", selectedVersionStr));
                var saveFolder = SystemDialogs.SelectFolder();
                if (string.IsNullOrWhiteSpace(saveFolder))
                {
                    ts.Abort();
                    return;
                }

                ;
                // 获取有期望版本号的文件
                List<DownloadFile> res = [];
                foreach (var Target in allFiles)
                {
                    // 按照发布日期排序
                    var finalChoices = Target.Where(i => i.GameVersions.Contains(selectedVersionStr)).ToList();
                    finalChoices.Sort((a, b) => a.ReleaseDate > b.ReleaseDate);
                    // 获取文件名
                    var targetProject = ModComp.compProjectCache[finalChoices.First().ProjectId];
                    var fileName = ModComp.CompFileNameGet(targetProject, finalChoices.First());
                    // 选择最新版本进行下载
                    res.Add(finalChoices.First().ToNetFile(System.IO.Path.Combine(saveFolder, fileName)));
                }

                ts.output = res;
            })
            {
                ProgressWeight = 2d
            });

            getInfoAndDownloadLoader.Add(
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
                getInfoAndDownloadLoader
            )
            {
                OnStateChanged = ModDownloadLib.LoaderStateChangedHintOnly
            };

            checkLoader.Start(selectedItemList.Select(i => ((ModComp.CompProject)i.Tag).Id).ToList());
            ModLoader.LoaderTaskbarAdd(checkLoader);
            ModMain.frmMain.BtnExtraDownload.ShowRefresh();
            ModMain.frmMain.BtnExtraDownload.Ribble();
            Items_SetSelectAll(false);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "批量下载收藏时发生错误", ModBase.LogLevel.Hint);
        }
    }

    private void Items_SetSelectAll(bool targetStatus)
    {
        if (IsSearching)
            foreach (var Item in searchResult)
                Item.Checked = targetStatus;
        else
            foreach (var Item in compItemList)
                Item.Checked = targetStatus;
        selectedItemList = compItemList.Where(e => e.Checked).ToList();
    }

    private void Items_CancelFavorites(MyListItem item)
    {
        try
        {
            compItemList.Remove(item);
            if (selectedItemList.Contains(item))
                selectedItemList.Remove(item);
            if (searchResult.Contains(item))
                searchResult.Remove(item);
            CurrentFavTarget.Favs.Remove(((ModComp.CompProject)item.Tag).Id);
            ModComp.CompFavorites.Save();
            if (!compItemList.Any())
                ModMain.frmDownloadCompFavorites.PageLoaderRestart();
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
        var body = new ContextMenu();
        var newItem = new MyMenuItem
        {
            Header = Lang.Text("Download.Comp.Favorites.Menu.Share"),
            Icon = Icon.IconButtonShare
        };
        newItem.Click += (_, _) =>
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
        body.Items.Add(newItem);
        newItem = new MyMenuItem
        {
            Header = Lang.Text("Download.Comp.Favorites.Menu.Import"),
            Icon = Icon.IconButtonAdd
        };
        newItem.Click += (_, _) =>
        {
            try
            {
                var clipData = ModMain.MyMsgBoxInput(Lang.Text("Download.Comp.Favorites.Dialog.Import.Input"), hintText: Lang.Text("Download.Comp.Favorites.Dialog.Import.Hint"));
                if (string.IsNullOrWhiteSpace(clipData)) return;
                var newFavs = ModComp.CompFavorites.GetIdsByShareCode(clipData);
                if (newFavs.Count == 0)
                {
                    ModMain.Hint(Lang.Text("Download.Comp.Favorites.Hint.NothingShared"));
                    return;
                }

                var userWant = ModMain.MyMsgBox(Lang.Text("Download.Comp.Favorites.Dialog.Import.Type"), button1: Lang.Text("Download.Comp.Favorites.Dialog.Import.NewFolder"), button2: Lang.Text("Download.Comp.Favorites.Dialog.Import.CurrentFolder"));
                switch (userWant)
                {
                    case 1:
                    {
                        var newFavName = ModMain.MyMsgBoxInput(Lang.Text("Download.Comp.Favorites.Dialog.Import.New"), Lang.Text("Download.Comp.Favorites.Dialog.NewPrompt"));
                        if (string.IsNullOrWhiteSpace(newFavName)) return;
                        ModComp.CompFavorites.FavoritesList.Add(ModComp.CompFavorites.GetNewFav(newFavName, newFavs));
                        ModComp.CompFavorites.Save();
                        RefreshFavTargets();
                        ComboTargetFav.SelectedIndex = ComboTargetFav.Items.Count - 1;
                        break;
                    }
                    case 2:
                    {
                        newFavs.ToList().ForEach(x => CurrentFavTarget.Favs.Add(x));
                        ModComp.CompFavorites.Save();
                        loader.Start(isForceRestart: true);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "解析分享数据失败", ModBase.LogLevel.Hint);
            }
        };
        body.Items.Add(newItem);
        newItem = new MyMenuItem
        {
            Header = Lang.Text("Download.Comp.Favorites.Menu.New"),
            Icon = Icon.IconButtonCreate
        };
        newItem.Click += (_, _) =>
        {
            var newFavName = ModMain.MyMsgBoxInput(Lang.Text("Download.Comp.Favorites.Menu.New"), Lang.Text("Download.Comp.Favorites.Dialog.NewPrompt"));
            if (string.IsNullOrWhiteSpace(newFavName))
                return;
            ModComp.CompFavorites.FavoritesList.Add(ModComp.CompFavorites.GetNewFav(newFavName, null));
            ModComp.CompFavorites.Save();
            RefreshFavTargets();
            ComboTargetFav.SelectedIndex = ComboTargetFav.Items.Count - 1;
        };
        body.Items.Add(newItem);
        newItem = new MyMenuItem
        {
            Header = Lang.Text("Download.Comp.Favorites.Menu.Rename"),
            Icon = Icon.IconButtonEdit
        };
        newItem.Click += (_, _) =>
        {
            var newName = ModMain.MyMsgBoxInput(Lang.Text("Download.Comp.Favorites.Dialog.Rename.Title"), defaultInput: CurrentFavTarget.Name);
            if (string.IsNullOrWhiteSpace(newName) || (CurrentFavTarget.Name ?? "") == (newName ?? ""))
                return;
            CurrentFavTarget.Name = newName;
            ModComp.CompFavorites.Save();
            RefreshFavTargets();
        };
        body.Items.Add(newItem);
        newItem = new MyMenuItem
        {
            Header = Lang.Text("Download.Comp.Favorites.Menu.Delete"),
            Icon = Icon.IconButtonDelete
        };
        newItem.Click += (_, _) =>
        {
            if (ModComp.CompFavorites.FavoritesList.Count == 1)
            {
                ModMain.Hint(Lang.Text("Download.Comp.Favorites.Hint.LastCollection"));
                return;
            }

            var content = Lang.Text("Download.Comp.Favorites.Dialog.Delete.Confirm", CurrentFavTarget.Name, CurrentFavTarget.Favs.Count, CurrentFavTarget.Id);
            var res = ModMain.MyMsgBox(content, Lang.Text("Download.Comp.Favorites.Dialog.Delete.Title"), isWarn: true, button1: Lang.Text("Common.Option.No"), button2: Lang.Text("Common.Option.Yes"), button3: Lang.Text("Common.Option.No"));
            if (res == 2)
            {
                ModComp.CompFavorites.FavoritesList.Remove(CurrentFavTarget);
                ModComp.CompFavorites.Save();
                ModMain.Hint(Lang.Text("Download.Comp.Favorites.Hint.Deleted"), ModMain.HintType.Finish);
                RefreshFavTargets();
                ComboTargetFav.SelectedIndex = 0;
            }
        };
        body.Items.Add(newItem);
        body.PlacementTarget = (UIElement)sender;
        body.Placement = PlacementMode.Bottom;
        body.IsOpen = true;
    }

    private void ComboTargetFav_Selected(object sender, RoutedEventArgs e)
    {
        if (ComboTargetFav.SelectedItem is null)
            return;
        Items_SetSelectAll(false);
        loader.Start(isForceRestart: true);
    }

    private void HintGetFail_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var content = Lang.Text("Download.Comp.Favorites.Dialog.GetFailed.Content") + "\r\n" + "\r\n";
        var failIds = loader.input.Except(loader.output.Select(i => i.Id).ToList()).ToList();
        foreach (var Id in failIds)
            content += $" - {Id}" + "\r\n";
        ModMain.MyMsgBox(content, Lang.Text("Download.Comp.Favorites.Dialog.GetFailed.Title"), button2: Lang.Text("Download.Comp.Favorites.Dialog.GetFailed.CopyIds"), button3: Lang.Text("Download.Comp.Favorites.Dialog.GetFailed.Remove"),
            button2Action: () => ModBase.ClipboardSet(failIds.Join("\r\n")), button3Action: () =>
            {
                foreach (var Id in failIds)
                    CurrentFavTarget.Favs.Remove(Id);
                ModComp.CompFavorites.Save();
                ModMain.Hint(Lang.Text("Download.Comp.Favorites.Hint.Removed"), ModMain.HintType.Finish);
            });
    }

    #endregion

    #region 搜索

    private bool IsSearching => !string.IsNullOrWhiteSpace(PanSearchBox.Text);

    private bool allowSearch = true;
    private List<MyListItem> searchResult = new();

    public void SearchRun(object sender, EventArgs e)
    {
        if (!allowSearch)
            return;
        if (IsSearching)
        {
            // 构造请求
            var queryList = new List<ModBase.SearchEntry<MyListItem>>();
            foreach (var Item in compItemList)
            {
                if (Item.Tag is not ModComp.CompProject)
                    continue;
                var entry = (ModComp.CompProject)Item.Tag;
                var searchSource = new List<ModBase.SearchSource>();
                searchSource.Add(new ModBase.SearchSource(entry.RawName, 1d));
                if (entry.Description is not null && !string.IsNullOrEmpty(entry.Description))
                    searchSource.Add(new ModBase.SearchSource(entry.Description, 0.4d));
                if ((entry.TranslatedName ?? "") != (entry.RawName ?? ""))
                    searchSource.Add(new ModBase.SearchSource(entry.TranslatedName, 1d));
                searchSource.Add(new ModBase.SearchSource(string.Join("", entry.Tags), 0.2d));
                queryList.Add(new ModBase.SearchEntry<MyListItem> { item = Item, searchSource = searchSource });
            }

            // 进行搜索
            searchResult = ModBase.Search(queryList, PanSearchBox.Text, 6, 0.35d).Select(r => r.item).ToList();
        }

        RefreshContent();
        RefreshCardTitle();
    }

    #endregion
}
