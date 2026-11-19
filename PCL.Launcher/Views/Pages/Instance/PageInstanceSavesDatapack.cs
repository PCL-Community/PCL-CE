using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI;
using PCL.Core.UI.Dialogs;
using PCL.Navigation;
using PCL.UI.Controls;
using PCL.Views.Pages.Download;
using PCL.Views.Shell;
using Path = System.IO.Path;

namespace PCL.Views.Pages.Instance;

/// <summary>
/// 存档数据包页：列出存档 <c>datapacks</c> 目录下的数据包文件与目录。
/// 支持搜索、按启用状态/可读性筛选、排序、全选与批量启用/禁用/删除、从文件安装与下载新数据包； 删除优先移入系统回收站，回收站不可用时明确提示永久删除。
/// </summary>
public partial class PageInstanceSavesDatapack : MyPageRight, IRefreshable, ICompLocalPage
{
    private readonly ResourceSwipeSelector _swipeSelector;
    private bool _building;

    public PageInstanceSavesDatapack()
    {
        InitializeComponent();
        DataContext = ViewModel;
        PanScroll = PanBack;
        ViewModel.SetPage(this);
        BtnFilterAll.Bind(
            MyRadioButton.TextProperty,
            new Binding(nameof(PageInstanceSavesDatapackViewModel.FilterAllText))
            {
                Source = ViewModel
            });
        BtnFilterEnabled.Bind(
            MyRadioButton.TextProperty,
            new Binding(nameof(PageInstanceSavesDatapackViewModel.FilterEnabledText))
            {
                Source = ViewModel
            });
        BtnFilterDisabled.Bind(
            MyRadioButton.TextProperty,
            new Binding(nameof(PageInstanceSavesDatapackViewModel.FilterDisabledText))
            {
                Source = ViewModel
            });
        BtnFilterUpdatable.Bind(
            MyRadioButton.TextProperty,
            new Binding(nameof(PageInstanceSavesDatapackViewModel.FilterCanUpdateText))
            {
                Source = ViewModel
            });
        BtnFilterError.Bind(
            MyRadioButton.TextProperty,
            new Binding(nameof(PageInstanceSavesDatapackViewModel.FilterErrorText))
            {
                Source = ViewModel
            });
        BtnSort.Bind(
            MyIconTextButton.TextProperty,
            new Binding(nameof(PageInstanceSavesDatapackViewModel.SortText))
            {
                Source = ViewModel
            });
        ViewModel.PropertyChanged += (_, args) =>
        {
            if (string.IsNullOrEmpty(args.PropertyName))
                RefreshSelectBar();
        };
        _swipeSelector = new ResourceSwipeSelector(
            PanList,
            PanSelect,
            () => ViewModel.VisibleEntries.Count,
            index => index >= 0
                && index < ViewModel.VisibleEntries.Count
                && ViewModel.IsSelected(ViewModel.VisibleEntries[index]),
            SetSwipeSelected,
            RefreshSelectBar);

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

        PanSelect.UpdateButton.Click += (_, _) => _ = UpdateSelectedAsync();
        PanSelect.EnableButton.Click += (_, _) => ViewModel.SetSelectedEnabled(true);
        PanSelect.DisableButton.Click += (_, _) => ViewModel.SetSelectedEnabled(false);
        PanSelect.FavoritesButton.Click += (_, _) => ShowFavoritesMenu();
        PanSelect.ShareButton.Click += (_, _) => ShareSelected();
        PanSelect.DeleteButton.Click += (_, _) =>
        {
            var targets = ViewModel
                .SelectedKeys
                .Select(key => ViewModel.Entries.FirstOrDefault(entry => entry.path == key))
                .OfType<LocalComp.LocalCompFile>()
                .ToList();
            var permanent =
                PanSelect.DeleteButton.LastClickKeyModifiers.HasFlag(KeyModifiers.Shift);
            _ = ViewModel.DeleteAsync(targets, permanent);
        };
        PanSelect.CancelButton.Click += (_, _) =>
        {
            ViewModel.ClearSelection();
            BuildList();
        };

        BtnHintInstall.Click += (_, _) => _ = InstallFromFilesAsync();
        BtnHintDownload.Click += (_, _) => ViewModel.DownloadNewCommand.Execute(null);
        BtnHintOpen.Click += (_, _) => ViewModel.OpenFolderCommand.Execute(null);

        ViewModel.ReloadRequested += Reload;
        BtnErrorRetry.Click += (_, _) => Reload();
        AttachedToVisualTree += (_, _) => FileDragHandler.FileDropped += OnFileDropped;
        DetachedFromVisualTree += (_, _) => FileDragHandler.FileDropped -= OnFileDropped;
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

    public bool LoadProgressVisible { get; set; }

    public string? CurrentFolderPath => DatapacksPath;

    public void SetLoadText(string key)
    {
    }

    public void UpdateOnly()
    {
    }

    public void RefreshModList()
    {
        BuildList();
    }

    public void Refresh()
    {
        Reload();
    }

    public override void OnNavigatedTo(PageParameter? parameter)
    {
        var saveFolder = parameter is string routedSave ? routedSave : null;
        if (string.IsNullOrEmpty(saveFolder))
            saveFolder = PageInstanceSavesLeft.CurrentSave;
        ViewModel.SetInstance(PageInstanceLeft.ResolveInstance());
        ViewModel.SetSaveFolder(saveFolder ?? string.Empty);
        ViewModel.ResetListViewState();
        SearchBox.Text = string.Empty;
        PanBack.ScrollToHome();
        Reload();
    }

    public override void OnNavigatedFrom()
    {
        _swipeSelector.Cancel();
    }

    public void Reload()
    {
        ViewModel.ResetListViewState();
        SearchBox.Text = string.Empty;
        PanBack.ScrollToHome();
        var path = DatapacksPath;
        if (path is null)
        {
            UpdateEmptyState();
            return;
        }

        PanLoad.IsVisible = true;
        PanEmpty.IsVisible = false;
        PanMain.IsVisible = false;
        PanSelect.UpdateSelection(false, 0, ViewModel.SelectedText);
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
            _swipeSelector.Cancel();
            ViewModel.Rebuild();
            PanList.Children.Clear();
            foreach (var placeholder in ViewModel.VisibleEntries.Select(entry =>
                         new MyVirtualizingElement<MyLocalModItem>(() => CreateItem(entry))
                         {
                             Height = 44d,
                             RecycleOffscreen = true
                         }))
                PanList.Children.Add(placeholder);

            RefreshFilterTexts();
            RefreshSelectBar();
        }
        finally
        {
            _building = false;
        }
    }

    private void RefreshFilterTexts()
    {
        BtnFilterUpdatable.IsVisible = ViewModel.ShowFilterCanUpdate;
        BtnFilterError.IsVisible = ViewModel.ShowFilterError;
        BtnFilterEnabled.IsVisible = ViewModel.ShowFilterEnabled;
        BtnFilterDisabled.IsVisible = ViewModel.ShowFilterDisabled;
    }

    private void SetSwipeSelected(int index, bool selected, bool animate)
    {
        LocalCompPageSupport.SetSwipeSelected(
            ViewModel.VisibleEntries,
            index,
            selected,
            animate,
            ViewModel.SetSelected,
            PanList.Children);
    }

    private void RefreshSelectBar()
    {
        PanListBack.Margin = new Thickness(
            0d,
            0d,
            0d,
            ViewModel.ShowSelection ? 95d : 15d);
        if (!ViewModel.ShowSelection)
        {
            PanSelect.UpdateSelection(false, 0, ViewModel.SelectedText);
            return;
        }

        PanSelect.EnableButton.IsEnabled = ViewModel.CanEnable;
        PanSelect.DisableButton.IsEnabled = ViewModel.CanDisable;
        PanSelect.UpdateButton.IsEnabled = ViewModel.CanUpdate;
        PanSelect.FavoritesButton.IsEnabled = ViewModel.CanFavoriteAndShare;
        PanSelect.ShareButton.IsEnabled = ViewModel.CanFavoriteAndShare;
        PanSelect.DeleteButton.IsEnabled = ViewModel.CanDelete;
        PanSelect.UpdateSelection(
            true,
            ViewModel.SelectedKeys.Count,
            ViewModel.SelectedText,
            !PanSelect.IsSwipeActive);
    }

    private void ShowSortMenu()
    {
        LocalCompPageSupport.ShowSortMenu(
            BtnSort,
            PageInstanceSavesDatapackViewModel.GetSortNames(),
            index =>
            {
                ViewModel.SetSortMethod(
                    (PageInstanceSavesDatapackViewModel.DatapackSortMethod)index);
                BuildList();
            });
    }

    private void ShowFavoritesMenu()
    {
        LocalCompPageSupport.ShowFavoritesMenu(
            ViewModel.GetFavoriteProjects(),
            PanSelect.FavoritesButton);
    }

    private void ShareSelected()
    {
        LocalCompPageSupport.ShareSelected(
            this,
            ViewModel.GetShareIds,
            ViewModel.ClearSelection,
            BuildList);
    }

    private void UpdateEmptyState()
    {
        var failed = ViewModel.HasError;
        PanError.IsVisible = failed;
        var empty = !failed
            && (ViewModel.ShowEmpty || (DatapacksPath is null && ViewModel.Entries.Count == 0));
        PanEmpty.IsVisible = empty;
        PanMain.IsVisible = !failed && !empty;
        if (empty || failed)
            PanSelect.UpdateSelection(false, 0, ViewModel.SelectedText);
    }

    private MyLocalModItem CreateItem(LocalComp.LocalCompFile entry)
    {
        var item = new MyLocalModItem(entry)
        {
            AllowFolderNavigation = false
        };
        item.CheckedChanged += (_, value) =>
        {
            ViewModel.SetSelected(entry, value);
            RefreshSelectBar();
        };
        item.InfoRequested += i => _ = ShowInfoAsync(entry);

        var toggle = new MyIconButton
        {
            SvgIcon = entry.State == LocalComp.LocalCompFile.LocalFileStatus.Disabled
                ? "lucide/check"
                : "lucide/x",
            LogoScale = 0.9d
        };
        toggle.SetLocalizedValue(
            ToolTip.TipProperty,
            entry.State == LocalComp.LocalCompFile.LocalFileStatus.Disabled
                ? "Instance.Resource.Enable"
                : "Instance.Resource.Disable");
        toggle.Click += (_, _) => ViewModel.Toggle(entry);
        toggle.IsVisible = !entry.IsFolder
            && entry.State != LocalComp.LocalCompFile.LocalFileStatus.Unavailable;

        var info = new MyIconButton
        {
            SvgIcon = "lucide/info",
            LogoScale = 1d
        };
        info.SetLocalizedValue(ToolTip.TipProperty, "Instance.Saves.Details");
        info.Click += (_, _) => _ = ShowInfoAsync(entry);

        var open = new MyIconButton
        {
            SvgIcon = "lucide/folder-open",
            LogoScale = 0.9d
        };
        open.SetLocalizedValue(ToolTip.TipProperty, "Instance.Saves.OpenFolder");
        open.Click += (_, _) =>
        {
            var path = LocalCompFileOperations.GetEntryPath(entry);
            InstancePageHelper.OpenFolder(
                entry.IsFolder ? path : Path.GetDirectoryName(path) ?? path);
        };

        var delete = new MyIconButton
        {
            SvgIcon = "lucide/trash-2",
            LogoScale = 0.9d
        };
        delete.SetLocalizedValue(ToolTip.TipProperty, "Common.Action.Delete");
        delete.Click += (_, _) => _ = ViewModel.DeleteAsync(
            [entry],
            delete.LastClickKeyModifiers.HasFlag(KeyModifiers.Shift));

        var buttons = new List<MyIconButton>
        {
            info,
            open
        };
        if (toggle.IsVisible)
            buttons.Add(toggle);
        buttons.Add(delete);
        item.Buttons = buttons;
        item.SetChecked(ViewModel.IsSelected(entry), false);
        return item;
    }

    private async Task ShowUpdateConfirmAsync(LocalComp.LocalCompFile entry)
    {
        var result = await DialogService.Instance.ShowAsync(
            new TextDialogRequest
            {
                Title = Lang.Text("Instance.Resource.Item.UpdateConfirm.Title"),
                Content = Lang.Text(
                        "Instance.Resource.Item.UpdateConfirm.Message",
                        entry.Name)
                    + "\r\n\r\n"
                    + $"{entry.Version} -> {entry.UpdateFile?.Version}",
                Buttons = DialogButtons.Create(
                    "Instance.Resource.Item.Update",
                    "Instance.Resource.Item.ViewChangelog",
                    "Common.Action.Cancel")
            });
        switch (result.Button)
        {
            case 1:
                await ViewModel.UpdateEntriesAsync([entry]);
                break;
            case 2:
                PageInstanceCompResourceViewModel.ShowUpdateLog(entry);
                break;
        }
    }

    private static async Task ShowInfoAsync(LocalComp.LocalCompFile entry)
    {
        if (entry.State == LocalComp.LocalCompFile.LocalFileStatus.Unavailable)
        {
            await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Instance.Saves.Datapack.Info.ReadFailedTitle"),
                    Content = Lang.Text(
                        "Instance.Saves.Datapack.Info.ReadFailed.WithDetail",
                        entry.FileUnavailableReason?.ToString() ?? string.Empty),
                    Buttons = DialogButtons.Ok
                });
            return;
        }

        if (entry.Comp is { } comp)
        {
            var instance = PageInstanceLeft.ResolveInstance();
            PageRouter.Navigate(
                PageIds.CompDetail,
                null,
                new CompDetailParameter(
                    comp,
                    Comp.CompType.DataPack,
                    instance?.Info.VanillaName,
                    Comp.CompLoaderType.Any));
            return;
        }

        var lines = new List<string>();
        if (entry.IsFolder)
        {
            lines.Add(Lang.Text("Instance.Resource.Item.FolderTag"));
            lines.Add(Lang.Text("Instance.Resource.Item.Info.Path", entry.ActualPath));
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(entry.Authors))
                lines.Add(Lang.Text("Instance.Saves.Datapack.Info.Author") + entry.Authors);
            lines.Add(
                Lang.Text("Instance.Saves.Datapack.Info.File")
                + entry.Name
                + $"（{InstancePageHelper.FormatSize(LocalCompFileOperations.GetSafeSize(entry))}）");
            if (!string.IsNullOrWhiteSpace(entry.Version))
                lines.Add(Lang.Text("Instance.Saves.Datapack.Info.Version") + entry.Version);
            if (!string.IsNullOrWhiteSpace(entry.ModId))
                lines.Add(Lang.Text("Instance.Saves.Datapack.Info.DatapackId") + entry.ModId);
            if (!string.IsNullOrWhiteSpace(entry.Description))
                lines.Add(entry.Description);
        }

        if (lines.Count == 0)
            lines.Add(entry.Name);

        var showWebsite = !entry.IsFolder && !string.IsNullOrWhiteSpace(entry.Url);
        var buttons = new List<string>();
        if (showWebsite)
            buttons.Add("Instance.Resource.Item.Info.OpenWebsite");
        buttons.Add("Instance.Resource.Item.Info.Return");
        var result = await DialogService.Instance.ShowAsync(
            new TextDialogRequest
            {
                Title = entry.Name,
                Content = string.Join("\r\n", lines),
                Buttons = DialogButtons.Create([.. buttons])
            });
        if (showWebsite && result.Button == 1)
            try
            {
                Basics.OpenPath(entry.Url!);
            }
            catch (Exception ex)
            {
                InstancePageHelper.ReportError("Instance.Resource.Error.OperationFailed", ex);
            }
    }

    private async Task UpdateSelectedAsync()
    {
        await ViewModel.UpdateSelectedAsync();
        BuildList();
    }

    private Task UpdateEntryAsync(LocalComp.LocalCompFile entry)
    {
        return ViewModel.UpdateEntryAsync(entry);
    }

    private bool OnFileDropped(IReadOnlyList<string> paths)
    {
        if (!ReferenceEquals(PageRouter.CurrentPage, this))
            return false;
        var accepted = paths
            .Where(path => path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".zip.disabled", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (accepted.Count == 0)
            return false;
        _ = ViewModel.InstallFilesAsync(accepted);
        return true;
    }

    #region 安装与导出

    private async Task InstallFromFilesAsync()
    {
        try
        {
            var path = DatapacksPath;
            if (path is null)
                return;
            var files = await SystemDialogs.SelectFilesAsync(
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