using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.VisualBasic.FileIO;
using PCL.Core.App;
using PCL.Core.Logging;
using PCL.Core.UI;
using PCL.Core.UI.Theme;
using PCL.Network;
using PCL.Network.Loaders;
using FileSystem = Microsoft.VisualBasic.FileSystem;
using SearchOption = System.IO.SearchOption;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageInstanceCompResource : IRefreshable
{
    #region 模组信息缓存

    // 模组信息缓存 - 解决排序时重复创建FileInfo导致的性能问题
    private readonly Dictionary<string, (DateTime CreationTime, long Length)> modFileInfoCache = new();

    public PageInstanceCompResource()
    {
        InitializeComponent();
        Unloaded += Page_Unloaded;
        Loaded += (_, _) => PageOther_Loaded();
        Initialized += (_, _) => LoaderInit();
        PageExit += UnselectedAllWithAnimation;
        Load.Click += Load_Click;
        BtnManageBack.Click += BtnManageBack_Click;
        BtnHintBack.Click += BtnHintBack_Click;
        BtnManageOpen.Click += BtnManageOpen_Click;
        BtnHintOpen.Click += BtnManageOpen_Click;
        BtnManageSelectAll.Click += BtnManageSelectAll_Click;
        BtnManageInstall.Click += BtnManageInstall_Click;
        BtnHintInstall.Click += BtnManageInstall_Click;
        BtnManageInfoExport.Click += BtnManageInfoExport_Click;
        BtnManageDownload.Click += BtnManageDownload_Click;
        BtnHintDownload.Click += BtnManageDownload_Click;
        BtnSchematicDownloadMod.Click += BtnSchematicDownloadMod_Click;
        BtnSchematicVersionSelect.Click += BtnSchematicVersionSelect_Click;
        Load.StateChanged += (_, _, _) => UnselectedAllWithAnimation();
        SearchBox.PreviewKeyDown += SearchBox_PreviewKeyDown;
        BtnFilterAll.Check += ChangeFilter;
        BtnFilterCanUpdate.Check += ChangeFilter;
        BtnFilterDisabled.Check += ChangeFilter;
        BtnFilterEnabled.Check += ChangeFilter;
        BtnFilterError.Check += ChangeFilter;
        BtnFilterDuplicate.Check += ChangeFilter;
        BtnSort.Click += BtnSortClick;
        BtnSelectEnable.Click += BtnSelectED_Click;
        BtnSelectDisable.Click += BtnSelectED_Click;
        BtnSelectUpdate.Click += BtnSelectUpdate_Click;
        BtnSelectDelete.Click += BtnSelectDelete_Click;
        BtnSelectCancel.Click += BtnSelectCancel_Click;
        BtnSelectFavorites.Click += BtnSelectFavorites_Click;
        BtnSelectShare.Click += BtnSelectShare_Click;
        SearchBox.TextChanged += SearchRun;
    }

    // 获取模组信息（带缓存）
    private (DateTime CreationTime, long Length) GetModFileInfo(string path)
    {
        (DateTime CreationTime, long Length) cacheItem;
        if (modFileInfoCache.TryGetValue(path, out cacheItem)) return cacheItem;

        try
        {
            var fileInfo = new FileInfo(path);
            var newItem = (fileInfo.CreationTime, fileInfo.Length);
            if (!modFileInfoCache.ContainsKey(path)) modFileInfoCache.Add(path, newItem);
            return newItem;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "获取模组信息失败: " + path);
            return (DateTime.MinValue, 0L);
        }
    }

    // 页面关闭时清理缓存
    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        modFileInfoCache.Clear();
    }

    #endregion

    #region 初始化

    private readonly ModComp.CompType currentCompType = ModComp.CompType.Mod;

    private readonly MyLocalCompItem.SwipeSelect currentSwipSelect;

    public PageInstanceCompResource(ModComp.CompType LoadCompType)
    {
        currentCompType = LoadCompType;
        CurrentFolderPath = ""; // 确保文件夹路径被重置为根目录
        currentSwipSelect = new MyLocalCompItem.SwipeSelect { TargetFrm = this };

        // 此调用是设计器所必需的。
        InitializeComponent();

        // 在 InitializeComponent() 调用之后添加任何初始化。

        if (new[] { ModComp.CompType.Shader, ModComp.CompType.ResourcePack, ModComp.CompType.Schematic }.Contains(
                currentCompType))
        {
            BtnSelectEnable.Visibility = Visibility.Collapsed;
            BtnSelectDisable.Visibility = Visibility.Collapsed;
        }

        // 投影文件管理页隐藏下载按钮
        if (currentCompType == ModComp.CompType.Schematic)
        {
            BtnManageDownload.Visibility = Visibility.Collapsed;
            BtnHintDownload.Visibility = Visibility.Collapsed;
        }

        Unloaded += Page_Unloaded;
        Loaded += (_, _) => PageOther_Loaded();
        LoaderInit();
        PageExit += UnselectedAllWithAnimation;
        // Handles
        Load.Click += Load_Click;
        BtnManageBack.Click += BtnManageBack_Click;
        BtnHintBack.Click += BtnHintBack_Click;
        BtnManageOpen.Click += BtnManageOpen_Click;
        BtnHintOpen.Click += BtnManageOpen_Click;
        BtnManageSelectAll.Click += BtnManageSelectAll_Click;
        BtnManageInstall.Click += BtnManageInstall_Click;
        BtnHintInstall.Click += BtnManageInstall_Click;
        BtnManageDownload.Click += BtnManageDownload_Click;
        BtnHintDownload.Click += BtnManageDownload_Click;
        BtnManageInfoExport.Click += BtnManageInfoExport_Click;
        BtnSchematicDownloadMod.Click += BtnSchematicDownloadMod_Click;
        BtnSchematicVersionSelect.Click += BtnSchematicVersionSelect_Click;
        Load.StateChanged += (_, _, _) => UnselectedAllWithAnimation();
        SearchBox.PreviewKeyDown += SearchBox_PreviewKeyDown;
        BtnFilterAll.Check += ChangeFilter;
        BtnFilterCanUpdate.Check += ChangeFilter;
        BtnFilterDisabled.Check += ChangeFilter;
        BtnFilterEnabled.Check += ChangeFilter;
        BtnFilterError.Check += ChangeFilter;
        BtnFilterDuplicate.Check += ChangeFilter;
        BtnSort.Click += BtnSortClick;
        BtnSelectEnable.Click += BtnSelectED_Click;
        BtnSelectDisable.Click += BtnSelectED_Click;
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
        res.frm = this;
        var requireLoaders = new List<ModComp.CompLoaderType>();
        switch (currentCompType)
        {
            case ModComp.CompType.Mod:
            {
                requireLoaders = ModLocalComp.GetCurrentVersionModLoader();
                break;
            }
            case ModComp.CompType.ResourcePack:
            {
                requireLoaders = new[] { ModComp.CompLoaderType.Minecraft }.ToList();
                break;
            }
            case ModComp.CompType.Shader:
            {
                requireLoaders = new[]
                {
                    ModComp.CompLoaderType.OptiFine, ModComp.CompLoaderType.Iris, ModComp.CompLoaderType.Vanilla,
                    ModComp.CompLoaderType.Canvas
                }.ToList();
                break;
            }
            case ModComp.CompType.Schematic:
            {
                requireLoaders = new[] { ModComp.CompLoaderType.Minecraft }.ToList();
                break;
            }
        }

        res.loaders = requireLoaders;
        res.compPath = PageInstanceLeft.instance.PathIndie +
                       (PageInstanceLeft.instance.Info.hasLabyMod
                           ? Path.Combine("labymod-neo", "fabric", PageInstanceLeft.instance.Info.vanillaName)
                           : "") + ModLocalComp.GetPathNameByCompType(currentCompType) + @"\";
        res.compType = currentCompType;
        return res;
    }

    private bool isLoad;

    public void PageOther_Loaded()
    {
        CurrentFolderPath = string.Empty;

        if (ModMain.frmMain.pageLast.page != FormMain.PageType.CompDetail)
            PanBack.ScrollToHome();
        ModAnimation.AniControlEnabled += 1;
        selectedMods.Clear();
        ReloadCompFileList();
        ChangeAllSelected(false);
        ModAnimation.AniControlEnabled -= 1;

        // 非重复加载部分
        if (isLoad)
            return;
        isLoad = true;

        // 检查是否为原理图管理界面且首次打开
        if (currentCompType == ModComp.CompType.Schematic && !States.Hint.SchematicFirstTime)
            // 显示首次打开提示
            ModBase.RunInUi(() =>
            {
                ModMain.MyMsgBox(Lang.Text("Instance.Saves.Folder.DoubleClickHint.Message"), Lang.Text("Instance.Saves.Folder.DoubleClickHint.Title"), Lang.Text("Common.Action.GotIt"));
                States.Hint.SchematicFirstTime = true;
            }, true);

        ModMain.frmMain.KeyDown += FrmMain_KeyDown;
        // 调整按钮边距（这玩意儿没法从 XAML 改）
        foreach (MyRadioButton Btn in PanFilter.Children)
            Btn.LabText.Margin = new Thickness(-2, 0d, 8d, 0d);
    }

    /// <summary>
    ///     刷新 Mod 列表。
    /// </summary>
    public void ReloadCompFileList(bool ForceReload = false)
    {
        if (LoaderRun(ForceReload
                ? ModLoader.LoaderFolderRunType.ForceRun
                : ModLoader.LoaderFolderRunType.RunOnUpdated))
        {
            ModBase.Log($"[System] 已刷新 {currentCompType} 列表");
            modFileInfoCache.Clear();

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
        Refresh(currentCompType);
    }

    void IRefreshable.Refresh()
    {
        RefreshSelf();
    }

    public static void Refresh(ModComp.CompType WhichPage)
    {
        // 强制刷新
        try
        {
            ModComp.compProjectCache.Clear();
            ModComp.compFilesCache.Clear();
            File.Delete(ModBase.pathTemp + @"Cache\LocalComp.json");
            ModBase.Log("[CompResource] 由于点击刷新按钮，清理本地工程信息缓存");
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "强制刷新时清理本地工程信息缓存失败");
        }

        switch (WhichPage)
        {
            case ModComp.CompType.Mod:
            {
                if (ModMain.frmInstanceMod is not null)
                    ModMain.frmInstanceMod.ReloadCompFileList(true); // 无需 Else，还没加载刷个鬼的新
                ModMain.frmInstanceLeft.ItemMod.Checked = true;
                break;
            }
            case ModComp.CompType.ResourcePack:
            {
                if (ModMain.frmInstanceResourcePack is not null)
                    ModMain.frmInstanceResourcePack.ReloadCompFileList(true);
                ModMain.frmInstanceLeft.ItemResourcePack.Checked = true;
                break;
            }
            case ModComp.CompType.Shader:
            {
                if (ModMain.frmInstanceShader is not null)
                    ModMain.frmInstanceShader.ReloadCompFileList(true);
                ModMain.frmInstanceLeft.ItemShader.Checked = true;
                break;
            }
            case ModComp.CompType.Schematic:
            {
                if (ModMain.frmInstanceSchematic is not null)
                    ModMain.frmInstanceSchematic.ReloadCompFileList(true);
                ModMain.frmInstanceLeft.ItemSchematic.Checked = true;
                break;
            }
        }

        ModMain.Hint(Lang.Text("Instance.Left.Refreshing"), Log: false);
    }

    private void LoaderInit()
    {
        PageLoaderInit(Load, PanLoad, PanAllBack, null, ModLocalComp.compResourceListLoader,
            _ => LoadUIFromLoaderOutput(), () => currentCompType, false);
    }

    private void Load_Click(object sender, MouseButtonEventArgs e)
    {
        if (ModLocalComp.compResourceListLoader.State == ModBase.LoadState.Failed)
            LoaderRun(ModLoader.LoaderFolderRunType.ForceRun);
    }

    public bool LoaderRun(ModLoader.LoaderFolderRunType Type)
    {
        string loadPath;
        if (string.IsNullOrEmpty(CurrentFolderPath))
            // 加载根目录
            loadPath = PageInstanceLeft.instance.PathIndie +
                       (PageInstanceLeft.instance.Info.hasLabyMod
                           ? Path.Combine("labymod-neo", "fabric", PageInstanceLeft.instance.Info.vanillaName)
                           : "") + ModLocalComp.GetPathNameByCompType(currentCompType) + @"\";
        else
            // 加载当前文件夹
            loadPath = CurrentFolderPath;
        return ModLoader.LoaderFolderRun(ModLocalComp.compResourceListLoader, loadPath, Type,
            LoaderInput: GetRequireLoaderData());
    }

    #endregion

    #region 文件夹导航

    /// <summary>
    ///     当前显示的文件夹路径。空字符串表示根目录。
    /// </summary>
    public string CurrentFolderPath { get; set; } = "";

    /// <summary>
    ///     进入指定的文件夹。
    /// </summary>
    private void EnterFolder(string folderPath)
    {
        try
        {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
            {
                ModMain.Hint(Lang.Text("Instance.Saves.Folder.NotFound"), ModMain.HintType.Critical);
                return;
            }

            CurrentFolderPath = folderPath;
            ModBase.Log($"[原理图] 进入文件夹：{folderPath}");

            ModLoader.LoaderFolderRun(ModLocalComp.compResourceListLoader, folderPath,
                ModLoader.LoaderFolderRunType.ForceRun, LoaderInput: GetRequireLoaderData());
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "进入文件夹失败", ModBase.LogLevel.Msgbox);
        }
    }

    /// <summary>
    ///     进入指定文件夹。
    /// </summary>
    private void EnterFolderWithCheck(string folderPath)
    {
        try
        {
            EnterFolder(folderPath);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "进入文件夹失败", ModBase.LogLevel.Msgbox);
        }
    }

    /// <summary>
    ///     返回上级文件夹。
    /// </summary>
    private void GoBackToParentFolder()
    {
        if (string.IsNullOrEmpty(CurrentFolderPath))
            return;

        try
        {
            // 获取根路径
            var rootPath = PageInstanceLeft.instance.PathIndie +
                           (PageInstanceLeft.instance.Info.hasLabyMod
                               ? Path.Combine("labymod-neo", "fabric", PageInstanceLeft.instance.Info.vanillaName)
                               : "") + ModLocalComp.GetPathNameByCompType(currentCompType) + @"\";
            rootPath = Path.GetFullPath(rootPath.TrimEnd('\\'));

            // 获取父级路径
            var parentPath = Directory.GetParent(CurrentFolderPath)?.FullName;

            // 如果父级路径就是根路径或者父级路径不在根路径范围内，则返回根目录
            if (parentPath is null || parentPath.Equals(rootPath, StringComparison.OrdinalIgnoreCase) ||
                !parentPath.StartsWith(rootPath + @"\", StringComparison.OrdinalIgnoreCase))
                CurrentFolderPath = "";
            else
                CurrentFolderPath = parentPath;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "路径处理失败");
            // 发生错误时直接返回根目录
            CurrentFolderPath = "";
        }

        ModBase.Log($"[原理图] 返回上级文件夹：{(string.IsNullOrEmpty(CurrentFolderPath) ? "根目录" : CurrentFolderPath)}");

        // 重新加载当前文件夹的内容
        string loadPath;
        if (string.IsNullOrEmpty(CurrentFolderPath))
            // 返回到根目录
            loadPath = PageInstanceLeft.instance.PathIndie +
                       (PageInstanceLeft.instance.Info.hasLabyMod
                           ? Path.Combine("labymod-neo", "fabric", PageInstanceLeft.instance.Info.vanillaName)
                           : "") + ModLocalComp.GetPathNameByCompType(currentCompType) + @"\";
        else
            // 加载当前文件夹
            loadPath = CurrentFolderPath;

        // 强制刷新UI状态
        // 确保按钮状态正确
        ModBase.RunInUi(() =>
            BtnManageBack.Visibility =
                !string.IsNullOrEmpty(CurrentFolderPath) ? Visibility.Visible : Visibility.Collapsed);

        // 延迟一帧后再加载，确保UI状态已更新
        ModBase.RunInUi(
            () => ModLoader.LoaderFolderRun(ModLocalComp.compResourceListLoader, loadPath,
                ModLoader.LoaderFolderRunType.ForceRun, LoaderInput: GetRequireLoaderData()), true);
    }

    #endregion

    #region UI 化

    /// <summary>
    ///     已加载的 Mod UI 缓存，不确保按显示顺序排列。Key 为 Mod 的 RawPath。
    /// </summary>
    public Dictionary<string, MyLocalCompItem> modItems = new();

    /// <summary>
    ///     将加载器结果的 Mod 列表加载为 UI。
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
                PanSchematicEmpty.Visibility = Visibility.Collapsed;
            }
            else
            {
                // 检查是否为投影文件类型且schematics文件夹不存在
                if (currentCompType == ModComp.CompType.Schematic)
                {
                    var schematicsPath = PageInstanceLeft.instance.PathIndie + @"schematics\";
                    if (!Directory.Exists(schematicsPath))
                    {
                        PanSchematicEmpty.Visibility = Visibility.Visible;
                        PanEmpty.Visibility = Visibility.Collapsed;
                        PanBack.Visibility = Visibility.Collapsed;
                        return;
                    }
                }

                // 根据组件类型设置PanEmpty的文本内容
                if (currentCompType == ModComp.CompType.Schematic)
                {
                    // 检查是否在子文件夹中
                    if (!string.IsNullOrEmpty(CurrentFolderPath))
                    {
                        // 子文件夹为空的提示
                        TxtEmptyTitle.Text = Lang.Text("Instance.Resource.EmptyFolder.Title");
                        TxtEmptyDescription.Text = Lang.Text("Instance.Resource.EmptyFolder.Description");
                    }
                    else
                    {
                        // 根目录为空的提示
                        TxtEmptyTitle.Text = Lang.Text("Instance.Resource.Empty.Title");
                        TxtEmptyDescription.Text = Lang.Text("Instance.Resource.Empty.Description");
                    }
                }
                else
                {
                    TxtEmptyTitle.Text = Lang.Text("Instance.Resource.Empty.Title");
                    TxtEmptyDescription.Text = Lang.Text("Instance.Resource.Empty.DescriptionWithDownload");
                }

                // 如果当前在子文件夹中，显示返回上一级按钮
                if (!string.IsNullOrEmpty(CurrentFolderPath))
                    BtnHintBack.Visibility = Visibility.Visible;
                else
                    BtnHintBack.Visibility = Visibility.Collapsed;

                PanEmpty.Visibility = Visibility.Visible;
                PanBack.Visibility = Visibility.Collapsed;
                PanSchematicEmpty.Visibility = Visibility.Collapsed;
                return;
            }

            // 修改缓存
            modItems.Clear();
            var rootPath = PageInstanceLeft.instance.PathIndie +
                           (PageInstanceLeft.instance.Info.hasLabyMod
                               ? Path.Combine("labymod-neo", "fabric", PageInstanceLeft.instance.Info.vanillaName)
                               : "") + ModLocalComp.GetPathNameByCompType(currentCompType) + @"\";
            rootPath = Path.GetFullPath(rootPath.TrimEnd('\\'));

            var itemsToShow = ModLocalComp.compResourceListLoader.output.Where(item =>
            {
                var itemPath = item.IsFolder ? item.ActualPath : item.path;
                var parentDir = Directory.GetParent(itemPath)?.FullName;
                if (string.IsNullOrEmpty(CurrentFolderPath))
                    return parentDir.Equals(rootPath, StringComparison.OrdinalIgnoreCase);

                return parentDir.Equals(CurrentFolderPath, StringComparison.OrdinalIgnoreCase);
            }).ToList();

            foreach (var ModEntity in itemsToShow)
                modItems[ModEntity.RawPath] = BuildLocalCompItem(ModEntity);
            // 显示结果
            ModBase.RunInUi(() =>
            {
                Filter = FilterType.All;
                SearchBox.Text = ""; // 这会触发结果刷新，所以需要在 ModItems 更新之后，详见 #3124 的视频
                RefreshUI();
                SetSortMethod(SortMethod.CompName);
            });
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, $"加载 {currentCompType} 列表 UI 失败", ModBase.LogLevel.Feedback);
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
                Checked = selectedMods.Contains(Entry.RawPath)
            };
            newItem.CurrentSwipe = currentSwipSelect;
            newItem.Tags = Entry.Tags;
            Entry.OnCompUpdate += _ => newItem.Refresh();
            // AddHandler Entry.OnCompUpdate, Sub() RunInUi(Sub() DoSort())
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
        sender.Changed += (ss, ee) => CheckChanged((MyLocalCompItem)ss, ee);
        if (sender.Entry.IsFolder)
        {
            // 文件夹项的点击事件：双击进入文件夹，单击切换选中状态
            var lastClickTime = DateTime.MinValue;
            sender.Click += (sss, _) =>
            {
                var ss = (MyLocalCompItem)sss;
                var currentTime = DateTime.Now;
                var timeDiff = (currentTime - lastClickTime).TotalMilliseconds;

                if (timeDiff <= 300d)
                    // 300ms内双击，进入文件夹
                    EnterFolderWithCheck(ss.Entry.ActualPath);
                else
                    // 单击切换选中状态
                    ss.Checked = !ss.Checked;

                lastClickTime = currentTime;
            };
        }
        else
        {
            // 文件项的点击事件：切换选中状态
            sender.Click += (sss, _) =>
            {
                var ss = (MyLocalCompItem)sss;
                ss.Checked = !ss.Checked;
            };
        }

        // 图标按钮
        var btnOpen = new MyIconButton { LogoScale = 1.05d, Logo = Icon.IconButtonOpen, Tag = sender };
        btnOpen.ToolTip = Lang.Text("Instance.Saves.OpenFileLocation");
        ToolTipService.SetPlacement(btnOpen, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnOpen, 30d);
        ToolTipService.SetHorizontalOffset(btnOpen, 2d);
        btnOpen.Click += (ss, ee) => Open_Click((MyIconButton)ss, ee);
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
        btnDelete.Click += (ss, ee) => Delete_Click((MyIconButton)ss, ee);
        if (currentCompType != ModComp.CompType.Mod ||
            sender.Entry.State == ModLocalComp.LocalCompFile.LocalFileStatus.Unavailable)
        {
            sender.Buttons = new[] { btnCont, btnOpen, btnDelete };
        }
        else
        {
            var btnED = new MyIconButton
            {
                LogoScale = 1d,
                Logo = sender.Entry.State == ModLocalComp.LocalCompFile.LocalFileStatus.Fine
                    ? Icon.IconButtonStop
                    : Icon.IconButtonCheck,
                Tag = sender
            };
            btnED.ToolTip = sender.Entry.State == ModLocalComp.LocalCompFile.LocalFileStatus.Fine ? Lang.Text("Instance.Resource.Disable") : Lang.Text("Instance.Resource.Enable");
            ToolTipService.SetPlacement(btnED, PlacementMode.Center);
            ToolTipService.SetVerticalOffset(btnED, 30d);
            ToolTipService.SetHorizontalOffset(btnED, 2d);
            btnED.Click += (ss, ee) => ED_Click((MyIconButton)ss, ee);
            sender.Buttons = new[] { btnCont, btnOpen, btnED, btnDelete };
        }
    }

    /// <summary>
    ///     刷新整个 UI。
    /// </summary>
    public void RefreshUI()
    {
        if (PanList is null)
            return;
        var showingMods = (IsSearching ? searchResult : modItems.Values.Select(i => i.Entry))
            .Where(m => CanPassFilter(m)).ToList();

        // 对显示的资源进行排序，确保文件夹置顶
        if (showingMods.Any())
        {
            var sortMethod = GetSortMethod(currentSortMethod);
            showingMods.Sort((a, b) => sortMethod(a, b));
        }

        // 重新列出列表
        ModAnimation.AniControlEnabled += 1;
        if (showingMods.Any())
        {
            PanList.Visibility = Visibility.Visible;
            PanList.Children.Clear();
            foreach (var TargetMod in showingMods)
            {
                if (!modItems.ContainsKey(TargetMod.RawPath))
                    continue;
                var item = modItems[TargetMod.RawPath];

                // 确保元素没有父容器，避免重复添加异常
                if (item.Parent is not null) ((Panel)item.Parent).Children.Remove(item);

                ModStyle.MinecraftFormatter.SetColorfulTextLab(item.LabTitle.Text, item.LabTitle,
                    ThemeService.IsDarkMode);
                ModStyle.MinecraftFormatter.SetColorfulTextLab(item.LabInfo.Text, item.LabInfo,
                    ThemeService.IsDarkMode);
                item.Checked = selectedMods.Contains(TargetMod.RawPath); // 更新选中状态
                PanList.Children.Add(item);
            }
        }
        else
        {
            PanList.Visibility = Visibility.Collapsed;
        }

        ModAnimation.AniControlEnabled -= 1;
        selectedMods =
            new HashSet<string>(selectedMods.Where(m => showingMods.Any(s => (s.RawPath ?? "") == (m ?? ""))));
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
            var itemSource = (IsSearching ? searchResult : modItems.Values.Select(i => i.Entry)).ToArray();
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
            // 查找重复项目
            var duplicateItems = await Task.Run(() => itemSource.GroupBy(m =>
            {
                if (m.Comp is null) return ":Nothing:";

                return m.Comp.id;
            }).Where(g => g.Count() > 1 && g.First().Comp is not null).SelectMany(g => g).ToList());
            BtnFilterDuplicate.Text = Lang.Text("Instance.Resource.Filter.DuplicateWithCount", duplicateItems.Count);
            BtnFilterDuplicate.Visibility = Filter == FilterType.Duplicate || duplicateItems.Any()
                ? Visibility.Visible
                : Visibility.Collapsed;

            // 返回按钮显示控制（在子文件夹中时显示）
            if (!string.IsNullOrEmpty(CurrentFolderPath))
                BtnManageBack.Visibility = Visibility.Visible;
            else
                BtnManageBack.Visibility = Visibility.Collapsed;

            // -----------------
            // 底部栏
            // -----------------

            // 计数
            var newCount = selectedMods.Count;
            var selected = newCount > 0;
            if (selected)
                LabSelect.Text = Lang.Text("Instance.Resource.SelectedCount", newCount); // 取消所有选择时不更新数字
            // 按钮可用性
            if (selected)
            {
                var hasUpdate = false;
                var hasEnabled = false;
                var hasDisabled = false;
                var canFavoriteAndShare = true; // 是否可以收藏和分享


                // 检查是否所有选中的资源都有有效的项目信息（即已完成联网更新）
                await Task.Run(() =>
                {
                    foreach (var ModEntity in ModLocalComp.compResourceListLoader.output)
                        if (selectedMods.Contains(ModEntity.RawPath))
                        {
                            if (ModEntity.CanUpdate) hasUpdate = true;
                            if (ModEntity.State == ModLocalComp.LocalCompFile.LocalFileStatus.Fine)
                                hasEnabled = true;
                            else if (ModEntity.State == ModLocalComp.LocalCompFile.LocalFileStatus.Disabled)
                                hasDisabled = true;
                            if (ModEntity.Comp is null || string.IsNullOrEmpty(ModEntity.Comp.id))
                                canFavoriteAndShare = false;
                        }
                });

                BtnSelectDisable.IsEnabled = hasEnabled;
                BtnSelectEnable.IsEnabled = hasDisabled;
                BtnSelectUpdate.IsEnabled = hasUpdate;

                // 针对投影原理图隐藏分享 更新 收藏按钮
                if (currentCompType == ModComp.CompType.Schematic)
                {
                    BtnSelectUpdate.Visibility = Visibility.Collapsed;
                    BtnSelectFavorites.Visibility = Visibility.Collapsed;
                    BtnSelectShare.Visibility = Visibility.Collapsed;
                }
                else
                {
                    BtnSelectUpdate.Visibility = Visibility.Visible;
                    BtnSelectFavorites.Visibility = Visibility.Visible;
                    BtnSelectShare.Visibility = Visibility.Visible;

                    // 根据是否已加载项目信息来启用/禁用收藏和分享按钮
                    BtnSelectFavorites.IsEnabled = canFavoriteAndShare;
                    BtnSelectShare.IsEnabled = canFavoriteAndShare;
                }
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
                        }, "Mod Sidebar");
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
                        }, "Mod Sidebar");
                }
            }
            else
            {
                ModAnimation.AniStop("Mod Sidebar");
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
    ///     打开 Mods 文件夹。
    /// </summary>
    private void BtnManageBack_Click(object sender, EventArgs e)
    {
        GoBackToParentFolder();
    }

    private void BtnHintBack_Click(object sender, EventArgs e)
    {
        GoBackToParentFolder();
    }

    private void BtnManageOpen_Click(object sender, EventArgs e)
    {
        try
        {
            string compFilePath;

            // 如果当前在子文件夹中，则打开当前子文件夹；否则打开根目录
            if (string.IsNullOrEmpty(CurrentFolderPath))
                // 打开根目录
                compFilePath = PageInstanceLeft.instance.PathIndie +
                               (PageInstanceLeft.instance.Info.hasLabyMod
                                   ? Path.Combine("labymod-neo", "fabric", PageInstanceLeft.instance.Info.vanillaName)
                                   : "") + ModLocalComp.GetPathNameByCompType(currentCompType) + @"\";
            else
                // 打开当前子文件夹
                compFilePath = CurrentFolderPath.EndsWith(@"\") ? CurrentFolderPath : CurrentFolderPath + @"\";
            Directory.CreateDirectory(compFilePath);
            ModBase.OpenExplorer(compFilePath);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "打开 Mods 文件夹失败", ModBase.LogLevel.Msgbox);
        }
    }


    /// <summary>
    ///     全选。
    /// </summary>
    private void BtnManageSelectAll_Click(object sender, MouseButtonEventArgs e)
    {
        ChangeAllSelected(selectedMods.Count < PanList.Children.Count);
    }

    /// <summary>
    ///     安装 Mod。
    /// </summary>
    private void BtnManageInstall_Click(object sender, MouseButtonEventArgs e)
    {
        string[] fileList = null;
        switch (currentCompType)
        {
            case ModComp.CompType.Mod:
            {
                fileList = SystemDialogs.SelectFiles(
                    "Mod 文件(*.jar;*.litemod;*.disabled;*.old)|*.jar;*.litemod;*.disabled;*.old", "选择要安装的 Mod");
                break;
            }
            case ModComp.CompType.ResourcePack:
            {
                fileList = SystemDialogs.SelectFiles("资源包文件(*.zip)|*.zip", "选择要安装的资源包");
                break;
            }
            case ModComp.CompType.Shader:
            {
                fileList = SystemDialogs.SelectFiles("光影包文件(*.zip)|*.zip", "选择要安装的光影包");
                break;
            }
            case ModComp.CompType.Schematic:
            {
                fileList = SystemDialogs.SelectFiles(
                    "投影原理图文件(*.litematic;*.nbt;*.schematic;*.schem)|*.litematic;*.nbt;*.schematic;*.schem",
                    "选择要安装的投影原理图");
                break;
            }
        }

        if (fileList is null || !fileList.Any())
            return;
        InstallCompFiles(fileList, currentCompType, CurrentFolderPath);
    }

    /// <summary>
    ///     尝试安装 Mod。
    ///     返回输入的文件是否为一个 Mod 文件，仅用于判断拖拽行为。
    /// </summary>
    public static bool InstallMods(IEnumerable<string> filePathList)
    {
        if (!filePathList.Any()) return false;

        // 1. Check file extension
        var firstFile = filePathList.First();
        var extension = firstFile.Split('.').LastOrDefault()?.ToLower();
        string[] allowedExtensions = { "jar", "litemod", "disabled", "old" };

        if (!allowedExtensions.Contains(extension)) return false;

        LogWrapper.Info("[System] 文件格式为 jar/litemod，尝试安装为 Mod");

        // 2. Check recycle bin
        if (firstFile.Contains(@":\$RECYCLE.BIN\"))
        {
            HintWrapper.Show(Lang.Text("Instance.Resource.Install.RestoreFromRecycleBin"), HintTheme.Error);
            return true;
        }

        // 3. Determine target instance
        var targetInstance = ModMinecraft.McInstanceSelected;
        if (ModMain.frmMain.pageCurrent == FormMain.PageType.InstanceSetup) targetInstance = PageInstanceLeft.instance;

        // 4. Validate instance status
        if (ModMain.frmMain.pageCurrent == FormMain.PageType.InstanceSelect || targetInstance is null ||
            !targetInstance.Modable)
        {
            HintWrapper.Show(Lang.Text("Instance.Resource.Install.SelectModableInstance"));
            return true;
        }

        // 5. Check if user confirmation is required
        var isModPage = ModMain.frmMain.pageCurrent == FormMain.PageType.InstanceSetup &&
                        ModMain.frmMain.PageCurrentSub == FormMain.PageSubType.VersionMod;

        if (!isModPage)
        {
            if (ModMain.MyMsgBox(Lang.Text("Instance.Resource.Install.ModConfirm.Message", targetInstance.Name), Lang.Text("Instance.Resource.Install.ModConfirm.Title"), Lang.Text("Common.Action.Confirm"), Lang.Text("Common.Action.Cancel")) !=
                1) return true;
        }

        // 6. Execution: Install Mods
        ExecuteModInstallation(targetInstance, filePathList, isModPage);

        return true;
    }

    private static void ExecuteModInstallation(ModMinecraft.McInstance targetInstance, IEnumerable<string> filePathList,
        bool refreshList)
    {
        // Path resolution logic
        var modPathSuffix = targetInstance.Info.hasLabyMod
            ? $@"labymod-neo\fabric\{targetInstance.Info.vanillaName}\"
            : "";
        var modFolder = $@"{targetInstance.PathIndie}{modPathSuffix}mods\";

        try
        {
            foreach (var modFile in filePathList)
            {
                var fileName = ModBase.GetFileNameFromPath(modFile)
                    .Replace(".disabled", "")
                    .Replace(".old", "");

                if (!fileName.Contains(".")) fileName += ".jar"; // Ensure extension (#4227)

                ModBase.CopyFile(modFile, Path.Combine(modFolder, fileName));
            }

            // Success hint
            if (filePathList.Count() == 1)
            {
                var installedName = ModBase.GetFileNameFromPath(filePathList.First()).Replace(".disabled", "")
                    .Replace(".old", "");
                HintWrapper.Show(Lang.Text("Instance.Resource.Install.SuccessSingle", installedName), HintTheme.Success);
            }
            else
            {
                HintWrapper.Show(Lang.Text("Instance.Resource.Install.SuccessMultiple", filePathList.Count(), Lang.Text("Download.Comp.Type.Mod")), HintTheme.Success);
            }

            // 7. Refresh list if necessary
            if (refreshList)
                ModLoader.LoaderFolderRun(ModLocalComp.compResourceListLoader,
                    modFolder,
                    ModLoader.LoaderFolderRunType.ForceRun,
                    LoaderInput: ModMain.frmInstanceMod.GetRequireLoaderData()
                );
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "拷贝文件失败");
        }
    }

    /// <summary>
    ///     安装组件文件（Mod、资源包、光影包、投影文件等）。
    /// </summary>
    public static void InstallCompFiles(IEnumerable<string> FilePathList, ModComp.CompType CompType,
        string TargetFolderPath = "")
    {
        if (!FilePathList.Any())
            return;

        var extension = FilePathList.First().AfterLast(".").ToLower();
        string[] validExtensions = null;
        var compTypeName = "";
        var compFolder = "";

        // 检查回收站：回收站中的文件有错误的文件名
        if (FilePathList.First().Contains(@":\$RECYCLE.BIN\"))
        {
            ModMain.Hint(Lang.Text("Instance.Resource.Install.RestoreFromRecycleBin"), ModMain.HintType.Critical);
            return;
        }

        // 获取并检查目标实例
        var targetInstance = ModMinecraft.McInstanceSelected;
        if (ModMain.frmMain.pageCurrent == FormMain.PageType.InstanceSetup)
            targetInstance = PageInstanceLeft.instance;

        // 根据组件类型设置相关参数
        switch (CompType)
        {
            case ModComp.CompType.Mod:
            {
                validExtensions = new[] { "jar", "litemod", "disabled", "old" };
                compTypeName = "Mod";
                if (string.IsNullOrEmpty(TargetFolderPath))
                    compFolder = targetInstance.PathIndie +
                                 (targetInstance.Info.hasLabyMod
                                     ? Path.Combine("labymod-neo", "fabric", targetInstance.Info.vanillaName)
                                     : "") + @"mods\";
                else
                    compFolder = TargetFolderPath;

                break;
            }
            case ModComp.CompType.ResourcePack:
            {
                validExtensions = new[] { "zip" };
                compTypeName = Lang.Text("Download.Comp.Type.ResourcePack");
                if (string.IsNullOrEmpty(TargetFolderPath))
                    compFolder = targetInstance.PathIndie + @"resourcepacks\";
                else
                    compFolder = TargetFolderPath;

                break;
            }
            case ModComp.CompType.Shader:
            {
                validExtensions = new[] { "zip" };
                compTypeName = Lang.Text("Download.Comp.Type.Shader");
                if (string.IsNullOrEmpty(TargetFolderPath))
                    compFolder = targetInstance.PathIndie + @"shaderpacks\";
                else
                    compFolder = TargetFolderPath;

                break;
            }
            case ModComp.CompType.Schematic:
            {
                validExtensions = new[] { "litematic", "nbt", "schematic", "schem" };
                compTypeName = Lang.Text("Download.Comp.Type.Schematic");
                if (string.IsNullOrEmpty(TargetFolderPath))
                    compFolder = targetInstance.PathIndie + @"schematics\";
                else
                    compFolder = TargetFolderPath;

                break;
            }
        }

        // 检查文件扩展名
        if (!validExtensions.Contains(extension))
        {
            ModMain.Hint(Lang.Text("Instance.Resource.Install.UnsupportedFormat", extension, compTypeName, string.Join(", ", validExtensions)),
                ModMain.HintType.Critical);
            return;
        }

        ModBase.Log($"[System] 文件为 {extension} 格式，尝试作为{compTypeName}安装");

        // 检查实例兼容性
        if (CompType == ModComp.CompType.Mod && (ModMain.frmMain.pageCurrent == FormMain.PageType.InstanceSelect ||
                                                 targetInstance is null || !targetInstance.Modable))
        {
            ModMain.Hint(Lang.Text("Instance.Resource.Install.SelectModableInstance"));
            return;
        }

        // 确认安装
        var currentPage = FormMain.PageSubType.VersionMod;
        switch (CompType)
        {
            case ModComp.CompType.Mod:
            {
                currentPage = FormMain.PageSubType.VersionMod;
                break;
            }
            case ModComp.CompType.ResourcePack:
            {
                currentPage = FormMain.PageSubType.VersionResourcePack;
                break;
            }
            case ModComp.CompType.Shader:
            {
                currentPage = FormMain.PageSubType.VersionShader;
                break;
            }
            case ModComp.CompType.Schematic:
            {
                currentPage = FormMain.PageSubType.VersionSchematic;
                break;
            }
        }

        if (!(ModMain.frmMain.pageCurrent == FormMain.PageType.InstanceSetup &&
              ModMain.frmMain.PageCurrentSub == currentPage))
            if (ModMain.MyMsgBox(
                    Lang.Text("Instance.Resource.Install.GenericConfirm.Message", compTypeName, targetInstance.Name),
                    Lang.Text("Instance.Resource.Install.GenericConfirm.Title", compTypeName), Lang.Text("Common.Action.Confirm"), Lang.Text("Common.Action.Cancel")) != 1)
                return;

        // 执行安装
        try
        {
            Directory.CreateDirectory(compFolder);
            foreach (var FilePath in FilePathList)
            {
                var newFileName = ModBase.GetFileNameFromPath(FilePath);
                if (CompType == ModComp.CompType.Mod)
                {
                    newFileName = newFileName.Replace(".disabled", "").Replace(".old", "");
                    if (!newFileName.Contains("."))
                        newFileName += ".jar";
                }

                var destFile = compFolder + newFileName;
                if (File.Exists(destFile))
                    if (ModMain.MyMsgBox(Lang.Text("Instance.Resource.Install.OverwriteConfirm.Message", newFileName), Lang.Text("Instance.Resource.Install.OverwriteConfirm.Title"), Lang.Text("Common.Action.Overwrite"), Lang.Text("Common.Action.Cancel")) != 1)
                        continue;

                ModBase.CopyFile(FilePath, destFile);
            }

            if (FilePathList.Count() == 1)
                ModMain.Hint(Lang.Text("Instance.Resource.Install.SuccessSingle", ModBase.GetFileNameFromPath(FilePathList.First())), ModMain.HintType.Finish);
            else
                ModMain.Hint(Lang.Text("Instance.Resource.Install.SuccessMultiple", FilePathList.Count(), compTypeName), ModMain.HintType.Finish);

            // 刷新列表
            if (ModMain.frmMain.pageCurrent == FormMain.PageType.InstanceSetup &&
                ModMain.frmMain.PageCurrentSub == currentPage)
                switch (CompType)
                {
                    case ModComp.CompType.Mod:
                    {
                        if (ModMain.frmInstanceMod is not null)
                            ModLoader.LoaderFolderRun(ModLocalComp.compResourceListLoader, compFolder,
                                ModLoader.LoaderFolderRunType.ForceRun,
                                LoaderInput: ModMain.frmInstanceMod?.GetRequireLoaderData());

                        break;
                    }
                    case ModComp.CompType.ResourcePack:
                    case ModComp.CompType.Shader:
                    case ModComp.CompType.Schematic:
                    {
                        var currentForm = GetCurrentCompResourceForm();
                        if (currentForm is not null) ModBase.RunInUi(() => currentForm.ReloadCompFileList(true));

                        break;
                    }
                }
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, $"复制{compTypeName}文件失败", ModBase.LogLevel.Msgbox);
        }
    }

    /// <summary>
    ///     获取当前的组件资源管理窗体。
    /// </summary>
    private static PageInstanceCompResource GetCurrentCompResourceForm()
    {
        switch (ModMain.frmMain.PageCurrentSub)
        {
            case FormMain.PageSubType.VersionMod:
            {
                return ModMain.frmInstanceMod;
            }
            case FormMain.PageSubType.VersionResourcePack:
            {
                return ModMain.frmInstanceResourcePack;
            }
            case FormMain.PageSubType.VersionShader:
            {
                return ModMain.frmInstanceShader;
            }
            case FormMain.PageSubType.VersionSchematic:
            {
                return ModMain.frmInstanceSchematic;
            }

            default:
            {
                return null;
            }
        }
    }

    private void BtnManageInfoExport_Click(object sender, MouseButtonEventArgs e)
    {
        var choice =
            ModMain.MyMsgBox(
                Lang.Text("Instance.Resource.Export.Mode.Message"), Lang.Text("Instance.Resource.Export.Mode.Title"), Lang.Text("Instance.Resource.Export.Mode.Txt"), Lang.Text("Instance.Resource.Export.Mode.Csv"), Lang.Text("Common.Action.Cancel"));

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
                ModBase.Log(ex, "导出资源信息失败", ModBase.LogLevel.Msgbox);
            }
        }

        ;
        switch (choice)
        {
            case 1: // TXT
            {
                var exportContent = new List<string>();
                foreach (var ModEntity in ModLocalComp.compResourceListLoader.output)
                    exportContent.Add(ModEntity.FileName);
                ExportText(exportContent.Join("\r\n"), PageInstanceLeft.instance.Name + "已安装的资源信息.txt");
                break;
            }

            case 2: // CSV
            {
                var exportContent = new List<string>();
                exportContent.Add("文件名,资源名称,资源版本,此版本更新时间,Mod ID,对应平台工程 ID,文件大小（字节）,文件路径");
                foreach (var ModEntity in ModLocalComp.compResourceListLoader.output)
                    exportContent.Add(
                        $"{ModEntity.FileName},{ModEntity.Comp?.TranslatedName},{ModEntity.Version},{ModEntity.compFile?.releaseDate},{ModEntity.ModId},{ModEntity.Comp?.id},{GetModFileInfo(ModEntity.path).Length},{ModEntity.path}");
                ExportText(exportContent.Join("\r\n"), PageInstanceLeft.instance.Name + "已安装的资源信息.csv");
                break;
            }
        }
    }

    /// <summary>
    ///     下载 Mod。
    /// </summary>
    private void BtnManageDownload_Click(object sender, MouseButtonEventArgs e)
    {
        switch (currentCompType)
        {
            case ModComp.CompType.Mod:
            {
                ModMain.frmMain.PageChange(FormMain.PageType.Download, FormMain.PageSubType.DownloadMod);
                break;
            }
            case ModComp.CompType.ResourcePack:
            {
                ModMain.frmMain.PageChange(FormMain.PageType.Download, FormMain.PageSubType.DownloadResourcePack);
                break;
            }
            case ModComp.CompType.Shader:
            {
                ModMain.frmMain.PageChange(FormMain.PageType.Download, FormMain.PageSubType.DownloadShader);
                break;
            }
        }

        PageComp.targetVersion = PageInstanceLeft.instance; // 将当前实例设置为筛选器
    }

    /// <summary>
    ///     下载投影Mod按钮点击事件。
    /// </summary>
    private void BtnSchematicDownloadMod_Click(object sender, MouseButtonEventArgs e)
    {
        ModMain.frmMain.PageChange(FormMain.PageType.Download, FormMain.PageSubType.DownloadMod);
        PageComp.targetVersion = PageInstanceLeft.instance; // 将当前实例设置为筛选器
    }

    /// <summary>
    ///     实例选择按钮点击事件。
    /// </summary>
    private void BtnSchematicVersionSelect_Click(object sender, MouseButtonEventArgs e)
    {
        ModMain.frmMain.PageChange(FormMain.PageType.Launch);
        ModMain.frmMain.PageChange(FormMain.PageType.InstanceSelect);
    }

    #endregion

    #region 选择

    /// <summary>
    ///     选择的 Mod 的路径（不含 .disabled 和 .old）。
    /// </summary>
    public HashSet<string> selectedMods = new();

    // 单项切换选择状态
    public void CheckChanged(MyLocalCompItem sender, ModBase.RouteEventArgs e)
    {
        if (ModAnimation.AniControlEnabled != 0)
            return;
        // 更新选择了的内容
        var selectedKey = sender.Entry.RawPath;
        if (sender.Checked)
            selectedMods.Add(selectedKey);
        else
            selectedMods.Remove(selectedKey);
        RefreshBars();
    }

    // 切换所有项的选择状态
    private void ChangeAllSelected(bool Value)
    {
        ModAnimation.AniControlEnabled += 1;
        selectedMods.Clear();
        foreach (var Item in modItems.Values)
        {
            // #4992，Mod 从过滤器看可能不应在列表中，但因为刚切换状态所以依然保留在列表中，所以应该从列表 UI 判断，而非从过滤器判断
            var shouldSelected = Value && PanList.Children.Contains(Item);
            Item.Checked = shouldSelected;
            if (shouldSelected)
                selectedMods.Add(Item.Entry.RawPath);
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

    private void FrmMain_KeyDown(object sender, KeyEventArgs e) // 若监听自己的事件则在进入页面后需点击右侧控件才可监听到 (#4311)
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
                case FilterType.Duplicate:
                {
                    BtnFilterDuplicate.Checked = true;
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
        Unavailable = 4,
        Duplicate = 5
    }

    /// <summary>
    ///     检查该 Mod 项是否符合当前筛选的类别。
    /// </summary>
    private bool CanPassFilter(ModLocalComp.LocalCompFile CheckingMod)
    {
        switch (Filter)
        {
            case FilterType.All:
            {
                return true;
            }
            case FilterType.Enabled:
            {
                return CheckingMod.State == ModLocalComp.LocalCompFile.LocalFileStatus.Fine;
            }
            case FilterType.Disabled:
            {
                return CheckingMod.State == ModLocalComp.LocalCompFile.LocalFileStatus.Disabled;
            }
            case FilterType.CanUpdate:
            {
                return CheckingMod.CanUpdate;
            }
            case FilterType.Unavailable:
            {
                return CheckingMod.State == ModLocalComp.LocalCompFile.LocalFileStatus.Unavailable;
            }
            case FilterType.Duplicate:
            {
                var itemSource = IsSearching
                    ? searchResult
                    : ModLocalComp.compResourceListLoader.output ?? new List<ModLocalComp.LocalCompFile>();
                return itemSource is not null && itemSource.Where(m =>
                    CheckingMod.Comp is not null && m.Comp is not null &&
                    (CheckingMod.Comp.id ?? "") == (m.Comp.id ?? "")).Skip(1).Any();
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
        // RefreshUI()
        DoSort();
    }

    private enum SortMethod
    {
        FileName,
        CompName,
        TagNums,
        CreateTime,
        ModFileSize
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
            case SortMethod.TagNums:
            {
                return Lang.Text("Instance.Resource.Sort.TagCount");
            }
            case SortMethod.CreateTime:
            {
                return Lang.Text("Instance.Resource.Sort.AddTime");
            }
            case SortMethod.ModFileSize:
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
                var invalid = items.Where(i =>
                    i.Entry is null || (currentSortMethod == SortMethod.TagNums && i.Entry.Comp is null &&
                                        !i.Entry.IsFolder)).ToList();
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
        // 通用的文件夹置顶比较函数
        int folderFirstCompare(ModLocalComp.LocalCompFile a, ModLocalComp.LocalCompFile b)
        {
            if (a.IsFolder && !b.IsFolder)
                return -1;
            if (!a.IsFolder && b.IsFolder)
                return 1;
            return 0; // 相同类型，需要进一步比较
        }

        ;

        switch (Method)
        {
            case SortMethod.FileName:
            {
                return (a, b) =>
                {
                    // 文件夹始终排在最前面
                    var folderResult = folderFirstCompare(a, b);
                    if (folderResult != 0)
                        return folderResult;
                    // 如果都是文件夹或都是文件，则按文件名排序
                    return string.Compare(a.FileName, b.FileName, StringComparison.OrdinalIgnoreCase);
                };
            }
            case SortMethod.CompName:
            {
                return (a, b) =>
                {
                    // 文件夹始终排在最前面
                    var folderResult = folderFirstCompare(a, b);
                    if (folderResult != 0)
                        return folderResult;
                    // 如果都是文件夹或都是文件，则按资源名称排序
                    return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                };
            }
            case SortMethod.TagNums:
            {
                return (a, b) =>
                {
                    // 文件夹始终排在最前面
                    var folderResult = folderFirstCompare(a, b);
                    if (folderResult != 0)
                        return folderResult;
                    // 如果都是文件夹，则按名称排序
                    if (a.IsFolder && b.IsFolder)
                        return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                    // 如果都是文件，则按标签数量排序（标签多的在前）
                    if (!a.IsFolder && !b.IsFolder)
                    {
                        // 安全检查，确保Comp不为空
                        var aTagCount = a.Comp?.tags?.Count ?? 0;
                        var bTagCount = b.Comp?.tags?.Count ?? 0;
                        return bTagCount.CompareTo(aTagCount);
                    }

                    // 理论上不会到达这里，但为了安全起见
                    return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                };
            }
            case SortMethod.CreateTime:
            {
                return (a, b) =>
                {
                    // 文件夹始终排在最前面
                    var folderResult = folderFirstCompare(a, b);
                    if (folderResult != 0)
                        return folderResult;
                    // 如果都是文件夹或都是文件，则按创建时间排序（新的在前）
                    var aPath = a.IsFolder ? a.ActualPath : a.path;
                    var bPath = b.IsFolder ? b.ActualPath : b.path;
                    var aDate = GetModFileInfo(aPath).CreationTime;
                    var bDate = GetModFileInfo(bPath).CreationTime;
                    if (aDate == DateTime.MinValue && bDate == DateTime.MinValue)
                        return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);

                    if (aDate == DateTime.MinValue) return 1; // 出错的文件排在后面

                    if (bDate == DateTime.MinValue) return -1;
                    return bDate.CompareTo(aDate);
                };
            }
            case SortMethod.ModFileSize:
            {
                return (a, b) =>
                {
                    // 文件夹始终排在最前面
                    var folderResult = folderFirstCompare(a, b);
                    if (folderResult != 0)
                        return folderResult;
                    // 如果都是文件夹，则按名称排序
                    if (a.IsFolder && b.IsFolder)
                        return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                    // 如果都是文件，则按文件大小排序（大的在前）
                    if (!a.IsFolder && !b.IsFolder)
                    {
                        var aSize = GetModFileInfo(a.ActualPath).Length;
                        var bSize = GetModFileInfo(b.ActualPath).Length;
                        if (aSize == 0L && bSize == 0L)
                            return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);

                        if (aSize == 0L) return 1;

                        if (bSize == 0L) return -1;
                        return bSize.CompareTo(aSize);
                    }

                    // 理论上不会到达这里，但为了安全起见
                    return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                };
            }

            default:
            {
                return (a, b) =>
                {
                    // 文件夹始终排在最前面
                    var folderResult = folderFirstCompare(a, b);
                    if (folderResult != 0)
                        return folderResult;
                    // 如果都是文件夹或都是文件，则按名称排序
                    return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                };
            }
        }
    }

    #endregion

    #region 下边栏

    // 启用 / 禁用
    private void BtnSelectED_Click(object sender, ModBase.RouteEventArgs e)
    {
        EDMods(ModLocalComp.compResourceListLoader.output.Where(m => selectedMods.Contains(m.RawPath)).ToList(),
            !sender.Equals(BtnSelectDisable));
        ChangeAllSelected(false);
    }

    private void EDMods(IEnumerable<ModLocalComp.LocalCompFile> ModList, bool IsEnable)
    {
        var isSuccessful = true;
        foreach (var ModE in ModList)
        {
            var modEntity = ModE; // 仅用于去除迭代变量无法修改的限制
            string newPath = null;
            if (modEntity.State == ModLocalComp.LocalCompFile.LocalFileStatus.Fine && !IsEnable)
                // 禁用
                newPath = modEntity.path + (File.Exists(modEntity.path + ".old") ? ".old" : ".disabled");
            else if (modEntity.State == ModLocalComp.LocalCompFile.LocalFileStatus.Disabled && IsEnable)
                // 启用
                newPath = modEntity.RawPath;
            else
                continue;
            // 重命名
            try
            {
                if (File.Exists(newPath))
                {
                    if (File.Exists(modEntity.path))
                    {
                        // 同时存在两个名称的 Mod
                        if ((ModBase.GetFileMD5(modEntity.path) ?? "") != (ModBase.GetFileMD5(newPath) ?? ""))
                        {
                            ModMain.MyMsgBox(
                                Lang.Text("Instance.Resource.Ed.FileConflict.Message", newPath, modEntity.path),
                                Lang.Text("Instance.Resource.Ed.FileConflict"));
                            continue;
                        }
                    }
                    else
                    {
                        // 已经重命名过了
                        ModBase.Log("[Mod] Mod 的状态已被切换", ModBase.LogLevel.Debug);
                        continue;
                    }
                }

                File.Delete(newPath);
                FileSystem.Rename(modEntity.path, newPath);
            }
            catch (FileNotFoundException ex)
            {
                ModBase.Log(ex, $"未找到需要重命名的 Mod（{modEntity.path ?? "null"}）", ModBase.LogLevel.Feedback);
                ReloadCompFileList(true);
                return;
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, $"重命名 Mod 失败（{modEntity.path ?? "null"}）");
                isSuccessful = false;
            }

            // 更改 Loader 中的列表
            var newModEntity = new ModLocalComp.LocalCompFile(newPath);
            newModEntity.FromJson(modEntity.ToJson());
            if (ModLocalComp.compResourceListLoader.output.Contains(modEntity))
            {
                var indexOfLoader = ModLocalComp.compResourceListLoader.output.IndexOf(modEntity);
                ModLocalComp.compResourceListLoader.output.RemoveAt(indexOfLoader);
                ModLocalComp.compResourceListLoader.output.Insert(indexOfLoader, newModEntity);
            }

            if (searchResult is not null && searchResult.Contains(modEntity)) // #4862
            {
                var indexOfResult = searchResult.IndexOf(modEntity);
                searchResult.Remove(modEntity);
                searchResult.Insert(indexOfResult, newModEntity);
            }

            // 更改 UI 中的列表
            try
            {
                var newItem = BuildLocalCompItem(newModEntity);
                modItems[modEntity.RawPath] = newItem;
                var indexOfUi = PanList.Children.IndexOf(PanList.Children.OfType<MyLocalCompItem>()
                    .FirstOrDefault(i => ReferenceEquals(i.Entry, modEntity)));
                if (indexOfUi == -1)
                    continue; // 因为未知原因 Mod 的状态已经切换完了
                PanList.Children.RemoveAt(indexOfUi);
                PanList.Children.Insert(indexOfUi, newItem);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, $"更新 UI 列表项失败：{modEntity.FileName}", ModBase.LogLevel.Hint);
            }
        }

        Dispatcher.Invoke(() => PanList.UpdateLayout(), DispatcherPriority.Background);
        if (isSuccessful)
        {
            RefreshBars();
        }
        else
        {
            ModMain.Hint(Lang.Text("Instance.Resource.Ed.ToggleFailed"), ModMain.HintType.Critical);
            ReloadCompFileList(true);
        }

        LoaderRun(ModLoader.LoaderFolderRunType.UpdateOnly);
    }

    // 更新
    private void BtnSelectUpdate_Click(object sender, ModBase.RouteEventArgs e)
    {
        var updateList = ModLocalComp.compResourceListLoader.output
            .Where(m => selectedMods.Contains(m.RawPath) && m.CanUpdate).ToList();
        if (!updateList.Any())
            return;
        UpdateResource(updateList);
        ChangeAllSelected(false);
    }

    /// <summary>
    ///     记录正在进行 Mod 更新的 mods 文件夹路径。
    /// </summary>
    public static List<string> updatingVersions = new();

    public void UpdateResource(IEnumerable<ModLocalComp.LocalCompFile> ModList)
    {
        // 更新前警告
        if (currentCompType == ModComp.CompType.Mod && (!States.Hint.UpdateMod || ModList.Count() >= 15))
        {
            if (ModMain.MyMsgBox(
                    Lang.Text("Instance.Resource.Update.Warning.Message"),
                    Lang.Text("Instance.Resource.Update.Warning.Title"), Lang.Text("Instance.Resource.Update.Warning.Confirm"), Lang.Text("Common.Action.Cancel"), IsWarn: true) == 1)
                States.Hint.UpdateMod = true;
            else
                return;
        }

        try
        {
            // 构造下载信息
            ModList = ModList.ToList(); // 防止刷新影响迭代器
            var fileList = new List<DownloadFile>();
            var fileCopyList = new Dictionary<string, string>();
            foreach (var Entry in ModList)
            {
                var file = Entry.UpdateFile;
                if (!file.Available)
                    continue;
                // 确认更新后的文件名
                var currentReplaceName = Entry.compFile.fileName.Replace(".jar", "").Replace(".old", "")
                    .Replace(".disabled", "");
                var newestReplaceName = Entry.UpdateFile.fileName.Replace(".jar", "").Replace(".old", "")
                    .Replace(".disabled", "");
                var currentSegs = currentReplaceName.Split('-').ToList();
                var newestSegs = newestReplaceName.Split('-').ToList();
                var shortened = false;
                while (true) // 移除前导相同部分（不能移除所有相同项，这会导致例如 1.2-forge-2 和 1.3-forge-3 中间的 forge 被去掉，导致尝试替换 1.2-2）
                {
                    if (!currentSegs.Any() || !newestSegs.Any())
                        break;
                    if ((currentSegs.First() ?? "") != (newestSegs.First() ?? ""))
                        break;
                    currentSegs.RemoveAt(0);
                    newestSegs.RemoveAt(0);
                    shortened = true;
                }

                while (true) // 移除后导相同部分
                {
                    if (!currentSegs.Any() || !newestSegs.Any())
                        break;
                    if ((currentSegs.Last() ?? "") != (newestSegs.Last() ?? ""))
                        break;
                    currentSegs.RemoveAt(currentSegs.Count - 1);
                    newestSegs.RemoveAt(newestSegs.Count - 1);
                    shortened = true;
                }

                if (shortened && currentSegs.Any() && newestSegs.Any())
                {
                    currentReplaceName = currentSegs.Join("-");
                    newestReplaceName = newestSegs.Join("-");
                }

                // 添加到下载列表
                var tempAddress = ModBase.pathTemp + @"DownloadedComp\" +
                                  Entry.FileName.Replace(currentReplaceName, newestReplaceName);
                var realAddress = ModBase.GetPathFromFullPath(Entry.path) +
                                  Entry.FileName.Replace(currentReplaceName, newestReplaceName);
                fileList.Add(file.ToNetFile(tempAddress));
                fileCopyList[tempAddress] = realAddress;
            }

            // 构造加载器
            var installLoaders = new List<ModLoader.LoaderBase>();
            var finishedFileNames = new List<string>();
            installLoaders.Add(new LoaderDownload("下载新版资源文件", fileList)
                { ProgressWeight = ModList.Count() * 1.5d }); // 每个 Mod 需要 1.5s
            installLoaders.Add(new ModLoader.LoaderTask<int, int>("替换旧版资源文件", _ =>
            {
                try
                {
                    foreach (var Entry in ModList)
                        if (File.Exists(Entry.path))
                            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(Entry.path, UIOption.AllDialogs,
                                RecycleOption.SendToRecycleBin);
                        else
                            ModBase.Log($"[CompUpdate] 未找到更新前的资源文件，跳过对它的删除：{Entry.path}", ModBase.LogLevel.Debug);

                    foreach (var Entry in fileCopyList)
                    {
                        if (File.Exists(Entry.Value))
                        {
                            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(Entry.Value, UIOption.AllDialogs,
                                RecycleOption.SendToRecycleBin);
                            ModBase.Log($"[Mod] 更新后的资源文件已存在，将会把它放入回收站：{Entry.Value}", ModBase.LogLevel.Debug);
                        }

                        if (Directory.Exists(ModBase.GetPathFromFullPath(Entry.Value)))
                        {
                            File.Move(Entry.Key, Entry.Value);
                            finishedFileNames.Add(ModBase.GetFileNameFromPath(Entry.Value));
                        }
                        else
                        {
                            ModBase.Log($"[Mod] 更新后的目标文件夹已被删除：{Entry.Value}", ModBase.LogLevel.Debug);
                        }
                    }
                }
                catch (OperationCanceledException ex)
                {
                    ModBase.Log(ex, "替换旧版资源文件时被主动取消");
                }
            }));
            // 结束处理
            var loader =
                new ModLoader.LoaderCombo<IEnumerable<ModLocalComp.LocalCompFile>>(
                    "资源更新：" + PageInstanceLeft.instance.Name, installLoaders);
            var pathMods = PageInstanceLeft.instance.PathIndie +
                           (PageInstanceLeft.instance.Info.hasLabyMod
                               ? Path.Combine("labymod-neo", "fabric", PageInstanceLeft.instance.Info.vanillaName)
                               : "") + ModLocalComp.GetPathNameByCompType(currentCompType) + @"\";
            loader.OnStateChanged = _ =>
            {
                // 结果提示
                switch (loader.State)
                {
                    case ModBase.LoadState.Finished:
                    {
                        switch (finishedFileNames.Count)
                        {
                            case 0: // 一般是由于 Mod 文件被占用，然后玩家主动取消
                            {
                                ModBase.Log("[CompUpdate] 没有资源被成功更新");
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

                ModBase.Log($"[CompUpdate] 已从正在进行资源更新的文件夹列表移除：{pathMods}");
                updatingVersions.Remove(pathMods);
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
                        ModBase.Log(ex, "清理资源更新缓存失败");
                    }
                }, "Clean Comp Update Cache", ThreadPriority.BelowNormal);
            };
            // 启动加载器
            ModBase.Log($"[CompUpdate] 开始更新 {ModList.Count()} 个资源：{pathMods}");
            updatingVersions.Add(pathMods);
            loader.Start();
            ModLoader.LoaderTaskbarAdd(loader);
            ModMain.frmMain.BtnExtraDownload.ShowRefresh();
            ModMain.frmMain.BtnExtraDownload.Ribble();
            ReloadCompFileList(true);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "初始化资源更新失败");
        }
    }

    // 删除
    private void BtnSelectDelete_Click(object sender, ModBase.RouteEventArgs e)
    {
        DeleteMods(ModLocalComp.compResourceListLoader.output.Where(m => selectedMods.Contains(m.RawPath)));
        ChangeAllSelected(false);
    }

    private void DeleteMods(IEnumerable<ModLocalComp.LocalCompFile> ModList)
    {
        try
        {
            var isSuccessful = true;
            var isShiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
            // 确认需要删除的文件
            // 文件夹只需要删除自身
            ModList = ModList.SelectMany(Target =>
                {
                    if (Target.IsFolder) return new[] { Target.path };

                    if (Target.State == ModLocalComp.LocalCompFile.LocalFileStatus.Fine)
                        return new[]
                            { Target.path, Target.path + (File.Exists(Target.path + ".old") ? ".old" : ".disabled") };

                    return new[] { Target.path, Target.RawPath };
                }).Distinct()
                .Where(m => m.EndsWithF(@"\__FOLDER__", true)
                    ? Directory.Exists(m.Replace(@"\__FOLDER__", ""))
                    : File.Exists(m)).Select(m => new ModLocalComp.LocalCompFile(m)).ToList();
            // 实际删除文件
            foreach (var ModEntity in ModList)
            {
                // 删除
                try
                {
                    if (ModEntity.IsFolder)
                    {
                        // 删除文件夹
                        if (isShiftPressed)
                            Directory.Delete(ModEntity.ActualPath, true);
                        else
                            Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(ModEntity.ActualPath,
                                UIOption.AllDialogs, RecycleOption.SendToRecycleBin);
                    }
                    // 删除文件
                    else if (isShiftPressed)
                    {
                        File.Delete(ModEntity.path);
                    }
                    else
                    {
                        Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(ModEntity.path, UIOption.OnlyErrorDialogs,
                            RecycleOption.SendToRecycleBin);
                    }
                }
                catch (OperationCanceledException ex)
                {
                    ModBase.Log(ex, "删除资源被主动取消");
                    ReloadCompFileList(true);
                    return;
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, $"删除资源失败（{ModEntity.path}）", ModBase.LogLevel.Msgbox);
                    isSuccessful = false;
                }

                // 取消选中
                selectedMods.Remove(ModEntity.RawPath);
                // 更改 Loader 和 UI 中的列表
                ModLocalComp.compResourceListLoader.output.Remove(ModEntity);
                searchResult?.Remove(ModEntity);
                modItems.Remove(ModEntity.RawPath);
                var indexOfUi = PanList.Children.IndexOf(PanList.Children.OfType<MyLocalCompItem>()
                    .FirstOrDefault(i => i.Entry.Equals(ModEntity)));
                if (indexOfUi >= 0)
                    PanList.Children.RemoveAt(indexOfUi);
            }

            RefreshBars();
            if (!isSuccessful)
            {
                ModMain.Hint(Lang.Text("Instance.Resource.Delete.Failed"), ModMain.HintType.Critical);
                ReloadCompFileList(true);
            }
            else if (PanList.Children.Count == 0)
            {
                ReloadCompFileList(true); // 删除了全部项目
            }
            else
            {
                RefreshBars();
            }

            // 显示结果提示
            if (!isSuccessful)
                return;
            if (isShiftPressed)
            {
                if (ModList.Count() == 1)
                    ModMain.Hint(Lang.Text("Instance.Resource.Delete.PermanentSingle", ModList.Single().FileName), ModMain.HintType.Finish);
                else
                    ModMain.Hint(Lang.Text("Instance.Resource.Delete.PermanentMultiple", ModList.Count()), ModMain.HintType.Finish);
            }
            else if (ModList.Count() == 1)
            {
                ModMain.Hint(Lang.Text("Instance.Resource.Delete.RecycleSingle", ModList.Single().FileName), ModMain.HintType.Finish);
            }
            else
            {
                ModMain.Hint(Lang.Text("Instance.Resource.Delete.RecycleMultiple", ModList.Count()), ModMain.HintType.Finish);
            }
        }
        catch (OperationCanceledException ex)
        {
            ModBase.Log(ex, "删除资源被主动取消");
            ReloadCompFileList(true);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "删除资源出现未知错误", ModBase.LogLevel.Feedback);
            ReloadCompFileList(true);
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
            .Where(m => selectedMods.Contains(m.RawPath) && m.Comp is not null).Select(i => i.Comp).ToList();
        ModComp.CompFavorites.ShowMenu(selected, (UIElement)sender);
    }

    // 分享
    private void BtnSelectShare_Click(object sender, ModBase.RouteEventArgs e)
    {
        var shareList = ModLocalComp.compResourceListLoader.output
            .Where(m => selectedMods.Contains(m.RawPath) && m.Comp is not null).Select(i => i.Comp.id).ToHashSet();
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
            var modEntry = ((MyLocalCompItem)(sender is MyIconButton iconButton ? iconButton.Tag : sender)).Entry;
            // 判断该 LabyMod 是否支持安装 Fabric Mod
            var moddedLabyMod = PageInstanceLeft.instance.Info.hasLabyMod && PageInstanceLeft.instance.Modable;
            // 加载失败信息
            if (modEntry.State == ModLocalComp.LocalCompFile.LocalFileStatus.Unavailable)
            {
                ModMain.MyMsgBox(
                    Lang.Text("Instance.Resource.Item.Info.FailedMessage") + "\r\n" + "\r\n" + Lang.Text("Instance.Resource.Item.Info.DetailedError") +
                    modEntry.FileUnavailableReason.Message, Lang.Text("Instance.Resource.Item.Info.FailedTitle"));
                return;
            }

            if (modEntry.Comp is not null)
            {
                // 跳转到 Mod 下载页面
                ModMain.frmMain.PageChange(new FormMain.PageStackData
                {
                    page = FormMain.PageType.CompDetail,
                    additional = (modEntry.Comp, new List<string>(), PageInstanceLeft.instance.Info.vanillaName,
                        PageInstanceLeft.instance.Info.hasForge ? ModComp.CompLoaderType.Forge :
                        PageInstanceLeft.instance.Info.hasNeoForge ? ModComp.CompLoaderType.NeoForge :
                        PageInstanceLeft.instance.Info.hasFabric || moddedLabyMod ? ModComp.CompLoaderType.Fabric :
                        ModComp.CompLoaderType.Any,
                        currentCompType, null, null, null)
                });
            }
            else
            {
                // 对于原理图文件，使用异步加载避免UI卡顿
                if (modEntry.path.EndsWithF(".litematic", true) || modEntry.path.EndsWithF(".schem", true) ||
                    modEntry.path.EndsWithF(".schematic", true) || modEntry.path.EndsWithF(".nbt", true))
                {
                    ShowSchematicInfoAsync(modEntry);
                    return;
                }

                // 获取信息
                var contentLines = new List<string>();

                // 检查是否为文件夹
                if (modEntry.IsFolder)
                {
                    // 处理文件夹详情
                    var folderPath = modEntry.ActualPath;
                    if (Directory.Exists(folderPath))
                    {
                        var fileCount = 0;
                        try
                        {
                            // 根据当前资源类型计算文件数量
                            switch (currentCompType)
                            {
                                case ModComp.CompType.Schematic:
                                {
                                    fileCount = new DirectoryInfo(folderPath)
                                        .EnumerateFiles("*", SearchOption.AllDirectories).Where(f =>
                                            ModLocalComp.LocalCompFile.IsCompFile(f.FullName,
                                                ModComp.CompType.Schematic)).Count();
                                    break;
                                }
                                case ModComp.CompType.Mod:
                                {
                                    fileCount = new DirectoryInfo(folderPath)
                                        .EnumerateFiles("*.jar", SearchOption.AllDirectories).Count();
                                    break;
                                }
                                case ModComp.CompType.ResourcePack:
                                {
                                    fileCount = new DirectoryInfo(folderPath)
                                        .EnumerateFiles("*.zip", SearchOption.AllDirectories).Count();
                                    break;
                                }
                                case ModComp.CompType.Shader:
                                {
                                    fileCount = new DirectoryInfo(folderPath)
                                        .EnumerateFiles("*.zip", SearchOption.AllDirectories).Count();
                                    break;
                                }

                                default:
                                {
                                    fileCount = new DirectoryInfo(folderPath)
                                        .EnumerateFiles("*", SearchOption.AllDirectories).Count();
                                    break;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            fileCount = 0;
                        }

                        if (fileCount == 0)
                            contentLines.Add(Lang.Text("Instance.Resource.Item.Info.EmptyFolder") + "\r\n");
                        else if (fileCount == 1)
                            contentLines.Add(Lang.Text("Instance.Resource.Item.Info.ContainsOne") + "\r\n");
                        else
                            contentLines.Add(Lang.Text("Instance.Resource.Item.Info.ContainsMany", fileCount) + "\r\n");
                    }
                    else
                    {
                        contentLines.Add(Lang.Text("Instance.Resource.Item.Info.FolderNotFound") + "\r\n");
                    }

                    contentLines.Add(Lang.Text("Instance.Resource.Item.Info.Path", folderPath));
                }
                else
                {
                    // 处理普通文件详情
                    if (modEntry.Description is not null)
                        contentLines.Add(modEntry.Description + "\r\n");
                    if (modEntry.Authors is not null)
                        contentLines.Add(Lang.Text("Instance.Resource.Item.Info.Author", modEntry.Authors));
                    contentLines.Add(Lang.Text("Instance.Resource.Item.Info.File", modEntry.FileName, ModBase.GetString(GetModFileInfo(modEntry.path).Length)));
                    if (modEntry.Version is not null)
                        contentLines.Add(Lang.Text("Instance.Resource.Item.Info.Version", modEntry.Version));

                    // 原理图文件的详情信息已通过异步方法处理
                }

                // 只有普通文件才显示调试信息
                if (!modEntry.IsFolder)
                {
                    var debugInfo = new List<string>();
                    if (modEntry.ModId is not null) debugInfo.Add(Lang.Text("Instance.Resource.Item.Info.ModId", modEntry.ModId));
                    if (modEntry.Dependencies.Any())
                    {
                        debugInfo.Add(Lang.Text("Instance.Resource.Item.Info.Dependency"));
                        foreach (var Dep in modEntry.Dependencies)
                            debugInfo.Add(" - " + (Dep.Value is null
                                ? Dep.Key
                                : Lang.Text("Instance.Resource.Item.Info.DependencyVersion", Dep.Key, Dep.Value)));
                    }

                    if (debugInfo.Any())
                    {
                        contentLines.Add("");
                        contentLines.AddRange(debugInfo);
                    }
                }

                // 显示详情信息
                if (modEntry.IsFolder)
                {
                    // 文件夹只显示基本信息，不提供搜索功能
                    ModMain.MyMsgBox(contentLines.Join("\r\n"), modEntry.Name, Lang.Text("Instance.Resource.Item.Info.Return"));
                }
                else
                {
                    // 获取用于搜索的 Mod 名称
                    var modOriginalName = modEntry.Name.Replace(" ", "+");
                    var modSearchName = modOriginalName.Substring(0, 1);
                    for (int i = 1, loopTo = modOriginalName.Count() - 1; i <= loopTo; i++)
                    {
                        var isLastLower = modOriginalName[i - 1].ToString().ToLower()
                            .Equals(modOriginalName[i - 1].ToString());
                        var isCurrentLower = modOriginalName[i].ToString().ToLower()
                            .Equals(modOriginalName[i].ToString());
                        if (isLastLower && !isCurrentLower)
                            // 上一个字母为小写，这一个字母为大写
                            modSearchName += "+";
                        modSearchName += modOriginalName[i].ToString();
                    }

                    modSearchName = modSearchName.Replace("++", "+").Replace("pti+Fine", "ptiFine");
                    // 显示
                    if (currentCompType == ModComp.CompType.Schematic)
                    {
                        // 投影原理图文件不显示百科搜索选项
                        if (modEntry.Url is null)
                            ModMain.MyMsgBox(contentLines.Join("\r\n"), modEntry.Name, Lang.Text("Instance.Resource.Item.Info.Return"));
                        else if (ModMain.MyMsgBox(contentLines.Join("\r\n"), modEntry.Name, Lang.Text("Instance.Resource.Item.Info.OpenWebsite"), Lang.Text("Instance.Resource.Item.Info.Return")) ==
                                 1) ModBase.OpenWebsite(modEntry.Url);
                    }
                    // 其他资源类型保留百科搜索功能
                    else if (modEntry.Url is null)
                    {
                        if (ModMain.MyMsgBox(contentLines.Join("\r\n"), modEntry.Name, Lang.Text("Instance.Resource.Item.Info.McMod"), Lang.Text("Instance.Resource.Item.Info.Return")) == 1)
                            ModBase.OpenWebsite("https://www.mcmod.cn/s?key=" + modSearchName + "&site=all&filter=0");
                    }
                    else
                    {
                        switch (ModMain.MyMsgBox(contentLines.Join("\r\n"), modEntry.Name, Lang.Text("Instance.Resource.Item.Info.OpenWebsite"), Lang.Text("Instance.Resource.Item.Info.McMod"),
                                    Lang.Text("Instance.Resource.Item.Info.Return")))
                        {
                            case 1:
                            {
                                ModBase.OpenWebsite(modEntry.Url);
                                break;
                            }
                            case 2:
                            {
                                ModBase.OpenWebsite(
                                    "https://www.mcmod.cn/s?key=" + modSearchName + "&site=all&filter=0");
                                break;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "获取资源详情失败", ModBase.LogLevel.Feedback);
        }
    }

    // 打开文件所在的位置
    public void Open_Click(MyIconButton sender, EventArgs e)
    {
        try
        {
            var listItem = (MyLocalCompItem)sender.Tag;
            // 对于文件夹使用实际路径，对于文件使用原路径
            var targetPath = listItem.Entry.IsFolder ? listItem.Entry.ActualPath : listItem.Entry.path;
            ModBase.OpenExplorer(targetPath);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "打开资源文件位置失败", ModBase.LogLevel.Feedback);
        }
    }

    // 删除
    public void Delete_Click(MyIconButton sender, EventArgs e)
    {
        var listItem = (MyLocalCompItem)sender.Tag;
        DeleteMods(new[] { listItem.Entry });
    }

    // 启用 / 禁用
    public void ED_Click(MyIconButton sender, EventArgs e)
    {
        var listItem = (MyLocalCompItem)sender.Tag;
        EDMods(new[] { listItem.Entry }, listItem.Entry.State == ModLocalComp.LocalCompFile.LocalFileStatus.Disabled);
    }

    /// <summary>
    ///     异步显示原理图详情信息，避免UI卡顿
    /// </summary>
    private void ShowSchematicInfoAsync(ModLocalComp.LocalCompFile ModEntry)
    {
        // 显示加载提示
        ModMain.Hint(Lang.Text("Instance.Resource.Item.Info.LoadingDetail"));

        // 在后台线程中加载NBT数据
        // 确保 NBT 数据已加载

        // 在 UI 线程中显示详情
        // 构建详情信息


        // 根据文件类型显示详细信息

        // 显示调试信息

        // 显示详情对话框


        // 记录错误日志但不显示错误提示，因为通用的文件状态检查已经处理了
        ModBase.RunInNewThread(() =>
        {
            try
            {
                ModEntry.LoadNbtDataIfNeeded();
                ModBase.RunInUi(() =>
                {
                    try
                    {
                        var contentLines = new List<string>();
                        if (ModEntry.Description is not null) contentLines.Add(ModEntry.Description + "\r\n");
                        if (ModEntry.Authors is not null) contentLines.Add(Lang.Text("Instance.Resource.Item.Info.Author", ModEntry.Authors));
                        contentLines.Add(Lang.Text("Instance.Resource.Item.Info.File", ModEntry.FileName, ModBase.GetString(GetModFileInfo(ModEntry.path).Length)));
                        if (ModEntry.Version is not null) contentLines.Add(Lang.Text("Instance.Resource.Item.Info.Version", ModEntry.Version));
                        if (ModEntry.path.EndsWithF(".litematic", true))
                            ShowLitematicDetails(contentLines, ModEntry);
                        else if (ModEntry.path.EndsWithF(".schem", true))
                            ShowSchemDetails(contentLines, ModEntry);
                        else if (ModEntry.path.EndsWithF(".schematic", true))
                            ShowSchematicDetails(contentLines, ModEntry);
                        else if (ModEntry.path.EndsWithF(".nbt", true)) ShowNbtDetails(contentLines, ModEntry);
                        ShowDebugInfo(contentLines, ModEntry);
                        ShowSchematicDialog(contentLines, ModEntry);
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, "显示原理图详情失败", ModBase.LogLevel.Feedback);
                    }
                });
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "加载原理图 NBT 数据失败", ModBase.LogLevel.Feedback);
            }
        });
    }

    #region 原理图文件详细信息显示

    /// <summary>
    ///     显示 Litematic 文件的详细信息
    /// </summary>
    private void ShowLitematicDetails(List<string> ContentLines, ModLocalComp.LocalCompFile ModEntry)
    {
        ContentLines.Add("");
        ContentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.DetailInfo"));

        // 显示原始名称（从 NBT Metadata/Name 读取）
        if (ModEntry.LitematicOriginalName is not null) ContentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.OriginalName") + ModEntry.LitematicOriginalName);

        // 显示版本信息
        if (ModEntry.LitematicVersion.HasValue) ContentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.Version") + ModEntry.LitematicVersion.Value);

        // 显示尺寸信息
        if (ModEntry.LitematicEnclosingSize is not null) ContentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.EnclosingSize") + ModEntry.LitematicEnclosingSize);

        // 显示方块和体积统计
        if (ModEntry.LitematicTotalBlocks.HasValue)
            ContentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.TotalBlocks") + Lang.Number(ModEntry.LitematicTotalBlocks.Value, "N0"));

        if (ModEntry.LitematicTotalVolume.HasValue)
            ContentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.TotalVolume") + Lang.Number(ModEntry.LitematicTotalVolume.Value, "N0"));

        // 显示区域数量
        if (ModEntry.LitematicRegionCount.HasValue) ContentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.RegionCount") + ModEntry.LitematicRegionCount.Value);

        // 显示时间信息
        if (ModEntry.LitematicTimeCreated.HasValue)
            try
            {
                var createdTime = DateTimeOffset.FromUnixTimeMilliseconds(ModEntry.LitematicTimeCreated.Value)
                    .ToLocalTime().DateTime;
                ContentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.CreatedTime") + Lang.Date(createdTime, "G"));
            }
            catch
            {
                ContentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.CreatedTime") + ModEntry.LitematicTimeCreated.Value);
            }

        if (ModEntry.LitematicTimeModified.HasValue)
            try
            {
                var modifiedTime = DateTimeOffset.FromUnixTimeMilliseconds(ModEntry.LitematicTimeModified.Value)
                    .ToLocalTime().DateTime;
                ContentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.ModifiedTime") + Lang.Date(modifiedTime, "G"));
            }
            catch
            {
                ContentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.ModifiedTime") + ModEntry.LitematicTimeModified.Value);
            }
    }

    /// <summary>
    ///     显示 Schem 文件的详细信息
    /// </summary>
    private void ShowSchemDetails(List<string> ContentLines, ModLocalComp.LocalCompFile ModEntry)
    {
        ContentLines.Add("");
        ContentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.DetailInfo"));

        // 显示原始名称（从 NBT Metadata/Name 读取）
        if (ModEntry.SchemOriginalName is not null) ContentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.OriginalName") + ModEntry.SchemOriginalName);

        // 显示版本信息
        if (ModEntry.StructureGameVersion is not null) ContentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.GameVersion") + ModEntry.StructureGameVersion);

        if (ModEntry.SpongeVersion.HasValue) ContentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.SpongeVersion") + ModEntry.SpongeVersion.Value);

        if (ModEntry.StructureDataVersion.HasValue) ContentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.DataVersion") + ModEntry.StructureDataVersion.Value);

        // 显示尺寸信息
        if (ModEntry.LitematicEnclosingSize is not null) ContentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.EnclosingDimensions") + ModEntry.LitematicEnclosingSize);

        // 显示方块和体积统计
        if (ModEntry.LitematicTotalBlocks.HasValue)
            ContentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.TotalBlocks") + Lang.Number(ModEntry.LitematicTotalBlocks.Value, "N0"));

        if (ModEntry.LitematicTotalVolume.HasValue)
            ContentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.TotalVolume") + Lang.Number(ModEntry.LitematicTotalVolume.Value, "N0"));

        // 显示区域数量
        if (ModEntry.LitematicRegionCount.HasValue) ContentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.RegionCount") + ModEntry.LitematicRegionCount.Value);

        ContentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.FileType",
            Lang.Text("Instance.Resource.Item.Schematic.FileType.Sponge")));
    }

    /// <summary>
    ///     显示 Schematic 文件的详细信息
    /// </summary>
    private void ShowSchematicDetails(List<string> ContentLines, ModLocalComp.LocalCompFile ModEntry)
    {
        ContentLines.Add("");
        ContentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.DetailInfo"));

        // 显示尺寸信息
        if (ModEntry.LitematicEnclosingSize is not null) ContentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.Size") + ModEntry.LitematicEnclosingSize);

        // 显示方块和体积统计
        if (ModEntry.LitematicTotalBlocks.HasValue)
            ContentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.TotalBlocks") + Lang.Number(ModEntry.LitematicTotalBlocks.Value, "N0"));

        if (ModEntry.LitematicTotalVolume.HasValue)
            ContentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.TotalVolume") + Lang.Number(ModEntry.LitematicTotalVolume.Value, "N0"));

        ContentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.FileType",
            Lang.Text("Instance.Resource.Item.Schematic.FileType.Mcedit")));
    }

    /// <summary>
    ///     显示 NBT 结构文件的详细信息
    /// </summary>
    private void ShowNbtDetails(List<string> ContentLines, ModLocalComp.LocalCompFile ModEntry)
    {
        ContentLines.Add("");
        ContentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.DetailInfo"));

        // 显示作者信息
        if (ModEntry.StructureAuthor is not null) ContentLines.Add(Lang.Text("Instance.Resource.Item.Info.Author", ModEntry.StructureAuthor));

        // 显示版本信息
        if (ModEntry.StructureGameVersion is not null) ContentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.GameVersion") + ModEntry.StructureGameVersion);

        if (ModEntry.StructureDataVersion.HasValue) ContentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.DataVersion") + ModEntry.StructureDataVersion.Value);

        // 显示尺寸信息
        if (ModEntry.LitematicEnclosingSize is not null) ContentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.EnclosingDimensions") + ModEntry.LitematicEnclosingSize);

        // 显示方块和体积统计
        if (ModEntry.LitematicTotalBlocks.HasValue)
            ContentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.TotalBlocks") + Lang.Number(ModEntry.LitematicTotalBlocks.Value, "N0"));

        if (ModEntry.LitematicTotalVolume.HasValue)
            ContentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.TotalVolume") + Lang.Number(ModEntry.LitematicTotalVolume.Value, "N0"));

        // 显示区域数量
        if (ModEntry.LitematicRegionCount.HasValue) ContentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.RegionCount") + ModEntry.LitematicRegionCount.Value);

        ContentLines.Add(Lang.Text("Instance.Resource.Item.Schematic.FileType",
            Lang.Text("Instance.Resource.Item.Schematic.FileType.Nbt")));
    }

    #endregion

    private void ShowDebugInfo(List<string> ContentLines, ModLocalComp.LocalCompFile ModEntry)
    {
        var debugInfo = new List<string>();
        if (ModEntry.ModId is not null) debugInfo.Add(Lang.Text("Instance.Resource.Item.Info.ModId", ModEntry.ModId));
        if (ModEntry.Dependencies.Any())
        {
            debugInfo.Add(Lang.Text("Instance.Resource.Item.Info.Dependency"));
            foreach (var Dep in ModEntry.Dependencies)
                debugInfo.Add(" - " + Dep.Key + (Dep.Value is null
                    ? Dep.Key
                    : Lang.Text("Instance.Resource.Item.Info.DependencyVersion", Dep.Key, Dep.Value)));
        }

        if (debugInfo.Any())
        {
            ContentLines.Add("");
            ContentLines.AddRange(debugInfo);
        }
    }

    private void ShowSchematicDialog(List<string> ContentLines, ModLocalComp.LocalCompFile ModEntry)
    {
        // 投影原理图文件不显示百科搜索选项
        if (ModEntry.Url is null)
            ModMain.MyMsgBox(ContentLines.Join("\r\n"), ModEntry.Name, Lang.Text("Instance.Resource.Item.Info.Return"));
        else if (ModMain.MyMsgBox(ContentLines.Join("\r\n"), ModEntry.Name, Lang.Text("Instance.Resource.Item.Info.OpenWebsite"), Lang.Text("Instance.Resource.Item.Info.Return")) == 1)
            ModBase.OpenWebsite(ModEntry.Url);
    }

    #endregion

    #region 搜索

    public bool IsSearching => !string.IsNullOrWhiteSpace(SearchBox.Text);
    private List<ModLocalComp.LocalCompFile> searchResult;
    private CancellationTokenSource _cancelToken;

    public void SearchRun(object sender, EventArgs e)
    {
        var curToken = new CancellationTokenSource();
        var oldToken = Interlocked.Exchange(ref _cancelToken, curToken);
        oldToken?.Cancel();
        oldToken?.Dispose();

        // this exception is ignored
        Dispatcher.BeginInvoke(new Func<Task>(async () =>
        {
            try
            {
                await Task.Delay(350, curToken.Token);
                if (curToken.IsCancellationRequested) return;
                if (IsSearching)
                {
                    var searchText = SearchBox.Text;
                    searchResult = await Task.Run(() => GetSearchResult(searchText), curToken.Token);
                }

                if (curToken.IsCancellationRequested) return;
                RefreshUI();
            }
            catch (TaskCanceledException ignore)
            {
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "搜索过程中发生异常");
            }
        }));
    }

    private List<ModLocalComp.LocalCompFile> GetSearchResult(string query)
    {
        // 构造请求
        var queryList = new List<ModBase.SearchEntry<ModLocalComp.LocalCompFile>>();
        foreach (var Entry in ModLocalComp.compResourceListLoader.output.AsReadOnly())
        {
            var searchSource = new List<ModBase.SearchSource>();
            searchSource.Add(new ModBase.SearchSource(Entry.Name, 1d));
            searchSource.Add(new ModBase.SearchSource(Entry.FileName, 1d));
            if (Entry.Version is not null) searchSource.Add(new ModBase.SearchSource(Entry.Version, 0.2d));
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
        return ModBase.Search(queryList, query, 6, 0.35d).Select(r => r.item).ToList();
    }

    #endregion
}
