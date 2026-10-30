using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.VisualBasic.FileIO;
using PCL.Core.App;
using PCL.Core.UI;
using PCL.Core.UI.Theme;
using PCL.Network;
using PCL.Network.Loaders;
using FileSystem = Microsoft.VisualBasic.FileSystem;
using PCL.Core.App.Localization;
using SortMethod = PCL.PageInstanceSavesDatapackViewModel.DatapackSortMethod;
using FilterType = PCL.PageInstanceSavesDatapackViewModel.DatapackFilterType;

namespace PCL;

public partial class PageInstanceSavesDatapack : IRefreshable, IPageView
{
    #region 初始化

    private readonly PageInstanceSavesDatapackViewModel _vm = new();
    private readonly MyLocalCompItem.SwipeSelect currentSwipSelect;
    private readonly Dictionary<string, MyLocalCompItem> datapackItems = new();
    private readonly object sortLock = new();
    private bool _refreshed;
    private bool _syncingFilter;
    private bool isLoad;
    private int bottomBarShownCount;

    public PageInstanceSavesDatapackViewModel ViewModel => _vm;

    public PageInstanceSavesDatapack()
    {
        currentSwipSelect = new MyLocalCompItem.SwipeSelect { TargetFrm = this };

        InitializeComponent();
        DataContext = _vm;
        Unloaded += Page_Unloaded;
        Loaded += (_, _) => RefreshOnNavigate();
        LoaderInit();
        PageExit += UnselectedAllWithAnimation;
        // Handles
        Load.Click += Load_Click;
        BtnManageOpen.Click += BtnManageOpen_Click;
        BtnHintOpen.Click += BtnManageOpen_Click;
        BtnManageSelectAll.Click += BtnManageSelectAll_Click;
        BtnManageInstall.Click += BtnManageInstall_Click;
        BtnHintInstall.Click += BtnManageInstall_Click;
        BtnManageDownload.Click += BtnManageDownload_Click;
        BtnHintDownload.Click += BtnManageDownload_Click;
        BtnManageInfoExport.Click += BtnManageInfoExport_Click;
        Load.StateChanged += (_, _, _) => UnselectedAllWithAnimation();
        SearchBox.PreviewKeyDown += SearchBox_PreviewKeyDown;
        BtnFilterAll.Check += ChangeFilter;
        BtnFilterCanUpdate.Check += ChangeFilter;
        BtnFilterDisabled.Check += ChangeFilter;
        BtnFilterEnabled.Check += ChangeFilter;
        BtnFilterError.Check += ChangeFilter;
        BtnSort.Click += BtnSortClick;
        BtnSelectEnable.Click += BtnSelectEnable_Click;
        BtnSelectDisable.Click += BtnSelectDisable_Click;
        BtnSelectUpdate.Click += BtnSelectUpdate_Click;
        BtnSelectDelete.Click += BtnSelectDelete_Click;
        BtnSelectCancel.Click += BtnSelectCancel_Click;
        BtnSelectFavorites.Click += BtnSelectFavorites_Click;
        BtnSelectShare.Click += BtnSelectShare_Click;
        SearchBox.TextChanged += SearchRun;
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        _refreshed = false;
        _vm.ClearFileInfoCache();
    }

    void IPageView.OnNavigatedTo(object? parameter)
    {
        RefreshOnNavigate();
    }

    void IPageView.OnNavigatedFrom()
    {
    }

    private void RefreshOnNavigate()
    {
        if (ModMain.frmMain.pageLast.page != FormMain.PageType.CompDetail)
            PanBack.ScrollToHome();

        if (_refreshed)
            return;
        _refreshed = true;

        ModAnimation.AniBeginControl();
        _vm.ClearSelection();
        ReloadDatapackFileList();
        ChangeAllSelected(false);
        ModAnimation.AniEndControl();

        // 非重复加载部分
        if (isLoad)
            return;
        isLoad = true;

        ModMain.frmMain.KeyDown += FrmMain_KeyDown;
        // 调整按钮边距（这玩意儿没法从 XAML 改）
        foreach (MyRadioButton Btn in PanFilter.Children)
            Btn.LabText.Margin = new Thickness(-2, 0d, 8d, 0d);
    }

    private ModLocalComp.CompLocalLoaderData GetRequireLoaderData()
    {
        var res = new ModLocalComp.CompLocalLoaderData();
        res.gameVersion = PageInstanceLeft.McInstance;
        res.frm = null;
        res.loaders = new[] { ModComp.CompLoaderType.Minecraft }.ToList();
        res.compPath = Path.Combine(PageInstanceSavesLeft.currentSave, "datapacks");
        res.compType = ModComp.CompType.DataPack;
        return res;
    }

    /// <summary>
    ///     刷新数据包列表。
    /// </summary>
    public void ReloadDatapackFileList(bool forceReload = false)
    {
        if (LoaderRun(forceReload
                ? ModLoader.LoaderFolderRunType.ForceRun
                : ModLoader.LoaderFolderRunType.RunOnUpdated))
        {
            ModBase.Log("[System] 已刷新数据包列表");
            _vm.ClearFileInfoCache();

            ModBase.RunInUi(() =>
            {
                _vm.Filter = FilterType.All;
                SyncFilterRadios();
                PanBack.ScrollToHome();
                SearchBox.Text = "";
                _vm.SearchText = "";
                RefreshUI();
            });
        }
    }

    void IRefreshable.Refresh()
    {
        Refresh();
    }

    public void Refresh()
    {
        ModMain.frmInstanceSavesDatapack.ReloadDatapackFileList(true);
        ModBase.Log("[Datapack] 刷新数据包列表");
    }

    private void LoaderInit()
    {
        PageLoaderInit(Load, PanLoad, PanAllBack, null, ModLocalComp.compResourceListLoader,
            _ => LoadUIFromLoaderOutput(), () => ModComp.CompType.DataPack, false);
    }

    private void Load_Click(object sender, MouseButtonEventArgs e)
    {
        if (ModLocalComp.compResourceListLoader.State == ModBase.LoadState.Failed)
            LoaderRun(ModLoader.LoaderFolderRunType.ForceRun);
    }

    public bool LoaderRun(ModLoader.LoaderFolderRunType type)
    {
        var loadPath = Path.Combine(PageInstanceSavesLeft.currentSave, "datapacks");
        return ModLoader.LoaderFolderRun(ModLocalComp.compResourceListLoader, loadPath, type,
            loaderInput: GetRequireLoaderData());
    }

    #endregion

    #region UI 化

    protected override void RefreshLocalization()
    {
        _vm.SetSortMethod(_vm.SortMethod);
        foreach (var item in datapackItems.Values)
            item.Refresh();
        RefreshBars();
    }

    private void LoadUIFromLoaderOutput()
    {
        try
        {
            _vm.ShowDatapacks = ModLocalComp.compResourceListLoader.output.Any();
            if (!_vm.ShowDatapacks)
                return;

            datapackItems.Clear();
            var itemsToShow = ModLocalComp.compResourceListLoader.output.ToList();

            foreach (var DatapackEntity in itemsToShow)
                datapackItems[DatapackEntity.RawPath] = BuildLocalCompItem(DatapackEntity);

            ModBase.RunInUi(() =>
            {
                _vm.Filter = FilterType.All;
                SyncFilterRadios();
                SearchBox.Text = "";
                _vm.SetSortMethod(SortMethod.CompName);
                RefreshUI();
            });
        }
        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                "加载数据包列表 UI 失败",
                ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Instance.Saves.Error.OperationFailed"));
        }
    }

    private MyLocalCompItem BuildLocalCompItem(ModLocalComp.LocalCompFile entry)
    {
        try
        {
            ModAnimation.AniBeginControl();
            var newItem = new MyLocalCompItem
            {
                SnapsToDevicePixels = true,
                Entry = entry,
                buttonHandler = BuildLocalCompItemBtnHandler,
                Checked = _vm.SelectedDatapacks.Contains(entry.RawPath)
            };
            newItem.CurrentSwipe = currentSwipSelect;
            newItem.Tags = entry.Tags;
            entry.OnCompUpdate += _ => newItem.Refresh();
            newItem.Refresh();
            ModAnimation.AniEndControl();
            return newItem;
        }
        catch (Exception ex)
        {
            ModAnimation.AniEndControl();
            ModBase.Log(ex, $"创建 UI 项失败：{entry.RawPath}");
            throw;
        }
    }

    private void BuildLocalCompItemBtnHandler(MyLocalCompItem sender, EventArgs e)
    {
        // 点击事件
        sender.Changed += (ss, e) => CheckChanged((MyLocalCompItem)ss, e);

        // 文件项的点击事件：切换选中状态
        sender.Click += (ss, e) =>
        {
            var s = (MyLocalCompItem)ss;
            s.Checked = !s.Checked;
        };

        // 图标按钮
        var btnOpen = new MyIconButton { LogoScale = 1.05d, SvgIcon = "lucide/folder-open", Tag = sender };
        btnOpen.TrySetLocalizedValue("ToolTip", "Instance.Saves.OpenFileLocation");
        ToolTipService.SetPlacement(btnOpen, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnOpen, 30d);
        ToolTipService.SetHorizontalOffset(btnOpen, 2d);
        btnOpen.Click += (sender, e) => Open_Click((MyIconButton)sender, e);

        var btnCont = new MyIconButton { LogoScale = 1d, SvgIcon = "lucide/info", Tag = sender };
        btnCont.TrySetLocalizedValue("ToolTip", "Instance.Saves.Detail");
        ToolTipService.SetPlacement(btnCont, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnCont, 30d);
        ToolTipService.SetHorizontalOffset(btnCont, 2d);
        btnCont.Click += Info_Click;
        sender.MouseRightButtonUp += Info_Click;

        var btnDelete = new MyIconButton { LogoScale = 1d, SvgIcon = "lucide/trash-2", Tag = sender };
        btnDelete.TrySetLocalizedValue("ToolTip", "Common.Action.Delete");
        ToolTipService.SetPlacement(btnDelete, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnDelete, 30d);
        ToolTipService.SetHorizontalOffset(btnDelete, 2d);
        btnDelete.Click += (sender, e) => Delete_Click((MyIconButton)sender, e);

        if (sender.Entry.State == ModLocalComp.LocalCompFile.LocalFileStatus.Fine)
        {
            var btnDisable = new MyIconButton { LogoScale = 1d, SvgIcon = "lucide/circle-minus", Tag = sender };
            btnDisable.TrySetLocalizedValue("ToolTip", "Instance.Resource.Disable");
            ToolTipService.SetPlacement(btnDisable, PlacementMode.Center);
            ToolTipService.SetVerticalOffset(btnDisable, 30d);
            ToolTipService.SetHorizontalOffset(btnDisable, 2d);
            btnDisable.Click += (ss, e) => Disable_Click((MyIconButton)ss, e);
            sender.Buttons = new[] { btnCont, btnOpen, btnDisable, btnDelete };
        }
        else if (sender.Entry.State == ModLocalComp.LocalCompFile.LocalFileStatus.Disabled)
        {
            var btnEnable = new MyIconButton { LogoScale = 1d, SvgIcon = "lucide/circle-check", Tag = sender };
            btnEnable.TrySetLocalizedValue("ToolTip", "Instance.Resource.Enable");
            ToolTipService.SetPlacement(btnEnable, PlacementMode.Center);
            ToolTipService.SetVerticalOffset(btnEnable, 30d);
            ToolTipService.SetHorizontalOffset(btnEnable, 2d);
            btnEnable.Click += (ss, e) => Enable_Click((MyIconButton)ss, e);
            sender.Buttons = new[] { btnCont, btnOpen, btnEnable, btnDelete };
        }
        else
        {
            sender.Buttons = new[] { btnCont, btnOpen, btnDelete };
        }
    }

    /// <summary>
    ///     刷新整个 UI。
    /// </summary>
    public void RefreshUI()
    {
        if (PanList is null)
            return;

        ModAnimation.AniBeginControl();
        _vm.Apply();
        if (_vm.VisibleEntries.Count > 0)
        {
            PanList.Children.Clear();
            foreach (var TargetDatapack in _vm.VisibleEntries)
            {
                if (!datapackItems.TryGetValue(TargetDatapack.RawPath, out var item))
                    continue;

                // 确保元素没有父容器，避免重复添加异常
                if (item.Parent is not null) ((Panel)item.Parent).Children.Remove(item);

                ModStyle.MinecraftFormatter.SetColorfulTextLab(item.LabTitle.Text, item.LabTitle,
                    ThemeService.IsDarkMode);
                ModStyle.MinecraftFormatter.SetColorfulTextLab(item.LabInfo.Text, item.LabInfo,
                    ThemeService.IsDarkMode);
                item.Checked = _vm.SelectedDatapacks.Contains(TargetDatapack.RawPath); // 更新选中状态
                PanList.Children.Add(item);
            }
        }

        ModAnimation.AniEndControl();
        RefreshBars();
    }

    /// <summary>
    ///     刷新顶栏和底栏显示。
    /// </summary>
    public void RefreshBars()
    {
        _vm.RefreshBarState();
        var selected = _vm.SelectedCount > 0;

        if (!ModAnimation.AniExitsAnyControl())
        {
            PanListBack.Margin = new Thickness(0d, 0d, 0d, selected ? 95 : 15);
            if (selected)
            {
                // 仅在数量增加时播放出现/跳跃动画
                if (bottomBarShownCount >= _vm.SelectedCount)
                {
                    bottomBarShownCount = _vm.SelectedCount;
                    return;
                }

                bottomBarShownCount = _vm.SelectedCount;
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
                    }, "Datapack Sidebar");
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
                    }, "Datapack Sidebar");
            }
        }
        else
        {
            ModAnimation.AniStop("Datapack Sidebar");
            bottomBarShownCount = _vm.SelectedCount;
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

    #region 管理

    /// <summary>
    ///     打开 datapacks 文件夹。
    /// </summary>
    private void BtnManageOpen_Click(object sender, EventArgs e)
    {
        try
        {
            var datapackPath = Path.Combine(PageInstanceSavesLeft.currentSave, "datapacks");
            Directory.CreateDirectory(datapackPath);
            ModBase.OpenExplorer(datapackPath);
        }
        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                "打开 datapacks 文件夹失败",
                ModBase.LogLevel.Msgbox,
                userSummary: Lang.Text("Instance.Saves.Error.OperationFailed"));
        }
    }

    /// <summary>
    ///     全选。
    /// </summary>
    private void BtnManageSelectAll_Click(object sender, MouseButtonEventArgs e)
    {
        ChangeAllSelected(_vm.SelectedCount < PanList.Children.Count);
    }

    /// <summary>
    ///     安装数据包。
    /// </summary>
    private async void BtnManageInstall_Click(object sender, MouseButtonEventArgs e)
    {
        var ctl = sender as Control;
        if (ctl is not null) ctl.IsEnabled = false;
        try
        {
            var fileList = SystemDialogs.SelectFiles(
                Lang.Text("Instance.Saves.Datapack.Install.FileDialog.Filter"),
                Lang.Text("Instance.Saves.Datapack.Install.FileDialog.Title"));
            if (fileList is null || fileList.Length == 0)
                return;
            await PageInstanceSavesDatapackViewModel.InstallFilesAsync(fileList);
            Refresh();
        }
        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                "复制数据包文件失败",
                ModBase.LogLevel.Msgbox,
                userSummary: Lang.Text("Instance.Saves.Error.OperationFailed"));
        }
        finally
        {
            if (ctl is not null) ctl.IsEnabled = true;
        }
    }

    /// <summary>
    ///     安装数据包文件。
    /// </summary>
    public static void InstallDatapackFiles(IEnumerable<string> filePathList)
    {
        _ = PageInstanceSavesDatapackViewModel.InstallFilesAsync(filePathList.ToList());
    }

    /// <summary>
    ///     下载数据包。
    /// </summary>
    private void BtnManageDownload_Click(object sender, MouseButtonEventArgs e)
    {
        var datapackPath = Path.Combine(PageInstanceSavesLeft.currentSave, "datapacks");
        Directory.CreateDirectory(datapackPath);
        PageDownloadCompDetail.cachedFolder[ModComp.CompType.DataPack] = datapackPath;
        ModMain.frmMain.PageChange(FormMain.PageType.Download, FormMain.PageSubType.DownloadDataPack);
        PageComp.targetVersion = PageInstanceLeft.McInstance; // 将当前实例设置为筛选器
    }

    /// <summary>
    ///     导出信息。
    /// </summary>
    private async void BtnManageInfoExport_Click(object sender, MouseButtonEventArgs e)
    {
        var ctl = sender as Control;
        if (ctl is not null) ctl.IsEnabled = false;
        try
        {
            var choice = await ModMain.MyMsgBoxAsync(
                Lang.Text("Instance.Saves.Datapack.Export.Mode.Message"),
                Lang.Text("Instance.Resource.Export.Mode.Title"), Lang.Text("Instance.Resource.Export.Mode.Txt"),
                Lang.Text("Instance.Resource.Export.Mode.Csv"), Lang.Text("Common.Action.Cancel"));
            if (choice is not (1 or 2))
                return;

            var isCsv = choice == 2;
            await _vm.EnsureFileInfoAsync(ModLocalComp.compResourceListLoader.output.Select(entry => entry.path));
            var content = _vm.BuildExportContent(isCsv);
            var savePath = SystemDialogs.SelectSaveFile(
                Lang.Text("Instance.Resource.Export.SelectSaveLocation"),
                ModBase.GetFolderNameFromPath(PageInstanceSavesLeft.currentSave) +
                (isCsv ? "的数据包信息.csv" : "的数据包信息.txt"),
                Lang.Text("Instance.Resource.Export.FilesFilter"));
            if (string.IsNullOrWhiteSpace(savePath))
                return;
            await _vm.WriteExportFileAsync(savePath, content);
            ModBase.OpenExplorer(savePath);
        }
        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                "导出数据包信息失败",
                ModBase.LogLevel.Msgbox,
                userSummary: Lang.Text("Instance.Saves.Error.OperationFailed"));
        }
        finally
        {
            if (ctl is not null) ctl.IsEnabled = true;
        }
    }

    #endregion

    #region 选择

    // 单项切换选择状态
    public void CheckChanged(MyLocalCompItem sender, ModBase.RouteEventArgs e)
    {
        if (ModAnimation.AniExitsAnyControl())
            return;
        _vm.SetSelected(sender.Entry.RawPath, sender.Checked);
        RefreshBars();
    }

    // 切换所有项的选择状态
    private void ChangeAllSelected(bool value)
    {
        ModAnimation.AniBeginControl();
        _vm.ClearSelection();
        foreach (var Item in datapackItems.Values)
        {
            var shouldSelected = value && PanList.Children.Contains(Item);
            Item.Checked = shouldSelected;
            if (shouldSelected)
                _vm.SetSelected(Item.Entry.RawPath, true);
        }

        ModAnimation.AniEndControl();
        RefreshBars();
    }

    private void UnselectedAllWithAnimation()
    {
        var cacheAniControlEnabled = ModAnimation.AniResetControl(0);
        ChangeAllSelected(false);
        ModAnimation.AniRestoreControl(cacheAniControlEnabled);
    }

    private void FrmMain_KeyDown(object sender, KeyEventArgs e)
    {
        if (!ReferenceEquals(ModMain.frmMain.pageRight, this))
            return;
        if ((Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) && e.Key == Key.A)
            ChangeAllSelected(true);
    }

    private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Ctrl + A 会被搜索框捕获，导致无法全选，所以在按下 Ctrl + A 时转移焦点以便捕获
        if (SearchBox.Text.Any())
            return;
        if ((Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) && e.Key == Key.A)
            PanBack.Focus();
    }

    #endregion

    #region 筛选

    private void SyncFilterRadios()
    {
        _syncingFilter = true;
        try
        {
            switch (_vm.Filter)
            {
                case FilterType.All:
                {
                    BtnFilterAll.Checked = true;
                    break;
                }
                case FilterType.Enabled:
                {
                    BtnFilterEnabled.Checked = true;
                    break;
                }
                case FilterType.Disabled:
                {
                    BtnFilterDisabled.Checked = true;
                    break;
                }
                case FilterType.CanUpdate:
                {
                    BtnFilterCanUpdate.Checked = true;
                    break;
                }

                default:
                {
                    BtnFilterError.Checked = true;
                    break;
                }
            }
        }
        finally
        {
            _syncingFilter = false;
        }
    }

    // 点击筛选项触发的改变
    private void ChangeFilter(MyRadioButton sender, bool raiseByMouse)
    {
        if (_syncingFilter)
            return;
        _vm.Filter = (FilterType)Convert.ToInt32(sender.Tag);
        RefreshUI();
        DoSort();
    }

    #endregion

    #region 排序

    private void SetSortMethod(SortMethod target)
    {
        _vm.SetSortMethod(target);
        DoSort();
    }

    private void BtnSortClick(object sender, ModBase.RouteEventArgs e)
    {
        var body = new ContextMenu();
        foreach (SortMethod i in Enum.GetValues(typeof(SortMethod)))
        {
            var item = new MyMenuItem();
            item.Header = _vm.GetSortName(i);
            item.Click += (_, _) => SetSortMethod(i);
            body.Items.Add(item);
        }

        body.PlacementTarget = (UIElement)sender;
        body.Placement = PlacementMode.Bottom;
        body.IsOpen = true;
    }

    private void DoSort()
    {
        lock (sortLock)
        {
            try
            {
                if (PanList is null || PanList.Children.Count < 2)
                    return;

                // 将子元素转换为可排序的列表
                var items = PanList.Children.OfType<MyLocalCompItem>().ToList();
                var method = _vm.GetSortMethod(_vm.SortMethod);

                // 分离有效和无效项（保持原始相对顺序）
                var invalid = items.Where(i => i.Entry is null).ToList();
                var valid = items.Except(invalid).ToList();
                // 仅对有效项进行排序
                valid.Sort((x, y) => method(x.Entry, y.Entry));
                // 合并保持无效项的原始顺序
                items = valid.Concat(invalid).ToList();

                // 批量更新UI元素
                PanList.Children.Clear();
                items.ForEach(i => PanList.Children.Add(i));
            }

            catch (Exception ex)
            {
                ModBase.Log(
                    ex,
                    "执行排序时出错",
                    ModBase.LogLevel.Hint,
                    userSummary: Lang.Text("Instance.Saves.Error.OperationFailed"));
            }
        }
    }

    #endregion

    #region 下边栏

    // 启用
    private async void BtnSelectEnable_Click(object sender, ModBase.RouteEventArgs e)
    {
        try
        {
            await ToggleDatapacksAsync(
                ModLocalComp.compResourceListLoader.output.Where(m => _vm.SelectedDatapacks.Contains(m.RawPath))
                    .ToList(), true);
            ChangeAllSelected(false);
        }
        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                "切换数据包启用状态失败",
                ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Instance.Saves.Error.OperationFailed"));
        }
    }

    // 禁用
    private async void BtnSelectDisable_Click(object sender, ModBase.RouteEventArgs e)
    {
        try
        {
            await ToggleDatapacksAsync(
                ModLocalComp.compResourceListLoader.output.Where(m => _vm.SelectedDatapacks.Contains(m.RawPath))
                    .ToList(), false);
            ChangeAllSelected(false);
        }
        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                "切换数据包启用状态失败",
                ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Instance.Saves.Error.OperationFailed"));
        }
    }

    /// <summary>
    ///     启用/禁用数据包（通过重命名文件夹为 .disabled）
    /// </summary>
    private async Task ToggleDatapacksAsync(IEnumerable<ModLocalComp.LocalCompFile> datapackList, bool isEnable)
    {
        var outcome = await _vm.ToggleDatapacksAsync(datapackList, isEnable);

        foreach (var (OldEntry, NewEntry) in outcome.Replaced)
        {
            // 更改 UI 中的列表
            try
            {
                var newItem = BuildLocalCompItem(NewEntry);
                datapackItems[OldEntry.RawPath] = newItem;
                var indexOfUi = PanList.Children.IndexOf(PanList.Children.OfType<MyLocalCompItem>()
                    .FirstOrDefault(i => ReferenceEquals(i.Entry, OldEntry)));
                if (indexOfUi == -1)
                    continue;
                PanList.Children.RemoveAt(indexOfUi);
                PanList.Children.Insert(indexOfUi, newItem);
            }
            catch (Exception ex)
            {
                ModBase.Log(
                    ex,
                    $"更新 UI 列表项失败：{OldEntry.FileName}",
                    ModBase.LogLevel.Hint,
                    userSummary: Lang.Text("Instance.Saves.Error.OperationFailed"));
            }
        }

        if (outcome.Aborted)
        {
            ReloadDatapackFileList(true);
            return;
        }

        Dispatcher.Invoke(() => PanList.UpdateLayout(), DispatcherPriority.Background);

        if (outcome.IsSuccessful)
        {
            RefreshBars();
        }
        else
        {
            HintService.Hint(Lang.Text("Instance.Saves.Datapack.ToggleWarning"), HintType.Error);
            ReloadDatapackFileList(true);
        }

        LoaderRun(ModLoader.LoaderFolderRunType.UpdateOnly);
    }

    // 更新
    private async void BtnSelectUpdate_Click(object sender, ModBase.RouteEventArgs e)
    {
        try
        {
            var updateList = ModLocalComp.compResourceListLoader.output
                .Where(m => _vm.SelectedDatapacks.Contains(m.RawPath) && m.CanUpdate).ToList();
            if (!updateList.Any())
                return;
            await UpdateResourceAsync(updateList);
            ChangeAllSelected(false);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "初始化数据包更新失败");
        }
    }

    public void UpdateResource(IEnumerable<ModLocalComp.LocalCompFile> datapackList)
    {
        _ = UpdateResourceAsync(datapackList);
    }

    private async Task UpdateResourceAsync(IEnumerable<ModLocalComp.LocalCompFile> datapackList)
    {
        try
        {
            var list = datapackList.ToList();
            // 更新前警告
            if (!States.Hint.FunctionDatapackUpdate || list.Count >= 15)
            {
                if (1 != await ModMain.MyMsgBoxAsync(
                        Lang.Text("Instance.Saves.Datapack.Update.Warning.Message"),
                        Lang.Text("Instance.Saves.Datapack.Update.Warning.Title"),
                        Lang.Text("Instance.Saves.Datapack.Update.Warning.Confirm"),
                        Lang.Text("Common.Action.Cancel"), isWarn: true))
                    return;
                States.Hint.FunctionDatapackUpdate = true;
            }

            if (await _vm.UpdateAsync(list))
                ReloadDatapackFileList(true);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "初始化数据包更新失败");
        }
    }

    // 删除
    private void BtnSelectDelete_Click(object sender, ModBase.RouteEventArgs e)
    {
        _ = DeleteDatapacksAsync(ModLocalComp.compResourceListLoader.output
            .Where(m => _vm.SelectedDatapacks.Contains(m.RawPath)));
        ChangeAllSelected(false);
    }

    private async Task DeleteDatapacksAsync(IEnumerable<ModLocalComp.LocalCompFile> datapackList)
    {
        try
        {
            var isShiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
            var outcome = await _vm.DeleteFilesAsync(datapackList, isShiftPressed);
            if (outcome.Fatal is not null)
            {
                ModBase.Log(
                    outcome.Fatal,
                    "删除数据包出现未知错误",
                    ModBase.LogLevel.Feedback,
                    userSummary: Lang.Text("Instance.Saves.Error.OperationFailed"));
                ReloadDatapackFileList(true);
                LoaderRun(ModLoader.LoaderFolderRunType.UpdateOnly);
                return;
            }

            foreach (var DatapackEntity in outcome.Deleted)
            {
                datapackItems.Remove(DatapackEntity.RawPath);
                var indexOfUi = PanList.Children.IndexOf(PanList.Children.OfType<MyLocalCompItem>()
                    .FirstOrDefault(i => i.Entry.Equals(DatapackEntity)));
                if (indexOfUi >= 0)
                    PanList.Children.RemoveAt(indexOfUi);
            }

            if (outcome.Canceled)
            {
                ReloadDatapackFileList(true);
                return;
            }

            RefreshBars();
            if (!outcome.IsSuccessful)
            {
                HintService.Hint(Lang.Text("Instance.Saves.Datapack.Delete.FileOccupied"), HintType.Error);
                ReloadDatapackFileList(true);
                return;
            }

            if (PanList.Children.Count == 0)
                ReloadDatapackFileList(true);
            else
                RefreshBars();

            if (isShiftPressed)
            {
                if (outcome.Deleted.Count == 1)
                    HintService.Hint(
                        Lang.Text("Instance.Saves.Datapack.Delete.PermanentSingle", outcome.Deleted[0].FileName),
                        HintType.Success);
                else
                    HintService.Hint(
                        Lang.Text("Instance.Saves.Datapack.Delete.PermanentMultiple", outcome.Deleted.Count),
                        HintType.Success);
            }
            else if (outcome.Deleted.Count == 1)
            {
                HintService.Hint(
                    Lang.Text("Instance.Saves.Datapack.Delete.RecycleSingle", outcome.Deleted[0].FileName),
                    HintType.Success);
            }
            else
            {
                HintService.Hint(
                    Lang.Text("Instance.Saves.Datapack.Delete.RecycleMultiple", outcome.Deleted.Count),
                    HintType.Success);
            }

            LoaderRun(ModLoader.LoaderFolderRunType.UpdateOnly);
        }
        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                "删除数据包出现未知错误",
                ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Instance.Saves.Error.OperationFailed"));
            ReloadDatapackFileList(true);
            LoaderRun(ModLoader.LoaderFolderRunType.UpdateOnly);
        }
    }

    // 取消选择
    private void BtnSelectCancel_Click(object sender, ModBase.RouteEventArgs e)
    {
        ChangeAllSelected(false);
    }

    // 收藏
    private void BtnSelectFavorites_Click(object sender, ModBase.RouteEventArgs e)
    {
        var selected = ModLocalComp.compResourceListLoader.output
            .Where(m => _vm.SelectedDatapacks.Contains(m.RawPath) && m.Comp is not null).Select(i => i.Comp).ToList();
        ModComp.CompFavorites.ShowMenu(selected, (UIElement)sender);
    }

    // 分享
    private void BtnSelectShare_Click(object sender, ModBase.RouteEventArgs e)
    {
        var shareList = ModLocalComp.compResourceListLoader.output
            .Where(m => _vm.SelectedDatapacks.Contains(m.RawPath) && m.Comp is not null).Select(i => i.Comp.Id)
            .ToHashSet();
        ModBase.ClipboardSet(ModComp.CompFavorites.GetShareCode(shareList));
        ChangeAllSelected(false);
    }

    #endregion

    #region 单个资源项

    // 详情
    public async void Info_Click(object sender, EventArgs e)
    {
        try
        {
            var datapackEntry = ((MyLocalCompItem)(sender is MyIconButton iconBtn ? iconBtn.Tag : sender)).Entry;

            // 加载失败信息
            if (datapackEntry.State == ModLocalComp.LocalCompFile.LocalFileStatus.Unavailable)
            {
                await ModMain.MyMsgBoxAsync(
                    Lang.Text(
                        "Instance.Saves.Datapack.Info.ReadFailed.WithDetail",
                        datapackEntry.FileUnavailableReason.ToString()),
                    Lang.Text("Instance.Saves.Datapack.Info.ReadFailedTitle"));
                return;
            }

            if (datapackEntry.Comp is not null)
            {
                // 跳转到数据包下载页面
                ModMain.frmMain.PageChange(new FormMain.PageStackData
                {
                    page = FormMain.PageType.CompDetail,
                    additional = (datapackEntry.Comp, new List<string>(), PageInstanceLeft.McInstance.Info.VanillaName,
                        ModComp.CompLoaderType.Minecraft, ModComp.CompType.DataPack, null)
                });
            }
            else
            {
                await _vm.EnsureFileInfoAsync(new[] { datapackEntry.path });

                // 获取信息
                var contentLines = new List<string>();

                if (datapackEntry.Description is not null)
                    contentLines.Add(datapackEntry.Description + "\r\n");
                if (datapackEntry.Authors is not null)
                    contentLines.Add(Lang.Text("Instance.Saves.Datapack.Info.Author") + datapackEntry.Authors);
                contentLines.Add(Lang.Text("Instance.Saves.Datapack.Info.File") + datapackEntry.FileName + "（" +
                                 ModBase.GetString(_vm.GetDatapackFileInfo(datapackEntry.path).Length) + "）");
                if (datapackEntry.Version is not null)
                    contentLines.Add(Lang.Text("Instance.Saves.Datapack.Info.Version") + datapackEntry.Version);

                var debugInfo = new List<string>();
                if (datapackEntry.ModId is not null)
                    debugInfo.Add(Lang.Text("Instance.Saves.Datapack.Info.DatapackId") + datapackEntry.ModId);
                if (debugInfo.Any())
                {
                    contentLines.Add("");
                    contentLines.AddRange(debugInfo);
                }

                // 显示详情信息
                if (datapackEntry.Url is null)
                    await ModMain.MyMsgBoxAsync(contentLines.Join("\r\n"), datapackEntry.Name,
                        Lang.Text("Instance.Resource.Item.Info.Return"));
                else if (1 == await ModMain.MyMsgBoxAsync(contentLines.Join("\r\n"), datapackEntry.Name,
                             Lang.Text("Instance.Resource.Item.Info.OpenWebsite"),
                             Lang.Text("Instance.Resource.Item.Info.Return")))
                    ModBase.OpenWebsite(datapackEntry.Url);
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                "获取数据包详情失败",
                ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Instance.Saves.Error.OperationFailed"));
        }
    }

    // 打开文件所在的位置
    public void Open_Click(MyIconButton sender, EventArgs e)
    {
        try
        {
            var listItem = (MyLocalCompItem)sender.Tag;
            ModBase.OpenExplorer(listItem.Entry.path);
        }
        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                "打开数据包文件位置失败",
                ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Instance.Saves.Error.OperationFailed"));
        }
    }

    // 删除
    public void Delete_Click(MyIconButton sender, EventArgs e)
    {
        var listItem = (MyLocalCompItem)sender.Tag;
        _ = DeleteDatapacksAsync(new[] { listItem.Entry });
    }

    // 启用
    public void Enable_Click(MyIconButton sender, EventArgs e)
    {
        var listItem = (MyLocalCompItem)sender.Tag;
        _ = ToggleDatapacksAsync(new[] { listItem.Entry }, true);
    }

    // 禁用
    public void Disable_Click(MyIconButton sender, EventArgs e)
    {
        var listItem = (MyLocalCompItem)sender.Tag;
        _ = ToggleDatapacksAsync(new[] { listItem.Entry }, false);
    }

    #endregion

    #region 搜索

    public bool IsSearching => _vm.IsSearching;

    public void SearchRun(object sender, EventArgs e)
    {
        try
        {
            _vm.SearchText = SearchBox.Text;
            if (_vm.IsSearching)
            {
                // 构造请求
                var queryList = new List<ModBase.SearchEntry<ModLocalComp.LocalCompFile>>();
                foreach (var Entry in ModLocalComp.compResourceListLoader.output)
                {
                    var searchSource = new List<ModBase.SearchSource>();
                    searchSource.Add(new ModBase.SearchSource(Entry.Name, 1d));
                    searchSource.Add(new ModBase.SearchSource(Entry.FileName, 1d));
                    if (Entry.Version is not null)
                        searchSource.Add(new ModBase.SearchSource(Entry.Version, 0.2d));
                    if (Entry.Description is not null && !string.IsNullOrEmpty(Entry.Description))
                        searchSource.Add(new ModBase.SearchSource(Entry.Description, 0.4d));
                    if (Entry.Comp is not null)
                    {
                        if ((Entry.Comp.RawName ?? "") != (Entry.Name ?? ""))
                            searchSource.Add(new ModBase.SearchSource(Entry.Comp.RawName, 1d));
                        if ((Entry.Comp.TranslatedName ?? "") != (Entry.Comp.RawName ?? ""))
                            searchSource.Add(new ModBase.SearchSource(Entry.Comp.TranslatedName, 1d));
                        if ((Entry.Comp.Description ?? "") != (Entry.Description ?? ""))
                            searchSource.Add(new ModBase.SearchSource(Entry.Comp.Description, 0.4d));
                        searchSource.Add(new ModBase.SearchSource(
                            string.Join("", ModComp.GetLocalizedTags(Entry.Comp.Tags)),
                            0.2d));
                    }

                    queryList.Add(new ModBase.SearchEntry<ModLocalComp.LocalCompFile>
                        { item = Entry, searchSource = searchSource });
                }

                // 进行搜索
                _vm.SetSearchResult(
                    ModBase.Search(queryList, SearchBox.Text, ModBase.MaxLocalSearchDepth, 0.35d)
                        .Select(r => r.item).ToList());
            }

            RefreshUI();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "搜索过程中发生异常");
        }
    }

    #endregion
}
