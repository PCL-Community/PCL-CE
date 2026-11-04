using System.Text;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI;
using PCL.Core.UI.Hints;
using PCL.Navigation;
using PCL.Network;
using PCL.Network.Loaders;
using PCL.UI.Controls;
using PCL.UI.Seams;
using PCL.Views.Pages;
using PCL.Views.Shell;

namespace PCL.Views.Pages.Instance;

/// <summary>
///     实例本地资源页：模组 / 资源包 / 光影 / 结构文件的列表与管理。
///     支持搜索、按状态筛选、排序、全选与多选批量启用/禁用/更新/删除、导出资源信息与进入内嵌依赖页。
///     加载由 <see cref="LocalComp.CompResourceListLoader" /> 驱动，页面实现
///     <see cref="ISeamCompLocalPage" /> 以便业务层查询加载状态和当前目录。
/// </summary>
public partial class PageInstanceCompResource : MyPageRight, IPageView, IRefreshable, ISeamCompLocalPage
{
    private readonly Comp.CompType _compType;
    private readonly List<LocalComp.LocalCompFile> _entries = [];
    private readonly List<LocalComp.LocalCompFile> _visibleEntries = [];
    private readonly HashSet<string> _selected = new(StringComparer.OrdinalIgnoreCase);

    private string _currentFolderPath = string.Empty;
    private bool _loaderInitialized;
    private bool _refreshed;
    private bool _isBuilding;
    private int _filter;
    private int _sort;

    public PageInstanceCompResource() : this(Comp.CompType.Mod)
    {
    }

    public PageInstanceCompResource(Comp.CompType compType)
    {
        _compType = compType;
        InitializeComponent();
        PanScroll = PanBack;
        SearchBox.HintText = Lang.Text("Instance.Resource.Search.Hint");
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
        ToolTip.SetTip(BtnManageJarInJar, Lang.Text("Instance.Resource.Mod.JarInJar.Entry.ToolTip"));
        BtnHintBack.Click += (_, _) => GoBackToParentFolder();
        BtnHintInstall.Click += (_, _) => InstallFromFiles();
        BtnHintOpen.Click += (_, _) => Basics.OpenPath(CurrentLoaderFolder);

        foreach (var button in new[]
                 {
                     BtnFilterAll, BtnFilterEnabled, BtnFilterDisabled, BtnFilterUpdatable,
                     BtnFilterError, BtnFilterDuplicate
                 })
            button.Check += (sender, _) =>
            {
                if (sender.Tag is string tag && int.TryParse(tag, out var index))
                    _filter = index;
                BuildList();
            };
        BtnSort.Click += (_, _) =>
        {
            _sort = (_sort + 1) % 5;
            BuildList();
        };

        BtnSelectUpdate.Click += (_, _) => UpdateSelected();
        BtnSelectEnable.Click += (_, _) => SetSelectedEnabled(true);
        BtnSelectDisable.Click += (_, _) => SetSelectedEnabled(false);
        BtnSelectDelete.Click += (_, _) => DeleteSelected();
        BtnSelectCancel.Click += (_, _) =>
        {
            _selected.Clear();
            BuildList();
        };

        AttachedToVisualTree += (_, _) => FileDragHandler.FileDropped += OnFileDropped;
        DetachedFromVisualTree += (_, _) => FileDragHandler.FileDropped -= OnFileDropped;
        LoaderInit();
    }

    private string CompNameKey => _compType switch
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

    protected override void RefreshLocalization()
    {
        SearchBox.HintText = Lang.Text("Instance.Resource.Search.Hint");
        BuildList();
    }

    public void Refresh()
    {
        PageInstanceCompResource.ForceReload(_compType);
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
            HintService.Show(Hints.Info(Lang.Text("Instance.Left.Refreshing"), log: false));
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
            var shown = _entries.Where(entry =>
            {
                var itemPath = entry.IsFolder ? entry.ActualPath : entry.path;
                var parent = Directory.GetParent(itemPath)?.FullName;
                return string.IsNullOrEmpty(_currentFolderPath)
                    ? string.Equals(parent, root, StringComparison.OrdinalIgnoreCase)
                    : string.Equals(parent, _currentFolderPath, StringComparison.OrdinalIgnoreCase);
            }).ToList();

            if (shown.Count == 0)
            {
                ShowEmptyPanel();
                return;
            }

            PanAllBack.IsVisible = true;
            PanEmpty.IsVisible = false;
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
    }

    private void BuildList(List<LocalComp.LocalCompFile>? entries = null)
    {
        if (_isBuilding || PanList is null)
            return;
        _isBuilding = true;
        try
        {
            var searchText = SearchBox.Text?.Trim().ToLower() ?? string.Empty;
            var source = entries ?? _entries.Where(entry =>
            {
                var itemPath = entry.IsFolder ? entry.ActualPath : entry.path;
                var parent = Directory.GetParent(itemPath)?.FullName;
                return string.IsNullOrEmpty(_currentFolderPath)
                    ? true
                    : string.Equals(parent, _currentFolderPath, StringComparison.OrdinalIgnoreCase);
            }).ToList();

            var available = source.Select(entry => entry.RawPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
            _selected.RemoveWhere(path => !available.Contains(path));

            var filtered = source
                .Where(entry => MatchFilter(entry))
                .Where(entry =>
                    string.IsNullOrEmpty(searchText)
                    || (entry.Name ?? "").ToLower().Contains(searchText)
                    || (entry.Description ?? "").ToLower().Contains(searchText)
                    || (entry.FileName ?? "").ToLower().Contains(searchText))
                .ToList();
            filtered = SortEntries(filtered);

            _visibleEntries.Clear();
            _visibleEntries.AddRange(filtered);
            PanList.Children.Clear();
            foreach (var entry in filtered)
                PanList.Children.Add(CreateEntryItem(entry));

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
        return _entries.Count(other =>
            !ReferenceEquals(other, entry)
            && string.Equals(other.Name, name, StringComparison.OrdinalIgnoreCase)) > 0;
    }

    private List<LocalComp.LocalCompFile> SortEntries(List<LocalComp.LocalCompFile> list)
    {
        return _sort switch
        {
            1 => list.OrderByDescending(entry => SafeCreationTime(entry)).ToList(),
            2 => list.OrderBy(entry => SafeFileSize(entry)).ToList(),
            3 => list.OrderBy(entry => entry.Name ?? entry.FileName, StringComparer.OrdinalIgnoreCase).ToList(),
            4 => list.OrderByDescending(entry => entry.Tags.Count).ToList(),
            _ => list.OrderBy(entry => entry.FileName, StringComparer.OrdinalIgnoreCase).ToList()
        };
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
        var enabled = _entries.Count(entry => entry.State == LocalComp.LocalCompFile.LocalFileStatus.Fine);
        var disabled = _entries.Count(entry => entry.State == LocalComp.LocalCompFile.LocalFileStatus.Disabled);
        var updatable = _entries.Count(entry => entry.CanUpdate);
        var error = _entries.Count(entry => entry.State == LocalComp.LocalCompFile.LocalFileStatus.Unavailable);
        var duplicate = _entries.Count(IsDuplicate);
        BtnFilterAll.Text = Lang.Text("Instance.Resource.Filter.AllWithCount", all);
        BtnFilterEnabled.Text = Lang.Text("Instance.Resource.Filter.EnabledWithCount", enabled);
        BtnFilterDisabled.Text = Lang.Text("Instance.Resource.Filter.DisabledWithCount", disabled);
        BtnFilterUpdatable.Text = Lang.Text("Instance.Resource.Filter.UpdatableWithCount", updatable);
        BtnFilterError.Text = Lang.Text("Instance.Resource.Filter.ErrorWithCount", error);
        BtnFilterDuplicate.Text = Lang.Text("Instance.Resource.Filter.DuplicateWithCount", duplicate);
    }

    private void RefreshSortText()
    {
        var key = _sort switch
        {
            1 => "Instance.Resource.Sort.AddTime",
            2 => "Instance.Resource.Sort.FileSize",
            3 => "Instance.Resource.Sort.ResourceName",
            4 => "Instance.Resource.Sort.TagCount",
            _ => "Instance.Resource.Sort.FileName"
        };
        BtnSort.Text = Lang.Text("Instance.Resource.Sort.Text", Lang.Text(key));
    }

    private MyListItem CreateEntryItem(LocalComp.LocalCompFile entry)
    {
        var selected = !entry.IsFolder && _selected.Contains(entry.RawPath);
        var item = new MyListItem
        {
            Title = entry.Name ?? string.Empty,
            Info = entry.Description
                   ?? (string.IsNullOrEmpty(entry.Version) ? string.Empty : $"{entry.Version}"),
            Logo = entry.GetLogo(),
            Type = entry.IsFolder ? MyListItem.CheckType.Clickable : MyListItem.CheckType.CheckBox,
            Checked = selected,
            tag = entry
        };
        if (entry.Tags.Count > 0)
            item.PlainTags = entry.Tags.Take(4);

        if (entry.IsFolder)
        {
            item.Click += (_, _) => EnterFolder(entry.ActualPath);
        }
        else
        {
            item.Check += (_, _) =>
            {
                if (item.Checked)
                    _selected.Add(entry.RawPath);
                else
                    _selected.Remove(entry.RawPath);
                RefreshSelectBar();
            };
        }

        var btnOpen = new MyIconButton
        {
            SvgIcon = "lucide/folder-open",
            LogoScale = 1.05d
        };
        ToolTip.SetTip(btnOpen, Lang.Text("Instance.Saves.OpenFileLocation"));
        btnOpen.Click += (_, _) =>
            Basics.OpenPath(entry.IsFolder ? entry.ActualPath : Path.GetDirectoryName(entry.path) ?? CurrentLoaderFolder);

        var buttons = new List<MyIconButton> { btnOpen };
        if (_compType == Comp.CompType.Mod && !entry.IsFolder
            && entry.State != LocalComp.LocalCompFile.LocalFileStatus.Unavailable)
        {
            var fine = entry.State == LocalComp.LocalCompFile.LocalFileStatus.Fine;
            var btnEd = new MyIconButton
            {
                SvgIcon = fine ? "lucide/circle-minus" : "lucide/circle-check",
                LogoScale = 1.0d
            };
            ToolTip.SetTip(btnEd, Lang.Text(fine ? "Instance.Resource.Disable" : "Instance.Resource.Enable"));
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
        BtnSelectEnable.IsEnabled = selectedEntries.Any(
            entry => entry.State == LocalComp.LocalCompFile.LocalFileStatus.Disabled);
        BtnSelectDisable.IsEnabled = selectedEntries.Any(
            entry => entry.State == LocalComp.LocalCompFile.LocalFileStatus.Fine);
        BtnSelectDelete.IsEnabled = selectedEntries.Count > 0;
    }

    private void ToggleSelectAll()
    {
        var selectable = _visibleEntries.Where(entry => !entry.IsFolder).ToList();
        if (selectable.Count == 0)
            return;
        var allSelected = selectable.All(entry => _selected.Contains(entry.RawPath));
        foreach (var entry in selectable)
        {
            if (allSelected)
                _selected.Remove(entry.RawPath);
            else
                _selected.Add(entry.RawPath);
        }

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
            var dialog = await PCL.Core.UI.Dialogs.DialogService.Instance.ShowAsync(
                new PCL.Core.UI.Dialogs.TextDialogRequest
                {
                    Title = Lang.Text("Common.Dialog.Warning"),
                    Content = Lang.Text(
                        "Event.Safety.ConfirmMessage",
                        Lang.Text("Common.Action.Delete") + " " + targets.Count),
                    Buttons = PCL.Core.UI.Dialogs.DialogButtons.OkCancel,
                    IsWarn = true
                });
            if (dialog.Button != 1)
                return;

            var folder = string.Empty;
            foreach (var entry in targets)
            {
                try
                {
                    folder = Path.GetDirectoryName(entry.IsFolder ? entry.ActualPath : entry.path)
                             ?? CurrentLoaderFolder;
                    DeleteEntryCore(entry);
                }
                catch (Exception ex)
                {
                    LauncherCommon.Log(ex, "删除资源失败：" + entry.path, LauncherLogLevel.Feedback);
                }
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

            var recycle = PCL.Core.IO.RecycleBin.IsSupported(entry.path);
            var moved = false;
            if (recycle)
            {
                var outcome = PCL.Core.IO.RecycleBin.Move(entry.path, out _);
                moved = outcome == PCL.Core.IO.RecycleBin.Outcome.Moved;
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

    private async void UpdateSelected()
    {
        var targets = _entries
            .Where(entry => _selected.Contains(entry.RawPath) && entry.CanUpdate && entry.UpdateFile is not null)
            .ToList();
        if (targets.Count == 0)
            return;

        try
        {
            var warn = await PCL.Core.UI.Dialogs.DialogService.Instance.ShowAsync(
                new PCL.Core.UI.Dialogs.TextDialogRequest
                {
                    Title = Lang.Text("Instance.Resource.Update.Warning.Title"),
                    Content = Lang.Text("Instance.Resource.Update.Warning.Message"),
                    Buttons = PCL.Core.UI.Dialogs.DialogButtons.Create(
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
                HintService.Show(Hints.Error(Lang.Text("Instance.Resource.Update.Failed", string.Empty)));
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
                                    if (string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase))
                                        continue;
                                    foreach (var variant in new[] { oldPath, oldPath + ".disabled", oldPath + ".old" })
                                        if (File.Exists(variant) && !string.Equals(
                                                variant,
                                                newPath,
                                                StringComparison.OrdinalIgnoreCase))
                                            File.Delete(variant);
                                }

                                HintService.Show(
                                    Hints.Success(
                                        Lang.Text("Instance.Resource.Update.SuccessMultiple", replacements.Count)));
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
                            HintService.Show(Hints.Info(Lang.Text("Instance.Resource.Update.Aborted")));
                            break;
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

    private void ToggleEntry(LocalComp.LocalCompFile entry) => ToggleEntry(entry, true);

    private void ToggleEntry(LocalComp.LocalCompFile entry, bool reload)
    {
        try
        {
            var path = entry.path;
            if (entry.State == LocalComp.LocalCompFile.LocalFileStatus.Fine)
            {
                CompLocalUpdateRegistry.Begin(Path.GetDirectoryName(path) ?? CurrentLoaderFolder);
                try
                {
                    File.Move(path, path + ".disabled");
                }
                finally
                {
                    CompLocalUpdateRegistry.End(Path.GetDirectoryName(path) ?? CurrentLoaderFolder);
                }
            }
            else if (entry.State == LocalComp.LocalCompFile.LocalFileStatus.Disabled)
            {
                var enabledPath = path.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)
                    ? path[..^".disabled".Length]
                    : path[..^".old".Length];
                CompLocalUpdateRegistry.Begin(Path.GetDirectoryName(path) ?? CurrentLoaderFolder);
                try
                {
                    File.Move(path, enabledPath);
                }
                finally
                {
                    CompLocalUpdateRegistry.End(Path.GetDirectoryName(path) ?? CurrentLoaderFolder);
                }
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
            var dialog = await PCL.Core.UI.Dialogs.DialogService.Instance.ShowAsync(
                new PCL.Core.UI.Dialogs.TextDialogRequest
                {
                    Title = Lang.Text("Common.Dialog.Warning"),
                    Content = Lang.Text(
                        "Event.Safety.ConfirmMessage",
                        $"{Lang.Text("Common.Action.Delete")} {entry.Name}"),
                    Buttons = PCL.Core.UI.Dialogs.DialogButtons.OkCancel,
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
                "Comp Delete " + LauncherCommon.GetUuid(),
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
            var files = SystemDialogs.SelectFiles(filter, Lang.Text("Instance.Resource.InstallFromFiles"));
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
                "Comp Install " + LauncherCommon.GetUuid(),
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

    private void OpenJarInJar()
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
            var mode = await PCL.Core.UI.Dialogs.DialogService.Instance.ShowAsync(
                new PCL.Core.UI.Dialogs.TextDialogRequest
                {
                    Title = Lang.Text("Instance.Resource.Export.Mode.Title"),
                    Content = Lang.Text("Instance.Resource.Export.Mode.Message"),
                    Buttons = PCL.Core.UI.Dialogs.DialogButtons.Create(
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
                builder.AppendLine("FileName,Name,Version,Tags");
            foreach (var entry in _entries)
            {
                if (isCsv)
                    builder.AppendLine(
                        string.Join(
                            ',',
                            entry.FileName,
                            entry.Name ?? string.Empty,
                            entry.Version ?? string.Empty,
                            string.Join('|', entry.Tags)));
                else
                    builder.AppendLine(entry.FileName);
                AppendEmbeddedForExport(builder, entry.EmbeddedMods, 1, isCsv);
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
            if (isCsv)
                builder.AppendLine(
                    string.Join(
                        ',',
                        entry.FileName,
                        entry.Name ?? string.Empty,
                        entry.Version ?? string.Empty,
                        string.Join('|', entry.Tags)));
            else
                builder.AppendLine(new string(' ', depth * 2) + entry.FileName);
            AppendEmbeddedForExport(builder, entry.EmbeddedMods, depth + 1, isCsv);
        }
    }

    private bool OnFileDropped(IReadOnlyList<string> paths)
    {
        if (!ReferenceEquals(PageRouter.CurrentPage, this))
            return false;

        var accepted = paths.Where(path =>
            _compType switch
            {
                Comp.CompType.Mod => LocalComp.LocalCompFile.IsModFile(path),
                Comp.CompType.ResourcePack or Comp.CompType.Shader =>
                    path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase),
                _ => LocalComp.LocalCompFile.IsCompFile(path, _compType)
            }).ToList();
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
                    Lang.Text("Instance.Resource.Install.SuccessMultiple", accepted.Count, Lang.Text(CompNameKey))));
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
