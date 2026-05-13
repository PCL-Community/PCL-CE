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

namespace PCL;

public partial class PageInstanceSavesDatapack : IRefreshable
{
    #region 数据包信息缓存

    private readonly Dictionary<string, (DateTime CreationTime, long Length)> DatapackFileInfoCache = new();

    // 获取数据包信息（带缓存）
    private (DateTime CreationTime, long Length) GetDatapackFileInfo(string path)
    {
        (DateTime CreationTime, long Length) cacheItem;
        if (DatapackFileInfoCache.TryGetValue(path, out cacheItem)) return cacheItem;

        try
        {
            var fileInfo = new FileInfo(path);
            var newItem = (fileInfo.CreationTime, fileInfo.Length);
            if (!DatapackFileInfoCache.ContainsKey(path)) DatapackFileInfoCache.Add(path, newItem);
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
        DatapackFileInfoCache.Clear();
    }

    #endregion

    #region 初始化

    private readonly MyLocalCompItem.SwipeSelect CurrentSwipSelect;

    public PageInstanceSavesDatapack()
    {
        CurrentSwipSelect = new MyLocalCompItem.SwipeSelect { TargetFrm = this };

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

    private ModLocalComp.CompLocalLoaderData GetRequireLoaderData()
    {
        var res = new ModLocalComp.CompLocalLoaderData();
        res.GameVersion = PageInstanceLeft.Instance;
        res.Frm = null;
        res.Loaders = new[] { ModComp.CompLoaderType.Minecraft }.ToList();
        res.CompPath = Path.Combine(PageInstanceSavesLeft.CurrentSave, "datapacks");
        res.CompType = ModComp.CompType.DataPack;
        return res;
    }

    private bool IsLoad;

    public void PageOther_Loaded()
    {
        if (ModMain.FrmMain.PageLast.Page != FormMain.PageType.CompDetail)
            PanBack.ScrollToHome();
        ModAnimation.AniControlEnabled += 1;
        SelectedDatapacks.Clear();
        ReloadDatapackFileList();
        ChangeAllSelected(false);
        ModAnimation.AniControlEnabled -= 1;

        // 非重复加载部分
        if (IsLoad)
            return;
        IsLoad = true;

        ModMain.FrmMain.KeyDown += FrmMain_KeyDown;
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
            DatapackFileInfoCache.Clear();

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
        ModMain.FrmInstanceSavesDatapack.ReloadDatapackFileList(true);
        ModBase.Log("[Datapack] 刷新数据包列表");
    }

    private void LoaderInit()
    {
        PageLoaderInit(Load, PanLoad, PanAllBack, null, ModLocalComp.CompResourceListLoader,
            _ => LoadUIFromLoaderOutput(), () => ModComp.CompType.DataPack, false);
    }

    private void Load_Click(object sender, MouseButtonEventArgs e)
    {
        if (ModLocalComp.CompResourceListLoader.State == ModBase.LoadState.Failed)
            LoaderRun(ModLoader.LoaderFolderRunType.ForceRun);
    }

    public bool LoaderRun(ModLoader.LoaderFolderRunType Type)
    {
        var LoadPath = Path.Combine(PageInstanceSavesLeft.CurrentSave, "datapacks");
        return ModLoader.LoaderFolderRun(ModLocalComp.CompResourceListLoader, LoadPath, Type,
            LoaderInput: GetRequireLoaderData());
    }

    #endregion

    #region UI 化

    /// <summary>
    ///     已加载的数据包 UI 缓存。Key 为数据包的 RawPath。
    /// </summary>
    public Dictionary<string, MyLocalCompItem> DatapackItems = new();

    /// <summary>
    ///     将加载器结果的数据包列表加载为 UI。
    /// </summary>
    private void LoadUIFromLoaderOutput()
    {
        try
        {
            // 判断应该显示哪一个页面
            if (ModLocalComp.CompResourceListLoader.Output.Any())
            {
                PanBack.Visibility = Visibility.Visible;
                PanEmpty.Visibility = Visibility.Collapsed;
            }
            else
            {
                // 根据组件类型设置 PanEmpty 的文本内容
                TxtEmptyTitle.Text = "尚未安装数据包";
                TxtEmptyDescription.Text = "你可以从已经下载好的文件安装数据包。" + "\r\n" + "数据包需要放置在存档的 datapacks 文件夹中才能生效。";

                PanEmpty.Visibility = Visibility.Visible;
                PanBack.Visibility = Visibility.Collapsed;
                return;
            }

            // 修改缓存
            DatapackItems.Clear();
            var itemsToShow = ModLocalComp.CompResourceListLoader.Output.ToList();

            foreach (var DatapackEntity in itemsToShow)
                DatapackItems[DatapackEntity.RawPath] = BuildLocalCompItem(DatapackEntity);

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
            var NewItem = new MyLocalCompItem
            {
                SnapsToDevicePixels = true,
                Entry = Entry,
                ButtonHandler = BuildLocalCompItemBtnHandler,
                Checked = SelectedDatapacks.Contains(Entry.RawPath)
            };
            NewItem.CurrentSwipe = CurrentSwipSelect;
            NewItem.Tags = Entry.Tags;
            Entry.OnCompUpdate += _ => NewItem.Refresh();
            NewItem.Refresh();
            ModAnimation.AniControlEnabled -= 1;
            return NewItem;
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
        sender.Changed += (ss, e) => CheckChanged((MyLocalCompItem)ss, e);

        // 文件项的点击事件：切换选中状态
        sender.Click += (ss, e) =>
        {
            var s = (MyLocalCompItem)ss;
            s.Checked = !s.Checked;
        };

        // 图标按钮
        var BtnOpen = new MyIconButton { LogoScale = 1.05d, Logo = ModBase.Logo.IconButtonOpen, Tag = sender };
        BtnOpen.ToolTip = "打开文件位置";
        ToolTipService.SetPlacement(BtnOpen, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(BtnOpen, 30d);
        ToolTipService.SetHorizontalOffset(BtnOpen, 2d);
        BtnOpen.Click += (sender, e) => Open_Click((MyIconButton)sender, e);

        var BtnCont = new MyIconButton { LogoScale = 1d, Logo = ModBase.Logo.IconButtonInfo, Tag = sender };
        BtnCont.ToolTip = "详情";
        ToolTipService.SetPlacement(BtnCont, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(BtnCont, 30d);
        ToolTipService.SetHorizontalOffset(BtnCont, 2d);
        BtnCont.Click += Info_Click;
        sender.MouseRightButtonUp += Info_Click;

        var BtnDelete = new MyIconButton { LogoScale = 1d, Logo = ModBase.Logo.IconButtonDelete, Tag = sender };
        BtnDelete.ToolTip = "删除";
        ToolTipService.SetPlacement(BtnDelete, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(BtnDelete, 30d);
        ToolTipService.SetHorizontalOffset(BtnDelete, 2d);
        BtnDelete.Click += (sender, e) => Delete_Click((MyIconButton)sender, e);

        if (sender.Entry.State == ModLocalComp.LocalCompFile.LocalFileStatus.Fine)
        {
            var BtnDisable = new MyIconButton { LogoScale = 1d, Logo = ModBase.Logo.IconButtonStop, Tag = sender };
            BtnDisable.ToolTip = "禁用";
            ToolTipService.SetPlacement(BtnDisable, PlacementMode.Center);
            ToolTipService.SetVerticalOffset(BtnDisable, 30d);
            ToolTipService.SetHorizontalOffset(BtnDisable, 2d);
            BtnDisable.Click += (ss, e) => Disable_Click((MyIconButton)ss, e);
            sender.Buttons = new[] { BtnCont, BtnOpen, BtnDisable, BtnDelete };
        }
        else if (sender.Entry.State == ModLocalComp.LocalCompFile.LocalFileStatus.Disabled)
        {
            var BtnEnable = new MyIconButton { LogoScale = 1d, Logo = ModBase.Logo.IconButtonCheck, Tag = sender };
            BtnEnable.ToolTip = "启用";
            ToolTipService.SetPlacement(BtnEnable, PlacementMode.Center);
            ToolTipService.SetVerticalOffset(BtnEnable, 30d);
            ToolTipService.SetHorizontalOffset(BtnEnable, 2d);
            BtnEnable.Click += (ss, e) => Enable_Click((MyIconButton)ss, e);
            sender.Buttons = new[] { BtnCont, BtnOpen, BtnEnable, BtnDelete };
        }
        else
        {
            sender.Buttons = new[] { BtnCont, BtnOpen, BtnDelete };
        }
    }

    /// <summary>
    ///     刷新整个 UI。
    /// </summary>
    public void RefreshUI()
    {
        if (PanList is null)
            return;
        var ShowingDatapacks = (IsSearching ? SearchResult : DatapackItems.Values.Select(i => i.Entry))
            .Where(m => CanPassFilter(m)).ToList();

        // 对显示的数据包进行排序
        if (ShowingDatapacks.Any())
        {
            var sortMethod = GetSortMethod(CurrentSortMethod);
            ShowingDatapacks.Sort((a, b) => sortMethod(a, b));
        }

        // 重新列出列表
        ModAnimation.AniControlEnabled += 1;
        if (ShowingDatapacks.Any())
        {
            PanList.Visibility = Visibility.Visible;
            PanList.Children.Clear();
            foreach (var TargetDatapack in ShowingDatapacks)
            {
                if (!DatapackItems.ContainsKey(TargetDatapack.RawPath))
                    continue;
                var Item = DatapackItems[TargetDatapack.RawPath];

                // 确保元素没有父容器，避免重复添加异常
                if (Item.Parent is not null) ((Panel)Item.Parent).Children.Remove(Item);

                ModStyle.MinecraftFormatter.SetColorfulTextLab(Item.LabTitle.Text, Item.LabTitle,
                    ThemeService.IsDarkMode);
                ModStyle.MinecraftFormatter.SetColorfulTextLab(Item.LabInfo.Text, Item.LabInfo,
                    ThemeService.IsDarkMode);
                Item.Checked = SelectedDatapacks.Contains(TargetDatapack.RawPath); // 更新选中状态
                PanList.Children.Add(Item);
            }
        }
        else
        {
            PanList.Visibility = Visibility.Collapsed;
        }

        ModAnimation.AniControlEnabled -= 1;
        SelectedDatapacks =
            new HashSet<string>(SelectedDatapacks.Where(m =>
                ShowingDatapacks.Any(s => (s.RawPath ?? "") == (m ?? ""))));
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
            var AnyCount = 0;
            var EnabledCount = 0;
            var DisabledCount = 0;
            var UpdateCount = 0;
            var UnavalialeCount = 0;
            var ItemSource = (IsSearching ? SearchResult : DatapackItems.Values.Select(i => i.Entry)).ToArray();
            await Task.Run(() =>
            {
                foreach (var item in ItemSource)
                {
                    AnyCount += 1;
                    if (item.CanUpdate) UpdateCount += 1;
                    if (item.State == ModLocalComp.LocalCompFile.LocalFileStatus.Fine) EnabledCount += 1;
                    if (item.State == ModLocalComp.LocalCompFile.LocalFileStatus.Disabled) DisabledCount += 1;
                    if (item.State == ModLocalComp.LocalCompFile.LocalFileStatus.Unavailable) UnavalialeCount += 1;
                }
            });
            // 显示
            BtnFilterAll.Text = (IsSearching ? "搜索结果" : "全部") + $" ({AnyCount})";
            BtnFilterCanUpdate.Text = $"可更新 ({UpdateCount})";
            BtnFilterCanUpdate.Visibility = Filter == FilterType.CanUpdate || UpdateCount > 0
                ? Visibility.Visible
                : Visibility.Collapsed;
            BtnFilterEnabled.Text = $"启用 ({EnabledCount})";
            BtnFilterEnabled.Visibility = Filter == FilterType.Enabled || (EnabledCount > 0 && EnabledCount < AnyCount)
                ? Visibility.Visible
                : Visibility.Collapsed;
            BtnFilterDisabled.Text = $"禁用 ({DisabledCount})";
            BtnFilterDisabled.Visibility = Filter == FilterType.Disabled || DisabledCount > 0
                ? Visibility.Visible
                : Visibility.Collapsed;
            BtnFilterError.Text = $"错误 ({UnavalialeCount})";
            BtnFilterError.Visibility = Filter == FilterType.Unavailable || UnavalialeCount > 0
                ? Visibility.Visible
                : Visibility.Collapsed;

            // -----------------
            // 底部栏
            // -----------------

            // 计数
            var NewCount = SelectedDatapacks.Count;
            var Selected = NewCount > 0;
            if (Selected)
                LabSelect.Text = $"已选择 {NewCount} 个文件";

            // 按钮可用性
            if (Selected)
            {
                var HasUpdate = false;
                var HasEnabled = false;
                var HasDisabled = false;
                var CanFavoriteAndShare = true;


                // 检查是否所有选中的数据包都有有效的项目信息
                await Task.Run(() =>
                {
                    foreach (var DatapackEntity in ModLocalComp.CompResourceListLoader.Output)
                        if (SelectedDatapacks.Contains(DatapackEntity.RawPath))
                        {
                            if (DatapackEntity.CanUpdate) HasUpdate = true;
                            if (DatapackEntity.State == ModLocalComp.LocalCompFile.LocalFileStatus.Fine)
                                HasEnabled = true;
                            else if (DatapackEntity.State == ModLocalComp.LocalCompFile.LocalFileStatus.Disabled)
                                HasDisabled = true;
                            if (DatapackEntity.Comp is null || string.IsNullOrEmpty(DatapackEntity.Comp.Id))
                                CanFavoriteAndShare = false;
                        }
                });

                BtnSelectDisable.IsEnabled = HasEnabled;
                BtnSelectEnable.IsEnabled = HasDisabled;
                BtnSelectUpdate.IsEnabled = HasUpdate;
                BtnSelectFavorites.IsEnabled = CanFavoriteAndShare;
                BtnSelectShare.IsEnabled = CanFavoriteAndShare;
            }

            // 更新显示状态
            if (ModAnimation.AniControlEnabled == 0)
            {
                PanListBack.Margin = new Thickness(0d, 0d, 0d, Selected ? 95 : 15);
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
                        }, "Datapack Sidebar");
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
                        }, "Datapack Sidebar");
                }
            }
            else
            {
                ModAnimation.AniStop("Datapack Sidebar");
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
        }));
    }

    private int BottomBarShownCount;

    #endregion

    #region 管理

    /// <summary>
    ///     打开 datapacks 文件夹。
    /// </summary>
    private void BtnManageOpen_Click(object sender, EventArgs e)
    {
        try
        {
            var DatapackPath = Path.Combine(PageInstanceSavesLeft.CurrentSave, "datapacks");
            Directory.CreateDirectory(DatapackPath);
            ModBase.OpenExplorer(DatapackPath);
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
        ChangeAllSelected(SelectedDatapacks.Count < PanList.Children.Count);
    }

    /// <summary>
    ///     安装数据包。
    /// </summary>
    private void BtnManageInstall_Click(object sender, MouseButtonEventArgs e)
    {
        var FileList = SystemDialogs.SelectFiles("数据包文件(*.zip)|*.zip", "选择要安装的数据包");
        if (FileList is null || !FileList.Any())
            return;
        InstallDatapackFiles(FileList);
        Refresh();
    }

    /// <summary>
    ///     安装数据包文件。
    /// </summary>
    public static void InstallDatapackFiles(IEnumerable<string> FilePathList)
    {
        if (!FilePathList.Any())
            return;

        var Extension = FilePathList.First().AfterLast(".").ToLower();

        // 检查文件扩展名
        if (Extension != "zip")
        {
            ModMain.Hint($"不支持的文件格式：{Extension}，数据包支持的格式：zip", ModMain.HintType.Critical);
            return;
        }

        // 检查回收站
        if (FilePathList.First().Contains(@":\$RECYCLE.BIN\"))
        {
            ModMain.Hint("请先将文件从回收站还原，再尝试安装！", ModMain.HintType.Critical);
            return;
        }

        ModBase.Log($"[System] 文件为 {Extension} 格式，尝试作为数据包安装");

        // 确认安装
        if (!(ModMain.FrmMain.PageCurrent == FormMain.PageType.InstanceSetup &&
              ModMain.FrmMain.PageCurrentSub == FormMain.PageSubType.VersionSavesDatapack))
            if (ModMain.MyMsgBox($"是否要将这{(FilePathList.Count() == 1 ? "个" : "些")}文件作为数据包安装到当前存档？", "数据包安装确认", "确定",
                    "取消") != 1)
                return;

        // 执行安装
        try
        {
            var DatapackFolder = Path.Combine(PageInstanceSavesLeft.CurrentSave, "datapacks");
            Directory.CreateDirectory(DatapackFolder);

            foreach (var FilePath in FilePathList)
            {
                var NewFileName = ModBase.GetFileNameFromPath(FilePath);
                var DestFile = DatapackFolder + NewFileName;

                if (File.Exists(DestFile))
                    if (ModMain.MyMsgBox($"已存在同名文件：{NewFileName}，是否要覆盖？", "文件覆盖确认", "覆盖", "取消") != 1)
                        continue;

                ModBase.CopyFile(FilePath, DestFile);
            }

            if (FilePathList.Count() == 1)
                ModMain.Hint($"已安装 {ModBase.GetFileNameFromPath(FilePathList.First())}！", ModMain.HintType.Finish);
            else
                ModMain.Hint($"已安装 {FilePathList.Count()} 个数据包！", ModMain.HintType.Finish);

            // 刷新列表
            if (ModMain.FrmMain.PageCurrent == FormMain.PageType.InstanceSetup &&
                ModMain.FrmMain.PageCurrentSub == FormMain.PageSubType.VersionSavesDatapack)
                if (ModMain.FrmInstanceSavesDatapack is not null)
                    ModMain.FrmInstanceSavesDatapack.ReloadDatapackFileList(true);
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
        ModMain.FrmMain.PageChange(FormMain.PageType.Download, FormMain.PageSubType.DownloadDataPack);
        PageComp.TargetVersion = PageInstanceLeft.Instance; // 将当前实例设置为筛选器
    }

    /// <summary>
    ///     导出信息。
    /// </summary>
    private void BtnManageInfoExport_Click(object sender, MouseButtonEventArgs e)
    {
        var Choice =
            ModMain.MyMsgBox("TXT 格式：仅导出当前的数据包文件名称信息" + "\r\n" + "CSV 格式：导出详细的数据包信息，包括文件名、工程 ID、版本信息等详细信息",
                "选择导出模式", "TXT 格式", "CSV 格式", "取消");

        void ExportText(string Content, string FileName)
        {
            try
            {
                var savePath =
                    SystemDialogs.SelectSaveFile("选择保存位置", FileName, "文本文件(*.txt)|*.txt|CSV 文件(*.csv)|*.csv");
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
        switch (Choice)
        {
            case 1: // TXT
            {
                var ExportContent = new List<string>();
                foreach (var DatapackEntity in ModLocalComp.CompResourceListLoader.Output)
                    ExportContent.Add(DatapackEntity.FileName);
                ExportText(ExportContent.Join("\r\n"),
                    ModBase.GetFolderNameFromPath(PageInstanceSavesLeft.CurrentSave) + "的数据包信息.txt");
                break;
            }

            case 2: // CSV
            {
                var ExportContent = new List<string>();
                ExportContent.Add("文件名,数据包名称,数据包版本,此版本更新时间,工程 ID,文件大小（字节）,文件路径");
                foreach (var DatapackEntity in ModLocalComp.CompResourceListLoader.Output)
                    ExportContent.Add(
                        $"{DatapackEntity.FileName},{DatapackEntity.Comp?.TranslatedName},{DatapackEntity.Version},{DatapackEntity.CompFile?.ReleaseDate},{DatapackEntity.Comp?.Id},{GetDatapackFileInfo(DatapackEntity.Path).Length},{DatapackEntity.Path}");
                ExportText(ExportContent.Join("\r\n"),
                    ModBase.GetFolderNameFromPath(PageInstanceSavesLeft.CurrentSave) + "的数据包信息.csv");
                break;
            }
        }
    }

    #endregion

    #region 选择

    /// <summary>
    ///     选择的数据包的路径。
    /// </summary>
    public HashSet<string> SelectedDatapacks = new();

    // 单项切换选择状态
    public void CheckChanged(MyLocalCompItem sender, ModBase.RouteEventArgs e)
    {
        if (ModAnimation.AniControlEnabled != 0)
            return;
        // 更新选择了的内容
        var SelectedKey = sender.Entry.RawPath;
        if (sender.Checked)
            SelectedDatapacks.Add(SelectedKey);
        else
            SelectedDatapacks.Remove(SelectedKey);
        RefreshBars();
    }

    // 切换所有项的选择状态
    private void ChangeAllSelected(bool Value)
    {
        ModAnimation.AniControlEnabled += 1;
        SelectedDatapacks.Clear();
        foreach (var Item in DatapackItems.Values)
        {
            var ShouldSelected = Value && PanList.Children.Contains(Item);
            Item.Checked = ShouldSelected;
            if (ShouldSelected)
                SelectedDatapacks.Add(Item.Entry.RawPath);
        }

        ModAnimation.AniControlEnabled -= 1;
        RefreshBars();
    }

    private void UnselectedAllWithAnimation()
    {
        var CacheAniControlEnabled = ModAnimation.AniControlEnabled;
        ModAnimation.AniControlEnabled = 0;
        ChangeAllSelected(false);
        ModAnimation.AniControlEnabled += CacheAniControlEnabled;
    }

    private void FrmMain_KeyDown(object sender, KeyEventArgs e)
    {
        if (!ReferenceEquals(ModMain.FrmMain.PageRight, this))
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

    private SortMethod CurrentSortMethod = SortMethod.CompName;

    private void SetSortMethod(SortMethod Target)
    {
        CurrentSortMethod = Target;
        BtnSort.Text = $"排序：{GetSortName(Target)}";
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
                return "文件名";
            }
            case SortMethod.CompName:
            {
                return "资源名称";
            }
            case SortMethod.CreateTime:
            {
                return "加入时间";
            }
            case SortMethod.DatapackFileSize:
            {
                return "文件大小";
            }

            default:
            {
                return "资源名称";
            }
        }

        return "";
    }

    private void BtnSortClick(object sender, ModBase.RouteEventArgs e)
    {
        var Body = new ContextMenu();
        foreach (SortMethod i in Enum.GetValues(typeof(SortMethod)))
        {
            var Item = new MyMenuItem();
            Item.Header = GetSortName(i);
            Item.Click += (_, _) => SetSortMethod(i);
            Body.Items.Add(Item);
        }

        Body.PlacementTarget = (UIElement)sender;
        Body.Placement = PlacementMode.Bottom;
        Body.IsOpen = true;
    }

    private readonly object SortLock = new();

    private void DoSort()
    {
        lock (SortLock)
        {
            try
            {
                if (PanList is null || PanList.Children.Count < 2)
                    return;

                // 将子元素转换为可排序的列表
                var items = PanList.Children.OfType<MyLocalCompItem>().ToList();
                var Method = GetSortMethod(CurrentSortMethod);

                // 分离有效和无效项（保持原始相对顺序）
                var invalid = items.Where(i => i.Entry is null).ToList();
                var valid = items.Except(invalid).ToList();
                // 仅对有效项进行排序
                valid.Sort((x, y) => Method(x.Entry, y.Entry));
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
                    var aDate = GetDatapackFileInfo(a.Path).CreationTime;
                    var bDate = GetDatapackFileInfo(b.Path).CreationTime;
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
                    var aSize = GetDatapackFileInfo(a.Path).Length;
                    var bSize = GetDatapackFileInfo(b.Path).Length;
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
            ModLocalComp.CompResourceListLoader.Output.Where(m => SelectedDatapacks.Contains(m.RawPath)).ToList(),
            true);
        ChangeAllSelected(false);
    }

    // 禁用
    private void BtnSelectDisable_Click(object sender, ModBase.RouteEventArgs e)
    {
        ToggleDatapacks(
            ModLocalComp.CompResourceListLoader.Output.Where(m => SelectedDatapacks.Contains(m.RawPath)).ToList(),
            false);
        ChangeAllSelected(false);
    }

    /// <summary>
    ///     启用/禁用数据包（通过重命名文件夹为 .disabled）
    /// </summary>
    private void ToggleDatapacks(IEnumerable<ModLocalComp.LocalCompFile> DatapackList, bool IsEnable)
    {
        var IsSuccessful = true;
        foreach (var DatapackE in DatapackList)
        {
            var DatapackEntity = DatapackE;
            string NewPath = null;

            if (DatapackEntity.State == ModLocalComp.LocalCompFile.LocalFileStatus.Fine && !IsEnable)
                // 禁用 - 添加 .disabled 后缀
                NewPath = DatapackEntity.Path + ".disabled";
            else if (DatapackEntity.State == ModLocalComp.LocalCompFile.LocalFileStatus.Disabled && IsEnable)
                // 启用 - 移除 .disabled 后缀
                NewPath = DatapackEntity.RawPath;
            else
                continue;

            // 重命名
            try
            {
                if (File.Exists(NewPath))
                {
                    ModMain.MyMsgBox($"已存在同名文件：{ModBase.GetFileNameFromPath(NewPath)}，请先处理该文件再重试。");
                    continue;
                }

                FileSystem.Rename(DatapackEntity.Path, NewPath);
            }
            catch (FileNotFoundException ex)
            {
                ModBase.Log(ex, $"未找到需要重命名的数据包（{DatapackEntity.Path ?? "null"}）", ModBase.LogLevel.Feedback);
                ReloadDatapackFileList(true);
                return;
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, $"重命名数据包失败（{DatapackEntity.Path ?? "null"}）");
                IsSuccessful = false;
            }

            // 更改 Loader 中的列表
            var NewDatapackEntity = new ModLocalComp.LocalCompFile(NewPath);
            NewDatapackEntity.FromJson(DatapackEntity.ToJson());
            if (ModLocalComp.CompResourceListLoader.Output.Contains(DatapackEntity))
            {
                var IndexOfLoader = ModLocalComp.CompResourceListLoader.Output.IndexOf(DatapackEntity);
                ModLocalComp.CompResourceListLoader.Output.RemoveAt(IndexOfLoader);
                ModLocalComp.CompResourceListLoader.Output.Insert(IndexOfLoader, NewDatapackEntity);
            }

            if (SearchResult is not null && SearchResult.Contains(DatapackEntity))
            {
                var IndexOfResult = SearchResult.IndexOf(DatapackEntity);
                SearchResult.Remove(DatapackEntity);
                SearchResult.Insert(IndexOfResult, NewDatapackEntity);
            }

            // 更改 UI 中的列表
            try
            {
                var NewItem = BuildLocalCompItem(NewDatapackEntity);
                DatapackItems[DatapackEntity.RawPath] = NewItem;
                var IndexOfUi = PanList.Children.IndexOf(PanList.Children.OfType<MyLocalCompItem>()
                    .FirstOrDefault(i => ReferenceEquals(i.Entry, DatapackEntity)));
                if (IndexOfUi == -1)
                    continue;
                PanList.Children.RemoveAt(IndexOfUi);
                PanList.Children.Insert(IndexOfUi, NewItem);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, $"更新 UI 列表项失败：{DatapackEntity.FileName}", ModBase.LogLevel.Hint);
            }
        }

        Dispatcher.Invoke(() => PanList.UpdateLayout(), DispatcherPriority.Background);

        if (IsSuccessful)
        {
            RefreshBars();
        }
        else
        {
            ModMain.Hint("由于文件被占用，数据包的状态切换失败，请尝试关闭正在运行的游戏后再试！", ModMain.HintType.Critical);
            ReloadDatapackFileList(true);
        }

        LoaderRun(ModLoader.LoaderFolderRunType.UpdateOnly);
    }

    // 更新
    private void BtnSelectUpdate_Click(object sender, ModBase.RouteEventArgs e)
    {
        var UpdateList = ModLocalComp.CompResourceListLoader.Output
            .Where(m => SelectedDatapacks.Contains(m.RawPath) && m.CanUpdate).ToList();
        if (!UpdateList.Any())
            return;
        UpdateResource(UpdateList);
        ChangeAllSelected(false);
    }

    /// <summary>
    ///     记录正在进行数据包更新的 datapacks 文件夹路径。
    /// </summary>
    public static List<string> UpdatingVersions = new();

    public void UpdateResource(IEnumerable<ModLocalComp.LocalCompFile> DatapackList)
    {
        // 更新前警告
        if (!States.Hint.FunctionDatapackUpdate || DatapackList.Count() >= 15)
        {
            if (ModMain.MyMsgBox(
                    $"新版本数据包可能不兼容旧存档或者其他数据包，这可能导致游戏崩溃或存档损坏！{"\r\n"}{"\r\n"}在更新前，请先备份存档。{"\r\n"}如果更新后出现问题，你也可以在回收站找回更新前的数据包。",
                    "数据包更新警告", "我已了解风险，继续更新", "取消", IsWarn: true) == 1)
                States.Hint.FunctionDatapackUpdate = true;
            else
                return;
        }

        try
        {
            // 构造下载信息
            DatapackList = DatapackList.ToList(); // 防止刷新影响迭代器
            var FileList = new List<DownloadFile>();
            var FileCopyList = new Dictionary<string, string>();
            foreach (var Entry in DatapackList)
            {
                var File = Entry.UpdateFile;
                if (!File.Available)
                    continue;
                // 添加到下载列表
                var TempAddress = ModBase.PathTemp + @"DownloadedComp\" + File.FileName;
                var RealAddress = Path.Combine(PageInstanceSavesLeft.CurrentSave, "datapacks", File.FileName);
                FileList.Add(File.ToNetFile(TempAddress));
                FileCopyList[TempAddress] = RealAddress;
            }

            // 构造加载器
            var InstallLoaders = new List<ModLoader.LoaderBase>();
            var FinishedFileNames = new List<string>();
            InstallLoaders.Add(new LoaderDownload("下载新版数据包文件", FileList)
                { ProgressWeight = DatapackList.Count() * 1.5d });

            InstallLoaders.Add(new ModLoader.LoaderTask<int, int>("替换旧版数据包文件", _ =>
            {
                try
                {
                    foreach (var Entry in DatapackList)
                        if (File.Exists(Entry.Path))
                            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(Entry.Path, UIOption.AllDialogs,
                                RecycleOption.SendToRecycleBin);
                        else
                            ModBase.Log($"[DatapackUpdate] 未找到更新前的数据包文件，跳过对它的删除：{Entry.Path}", ModBase.LogLevel.Debug);

                    foreach (var Entry in FileCopyList)
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
                            FinishedFileNames.Add(ModBase.GetFileNameFromPath(Entry.Value));
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
            var Loader = new ModLoader.LoaderCombo<IEnumerable<ModLocalComp.LocalCompFile>>(
                $"数据包更新：{ModBase.GetFolderNameFromPath(PageInstanceSavesLeft.CurrentSave)}", InstallLoaders);
            var PathDatapacks = Path.Combine(PageInstanceSavesLeft.CurrentSave, "datapacks");

            Loader.OnStateChanged = _ =>
            {
                switch (Loader.State)
                {
                    case ModBase.LoadState.Finished:
                    {
                        switch (FinishedFileNames.Count)
                        {
                            case 0:
                            {
                                ModBase.Log("[DatapackUpdate] 没有数据包被成功更新");
                                break;
                            }
                            case 1:
                            {
                                ModMain.Hint($"已成功更新 {FinishedFileNames.Single()}！", ModMain.HintType.Finish);
                                break;
                            }

                            default:
                            {
                                ModMain.Hint($"已成功更新 {FinishedFileNames.Count} 个数据包！", ModMain.HintType.Finish);
                                break;
                            }
                        }

                        break;
                    }
                    case ModBase.LoadState.Failed:
                    {
                        ModMain.Hint("数据包更新失败：" + Loader.Error.Message, ModMain.HintType.Critical);
                        break;
                    }
                    case ModBase.LoadState.Aborted:
                    {
                        ModMain.Hint("数据包更新已中止！");
                        break;
                    }

                    default:
                    {
                        return;
                    }
                }

                ModBase.Log($"[DatapackUpdate] 已从正在进行数据包更新的文件夹列表移除：{PathDatapacks}");
                UpdatingVersions.Remove(PathDatapacks);

                // 清理缓存
                ModBase.RunInNewThread(() =>
                {
                    try
                    {
                        foreach (var TempFile in FileCopyList.Keys)
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
            ModBase.Log($"[DatapackUpdate] 开始更新 {DatapackList.Count()} 个数据包：{PathDatapacks}");
            UpdatingVersions.Add(PathDatapacks);
            Loader.Start();
            ModLoader.LoaderTaskbarAdd(Loader);
            ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
            ModMain.FrmMain.BtnExtraDownload.Ribble();
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
        DeleteDatapacks(ModLocalComp.CompResourceListLoader.Output.Where(m => SelectedDatapacks.Contains(m.RawPath)));
        ChangeAllSelected(false);
    }

    private void DeleteDatapacks(IEnumerable<ModLocalComp.LocalCompFile> DatapackList)
    {
        try
        {
            var IsSuccessful = true;
            var IsShiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

            // 确认需要删除的文件
            DatapackList = DatapackList.SelectMany(Target =>
            {
                if (Target.State == ModLocalComp.LocalCompFile.LocalFileStatus.Fine)
                    return new[] { Target.Path, Target.Path + ".disabled" };

                return new[] { Target.Path, Target.RawPath };
            }).Distinct().Where(m => File.Exists(m)).Select(m => new ModLocalComp.LocalCompFile(m)).ToList();

            // 实际删除文件
            foreach (var DatapackEntity in DatapackList)
            {
                try
                {
                    if (IsShiftPressed)
                        File.Delete(DatapackEntity.Path);
                    else
                        Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(DatapackEntity.Path,
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
                    ModBase.Log(ex, $"删除数据包失败（{DatapackEntity.Path}）", ModBase.LogLevel.Msgbox);
                    IsSuccessful = false;
                }

                // 取消选中
                SelectedDatapacks.Remove(DatapackEntity.RawPath);
                // 更改 Loader 和 UI 中的列表
                ModLocalComp.CompResourceListLoader.Output.Remove(DatapackEntity);
                SearchResult?.Remove(DatapackEntity);
                DatapackItems.Remove(DatapackEntity.RawPath);
                var IndexOfUi = PanList.Children.IndexOf(PanList.Children.OfType<MyLocalCompItem>()
                    .FirstOrDefault(i => i.Entry.Equals(DatapackEntity)));
                if (IndexOfUi >= 0)
                    PanList.Children.RemoveAt(IndexOfUi);
            }

            RefreshBars();
            if (!IsSuccessful)
            {
                ModMain.Hint("由于文件被占用，删除失败，请尝试关闭正在运行的游戏后再试！", ModMain.HintType.Critical);
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

            if (!IsSuccessful)
                return;
            if (IsShiftPressed)
            {
                if (DatapackList.Count() == 1)
                    ModMain.Hint($"已彻底删除 {DatapackList.Single().FileName}！", ModMain.HintType.Finish);
                else
                    ModMain.Hint($"已彻底删除 {DatapackList.Count()} 个项目！", ModMain.HintType.Finish);
            }
            else if (DatapackList.Count() == 1)
            {
                ModMain.Hint($"已将 {DatapackList.Single().FileName} 删除到回收站！", ModMain.HintType.Finish);
            }
            else
            {
                ModMain.Hint($"已将 {DatapackList.Count()} 个项目删除到回收站！", ModMain.HintType.Finish);
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
        var Selected = ModLocalComp.CompResourceListLoader.Output
            .Where(m => SelectedDatapacks.Contains(m.RawPath) && m.Comp is not null).Select(i => i.Comp).ToList();
        ModComp.CompFavorites.ShowMenu(Selected, (UIElement)sender);
    }

    // 分享
    private void BtnSelectShare_Click(object sender, ModBase.RouteEventArgs e)
    {
        var ShareList = ModLocalComp.CompResourceListLoader.Output
            .Where(m => SelectedDatapacks.Contains(m.RawPath) && m.Comp is not null).Select(i => i.Comp.Id).ToHashSet();
        ModBase.ClipboardSet(ModComp.CompFavorites.GetShareCode(ShareList));
        ChangeAllSelected(false);
    }

    #endregion

    #region 单个资源项

    // 详情
    public void Info_Click(object sender, EventArgs e)
    {
        try
        {
            var DatapackEntry = ((MyLocalCompItem)(sender is MyIconButton iconBtn ? iconBtn.Tag : sender)).Entry;

            // 加载失败信息
            if (DatapackEntry.State == ModLocalComp.LocalCompFile.LocalFileStatus.Unavailable)
            {
                ModMain.MyMsgBox(
                    "无法读取此数据包的信息。" + "\r\n" + "\r\n" + "详细的错误信息：" +
                    DatapackEntry.FileUnavailableReason.Message, "数据包读取失败");
                return;
            }

            if (DatapackEntry.Comp is not null)
            {
                // 跳转到数据包下载页面
                ModMain.FrmMain.PageChange(new FormMain.PageStackData
                {
                    Page = FormMain.PageType.CompDetail,
                    Additional = (DatapackEntry.Comp, new List<string>(), PageInstanceLeft.Instance.Info.VanillaName,
                        ModComp.CompLoaderType.Minecraft, ModComp.CompType.DataPack, null, null, null)
                });
            }
            else
            {
                // 获取信息
                var ContentLines = new List<string>();

                if (DatapackEntry.Description is not null)
                    ContentLines.Add(DatapackEntry.Description + "\r\n");
                if (DatapackEntry.Authors is not null)
                    ContentLines.Add("作者：" + DatapackEntry.Authors);
                ContentLines.Add("文件：" + DatapackEntry.FileName + "（" +
                                 ModBase.GetString(GetDatapackFileInfo(DatapackEntry.Path).Length) + "）");
                if (DatapackEntry.Version is not null)
                    ContentLines.Add("版本：" + DatapackEntry.Version);

                var DebugInfo = new List<string>();
                if (DatapackEntry.ModId is not null) DebugInfo.Add("数据包 ID：" + DatapackEntry.ModId);
                if (DebugInfo.Any())
                {
                    ContentLines.Add("");
                    ContentLines.AddRange(DebugInfo);
                }

                // 显示详情信息
                if (DatapackEntry.Url is null)
                    ModMain.MyMsgBox(ContentLines.Join("\r\n"), DatapackEntry.Name, "返回");
                else if (ModMain.MyMsgBox(ContentLines.Join("\r\n"), DatapackEntry.Name, "打开官网", "返回") == 1)
                    ModBase.OpenWebsite(DatapackEntry.Url);
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
            var ListItem = (MyLocalCompItem)sender.Tag;
            ModBase.OpenExplorer(ListItem.Entry.Path);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "打开数据包文件位置失败", ModBase.LogLevel.Feedback);
        }
    }

    // 删除
    public void Delete_Click(MyIconButton sender, EventArgs e)
    {
        var ListItem = (MyLocalCompItem)sender.Tag;
        DeleteDatapacks(new[] { ListItem.Entry });
    }

    // 启用
    public void Enable_Click(MyIconButton sender, EventArgs e)
    {
        var ListItem = (MyLocalCompItem)sender.Tag;
        ToggleDatapacks(new[] { ListItem.Entry }, true);
    }

    // 禁用
    public void Disable_Click(MyIconButton sender, EventArgs e)
    {
        var ListItem = (MyLocalCompItem)sender.Tag;
        ToggleDatapacks(new[] { ListItem.Entry }, false);
    }

    #endregion

    #region 搜索

    public bool IsSearching => !string.IsNullOrWhiteSpace(SearchBox.Text);
    private List<ModLocalComp.LocalCompFile> SearchResult;

    public void SearchRun(object sender, EventArgs e)
    {
        try
        {
            if (IsSearching)
            {
                // 构造请求
                var QueryList = new List<ModBase.SearchEntry<ModLocalComp.LocalCompFile>>();
                foreach (var Entry in ModLocalComp.CompResourceListLoader.Output)
                {
                    var SearchSource = new List<ModBase.SearchSource>();
                    SearchSource.Add(new ModBase.SearchSource(Entry.Name, 1d));
                    SearchSource.Add(new ModBase.SearchSource(Entry.FileName, 1d));
                    if (Entry.Version is not null)
                        SearchSource.Add(new ModBase.SearchSource(Entry.Version, 0.2d));
                    if (Entry.Description is not null && !string.IsNullOrEmpty(Entry.Description))
                        SearchSource.Add(new ModBase.SearchSource(Entry.Description, 0.4d));
                    if (Entry.Comp is not null)
                    {
                        if ((Entry.Comp.RawName ?? "") != (Entry.Name ?? ""))
                            SearchSource.Add(new ModBase.SearchSource(Entry.Comp.RawName, 1d));
                        if ((Entry.Comp.TranslatedName ?? "") != (Entry.Comp.RawName ?? ""))
                            SearchSource.Add(new ModBase.SearchSource(Entry.Comp.TranslatedName, 1d));
                        if ((Entry.Comp.Description ?? "") != (Entry.Description ?? ""))
                            SearchSource.Add(new ModBase.SearchSource(Entry.Comp.Description, 0.4d));
                        SearchSource.Add(new ModBase.SearchSource(string.Join("", Entry.Comp.Tags), 0.2d));
                    }

                    QueryList.Add(new ModBase.SearchEntry<ModLocalComp.LocalCompFile>
                        { Item = Entry, SearchSource = SearchSource });
                }

                // 进行搜索
                SearchResult = ModBase.Search(QueryList, SearchBox.Text, 6, 0.35d).Select(r => r.Item).ToList();
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
