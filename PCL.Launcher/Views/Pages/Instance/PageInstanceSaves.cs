using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.IO;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.Navigation;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Instance;

/// <summary>
///     实例存档列表页：扫描实例 <c>saves</c> 目录下的存档文件夹并显示名称、创建/修改时间与图标；
///     支持搜索、按文件名/创建时间/修改时间排序、空态三按钮（打开文件夹/下载新存档/粘贴剪贴板文件），
///     点击进入存档详情；删除优先移入系统回收站，不支持回收站时明确告知永久删除。
/// </summary>
public class PageInstanceSaves : MyPageRight, IRefreshable
{
    private enum SaveSortMethod
    {
        FileName,
        CreateTime,
        ModifyTime
    }

    private StackPanel _panMain = null!;
    private MySearchBox _searchBox = null!;
    private MyIconTextButton _btnSort = null!;
    private MyCard _listCard = null!;
    private StackPanel _listPanel = null!;
    private MyCard _panEmpty = null!;
    private readonly List<string> _saveFolders = [];
    private string? _savesPath;
    private SaveSortMethod _sortMethod = SaveSortMethod.FileName;
    private bool _loading;

    public PageInstanceSaves()
    {
        BuildUi();
    }

    private void BuildUi()
    {
        var root = new Grid();
        var panel = new StackPanel { Margin = new Thickness(25d, 10d, 25d, 10d) };
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
            Margin = new Thickness(0d, 15d)
        };
        _searchBox.TextChanged += (_, _) => BuildList();
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
        var openFolder = NewButton("Instance.Saves.OpenFolder", OpenFolder, MyButton.ColorState.Highlight);
        quickBody.Children.Add(openFolder);
        var download = NewButton("Instance.Resource.DownloadNew", DownloadNew);
        Grid.SetColumn(download, 1);
        quickBody.Children.Add(download);
        var paste = NewButton("Instance.Saves.PasteFile", () => _ = PasteAsync());
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
        var listGrid = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto") };
        var sortRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Height = 28d,
            Margin = new Thickness(0d, 13d, 15d, 0d)
        };
        _btnSort = new MyIconTextButton
        {
            Text = Lang.Text("Instance.Saves.SortBy", Lang.Text("Instance.Saves.SortFileName")),
            SvgIcon = "lucide/arrow-up-down"
        };
        _btnSort.Click += (_, _) =>
        {
            _sortMethod = _sortMethod switch
            {
                SaveSortMethod.FileName => SaveSortMethod.CreateTime,
                SaveSortMethod.CreateTime => SaveSortMethod.ModifyTime,
                _ => SaveSortMethod.FileName
            };
            UpdateSortText();
            BuildList();
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
            IsVisible = false
        };
        _panEmpty.BorderChild = CreateEmptyBody();
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
        var divider = new Avalonia.Controls.Shapes.Rectangle
        {
            Height = 2d,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        ContainerTheme.BindDynamic(divider, Avalonia.Controls.Shapes.Shape.FillProperty, "ColorBrush3");
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
        openFolder.Click += (_, _) => OpenFolder();
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
        download.Click += (_, _) => DownloadNew();
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
        paste.Click += (_, _) => _ = PasteAsync();
        Grid.SetRow(paste, 3);
        Grid.SetColumn(paste, 3);
        grid.Children.Add(paste);
        return grid;
    }

    public override void OnNavigatedTo(object? parameter)
    {
        var instance = InstancePageHelper.ResolveInstance(parameter);
        _savesPath = instance is null ? null : InstancePageHelper.SavesPath(instance);
        _ = ReloadAsync();
    }

    protected override void RefreshLocalization()
    {
        var text = _searchBox.Text;
        BuildUi();
        _searchBox.Text = text;
        BuildList();
    }

    public void Refresh()
    {
        _ = ReloadAsync();
    }

    public async Task ReloadAsync()
    {
        if (_loading)
            return;
        _loading = true;
        try
        {
            _saveFolders.Clear();
            if (string.IsNullOrEmpty(_savesPath))
            {
                BuildList();
                return;
            }

            InstancePageHelper.EnsureDirectory(_savesPath);
            var folders = await Task.Run(
                () => Directory.EnumerateDirectories(_savesPath)
                    .Where(folder => File.Exists(Path.Combine(folder, "level.dat")))
                    .ToList());
            _saveFolders.AddRange(folders);
            BuildList();
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Saves.LoadListFailed", ex);
            BuildList();
        }
        finally
        {
            _loading = false;
        }
    }

    private void BuildList()
    {
        if (_listPanel is null)
            return;

        var keyword = (_searchBox?.Text ?? string.Empty).Trim();
        var filtered = _saveFolders
            .Where(folder => keyword.Length == 0
                             || Path.GetFileName(folder).Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .ToList();
        filtered.Sort(CompareFolders);

        _listPanel.Children.Clear();
        foreach (var folder in filtered)
            _listPanel.Children.Add(CreateSaveItem(folder));

        var searching = keyword.Length > 0;
        _listCard.Title = searching
            ? Lang.Text("Instance.Saves.SearchResultTitle", filtered.Count)
            : Lang.Text("Instance.Saves.SaveListTitle", _saveFolders.Count);

        var hasAnySave = _saveFolders.Count > 0;
        _panEmpty.IsVisible = !hasAnySave;
        _panMain.IsVisible = hasAnySave;
    }

    private int CompareFolders(string left, string right)
    {
        return _sortMethod switch
        {
            SaveSortMethod.CreateTime => Directory.GetCreationTime(right)
                .CompareTo(Directory.GetCreationTime(left)),
            SaveSortMethod.ModifyTime => Directory.GetLastWriteTime(right)
                .CompareTo(Directory.GetLastWriteTime(left)),
            _ => string.Compare(
                Path.GetFileName(left),
                Path.GetFileName(right),
                StringComparison.OrdinalIgnoreCase)
        };
    }

    private void UpdateSortText()
    {
        if (_btnSort is null)
            return;
        var key = _sortMethod switch
        {
            SaveSortMethod.CreateTime => "Instance.Saves.SortCreateTime",
            SaveSortMethod.ModifyTime => "Instance.Saves.SortModifyTime",
            _ => "Instance.Saves.SortFileName"
        };
        _btnSort.Text = Lang.Text("Instance.Saves.SortBy", Lang.Text(key));
    }

    private MyListItem CreateSaveItem(string folder)
    {
        var name = Path.GetFileName(folder);
        var info = Lang.Text(
            "Instance.Saves.CreationTime",
            Lang.Date(Directory.GetCreationTime(folder), "d"),
            Lang.Date(Directory.GetLastWriteTime(folder), "d"));
        var item = new MyListItem
        {
            Type = MyListItem.CheckType.Clickable,
            Title = name,
            Info = info,
            Logo = GetSaveLogo(folder),
            tag = folder
        };

        var open = new MyIconButton { SvgIcon = "lucide/folder-open", LogoScale = 0.9d };
        ToolTip.SetTip(open, Lang.Text("Instance.Saves.OpenFolder"));
        open.Click += (_, _) => InstancePageHelper.OpenFolder(folder);

        var detail = new MyIconButton { SvgIcon = "lucide/info", LogoScale = 0.9d };
        ToolTip.SetTip(detail, Lang.Text("Instance.Saves.Details"));
        detail.Click += (_, _) => OpenDetail(folder);

        var delete = new MyIconButton { SvgIcon = "lucide/trash-2", LogoScale = 0.9d };
        ToolTip.SetTip(delete, Lang.Text("Common.Action.Delete"));
        delete.Click += (_, _) => _ = DeleteSaveAsync(item, folder);

        item.Buttons = [open, detail, delete];
        item.Click += (_, _) => OpenDetail(folder);
        return item;
    }

    private static string GetSaveLogo(string folder)
    {
        var iconPath = Path.Combine(folder, "icon.png");
        if (!File.Exists(iconPath))
            return LauncherPaths.ImageRoot + "Icons/NoIcon.png";
        try
        {
            var instancePath = PageInstanceLeft.ResolveInstance()?.PathInstance;
            if (string.IsNullOrEmpty(instancePath))
                return LauncherPaths.ImageRoot + "Icons/NoIcon.png";
            var cacheFolder = Path.Combine(instancePath, "PCL", "ImgCache");
            Directory.CreateDirectory(cacheFolder);
            var target = Path.Combine(cacheFolder, LauncherCommon.GetFileMd5(iconPath) + ".png");
            if (!File.Exists(target))
                File.Copy(iconPath, target, true);
            return target;
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "缓存存档图标失败", LauncherLogLevel.Developer);
            return LauncherPaths.ImageRoot + "Icons/NoIcon.png";
        }
    }

    private void OpenDetail(string folder)
    {
        PageInstanceSavesLeft.currentSave = folder;
        PageRouter.Navigate(new PageStackData(PageType.VersionSaves, PageSubType.VersionSavesInfo, folder));
    }

    private void OpenFolder()
    {
        if (string.IsNullOrEmpty(_savesPath))
            return;
        InstancePageHelper.EnsureDirectory(_savesPath);
        InstancePageHelper.OpenFolder(_savesPath);
    }

    private void DownloadNew()
    {
        PageRouter.Navigate(PageType.Download, PageSubType.DownloadWorld);
    }

    private async Task PasteAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(_savesPath))
                return;
            InstancePageHelper.EnsureDirectory(_savesPath);
            var copied = await Files.PasteFromClipboardAsync(_savesPath, copyFile: false, copyDir: true);
            if (copied == 0)
            {
                HintService.Show(Hints.Warning(Lang.Text("Instance.Saves.Empty.Message")));
                return;
            }

            HintService.Show(Hints.Success(Lang.Text("Instance.Saves.PastedCount", copied)));
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Saves.CopyFailed", ex);
        }
    }

    private async Task DeleteSaveAsync(MyListItem item, string folder)
    {
        try
        {
            var saveName = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar));
            var canRecycle = RecycleBin.IsSupported(folder);
            var result = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Instance.Saves.Delete.ConfirmTitle"),
                    Content = Lang.Text(
                        canRecycle
                            ? "Instance.Saves.Delete.ConfirmMessage"
                            : "Instance.Saves.Delete.ConfirmPermanent",
                        saveName),
                    Buttons = DialogButtons.OkCancel,
                    IsWarn = true
                });
            if (!result.IsConfirmed)
                return;

            item.Info = Lang.Text("Instance.Saves.Deleting");
            var (outcome, error) = await Task.Run(
                () =>
                {
                    var moved = RecycleBin.Move(folder, out var reason);
                    return (moved, reason);
                });
            switch (outcome)
            {
                case RecycleBin.Outcome.Moved:
                    HintService.Show(Hints.Success(Lang.Text("Instance.Saves.DeletedToRecycleBin")));
                    break;
                case RecycleBin.Outcome.Unsupported:
                    // 确认阶段已经告知这是不可恢复的永久删除，这里按提示如实执行。
                    await Task.Run(() => Directory.Delete(folder, true));
                    HintService.Show(Hints.Success(Lang.Text("Instance.Saves.DeletedPermanent", saveName)));
                    break;
                default:
                    throw new IOException(error ?? Lang.Text("Instance.Saves.DeleteFailed"));
            }
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Saves.DeleteFailed", ex);
            await ReloadAsync();
        }
    }
}
