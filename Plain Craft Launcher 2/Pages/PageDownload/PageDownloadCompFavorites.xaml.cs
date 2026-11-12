using PCL.Core.App.Localization;
using PCL.Core.UI;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.Network;
using PCL.Network.Loaders;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace PCL;

public partial class PageDownloadCompFavorites : IPageView
{
    private readonly List<FavoriteItemViewModel> _compItemList = [];
    private List<FavoriteItemViewModel> _selectedItemList = [];

    private readonly PageDownloadCompFavoritesViewModel _vm = new();

    private bool _refreshed;

    public PageDownloadCompFavoritesViewModel ViewModel => _vm;

    public PageDownloadCompFavorites()
    {
        loader = new Loading.LoaderTask<List<string>, List<Comp.CompProject>>("CompProject Favorites",
            CompFavoritesGet, inputDelegate: LoaderInput);
        Initialized += PageDownloadCompFavorites_Inited;
        Loaded += PageDownloadCompFavorites_Loaded;
        Unloaded += (_, _) => _refreshed = false;
        InitializeComponent();
        DataContext = _vm;
        DialogServiceInstaller.Install(_vm);
        {

            // 这是选择收藏夹旁边那个图标按钮
            // 实在不想把布局写动态代码里，但是奈何龙猫的石山没办法在 XAML 里定义 Logo 属性为已有常量值
            // 还有一个很扯淡的点，同样自定义的 MyButton 能在 XAML 直接设置 Click 事件
            // 到 MyIconButton 就不行了，死活跑不了，也不知道是不是漏了什么依赖属性没写
            Btn_ManageTargetFav.SvgIcon = "lucide/settings";
        }
        // Handles
        WeakLanguageChanged.Add(this, OnLanguageChanged);
    }

    public void OnNavigatedTo(object? parameter)
    {
        RefreshOnNavigate();
    }

    public void OnNavigatedFrom()
    {
    }

    private static void OnLanguageChanged(PageDownloadCompFavorites page) => Common.RunInUi(page._RefreshCategoryTitles);

    private Comp.CompFavorites.FavData CurrentFavTarget
    {
        get
        {
            var selectedItem = (MyComboBoxItem)ComboTargetFav.SelectedItem;
            if (selectedItem is null)
            {
                Common.Log("[Favorites] 异常：未选择收藏夹");
                selectedItem = (MyComboBoxItem)ComboTargetFav.Items.GetItemAt(0);
            }

            return Comp.CompFavorites.FavoritesList
                .First(e => string.Equals(e.Id, selectedItem.Tag?.ToString(), StringComparison.OrdinalIgnoreCase));
        }
    }

    #region 加载器信息

    public readonly Loading.LoaderTask<List<string>, List<Comp.CompProject>> loader;

    private void PageDownloadCompFavorites_Inited(object sender, EventArgs e)
    {
        RefreshFavTargets();
        PageLoaderInit(Load, PanLoad, PanContent, null, loader, _ => Load_OnFinish(), LoaderInput);
    }

    private void PageDownloadCompFavorites_Loaded(object sender, EventArgs e)
    {
        RefreshOnNavigate();
    }

    private void RefreshOnNavigate()
    {
        if (_refreshed)
            return;
        _refreshed = true;
        Items_SetSelectAll(false);
        RefreshBar();
        if (loader.input is not null && loader.input.Count != CurrentFavTarget.Favs.Count) RefreshFavTargets();
    }

    private List<string> LoaderInput()
    {
        List<string> targetList = [];
        try
        {
            targetList = CurrentFavTarget.Favs.Distinct().ToList();
        }
        catch (Exception ex)
        {
            Common.Log(ex, "[Favorites] 加载收藏夹列表时出错");
        }

        return (List<string>)targetList.Clone(); // 复制而不是直接引用！
    }

    private void CompFavoritesGet(Loading.LoaderTask<List<string>, List<Comp.CompProject>> task)
    {
        task.output = Comp.CompRequest.GetCompProjectsByIds(task.input!);
    }

    #endregion

    #region UI 化 - 自适应卡片

    /// <summary>
    ///     当前参与显示的分组缓存。按工程类型复用同一个分组实例，
    ///     以免每次刷新都重建 MyCard 而丢失用户的展开/折叠状态。
    /// </summary>
    private readonly List<FavoriteGroupViewModel> _groupCache = [];

    /// <summary>
    ///     刷新收藏夹列表
    /// </summary>
    private void RefreshFavTargets()
    {
        ComboTargetFav.Items.Clear();
        foreach (var target in Comp.CompFavorites.FavoritesList ?? [])
        {
            var item = new MyComboBoxItem
            {
                Content = target.Name,
                Tag = target.Id
            };
            ComboTargetFav.Items.Add(item);
        }

        if (_vm.FavTargetSelectedIndex == -1) _vm.FavTargetSelectedIndex = 0; // 默认选择第一个
    }

    /// <summary>
    ///     按当前数据源重建分组，并整体替换 VM 上的分组集合。
    /// </summary>
    private void RefreshContent()
    {
        var dataSource = IsSearching ? _searchResult : _compItemList;

        var order = new List<int>();
        var grouped = new Dictionary<int, List<FavoriteItemViewModel>>();
        foreach (var item in dataSource)
        {
            var type = IsSearching ? -1 : item.Type;
            if (!grouped.TryGetValue(type, out var list))
            {
                grouped[type] = list = new List<FavoriteItemViewModel>();
                order.Add(type);
            }

            list.Add(item);
        }

        var newGroups = new List<FavoriteGroupViewModel>();
        foreach (var type in order)
        {
            var group = _groupCache.FirstOrDefault(e => e.CompType == type);
            if (group is null)
            {
                group = new FavoriteGroupViewModel(type);
                _groupCache.Add(group);
            }

            group.SetItems(grouped[type]); // 空分组由 SetItems 置为 Collapsed
            newGroups.Add(group);
        }

        _groupCache.Clear();
        _groupCache.AddRange(newGroups);

        if (_vm.Groups.SequenceEqual(newGroups))
            return;
        _vm.Groups = new ObservableCollection<FavoriteGroupViewModel>(newGroups);
    }

    private void _RefreshCategoryTitles()
    {
        RefreshCardTitle();
    }

    private void RefreshCardTitle()
    {
        foreach (var group in _vm.Groups)
            group.RefreshTitle();
    }

    #endregion

    #region UI 化 - 加载主逻辑

    private void Load_OnFinish()
    {
        _groupCache.Clear();
        try
        {
            _allowSearch = false;
            _vm.SearchText = string.Empty;
            _allowSearch = true;
            _compItemList.Clear();
            var someGetFail = loader.input!.Count != loader.output!.Count;
            _vm.ShowHintGetFail = someGetFail;
            foreach (var item in loader.output)
            {
                CurrentFavTarget.Notes.TryGetValue(item.Id, out var note);
                _compItemList.Add(new FavoriteItemViewModel(item, note));
            }

            if (_compItemList.Any()) // 有收藏
            {
                if (!IsSearching)
                {
                    _vm.ShowSearchBox = true;
                    _vm.ShowContentList = true;
                    _vm.ShowNoContent = false;
                }
            }
            else // 没有收藏
            {
                _vm.ShowSearchBox = false;
                _vm.ShowContentList = false;
                _vm.ShowNoContent = true;
            }

            RefreshContent();
            RefreshCardTitle();
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "可视化收藏夹列表出错",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Download.Comp.Favorites.Error.OperationFailed"));
        }
    }

    /// <summary>
    ///     条目控件生成后做一次性映射：备注 Run、编辑/删除按钮、右键菜单这些无法用 XAML 绑定表达的部分在此补齐。
    /// </summary>
    private void FavoriteItem_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not MyListItem { DataContext: FavoriteItemViewModel item } compItem)
            return;
        if (compItem.Tag is not null)
            return; // 已经映射过
        compItem.Tag = item.Project;
        var compId = item.Project.Id;

        var noteItem = new Run
        {
            Foreground = new SolidColorBrush(Color.FromRgb(0, 184, 148)),
            Text = item.NoteText
        };
        compItem.LabTitle.Inlines.Add(noteItem);
        item.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(FavoriteItemViewModel.NoteText))
                noteItem.Text = item.NoteText;
        };

        // 添加按钮
        var btnEditNote = new MyIconButton
        {
            SvgIcon = "lucide/pencil",
            ToolTip = Lang.Text("Download.Comp.Favorites.EditNote")
        };
        ToolTipService.SetPlacement(btnEditNote, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnEditNote, 30d);
        ToolTipService.SetHorizontalOffset(btnEditNote, 2d);
        btnEditNote.Click += async (button, _) =>
        {
            var ctl = button as UIElement;
            ctl?.IsEnabled = false;
            try
            {
                CurrentFavTarget.Notes.TryGetValue(compId, out var notes);
                var desiredNoteResult = await _vm.Dialog.ShowAsync(new InputDialogRequest
                {
                    Title = Lang.Text("Download.Comp.Favorites.EditNote"),
                    DefaultValue = notes ?? ""
                }).ConfigureAwait(true);
                var desiredNote = desiredNoteResult.IsConfirmed ? desiredNoteResult.InputText : null;
                if (desiredNote is not null)
                {
                    CurrentFavTarget.Notes[compId] = desiredNote;
                    item.SetNote(desiredNote);
                    Comp.CompFavorites.Save();
                }
            }
            catch (Exception ex)
            {
                Common.Log(
                    ex,
                    "[Favourites] 修改备注时发生错误",
                    Common.LogLevel.Feedback,
                    userSummary: Lang.Text("Download.Comp.Favorites.Error.OperationFailed"));
            }
            finally
            {
                ctl?.IsEnabled = true;
            }
        };

        // 删除按钮
        var btnDelete = new MyIconButton
        {
            SvgIcon = "lucide/heart-filled",
            ToolTip = Lang.Text("Download.Comp.Favorites.Action.Unfavorite")
        };
        ToolTipService.SetPlacement(btnDelete, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnDelete, 30d);
        ToolTipService.SetHorizontalOffset(btnDelete, 2d);
        btnDelete.Click += (_, _) =>
        {
            Items_CancelFavorites(item);
            RefreshContent();
            RefreshCardTitle();
            RefreshBar();
        };
        compItem.Buttons = [btnEditNote, btnDelete];

        // 操作逻辑
        compItem.MouseRightButtonUp += (_, _) => Launcher.frmMain.PageChange(
            new FormMain.PageStackData
            {
                page = FormMain.PageType.CompDetail,
                additional = (item.Project, new List<string>(), string.Empty, Comp.CompLoaderType.Any,
                    item.Project.Type, null)
            });

        // 其它事件
        compItem.Changed += ItemCheckStatusChanged;
    }

    #endregion

    #region UI 化 - 选择操作

    private int _bottomBarShownCount;

    private void RefreshBar()
    {
        var newCount = _selectedItemList.Count;
        var selected = newCount > 0;
        if (selected)
            _vm.SelectedText = Lang.Text("Download.Comp.Favorites.Hint.SelectedCount", newCount); // 取消所有选择时不更新数字
        // 更新显示状态
        if (!Animations.AniExitsAnyControl())
        {
            _vm.ContentListMargin = new Thickness(0d, 0d, 0d, selected ? 80 : 0);
            if (selected)
            {
                // 仅在数量增加时播放出现/跳跃动画
                if (_bottomBarShownCount >= newCount)
                {
                    _bottomBarShownCount = newCount;
                    return;
                }

                _bottomBarShownCount = newCount;
                // 出现/跳跃动画
                CardSelect.Visibility = Visibility.Visible;
                Animations.AniStart(
                    new[]
                    {
                        Animations.AaOpacity(CardSelect, 1d - CardSelect.Opacity, 60),
                        Animations.AaTranslateY(CardSelect, -27 - TransSelect.Y, 120,
                            ease: new Animations.AniEaseOutFluent(Animations.AniEasePower.Weak)),
                        Animations.AaTranslateY(CardSelect, 3d, 150, 120,
                            new Animations.AniEaseInoutFluent(Animations.AniEasePower.Weak)),
                        Animations.AaTranslateY(CardSelect, -1, 90, 270,
                            new Animations.AniEaseInoutFluent(Animations.AniEasePower.Weak))
                    }, "CompFavorites Sidebar");
            }
            else
            {
                // 不重复播放隐藏动画
                if (_bottomBarShownCount == 0)
                    return;
                _bottomBarShownCount = 0;
                // 隐藏动画
                Animations.AniStart(
                    new[]
                    {
                        Animations.AaOpacity(CardSelect, -CardSelect.Opacity, 90),
                        Animations.AaTranslateY(CardSelect, -10 - TransSelect.Y, 90,
                            ease: new Animations.AniEaseInFluent(Animations.AniEasePower.Weak)),
                        Animations.AaCode(() => CardSelect.Visibility = Visibility.Collapsed, after: true)
                    }, "CompFavorites Sidebar");
            }
        }
        else
        {
            Animations.AniStop("CompFavorites Sidebar");
            _bottomBarShownCount = newCount;
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

    private void ItemCheckStatusChanged(object sender, Common.RouteEventArgs e)
    {
        if (sender is not MyListItem { DataContext: FavoriteItemViewModel item })
            return;
        if (_selectedItemList.Contains(item))
            _selectedItemList.Remove(item);
        if (item.IsChecked)
            _selectedItemList.Add(item);
        RefreshBar();
    }

    private void Load_State(object sender, LoadingState state, LoadingState oldState)
    {
        switch (loader.State)
        {
            case LoadState.Failed:
                {
                    var errorMessage = "";
                    // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
                    if (loader.Error is not null)
                        errorMessage = loader.Error.Message;
                    if (errorMessage.Contains(Lang.Text("Common.Error.InvalidJson")))
                    {
                        Common.Log("[Download] 下载的工程列表 JSON 文件损坏，已自动重试", Common.LogLevel.Debug);
                        PageLoaderRestart();
                    }

                    break;
                }
        }
    }

    private void Btn_FavoritesCancel_Clicked(object sender, Common.RouteEventArgs e)
    {
        foreach (var items in _selectedItemList.Clone())
            Items_CancelFavorites(items);
        if (_compItemList.Any())
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

    private void Btn_SelectCancel_Clicked(object sender, Common.RouteEventArgs e)
    {
        Items_SetSelectAll(false);
    }

    private void Btn_FavoritesShare_Clicked(object sender, Common.RouteEventArgs e)
    {
        try
        {
            Common.ClipboardSet(
                Comp.CompFavorites.GetShareCode(_selectedItemList.Select(i => i.Project.Id)
                    .ToHashSet()));
            Items_SetSelectAll(false);
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "[CompFavourites] 分享收藏时发生错误",
                Common.LogLevel.Hint,
                userSummary: Lang.Text("Download.Comp.Favorites.Error.OperationFailed"));
        }
    }

    private async void Btn_FavoritesDownload_Clicked(object sender, Common.RouteEventArgs e)
    {
        _vm.IsBulkDownloadEnabled = false;
        try
        {
            if (!(await _vm.Dialog.ShowAsync(new TextDialogRequest
            {
                Title = Lang.Text("Download.Comp.Favorites.Dialog.BulkDownload.Title"),
                Content = Lang.Text("Download.Comp.Favorites.Dialog.BulkDownload.Content"),
                Buttons = DialogButtons.ContinueCancel,
                IsWarn = true
            }).ConfigureAwait(true)).IsConfirmed) return;
            var supportedModLoader = new List<Comp.CompLoaderType>();
            var loaderFirstSet = true;
            var hasMod = false;
            foreach (var item in _selectedItemList) // 获取共同支持的 Loading
            {
                var proj = item.Project;
                if (proj.Type == Comp.CompType.Mod)
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

            // 检查是否有共同支持的 Loading
            if (hasMod && supportedModLoader.Count == 0)
            {
                HintService.Show(Hints.Error(Lang.Text("Download.Comp.Favorites.Hint.SelectLoader")));
                return;
            }

            // 要求选择版本
            var desiredModLoader = Comp.CompLoaderType.Any;
            if (hasMod && supportedModLoader.Count > 0)
                if (supportedModLoader.Count > 0)
                {
                    var selections = supportedModLoader.Select(i => i.ToString()).ToList();
                    var selectedModLoaderDialog = await _vm.Dialog.ShowAsync(new SelectDialogRequest
                    {
                        Title = Lang.Text("Download.Comp.Favorites.Dialog.SelectLoader.Title"),
                        Options = selections.Select(x => new DialogOption { Text = x }).ToList(),
                        Buttons = DialogButtons.OkCancel,
                        HighLight = false
                    }).ConfigureAwait(true);
                    var selectedModLoaderStr = selectedModLoaderDialog.IsConfirmed
                        ? selectedModLoaderDialog.SelectedIndex
                        : null;
                    if (selectedModLoaderStr is null)
                        return;
                    desiredModLoader = supportedModLoader[(int)selectedModLoaderStr];
                }

            HintService.Show(Hints.Info(Lang.Text("Download.Comp.Favorites.Hint.LoadingVersions")));
            // 输入 Ids，输出合适版本
            var getInfoAndDownloadLoader = new List<Loading.LoaderBase>
            {
                new Loading.LoaderTask<List<string>, List<DownloadFile>>(
                    Lang.Text("Download.Comp.Favorites.LoaderName.QueryInfo"), ts =>
                    {
                        List<List<Comp.CompFile>> allFiles = [];
                        List<string> suitVersion = [];
                        var versionFirstSet = true;

                        // 获取多个工程之间支持的版本的交集
                        var finishedTasks = 0;
                        foreach (var item in ts.input!)
                            Common.RunInNewThread(() =>
                            {
                                try
                                {
                                    allFiles.Add(Comp.CompFilesGet(item, Comp.CompRequest.IsFromCurseForge(item))
                                        .Where(i => i.Type != Comp.CompType.Mod || i.ModLoaders.Contains(desiredModLoader))
                                        .ToList());
                                }
                                catch (Exception ex)
                                {
                                    Common.Log(
                                        ex,
                                        $"获取 {item} 的下载信息失败",
                                        Common.LogLevel.Hint,
                                        userSummary: Lang.Text("Download.Comp.Favorites.Error.OperationFailed"));
                                }
                                finally
                                {
                                    finishedTasks += 1;
                                }
                            });
                        while (finishedTasks != ts.input.Count)
                            Thread.Sleep(200);
                        // 求取共同的版本
                        foreach (var item in allFiles)
                        {
                            var current = GetAllVersionList(item.Select(i => i.GameVersions).ToList());
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
                                HintService.Show(Hints.Error(Lang.Text("Download.Comp.Favorites.Hint.NoResource")));
                                ts.Abort();
                                return;
                            }
                            // 要求用户选择希望下载的版本
                        }

                        int? selectedVersion = 0;
                        Common.RunInUiWait(() =>
                        {
                            List<IMyRadio> selection = [];
                            foreach (var i in suitVersion)
                                selection.Add(new MyRadioBox { Text = i });
                            selectedVersion = _vm.Dialog.Show(new SelectDialogRequest
                            {
                                Title = Lang.Text("Download.Comp.Favorites.Dialog.SelectVersion.Title"),
                                Options = DialogOptions.From(selection),
                                Buttons = DialogButtons.OkCancel,
                                HighLight = false,
                                ForceWait = true
                            }).Result?.SelectedIndex;
                            if (selectedVersion is null) ts.Abort();
                        });
                        string selectedVersionStr = suitVersion[(int)selectedVersion];
                        HintService.Show(Hints.Info(Lang.Text("Download.Comp.Favorites.Hint.SelectSaveLocation", selectedVersionStr)));
                        var saveFolder = SystemDialogs.SelectFolder();
                        if (string.IsNullOrWhiteSpace(saveFolder))
                        {
                            ts.Abort();
                            return;
                        }

                        ;
                        // 获取有期望版本号的文件
                        List<DownloadFile> res = [];
                        foreach (var target in allFiles)
                        {
                            // 按照发布日期排序
                            var finalChoices = target.Where(i => i.GameVersions.Contains(selectedVersionStr)).ToList();
                            finalChoices.Sort((a, b) => a.ReleaseDate > b.ReleaseDate);
                            // 获取文件名
                            var targetProject = Comp.compProjectCache[finalChoices[0].ProjectId];
                            var fileName = Comp.CompFileNameGet(targetProject, finalChoices[0]);
                            // 选择最新版本进行下载
                            res.Add(finalChoices[0].ToNetFile(System.IO.Path.Combine(saveFolder, fileName)));
                        }

                        ts.output = res;
                        return;

                        // 工程支持的全部版本获取
                        List<string> GetAllVersionList(List<List<string>> ls)
                        {
                            var allVersionList = new List<string>();
                            foreach (var i in ls) allVersionList.AddRange(i);

                            return allVersionList.Distinct().ToList();
                        }
                    })
                {
                    ProgressWeight = 2d
                },
                new LoaderDownload(
                    Lang.Text("Download.Comp.Favorites.LoaderName.BatchDownloadSuitable"),
                    []
                )
                {
                    ProgressWeight = 8d
                }
            };

            var checkLoader = new Loading.LoaderCombo<List<string>>(
                Lang.Text("Download.Comp.Favorites.LoaderName.BatchDownload", Common.GetUuid()),
                getInfoAndDownloadLoader
            )
            {
                OnStateChanged = DownloadUi.LoaderStateChangedHintOnly
            };

            checkLoader.Start(_selectedItemList.Select(i => i.Project.Id).ToList());
            Loading.LoaderTaskbarAdd(checkLoader);
            Launcher.frmMain!.BtnExtraDownload.ShowRefresh();
            Launcher.frmMain.BtnExtraDownload.Ribble();
            Items_SetSelectAll(false);
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "批量下载收藏时发生错误",
                Common.LogLevel.Hint,
                userSummary: Lang.Text("Download.Comp.Favorites.Error.OperationFailed"));
        }
        finally
        {
            _vm.IsBulkDownloadEnabled = true;
        }
    }

    private void Items_SetSelectAll(bool targetStatus)
    {
        if (IsSearching)
            foreach (var item in _searchResult)
                item.IsChecked = targetStatus;
        else
            foreach (var item in _compItemList)
                item.IsChecked = targetStatus;
        _selectedItemList = _compItemList.Where(e => e.IsChecked).ToList();
    }

    private void Items_CancelFavorites(FavoriteItemViewModel item)
    {
        try
        {
            _compItemList.Remove(item);
            if (_selectedItemList.Contains(item))
                _selectedItemList.Remove(item);
            if (_searchResult.Contains(item))
                _searchResult.Remove(item);
            CurrentFavTarget.Favs.Remove(item.Project.Id);
            Comp.CompFavorites.Save();
            if (!_compItemList.Any())
                Launcher.frmDownloadCompFavorites!.PageLoaderRestart();
        }
        catch (Exception ex)
        {
            Common.Log(ex, "[CompFavourites] 移除收藏时发生错误");
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
            SvgIcon = "lucide/share-2"
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

                Common.ClipboardSet(Comp.CompFavorites.GetShareCode(CurrentFavTarget.Favs));
            }
            catch (Exception ex)
            {
                Common.Log(
                    ex,
                    "[Favourites] 分享收藏时发生错误",
                    Common.LogLevel.Hint,
                    userSummary: Lang.Text("Download.Comp.Favorites.Error.OperationFailed"));
            }
        };
        body.Items.Add(newItem);
        newItem = new MyMenuItem
        {
            Header = Lang.Text("Download.Comp.Favorites.Menu.Import"),
            SvgIcon = "lucide/circle-plus"
        };
        newItem.Click += async (sender, _) =>
        {
            var ctl = sender as Control;
            ctl?.IsEnabled = false;
            try
            {
                var clipDataDialog = await _vm.Dialog.ShowAsync(new InputDialogRequest
                {
                    Title = Lang.Text("Download.Comp.Favorites.Dialog.Import.Input"),
                    Content = "",
                    HintText = Lang.Text("Download.Comp.Favorites.Dialog.Import.Hint"),
                    HighLight = false
                }).ConfigureAwait(true);
                var clipData = clipDataDialog.IsConfirmed ? clipDataDialog.InputText : null;
                if (string.IsNullOrWhiteSpace(clipData)) return;
                var newFavs = Comp.CompFavorites.GetIdsByShareCode(clipData);
                if (newFavs.Count == 0)
                {
                    HintService.Show(Hints.Info(Lang.Text("Download.Comp.Favorites.Hint.NothingShared")));
                    return;
                }

                var userWant = (await _vm.Dialog.ShowAsync(new TextDialogRequest
                {
                    Title = Lang.Text("Common.Dialog.Title"),
                    Content = Lang.Text("Download.Comp.Favorites.Dialog.Import.Type"),
                    Buttons = DialogButtons.From(
                        DialogButton.Create(Lang.Text("Download.Comp.Favorites.Dialog.Import.NewFolder")),
                        DialogButton.Create(Lang.Text("Download.Comp.Favorites.Dialog.Import.CurrentFolder")))
                }).ConfigureAwait(true)).Button;
                switch (userWant)
                {
                    case 1:
                        {
                            var newFavNameDialog = await _vm.Dialog.ShowAsync(new InputDialogRequest
                            {
                                Title = Lang.Text("Download.Comp.Favorites.Dialog.Import.New"),
                                Content = Lang.Text("Download.Comp.Favorites.Dialog.NewPrompt"),
                                HighLight = false
                            }).ConfigureAwait(true);
                            var newFavName = newFavNameDialog.IsConfirmed ? newFavNameDialog.InputText : null;
                            if (string.IsNullOrWhiteSpace(newFavName)) return;
                            Comp.CompFavorites.FavoritesList!.Add(Comp.CompFavorites.GetNewFav(newFavName, newFavs));
                            Comp.CompFavorites.Save();
                            RefreshFavTargets();
                            _vm.FavTargetSelectedIndex = ComboTargetFav.Items.Count - 1;
                            break;
                        }
                    case 2:
                        {
                            newFavs.ToList().ForEach(x => CurrentFavTarget.Favs.Add(x));
                            Comp.CompFavorites.Save();
                            loader.Start(isForceRestart: true);
                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                Common.Log(
                    ex,
                    "解析分享数据失败",
                    Common.LogLevel.Hint,
                    userSummary: Lang.Text("Download.Comp.Favorites.Error.OperationFailed"));
            }
            finally
            {
                ctl?.IsEnabled = true;
            }
        };
        body.Items.Add(newItem);
        newItem = new MyMenuItem
        {
            Header = Lang.Text("Download.Comp.Favorites.Menu.New"),
            SvgIcon = "lucide/folder-plus"
        };
        newItem.Click += async (sender, _) =>
        {
            var ctl = sender as Control;
            ctl?.IsEnabled = false;
            try
            {
                var newFavNameDialog = await _vm.Dialog.ShowAsync(new InputDialogRequest
                {
                    Title = Lang.Text("Download.Comp.Favorites.Menu.New"),
                    Content = Lang.Text("Download.Comp.Favorites.Dialog.NewPrompt"),
                    HighLight = false
                }).ConfigureAwait(true);
                var newFavName = newFavNameDialog.IsConfirmed ? newFavNameDialog.InputText : null;
                if (string.IsNullOrWhiteSpace(newFavName))
                    return;
                Comp.CompFavorites.FavoritesList!.Add(Comp.CompFavorites.GetNewFav(newFavName, null));
                Comp.CompFavorites.Save();
                RefreshFavTargets();
                _vm.FavTargetSelectedIndex = ComboTargetFav.Items.Count - 1;
            }
            catch (Exception ex)
            {
                Common.Log(
                    ex,
                    "新建收藏夹时发生错误",
                    Common.LogLevel.Feedback,
                    userSummary: Lang.Text("Download.Comp.Favorites.Error.OperationFailed"));
            }
            finally
            {
                ctl?.IsEnabled = true;
            }
        };
        body.Items.Add(newItem);
        newItem = new MyMenuItem
        {
            Header = Lang.Text("Download.Comp.Favorites.Menu.Rename"),
            SvgIcon = "lucide/pencil"
        };
        newItem.Click += async (sender, _) =>
        {
            var ctl = sender as Control;
            ctl?.IsEnabled = false;
            try
            {
                var newNameDialog = await _vm.Dialog.ShowAsync(new InputDialogRequest
                {
                    Title = Lang.Text("Download.Comp.Favorites.Dialog.Rename.Title"),
                    Content = "",
                    DefaultValue = CurrentFavTarget.Name,
                    HighLight = false
                }).ConfigureAwait(true);
                var newName = newNameDialog.IsConfirmed ? newNameDialog.InputText : null;
                if (string.IsNullOrWhiteSpace(newName) || CurrentFavTarget.Name == newName)
                    return;
                CurrentFavTarget.Name = newName;
                Comp.CompFavorites.Save();
                RefreshFavTargets();
            }
            catch (Exception ex)
            {
                Common.Log(
                    ex,
                    "重命名收藏夹时发生错误",
                    Common.LogLevel.Feedback,
                    userSummary: Lang.Text("Download.Comp.Favorites.Error.OperationFailed"));
            }
            finally
            {
                ctl?.IsEnabled = true;
            }
        };
        body.Items.Add(newItem);
        newItem = new MyMenuItem
        {
            Header = Lang.Text("Download.Comp.Favorites.Menu.Delete"),
            SvgIcon = "lucide/trash-2"
        };
        newItem.Click += async (sender, _) =>
        {
            var ctl = sender as Control;
            ctl?.IsEnabled = false;
            try
            {
                if (Comp.CompFavorites.FavoritesList!.Count == 1)
                {
                    HintService.Show(Hints.Info(Lang.Text("Download.Comp.Favorites.Hint.LastCollection")));
                    return;
                }

                var content = Lang.Text("Download.Comp.Favorites.Dialog.Delete.Confirm", CurrentFavTarget.Name, CurrentFavTarget.Favs.Count, CurrentFavTarget.Id);
                var res = (await _vm.Dialog.ShowAsync(new TextDialogRequest
                {
                    Title = Lang.Text("Download.Comp.Favorites.Dialog.Delete.Title"),
                    Content = content,
                    Buttons = DialogButtons.From(
                        DialogButton.Create(Lang.Text("Common.Option.No")),
                        DialogButton.Create(Lang.Text("Common.Option.Yes")),
                        DialogButton.Create(Lang.Text("Common.Option.No"))),
                    IsWarn = true
                }).ConfigureAwait(true)).Button;
                if (res == 2)
                {
                    Comp.CompFavorites.FavoritesList.Remove(CurrentFavTarget);
                    Comp.CompFavorites.Save();
                    HintService.Show(Hints.Success(Lang.Text("Download.Comp.Favorites.Hint.Deleted")));
                    RefreshFavTargets();
                    _vm.FavTargetSelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                Common.Log(
                    ex,
                    "删除收藏夹时发生错误",
                    Common.LogLevel.Feedback,
                    userSummary: Lang.Text("Download.Comp.Favorites.Error.OperationFailed"));
            }
            finally
            {
                ctl?.IsEnabled = true;
            }
        };
        body.Items.Add(newItem);
        body.PlacementTarget = (UIElement)sender;
        body.Placement = PlacementMode.Bottom;
        body.IsOpen = true;
    }

    private void ComboTargetFav_Selected(object sender, SelectionChangedEventArgs e)
    {
        if (ComboTargetFav.SelectedItem is null)
            return;
        // 初次填充下拉项时也会触发 SelectionChanged，此时尚未完成加载，不应重启加载器
        if (!_allowSearch)
            return;
        Items_SetSelectAll(false);
        loader.Start(isForceRestart: true);
    }

    private void HintGetFail_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var content = $"{Lang.Text("Download.Comp.Favorites.Dialog.GetFailed.Content")}\r\n\r\n";
        var failIds = loader.input!.Except(loader.output!.Select(i => i.Id).ToList()).ToList();
        foreach (var id in failIds)
            content += $" - {id}" + "\r\n";
        _ = _vm.Dialog.ShowAsync(new TextDialogRequest
        {
            Title = Lang.Text("Download.Comp.Favorites.Dialog.GetFailed.Title"),
            Content = content,
            Buttons = DialogButtons.From(
                DialogButton.Create(Lang.Text("Common.Action.Confirm")),
                DialogButton.Create(Lang.Text("Download.Comp.Favorites.Dialog.GetFailed.CopyIds"),
                    () => Common.ClipboardSet(failIds.Join("\r\n"))),
                DialogButton.Create(Lang.Text("Download.Comp.Favorites.Dialog.GetFailed.Remove"), () =>
                {
                    foreach (var id in failIds)
                        CurrentFavTarget.Favs.Remove(id);
                    Comp.CompFavorites.Save();
                    HintService.Show(Hints.Success(Lang.Text("Download.Comp.Favorites.Hint.Removed")));
                })),
            ForceWait = true
        });
    }

    #endregion

    #region 搜索

    private bool IsSearching => !string.IsNullOrWhiteSpace(_vm.SearchText);

    private bool _allowSearch = true;
    private List<FavoriteItemViewModel> _searchResult = [];

    public void SearchRun(object sender, EventArgs e)
    {
        if (!_allowSearch)
            return;
        if (IsSearching)
        {
            // 构造请求
            var queryList = new List<SearchService.SearchEntry<FavoriteItemViewModel>>();
            foreach (var item in _compItemList)
            {
                var entry = item.Project;
                var searchSource = new List<SearchService.SearchSource> { new(entry.RawName) };
                if (!string.IsNullOrEmpty(entry.Description))
                    searchSource.Add(new SearchService.SearchSource(entry.Description, 0.4d));
                if (entry.TranslatedName != entry.RawName)
                    searchSource.Add(new SearchService.SearchSource(entry.TranslatedName));
                searchSource.Add(new SearchService.SearchSource(string.Join("", entry.Tags), 0.2d));
                queryList.Add(new SearchService.SearchEntry<FavoriteItemViewModel>
                { item = item, searchSource = searchSource });
            }

            // 进行搜索
            _searchResult = SearchService.Search(queryList, _vm.SearchText, SearchService.MaxLocalSearchDepth, 0.35d)
                .Select(r => r.item)
                .Where(i => i is not null)
                .ToList();
        }

        RefreshContent();
        RefreshCardTitle();
    }

    #endregion
}
