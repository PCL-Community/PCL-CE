using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI;
using PCL.Core.UI.Hints;
using PCL.UI.Controls;
using PCL.Views.Pages.Download;
using PCL.Views.Shell;
using Path = System.IO.Path;

namespace PCL.Views.Pages.Instance;

/// <summary>
///     存档数据包页：列出存档 <c>datapacks</c> 目录下的数据包文件与目录。
///     支持搜索、按启用状态/可读性筛选、排序、全选与批量启用/禁用/删除、从文件安装与下载新数据包；
///     删除优先移入系统回收站，回收站不可用时明确提示永久删除。
/// </summary>
public partial class PageInstanceSavesDatapack : MyPageRight, IRefreshable
{
    private bool _building;

    public PageInstanceSavesDatapack()
    {
        InitializeComponent();
        DataContext = ViewModel;
        PanScroll = PanBack;

        SearchBox.TextChanged += (_, _) =>
        {
            ViewModel.SetSearch(SearchBox.Text ?? string.Empty);
            BuildList();
        };
        BtnManageRefresh.Click += (_, _) => Reload();
        BtnManageOpen.Click += (_, _) => ViewModel.OpenFolderCommand.Execute(null);
        BtnManageInstall.Click += (_, _) => _ = InstallFromFilesAsync();
        BtnManageDownload.Click += (_, _) => ViewModel.DownloadNewCommand.Execute(null);
        BtnManageDownload.IsVisible =
            HiddenFeatureState.ForceShow || !Config.Preference.Hide.PageDownload;
        BtnManageSelectAll.Click += (_, _) =>
        {
            ViewModel.ToggleSelectAll();
            BuildList();
        };
        BtnManageInfoExport.Click += (_, _) => _ = ViewModel.ExportInfoAsync();

        foreach (var button in new[]
                 {
                     BtnFilterAll,
                     BtnFilterEnabled,
                     BtnFilterDisabled,
                     BtnFilterUpdatable,
                     BtnFilterError
                 })
            button.Check += (sender, _) =>
            {
                if (sender.Tag is string tag && int.TryParse(tag, out var index))
                    ViewModel.SetFilter(index);
                BuildList();
            };
        BtnSort.Click += (_, _) => ShowSortMenu();

        BtnSelectUpdate.Click += (_, _) => _ = UpdateSelectedAsync();
        BtnSelectEnable.Click += (_, _) => ViewModel.SetSelectedEnabled(true);
        BtnSelectDisable.Click += (_, _) => ViewModel.SetSelectedEnabled(false);
        BtnSelectFavorites.Click += (_, _) => ShowFavoritesMenu();
        BtnSelectShare.Click += (_, _) => ShareSelected();
        BtnSelectDelete.Click += (_, _) => _ = ViewModel.DeleteSelectedAsync();
        BtnSelectCancel.Click += (_, _) =>
        {
            ViewModel.ClearSelection();
            BuildList();
        };

        BtnHintInstall.Click += (_, _) => _ = InstallFromFilesAsync();
        BtnHintDownload.Click += (_, _) => ViewModel.DownloadNewCommand.Execute(null);
        BtnHintOpen.Click += (_, _) => ViewModel.OpenFolderCommand.Execute(null);

        ViewModel.ReloadRequested += Reload;
        AddHandler(
            KeyDownEvent,
            (_, e) =>
            {
                if (e.Key != Key.A || !e.KeyModifiers.HasFlag(KeyModifiers.Control))
                    return;
                if (SearchBox.IsKeyboardFocusWithin && !string.IsNullOrEmpty(SearchBox.Text))
                    return;
                ViewModel.ToggleSelectAll();
                BuildList();
                e.Handled = true;
            },
            RoutingStrategies.Tunnel);

        BuildList();
    }

    public PageInstanceSavesDatapackViewModel ViewModel { get; } = new();

    private string? DatapacksPath => ViewModel.DatapacksPath;

    public void Refresh()
    {
        Reload();
    }

    protected override void RefreshLocalization()
    {
        BtnManageDownload.IsVisible =
            HiddenFeatureState.ForceShow || !Config.Preference.Hide.PageDownload;
        BuildList();
        UpdateEmptyState();
    }

    public override void OnNavigatedTo(object? parameter)
    {
        var saveFolder = parameter as string;
        if (string.IsNullOrEmpty(saveFolder))
            saveFolder = PageInstanceSavesLeft.CurrentSave;
        ViewModel.SetSaveFolder(saveFolder ?? string.Empty);
        Reload();
    }

    public void Reload()
    {
        ViewModel.ClearSelection();
        var path = DatapacksPath;
        if (path is null)
        {
            UpdateEmptyState();
            return;
        }

        PanLoad.IsVisible = true;
        PanEmpty.IsVisible = false;
        PanMain.IsVisible = false;
        PanSelect.IsVisible = false;
        Load.State = new MyLoadingStateSimulator
        {
            LoadingState = LoadingState.Run
        };
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            await ViewModel.LoadAsync();
        }
        finally
        {
            PanLoad.IsVisible = false;
            if (Load.State is MyLoadingStateSimulator simulator)
                simulator.LoadingState = LoadingState.Stop;
        }

        BuildList();
        UpdateEmptyState();
    }

    private void BuildList()
    {
        if (_building)
            return;
        _building = true;
        try
        {
            ViewModel.Rebuild();
            PanList.Children.Clear();
            foreach (var entry in ViewModel.VisibleEntries)
                PanList.Children.Add(CreateItem(entry));

            RefreshFilterTexts();
            RefreshSortText();
            RefreshSelectBar();
        }
        finally
        {
            _building = false;
        }
    }

    private void RefreshFilterTexts()
    {
        BtnFilterAll.Text = ViewModel.FilterAllText;
        BtnFilterEnabled.Text = ViewModel.FilterEnabledText;
        BtnFilterDisabled.Text = ViewModel.FilterDisabledText;
        BtnFilterUpdatable.Text = ViewModel.FilterCanUpdateText;
        BtnFilterError.Text = ViewModel.FilterErrorText;
        BtnFilterUpdatable.IsVisible = ViewModel.ShowFilterCanUpdate;
        BtnFilterError.IsVisible = ViewModel.ShowFilterError;
        BtnFilterEnabled.IsVisible = ViewModel.ShowFilterEnabled;
        BtnFilterDisabled.IsVisible = ViewModel.ShowFilterDisabled;
    }

    private void RefreshSortText()
    {
        BtnSort.Text = ViewModel.SortText;
    }

    private void RefreshSelectBar()
    {
        PanSelect.IsVisible = ViewModel.ShowSelection;
        if (!ViewModel.ShowSelection)
            return;
        LabSelect.Text = ViewModel.SelectedText;
        BtnSelectEnable.IsEnabled = ViewModel.CanEnable;
        BtnSelectDisable.IsEnabled = ViewModel.CanDisable;
        BtnSelectUpdate.IsEnabled = ViewModel.CanUpdate;
        BtnSelectFavorites.IsEnabled = ViewModel.CanFavoriteAndShare;
        BtnSelectShare.IsEnabled = ViewModel.CanFavoriteAndShare;
        BtnSelectDelete.IsEnabled = ViewModel.CanDelete;
    }

    private void ShowSortMenu()
    {
        var menu = new ContextMenu();
        var names = PageInstanceSavesDatapackViewModel.GetSortNames();
        for (var i = 0; i < names.Count; i++)
        {
            var index = i;
            var item = new MyMenuItem
            {
                Header = names[i]
            };
            item.Click += (_, _) =>
            {
                ViewModel.SetSortMethod(
                    (PageInstanceSavesDatapackViewModel.DatapackSortMethod)index);
                BuildList();
            };
            menu.Items.Add(item);
        }

        menu.Open(BtnSort);
    }

    private void ShowFavoritesMenu()
    {
        var projects = ViewModel.GetFavoriteProjects();
        if (projects.Count > 0)
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

    private void UpdateEmptyState()
    {
        var empty = ViewModel.ShowEmpty || (DatapacksPath is null && ViewModel.Entries.Count == 0);
        PanEmpty.IsVisible = empty;
        PanMain.IsVisible = !empty;
        if (empty)
            PanSelect.IsVisible = false;
    }

    private MyListItem CreateItem(DatapackEntry entry)
    {
        var selected = ViewModel.IsSelected(entry);
        var metadata = (entry.IsDirectory
                ? Lang.Text("Instance.Resource.Item.FolderTag")
                : string.Empty)
            + (string.IsNullOrWhiteSpace(entry.Version) ? string.Empty : $" {entry.Version}")
            + (string.IsNullOrWhiteSpace(entry.Description)
                ? string.Empty
                : $" · {entry.Description}");
        var item = new MyListItem
        {
            Title = entry.File?.Name ?? entry.DisplayName,
            Info = string.IsNullOrWhiteSpace(metadata)
                ? entry.Disabled
                    ? Lang.Text("Instance.Resource.Filter.Disabled")
                    : Lang.Text("Instance.Resource.Filter.Enabled")
                : metadata,
            SvgIcon = entry.IsDirectory ? "lucide/folder" : "lucide/package",
            Type = MyListItem.CheckType.CheckBox,
            Checked = selected,
            tag = entry
        };
        if (entry.Tags.Count > 0)
            item.PlainTags = entry.Tags.Take(4);
        item.Check += (_, _) =>
        {
            ViewModel.SetSelected(entry, item.Checked);
            RefreshSelectBar();
        };

        var toggle = new MyIconButton
        {
            SvgIcon = entry.Disabled ? "lucide/check" : "lucide/x",
            LogoScale = 0.9d
        };
        ToolTip.SetTip(
            toggle,
            Lang.Text(entry.Disabled ? "Instance.Resource.Enable" : "Instance.Resource.Disable"));
        toggle.Click += (_, _) => ViewModel.Toggle(entry);

        var open = new MyIconButton
        {
            SvgIcon = "lucide/folder-open",
            LogoScale = 0.9d
        };
        ToolTip.SetTip(open, Lang.Text("Instance.Saves.OpenFolder"));
        open.Click += (_, _) =>
            InstancePageHelper.OpenFolder(
                entry.IsDirectory ? entry.Path : Path.GetDirectoryName(entry.Path) ?? entry.Path);

        var delete = new MyIconButton
        {
            SvgIcon = "lucide/trash-2",
            LogoScale = 0.9d
        };
        ToolTip.SetTip(delete, Lang.Text("Common.Action.Delete"));
        delete.Click += (_, _) => _ = ViewModel.DeleteAsync([entry]);

        var buttons = new List<MyIconButton>();
        if (entry.CanUpdate)
        {
            var update = new MyIconButton
            {
                SvgIcon = "lucide/upload",
                LogoScale = 0.9d
            };
            ToolTip.SetTip(update, Lang.Text("Instance.Resource.Update"));
            update.Click += (_, _) => _ = ViewModel.UpdateEntryAsync(entry);
            buttons.Add(update);
        }

        buttons.Add(open);
        buttons.Add(delete);
        item.Buttons = buttons;
        return item;
    }

    private async Task UpdateSelectedAsync()
    {
        await ViewModel.UpdateSelectedAsync();
        BuildList();
    }

    private Task UpdateEntryAsync(DatapackEntry entry)
    {
        return ViewModel.UpdateEntryAsync(entry);
    }

    #region 安装与导出

    private async Task InstallFromFilesAsync()
    {
        try
        {
            var path = DatapacksPath;
            if (path is null)
                return;
            var files = SystemDialogs.SelectFiles(
                Lang.Text("Instance.Saves.Datapack.Install.FileDialog.Filter"),
                Lang.Text("Instance.Saves.Datapack.Install.FileDialog.Title"));
            if (files.Length != 0)
            {
                await ViewModel.InstallFilesAsync(files);
                BuildList();
                UpdateEmptyState();
            }
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Resource.Error.OperationFailed", ex);
        }
    }

    private Task ExportInfoAsync()
    {
        return ViewModel.ExportInfoAsync();
    }

    #endregion
}