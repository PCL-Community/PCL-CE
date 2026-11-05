using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.IO;
using PCL.Core.UI;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.Navigation;
using PCL.Network;
using PCL.Network.Loaders;
using PCL.UI.Controls;
using PCL.UI.Seams;
using PCL.Views.Pages.Download;
using PCL.Views.Shell;

namespace PCL.Views.Pages.Instance;

/// <summary>
///     实例本地资源页：模组 / 资源包 / 光影 / 结构文件的列表与管理。
///     支持搜索、按状态筛选、排序、全选与多选批量启用/禁用/更新/删除、导出资源信息与进入内嵌依赖页。
///     加载由 <see cref="LocalComp.CompResourceListLoader" /> 驱动，页面实现
///     <see cref="ISeamCompLocalPage" /> 以便业务层查询加载状态和当前目录。
/// </summary>
public partial class PageInstanceCompResource
    : MyPageRight, IPageView, IRefreshable, ISeamCompLocalPage
{
    private readonly Comp.CompType _compType;
    private readonly List<LocalComp.LocalCompFile> _entries = [];
    private readonly HashSet<string> _selected = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<LocalComp.LocalCompFile> _visibleEntries = [];

    private string _currentFolderPath = string.Empty;
    private int _filter;
    private bool _isBuilding;
    private bool _loaderInitialized;
    private bool _refreshed;
    private int _sort = 1;

    public PageInstanceCompResource() : this(Comp.CompType.Mod)
    {
    }

    public PageInstanceCompResource(Comp.CompType compType)
    {
        _compType = compType;
        InitializeComponent();
        PanScroll = PanBack;
        SearchBox.HintText = Lang.Text("Instance.Resource.Search.Hint");
        RefreshSortText();
        SearchBox.TextChanged += (_, _) => BuildList();
        BtnManageBack.Click += (_, _) => GoBackToParentFolder();
        BtnManageOpen.Click += (_, _) => Basics.OpenPath(CurrentLoaderFolder);
        BtnManageInstall.Click += (_, _) => InstallFromFiles();
        BtnManageDownload.Click += (_, _) => DownloadNew();
        BtnManageDownload.IsVisible =
            HiddenFeatureState.ForceShow || !Config.Preference.Hide.PageDownload;
        BtnManageSelectAll.Click += (_, _) => ToggleSelectAll();
        BtnManageInfoExport.Click += (_, _) => _ = ExportInfoAsync();
        ToolTip.SetTip(BtnManageInfoExport, Lang.Text("Instance.Resource.ExportInfo.ToolTip"));
        BtnManageJarInJar.Click += (_, _) => OpenJarInJar();
        ToolTip.SetTip(
            BtnManageJarInJar,
            Lang.Text("Instance.Resource.Mod.JarInJar.Entry.ToolTip"));
        BtnHintBack.Click += (_, _) => GoBackToParentFolder();
        BtnHintInstall.Click += (_, _) => InstallFromFiles();
        BtnHintDownload.Click += (_, _) => DownloadNew();
        BtnHintOpen.Click += (_, _) => Basics.OpenPath(CurrentLoaderFolder);
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
                    _filter = index;
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
            _selected.Clear();
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
                ToggleSelectAll(true);
                e.Handled = true;
            },
            RoutingStrategies.Tunnel);

        AttachedToVisualTree += (_, _) => FileDragHandler.FileDropped += OnFileDropped;
        DetachedFromVisualTree += (_, _) => FileDragHandler.FileDropped -= OnFileDropped;
        LoaderInit();
    }

    private string CompNameKey =>
        _compType switch
        {
            Comp.CompType.ResourcePack => "Instance.Left.ResourcePacks",
            Comp.CompType.Shader => "Instance.Left.Shaders",
            Comp.CompType.Schematic => "Instance.Left.Schematics",
            _ => "Instance.Left.Mod"
        };

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
        _selected.Clear();
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
        RefreshSortText();
        BuildList();
        if (PanSchematicEmpty.IsVisible)
            ShowSchematicEmptyPanel();
        else if (PanEmpty.IsVisible)
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
            () => GetLoaderData());
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
        if (LoaderRun(Loading.LoaderFolderRunType.RunOnUpdated))
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
            page._currentFolderPath = string.Empty;
            page.ReloadList();
            HintService.Show(Hints.Info(Lang.Text("Instance.Left.Refreshing"), false));
        }
    }

    private void BuildFromLoaderOutput()
    {
        try
        {
            _entries.Clear();
            var output = LocalComp.CompResourceListLoader.output;
            if (output is not null)
                _entries.AddRange(output);

            var instance = PageInstanceLeft.ResolveInstance();
            if (instance is null)
                return;

            BtnManageJarInJar.IsVisible = _compType == Comp.CompType.Mod && instance.Modable;

            var root = Path.GetFullPath(CurrentLoaderFolder.TrimEnd(Path.DirectorySeparatorChar));
            var shown = _entries
                .Where(entry =>
                {
                    var itemPath = entry.IsFolder ? entry.ActualPath : entry.path;
                    var parent = Directory.GetParent(itemPath)?.FullName;
                    return string.IsNullOrEmpty(_currentFolderPath)
                        ? string.Equals(parent, root, StringComparison.OrdinalIgnoreCase)
                        : string.Equals(
                            parent,
                            _currentFolderPath,
                            StringComparison.OrdinalIgnoreCase);
                })
                .ToList();

            if (shown.Count == 0)
            {
                if (_compType == Comp.CompType.Schematic
                    && !Directory.Exists(Path.Combine(instance.PathIndie, "schematics")))
                    ShowSchematicEmptyPanel();
                else
                    ShowEmptyPanel();
                return;
            }

            PanAllBack.IsVisible = true;
            PanEmpty.IsVisible = false;
            PanSchematicEmpty.IsVisible = false;
            PanListBack.Opacity = 1d;
            _sort = 1;
            BuildList(shown);
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
        PanAllBack.IsVisible = false;
        PanLoad.IsVisible = false;
        PanEmpty.IsVisible = true;
        PanSchematicEmpty.IsVisible = false;
        PanSelect.IsVisible = false;
        var inSubFolder = !string.IsNullOrEmpty(_currentFolderPath);
        TxtEmptyTitle.Text = Lang.Text(
            inSubFolder ? "Instance.Resource.EmptyFolder.Title" : "Instance.Resource.Empty.Title");
        TxtEmptyDescription.Text = Lang.Text(
            inSubFolder
                ? "Instance.Resource.EmptyFolder.Description"
                : "Instance.Resource.Empty.DescriptionWithDownload");
        BtnHintBack.IsVisible = inSubFolder;
        BtnManageBack.IsVisible = inSubFolder;
        BtnHintDownload.IsVisible = _compType != Comp.CompType.Schematic;
    }

    private void ShowSchematicEmptyPanel()
    {
        PanAllBack.IsVisible = false;
        PanLoad.IsVisible = false;
        PanEmpty.IsVisible = false;
        PanSchematicEmpty.IsVisible = true;
        PanSelect.IsVisible = false;
    }

    private void BuildList(List<LocalComp.LocalCompFile>? entries = null)
    {
        if (_isBuilding || PanList is null)
            return;
        _isBuilding = true;
        try
        {
            var searchText = SearchBox.Text?.Trim().ToLower() ?? string.Empty;
            var source = entries
                ??
                [
                    .. _entries
                        .Where(entry =>
                        {
                            var itemPath = entry.IsFolder ? entry.ActualPath : entry.path;
                            var parent = Directory.GetParent(itemPath)?.FullName;
                            return string.IsNullOrEmpty(_currentFolderPath)
                                || string.Equals(
                                    parent,
                                    _currentFolderPath,
                                    StringComparison.OrdinalIgnoreCase);
                        })
                ];

            var available = source
                .Select(entry => entry.RawPath)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            _selected.RemoveWhere(path => !available.Contains(path));

            var filtered = source
                .Where(MatchFilter)
                .Where(entry =>
                    string.IsNullOrEmpty(searchText)
                    || (entry.Name ?? "").Contains(searchText, StringComparison.InvariantCulture)
                    || (entry.Description ?? "").Contains(
                        searchText,
                        StringComparison.InvariantCulture)
                    || (entry.FileName ?? "").Contains(
                        searchText,
                        StringComparison.InvariantCulture))
                .ToList();
            filtered = SortEntries(filtered);

            _visibleEntries.Clear();
            _visibleEntries.AddRange(filtered);
            PanList.Children.Clear();
            foreach (var entry in filtered)
                PanList.Children.Add(CreateEntryItem(entry));

            PanAllBack.IsVisible = true;
            PanListBack.Opacity = 1d;
            RefreshFilterTexts();
            RefreshSortText();
            RefreshSelectBar();
        }
        finally
        {
            _isBuilding = false;
        }
    }

    private bool MatchFilter(LocalComp.LocalCompFile entry)
    {
        return _filter switch
        {
            1 => entry.State == LocalComp.LocalCompFile.LocalFileStatus.Fine,
            2 => entry.State == LocalComp.LocalCompFile.LocalFileStatus.Disabled,
            3 => entry.CanUpdate,
            4 => entry.State == LocalComp.LocalCompFile.LocalFileStatus.Unavailable,
            5 => IsDuplicate(entry),
            _ => true
        };
    }

    private bool IsDuplicate(LocalComp.LocalCompFile entry)
    {
        var name = entry.Name;
        if (string.IsNullOrEmpty(name))
            return false;
        return _entries.Any(other =>
            !ReferenceEquals(other, entry)
            && string.Equals(other.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private List<LocalComp.LocalCompFile> SortEntries(List<LocalComp.LocalCompFile> list)
    {
        list.Sort((a, b) =>
        {
            if (a.IsFolder != b.IsFolder)
                return a.IsFolder ? -1 : 1;
            var nameCompare = string.Compare(
                a.Name ?? a.FileName,
                b.Name ?? b.FileName,
                StringComparison.OrdinalIgnoreCase);
            switch (_sort)
            {
                case 0:
                    return string.Compare(
                        a.FileName,
                        b.FileName,
                        StringComparison.OrdinalIgnoreCase);
                case 2:
                    if (a.IsFolder && b.IsFolder)
                        return nameCompare;
                    return b.Tags.Count.CompareTo(a.Tags.Count);
                case 3:
                {
                    var aTime = SafeCreationTime(a);
                    var bTime = SafeCreationTime(b);
                    return aTime == bTime ? nameCompare : bTime.CompareTo(aTime);
                }
                case 4:
                {
                    if (a.IsFolder && b.IsFolder)
                        return nameCompare;
                    var aSize = SafeFileSize(a);
                    var bSize = SafeFileSize(b);
                    return aSize == bSize ? nameCompare : bSize.CompareTo(aSize);
                }
                default:
                    return nameCompare;
            }
        });
        return list;
    }

    private static DateTime SafeCreationTime(LocalComp.LocalCompFile entry)
    {
        try
        {
            return File.GetCreationTime(entry.path);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    private static long SafeFileSize(LocalComp.LocalCompFile entry)
    {
        try
        {
            return new FileInfo(entry.path).Length;
        }
        catch
        {
            return 0L;
        }
    }

    private void RefreshFilterTexts()
    {
        var all = _entries.Count;
        var enabled = _entries.Count(entry =>
            entry.State == LocalComp.LocalCompFile.LocalFileStatus.Fine);
        var disabled = _entries.Count(entry =>
            entry.State == LocalComp.LocalCompFile.LocalFileStatus.Disabled);
        var updatable = _entries.Count(entry => entry.CanUpdate);
        var error = _entries.Count(entry =>
            entry.State == LocalComp.LocalCompFile.LocalFileStatus.Unavailable);
        var duplicate = _entries.Count(IsDuplicate);
        BtnFilterAll.Text = _filter == 0 && string.IsNullOrWhiteSpace(SearchBox.Text)
            ? Lang.Text("Instance.Resource.Filter.AllWithCount", all)
            : Lang.Text("Instance.Resource.Filter.SearchResult");
        BtnFilterEnabled.Text = Lang.Text("Instance.Resource.Filter.EnabledWithCount", enabled);
        BtnFilterDisabled.Text = Lang.Text("Instance.Resource.Filter.DisabledWithCount", disabled);
        BtnFilterUpdatable.Text = Lang.Text(
            "Instance.Resource.Filter.UpdatableWithCount",
            updatable);
        BtnFilterError.Text = Lang.Text("Instance.Resource.Filter.ErrorWithCount", error);
        BtnFilterDuplicate.Text = Lang.Text(
            "Instance.Resource.Filter.DuplicateWithCount",
            duplicate);
        BtnFilterEnabled.IsVisible = enabled > 0 && enabled < all;
        BtnFilterDisabled.IsVisible = disabled > 0;
        BtnFilterUpdatable.IsVisible = updatable > 0;
        BtnFilterError.IsVisible = error > 0;
        BtnFilterDuplicate.IsVisible = duplicate > 0;
    }

    private void RefreshSortText()
    {
        var key = _sort switch
        {
            1 => "Instance.Resource.Sort.ResourceName",
            2 => "Instance.Resource.Sort.TagCount",
            3 => "Instance.Resource.Sort.AddTime",
            4 => "Instance.Resource.Sort.FileSize",
            _ => "Instance.Resource.Sort.FileName"
        };
        BtnSort.Text = Lang.Text("Instance.Resource.Sort.Text", Lang.Text(key));
    }

    private void ShowSortMenu()
    {
        var menu = new ContextMenu();
        var names = new[]
        {
            "Instance.Resource.Sort.FileName",
            "Instance.Resource.Sort.ResourceName",
            "Instance.Resource.Sort.TagCount",
            "Instance.Resource.Sort.AddTime",
            "Instance.Resource.Sort.FileSize"
        };
        for (var i = 0; i < names.Length; i++)
        {
            var index = i;
            var item = new MyMenuItem
            {
                Header = Lang.Text(names[i])
            };
            item.Click += (_, _) =>
            {
                _sort = index;
                BuildList();
            };
            menu.Items.Add(item);
        }

        menu.Open(BtnSort);
    }

    private MyLocalModItem CreateEntryItem(LocalComp.LocalCompFile entry)
    {
        var selected = !entry.IsFolder && _selected.Contains(entry.RawPath);
        var item = new MyLocalModItem(entry)
        {
            Checked = selected
        };

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
                    if (item.Checked)
                        _selected.Add(entry.RawPath);
                    else
                        _selected.Remove(entry.RawPath);
                    RefreshSelectBar();
                    lastClick = now;
                }
            }
            else
            {
                item.Checked = !item.Checked;
                if (item.Checked)
                    _selected.Add(entry.RawPath);
                else
                    _selected.Remove(entry.RawPath);
                RefreshSelectBar();
            }
        };

        item.UpdateRequested += item2 => { _ = UpdateEntriesAsync([entry]); };
        item.ChangelogRequested += _ => ShowUpdateLog(entry);
        item.InfoRequested += _ => ShowEntryInfo(entry);
        item.OpenLocationRequested += _ =>
            Basics.OpenPath(
                entry.IsFolder
                    ? entry.ActualPath
                    : Path.GetDirectoryName(entry.path) ?? CurrentLoaderFolder);
        item.DeleteRequested += item2 => { _ = DeleteEntryAsync(entry); };
        item.EnableDisableRequested += _ => ToggleEntry(entry);

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
            btnEd.Click += (_, _) => ToggleEntry(entry);
            buttons.Add(btnEd);
        }

        var btnDelete = new MyIconButton
        {
            SvgIcon = "lucide/trash-2",
            LogoScale = 1.0d
        };
        ToolTip.SetTip(btnDelete, Lang.Text("Common.Action.Delete"));
        btnDelete.Click += (_, _) => _ = DeleteEntryAsync(entry);
        buttons.Add(btnDelete);
        item.Buttons = buttons;
        return item;
    }

    #endregion

    #region 多选

    private void RefreshSelectBar()
    {
        var count = _selected.Count;
        PanSelect.IsVisible = count > 0;
        if (count == 0)
            return;

        LabSelect.Text = Lang.Text("Instance.Resource.SelectedCount", count);
        var selectedEntries = _entries.Where(entry => _selected.Contains(entry.RawPath)).ToList();
        BtnSelectUpdate.IsEnabled = selectedEntries.Any(entry => entry.CanUpdate);
        BtnSelectEnable.IsEnabled = selectedEntries.Any(entry =>
            entry.State == LocalComp.LocalCompFile.LocalFileStatus.Disabled);
        BtnSelectDisable.IsEnabled = selectedEntries.Any(entry =>
            entry.State == LocalComp.LocalCompFile.LocalFileStatus.Fine);
        BtnSelectFavorites.IsVisible = _compType is Comp.CompType.Mod
            or Comp.CompType.ResourcePack
            or Comp.CompType.Shader
            or Comp.CompType.Schematic;
        BtnSelectShare.IsVisible = BtnSelectFavorites.IsVisible;
        BtnSelectFavorites.IsEnabled = selectedEntries.Any(entry => entry.Comp is not null);
        BtnSelectShare.IsEnabled = selectedEntries.Any(entry => entry.Comp is not null);
        BtnSelectDelete.IsEnabled = selectedEntries.Count > 0;
    }

    private void ToggleSelectAll(bool? select = null)
    {
        var selectable = _visibleEntries.Where(entry => !entry.IsFolder).ToList();
        if (selectable.Count == 0)
            return;
        var allSelected = selectable.All(entry => _selected.Contains(entry.RawPath));
        var target = select ?? !allSelected;
        foreach (var entry in selectable)
            if (target)
                _selected.Add(entry.RawPath);
            else
                _selected.Remove(entry.RawPath);

        BuildList();
    }

    private void SetSelectedEnabled(bool enabled)
    {
        var targets = _entries
            .Where(entry => _selected.Contains(entry.RawPath))
            .Where(entry => enabled
                ? entry.State == LocalComp.LocalCompFile.LocalFileStatus.Disabled
                : entry.State == LocalComp.LocalCompFile.LocalFileStatus.Fine)
            .ToList();
        if (targets.Count == 0)
            return;
        foreach (var entry in targets)
            ToggleEntry(entry, false);
        _selected.Clear();
        ReloadList();
    }

    private void DeleteSelected()
    {
        var targets = _entries.Where(entry => _selected.Contains(entry.RawPath)).ToList();
        if (targets.Count == 0)
            return;
        _ = DeleteEntriesAsync(targets);
    }

    private async Task DeleteEntriesAsync(List<LocalComp.LocalCompFile> targets)
    {
        try
        {
            var dialog = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Common.Dialog.Warning"),
                    Content = Lang.Text(
                        "Event.Safety.ConfirmMessage",
                        $"{Lang.Text("Common.Action.Delete")} {targets.Count}"),
                    Buttons = DialogButtons.OkCancel,
                    IsWarn = true
                });
            if (dialog.Button != 1)
                return;

            var folder = string.Empty;
            foreach (var entry in targets)
                try
                {
                    folder = Path.GetDirectoryName(entry.IsFolder ? entry.ActualPath : entry.path)
                        ?? CurrentLoaderFolder;
                    DeleteEntryCore(entry);
                }
                catch (Exception ex)
                {
                    LauncherCommon.Log(ex, $"删除资源失败：{entry.path}", LauncherLogLevel.Feedback);
                }

            if (!string.IsNullOrEmpty(folder))
                CompLocalUpdateRegistry.End(folder);
            _selected.Clear();
            ReloadList();
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

    private static void DeleteEntryCore(LocalComp.LocalCompFile entry)
    {
        var folder = Path.GetDirectoryName(entry.IsFolder ? entry.ActualPath : entry.path);
        if (!string.IsNullOrEmpty(folder))
            CompLocalUpdateRegistry.Begin(folder);
        try
        {
            if (entry.IsFolder || Directory.Exists(entry.ActualPath))
            {
                LauncherCommon.DeleteDirectory(entry.ActualPath);
                return;
            }

            var recycle = RecycleBin.IsSupported(entry.path);
            var moved = false;
            if (recycle)
            {
                var outcome = RecycleBin.Move(entry.path, out _);
                moved = outcome == RecycleBin.Outcome.Moved;
            }

            if (!moved)
                File.Delete(entry.path);
        }
        finally
        {
            if (!string.IsNullOrEmpty(folder))
                CompLocalUpdateRegistry.End(folder);
        }
    }

    #endregion

    #region 更新

    private Task UpdateSelectedAsync()
    {
        var targets = _entries
            .Where(entry => _selected.Contains(entry.RawPath)
                && entry is { CanUpdate: true, UpdateFile: not null })
            .ToList();
        return UpdateEntriesAsync(targets);
    }

    private async Task UpdateEntriesAsync(List<LocalComp.LocalCompFile> targets)
    {
        targets =
        [
            .. targets
                .Where(entry => entry is { CanUpdate: true, UpdateFile: not null })
        ];
        if (targets.Count == 0)
            return;

        try
        {
            var warn = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Instance.Resource.Update.Warning.Title"),
                    Content = Lang.Text("Instance.Resource.Update.Warning.Message"),
                    Buttons = DialogButtons.Create(
                        "Instance.Resource.Update.Warning.Confirm",
                        "Common.Action.Cancel"),
                    IsWarn = true
                });
            if (!warn.IsConfirmed)
                return;

            var downloads = new List<DownloadFile>();
            var replacements = new List<(LocalComp.LocalCompFile Entry, string NewPath)>();
            foreach (var entry in targets)
            {
                var updateFile = entry.UpdateFile!;
                if (!updateFile.Available)
                    continue;
                var folder = Path.GetDirectoryName(entry.path) ?? CurrentLoaderFolder;
                var targetPath = Path.Combine(folder, updateFile.FileName);
                var checker = string.IsNullOrEmpty(updateFile.Hash)
                    ? new FileChecker(1024L)
                    : new FileChecker(hash: updateFile.Hash);
                downloads.Add(new DownloadFile(updateFile.DownloadUrls, targetPath, checker));
                replacements.Add((entry, targetPath));
            }

            if (downloads.Count == 0)
            {
                HintService.Show(
                    Hints.Error(Lang.Text("Instance.Resource.Update.Failed", string.Empty)));
                return;
            }

            var loader = new Loading.LoaderCombo<string>(
                Lang.Text("Instance.Resource.Update.Task.Title", _compType.ToString()),
                [
                    new LoaderDownload(
                        Lang.Text("Instance.Resource.Update.Task.DownloadFiles"),
                        downloads)
                    {
                        ProgressWeight = 1d
                    }
                ])
            {
                OnStateChanged = changed =>
                {
                    switch (changed.State)
                    {
                        case LoadState.Finished:
                            try
                            {
                                foreach (var (entry, newPath) in replacements)
                                {
                                    var oldPath = entry.path;
                                    if (string.Equals(
                                            oldPath,
                                            newPath,
                                            StringComparison.OrdinalIgnoreCase))
                                        continue;
                                    foreach (var variant in new[]
                                             {
                                                 oldPath,
                                                 $"{oldPath}.disabled",
                                                 $"{oldPath}.old"
                                             })
                                        if (File.Exists(variant)
                                            && !string.Equals(
                                                variant,
                                                newPath,
                                                StringComparison.OrdinalIgnoreCase))
                                            File.Delete(variant);
                                }

                                HintService.Show(
                                    Hints.Success(
                                        Lang.Text(
                                            "Instance.Resource.Update.SuccessMultiple",
                                            replacements.Count)));
                            }
                            catch (Exception ex)
                            {
                                LauncherCommon.Log(ex, "替换资源文件失败", LauncherLogLevel.Feedback);
                                HintService.Show(
                                    Hints.Error(
                                        Lang.Text("Instance.Resource.Update.Failed", ex.Message)));
                            }

                            _selected.Clear();
                            LauncherCommon.RunInUi(ReloadList);
                            break;

                        case LoadState.Failed:
                            HintService.Show(
                                Hints.Error(
                                    Lang.Text(
                                        "Instance.Resource.Update.Failed",
                                        changed.Error?.Message ?? string.Empty)));
                            break;

                        case LoadState.Aborted:
                            HintService.Show(
                                Hints.Info(Lang.Text("Instance.Resource.Update.Aborted")));
                            break;

                        case LoadState.Waiting:
                        case LoadState.Loading:
                            break;

                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            };
            loader.Start();
            Loading.LoaderTaskbarAdd(loader);
            LoadingUi.Current?.RefreshDownloadButton();
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

    private static void ShowUpdateLog(LocalComp.LocalCompFile entry)
    {
        try
        {
            if (entry.Comp is not null)
            {
                var isNumeric = long.TryParse(entry.Comp.Id, out _);
                var url = isNumeric
                    ? entry.ChangelogUrls.FirstOrDefault(item => item.Contains(
                        "curseforge.com",
                        StringComparison.OrdinalIgnoreCase))
                    : entry.ChangelogUrls.FirstOrDefault(item => item.Contains(
                        "modrinth.com",
                        StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(url))
                {
                    Basics.OpenPath(url);
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "打开更新日志失败", LauncherLogLevel.Developer);
        }

        HintService.Show(Hints.Info(Lang.Text("Instance.Resource.Item.OpenChangelogFailed")));
    }

    private void ShowFavoritesMenu()
    {
        var project = _entries
            .Where(entry => _selected.Contains(entry.RawPath))
            .Select(entry => entry.Comp)
            .FirstOrDefault(comp => comp is not null);
        if (project is null)
            return;
        CompFavoritesMenu.Show(project, BtnSelectFavorites);
    }

    private void ShareSelected()
    {
        try
        {
            var ids = _entries
                .Where(entry => _selected.Contains(entry.RawPath) && entry.Comp is not null)
                .Select(entry => entry.Comp!.Id)
                .ToHashSet();
            if (ids.Count == 0)
                return;
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is null)
                return;
            _ = clipboard.SetTextAsync(Comp.CompFavorites.GetShareCode(ids));
            HintService.Show(Hints.Success(Lang.Text("Common.Hint.Copied")));
            _selected.Clear();
            BuildList();
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Resource.Error.OperationFailed", ex);
        }
    }

    private static void ShowEntryInfo(LocalComp.LocalCompFile entry)
    {
        if (entry.Comp is not null)
        {
            PageRouter.Navigate(PageType.CompDetail, PageSubType.Default, entry.Comp);
            return;
        }

        var lines = new List<string>();
        if (entry.IsFolder)
        {
            lines.Add(Lang.Text("Instance.Resource.Item.Info.Path", entry.ActualPath));
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(entry.Authors))
                lines.Add(Lang.Text("Instance.Resource.Item.Info.Author", entry.Authors));
            lines.Add(
                Lang.Text(
                    "Instance.Resource.Item.Info.File",
                    entry.FileName,
                    InstancePageHelper.FormatSize(SafeFileSize(entry))));
            if (!string.IsNullOrWhiteSpace(entry.Version))
                lines.Add(Lang.Text("Instance.Resource.Item.Info.Version", entry.Version));
            if (!string.IsNullOrWhiteSpace(entry.Description))
                lines.Add(string.Empty + entry.Description);
            if (!string.IsNullOrWhiteSpace(entry.ModId))
                lines.Add(Lang.Text("Instance.Resource.Item.Info.ModId", entry.ModId));
            var dependencies = entry
                .Dependencies
                .Where(pair => !DependencyIds.IsPlatform(pair.Key))
                .ToList();
            if (dependencies.Count > 0)
            {
                lines.Add(Lang.Text("Instance.Resource.Item.Info.Dependency"));
                lines.AddRange(
                    dependencies.Select(dependency => $" - {(dependency.Value is null
                        ? dependency.Key
                        : Lang.Text(
                            "Instance.Resource.Item.Info.DependencyVersion",
                            dependency.Key,
                            dependency.Value))}"));
            }
        }

        if (lines.Count == 0)
            lines.Add(entry.Name);
        _ = DialogService.Instance.ShowAsync(
            new TextDialogRequest
            {
                Title = entry.Name,
                Content = string.Join("\r\n", lines),
                Buttons = DialogButtons.Ok
            });
    }

    private static void DownloadNewForSchematic()
    {
        PageRouter.Navigate(PageType.Download, PageSubType.DownloadMod);
    }

    #endregion

    #region 目录与安装

    private void EnterFolder(string folderPath)
    {
        _currentFolderPath = folderPath;
        BtnManageBack.IsVisible = true;
        ReloadList();
    }

    private void GoBackToParentFolder()
    {
        if (string.IsNullOrEmpty(_currentFolderPath))
            return;
        var parent = Directory.GetParent(_currentFolderPath)?.FullName;
        _currentFolderPath = parent ?? string.Empty;
        BtnManageBack.IsVisible = !string.IsNullOrEmpty(_currentFolderPath);
        ReloadList();
    }

    #endregion

    #region 管理

    private void ToggleEntry(LocalComp.LocalCompFile entry, bool reload = true)
    {
        try
        {
            var path = entry.path;
            switch (entry.State)
            {
                case LocalComp.LocalCompFile.LocalFileStatus.Fine:
                {
                    CompLocalUpdateRegistry.Begin(
                        Path.GetDirectoryName(path) ?? CurrentLoaderFolder);
                    try
                    {
                        File.Move(path, $"{path}.disabled");
                    }
                    finally
                    {
                        CompLocalUpdateRegistry.End(
                            Path.GetDirectoryName(path) ?? CurrentLoaderFolder);
                    }

                    break;
                }

                case LocalComp.LocalCompFile.LocalFileStatus.Disabled:
                {
                    var enabledPath = path.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)
                        ? path[..^".disabled".Length]
                        : path[..^".old".Length];
                    CompLocalUpdateRegistry.Begin(
                        Path.GetDirectoryName(path) ?? CurrentLoaderFolder);
                    try
                    {
                        File.Move(path, enabledPath);
                    }
                    finally
                    {
                        CompLocalUpdateRegistry.End(
                            Path.GetDirectoryName(path) ?? CurrentLoaderFolder);
                    }

                    break;
                }

                case LocalComp.LocalCompFile.LocalFileStatus.Unavailable:
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Instance.Resource.Error.OperationFailed"),
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Instance.Resource.Error.OperationFailed"));
        }

        if (!reload)
            return;
        if (entry.State is LocalComp.LocalCompFile.LocalFileStatus.Fine
            or LocalComp.LocalCompFile.LocalFileStatus.Disabled)
            _selected.Remove(entry.RawPath);
        ReloadList();
    }

    private async Task DeleteEntryAsync(LocalComp.LocalCompFile entry)
    {
        try
        {
            var dialog = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Common.Dialog.Warning"),
                    Content = Lang.Text(
                        "Event.Safety.ConfirmMessage",
                        $"{Lang.Text("Common.Action.Delete")} {entry.Name}"),
                    Buttons = DialogButtons.OkCancel,
                    IsWarn = true
                });
            if (dialog.Button != 1)
                return;

            LauncherCommon.RunInNewThread(
                () =>
                {
                    try
                    {
                        DeleteEntryCore(entry);
                    }
                    catch (Exception ex)
                    {
                        LauncherCommon.Log(
                            ex,
                            Lang.Text("Instance.Resource.Error.OperationFailed"),
                            LauncherLogLevel.Feedback,
                            userSummary: Lang.Text("Instance.Resource.Error.OperationFailed"));
                    }
                    finally
                    {
                        LauncherCommon.RunInUi(ReloadList);
                    }
                },
                $"Comp Delete {LauncherCommon.GetUuid()}",
                ThreadPriority.BelowNormal);
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

    private void InstallFromFiles()
    {
        try
        {
            var filter = _compType switch
            {
                Comp.CompType.Mod => "*.jar;*.zip;*.disabled;*.old",
                Comp.CompType.ResourcePack => "*.zip",
                Comp.CompType.Shader => "*.zip",
                _ => "*.schematic;*.litematic;*.nbt;*.zip"
            };
            var files = SystemDialogs.SelectFiles(
                filter,
                Lang.Text("Instance.Resource.InstallFromFiles"));
            if (files.Length == 0)
                return;

            var targetFolder = string.IsNullOrEmpty(_currentFolderPath)
                ? CurrentLoaderFolder
                : _currentFolderPath;
            Directory.CreateDirectory(targetFolder);
            CompLocalUpdateRegistry.Begin(targetFolder);
            LauncherCommon.RunInNewThread(
                () =>
                {
                    try
                    {
                        foreach (var file in files)
                        {
                            var name = Path.GetFileName(file);
                            var target = Path.Combine(targetFolder, name);
                            File.Copy(file, target, true);
                        }

                        var successText = files.Length > 1
                            ? Lang.Text(
                                "Instance.Resource.Install.SuccessMultiple",
                                files.Length,
                                Lang.Text(CompNameKey))
                            : Lang.Text(
                                "Instance.Resource.Install.SuccessSingle",
                                Path.GetFileName(files[0]));
                        HintService.Show(Hints.Success(successText));
                    }
                    catch (Exception ex)
                    {
                        LauncherCommon.Log(
                            ex,
                            Lang.Text("Instance.Resource.Error.OperationFailed"),
                            LauncherLogLevel.Feedback,
                            userSummary: Lang.Text("Instance.Resource.Error.OperationFailed"));
                    }
                    finally
                    {
                        CompLocalUpdateRegistry.End(targetFolder);
                        LauncherCommon.RunInUi(ReloadList);
                    }
                },
                $"Comp Install {LauncherCommon.GetUuid()}",
                ThreadPriority.BelowNormal);
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
        var subType = _compType switch
        {
            Comp.CompType.ResourcePack => PageSubType.DownloadResourcePack,
            Comp.CompType.Shader => PageSubType.DownloadShader,
            Comp.CompType.Schematic => PageSubType.DownloadWorld,
            _ => PageSubType.DownloadMod
        };
        PageRouter.Navigate(PageType.Download, subType);
    }

    private static void OpenJarInJar()
    {
        PageRouter.Navigate(
            new PageStackData(
                PageType.InstanceSetup,
                PageSubType.VersionModJarInJar,
                PageInstanceLeft.ResolveInstance()));
    }

    private async Task ExportInfoAsync()
    {
        try
        {
            var mode = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Instance.Resource.Export.Mode.Title"),
                    Content = Lang.Text("Instance.Resource.Export.Mode.Message"),
                    Buttons = DialogButtons.Create(
                        "Instance.Resource.Export.Mode.Txt",
                        "Instance.Resource.Export.Mode.Csv",
                        "Common.Action.Cancel")
                });
            if (!mode.IsConfirmed && mode.Button != 2 && mode.Button != 3)
                return;

            var isCsv = mode.Button == 2;
            var savePath = SystemDialogs.SelectSaveFile(
                Lang.Text("Instance.Resource.Export.SelectSaveLocation"),
                isCsv ? "resource.csv" : "resource.txt",
                Lang.Text("Instance.Resource.Export.FilesFilter"));
            if (string.IsNullOrWhiteSpace(savePath))
                return;

            var builder = new StringBuilder();
            if (isCsv)
                builder.AppendLine("文件名,资源名称,资源版本,此版本更新时间,Mod ID,对应平台工程 ID,文件大小（字节）,文件路径,内嵌模组");
            foreach (var entry in _entries)
                if (isCsv)
                {
                    var fields = new[]
                    {
                        entry.FileName,
                        entry.Comp?.TranslatedName ?? entry.Name ?? string.Empty,
                        entry.Version ?? string.Empty,
                        entry.CompFile?.ReleaseDate.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty,
                        entry.ModId ?? string.Empty,
                        entry.Comp?.Id ?? string.Empty,
                        SafeFileSize(entry).ToString(),
                        entry.path,
                        string.Join(";", FlattenEmbeddedNames(entry.EmbeddedMods))
                    };
                    builder.AppendLine(string.Join(',', fields.Select(CsvEscape)));
                }
                else
                {
                    builder.AppendLine(entry.FileName);
                    AppendEmbeddedForExport(builder, entry.EmbeddedMods, 1, false);
                }

            await File.WriteAllTextAsync(savePath, builder.ToString(), Encoding.UTF8);
            InstancePageHelper.OpenFolder(savePath);
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

    private static void AppendEmbeddedForExport(
        StringBuilder builder,
        List<LocalComp.LocalCompFile>? embedded,
        int depth,
        bool isCsv)
    {
        if (embedded is null)
            return;
        foreach (var entry in embedded)
        {
            builder.AppendLine(new string(' ', depth * 2) + entry.FileName);
            AppendEmbeddedForExport(builder, entry.EmbeddedMods, depth + 1, isCsv);
        }
    }

    private static IEnumerable<string> FlattenEmbeddedNames(List<LocalComp.LocalCompFile>? embedded)
    {
        if (embedded is null)
            yield break;
        foreach (var entry in embedded)
        {
            yield return entry.Name ?? entry.ModId ?? entry.FileName;
            foreach (var child in FlattenEmbeddedNames(entry.EmbeddedMods))
                yield return child;
        }
    }

    private static string CsvEscape(string? value)
    {
        value ??= string.Empty;
        return value.Contains(',')
            || value.Contains('"')
            || value.Contains('\r')
            || value.Contains('\n')
                ? $"\"{value.Replace("\"", "\"\"")}\""
                : value;
    }

    private bool OnFileDropped(IReadOnlyList<string> paths)
    {
        if (!ReferenceEquals(PageRouter.CurrentPage, this))
            return false;

        var accepted = paths
            .Where(path =>
                _compType switch
                {
                    Comp.CompType.Mod => LocalComp.LocalCompFile.IsModFile(path),
                    Comp.CompType.ResourcePack or Comp.CompType.Shader =>
                        path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase),
                    _ => LocalComp.LocalCompFile.IsCompFile(path, _compType)
                })
            .ToList();
        if (accepted.Count == 0)
            return false;

        var targetFolder = string.IsNullOrEmpty(_currentFolderPath)
            ? CurrentLoaderFolder
            : _currentFolderPath;
        try
        {
            Directory.CreateDirectory(targetFolder);
            foreach (var path in accepted)
                File.Copy(path, Path.Combine(targetFolder, Path.GetFileName(path)), true);
            HintService.Show(
                Hints.Success(
                    Lang.Text(
                        "Instance.Resource.Install.SuccessMultiple",
                        accepted.Count,
                        Lang.Text(CompNameKey))));
            ReloadList();
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Instance.Resource.Error.OperationFailed"),
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Instance.Resource.Error.OperationFailed"));
        }

        return true;
    }

    #endregion

    #region ISeamCompLocalPage

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