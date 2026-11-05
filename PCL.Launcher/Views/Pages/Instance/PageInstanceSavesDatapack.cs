using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI;
using PCL.UI.Controls;
using PCL.Views.Shell;
using Path = System.IO.Path;

namespace PCL.Views.Pages.Instance;

/// <summary>
///     存档数据包页：列出存档 <c>datapacks</c> 目录下的数据包文件与目录。
///     支持搜索、按启用状态/可读性筛选、排序、全选与批量启用/禁用/删除、从文件安装与下载新数据包；
///     删除优先移入系统回收站，回收站不可用时明确提示永久删除。
/// </summary>
public class PageInstanceSavesDatapack : MyPageRight, IRefreshable
{
    private MyRadioButton _btnFilterAll = null!;
    private MyRadioButton _btnFilterDisabled = null!;
    private MyRadioButton _btnFilterEnabled = null!;
    private MyRadioButton _btnFilterError = null!;
    private MyRadioButton _btnFilterUpdatable = null!;
    private MyIconTextButton _btnSelectDelete = null!;
    private MyIconTextButton _btnSelectDisable = null!;
    private MyIconTextButton _btnSelectEnable = null!;
    private MyIconTextButton _btnSort = null!;
    private bool _building;
    private TextBlock _labSelect = null!;
    private MyLoading _load = null!;
    private MyCard _panEmpty = null!;
    private StackPanel _panFilter = null!;
    private StackPanel _panList = null!;
    private MyCard _panLoad = null!;

    private StackPanel _panMain = null!;
    private MyCard _panSelect = null!;
    private StackPanel _panSort = null!;
    private MySearchBox _searchBox = null!;

    public PageInstanceSavesDatapack()
    {
        BuildUi();
        ViewModel.ReloadRequested += Reload;
    }

    public PageInstanceSavesDatapackViewModel ViewModel { get; } = new();

    private string? DatapacksPath => ViewModel.DatapacksPath;

    public void Refresh()
    {
        Reload();
    }

    private void BuildUi()
    {
        var root = new Grid();
        var panel = new StackPanel
        {
            Margin = new Thickness(25d, 25d, 25d, 25d)
        };
        var scroll = new MyScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = panel
        };
        _panMain = panel;
        PanScroll = scroll;
        root.Children.Add(scroll);

        _searchBox = new MySearchBox
        {
            HintText = Lang.Text("Instance.Resource.Search.Hint"),
            Text = ViewModel.SearchText
        };
        _searchBox.TextChanged += (_, _) =>
        {
            ViewModel.SetSearch(_searchBox.Text ?? string.Empty);
            BuildList();
        };
        _panMain.Children.Add(_searchBox);

        var manageCard = new MyCard
        {
            Title = Lang.Text("Instance.Saves.Left.Datapack"),
            Margin = new Thickness(0d, 15d, 0d, 15d)
        };
        var manageBody = new WrapPanel
        {
            Margin = new Thickness(15d, 8d, 0d, 15d)
        };
        var refresh = NewButton("Common.Action.Refresh", Reload);
        manageBody.Children.Add(refresh);
        var openFolder = NewButton(
            "Instance.Saves.OpenFolder",
            () => ViewModel.OpenFolderCommand.Execute(null));
        manageBody.Children.Add(openFolder);
        var install = NewButton(
            "Instance.Resource.InstallFromFiles",
            () => _ = InstallFromFilesAsync());
        manageBody.Children.Add(install);
        var download = NewButton(
            "Instance.Resource.DownloadNew",
            () => ViewModel.DownloadNewCommand.Execute(null));
        download.IsVisible = HiddenFeatureState.ForceShow || !Config.Preference.Hide.PageDownload;
        manageBody.Children.Add(download);
        var selectAll = NewButton(
            "Instance.Resource.SelectAll",
            () => ViewModel.ToggleSelectAllCommand.Execute(null));
        manageBody.Children.Add(selectAll);
        var export = NewButton(
            "Instance.Resource.ExportInfo",
            () => _ = ViewModel.ExportInfoAsync());
        ToolTip.SetTip(export, Lang.Text("Instance.Resource.ExportInfo.ToolTip"));
        manageBody.Children.Add(export);
        manageCard.BorderChild = manageBody;
        _panMain.Children.Add(manageCard);

        var listCard = new MyCard
        {
            MinHeight = 55d,
            Title = " "
        };
        var listContainer = new StackPanel
        {
            Margin = new Thickness(20d, 38d, 18d, 22d),
            Spacing = 2d
        };
        _panFilter = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Height = 28d,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        _btnFilterAll = NewFilterButton("Instance.Resource.Filter.All", 0);
        _btnFilterEnabled = NewFilterButton("Instance.Resource.Filter.Enabled", 1);
        _btnFilterDisabled = NewFilterButton("Instance.Resource.Filter.Disabled", 2);
        _btnFilterUpdatable = NewFilterButton("Instance.Resource.Filter.Updatable", 3);
        _btnFilterError = NewFilterButton("Instance.Resource.Filter.Error", 4);
        _panFilter.Children.Add(_btnFilterAll);
        _panFilter.Children.Add(_btnFilterEnabled);
        _panFilter.Children.Add(_btnFilterDisabled);
        _panFilter.Children.Add(_btnFilterUpdatable);
        _panFilter.Children.Add(_btnFilterError);
        listContainer.Children.Add(_panFilter);

        _panSort = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Height = 28d,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        _btnSort = new MyIconTextButton
        {
            Text = Lang.Text(
                "Instance.Resource.Sort.Text",
                Lang.Text("Instance.Resource.Sort.FileName")),
            SvgIcon = "lucide/arrow-up-down"
        };
        _btnSort.Click += (_, _) =>
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

            menu.Open(_btnSort);
        };
        _panSort.Children.Add(_btnSort);
        listContainer.Children.Add(_panSort);

        _panList = new StackPanel();
        listContainer.Children.Add(_panList);
        listCard.BorderChild = listContainer;
        _panMain.Children.Add(listCard);

        _panEmpty = new MyCard
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(40d),
            IsVisible = false,
            BorderChild = CreateEmptyBody()
        };
        root.Children.Add(_panEmpty);

        _labSelect = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(9d)
        };
        _btnSelectEnable = NewSelectButton(
            "Instance.Resource.Enable",
            "lucide/circle-check",
            () => ViewModel.SetSelectedEnabled(true));
        _btnSelectDisable = NewSelectButton(
            "Instance.Resource.Disable",
            "lucide/circle-minus",
            () => ViewModel.SetSelectedEnabled(false));
        _btnSelectDelete = NewSelectButton(
            "Common.Action.Delete",
            "lucide/trash-2",
            () => _ = ViewModel.DeleteSelectedAsync());
        var cancel = NewSelectButton(
            "Instance.Resource.CancelSelection",
            "lucide/x",
            () =>
            {
                ViewModel.ClearSelection();
                BuildList();
            });
        var selectButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(5d),
            Spacing = 2d
        };
        selectButtons.Children.Add(_btnSelectEnable);
        selectButtons.Children.Add(_btnSelectDisable);
        selectButtons.Children.Add(_btnSelectDelete);
        selectButtons.Children.Add(cancel);
        var selectBody = new StackPanel
        {
            Margin = new Thickness(6d, 4d, 6d, 6d),
            Spacing = 2d
        };
        selectBody.Children.Add(_labSelect);
        selectBody.Children.Add(selectButtons);
        _panSelect = new MyCard
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(25d, 25d, 25d, 0d),
            UseAnimation = false,
            IsVisible = false,
            BorderChild = selectBody
        };
        root.Children.Add(_panSelect);

        _load = new MyLoading
        {
            Text = Lang.Text("Instance.Resource.Datapack.Loading"),
            Margin = new Thickness(20d, 20d, 20d, 17d),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _panLoad = new MyCard
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(40d, 0d),
            UseAnimation = false,
            IsVisible = false,
            BorderChild = _load
        };
        root.Children.Add(_panLoad);

        Child = root;
        BuildList();
    }

    protected override void RefreshLocalization()
    {
        BuildUi();
    }

    private MyRadioButton NewFilterButton(string textKey, int index)
    {
        var button = new MyRadioButton
        {
            Text = Lang.Text(textKey),
            Tag = index.ToString(),
            ColorType = MyRadioButton.ColorState.Highlight,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2d, 0d),
            Checked = index == 0
        };
        button.Check += (sender, _) =>
        {
            if (sender.Tag is string tag && int.TryParse(tag, out var value))
                ViewModel.SetFilter(value);
            BuildList();
        };
        return button;
    }

    private static MyButton NewButton(string textKey, Action onClick)
    {
        var button = new MyButton
        {
            Text = Lang.Text(textKey),
            MinWidth = 110d,
            Margin = new Thickness(0d, 7d, 15d, 0d),
            Padding = new Thickness(13d, 7d),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        button.Click += (_, _) => onClick();
        return button;
    }

    private static MyIconTextButton NewSelectButton(string textKey, string icon, Action onClick)
    {
        var button = new MyIconTextButton
        {
            Text = Lang.Text(textKey),
            SvgIcon = icon,
            LogoScale = 1d
        };
        button.Click += (_, _) => onClick();
        return button;
    }

    private Control CreateEmptyBody()
    {
        var grid = new Grid
        {
            Margin = new Thickness(20d, 17d),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto"),
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto,*")
        };
        var title = new TextBlock
        {
            Text = Lang.Text("Instance.Resource.Datapack.Empty.Title"),
            FontSize = 19d,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0d, 0d, 0d, 9d)
        };
        ContainerTheme.BindDynamic(title, TextBlock.ForegroundProperty, "ColorBrush3");
        Grid.SetColumnSpan(title, 5);
        grid.Children.Add(title);
        var divider = new Rectangle
        {
            Height = 2d,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        ContainerTheme.BindDynamic(divider, Shape.FillProperty, "ColorBrush3");
        Grid.SetRow(divider, 1);
        Grid.SetColumnSpan(divider, 5);
        grid.Children.Add(divider);
        var message = new TextBlock
        {
            Text = Lang.Text("Instance.Resource.Datapack.Empty.Description"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(10d, 15d, 10d, 5d)
        };
        Grid.SetRow(message, 2);
        Grid.SetColumnSpan(message, 5);
        grid.Children.Add(message);
        var openFolder = new MyButton
        {
            Text = Lang.Text("Instance.Saves.OpenFolder"),
            Height = 35d,
            MinWidth = 140d,
            Padding = new Thickness(13d, 0d),
            Margin = new Thickness(10d, 10d, 10d, 0d),
            HorizontalAlignment = HorizontalAlignment.Center,
            ColorType = MyButton.ColorState.Highlight
        };
        openFolder.Click += (_, _) => ViewModel.OpenFolderCommand.Execute(null);
        Grid.SetRow(openFolder, 3);
        Grid.SetColumn(openFolder, 1);
        grid.Children.Add(openFolder);

        var install = new MyButton
        {
            Text = Lang.Text("Instance.Resource.InstallFromFiles"),
            Height = 35d,
            MinWidth = 140d,
            Padding = new Thickness(13d, 0d),
            Margin = new Thickness(10d, 10d, 10d, 0d),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        install.Click += (_, _) => _ = InstallFromFilesAsync();
        Grid.SetRow(install, 3);
        Grid.SetColumn(install, 2);
        grid.Children.Add(install);

        var download = new MyButton
        {
            Text = Lang.Text("Instance.Resource.Datapack.DownloadNew"),
            Height = 35d,
            MinWidth = 140d,
            Padding = new Thickness(13d, 0d),
            Margin = new Thickness(10d, 10d, 10d, 0d),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        download.Click += (_, _) => ViewModel.DownloadNewCommand.Execute(null);
        Grid.SetRow(download, 3);
        Grid.SetColumn(download, 3);
        grid.Children.Add(download);
        return grid;
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

        _panLoad.IsVisible = true;
        _panEmpty.IsVisible = false;
        _panMain.IsVisible = false;
        _panSelect.IsVisible = false;
        _load.State = new MyLoadingStateSimulator
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
            _panLoad.IsVisible = false;
            if (_load.State is MyLoadingStateSimulator simulator)
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
            _panList.Children.Clear();
            foreach (var entry in ViewModel.VisibleEntries)
                _panList.Children.Add(CreateItem(entry));

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
        _btnFilterAll.Text = ViewModel.FilterAllText;
        _btnFilterEnabled.Text = ViewModel.FilterEnabledText;
        _btnFilterDisabled.Text = ViewModel.FilterDisabledText;
        _btnFilterUpdatable.Text = ViewModel.FilterCanUpdateText;
        _btnFilterError.Text = ViewModel.FilterErrorText;
        _btnFilterUpdatable.IsVisible = ViewModel.ShowFilterCanUpdate;
        _btnFilterError.IsVisible = ViewModel.ShowFilterError;
        _btnFilterEnabled.IsVisible = ViewModel.ShowFilterEnabled;
        _btnFilterDisabled.IsVisible = ViewModel.ShowFilterDisabled;
    }

    private void RefreshSortText()
    {
        _btnSort.Text = ViewModel.SortText;
    }

    private void RefreshSelectBar()
    {
        _panSelect.IsVisible = ViewModel.ShowSelection;
        if (!ViewModel.ShowSelection)
            return;
        _labSelect.Text = ViewModel.SelectedText;
        _btnSelectEnable.IsEnabled = ViewModel.CanEnable;
        _btnSelectDisable.IsEnabled = ViewModel.CanDisable;
        _btnSelectDelete.IsEnabled = ViewModel.CanDelete;
    }

    private void UpdateEmptyState()
    {
        var empty = ViewModel.ShowEmpty || (DatapacksPath is null && ViewModel.Entries.Count == 0);
        _panEmpty.IsVisible = empty;
        _panMain.IsVisible = !empty;
        if (empty)
            _panSelect.IsVisible = false;
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