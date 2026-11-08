using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.UI.Controls;
using PCL.UI.Controls.Common;

namespace PCL.Views.Pages.Select;

/// <summary>
///     Minecraft 文件夹选择页左栏：文件夹的排序、拖拽、重命名、增删与切换。
///     显示名不允许包含 <c>&gt;</c> 与 <c>|</c>（它们是 <see cref="States.Game" /> 中文件夹列表的分隔符），
///     新增与重命名都走带校验规则的输入弹窗。
/// </summary>
public partial class PageSelectLeft : MyPageLeft, IRefreshable
{
    private const double DragThreshold = 8d;

    private static readonly DataFormat<GameFolders.McFolder> FolderFormat =
        DataFormat.CreateInProcessFormat<GameFolders.McFolder>("pcl/select/mcfolder");

    private readonly List<(GameFolders.McFolder Folder, MyListItem Item)> _rows = [];
    private GameFolders.McFolder? _dragFolder;
    private Point _dragOrigin;

    private PointerPressedEventArgs? _dragTrigger;

    private bool _isBuilding;
    private bool _isDragging;
    private bool _refreshed;

    public PageSelectLeft()
    {
        InitializeComponent();
        AnimatedControl = PanList;
        DataContext = ViewModel;
        GameFolders.McFolderListLoader.PreviewFinish += _ => Dispatcher.UIThread.Post(RefreshList);
        AttachedToVisualTree += (_, _) => RefreshOnNavigate();
        DetachedFromVisualTree += (_, _) => _refreshed = false;
    }

    public PageSelectLeftViewModel ViewModel { get; } = new();

    public void Refresh()
    {
        RefreshCurrent();
    }

    public override void OnNavigatedTo(object? parameter)
    {
        RefreshOnNavigate();
    }

    public override void OnNavigatedFrom()
    {
    }

    protected override void RefreshLocalization()
    {
        ViewModel.InvalidateFolders();
        if (_refreshed)
            McFolderListUi();
    }

    private void RefreshOnNavigate()
    {
        if (_refreshed)
            return;
        _refreshed = true;

        // 页面可能是在文件夹加载器完成前被切走；重新进入时必须重建，
        // 不能只在 IsFirstLoad 时构建一次。
        if (GameFolders.McFolderListLoader.State is LoadState.Waiting
            or LoadState.Failed
            or LoadState.Aborted)
            GameFolders.McFolderListLoader.Start(null, true);
        McFolderListUi();
    }

    private void RefreshList()
    {
        if (_refreshed)
            McFolderListUi();
    }

    private void McFolderListUi()
    {
        var rebuilt = false;
        try
        {
            if (!ViewModel.RefreshFolders())
                return;

            rebuilt = true;
            _isBuilding = true;
            PanList.Children.Clear();
            _rows.Clear();

            PanList.Children.Add(CreateSectionTitle(Lang.Text("Select.Folder.ListTitle")));
            foreach (var folder in ViewModel.Folders)
                PanList.Children.Add(CreateFolderItem(folder));

            PanList.Children.Add(CreateSectionTitle(Lang.Text("Select.Folder.AddOrImport")));
            if (ViewModel.CanCreateFolder)
                PanList.Children.Add(
                    CreateActionItem(
                        "Select.Folder.CreateNew.Title",
                        "lucide/folder-plus",
                        "Select.Folder.CreateNew.ToolTip",
                        PageSelectLeftViewModel.CreateFolder));
            PanList.Children.Add(
                CreateActionItem(
                    "Select.Folder.AddExisting.Title",
                    "lucide/folder-input",
                    "Select.Folder.AddExisting.ToolTip",
                    () => _ = ViewModel.AddExistingFolderAsync()));
            PanList.Children.Add(
                CreateActionItem(
                    "Select.Folder.ImportModpack.Title",
                    "lucide/package-plus",
                    "Select.Folder.ImportModpack.ToolTip",
                    PageSelectLeftViewModel.InstallModpack));
            PanList.Children.Add(
                new Border
                {
                    Height = 10
                });

            foreach (var (folder, item) in _rows)
                item.Checked = folder.Location == ViewModel.SelectedFolder?.Location;
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "构建 Minecraft 文件夹列表 UI 出错");
        }
        finally
        {
            _isBuilding = false;
            // 只有文件夹列表或选中项确实变化时才触发实例列表刷新，
            // 避免每次导航/本地化重建都重复启动加载器。
            if (rebuilt)
                PageSelectLeftViewModel.ReloadInstanceList();
        }
    }

    private Control CreateFolderItem(GameFolders.McFolder folder)
    {
        var item = new MyListItem
        {
            Type = MyListItem.CheckType.RadioBox,
            IsScaleAnimationEnabled = false,
            MinPaddingRight = 30,
            Title = folder.DisplayName,
            Info = folder.Location,
            Height = 40,
            tag = folder
        };
        item.Changed += (sender, e) => FolderChanged((MyListItem)sender, e);
        AttachDragHandlers(item, folder);

        var contextMenu = CreateContextMenu(folder);
        item.ContextMenu = contextMenu;
        ContextMenuHelper.Attach(item, () => contextMenu);
        var menu = new MyIconButton
        {
            SvgIcon = "lucide/settings",
            LogoScale = 1.1
        };
        menu.Click += (_, _) => contextMenu.Open(item);
        item.Buttons = [menu];

        _rows.Add((folder, item));
        LauncherCommon.Log(
            $"[Minecraft] 有效的 Minecraft 文件夹：{folder.DisplayName} > {folder.Location}");
        return item;
    }

    private static TextBlock CreateSectionTitle(string text)
    {
        var title = new TextBlock
        {
            Text = text
        };
        title.Classes.Add("SectionTitle");
        return title;
    }

    private static Control CreateActionItem(
        string titleKey,
        string svgIcon,
        string toolTipKey,
        Action action)
    {
        var item = new MyListItem
        {
            Type = MyListItem.CheckType.Clickable,
            IsScaleAnimationEnabled = false,
            Title = Lang.Text(titleKey),
            Height = 34,
            LogoScale = 0.9,
            SvgIcon = svgIcon
        };
        ToolTip.SetTip(item, Lang.Text(toolTipKey));
        ToolTip.SetPlacement(item, PlacementMode.Right);
        item.Click += (_, _) => action();
        return item;
    }

    #region 右键菜单

    private ContextMenu CreateContextMenu(GameFolders.McFolder folder)
    {
        var items = new List<MyMenuItem>();

        if (folder.Type == GameFolders.McFolder.Types.RenamedOriginal)
            AddItem(
                "Select.Folder.RestoreName",
                "lucide/rotate-ccw",
                () => PageSelectLeftViewModel.RestoreName(folder));
        AddItem(
            "Select.Folder.Rename",
            "lucide/pencil",
            () => _ = ViewModel.RenameFolderAsync(folder));
        var canMoveUp = PageSelectLeftViewModel.CanMoveUp(folder);
        AddItem(
            "Select.Folder.MoveUp",
            "lucide/move-up",
            () => PageSelectLeftViewModel.MoveUp(folder),
            canMoveUp,
            canMoveUp);
        var canMoveDown = PageSelectLeftViewModel.CanMoveDown(folder);
        AddItem(
            "Select.Folder.MoveDown",
            "lucide/move-down",
            () => PageSelectLeftViewModel.MoveDown(folder),
            canMoveDown,
            canMoveDown);
        AddItem(
            "Common.Action.Open",
            "lucide/folder-open",
            () => PageSelectLeftViewModel.OpenFolder(folder));
        AddItem(
            "Common.Action.Refresh",
            "lucide/refresh-cw",
            () => PageSelectLeftViewModel.RefreshFolder(folder));
        if (folder.Type == GameFolders.McFolder.Types.Custom)
            AddItem(
                "Select.Folder.RemoveFromList",
                "lucide/list-x",
                () => _ = ViewModel.RemoveFolderAsync(folder));
        AddItem(
            PageSelectLeftViewModel.GetDeleteMenuKey(folder),
            "lucide/trash-2",
            () => _ = ViewModel.DeleteFolderAsync(folder));

        var menu = new ContextMenu
        {
            ItemsSource = items
        };
        menu.Classes.Add("NoScroll");
        return menu;

        void AddItem(
            string headerKey,
            string svgIcon,
            Action action,
            bool enabled = true,
            bool visible = true)
        {
            var item = new MyMenuItem
            {
                Header = Lang.Text(headerKey),
                SvgIcon = svgIcon,
                IsEnabled = enabled,
                IsVisible = visible
            };
            item.Click += (_, _) => action();
            items.Add(item);
        }
    }

    #endregion

    #region 拖拽排序

    private void AttachDragHandlers(MyListItem item, GameFolders.McFolder folder)
    {
        item.PointerPressed += (_, e) => OnItemPointerPressed(item, folder, e);
        item.PointerMoved += (_, e) => OnItemPointerMoved(item, e);
        item.PointerReleased += (_, _) => ResetDrag();
        DragDrop.SetAllowDrop(item, true);
        DragDrop.AddDragEnterHandler(item, (sender, _) => ((Control)sender!).Opacity = 0.7d);
        DragDrop.AddDragLeaveHandler(item, (sender, _) => ((Control)sender!).Opacity = 1d);
        DragDrop.AddDragOverHandler(
            item,
            (_, e) =>
            {
                e.DragEffects = e.DataTransfer.Contains(FolderFormat)
                    ? DragDropEffects.Move
                    : DragDropEffects.None;
                e.Handled = true;
            });
        DragDrop.AddDropHandler(item, OnItemDrop);
    }

    private void OnItemPointerPressed(
        MyListItem item,
        GameFolders.McFolder folder,
        PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(item).Properties.IsLeftButtonPressed)
            return;
        _dragTrigger = e;
        _dragFolder = folder;
        _dragOrigin = e.GetPosition(item);
    }

    private async void OnItemPointerMoved(MyListItem item, PointerEventArgs e)
    {
        if (_isDragging || _dragTrigger is null || _dragFolder is null)
            return;
        if (!e.GetCurrentPoint(item).Properties.IsLeftButtonPressed)
        {
            ResetDrag();
            return;
        }

        var delta = e.GetPosition(item) - _dragOrigin;
        if (Math.Abs(delta.X) < DragThreshold && Math.Abs(delta.Y) < DragThreshold)
            return;

        var trigger = _dragTrigger;
        var folder = _dragFolder;
        _isDragging = true;
        try
        {
            using var transfer = new DataTransfer();
            transfer.Add(DataTransferItem.Create(FolderFormat, folder));
            await DragDrop.DoDragDropAsync(trigger, transfer, DragDropEffects.Move);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Select.Folder.Error.DragDrop"),
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Select.Folder.Error.DragDrop"));
        }
        finally
        {
            _isDragging = false;
            ResetDrag();
            item.Opacity = 1d;
        }
    }

    private void ResetDrag()
    {
        _dragTrigger = null;
        _dragFolder = null;
        _isDragging = false;
    }

    private void OnItemDrop(object? sender, DragEventArgs e)
    {
        try
        {
            var target = (MyListItem)sender!;
            target.Opacity = 1d;

            var sourceFolder = e.DataTransfer.TryGetValue(FolderFormat);
            if (sourceFolder is null)
                return;
            if (target.tag is not GameFolders.McFolder targetFolder)
                return;
            PageSelectLeftViewModel.ReorderFolders(sourceFolder, targetFolder);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Select.Folder.Error.DragDrop"),
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Select.Folder.Error.DragDrop"));
        }
        finally
        {
            e.Handled = true;
        }
    }

    #endregion

    #region 文件夹操作

    public static void RefreshCurrent()
    {
        PageSelectLeftViewModel.RefreshCurrent();
    }

    public static void RefreshCurrent(string folder)
    {
        PageSelectLeftViewModel.RefreshCurrent(folder);
    }

    public static void AddFolder(string folderPath, string displayName, bool showHint)
    {
        PageSelectLeftViewModel.AddFolder(folderPath, displayName, showHint);
    }

    private void FolderChanged(MyListItem sender, RouteEventArgs e)
    {
        if (_isBuilding || !e.raiseByMouse || !sender.Checked)
            return;
        if (sender.tag is not GameFolders.McFolder folder)
            return;
        if (folder.Location == GameFolders.mcFolderSelected)
            return;

        if (ViewModel.SelectFolder(folder))
            return;

        _isBuilding = true;
        foreach (var (rowFolder, item) in _rows)
            item.Checked = rowFolder.Location == GameFolders.mcFolderSelected;
        _isBuilding = false;
        e.handled = true;
    }

    #endregion
}