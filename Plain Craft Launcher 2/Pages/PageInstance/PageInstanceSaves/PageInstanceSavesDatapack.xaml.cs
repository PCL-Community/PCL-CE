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

namespace PCL;

public partial class PageInstanceSavesDatapack : IRefreshable
{
    #region 数据包信息缓存

    private readonly Dictionary<string, (DateTime CreationTime, long Length)> datapackFileInfoCache = new();

    // 获取数据包信息（带缓存）
    private (DateTime CreationTime, long Length) GetDatapackFileInfo(string path)
    {
        (DateTime CreationTime, long Length) cacheItem;
        if (datapackFileInfoCache.TryGetValue(path, out cacheItem)) return cacheItem;

        try
        {
            var fileInfo = new FileInfo(path);
            var newItem = (fileInfo.CreationTime, fileInfo.Length);
            if (!datapackFileInfoCache.ContainsKey(path)) datapackFileInfoCache.Add(path, newItem);
            return newItem;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "获取数据包信息失败: " + path);
            return (DateTime.MinValue, 0L);
        }
    }

    // 页面关闭时清理缓存
    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        datapackFileInfoCache.Clear();
    }

    #endregion

    #region 初始化

    private readonly MyLocalCompItem.SwipeSelect currentSwipSelect;

    public PageInstanceSavesDatapack()
    {
        currentSwipSelect = new MyLocalCompItem.SwipeSelect { TargetFrm = this };

        InitializeComponent();
        Unloaded += Page_Unloaded;
        Loaded += (_, _) => PageOther_Loaded();
        LoaderInit();
        pageExit += UnselectedAllWithAnimation;
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
        Load.stateChanged += (_, _, _) => UnselectedAllWithAnimation();
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

    private ModLocalComp.CompLocalLoaderData GetRequireLoaderData()
    {
        var res = new ModLocalComp.CompLocalLoaderData();
        res.gameVersion = PageInstanceLeft.instance;
        res.frm = null;
        res.loaders = new[] { ModComp.CompLoaderType.Minecraft }.ToList();
        res.compPath = Path.Combine(PageInstanceSavesLeft.currentSave, "datapacks");
        res.compType = ModComp.CompType.DataPack;
        return res;
    }

    private bool isLoad;

    public void PageOther_Loaded()
    {
        if (ModMain.frmMain.pageLast.page != FormMain.PageType.CompDetail)
            PanBack.ScrollToHome();
        ModAnimation.AniControlEnabled += 1;
        selectedDatapacks.Clear();
        ReloadDatapackFileList();
        ChangeAllSelected(false);
        ModAnimation.AniControlEnabled -= 1;

        // 非重复加载部分
        if (isLoad)
            return;
        isLoad = true;

        ModMain.frmMain.KeyDown += FrmMain_KeyDown;
        // 调整按钮边距（这玩意儿没法从 XAML 改）
        foreach (MyRadioButton Btn in PanFilter.Children)
            Btn.LabText.Margin = new Thickness(-2, 0d, 8d, 0d);
    }

    /// <summary>
    ///     刷新数据包列表。
    /// </summary>
    public void ReloadDatapackFileList(bool ForceReload = false)
    {
        if (LoaderRun(ForceReload
                ? ModLoader.LoaderFolderRunType.ForceRun
                : ModLoader.LoaderFolderRunType.RunOnUpdated))
        {
            ModBase.Log("[System] 已刷新数据包列表");
            datapackFileInfoCache.Clear();

            ModBase.RunInUi(() =>
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

    public bool LoaderRun(ModLoader.LoaderFolderRunType Type)
    {
        var loadPath = Path.Combine(PageInstanceSavesLeft.currentSave, "datapacks");
        return ModLoader.LoaderFolderRun(ModLocalComp.compResourceListLoader, loadPath, Type,
            LoaderInput: GetRequireLoaderData());
    }

    #endregion

    #region UI 化

    /// <summary>
    ///     已加载的数据包 UI 缓存。Key 为数据包的 RawPath。
    /// </summary>
    public Dictionary<string, MyLocalCompItem> datapackItems = new();

    /// <summary>
    ///     将加载器结果的数据包列表加载为 UI。
    /// </summary>
    private void LoadUIFromLoaderOutput()
    {
        try
        {
            // 判断应该显示哪一个页面
            if (ModLocalComp.compResourceListLoader.output.Any())
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
            var itemsToShow = ModLocalComp.compResourceListLoader.output.ToList();

            foreach (var DatapackEntity in itemsToShow)
                datapackItems[DatapackEntity.RawPath] = BuildLocalCompItem(DatapackEntity);

            // 显示结果
            ModBase.RunInUi(() =>
            {
                Filter = FilterType.All;
                SearchBox.Text = ""; // 这会触发结果刷新，所以需要在 DatapackItems 更新之后
                RefreshUI();
                SetSortMethod(SortMethod.CompName);
            });
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "加载数据包列表 UI 失败", ModBase.LogLevel.Feedback);
        }
    }

    private MyLocalCompItem BuildLocalCompItem(ModLocalComp.LocalCompFile Entry)
    {
        try
        {
            ModAnimation.AniControlEnabled += 1;
            var newItem = new MyLocalCompItem
            {
                SnapsToDevicePixels = true,
                Entry = Entry,
                buttonHandler = BuildLocalCompItemBtnHandler,
                Checked = selectedDatapacks.Contains(Entry.RawPath)
            };
            newItem.CurrentSwipe = currentSwipSelect;
            newItem.Tags = Entry.Tags;
            Entry.onCompUpdate += _ => newItem.Refresh();
            newItem.Refresh();
            ModAnimation.AniControlEnabled -= 1;
            return newItem;
        }
        catch (Exception ex)
        {
            ModAnimation.AniControlEnabled -= 1;
            ModBase.Log(ex, $"创建 UI 项失败：{Entry.RawPath}");
            throw;
        }
    }

    private void BuildLocalCompItemBtnHandler(MyLocalCompItem sender, EventArgs e)
    {
        // 点击事件
        sender.changed += (ss, e) => CheckChanged((MyLocalCompItem)ss, e);

        // 文件项的点击事件：切换选中状态
        sender.Click += (ss, e) =>
        {
            var s = (MyLocalCompItem)ss;
            s.Checked = !s.Checked;
        };

        // 图标按钮
        var btnOpen = new MyIconButton { LogoScale = 1.05d, Logo = Icon.IconButtonOpen, Tag = sender };
        btnOpen.ToolTip = Lang.Text("Instance.Saves.OpenFileLocation");
        ToolTipService.SetPlacement(btnOpen, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnOpen, 30d);
        ToolTipService.SetHorizontalOffset(btnOpen, 2d);
        btnOpen.Click += (sender, e) => Open_Click((MyIconButton)sender, e);

        var btnCont = new MyIconButton { LogoScale = 1d, Logo = Icon.IconButtonInfo, Tag = sender };
        btnCont.ToolTip = Lang.Text("Instance.Saves.Detail");
        ToolTipService.SetPlacement(btnCont, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnCont, 30d);
        ToolTipService.SetHorizontalOffset(btnCont, 2d);
        btnCont.Click += Info_Click;
        sender.MouseRightButtonUp += Info_Click;

        var btnDelete = new MyIconButton { LogoScale = 1d, Logo = Icon.IconButtonDelete, Tag = sender };
        btnDelete.ToolTip = Lang.Text("Common.Action.Delete");
        ToolTipService.SetPlacement(btnDelete, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnDelete, 30d);
        ToolTipService.SetHorizontalOffset(btnDelete, 2d);
        btnDelete.Click += (sender, e) => Delete_Click((MyIconButton)sender, e);

        if (sender.Entry.State == ModLocalComp.LocalCompFile.LocalFileStatus.Fine)
        {
            var btnDisable = new MyIconButton { LogoScale = 1d, Logo = Icon.IconButtonStop, Tag = sender };
            btnDisable.ToolTip = Lang.Text("Instance.Resource.Disable");
            ToolTipService.SetPlacement(btnDisable, PlacementMode.Center);
            ToolTipService.SetVerticalOffset(btnDisable, 30d);
            ToolTipService.SetHorizontalOffset(btnDisable, 2d);
            btnDisable.Click += (ss, e) => Disable_Click((MyIconButton)ss, e);
            sender.Buttons = new[] { btnCont, btnOpen, btnDisable, btnDelete };
        }
        else if (sender.Entry.State == ModLocalComp.LocalCompFile.LocalFileStatus.Disabled)
        {
            var btnEnable = new MyIconButton { LogoScale = 1d, Logo = Icon.IconButtonCheck, Tag = sender };
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
    ///     刷新整个 UI。
    /// </summary>
    public void RefreshUI()
    {
        if (PanList is null)
            return;
        var showingDatapacks = (IsSearching ? searchResult : datapackItems.Values.Select(i => i.Entry))
            .Where(m => CanPassFilter(m)).ToList();

        // 对显示的数据包进行排序
        if (showingDatapacks.Any())
        {
            var sortMethod = GetSortMethod(currentSortMethod);
            showingDatapacks.Sort((a, b) => sortMethod(a, b));
        }

        // 重新列出列表
        ModAnimation.AniControlEnabled += 1;
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

                ModStyle.MinecraftFormatter.SetColorfulTextLab(item.LabTitle.Text, item.LabTitle,
                    ThemeService.IsDarkMode);
                ModStyle.MinecraftFormatter.SetColorfulTextLab(item.LabInfo.Text, item.LabInfo,
                    ThemeService.IsDarkMode);
                item.Checked = selectedDatapacks.Contains(TargetDatapack.RawPath); // 更新选中状态
                PanList.Children.Add(item);
            }
        }
        else
        {
            PanList.Visibility = Visibility.Collapsed;
        }

        ModAnimation.AniControlEnabled -= 1;
        selectedDatapacks =
            new HashSet<string>(selectedDatapacks.Where(m =>
                showingDatapacks.Any(s => (s.RawPath ?? "") == (m ?? ""))));
        RefreshBars();
    }

    /// <summary>
    ///     刷新顶栏和底栏显示。
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
            var itemSource = (IsSearching ? searchResult : datapackItems.Values.Select(i => i.Entry)).ToArray();
            await Task.Run(() =>
            {
                foreach (var item in itemSource)
                {
                    anyCount += 1;
                    if (item.CanUpdate) updateCount += 1;
                    if (item.State == ModLocalComp.LocalCompFile.LocalFileStatus.Fine) enabledCount += 1;
                    if (item.State == ModLocalComp.LocalCompFile.LocalFileStatus.Disabled) disabledCount += 1;
                    if (item.State == ModLocalComp.LocalCompFile.LocalFileStatus.Unavailable) unavalialeCount += 1;
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
                    foreach (var DatapackEntity in ModLocalComp.compResourceListLoader.output)
                        if (selectedDatapacks.Contains(DatapackEntity.RawPath))
                        {
                            if (DatapackEntity.CanUpdate) hasUpdate = true;
                            if (DatapackEntity.State == ModLocalComp.LocalCompFile.LocalFileStatus.Fine)
                                hasEnabled = true;
                            else if (DatapackEntity.State == ModLocalComp.LocalCompFile.LocalFileStatus.Disabled)
                                hasDisabled = true;
                            if (DatapackEntity.Comp is null || string.IsNullOrEmpty(DatapackEntity.Comp.id))
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
            if (ModAnimation.AniControlEnabled == 0)
            {
                PanListBack.Margin = new Thickness(0d, 0d, 0d, selected ? 95 : 15);
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
                                Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)),
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
                                Ease: new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)),
                            ModAnimation.AaCode(() => CardSelect.Visibility = Visibility.Collapsed, After: true)
                        }, "Datapack Sidebar");
                }
            }
            else
            {
                ModAnimation.AniStop("Datapack Sidebar");
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
        }));
    }

    private int bottomBarShownCount;

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
            ModBase.Log(ex, "打开 datapacks 文件夹失败", ModBase.LogLevel.Msgbox);
        }
    }

    /// <summary>
    ///     全选。
    /// </summary>
    private void BtnManageSelectAll_Click(object sender, MouseButtonEventArgs e)
    {
        ChangeAllSelected(selectedDatapacks.Count < PanList.Children.Count);
    }

    /// <summary>
    ///     安装数据包。
    /// </summary>
    private void BtnManageInstall_Click(object sender, MouseButtonEventArgs e)
    {
        var fileList = SystemDialogs.SelectFiles("数据包文件(*.zip)|*.zip", "选择要安装的数据包");
        if (fileList is null || !fileList.Any())
            return;
        InstallDatapackFiles(fileList);
        Refresh();
    }

    /// <summary>
    ///     安装数据包文件。
    /// </summary>
    public static void InstallDatapackFiles(IEnumerable<string> FilePathList)
    {
        if (!FilePathList.Any())
            return;

        var extension = FilePathList.First().AfterLast(".").ToLower();

        // 检查文件扩展名
        if (extension != "zip")
        {
            ModMain.Hint(Lang.Text("Instance.Resource.Install.UnsupportedFormat", extension, Lang.Text("Download.Comp.Type.DataPack"), "zip"), ModMain.HintType.Critical);
            return;
        }

        // 检查回收站
        if (FilePathList.First().Contains(@":\$RECYCLE.BIN\"))
        {
            ModMain.Hint(Lang.Text("Instance.Resource.Install.RestoreFromRecycleBin"), ModMain.HintType.Critical);
            return;
        }

        ModBase.Log($"[System] 文件为 {extension} 格式，尝试作为数据包安装");

        // 确认安装
        if (!(ModMain.frmMain.pageCurrent == FormMain.PageType.InstanceSetup &&
              ModMain.frmMain.PageCurrentSub == FormMain.PageSubType.VersionSavesDatapack))
            if (ModMain.MyMsgBox(Lang.Text("Instance.Saves.Datapack.Install.Message"),
                    Lang.Text("Instance.Saves.Datapack.Install.Title"), Lang.Text("Common.Action.Confirm"),
                    Lang.Text("Common.Action.Cancel")) != 1)
                return;

        // 执行安装
        try
        {
            var datapackFolder = Path.Combine(PageInstanceSavesLeft.currentSave, "datapacks");
            Directory.CreateDirectory(datapackFolder);

            foreach (var FilePath in FilePathList)
            {
                var newFileName = ModBase.GetFileNameFromPath(FilePath);
                var destFile = datapackFolder + newFileName;

                if (File.Exists(destFile))
                    if (ModMain.MyMsgBox(Lang.Text("Instance.Resource.Install.OverwriteConfirm.Message", newFileName), Lang.Text("Instance.Resource.Install.OverwriteConfirm.Title"), Lang.Text("Common.Action.Overwrite"), Lang.Text("Common.Action.Cancel")) != 1)
                        continue;

                ModBase.CopyFile(FilePath, destFile);
            }

            if (FilePathList.Count() == 1)
                ModMain.Hint(Lang.Text("Instance.Resource.Install.SuccessSingle", ModBase.GetFileNameFromPath(FilePathList.First())), ModMain.HintType.Finish);
            else
                ModMain.Hint(Lang.Text("Instance.Resource.Install.SuccessMultiple", FilePathList.Count(), Lang.Text("Download.Comp.Type.DataPack")), ModMain.HintType.Finish);

            // 刷新列表
            if (ModMain.frmMain.pageCurrent == FormMain.PageType.InstanceSetup &&
                ModMain.frmMain.PageCurrentSub == FormMain.PageSubType.VersionSavesDatapack)
                if (ModMain.frmInstanceSavesDatapack is not null)
                    ModMain.frmInstanceSavesDatapack.ReloadDatapackFileList(true);
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, "复制数据包文件失败", ModBase.LogLevel.Msgbox);
        }
    }

    /// <summary>
    ///     下载数据包。
    /// </summary>
    private void BtnManageDownload_Click(object sender, MouseButtonEventArgs e)
    {
        ModMain.frmMain.PageChange(FormMain.PageType.Download, FormMain.PageSubType.DownloadDataPack);
        PageComp.targetVersion = PageInstanceLeft.instance; // 将当前实例设置为筛选器
    }

    /// <summary>
    ///     导出信息。
    /// </summary>
    private void BtnManageInfoExport_Click(object sender, MouseButtonEventArgs e)
    {
        var choice =
            ModMain.MyMsgBox(
                Lang.Text("Instance.Saves.Datapack.Export.Mode.Message"),
                Lang.Text("Instance.Resource.Export.Mode.Title"), Lang.Text("Instance.Resource.Export.Mode.Txt"), Lang.Text("Instance.Resource.Export.Mode.Csv"), Lang.Text("Common.Action.Cancel"));

        void ExportText(string Content, string FileName)
        {
            try
            {
                var savePath =
                    SystemDialogs.SelectSaveFile(Lang.Text("Instance.Resource.Export.SelectSaveLocation"), FileName, Lang.Text("Instance.Resource.Export.FilesFilter"));
                if (string.IsNullOrWhiteSpace(savePath)) return;
                File.WriteAllText(savePath, Content, Encoding.UTF8);
                ModBase.OpenExplorer(savePath);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "导出数据包信息失败", ModBase.LogLevel.Msgbox);
            }
        }

        ;
        switch (choice)
        {
            case 1: // TXT
            {
                var exportContent = new List<string>();
                foreach (var DatapackEntity in ModLocalComp.compResourceListLoader.output)
                    exportContent.Add(DatapackEntity.FileName);
                ExportText(exportContent.Join("\r\n"),
                    ModBase.GetFolderNameFromPath(PageInstanceSavesLeft.currentSave) + "的数据包信息.txt");
                break;
            }

            case 2: // CSV
            {
                var exportContent = new List<string>();
                exportContent.Add("文件名,数据包名称,数据包版本,此版本更新时间,工程 ID,文件大小（字节）,文件路径");
                foreach (var DatapackEntity in ModLocalComp.compResourceListLoader.output)
                    exportContent.Add(
                        $"{DatapackEntity.FileName},{DatapackEntity.Comp?.TranslatedName},{DatapackEntity.Version},{DatapackEntity.compFile?.releaseDate},{DatapackEntity.Comp?.id},{GetDatapackFileInfo(DatapackEntity.path).Length},{DatapackEntity.path}");
                ExportText(exportContent.Join("\r\n"),
                    ModBase.GetFolderNameFromPath(PageInstanceSavesLeft.currentSave) + "的数据包信息.csv");
                break;
            }
        }
    }

    #endregion

    #region 选择

    /// <summary>
    ///     选择的数据包的路径。
    /// </summary>
    public HashSet<string> selectedDatapacks = new();

    // 单项切换选择状态
    public void CheckChanged(MyLocalCompItem sender, ModBase.RouteEventArgs e)
    {
        if (ModAnimation.AniControlEnabled != 0)
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
    private void ChangeAllSelected(bool Value)
    {
        ModAnimation.AniControlEnabled += 1;
        selectedDatapacks.Clear();
        foreach (var Item in datapackItems.Values)
        {
            var shouldSelected = Value && PanList.Children.Contains(Item);
            Item.Checked = shouldSelected;
            if (shouldSelected)
                selectedDatapacks.Add(Item.Entry.RawPath);
        }

        ModAnimation.AniControlEnabled -= 1;
        RefreshBars();
    }

    private void UnselectedAllWithAnimation()
    {
        var cacheAniControlEnabled = ModAnimation.AniControlEnabled;
        ModAnimation.AniControlEnabled = 0;
        ChangeAllSelected(false);
        ModAnimation.AniControlEnabled += cacheAniControlEnabled;
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

    private FilterType _Filter = FilterType.All;

    public FilterType Filter
    {
        get => _Filter;
        set
        {
            if (_Filter == value)
                return;
            _Filter = value;
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
    }

    public enum FilterType
    {
        All = 0,
        Enabled = 1,
        Disabled = 2,
        CanUpdate = 3,
        Unavailable = 4
    }

    /// <summary>
    ///     检查该数据包项是否符合当前筛选的类别。
    /// </summary>
    private bool CanPassFilter(ModLocalComp.LocalCompFile CheckingDatapack)
    {
        switch (Filter)
        {
            case FilterType.All:
            {
                return true;
            }
            case FilterType.Enabled:
            {
                return CheckingDatapack.State == ModLocalComp.LocalCompFile.LocalFileStatus.Fine;
            }
            case FilterType.Disabled:
            {
                return CheckingDatapack.State == ModLocalComp.LocalCompFile.LocalFileStatus.Disabled;
            }
            case FilterType.CanUpdate:
            {
                return CheckingDatapack.CanUpdate;
            }
            case FilterType.Unavailable:
            {
                return CheckingDatapack.State == ModLocalComp.LocalCompFile.LocalFileStatus.Unavailable;
            }

            default:
            {
                return false;
            }
        }
    }

    // 点击筛选项触发的改变
    private void ChangeFilter(MyRadioButton sender, bool raiseByMouse)
    {
        Filter = (FilterType)Convert.ToInt32(sender.Tag);
        RefreshUI();
        DoSort();
    }

    #endregion

    #region 排序

    private SortMethod currentSortMethod = SortMethod.CompName;

    private void SetSortMethod(SortMethod Target)
    {
        currentSortMethod = Target;
        BtnSort.Text = Lang.Text("Instance.Resource.Sort.Text", GetSortName(Target));
        DoSort();
    }

    private enum SortMethod
    {
        FileName,
        CompName,
        CreateTime,
        DatapackFileSize
    }

    private string GetSortName(SortMethod Method)
    {
        switch (Method)
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

    private void BtnSortClick(object sender, ModBase.RouteEventArgs e)
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

    private readonly object sortLock = new();

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
                var method = GetSortMethod(currentSortMethod);

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
                ModBase.Log(ex, "执行排序时出错", ModBase.LogLevel.Hint);
            }
        }
    }

    private Func<ModLocalComp.LocalCompFile, ModLocalComp.LocalCompFile, int> GetSortMethod(SortMethod Method)
    {
        switch (Method)
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

    #endregion

    #region 下边栏

    // 启用
    private void BtnSelectEnable_Click(object sender, ModBase.RouteEventArgs e)
    {
        ToggleDatapacks(
            ModLocalComp.compResourceListLoader.output.Where(m => selectedDatapacks.Contains(m.RawPath)).ToList(),
            true);
        ChangeAllSelected(false);
    }

    // 禁用
    private void BtnSelectDisable_Click(object sender, ModBase.RouteEventArgs e)
    {
        ToggleDatapacks(
            ModLocalComp.compResourceListLoader.output.Where(m => selectedDatapacks.Contains(m.RawPath)).ToList(),
            false);
        ChangeAllSelected(false);
    }

    /// <summary>
    ///     启用/禁用数据包（通过重命名文件夹为 .disabled）
    /// </summary>
    private void ToggleDatapacks(IEnumerable<ModLocalComp.LocalCompFile> DatapackList, bool IsEnable)
    {
        var isSuccessful = true;
        foreach (var DatapackE in DatapackList)
        {
            var datapackEntity = DatapackE;
            string newPath = null;

            if (datapackEntity.State == ModLocalComp.LocalCompFile.LocalFileStatus.Fine && !IsEnable)
                // 禁用 - 添加 .disabled 后缀
                newPath = datapackEntity.path + ".disabled";
            else if (datapackEntity.State == ModLocalComp.LocalCompFile.LocalFileStatus.Disabled && IsEnable)
                // 启用 - 移除 .disabled 后缀
                newPath = datapackEntity.RawPath;
            else
                continue;

            // 重命名
            try
            {
                if (File.Exists(newPath))
                {
                    ModMain.MyMsgBox(Lang.Text("Instance.Saves.Datapack.Replace.FileNameConflict", ModBase.GetFileNameFromPath(newPath)));
                    continue;
                }

                FileSystem.Rename(datapackEntity.path, newPath);
            }
            catch (FileNotFoundException ex)
            {
                ModBase.Log(ex, $"未找到需要重命名的数据包（{datapackEntity.path ?? "null"}）", ModBase.LogLevel.Feedback);
                ReloadDatapackFileList(true);
                return;
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, $"重命名数据包失败（{datapackEntity.path ?? "null"}）");
                isSuccessful = false;
            }

            // 更改 Loader 中的列表
            var newDatapackEntity = new ModLocalComp.LocalCompFile(newPath);
            newDatapackEntity.FromJson(datapackEntity.ToJson());
            if (ModLocalComp.compResourceListLoader.output.Contains(datapackEntity))
            {
                var indexOfLoader = ModLocalComp.compResourceListLoader.output.IndexOf(datapackEntity);
                ModLocalComp.compResourceListLoader.output.RemoveAt(indexOfLoader);
                ModLocalComp.compResourceListLoader.output.Insert(indexOfLoader, newDatapackEntity);
            }

            if (searchResult is not null && searchResult.Contains(datapackEntity))
            {
                var indexOfResult = searchResult.IndexOf(datapackEntity);
                searchResult.Remove(datapackEntity);
                searchResult.Insert(indexOfResult, newDatapackEntity);
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
                ModBase.Log(ex, $"更新 UI 列表项失败：{datapackEntity.FileName}", ModBase.LogLevel.Hint);
            }
        }

        Dispatcher.Invoke(() => PanList.UpdateLayout(), DispatcherPriority.Background);

        if (isSuccessful)
        {
            RefreshBars();
        }
        else
        {
            ModMain.Hint(Lang.Text("Instance.Saves.Datapack.ToggleWarning"), ModMain.HintType.Critical);
            ReloadDatapackFileList(true);
        }

        LoaderRun(ModLoader.LoaderFolderRunType.UpdateOnly);
    }

    // 更新
    private void BtnSelectUpdate_Click(object sender, ModBase.RouteEventArgs e)
    {
        var updateList = ModLocalComp.compResourceListLoader.output
            .Where(m => selectedDatapacks.Contains(m.RawPath) && m.CanUpdate).ToList();
        if (!updateList.Any())
            return;
        UpdateResource(updateList);
        ChangeAllSelected(false);
    }

    /// <summary>
    ///     记录正在进行数据包更新的 datapacks 文件夹路径。
    /// </summary>
    public static List<string> updatingVersions = new();

    public void UpdateResource(IEnumerable<ModLocalComp.LocalCompFile> DatapackList)
    {
        // 更新前警告
        if (!States.Hint.FunctionDatapackUpdate || DatapackList.Count() >= 15)
        {
            if (ModMain.MyMsgBox(
                    Lang.Text("Instance.Saves.Datapack.Update.Warning.Message"),
                    Lang.Text("Instance.Saves.Datapack.Update.Warning.Title"), Lang.Text("Instance.Saves.Datapack.Update.Warning.Confirm"), Lang.Text("Common.Action.Cancel"), IsWarn: true) == 1)
                States.Hint.FunctionDatapackUpdate = true;
            else
                return;
        }

        try
        {
            // 构造下载信息
            DatapackList = DatapackList.ToList(); // 防止刷新影响迭代器
            var fileList = new List<DownloadFile>();
            var fileCopyList = new Dictionary<string, string>();
            foreach (var Entry in DatapackList)
            {
                var file = Entry.UpdateFile;
                if (!file.Available)
                    continue;
                // 添加到下载列表
                var tempAddress = ModBase.pathTemp + @"DownloadedComp\" + file.fileName;
                var realAddress = Path.Combine(PageInstanceSavesLeft.currentSave, "datapacks", file.fileName);
                fileList.Add(file.ToNetFile(tempAddress));
                fileCopyList[tempAddress] = realAddress;
            }

            // 构造加载器
            var installLoaders = new List<ModLoader.LoaderBase>();
            var finishedFileNames = new List<string>();
            installLoaders.Add(new LoaderDownload("下载新版数据包文件", fileList)
                { ProgressWeight = DatapackList.Count() * 1.5d });

            installLoaders.Add(new ModLoader.LoaderTask<int, int>("替换旧版数据包文件", _ =>
            {
                try
                {
                    foreach (var Entry in DatapackList)
                        if (File.Exists(Entry.path))
                            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(Entry.path, UIOption.AllDialogs,
                                RecycleOption.SendToRecycleBin);
                        else
                            ModBase.Log($"[DatapackUpdate] 未找到更新前的数据包文件，跳过对它的删除：{Entry.path}", ModBase.LogLevel.Debug);

                    foreach (var Entry in fileCopyList)
                    {
                        if (File.Exists(Entry.Value))
                        {
                            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(Entry.Value, UIOption.AllDialogs,
                                RecycleOption.SendToRecycleBin);
                            ModBase.Log($"[Datapack] 更新后的数据包文件已存在，将会把它放入回收站：{Entry.Value}", ModBase.LogLevel.Debug);
                        }

                        if (Directory.Exists(ModBase.GetPathFromFullPath(Entry.Value)))
                        {
                            File.Move(Entry.Key, Entry.Value);
                            finishedFileNames.Add(ModBase.GetFileNameFromPath(Entry.Value));
                        }
                        else
                        {
                            ModBase.Log($"[Datapack] 更新后的目标文件夹已被删除：{Entry.Value}", ModBase.LogLevel.Debug);
                        }
                    }
                }
                catch (OperationCanceledException ex)
                {
                    ModBase.Log(ex, "替换旧版数据包文件时被主动取消");
                }
            }));

            // 结束处理
            var loader = new ModLoader.LoaderCombo<IEnumerable<ModLocalComp.LocalCompFile>>(
                $"数据包更新：{ModBase.GetFolderNameFromPath(PageInstanceSavesLeft.currentSave)}", installLoaders);
            var pathDatapacks = Path.Combine(PageInstanceSavesLeft.currentSave, "datapacks");

            loader.OnStateChanged = _ =>
            {
                switch (loader.State)
                {
                    case ModBase.LoadState.Finished:
                    {
                        switch (finishedFileNames.Count)
                        {
                            case 0:
                            {
                                ModBase.Log("[DatapackUpdate] 没有数据包被成功更新");
                                break;
                            }
                            case 1:
                            {
                                ModMain.Hint(Lang.Text("Instance.Resource.Update.SuccessSingle", finishedFileNames.Single()), ModMain.HintType.Finish);
                                break;
                            }

                            default:
                            {
                                ModMain.Hint(Lang.Text("Instance.Resource.Update.SuccessMultiple", finishedFileNames.Count), ModMain.HintType.Finish);
                                break;
                            }
                        }

                        break;
                    }
                    case ModBase.LoadState.Failed:
                    {
                        ModMain.Hint(Lang.Text("Instance.Resource.Update.Failed", loader.Error.Message), ModMain.HintType.Critical);
                        break;
                    }
                    case ModBase.LoadState.Aborted:
                    {
                        ModMain.Hint(Lang.Text("Instance.Resource.Update.Aborted"));
                        break;
                    }

                    default:
                    {
                        return;
                    }
                }

                ModBase.Log($"[DatapackUpdate] 已从正在进行数据包更新的文件夹列表移除：{pathDatapacks}");
                updatingVersions.Remove(pathDatapacks);

                // 清理缓存
                ModBase.RunInNewThread(() =>
                {
                    try
                    {
                        foreach (var TempFile in fileCopyList.Keys)
                            if (File.Exists(TempFile))
                                File.Delete(TempFile);
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, "清理数据包更新缓存失败");
                    }
                }, "Clean Datapack Update Cache", ThreadPriority.BelowNormal);
            };

            // 启动加载器
            ModBase.Log($"[DatapackUpdate] 开始更新 {DatapackList.Count()} 个数据包：{pathDatapacks}");
            updatingVersions.Add(pathDatapacks);
            loader.Start();
            ModLoader.LoaderTaskbarAdd(loader);
            ModMain.frmMain.BtnExtraDownload.ShowRefresh();
            ModMain.frmMain.BtnExtraDownload.Ribble();
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
        DeleteDatapacks(ModLocalComp.compResourceListLoader.output.Where(m => selectedDatapacks.Contains(m.RawPath)));
        ChangeAllSelected(false);
    }

    private void DeleteDatapacks(IEnumerable<ModLocalComp.LocalCompFile> DatapackList)
    {
        try
        {
            var isSuccessful = true;
            var isShiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

            // 确认需要删除的文件
            DatapackList = DatapackList.SelectMany(Target =>
            {
                if (Target.State == ModLocalComp.LocalCompFile.LocalFileStatus.Fine)
                    return new[] { Target.path, Target.path + ".disabled" };

                return new[] { Target.path, Target.RawPath };
            }).Distinct().Where(m => File.Exists(m)).Select(m => new ModLocalComp.LocalCompFile(m)).ToList();

            // 实际删除文件
            foreach (var DatapackEntity in DatapackList)
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
                    ModBase.Log(ex, "删除数据包被主动取消");
                    ReloadDatapackFileList(true);
                    return;
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, $"删除数据包失败（{DatapackEntity.path}）", ModBase.LogLevel.Msgbox);
                    isSuccessful = false;
                }

                // 取消选中
                selectedDatapacks.Remove(DatapackEntity.RawPath);
                // 更改 Loader 和 UI 中的列表
                ModLocalComp.compResourceListLoader.output.Remove(DatapackEntity);
                searchResult?.Remove(DatapackEntity);
                datapackItems.Remove(DatapackEntity.RawPath);
                var indexOfUi = PanList.Children.IndexOf(PanList.Children.OfType<MyLocalCompItem>()
                    .FirstOrDefault(i => i.Entry.Equals(DatapackEntity)));
                if (indexOfUi >= 0)
                    PanList.Children.RemoveAt(indexOfUi);
            }

            RefreshBars();
            if (!isSuccessful)
            {
                ModMain.Hint(Lang.Text("Instance.Saves.Datapack.Delete.FileOccupied"), ModMain.HintType.Critical);
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
                if (DatapackList.Count() == 1)
                    ModMain.Hint(Lang.Text("Instance.Saves.Datapack.Delete.PermanentSingle", DatapackList.Single().FileName), ModMain.HintType.Finish);
                else
                    ModMain.Hint(Lang.Text("Instance.Saves.Datapack.Delete.PermanentMultiple", DatapackList.Count()), ModMain.HintType.Finish);
            }
            else if (DatapackList.Count() == 1)
            {
                ModMain.Hint(Lang.Text("Instance.Saves.Datapack.Delete.RecycleSingle", DatapackList.Single().FileName), ModMain.HintType.Finish);
            }
            else
            {
                ModMain.Hint(Lang.Text("Instance.Saves.Datapack.Delete.RecycleMultiple", DatapackList.Count()), ModMain.HintType.Finish);
            }
        }
        catch (OperationCanceledException ex)
        {
            ModBase.Log(ex, "删除数据包被主动取消");
            ReloadDatapackFileList(true);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "删除数据包出现未知错误", ModBase.LogLevel.Feedback);
            ReloadDatapackFileList(true);
        }

        LoaderRun(ModLoader.LoaderFolderRunType.UpdateOnly);
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
            .Where(m => selectedDatapacks.Contains(m.RawPath) && m.Comp is not null).Select(i => i.Comp).ToList();
        ModComp.CompFavorites.ShowMenu(selected, (UIElement)sender);
    }

    // 分享
    private void BtnSelectShare_Click(object sender, ModBase.RouteEventArgs e)
    {
        var shareList = ModLocalComp.compResourceListLoader.output
            .Where(m => selectedDatapacks.Contains(m.RawPath) && m.Comp is not null).Select(i => i.Comp.id).ToHashSet();
        ModBase.ClipboardSet(ModComp.CompFavorites.GetShareCode(shareList));
        ChangeAllSelected(false);
    }

    #endregion

    #region 单个资源项

    // 详情
    public void Info_Click(object sender, EventArgs e)
    {
        try
        {
            var datapackEntry = ((MyLocalCompItem)(sender is MyIconButton iconBtn ? iconBtn.Tag : sender)).Entry;

            // 加载失败信息
            if (datapackEntry.State == ModLocalComp.LocalCompFile.LocalFileStatus.Unavailable)
            {
                ModMain.MyMsgBox(
                    Lang.Text("Instance.Saves.Datapack.Info.ReadFailed") + "\r\n" + "\r\n" + Lang.Text("Instance.Resource.Item.Info.DetailedError") +
                    datapackEntry.FileUnavailableReason.Message, Lang.Text("Instance.Saves.Datapack.Info.ReadFailedTitle"));
                return;
            }

            if (datapackEntry.Comp is not null)
            {
                // 跳转到数据包下载页面
                ModMain.frmMain.PageChange(new FormMain.PageStackData
                {
                    page = FormMain.PageType.CompDetail,
                    additional = (datapackEntry.Comp, new List<string>(), PageInstanceLeft.instance.Info.vanillaName,
                        ModComp.CompLoaderType.Minecraft, ModComp.CompType.DataPack, null, null, null)
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
                                 ModBase.GetString(GetDatapackFileInfo(datapackEntry.path).Length) + "）");
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
                    ModMain.MyMsgBox(contentLines.Join("\r\n"), datapackEntry.Name, Lang.Text("Instance.Resource.Item.Info.Return"));
                else if (ModMain.MyMsgBox(contentLines.Join("\r\n"), datapackEntry.Name, Lang.Text("Instance.Resource.Item.Info.OpenWebsite"), Lang.Text("Instance.Resource.Item.Info.Return")) == 1)
                    ModBase.OpenWebsite(datapackEntry.Url);
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "获取数据包详情失败", ModBase.LogLevel.Feedback);
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
            ModBase.Log(ex, "打开数据包文件位置失败", ModBase.LogLevel.Feedback);
        }
    }

    // 删除
    public void Delete_Click(MyIconButton sender, EventArgs e)
    {
        var listItem = (MyLocalCompItem)sender.Tag;
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

    #endregion

    #region 搜索

    public bool IsSearching => !string.IsNullOrWhiteSpace(SearchBox.Text);
    private List<ModLocalComp.LocalCompFile> searchResult;

    public void SearchRun(object sender, EventArgs e)
    {
        try
        {
            if (IsSearching)
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
                        if ((Entry.Comp.rawName ?? "") != (Entry.Name ?? ""))
                            searchSource.Add(new ModBase.SearchSource(Entry.Comp.rawName, 1d));
                        if ((Entry.Comp.TranslatedName ?? "") != (Entry.Comp.rawName ?? ""))
                            searchSource.Add(new ModBase.SearchSource(Entry.Comp.TranslatedName, 1d));
                        if ((Entry.Comp.description ?? "") != (Entry.Description ?? ""))
                            searchSource.Add(new ModBase.SearchSource(Entry.Comp.description, 0.4d));
                        searchSource.Add(new ModBase.SearchSource(string.Join("", Entry.Comp.tags), 0.2d));
                    }

                    queryList.Add(new ModBase.SearchEntry<ModLocalComp.LocalCompFile>
                        { item = Entry, searchSource = searchSource });
                }

                // 进行搜索
                searchResult = ModBase.Search(queryList, SearchBox.Text, 6, 0.35d).Select(r => r.item).ToList();
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
