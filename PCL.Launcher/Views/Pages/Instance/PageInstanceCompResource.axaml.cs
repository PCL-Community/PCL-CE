using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.Navigation;
using PCL.UI.Controls;
using PCL.Views.Pages.Download;
using PCL.Views.Shell;

namespace PCL.Views.Pages.Instance;

/// <summary>
///     实例本地资源页：模组 / 资源包 / 光影 / 结构文件的列表与管理。
///     支持搜索、按状态筛选、排序、全选与多选批量启用/禁用/更新/删除与导出资源信息。
///     加载由 <see cref="LocalComp.CompResourceListLoader" /> 驱动，页面实现
///     <see cref="ICompLocalPage" /> 以便业务层查询加载状态和当前目录。
/// </summary>
public partial class PageInstanceCompResource
    : MyPageRight, IPageView, IRefreshable, ICompLocalPage
{
    private readonly Comp.CompType _compType;

    private string _currentFolderPath = string.Empty;
    private bool _isBuilding;
    private bool _loaderInitialized;
    private bool _refreshed;

    public PageInstanceCompResource() : this(Comp.CompType.Mod)
    {
    }

    public PageInstanceCompResource(Comp.CompType compType)
    {
        _compType = compType;
        InitializeComponent();
        DataContext = ViewModel;
        PanScroll = PanBack;
        SearchBox.HintText = Lang.Text("Instance.Resource.Search.Hint");
        ViewModel.Configure(compType, CurrentLoaderFolder);
        ViewModel.ReloadRequested += ReloadList;
        RefreshSortText();
        SearchBox.TextChanged += (_, _) =>
        {
            ViewModel.SetSearch(SearchBox.Text ?? string.Empty);
            BuildList();
        };
        BtnManageBack.Click += (_, _) => GoBackToParentFolder();
        BtnManageOpen.Click += (_, _) => OpenCurrentFolder();
        BtnManageInstall.Click += (_, _) => InstallFromFiles();
        BtnManageDownload.Click += (_, _) => DownloadNew();
        BtnManageDownload.IsVisible =
            HiddenFeatureState.ForceShow || !Config.Preference.Hide.PageDownload;
        BtnManageDownload.IsVisible &= ViewModel.ShowManageDownload;
        BtnHintDownload.IsVisible = ViewModel.ShowHintDownload;
        BtnManageSelectAll.Click += (_, _) => ToggleSelectAll();
        BtnManageInfoExport.Click += (_, _) => _ = ExportInfoAsync();
        BtnHintBack.Click += (_, _) => GoBackToParentFolder();
        BtnHintInstall.Click += (_, _) => InstallFromFiles();
        BtnHintDownload.Click += (_, _) => DownloadNew();
        BtnHintOpen.Click += (_, _) => OpenCurrentFolder();
        BtnSchematicDownloadMod.Click += (_, _) => DownloadNewForSchematic();
        BtnSchematicVersionSelect.Click += (_, _) =>
        {
            PageRouter.Navigate(PageType.Launch);
            PageRouter.Navigate(PageType.InstanceSelect);
        };

        foreach (var button in new[]
                 {
                     BtnFilterAll,
                     BtnFilterEnabled,
                     BtnFilterDisabled,
                     BtnFilterUpdatable,
                     BtnFilterError,
                     BtnFilterDuplicate
                 })
            button.Check += (sender, _) =>
            {
                if (sender.Tag is string tag && int.TryParse(tag, out var index))
                    ViewModel.ApplyFilter(index);
                BuildList();
            };
        BtnSort.Click += (_, _) => ShowSortMenu();

        BtnSelectUpdate.Click += (_, _) => _ = UpdateSelectedAsync();
        BtnSelectEnable.Click += (_, _) => SetSelectedEnabled(true);
        BtnSelectDisable.Click += (_, _) => SetSelectedEnabled(false);
        BtnSelectFavorites.Click += (_, _) => ShowFavoritesMenu();
        BtnSelectShare.Click += (_, _) => ShareSelected();
        BtnSelectDelete.Click += (_, _) => DeleteSelected();
        BtnSelectCancel.Click += (_, _) =>
        {
            ViewModel.ClearSelection();
            BuildList();
        };

        AddHandler(
            KeyDownEvent,
            (_, e) =>
            {
                if (e.Key != Key.A || !e.KeyModifiers.HasFlag(KeyModifiers.Control))
                    return;
                if (SearchBox.IsKeyboardFocusWithin && !string.IsNullOrEmpty(SearchBox.Text))
                    return;
                ViewModel.SetAllSelected(true);
                BuildList();
                e.Handled = true;
            },
            RoutingStrategies.Tunnel);

        AttachedToVisualTree += (_, _) => FileDragHandler.FileDropped += OnFileDropped;
        DetachedFromVisualTree += (_, _) => FileDragHandler.FileDropped -= OnFileDropped;
        LoaderInit();
    }

    private string CompNameKey => ViewModel.CompNameKey;

    public PageInstanceCompResourceViewModel ViewModel { get; } = new();

    private string CurrentLoaderFolder
    {
        get
        {
            var instance = PageInstanceLeft.ResolveInstance();
            if (instance is null)
                return string.Empty;
            var root = instance.PathIndie
                + (instance.Info.HasLabyMod
                    ? Path.Combine("labymod-neo", "fabric", instance.Info.VanillaName)
                    : "")
                + LocalComp.GetPathNameByCompType(_compType)
                + Path.DirectorySeparatorChar;
            return root;
        }
    }

    public override void OnNavigatedTo(object? parameter)
    {
        _currentFolderPath = string.Empty;
        ViewModel.ClearSelection();
        ViewModel.SetCurrentFolder(string.Empty);
        ViewModel.Configure(_compType, CurrentLoaderFolder);
        BtnManageBack.IsVisible = false;
        if (_refreshed)
        {
            ReloadList();
            return;
        }

        _refreshed = true;
        PanBack.ScrollToHome();
        ReloadList();
    }

    public override void OnNavigatedFrom()
    {
    }

    public void Refresh()
    {
        ForceReload(_compType);
    }

    protected override void RefreshLocalization()
    {
        SearchBox.HintText = Lang.Text("Instance.Resource.Search.Hint");
        ViewModel.Configure(_compType, CurrentLoaderFolder);
        BtnManageDownload.IsVisible =
            (HiddenFeatureState.ForceShow || !Config.Preference.Hide.PageDownload)
            && ViewModel.ShowManageDownload;
        BtnHintDownload.IsVisible = ViewModel.ShowHintDownload;
        RefreshSortText();
        BuildList();
        if (ViewModel.ShowSchematicEmpty)
            ShowSchematicEmptyPanel();
        else if (ViewModel.ShowEmpty)
            ShowEmptyPanel();
    }

    #region 加载

    private void LoaderInit()
    {
        if (_loaderInitialized)
            return;
        _loaderInitialized = true;
        PageLoaderInit(
            Load,
            PanLoad,
            PanAllBack,
            null,
            LocalComp.CompResourceListLoader,
            _ => BuildFromLoaderOutput(),
            GetLoaderData,
            false);
    }

    private LocalComp.CompLocalLoaderData GetLoaderData()
    {
        var instance = PageInstanceLeft.ResolveInstance();
        var loaders = new List<Comp.CompLoaderType>();
        switch (_compType)
        {
            case Comp.CompType.Mod:
                loaders = LocalComp.GetCurrentVersionModLoader();
                break;

            case Comp.CompType.ResourcePack:
            case Comp.CompType.Schematic:
                loaders = [Comp.CompLoaderType.Minecraft];
                break;

            case Comp.CompType.Shader:
                loaders =
                [
                    Comp.CompLoaderType.OptiFine,
                    Comp.CompLoaderType.Iris,
                    Comp.CompLoaderType.Vanilla,
                    Comp.CompLoaderType.Canvas
                ];
                break;

            case Comp.CompType.Any:
            case Comp.CompType.ModPack:
            case Comp.CompType.DataPack:
            case Comp.CompType.Plugin:
            case Comp.CompType.World:
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }

        return new LocalComp.CompLocalLoaderData
        {
            gameVersion = instance!,
            frm = this,
            loaders = loaders,
            compType = _compType,
            compPath = CurrentLoaderFolder
        };
    }

    public bool LoaderRun(Loading.LoaderFolderRunType type)
    {
        var loadPath = string.IsNullOrEmpty(_currentFolderPath)
            ? CurrentLoaderFolder
            : _currentFolderPath;
        return Loading.LoaderFolderRun(
            LocalComp.CompResourceListLoader,
            loadPath,
            type,
            loaderInput: GetLoaderData());
    }

    private void ReloadList()
    {
        if (!LoaderRun(Loading.LoaderFolderRunType.RunOnUpdated))
            return;

        // 只有真正开始新一轮加载时才收起旧内容，避免无变化刷新把页面清空。
        ViewModel.BeginLoading();
        LauncherCommon.Log($"[System] 已刷新 {_compType} 列表");
    }

    public static void ForceReload(Comp.CompType compType)
    {
        var subType = compType switch
        {
            Comp.CompType.ResourcePack => PageSubType.VersionResourcePack,
            Comp.CompType.Shader => PageSubType.VersionShader,
            Comp.CompType.Schematic => PageSubType.VersionSchematic,
            _ => PageSubType.VersionMod
        };
        if (PageRouter.Current.Page == PageType.InstanceSetup
            && PageRouter.Current.SubType == subType
            && PageRouter.CurrentPage is PageInstanceCompResource page)
        {
            Comp.compProjectCache.Clear();
            Comp.compFilesCache.Clear();
            try
            {
                File.Delete(Path.Combine(LauncherPaths.Temp, "Cache", "LocalComp.json"));
            }
            catch (Exception ex)
            {
                LauncherCommon.Log(ex, "强制刷新时清理本地工程信息缓存失败");
            }

            page._currentFolderPath = string.Empty;
            page.ViewModel.SetCurrentFolder(string.Empty);
            page.ViewModel.ClearSelection();
            page.ReloadList();
            HintService.Show(Hints.Info(Lang.Text("Instance.Left.Refreshing"), false));
        }
    }

    private void BuildFromLoaderOutput()
    {
        try
        {
            var output = LocalComp.CompResourceListLoader.output;
            ViewModel.Configure(_compType, CurrentLoaderFolder);
            ViewModel.SetCurrentFolder(_currentFolderPath);
            ViewModel.SetEntries(output ?? []);

            var instance = PageInstanceLeft.ResolveInstance();
            if (instance is null)
                return;

            if (!ViewModel.HasEntriesInCurrentFolder)
            {
                if (_compType == Comp.CompType.Schematic
                    && !Directory.Exists(Path.Combine(instance.PathIndie, "schematics")))
                    ShowSchematicEmptyPanel();
                else
                    ShowEmptyPanel();
                return;
            }

            ViewModel.ShowListPanel();
            PanListBack.Opacity = 1d;
            BuildList();
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Instance.Resource.Error.OperationFailed"),
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Instance.Resource.Error.OperationFailed"));
        }
    }

    private void ShowEmptyPanel()
    {
        PanLoad.IsVisible = false;
        PanSelect.IsVisible = false;
        ViewModel.ApplyEmptyState();
        TxtEmptyTitle.Text = ViewModel.EmptyTitle;
        TxtEmptyDescription.Text = ViewModel.EmptyDescription;
        BtnHintBack.IsVisible = ViewModel.ShowHintBack;
        BtnManageBack.IsVisible = ViewModel.ShowManageBack;
        BtnHintDownload.IsVisible = ViewModel.ShowHintDownload;
    }

    private void ShowSchematicEmptyPanel()
    {
        PanLoad.IsVisible = false;
        PanSelect.IsVisible = false;
        ViewModel.ApplySchematicEmptyState();
    }

    private void BuildList()
    {
        if (_isBuilding || PanList is null)
            return;
        _isBuilding = true;
        try
        {
            ViewModel.RebuildVisibleEntries();

            PanList.Children.Clear();
            foreach (var entry in ViewModel.VisibleEntries)
                PanList.Children.Add(CreateEntryItem(entry));

            PanListBack.Opacity = 1d;
            BtnManageBack.IsVisible = !string.IsNullOrEmpty(_currentFolderPath);
            RefreshFilterTexts();
            RefreshSortText();
            RefreshSelectBar();
        }
        finally
        {
            _isBuilding = false;
        }
    }

    private void RefreshFilterTexts()
    {
        BtnFilterAll.Text = ViewModel.FilterAllText;
        BtnFilterEnabled.Text = ViewModel.FilterEnabledText;
        BtnFilterDisabled.Text = ViewModel.FilterDisabledText;
        BtnFilterUpdatable.Text = ViewModel.FilterUpdatableText;
        BtnFilterError.Text = ViewModel.FilterErrorText;
        BtnFilterDuplicate.Text = ViewModel.FilterDuplicateText;
        BtnFilterEnabled.IsVisible = ViewModel.ShowFilterEnabled;
        BtnFilterDisabled.IsVisible = ViewModel.ShowFilterDisabled;
        BtnFilterUpdatable.IsVisible = ViewModel.ShowFilterUpdatable;
        BtnFilterError.IsVisible = ViewModel.ShowFilterError;
        BtnFilterDuplicate.IsVisible = ViewModel.ShowFilterDuplicate;
    }

    private void RefreshSortText()
    {
        BtnSort.Text = ViewModel.SortText;
    }

    private void ShowSortMenu()
    {
        var menu = new ContextMenu();
        var names = PageInstanceCompResourceViewModel.GetSortNames();
        for (var i = 0; i < names.Count; i++)
        {
            var index = i;
            var item = new MyMenuItem
            {
                Header = names[i]
            };
            item.Click += (_, _) =>
            {
                ViewModel.ApplySort(index);
                BuildList();
            };
            menu.Items.Add(item);
        }

        menu.Open(BtnSort);
    }

    private MyLocalModItem CreateEntryItem(LocalComp.LocalCompFile entry)
    {
        var selected = ViewModel.IsSelected(entry);
        var item = new MyLocalModItem(entry)
        {
            Checked = selected
        };
        entry.OnCompUpdate += _ => LauncherCommon.RunInUi(item.Refresh);

        var lastClick = DateTime.MinValue;
        item.PointerReleased += (_, e) =>
        {
            if (e.GetCurrentPoint(item).Properties.IsRightButtonPressed)
            {
                ShowEntryInfo(entry);
                return;
            }

            if (e.Source is Visual source
                && (source.FindAncestorOfType<MyIconButton>() is not null
                    || source.FindAncestorOfType<MyIconTextButton>() is not null))
                return;
            if (entry.IsFolder)
            {
                var now = DateTime.Now;
                if ((now - lastClick).TotalMilliseconds <= 300d)
                {
                    EnterFolder(entry.ActualPath);
                    lastClick = DateTime.MinValue;
                }
                else
                {
                    item.Checked = !item.Checked;
                    ViewModel.SetSelected(entry, item.Checked);
                    RefreshSelectBar();
                    lastClick = now;
                }
            }
            else
            {
                item.Checked = !item.Checked;
                ViewModel.SetSelected(entry, item.Checked);
                RefreshSelectBar();
            }
        };

        item.UpdateRequested += item2 => { _ = ViewModel.UpdateEntriesAsync([entry]); };
        item.ChangelogRequested += _ => PageInstanceCompResourceViewModel.ShowUpdateLog(entry);
        item.InfoRequested += _ => ShowEntryInfo(entry);
        item.OpenLocationRequested += _ =>
            Basics.OpenPath(
                entry.IsFolder
                    ? entry.ActualPath
                    : Path.GetDirectoryName(entry.path) ?? CurrentLoaderFolder);
        item.DeleteRequested += item2 => { _ = ViewModel.DeleteEntryAsync(entry); };
        item.EnableDisableRequested += _ => ViewModel.ToggleEntry(entry);

        var btnInfo = new MyIconButton
        {
            SvgIcon = "lucide/info",
            LogoScale = 1d
        };
        ToolTip.SetTip(btnInfo, Lang.Text("Instance.Saves.Details"));
        btnInfo.Click += (_, _) => ShowEntryInfo(entry);

        var btnOpen = new MyIconButton
        {
            SvgIcon = "lucide/folder-open",
            LogoScale = 1.05d
        };
        ToolTip.SetTip(btnOpen, Lang.Text("Instance.Saves.OpenFileLocation"));
        btnOpen.Click += (_, _) =>
            Basics.OpenPath(
                entry.IsFolder
                    ? entry.ActualPath
                    : Path.GetDirectoryName(entry.path) ?? CurrentLoaderFolder);

        var buttons = new List<MyIconButton>
        {
            btnInfo,
            btnOpen
        };
        if (_compType == Comp.CompType.Mod
            && !entry.IsFolder
            && entry.State != LocalComp.LocalCompFile.LocalFileStatus.Unavailable)
        {
            var fine = entry.State == LocalComp.LocalCompFile.LocalFileStatus.Fine;
            var btnEd = new MyIconButton
            {
                SvgIcon = fine ? "lucide/circle-minus" : "lucide/circle-check",
                LogoScale = 1.0d
            };
            ToolTip.SetTip(
                btnEd,
                Lang.Text(fine ? "Instance.Resource.Disable" : "Instance.Resource.Enable"));
            btnEd.Click += (_, _) => ViewModel.ToggleEntry(entry);
            buttons.Add(btnEd);
        }

        var btnDelete = new MyIconButton
        {
            SvgIcon = "lucide/trash-2",
            LogoScale = 1.0d
        };
        ToolTip.SetTip(btnDelete, Lang.Text("Common.Action.Delete"));
        btnDelete.Click += (_, _) => _ = ViewModel.DeleteEntryAsync(entry);
        buttons.Add(btnDelete);
        item.Buttons = buttons;
        return item;
    }

    #endregion

    #region 多选

    private void RefreshSelectBar()
    {
        PanSelect.IsVisible = ViewModel.IsSelectionVisible;
        if (!ViewModel.IsSelectionVisible)
            return;

        LabSelect.Text = ViewModel.SelectedText;
        BtnSelectUpdate.IsEnabled = ViewModel.CanUpdateSelected;
        BtnSelectEnable.IsEnabled = ViewModel.CanEnableSelected;
        BtnSelectDisable.IsEnabled = ViewModel.CanDisableSelected;
        BtnSelectFavorites.IsVisible = _compType is Comp.CompType.Mod
            or Comp.CompType.ResourcePack
            or Comp.CompType.Shader
            or Comp.CompType.Schematic;
        BtnSelectShare.IsVisible = BtnSelectFavorites.IsVisible;
        BtnSelectFavorites.IsEnabled = ViewModel.CanFavoriteAndShare;
        BtnSelectShare.IsEnabled = ViewModel.CanFavoriteAndShare;
        BtnSelectDelete.IsEnabled = ViewModel.SelectedPaths.Count > 0;
    }

    private void ToggleSelectAll()
    {
        ViewModel.ToggleSelectAll();
        BuildList();
    }

    private void SetSelectedEnabled(bool enabled)
    {
        ViewModel.SetSelectedEnabled(enabled);
    }

    private void DeleteSelected()
    {
        _ = ViewModel.DeleteSelectedAsync();
    }

    #endregion

    #region 更新

    private Task UpdateSelectedAsync()
    {
        return ViewModel.UpdateSelectedAsync();
    }

    private void ShowFavoritesMenu()
    {
        var projects = ViewModel.GetFavoriteProjects();
        if (projects.Count == 0)
            return;
        CompFavoritesMenu.Show(projects, BtnSelectFavorites);
    }

    private void ShareSelected()
    {
        try
        {
            var ids = ViewModel.GetShareIds();
            if (ids.Count == 0)
                return;
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is null)
                return;
            _ = clipboard.SetTextAsync(Comp.CompFavorites.GetShareCode(ids.ToHashSet()));
            HintService.Show(Hints.Success(Lang.Text("Common.Hint.Copied")));
            ViewModel.ClearSelection();
            BuildList();
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Resource.Error.OperationFailed", ex);
        }
    }

    private async void ShowEntryInfo(LocalComp.LocalCompFile entry)
    {
        if (entry.Comp is not null)
        {
            var instance = PageInstanceLeft.ResolveInstance();
            var loader = _compType == Comp.CompType.Mod
                ? LocalComp.GetCurrentVersionModLoader().FirstOrDefault()
                : Comp.CompLoaderType.Any;
            PageRouter.Navigate(
                PageType.CompDetail,
                PageSubType.Default,
                new CompDetailParameter(
                    entry.Comp,
                    _compType,
                    instance?.Info.VanillaName,
                    loader));
            return;
        }

        var lines = PageInstanceCompResourceViewModel.BuildInfoLines(entry).ToList();
        if (IsSchematicFile(entry))
            try
            {
                HintService.Show(
                    Hints.Info(Lang.Text("Instance.Resource.Item.Info.LoadingDetail")));
                await Task.Run(entry.LoadNbtDataIfNeeded);
                AppendSchematicInfoLines(lines, entry);
            }
            catch (Exception ex)
            {
                LauncherCommon.Log(ex, "加载原理图 NBT 数据失败");
            }

        await DialogService.Instance.ShowAsync(
            new TextDialogRequest
            {
                Title = entry.Name,
                Content = string.Join("\r\n", lines),
                Buttons = DialogButtons.Ok
            });
    }

    private static bool IsSchematicFile(LocalComp.LocalCompFile entry)
    {
        return !entry.IsFolder
            && (entry.path.EndsWith(".litematic", StringComparison.OrdinalIgnoreCase)
                || entry.path.EndsWith(".schem", StringComparison.OrdinalIgnoreCase)
                || entry.path.EndsWith(".schematic", StringComparison.OrdinalIgnoreCase)
                || entry.path.EndsWith(".nbt", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>把 WPF 原理图详情中的 NBT 字段追加到基础文件信息之后。</summary>
    private static void AppendSchematicInfoLines(
        List<string> lines,
        LocalComp.LocalCompFile entry)
    {
        lines.Add("");
        lines.Add(Lang.Text("Instance.Resource.Item.Schematic.DetailInfo"));

        if (entry.path.EndsWith(".litematic", StringComparison.OrdinalIgnoreCase))
        {
            if (entry.LitematicOriginalName is not null)
                lines.Add(
                    Lang.Text("Instance.Resource.Item.Schematic.OriginalName")
                    + entry.LitematicOriginalName);
            if (entry.LitematicVersion.HasValue)
                lines.Add(
                    Lang.Text("Instance.Resource.Item.Schematic.Version")
                    + entry.LitematicVersion.Value);
            if (entry.LitematicEnclosingSize is not null)
                lines.Add(
                    Lang.Text("Instance.Resource.Item.Schematic.EnclosingSize")
                    + entry.LitematicEnclosingSize);
            if (entry.LitematicTotalBlocks.HasValue)
                lines.Add(
                    Lang.Text("Instance.Resource.Item.Schematic.TotalBlocks")
                    + Lang.Number(entry.LitematicTotalBlocks.Value, "N0"));
            if (entry.LitematicTotalVolume.HasValue)
                lines.Add(
                    Lang.Text("Instance.Resource.Item.Schematic.TotalVolume")
                    + Lang.Number(entry.LitematicTotalVolume.Value, "N0"));
            if (entry.LitematicRegionCount.HasValue)
                lines.Add(
                    Lang.Text("Instance.Resource.Item.Schematic.RegionCount")
                    + entry.LitematicRegionCount.Value);
            if (entry.LitematicTimeCreated.HasValue)
                lines.Add(
                    Lang.Text("Instance.Resource.Item.Schematic.CreatedTime")
                    + FormatTimestamp(entry.LitematicTimeCreated.Value));
            if (entry.LitematicTimeModified.HasValue)
                lines.Add(
                    Lang.Text("Instance.Resource.Item.Schematic.ModifiedTime")
                    + FormatTimestamp(entry.LitematicTimeModified.Value));
            return;
        }

        if (entry.path.EndsWith(".schem", StringComparison.OrdinalIgnoreCase))
        {
            if (entry.SchemOriginalName is not null)
                lines.Add(
                    Lang.Text("Instance.Resource.Item.Schematic.OriginalName")
                    + entry.SchemOriginalName);
            if (entry.StructureGameVersion is not null)
                lines.Add(
                    Lang.Text("Instance.Resource.Item.Schematic.GameVersion")
                    + entry.StructureGameVersion);
            if (entry.SpongeVersion.HasValue)
                lines.Add(
                    Lang.Text("Instance.Resource.Item.Schematic.SpongeVersion")
                    + entry.SpongeVersion.Value);
            if (entry.StructureDataVersion.HasValue)
                lines.Add(
                    Lang.Text("Instance.Resource.Item.Schematic.DataVersion")
                    + entry.StructureDataVersion.Value);
        }
        else if (entry.path.EndsWith(".nbt", StringComparison.OrdinalIgnoreCase))
        {
            if (entry.StructureAuthor is not null)
                lines.Add(Lang.Text("Instance.Resource.Item.Info.Author", entry.StructureAuthor));
            if (entry.StructureGameVersion is not null)
                lines.Add(
                    Lang.Text("Instance.Resource.Item.Schematic.GameVersion")
                    + entry.StructureGameVersion);
            if (entry.StructureDataVersion.HasValue)
                lines.Add(
                    Lang.Text("Instance.Resource.Item.Schematic.DataVersion")
                    + entry.StructureDataVersion.Value);
        }

        if (entry.LitematicEnclosingSize is not null)
            lines.Add(
                Lang.Text("Instance.Resource.Item.Schematic.EnclosingDimensions")
                + entry.LitematicEnclosingSize);
        if (entry.LitematicTotalBlocks.HasValue)
            lines.Add(
                Lang.Text("Instance.Resource.Item.Schematic.TotalBlocks")
                + Lang.Number(entry.LitematicTotalBlocks.Value, "N0"));
        if (entry.LitematicTotalVolume.HasValue)
            lines.Add(
                Lang.Text("Instance.Resource.Item.Schematic.TotalVolume")
                + Lang.Number(entry.LitematicTotalVolume.Value, "N0"));
        if (entry.LitematicRegionCount.HasValue)
            lines.Add(
                Lang.Text("Instance.Resource.Item.Schematic.RegionCount")
                + entry.LitematicRegionCount.Value);

        var fileType = entry.path.EndsWith(".schem", StringComparison.OrdinalIgnoreCase)
            ? "Instance.Resource.Item.Schematic.FileType.Sponge"
            : entry.path.EndsWith(".nbt", StringComparison.OrdinalIgnoreCase)
                ? "Instance.Resource.Item.Schematic.FileType.Nbt"
                : "Instance.Resource.Item.Schematic.FileType.Mcedit";
        lines.Add(Lang.Text("Instance.Resource.Item.Schematic.FileType", Lang.Text(fileType)));
    }

    private static string FormatTimestamp(long unixMilliseconds)
    {
        try
        {
            return Lang.Date(
                DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds).ToLocalTime().DateTime);
        }
        catch
        {
            return unixMilliseconds.ToString();
        }
    }

    private static void DownloadNewForSchematic()
    {
        var instance = PageInstanceLeft.ResolveInstance();
        if (instance is not null)
        {
            var folder = Path.Combine(
                instance.PathIndie,
                LocalComp.GetPathNameByCompType(Comp.CompType.Mod));
            PageDownloadCompDetail.SetCachedFolder(Comp.CompType.Mod, folder);
            PageRouter.Navigate(
                PageType.Download,
                PageSubType.DownloadMod,
                new CompSearchContext(
                    instance.Info.VanillaName,
                    LocalComp.GetCurrentVersionModLoader().FirstOrDefault()));
            return;
        }

        PageRouter.Navigate(PageType.Download, PageSubType.DownloadMod);
    }

    #endregion

    #region 目录与安装

    private void OpenCurrentFolder()
    {
        var path = string.IsNullOrEmpty(_currentFolderPath)
            ? CurrentLoaderFolder
            : _currentFolderPath;
        if (string.IsNullOrEmpty(path))
            return;
        Directory.CreateDirectory(path);
        Basics.OpenPath(path);
    }

    private void EnterFolder(string folderPath)
    {
        _currentFolderPath = folderPath;
        BtnManageBack.IsVisible = true;
        ViewModel.SetCurrentFolder(folderPath);
        ReloadList();
    }

    private void GoBackToParentFolder()
    {
        if (string.IsNullOrEmpty(_currentFolderPath))
            return;
        var parent = Directory.GetParent(_currentFolderPath)?.FullName;
        _currentFolderPath = parent ?? string.Empty;
        BtnManageBack.IsVisible = !string.IsNullOrEmpty(_currentFolderPath);
        ViewModel.SetCurrentFolder(_currentFolderPath);
        ReloadList();
    }

    #endregion

    #region 管理

    private void InstallFromFiles()
    {
        try
        {
            var filter = _compType switch
            {
                Comp.CompType.Mod => "*.jar;*.litemod;*.disabled;*.old",
                Comp.CompType.ResourcePack => "*.zip",
                Comp.CompType.Shader => "*.zip",
                _ => "*.schematic;*.litematic;*.nbt;*.zip"
            };
            var files = SystemDialogs.SelectFiles(
                filter,
                Lang.Text("Instance.Resource.InstallFromFiles"));
            if (files.Length != 0)
                _ = ViewModel.InstallFilesAsync(files);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Instance.Resource.Error.OperationFailed"),
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Instance.Resource.Error.OperationFailed"));
        }
    }

    private void DownloadNew()
    {
        var instance = PageInstanceLeft.ResolveInstance();
        if (instance is not null)
        {
            var folder = Path.Combine(
                instance.PathIndie,
                LocalComp.GetPathNameByCompType(_compType));
            PageDownloadCompDetail.SetCachedFolder(_compType, folder);
        }

        var subType = _compType switch
        {
            Comp.CompType.ResourcePack => PageSubType.DownloadResourcePack,
            Comp.CompType.Shader => PageSubType.DownloadShader,
            Comp.CompType.Schematic => PageSubType.DownloadWorld,
            _ => PageSubType.DownloadMod
        };
        if (instance is null)
        {
            PageRouter.Navigate(PageType.Download, subType);
            return;
        }

        var loader = _compType == Comp.CompType.Mod
            ? LocalComp.GetCurrentVersionModLoader().FirstOrDefault()
            : Comp.CompLoaderType.Any;
        PageRouter.Navigate(
            PageType.Download,
            subType,
            new CompSearchContext(instance.Info.VanillaName, loader));
    }

    private Task ExportInfoAsync()
    {
        return ViewModel.ExportInfoAsync();
    }

    private bool OnFileDropped(IReadOnlyList<string> paths)
    {
        if (!ReferenceEquals(PageRouter.CurrentPage, this))
            return false;

        if (!ViewModel.AcceptDrop(paths, out var accepted))
            return false;

        _ = ViewModel.InstallFilesAsync(accepted);
        return true;
    }

    #endregion

    #region ICompLocalPage

    public bool LoadProgressVisible
    {
        get => Load.ShowProgress;
        set => Load.ShowProgress = value;
    }

    public void SetLoadText(string key)
    {
        Load.Text = Lang.Text(key);
    }

    public void UpdateOnly()
    {
        LoaderRun(Loading.LoaderFolderRunType.UpdateOnly);
    }

    public string? CurrentFolderPath =>
        string.IsNullOrEmpty(_currentFolderPath) ? null : _currentFolderPath;

    public void RefreshModList()
    {
        ReloadList();
    }

    #endregion
}