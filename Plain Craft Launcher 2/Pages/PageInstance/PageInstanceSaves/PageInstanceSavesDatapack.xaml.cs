using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.VisualBasic.FileIO;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI;
using PCL.Core.UI.Theme;
using PCL.Network;
using PCL.Network.Loaders;
using FileSystem = Microsoft.VisualBasic.FileSystem;

namespace PCL;

public partial class PageInstanceSavesDatapack : IRefreshable
{
    #region 数据包信息缓存
    private readonly Dictionary<string, (DateTime CreationTime, long Length)>
        _datapackFileInfoCache = new();

    // 获取数据包信息（带缓存）
    private (DateTime CreationTime, long Length) GetDatapackFileInfo(string path)
    {
        (DateTime CreationTime, long Length) cacheItem;
        if (_datapackFileInfoCache.TryGetValue(path, out cacheItem)) return cacheItem;

        try
        {
            var fileInfo = new FileInfo(path);
            var newItem = (fileInfo.CreationTime, fileInfo.Length);
            if (!_datapackFileInfoCache.ContainsKey(path))
                _datapackFileInfoCache.Add(path, newItem);
            return newItem;
        }
        catch (Exception ex)
        {
            Common.Log(ex, "获取数据包信息失败: " + path);
            return (DateTime.MinValue, 0L);
        }
    }

    // 页面关闭时清理缓存
    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        _datapackFileInfoCache.Clear();
    }
    #endregion
    #region 初始化


    private readonly MyLocalCompItem.SwipeSelect _currentSwipSelect;

    public PageInstanceSavesDatapack()
    {
        _currentSwipSelect = new MyLocalCompItem.SwipeSelect { TargetFrm = this };

        InitializeComponent();
        Unloaded += Page_Unloaded;
        Loaded += (_, _) => PageOther_Loaded();
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

    private LocalComp.CompLocalLoaderData GetRequireLoaderData()
    {
        var res = new LocalComp.CompLocalLoaderData();
        res.gameVersion = PageInstanceLeft.mcInstance;
        res.frm = null;
        res.loaders = new[] { Comp.CompLoaderType.Minecraft }.ToList();
        res.compPath = Path.Combine(PageInstanceSavesLeft.currentSave, "datapacks");
        res.compType = Comp.CompType.DataPack;
        return res;
    }

    private bool _isLoad;

    public void PageOther_Loaded()
    {
        if (Launcher.frmMain.pageLast.page != FormMain.PageType.CompDetail)
            PanBack.ScrollToHome();
        Animations.AniBeginControl();
        selectedDatapacks.Clear();
        ReloadDatapackFileList();
        ChangeAllSelected(false);
        Animations.AniEndControl();

        // 非重复加载部分
        if (_isLoad)
            return;
        _isLoad = true;

        Launcher.frmMain.KeyDown += FrmMain_KeyDown;
        // 调整按钮边距（这玩意儿没法从 XAML 改）
        foreach (MyRadioButton Btn in PanFilter.Children)
            Btn.LabText.Margin = new Thickness(-2, 0d, 8d, 0d);
    }

    /// <summary>
    /// 刷新数据包列表。
    /// </summary>
    public void ReloadDatapackFileList(bool forceReload = false)
    {
        if (LoaderRun(forceReload
                ? Loading.LoaderFolderRunType.ForceRun
                : Loading.LoaderFolderRunType.RunOnUpdated))
        {
            Common.Log("[System] 已刷新数据包列表");
            _datapackFileInfoCache.Clear();

            Common.RunInUi(() =>
            {
                Filter = FilterType.All;
                PanBack.ScrollToHome();
                SearchBox.Text = "";
            });
        }
    }

    // 强制刷新
    private void RefreshSelf()
    {
        Refresh();
    }

    void IRefreshable.Refresh()
    {
        RefreshSelf();
    }

    public void Refresh()
    {
        Launcher.frmInstanceSavesDatapack.ReloadDatapackFileList(true);
        Common.Log("[Datapack] 刷新数据包列表");
    }

    private void LoaderInit()
    {
        PageLoaderInit(Load, PanLoad, PanAllBack, null, LocalComp.CompResourceListLoader,
            _ => LoadUIFromLoaderOutput(), () => Comp.CompType.DataPack, false);
    }

    private void Load_Click(object sender, MouseButtonEventArgs e)
    {
        if (LocalComp.CompResourceListLoader.State == Common.LoadState.Failed)
            LoaderRun(Loading.LoaderFolderRunType.ForceRun);
    }

    public bool LoaderRun(Loading.LoaderFolderRunType type)
    {
        var loadPath = Path.Combine(PageInstanceSavesLeft.currentSave, "datapacks");
        return Loading.LoaderFolderRun(LocalComp.CompResourceListLoader, loadPath, type,
    #endregion
    #region UI 化
            loaderInput: GetRequireLoaderData());
    }



    /// <summary>
    /// 已加载的数据包 UI 缓存。Key 为数据包的 RawPath。
    /// </summary>
    public Dictionary<string, MyLocalCompItem> datapackItems = new();

    /// <summary>
    /// 将加载器结果的数据包列表加载为 UI。
    /// </summary>
    private void LoadUIFromLoaderOutput()
    {
        try
        {
            // 判断应该显示哪一个页面
            if (LocalComp.CompResourceListLoader.output.Any())
            {
                PanBack.Visibility = Visibility.Visible;
                PanEmpty.Visibility = Visibility.Collapsed;
            }
            else
            {
                // 根据组件类型设置 PanEmpty 的文本内容
                TxtEmptyTitle.Text = Lang.Text("Instance.Resource.Datapack.Empty.Title");
                TxtEmptyDescription.Text = Lang.Text("Instance.Resource.Datapack.Empty.Description");

                PanEmpty.Visibility = Visibility.Visible;
                PanBack.Visibility = Visibility.Collapsed;
                return;
            }

            // 修改缓存
            datapackItems.Clear();
            var itemsToShow = LocalComp.CompResourceListLoader.output.ToList();

            foreach (var DatapackEntity in itemsToShow)
                datapackItems[DatapackEntity.RawPath] = BuildLocalCompItem(DatapackEntity);

            // 显示结果
            Common.RunInUi(() =>
            {
                Filter = FilterType.All;
                SearchBox.Text = ""; // 这会触发结果刷新，所以需要在 DatapackItems 更新之后
                RefreshUI();
                SetSortMethod(SortMethod.CompName);
            });
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "加载数据包列表 UI 失败",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Instance.Saves.Error.OperationFailed"));
        }
    }

    private MyLocalCompItem BuildLocalCompItem(LocalComp.LocalCompFile entry)
    {
        try
        {
            Animations.AniBeginControl();
            var newItem = new MyLocalCompItem
            {
                SnapsToDevicePixels = true,
                Entry = entry,
                buttonHandler = BuildLocalCompItemBtnHandler,
                Checked = selectedDatapacks.Contains(entry.RawPath)
            };
            newItem.CurrentSwipe = _currentSwipSelect;
            newItem.Tags = entry.Tags;
            entry.OnCompUpdate += _ => newItem.Refresh();
            newItem.Refresh();
            Animations.AniEndControl();
            return newItem;
        }
        catch (Exception ex)
        {
            Animations.AniEndControl();
            Common.Log(ex, $"创建 UI 项失败：{entry.RawPath}");
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
        btnOpen.ToolTip = Lang.Text("Instance.Saves.OpenFileLocation");
        ToolTipService.SetPlacement(btnOpen, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnOpen, 30d);
        ToolTipService.SetHorizontalOffset(btnOpen, 2d);
        btnOpen.Click += (sender, e) => Open_Click((MyIconButton)sender, e);

        var btnCont = new MyIconButton { LogoScale = 1d, SvgIcon = "lucide/info", Tag = sender };
        btnCont.ToolTip = Lang.Text("Instance.Saves.Detail");
        ToolTipService.SetPlacement(btnCont, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnCont, 30d);
        ToolTipService.SetHorizontalOffset(btnCont, 2d);
        btnCont.Click += Info_Click;
        sender.MouseRightButtonUp += Info_Click;

        var btnDelete = new MyIconButton { LogoScale = 1d, SvgIcon = "lucide/trash-2", Tag = sender };
        btnDelete.ToolTip = Lang.Text("Common.Action.Delete");
        ToolTipService.SetPlacement(btnDelete, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnDelete, 30d);
        ToolTipService.SetHorizontalOffset(btnDelete, 2d);
        btnDelete.Click += (sender, e) => Delete_Click((MyIconButton)sender, e);

        if (sender.Entry.State == LocalComp.LocalCompFile.LocalFileStatus.Fine)
        {
            var btnDisable = new MyIconButton { LogoScale = 1d, SvgIcon = "lucide/circle-minus", Tag = sender };
            btnDisable.ToolTip = Lang.Text("Instance.Resource.Disable");
            ToolTipService.SetPlacement(btnDisable, PlacementMode.Center);
            ToolTipService.SetVerticalOffset(btnDisable, 30d);
            ToolTipService.SetHorizontalOffset(btnDisable, 2d);
            btnDisable.Click += (ss, e) => Disable_Click((MyIconButton)ss, e);
            sender.Buttons = new[] { btnCont, btnOpen, btnDisable, btnDelete };
        }
        else if (sender.Entry.State == LocalComp.LocalCompFile.LocalFileStatus.Disabled)
        {
            var btnEnable = new MyIconButton { LogoScale = 1d, SvgIcon = "lucide/circle-check", Tag = sender };
            btnEnable.ToolTip = Lang.Text("Instance.Resource.Enable");
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
    /// 刷新整个 UI。
    /// </summary>
    public void RefreshUI()
    {
        if (PanList is null)
            return;
        var showingDatapacks =
            (IsSearching ? _searchResult : datapackItems.Values.Select(i => i.Entry))
            .Where(m => CanPassFilter(m)).ToList();

        // 对显示的数据包进行排序
        if (showingDatapacks.Any())
        {
            var sortMethod = GetSortMethod(_currentSortMethod);
            showingDatapacks.Sort((a, b) => sortMethod(a, b));
        }

        // 重新列出列表
        Animations.AniBeginControl();
        if (showingDatapacks.Any())
        {
            PanList.Visibility = Visibility.Visible;
            PanList.Children.Clear();
            foreach (var TargetDatapack in showingDatapacks)
            {
                if (!datapackItems.ContainsKey(TargetDatapack.RawPath))
                    continue;
                var item = datapackItems[TargetDatapack.RawPath];

                // 确保元素没有父容器，避免重复添加异常
                if (item.Parent is not null) ((Panel)item.Parent).Children.Remove(item);

                TextStyles.MinecraftFormatter.SetColorfulTextLab(item.LabTitle.Text, item.LabTitle,
                    ThemeService.IsDarkMode);
                TextStyles.MinecraftFormatter.SetColorfulTextLab(item.LabInfo.Text, item.LabInfo,
                    ThemeService.IsDarkMode);
                item.Checked = selectedDatapacks.Contains(TargetDatapack.RawPath); // 更新选中状态
                PanList.Children.Add(item);
            }
        }
        else
        {
            PanList.Visibility = Visibility.Collapsed;
        }

        Animations.AniEndControl();
        selectedDatapacks =
            new HashSet<string>(selectedDatapacks.Where(m =>
                showingDatapacks.Any(s => (s.RawPath ?? "") == (m ?? ""))));
        RefreshBars();
    }

    /// <summary>
    /// 刷新顶栏和底栏显示。
    /// </summary>
    public void RefreshBars()
    {
        Dispatcher.BeginInvoke(new Func<Task>(async () =>
        {
            // -----------------
            // 顶部栏
            // -----------------

            // 计数
            var anyCount = 0;
            var enabledCount = 0;
            var disabledCount = 0;
            var updateCount = 0;
            var unavalialeCount = 0;
            var itemSource =
                (IsSearching ? _searchResult : datapackItems.Values.Select(i => i.Entry)).ToArray();
            await Task.Run(() =>
            {
                foreach (var item in itemSource)
                {
                    anyCount += 1;
                    if (item.CanUpdate) updateCount += 1;
                    if (item.State == LocalComp.LocalCompFile.LocalFileStatus.Fine)
                        enabledCount += 1;
                    if (item.State == LocalComp.LocalCompFile.LocalFileStatus.Disabled)
                        disabledCount += 1;
                    if (item.State == LocalComp.LocalCompFile.LocalFileStatus.Unavailable)
                        unavalialeCount += 1;
                }
            });
            // 显示
            BtnFilterAll.Text = IsSearching ? Lang.Text("Instance.Resource.Filter.SearchResult") : Lang.Text("Instance.Resource.Filter.AllWithCount", anyCount);
            BtnFilterCanUpdate.Text = Lang.Text("Instance.Resource.Filter.UpdatableWithCount", updateCount);
            BtnFilterCanUpdate.Visibility = Filter == FilterType.CanUpdate || updateCount > 0
                ? Visibility.Visible
                : Visibility.Collapsed;
            BtnFilterEnabled.Text = Lang.Text("Instance.Resource.Filter.EnabledWithCount", enabledCount);
            BtnFilterEnabled.Visibility = Filter == FilterType.Enabled || (enabledCount > 0 && enabledCount < anyCount)
                ? Visibility.Visible
                : Visibility.Collapsed;
            BtnFilterDisabled.Text = Lang.Text("Instance.Resource.Filter.DisabledWithCount", disabledCount);
            BtnFilterDisabled.Visibility = Filter == FilterType.Disabled || disabledCount > 0
                ? Visibility.Visible
                : Visibility.Collapsed;
            BtnFilterError.Text = Lang.Text("Instance.Resource.Filter.ErrorWithCount", unavalialeCount);
            BtnFilterError.Visibility = Filter == FilterType.Unavailable || unavalialeCount > 0
                ? Visibility.Visible
                : Visibility.Collapsed;

            // -----------------
            // 底部栏
            // -----------------

            // 计数
            var newCount = selectedDatapacks.Count;
            var selected = newCount > 0;
            if (selected)
                LabSelect.Text = Lang.Text("Instance.Resource.SelectedCount", newCount);

            // 按钮可用性
            if (selected)
            {
                var hasUpdate = false;
                var hasEnabled = false;
                var hasDisabled = false;
                var canFavoriteAndShare = true;


                // 检查是否所有选中的数据包都有有效的项目信息
                await Task.Run(() =>
                {
                    foreach (var DatapackEntity in LocalComp.CompResourceListLoader.output)
                        if (selectedDatapacks.Contains(DatapackEntity.RawPath))
                        {
                            if (DatapackEntity.CanUpdate) hasUpdate = true;
                            if (DatapackEntity.State ==
                                LocalComp.LocalCompFile.LocalFileStatus.Fine)
                                hasEnabled = true;
                            else if (DatapackEntity.State ==
                                     LocalComp.LocalCompFile.LocalFileStatus.Disabled)
                                hasDisabled = true;
                            if (DatapackEntity.Comp is null || string.IsNullOrEmpty(DatapackEntity.Comp.Id))
                                canFavoriteAndShare = false;
                        }
                });

                BtnSelectDisable.IsEnabled = hasEnabled;
                BtnSelectEnable.IsEnabled = hasDisabled;
                BtnSelectUpdate.IsEnabled = hasUpdate;
                BtnSelectFavorites.IsEnabled = canFavoriteAndShare;
                BtnSelectShare.IsEnabled = canFavoriteAndShare;
            }

            // 更新显示状态
            if (!Animations.AniExitsAnyControl())
            {
                PanListBack.Margin = new Thickness(0d, 0d, 0d, selected ? 95 : 15);
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
                                ease: new Animations.AniEaseOutFluent(Animations.AniEasePower
                                    .Weak)),
                            Animations.AaTranslateY(CardSelect, 3d, 150, 120,
                                new Animations.AniEaseInoutFluent(Animations.AniEasePower.Weak)),
                            Animations.AaTranslateY(CardSelect, -1, 90, 270,
                                new Animations.AniEaseInoutFluent(Animations.AniEasePower.Weak))
                        }, "Datapack Sidebar");
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
                            Animations.AaCode(() => CardSelect.Visibility = Visibility.Collapsed,
                                after: true)
                        }, "Datapack Sidebar");
                }
            }
            else
            {
                Animations.AniStop("Datapack Sidebar");
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
    #endregion
    #region 管理
        }));
    }

    private int _bottomBarShownCount;



    /// <summary>
    /// 打开 datapacks 文件夹。
    /// </summary>
    private void BtnManageOpen_Click(object sender, EventArgs e)
    {
        try
        {
            var datapackPath = Path.Combine(PageInstanceSavesLeft.currentSave, "datapacks");
            Directory.CreateDirectory(datapackPath);
            Common.OpenExplorer(datapackPath);
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "打开 datapacks 文件夹失败",
                Common.LogLevel.Msgbox,
                userSummary: Lang.Text("Instance.Saves.Error.OperationFailed"));
        }
    }

    /// <summary>
    /// 全选。
    /// </summary>
    private void BtnManageSelectAll_Click(object sender, MouseButtonEventArgs e)
    {
        ChangeAllSelected(selectedDatapacks.Count < PanList.Children.Count);
    }

    /// <summary>
    /// 安装数据包。
    /// </summary>
    private void BtnManageInstall_Click(object sender, MouseButtonEventArgs e)
    {
        var fileList = SystemDialogs.SelectFiles(
            Lang.Text("Instance.Saves.Datapack.Install.FileDialog.Filter"),
            Lang.Text("Instance.Saves.Datapack.Install.FileDialog.Title"));
        if (fileList is null || fileList.Length == 0)
            return;
        InstallDatapackFiles(fileList);
        Refresh();
    }

    /// <summary>
    /// 安装数据包文件。
    /// </summary>
    public static void InstallDatapackFiles(IEnumerable<string> filePathList)
    {
        if (!filePathList.Any())
            return;

        var extension = filePathList.First().AfterLast(".").ToLower();

        // 检查文件扩展名
        if (extension != "zip")
        {
            HintService.Hint(Lang.Text("Instance.Resource.Install.UnsupportedFormat", extension, Lang.Text("Download.Comp.Type.DataPack"), "zip"), HintType.Error);
            return;
        }

        // 检查回收站
        if (filePathList.First().Contains(@":\$RECYCLE.BIN\"))
        {
            HintService.Hint(Lang.Text("Instance.Resource.Install.RestoreFromRecycleBin"), HintType.Error);
            return;
        }

        Common.Log($"[System] 文件为 {extension} 格式，尝试作为数据包安装");

        // 确认安装
        if (!(Launcher.frmMain.pageCurrent == FormMain.PageType.InstanceSetup &&
              Launcher.frmMain.PageCurrentSub == FormMain.PageSubType.VersionSavesDatapack))
            if (Launcher.MyMsgBox(Lang.Text("Instance.Saves.Datapack.Install.Message"),
                    Lang.Text("Instance.Saves.Datapack.Install.Title"), Lang.Text("Common.Action.Confirm"),
                    Lang.Text("Common.Action.Cancel")) != 1)
                return;

        // 执行安装
        try
        {
            var datapackFolder = Path.Combine(PageInstanceSavesLeft.currentSave, "datapacks");
            Directory.CreateDirectory(datapackFolder);

            foreach (var FilePath in filePathList)
            {
                var newFileName = Common.GetFileNameFromPath(FilePath);
                var destFile = datapackFolder + newFileName;

                if (File.Exists(destFile))
                    if (Launcher.MyMsgBox(
                            Lang.Text("Instance.Resource.Install.OverwriteConfirm.Message",
                                newFileName),
                            Lang.Text("Instance.Resource.Install.OverwriteConfirm.Title"),
                            Lang.Text("Common.Action.Overwrite"),
                            Lang.Text("Common.Action.Cancel")) != 1)
                        continue;

                Common.CopyFile(FilePath, destFile);
            }

            if (filePathList.Count() == 1)
                HintService.Hint(
                    Lang.Text("Instance.Resource.Install.SuccessSingle",
                        Common.GetFileNameFromPath(filePathList.First())), HintType.Success);
            else
                HintService.Hint(Lang.Text("Instance.Resource.Install.SuccessMultiple", filePathList.Count(), Lang.Text("Download.Comp.Type.DataPack")), HintType.Success);

            // 刷新列表
            if (Launcher.frmMain.pageCurrent == FormMain.PageType.InstanceSetup &&
                Launcher.frmMain.PageCurrentSub == FormMain.PageSubType.VersionSavesDatapack)
                if (Launcher.frmInstanceSavesDatapack is not null)
                    Launcher.frmInstanceSavesDatapack.ReloadDatapackFileList(true);
        }

        catch (Exception ex)
        {
            Common.Log(
                ex,
                "复制数据包文件失败",
                Common.LogLevel.Msgbox,
                userSummary: Lang.Text("Instance.Saves.Error.OperationFailed"));
        }
    }

    /// <summary>
    /// 下载数据包。
    /// </summary>
    private void BtnManageDownload_Click(object sender, MouseButtonEventArgs e)
    {
        var datapackPath = Path.Combine(PageInstanceSavesLeft.currentSave, "datapacks");
        Directory.CreateDirectory(datapackPath);
        PageDownloadCompDetail.cachedFolder[Comp.CompType.DataPack] = datapackPath;
        Launcher.frmMain.PageChange(FormMain.PageType.Download,
            FormMain.PageSubType.DownloadDataPack);
        PageComp.targetVersion = PageInstanceLeft.mcInstance; // 将当前实例设置为筛选器
    }

    /// <summary>
    /// 导出信息。
    /// </summary>
    private void BtnManageInfoExport_Click(object sender, MouseButtonEventArgs e)
    {
        var choice =
            Launcher.MyMsgBox(
                Lang.Text("Instance.Saves.Datapack.Export.Mode.Message"),
                Lang.Text("Instance.Resource.Export.Mode.Title"), Lang.Text("Instance.Resource.Export.Mode.Txt"), Lang.Text("Instance.Resource.Export.Mode.Csv"), Lang.Text("Common.Action.Cancel"));

        void ExportText(string content, string fileName)
        {
            try
            {
                var savePath =
                    SystemDialogs.SelectSaveFile(Lang.Text("Instance.Resource.Export.SelectSaveLocation"), fileName, Lang.Text("Instance.Resource.Export.FilesFilter"));
                if (string.IsNullOrWhiteSpace(savePath)) return;
                File.WriteAllText(savePath, content, Encoding.UTF8);
                Common.OpenExplorer(savePath);
            }
            catch (Exception ex)
            {
                Common.Log(
                    ex,
                    "导出数据包信息失败",
                    Common.LogLevel.Msgbox,
                    userSummary: Lang.Text("Instance.Saves.Error.OperationFailed"));
            }
        }

        ;
        switch (choice)
        {
            case 1: // TXT
            {
                var exportContent = new List<string>();
                foreach (var DatapackEntity in LocalComp.CompResourceListLoader.output)
                    exportContent.Add(DatapackEntity.FileName);
                ExportText(exportContent.Join("\r\n"),
                    Common.GetFolderNameFromPath(PageInstanceSavesLeft.currentSave) + "的数据包信息.txt");
                break;
            }

            case 2: // CSV
            {
                var exportContent = new List<string>();
                exportContent.Add("文件名,数据包名称,数据包版本,此版本更新时间,工程 ID,文件大小（字节）,文件路径");
                foreach (var DatapackEntity in LocalComp.CompResourceListLoader.output)
                    exportContent.Add(
                        $"{DatapackEntity.FileName},{DatapackEntity.Comp?.TranslatedName},{DatapackEntity.Version},{DatapackEntity.CompFile?.ReleaseDate},{DatapackEntity.Comp?.Id},{GetDatapackFileInfo(DatapackEntity.path).Length},{DatapackEntity.path}");
    #endregion
    #region 选择
                ExportText(exportContent.Join("\r\n"),
                    Common.GetFolderNameFromPath(PageInstanceSavesLeft.currentSave) + "的数据包信息.csv");
                break;
            }
        }
    }



    /// <summary>
    /// 选择的数据包的路径。
    /// </summary>
    public HashSet<string> selectedDatapacks = new();

    // 单项切换选择状态
    public void CheckChanged(MyLocalCompItem sender, Common.RouteEventArgs e)
    {
        if (Animations.AniExitsAnyControl())
            return;
        // 更新选择了的内容
        var selectedKey = sender.Entry.RawPath;
        if (sender.Checked)
            selectedDatapacks.Add(selectedKey);
        else
            selectedDatapacks.Remove(selectedKey);
        RefreshBars();
    }

    // 切换所有项的选择状态
    private void ChangeAllSelected(bool value)
    {
        Animations.AniBeginControl();
        selectedDatapacks.Clear();
        foreach (var Item in datapackItems.Values)
        {
            var shouldSelected = value && PanList.Children.Contains(Item);
            Item.Checked = shouldSelected;
            if (shouldSelected)
                selectedDatapacks.Add(Item.Entry.RawPath);
        }

        Animations.AniEndControl();
        RefreshBars();
    }

    private void UnselectedAllWithAnimation()
    {
        var cacheAniControlEnabled = Animations.AniResetControl(0);
        ChangeAllSelected(false);
        Animations.AniRestoreControl(cacheAniControlEnabled);
    }

    private void FrmMain_KeyDown(object sender, KeyEventArgs e)
    {
        if (!ReferenceEquals(Launcher.frmMain.pageRight, this))
            return;
        if ((Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) && e.Key == Key.A)
            ChangeAllSelected(true);
    }

    #endregion
    #region 筛选
    private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Ctrl + A 会被搜索框捕获，导致无法全选，所以在按下 Ctrl + A 时转移焦点以便捕获
        if (SearchBox.Text.Any())
            return;
        if ((Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) && e.Key == Key.A)
            PanBack.Focus();
    }



    public FilterType Filter
    {
        get => field;
        set
        {
            if (field == value)
                return;
            field = value;
            switch (value)
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

            RefreshUI();
        }
    } = FilterType.All;

    public enum FilterType
    {
        All = 0,
        Enabled = 1,
        Disabled = 2,
        CanUpdate = 3,
        Unavailable = 4
    }

    /// <summary>
    /// 检查该数据包项是否符合当前筛选的类别。
    /// </summary>
    private bool CanPassFilter(LocalComp.LocalCompFile checkingDatapack)
    {
        switch (Filter)
        {
            case FilterType.All:
            {
                return true;
            }
            case FilterType.Enabled:
            {
                return checkingDatapack.State == LocalComp.LocalCompFile.LocalFileStatus.Fine;
            }
            case FilterType.Disabled:
            {
                return checkingDatapack.State == LocalComp.LocalCompFile.LocalFileStatus.Disabled;
            }
            case FilterType.CanUpdate:
            {
                return checkingDatapack.CanUpdate;
            }
            case FilterType.Unavailable:
            {
                return checkingDatapack.State ==
                       LocalComp.LocalCompFile.LocalFileStatus.Unavailable;
            }

            default:
            {
                return false;
            }
    #endregion
    #region 排序
        }
    }

    // 点击筛选项触发的改变
    private void ChangeFilter(MyRadioButton sender, bool raiseByMouse)
    {
        Filter = (FilterType)Convert.ToInt32(sender.Tag);
        RefreshUI();
        DoSort();
    }


    private SortMethod _currentSortMethod = SortMethod.CompName;

    private void SetSortMethod(SortMethod target)
    {
        _currentSortMethod = target;
        BtnSort.Text = Lang.Text("Instance.Resource.Sort.Text", GetSortName(target));
        DoSort();
    }

    private enum SortMethod
    {
        FileName,
        CompName,
        CreateTime,
        DatapackFileSize
    }

    private string GetSortName(SortMethod method)
    {
        switch (method)
        {
            case SortMethod.FileName:
            {
                return Lang.Text("Instance.Resource.Sort.FileName");
            }
            case SortMethod.CompName:
            {
                return Lang.Text("Instance.Resource.Sort.ResourceName");
            }
            case SortMethod.CreateTime:
            {
                return Lang.Text("Instance.Resource.Sort.AddTime");
            }
            case SortMethod.DatapackFileSize:
            {
                return Lang.Text("Instance.Resource.Sort.FileSize");
            }

            default:
            {
                return Lang.Text("Instance.Resource.Sort.ResourceName");
            }
        }

        return "";
    }

    private void BtnSortClick(object sender, Common.RouteEventArgs e)
    {
        var body = new ContextMenu();
        foreach (SortMethod i in Enum.GetValues(typeof(SortMethod)))
        {
            var item = new MyMenuItem();
            item.Header = GetSortName(i);
            item.Click += (_, _) => SetSortMethod(i);
            body.Items.Add(item);
        }

        body.PlacementTarget = (UIElement)sender;
        body.Placement = PlacementMode.Bottom;
        body.IsOpen = true;
    }

    private readonly object _sortLock = new();

    private void DoSort()
    {
        lock (_sortLock)
        {
            try
            {
                if (PanList is null || PanList.Children.Count < 2)
                    return;

                // 将子元素转换为可排序的列表
                var items = PanList.Children.OfType<MyLocalCompItem>().ToList();
                var method = GetSortMethod(_currentSortMethod);

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
                Common.Log(
                    ex,
                    "执行排序时出错",
                    Common.LogLevel.Hint,
                    userSummary: Lang.Text("Instance.Saves.Error.OperationFailed"));
            }
        }
    }

    private Func<LocalComp.LocalCompFile, LocalComp.LocalCompFile, int> GetSortMethod(
        SortMethod method)
    {
        switch (method)
        {
            case SortMethod.FileName:
            {
                return (a, b) => string.Compare(a.FileName, b.FileName, StringComparison.OrdinalIgnoreCase);
            }
            case SortMethod.CompName:
            {
                return (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            }
            case SortMethod.CreateTime:
            {
                return (a, b) =>
                {
                    var aDate = GetDatapackFileInfo(a.path).CreationTime;
                    var bDate = GetDatapackFileInfo(b.path).CreationTime;
                    if (aDate == DateTime.MinValue && bDate == DateTime.MinValue)
                        return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);

                    if (aDate == DateTime.MinValue) return 1;

                    if (bDate == DateTime.MinValue) return -1;
                    return bDate.CompareTo(aDate);
                };
            }
            case SortMethod.DatapackFileSize:
            {
                return (a, b) =>
                {
                    var aSize = GetDatapackFileInfo(a.path).Length;
                    var bSize = GetDatapackFileInfo(b.path).Length;
                    if (aSize == 0L && bSize == 0L)
                        return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);

                    if (aSize == 0L) return 1;
    #endregion
    #region 下边栏

                    if (bSize == 0L) return -1;
                    return bSize.CompareTo(aSize);
                };
            }

            default:
            {
                return (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            }
        }
    }



    // 启用
    private void BtnSelectEnable_Click(object sender, Common.RouteEventArgs e)
    {
        ToggleDatapacks(
            LocalComp.CompResourceListLoader.output
                .Where(m => selectedDatapacks.Contains(m.RawPath)).ToList(),
            true);
        ChangeAllSelected(false);
    }

    // 禁用
    private void BtnSelectDisable_Click(object sender, Common.RouteEventArgs e)
    {
        ToggleDatapacks(
            LocalComp.CompResourceListLoader.output
                .Where(m => selectedDatapacks.Contains(m.RawPath)).ToList(),
            false);
        ChangeAllSelected(false);
    }

    /// <summary>
    /// 启用/禁用数据包（通过重命名文件夹为 .disabled）
    /// </summary>
    private void ToggleDatapacks(IEnumerable<LocalComp.LocalCompFile> datapackList, bool isEnable)
    {
        var isSuccessful = true;
        foreach (var DatapackE in datapackList)
        {
            var datapackEntity = DatapackE;
            string newPath = null;

            if (datapackEntity.State == LocalComp.LocalCompFile.LocalFileStatus.Fine && !isEnable)
                // 禁用 - 添加 .disabled 后缀
                newPath = datapackEntity.path + ".disabled";
            else if (datapackEntity.State == LocalComp.LocalCompFile.LocalFileStatus.Disabled &&
                     isEnable)
                // 启用 - 移除 .disabled 后缀
                newPath = datapackEntity.RawPath;
            else
                continue;

            // 重命名
            try
            {
                if (File.Exists(newPath))
                {
                    Launcher.MyMsgBox(Lang.Text("Instance.Saves.Datapack.Replace.FileNameConflict",
                        Common.GetFileNameFromPath(newPath)));
                    continue;
                }

                FileSystem.Rename(datapackEntity.path, newPath);
            }
            catch (FileNotFoundException ex)
            {
                Common.Log(
                    ex,
                    $"未找到需要重命名的数据包（{datapackEntity.path ?? "null"}）",
                    Common.LogLevel.Feedback,
                    userSummary: Lang.Text("Instance.Saves.Error.OperationFailed"));
                ReloadDatapackFileList(true);
                return;
            }
            catch (Exception ex)
            {
                Common.Log(ex, $"重命名数据包失败（{datapackEntity.path ?? "null"}）");
                isSuccessful = false;
            }

            // 更改 Loader 中的列表
            var newDatapackEntity = new LocalComp.LocalCompFile(newPath);
            newDatapackEntity.FromJson(datapackEntity.ToJson());
            if (LocalComp.CompResourceListLoader.output.Contains(datapackEntity))
            {
                var indexOfLoader = LocalComp.CompResourceListLoader.output.IndexOf(datapackEntity);
                LocalComp.CompResourceListLoader.output.RemoveAt(indexOfLoader);
                LocalComp.CompResourceListLoader.output.Insert(indexOfLoader, newDatapackEntity);
            }

            if (_searchResult is not null && _searchResult.Contains(datapackEntity))
            {
                var indexOfResult = _searchResult.IndexOf(datapackEntity);
                _searchResult.Remove(datapackEntity);
                _searchResult.Insert(indexOfResult, newDatapackEntity);
            }

            // 更改 UI 中的列表
            try
            {
                var newItem = BuildLocalCompItem(newDatapackEntity);
                datapackItems[datapackEntity.RawPath] = newItem;
                var indexOfUi = PanList.Children.IndexOf(PanList.Children.OfType<MyLocalCompItem>()
                    .FirstOrDefault(i => ReferenceEquals(i.Entry, datapackEntity)));
                if (indexOfUi == -1)
                    continue;
                PanList.Children.RemoveAt(indexOfUi);
                PanList.Children.Insert(indexOfUi, newItem);
            }
            catch (Exception ex)
            {
                Common.Log(
                    ex,
                    $"更新 UI 列表项失败：{datapackEntity.FileName}",
                    Common.LogLevel.Hint,
                    userSummary: Lang.Text("Instance.Saves.Error.OperationFailed"));
            }
        }

        Dispatcher.Invoke(() => PanList.UpdateLayout(), DispatcherPriority.Background);

        if (isSuccessful)
        {
            RefreshBars();
        }
        else
        {
            HintService.Hint(Lang.Text("Instance.Saves.Datapack.ToggleWarning"), HintType.Error);
            ReloadDatapackFileList(true);
        }

        LoaderRun(Loading.LoaderFolderRunType.UpdateOnly);
    }

    // 更新
    private void BtnSelectUpdate_Click(object sender, Common.RouteEventArgs e)
    {
        var updateList = LocalComp.CompResourceListLoader.output
            .Where(m => selectedDatapacks.Contains(m.RawPath) && m.CanUpdate).ToList();
        if (!updateList.Any())
            return;
        UpdateResource(updateList);
        ChangeAllSelected(false);
    }

    /// <summary>
    /// 记录正在进行数据包更新的 datapacks 文件夹路径。
    /// </summary>
    public static List<string> updatingVersions = new();

    private static bool TryGetSafeDatapackUpdateFileName(Comp.CompFile file, out string fileName)
    {
        fileName = file.FileName?.Trim() ?? "";
        if (string.IsNullOrEmpty(fileName))
            return false;

        if (!fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            return false;

        if (fileName.IndexOfAny(new[] { '\\', '/', ':' }) >= 0)
            return false;

        if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return false;

        return fileName == Path.GetFileName(fileName) && fileName != "." && fileName != "..";
    }

    private static bool TryBuildDatapackUpdatePath(string rootPath, string fileName, out string fullPath)
    {
        var fullRootPath = Path.GetFullPath(rootPath);
        if (!fullRootPath.EndsWith(Path.DirectorySeparatorChar.ToString()) &&
            !fullRootPath.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
            fullRootPath += Path.DirectorySeparatorChar;

        fullPath = Path.GetFullPath(Path.Combine(fullRootPath, fileName));
        return fullPath.StartsWith(fullRootPath, StringComparison.OrdinalIgnoreCase);
    }

    public void UpdateResource(IEnumerable<LocalComp.LocalCompFile> datapackList)
    {
        // 更新前警告
        if (!States.Hint.FunctionDatapackUpdate || datapackList.Count() >= 15)
        {
            if (Launcher.MyMsgBox(
                    Lang.Text("Instance.Saves.Datapack.Update.Warning.Message"),
                    Lang.Text("Instance.Saves.Datapack.Update.Warning.Title"), Lang.Text("Instance.Saves.Datapack.Update.Warning.Confirm"), Lang.Text("Common.Action.Cancel"), isWarn: true) == 1)
                States.Hint.FunctionDatapackUpdate = true;
            else
                return;
        }

        try
        {
            // 构造下载信息
            datapackList = datapackList.ToList(); // 防止刷新影响迭代器
            var fileList = new List<DownloadFile>();
            var fileCopyList = new Dictionary<string, string>();
            var updateEntryList = new List<LocalComp.LocalCompFile>();
            var tempRoot = Path.Combine(Common.pathTemp, "DownloadedComp");
            var datapackRoot = Path.Combine(PageInstanceSavesLeft.currentSave, "datapacks");
            var skippedUnsafeFileCount = 0;
            foreach (var Entry in datapackList)
            {
                var file = Entry.UpdateFile;
                if (!file.Available)
                    continue;
                if (!TryGetSafeDatapackUpdateFileName(file, out var safeFileName) ||
                    !TryBuildDatapackUpdatePath(tempRoot, safeFileName, out var tempAddress) ||
                    !TryBuildDatapackUpdatePath(datapackRoot, safeFileName, out var realAddress))
                {
                    skippedUnsafeFileCount++;
                    Common.Log($"[DatapackUpdate] 已跳过不安全的数据包更新文件名：{file.FileName}",
                        Common.LogLevel.Debug);
                    continue;
                }

                // 添加到下载列表
                fileList.Add(file.ToNetFile(tempAddress, Comp.DownloadReason.Update,
                    file.RawGameVersions.FirstOrDefault()));
                fileCopyList[tempAddress] = realAddress;
                updateEntryList.Add(Entry);
            }

            if (skippedUnsafeFileCount > 0)
                HintService.Hint(
                    Lang.Text("Instance.Saves.Datapack.Update.UnsafeFilesSkipped", skippedUnsafeFileCount),
                    HintType.Error);
            if (!fileList.Any())
                return;

            // 构造加载器
            var installLoaders = new List<Loading.LoaderBase>();
            var finishedFileNames = new List<string>();
            installLoaders.Add(
                new LoaderDownload(Lang.Text("Instance.Saves.Datapack.Update.Task.DownloadFiles"), fileList)
                    { ProgressWeight = updateEntryList.Count * 1.5d });

            installLoaders.Add(new Loading.LoaderTask<int, int>(
                Lang.Text("Instance.Saves.Datapack.Update.Task.ReplaceFiles"), _ =>
                {
                    try
                    {
                        foreach (var Entry in updateEntryList)
                            if (File.Exists(Entry.path))
                                Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(Entry.path, UIOption.AllDialogs,
                                    RecycleOption.SendToRecycleBin);
                            else
                                Common.Log($"[DatapackUpdate] 未找到更新前的数据包文件，跳过对它的删除：{Entry.path}",
                                    Common.LogLevel.Debug);

                        foreach (var Entry in fileCopyList)
                        {
                            if (File.Exists(Entry.Value))
                            {
                                Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(Entry.Value, UIOption.AllDialogs,
                                    RecycleOption.SendToRecycleBin);
                                Common.Log($"[Datapack] 更新后的数据包文件已存在，将会把它放入回收站：{Entry.Value}",
                                    Common.LogLevel.Debug);
                            }

                            if (Directory.Exists(Common.GetPathFromFullPath(Entry.Value)))
                            {
                                File.Move(Entry.Key, Entry.Value);
                                finishedFileNames.Add(Common.GetFileNameFromPath(Entry.Value));
                            }
                            else
                            {
                                Common.Log($"[Datapack] 更新后的目标文件夹已被删除：{Entry.Value}",
                                    Common.LogLevel.Debug);
                            }
                        }
                    }
                    catch (OperationCanceledException ex)
                    {
                        Common.Log(ex, "替换旧版数据包文件时被主动取消");
                    }
                }));

            // 结束处理
            var loader = new Loading.LoaderCombo<IEnumerable<LocalComp.LocalCompFile>>(
                Lang.Text("Instance.Saves.Datapack.Update.Task.Title",
                    Common.GetFolderNameFromPath(PageInstanceSavesLeft.currentSave)),
                installLoaders);
            var pathDatapacks = Path.Combine(PageInstanceSavesLeft.currentSave, "datapacks");

            loader.OnStateChanged = _ =>
            {
                switch (loader.State)
                {
                    case Common.LoadState.Finished:
                    {
                        switch (finishedFileNames.Count)
                        {
                            case 0:
                            {
                                Common.Log("[DatapackUpdate] 没有数据包被成功更新");
                                break;
                            }
                            case 1:
                            {
                                HintService.Hint(Lang.Text("Instance.Resource.Update.SuccessSingle", finishedFileNames.Single()), HintType.Success);
                                break;
                            }

                            default:
                            {
                                HintService.Hint(Lang.Text("Instance.Resource.Update.SuccessMultiple", finishedFileNames.Count), HintType.Success);
                                break;
                            }
                        }

                        break;
                    }
                    case Common.LoadState.Failed:
                    {
                        HintService.Hint(Lang.Text("Instance.Resource.Update.Failed", loader.Error.Message), HintType.Error);
                        break;
                    }
                    case Common.LoadState.Aborted:
                    {
                        HintService.Hint(Lang.Text("Instance.Resource.Update.Aborted"));
                        break;
                    }

                    default:
                    {
                        return;
                    }
                }

                Common.Log($"[DatapackUpdate] 已从正在进行数据包更新的文件夹列表移除：{pathDatapacks}");
                updatingVersions.Remove(pathDatapacks);

                // 清理缓存
                Common.RunInNewThread(() =>
                {
                    try
                    {
                        foreach (var TempFile in fileCopyList.Keys)
                            if (File.Exists(TempFile))
                                File.Delete(TempFile);
                    }
                    catch (Exception ex)
                    {
                        Common.Log(ex, "清理数据包更新缓存失败");
                    }
                }, "Clean Datapack Update Cache", ThreadPriority.BelowNormal);
            };

            // 启动加载器
            Common.Log($"[DatapackUpdate] 开始更新 {datapackList.Count()} 个数据包：{pathDatapacks}");
            updatingVersions.Add(pathDatapacks);
            loader.Start();
            Loading.LoaderTaskbarAdd(loader);
            Launcher.frmMain.BtnExtraDownload.ShowRefresh();
            Launcher.frmMain.BtnExtraDownload.Ribble();
            ReloadDatapackFileList(true);
        }
        catch (Exception ex)
        {
            Common.Log(ex, "初始化数据包更新失败");
        }
    }

    // 删除
    private void BtnSelectDelete_Click(object sender, Common.RouteEventArgs e)
    {
        DeleteDatapacks(
            LocalComp.CompResourceListLoader.output.Where(m =>
                selectedDatapacks.Contains(m.RawPath)));
        ChangeAllSelected(false);
    }

    private void DeleteDatapacks(IEnumerable<LocalComp.LocalCompFile> datapackList)
    {
        try
        {
            var isSuccessful = true;
            var isShiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

            // 确认需要删除的文件
            datapackList = datapackList.SelectMany(target =>
            {
                if (target.State == LocalComp.LocalCompFile.LocalFileStatus.Fine)
                    return new[] { target.path, target.path + ".disabled" };

                return new[] { target.path, target.RawPath };
            }).Distinct().Where(m => File.Exists(m)).Select(m => new LocalComp.LocalCompFile(m))
            .ToList();

            // 实际删除文件
            foreach (var DatapackEntity in datapackList)
            {
                try
                {
                    if (isShiftPressed)
                        File.Delete(DatapackEntity.path);
                    else
                        Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(DatapackEntity.path,
                            UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                }
                catch (OperationCanceledException ex)
                {
                    Common.Log(ex, "删除数据包被主动取消");
                    ReloadDatapackFileList(true);
                    return;
                }
                catch (Exception ex)
                {
                    Common.Log(
                        ex,
                        $"删除数据包失败（{DatapackEntity.path}）",
                        Common.LogLevel.Msgbox,
                        userSummary: Lang.Text("Instance.Saves.Error.OperationFailed"));
                    isSuccessful = false;
                }

                // 取消选中
                selectedDatapacks.Remove(DatapackEntity.RawPath);
                // 更改 Loader 和 UI 中的列表
                LocalComp.CompResourceListLoader.output.Remove(DatapackEntity);
                _searchResult?.Remove(DatapackEntity);
                datapackItems.Remove(DatapackEntity.RawPath);
                var indexOfUi = PanList.Children.IndexOf(PanList.Children.OfType<MyLocalCompItem>()
                    .FirstOrDefault(i => i.Entry.Equals(DatapackEntity)));
                if (indexOfUi >= 0)
                    PanList.Children.RemoveAt(indexOfUi);
            }

            RefreshBars();
            if (!isSuccessful)
            {
                HintService.Hint(Lang.Text("Instance.Saves.Datapack.Delete.FileOccupied"), HintType.Error);
                ReloadDatapackFileList(true);
            }
            else if (PanList.Children.Count == 0)
            {
                ReloadDatapackFileList(true);
            }
            else
            {
                RefreshBars();
            }

            if (!isSuccessful)
                return;
            if (isShiftPressed)
            {
                if (datapackList.Count() == 1)
                    HintService.Hint(Lang.Text("Instance.Saves.Datapack.Delete.PermanentSingle", datapackList.Single().FileName), HintType.Success);
                else
                    HintService.Hint(Lang.Text("Instance.Saves.Datapack.Delete.PermanentMultiple", datapackList.Count()), HintType.Success);
            }
            else if (datapackList.Count() == 1)
            {
                HintService.Hint(Lang.Text("Instance.Saves.Datapack.Delete.RecycleSingle", datapackList.Single().FileName), HintType.Success);
            }
            else
            {
                HintService.Hint(Lang.Text("Instance.Saves.Datapack.Delete.RecycleMultiple", datapackList.Count()), HintType.Success);
            }
        }
        catch (OperationCanceledException ex)
        {
            Common.Log(ex, "删除数据包被主动取消");
            ReloadDatapackFileList(true);
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "删除数据包出现未知错误",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Instance.Saves.Error.OperationFailed"));
            ReloadDatapackFileList(true);
        }

        LoaderRun(Loading.LoaderFolderRunType.UpdateOnly);
    }

    // 取消选择
    private void BtnSelectCancel_Click(object sender, Common.RouteEventArgs e)
    {
        ChangeAllSelected(false);
    }

    // 收藏
    private void BtnSelectFavorites_Click(object sender, Common.RouteEventArgs e)
    #endregion
    #region 单个资源项
    {
        var selected = LocalComp.CompResourceListLoader.output
            .Where(m => selectedDatapacks.Contains(m.RawPath) && m.Comp is not null).Select(i => i.Comp).ToList();
        Comp.CompFavorites.ShowMenu(selected, (UIElement)sender);
    }

    // 分享
    private void BtnSelectShare_Click(object sender, Common.RouteEventArgs e)
    {
        var shareList = LocalComp.CompResourceListLoader.output
            .Where(m => selectedDatapacks.Contains(m.RawPath) && m.Comp is not null).Select(i => i.Comp.Id).ToHashSet();
        Common.ClipboardSet(Comp.CompFavorites.GetShareCode(shareList));
        ChangeAllSelected(false);
    }



    // 详情
    public void Info_Click(object sender, EventArgs e)
    {
        try
        {
            var datapackEntry = ((MyLocalCompItem)(sender is MyIconButton iconBtn ? iconBtn.Tag : sender)).Entry;

            // 加载失败信息
            if (datapackEntry.State == LocalComp.LocalCompFile.LocalFileStatus.Unavailable)
            {
                Launcher.MyMsgBox(
                    Lang.Text(
                        "Instance.Saves.Datapack.Info.ReadFailed.WithDetail",
                        datapackEntry.FileUnavailableReason.ToString()),
                    Lang.Text("Instance.Saves.Datapack.Info.ReadFailedTitle"));
                return;
            }

            if (datapackEntry.Comp is not null)
            {
                // 跳转到数据包下载页面
                Launcher.frmMain.PageChange(new FormMain.PageStackData
                {
                    page = FormMain.PageType.CompDetail,
                    additional = (datapackEntry.Comp, new List<string>(),
                        PageInstanceLeft.mcInstance.Info.VanillaName,
                        Comp.CompLoaderType.Minecraft, Comp.CompType.DataPack, null)
                });
            }
            else
            {
                // 获取信息
                var contentLines = new List<string>();

                if (datapackEntry.Description is not null)
                    contentLines.Add(datapackEntry.Description + "\r\n");
                if (datapackEntry.Authors is not null)
                    contentLines.Add(Lang.Text("Instance.Saves.Datapack.Info.Author") + datapackEntry.Authors);
                contentLines.Add(Lang.Text("Instance.Saves.Datapack.Info.File") + datapackEntry.FileName + "（" +
                                 Common.GetString(GetDatapackFileInfo(datapackEntry.path).Length) +
                                 "）");
                if (datapackEntry.Version is not null)
                    contentLines.Add(Lang.Text("Instance.Saves.Datapack.Info.Version") + datapackEntry.Version);

                var debugInfo = new List<string>();
                if (datapackEntry.ModId is not null) debugInfo.Add(Lang.Text("Instance.Saves.Datapack.Info.DatapackId") + datapackEntry.ModId);
                if (debugInfo.Any())
                {
                    contentLines.Add("");
                    contentLines.AddRange(debugInfo);
                }

                // 显示详情信息
                if (datapackEntry.Url is null)
                    Launcher.MyMsgBox(contentLines.Join("\r\n"), datapackEntry.Name,
                        Lang.Text("Instance.Resource.Item.Info.Return"));
                else if (Launcher.MyMsgBox(contentLines.Join("\r\n"), datapackEntry.Name,
                             Lang.Text("Instance.Resource.Item.Info.OpenWebsite"),
                             Lang.Text("Instance.Resource.Item.Info.Return")) == 1)
                    Common.OpenWebsite(datapackEntry.Url);
            }
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "获取数据包详情失败",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Instance.Saves.Error.OperationFailed"));
        }
    }

    // 打开文件所在的位置
    public void Open_Click(MyIconButton sender, EventArgs e)
    {
        try
        {
            var listItem = (MyLocalCompItem)sender.Tag;
            Common.OpenExplorer(listItem.Entry.path);
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "打开数据包文件位置失败",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Instance.Saves.Error.OperationFailed"));
        }
    }

    // 删除
    public void Delete_Click(MyIconButton sender, EventArgs e)
    {
        var listItem = (MyLocalCompItem)sender.Tag;
    #endregion
    #region 搜索
        DeleteDatapacks(new[] { listItem.Entry });
    }

    // 启用
    public void Enable_Click(MyIconButton sender, EventArgs e)
    {
        var listItem = (MyLocalCompItem)sender.Tag;
        ToggleDatapacks(new[] { listItem.Entry }, true);
    }

    // 禁用
    public void Disable_Click(MyIconButton sender, EventArgs e)
    {
        var listItem = (MyLocalCompItem)sender.Tag;
        ToggleDatapacks(new[] { listItem.Entry }, false);
    }



    public bool IsSearching => !string.IsNullOrWhiteSpace(SearchBox.Text);
    private List<LocalComp.LocalCompFile> _searchResult;

    public void SearchRun(object sender, EventArgs e)
    {
        try
        {
            if (IsSearching)
            {
                // 构造请求
                var queryList = new List<Common.SearchEntry<LocalComp.LocalCompFile>>();
                foreach (var Entry in LocalComp.CompResourceListLoader.output)
                {
                    var searchSource = new List<Common.SearchSource>();
                    searchSource.Add(new Common.SearchSource(Entry.Name));
                    searchSource.Add(new Common.SearchSource(Entry.FileName));
                    if (Entry.Version is not null)
                        searchSource.Add(new Common.SearchSource(Entry.Version, 0.2d));
                    if (Entry.Description is not null && !string.IsNullOrEmpty(Entry.Description))
                        searchSource.Add(new Common.SearchSource(Entry.Description, 0.4d));
                    if (Entry.Comp is not null)
                    {
                        if ((Entry.Comp.RawName ?? "") != (Entry.Name ?? ""))
                            searchSource.Add(new Common.SearchSource(Entry.Comp.RawName));
                        if ((Entry.Comp.TranslatedName ?? "") != (Entry.Comp.RawName ?? ""))
                            searchSource.Add(new Common.SearchSource(Entry.Comp.TranslatedName));
                        if ((Entry.Comp.Description ?? "") != (Entry.Description ?? ""))
                            searchSource.Add(new Common.SearchSource(Entry.Comp.Description, 0.4d));
                        searchSource.Add(new Common.SearchSource(string.Join("", Entry.Comp.Tags),
                            0.2d));
                    }
    #endregion

                    queryList.Add(new Common.SearchEntry<LocalComp.LocalCompFile>
                        { item = Entry, searchSource = searchSource });
                }

                // 进行搜索
                _searchResult = Common
                    .Search(queryList, SearchBox.Text, Common.MaxLocalSearchDepth, 0.35d)
                    .Select(r => r.item).ToList();
            }

            RefreshUI();
        }
        catch (Exception ex)
        {
            Common.Log(ex, "搜索过程中发生异常");
        }
    }

}
