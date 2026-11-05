using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Instance;

/// <summary>
///     实例存档列表页：扫描实例 <c>saves</c> 目录下的存档文件夹并显示名称、创建/修改时间与图标；
///     支持搜索、按文件名/创建时间/修改时间排序、空态三按钮（打开文件夹/下载新存档/粘贴剪贴板文件），
///     点击进入存档详情；删除优先移入系统回收站，不支持回收站时明确告知永久删除。
/// </summary>
public class PageInstanceSaves : MyPageRight, IRefreshable
{
    private readonly DispatcherTimer _fileSystemRefreshTimer;
    private readonly DispatcherTimer _searchTimer;
    private MyIconTextButton _btnSort = null!;
    private bool _isActive;
    private MyCard _listCard = null!;
    private StackPanel _listPanel = null!;
    private MyCard _panEmpty = null!;

    private StackPanel _panMain = null!;
    private MySearchBox _searchBox = null!;

    public PageInstanceSaves()
    {
        BuildUi();
        _fileSystemRefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100d)
        };
        _fileSystemRefreshTimer.Tick += (_, _) =>
        {
            _fileSystemRefreshTimer.Stop();
            _ = ReloadAsync();
        };
        _searchTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100d)
        };
        _searchTimer.Tick += (_, _) =>
        {
            _searchTimer.Stop();
            BuildList();
        };
        ViewModel.FileClipboardWriter = WriteFolderToClipboardAsync;
        ViewModel.Updated += BuildList;
    }

    public PageInstanceSavesViewModel ViewModel { get; } = new();

    public void Refresh()
    {
        _ = ReloadAsync();
        HintService.Show(Hints.Info(Lang.Text("Instance.Saves.Status.Refreshing"), false));
    }

    private void BuildUi()
    {
        var root = new Grid();
        var panel = new StackPanel
        {
            Margin = new Thickness(25d, 10d, 25d, 10d)
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
            HintText = Lang.Text("Instance.Saves.SearchHint"),
            Margin = new Thickness(0d, 15d),
            Text = ViewModel.SearchText
        };
        _searchBox.TextChanged += (_, _) =>
        {
            ViewModel.SearchText = _searchBox.Text ?? string.Empty;
            _searchTimer.Stop();
            _searchTimer.Start();
        };
        _panMain.Children.Add(_searchBox);

        var quickCard = new MyCard
        {
            Title = Lang.Text("Instance.Saves.QuickActions"),
            Margin = new Thickness(0d, 0d, 0d, 15d)
        };
        var quickBody = new Grid
        {
            Height = 35d,
            Margin = new Thickness(25d, 40d, 15d, 20d),
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto")
        };
        var openFolder = NewButton(
            "Instance.Saves.OpenFolder",
            () => ViewModel.OpenFolderCommand.Execute(null),
            MyButton.ColorState.Highlight);
        quickBody.Children.Add(openFolder);
        var download = NewButton(
            "Instance.Resource.DownloadNew",
            () => ViewModel.DownloadNewCommand.Execute(null));
        Grid.SetColumn(download, 1);
        quickBody.Children.Add(download);
        var paste = NewButton("Instance.Saves.PasteFile", () => _ = ViewModel.PasteAsync());
        Grid.SetColumn(paste, 2);
        quickBody.Children.Add(paste);
        quickCard.BorderChild = quickBody;
        _panMain.Children.Add(quickCard);

        _listCard = new MyCard
        {
            Title = Lang.Text("Instance.Saves.ListTitle"),
            Margin = new Thickness(0d, 0d, 0d, 15d),
            MinHeight = 55d
        };
        var listGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto")
        };
        var sortRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Height = 28d,
            Margin = new Thickness(0d, 13d, 15d, 0d)
        };
        _btnSort = new MyIconTextButton
        {
            Text = ViewModel.SortText,
            SvgIcon = "lucide/arrow-up-down"
        };
        _btnSort.Click += (_, _) =>
        {
            var menu = new ContextMenu();
            var methods = Enum.GetValues<SaveSortMethod>();
            foreach (var method in methods)
            {
                var item = new MyMenuItem
                {
                    Header = PageInstanceSavesViewModel.GetSortName(method)
                };
                var target = method;
                item.Click += (_, _) =>
                {
                    ViewModel.SetSortMethod(target);
                    UpdateSortText();
                    BuildList();
                };
                menu.Items.Add(item);
            }

            menu.Open(_btnSort);
        };
        sortRow.Children.Add(_btnSort);
        Grid.SetRow(sortRow, 0);
        listGrid.Children.Add(sortRow);
        _listPanel = new StackPanel
        {
            Margin = new Thickness(20d, 8d, 18d, 22d),
            VerticalAlignment = VerticalAlignment.Top
        };
        Grid.SetRow(_listPanel, 1);
        listGrid.Children.Add(_listPanel);
        _listCard.BorderChild = listGrid;
        _panMain.Children.Add(_listCard);

        _panEmpty = new MyCard
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(40d),
            IsVisible = false,
            BorderChild = CreateEmptyBody()
        };
        root.Children.Add(_panEmpty);

        Child = root;
        UpdateSortText();
    }

    private static MyButton NewButton(
        string textKey,
        Action onClick,
        MyButton.ColorState color = MyButton.ColorState.Normal)
    {
        var button = new MyButton
        {
            Text = Lang.Text(textKey),
            MinWidth = 140d,
            Padding = new Thickness(13d, 0d),
            Margin = new Thickness(0d, 0d, 20d, 0d),
            HorizontalAlignment = HorizontalAlignment.Left,
            ColorType = color
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
            Text = Lang.Text("Instance.Saves.Empty.Title"),
            FontSize = 19d,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0d, 0d, 0d, 9d)
        };
        ContainerTheme.BindDynamic(title, TextBlock.ForegroundProperty, "ColorBrush3");
        Grid.SetColumnSpan(title, 4);
        grid.Children.Add(title);
        var divider = new Rectangle
        {
            Height = 2d,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        ContainerTheme.BindDynamic(divider, Shape.FillProperty, "ColorBrush3");
        Grid.SetRow(divider, 1);
        Grid.SetColumnSpan(divider, 4);
        grid.Children.Add(divider);
        var message = new TextBlock
        {
            Text = Lang.Text("Instance.Saves.Empty.Message"),
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(10d, 15d, 10d, 5d)
        };
        Grid.SetRow(message, 2);
        Grid.SetColumnSpan(message, 4);
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

        var download = new MyButton
        {
            Text = Lang.Text("Instance.Resource.DownloadNew"),
            Height = 35d,
            MinWidth = 140d,
            Padding = new Thickness(13d, 0d),
            Margin = new Thickness(10d, 10d, 10d, 0d),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        download.Click += (_, _) => ViewModel.DownloadNewCommand.Execute(null);
        Grid.SetRow(download, 3);
        Grid.SetColumn(download, 2);
        grid.Children.Add(download);

        var paste = new MyButton
        {
            Text = Lang.Text("Instance.Saves.PasteFile"),
            Height = 35d,
            MinWidth = 140d,
            Padding = new Thickness(13d, 0d),
            Margin = new Thickness(10d, 10d, 10d, 0d),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        paste.Click += (_, _) => _ = ViewModel.PasteAsync();
        Grid.SetRow(paste, 3);
        Grid.SetColumn(paste, 3);
        grid.Children.Add(paste);
        return grid;
    }

    public override void OnNavigatedTo(object? parameter)
    {
        _isActive = true;
        ViewModel.StopWatcher();
        _fileSystemRefreshTimer?.Stop();
        var instance = InstancePageHelper.ResolveInstance(parameter);
        ViewModel.SetInstance(instance);
        _ = ReloadAsync();
    }

    public override void OnNavigatedFrom()
    {
        _isActive = false;
        ViewModel.StopWatcher();
        _fileSystemRefreshTimer?.Stop();
    }

    protected override void RefreshLocalization()
    {
        var text = ViewModel.SearchText;
        BuildUi();
        _searchBox.Text = text;
        ViewModel.RefreshLocalization();
        UpdateSortText();
        BuildList();
    }

    public async Task ReloadAsync()
    {
        try
        {
            await ViewModel.LoadAsync();
            if (!_isActive)
                return;
            BuildList();
            ViewModel.StartWatcher(() =>
                Dispatcher.UIThread.Post(() =>
                {
                    _fileSystemRefreshTimer.Stop();
                    _fileSystemRefreshTimer.Start();
                }));
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Saves.LoadListFailed", ex);
            BuildList();
        }
    }

    private void BuildList()
    {
        if (_listPanel is null)
            return;

        _listPanel.Children.Clear();
        foreach (var entry in ViewModel.Saves)
            _listPanel.Children.Add(CreateSaveItem(entry));

        _listCard.Title = ViewModel.ListTitle;
        _panEmpty.IsVisible = ViewModel.ShowNoWorld;
        _panMain.IsVisible = ViewModel.ShowContent;
    }

    private void UpdateSortText()
    {
        _btnSort?.Text = ViewModel.SortText;
    }

    private MyListItem CreateSaveItem(SaveEntry entry)
    {
        var folder = entry.FolderPath;
        var item = new MyListItem
        {
            Type = MyListItem.CheckType.Clickable,
            Title = entry.Title,
            Info = entry.Info,
            Logo = entry.Logo,
            tag = folder
        };
        entry.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SaveEntry.Info))
                item.Info = entry.Info;
        };

        var open = new MyIconButton
        {
            SvgIcon = "lucide/folder-open",
            LogoScale = 0.9d
        };
        ToolTip.SetTip(open, Lang.Text("Instance.Saves.OpenFolder"));
        open.Click += (_, _) => ViewModel.OpenSaveFolderCommand.Execute(folder);

        var detail = new MyIconButton
        {
            SvgIcon = "lucide/info",
            LogoScale = 0.9d
        };
        ToolTip.SetTip(detail, Lang.Text("Instance.Saves.Details"));
        detail.Click += (_, _) => ViewModel.OpenDetailsCommand.Execute(folder);

        var copy = new MyIconButton
        {
            SvgIcon = "lucide/copy",
            LogoScale = 0.9d
        };
        ToolTip.SetTip(copy, Lang.Text("Common.Action.Copy"));
        copy.Click += (_, _) => _ = ViewModel.CopySaveAsync(folder);

        var delete = new MyIconButton
        {
            SvgIcon = "lucide/trash-2",
            LogoScale = 0.9d
        };
        ToolTip.SetTip(delete, Lang.Text("Common.Action.Delete"));
        delete.Click += (_, _) => _ = ViewModel.DeleteSaveAsync(folder);

        var buttons = new List<MyIconButton>
        {
            open,
            delete,
            copy,
            detail
        };
        if (ViewModel.QuickPlayEnabled)
        {
            var launch = new MyIconButton
            {
                SvgIcon = "lucide/play",
                LogoScale = 0.9d
            };
            ToolTip.SetTip(launch, Lang.Text("Instance.Saves.QuickPlay"));
            launch.Click += (_, _) => ViewModel.LaunchSaveCommand.Execute(folder);
            buttons.Add(launch);
        }

        item.Buttons = buttons;
        item.Click += (_, _) => ViewModel.OpenDetailsCommand.Execute(folder);
        return item;
    }

    private async Task WriteFolderToClipboardAsync(string folder)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard is null)
            return;
        var item = await topLevel.StorageProvider.TryGetFolderFromPathAsync(new Uri(folder));
        if (item is null)
            return;
        await topLevel.Clipboard.SetFilesAsync([item]);
    }
}