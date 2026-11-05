using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.Core.Utils.Validate;
using PCL.Network;
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
    private bool _isFirstLoad = true;
    private List<GameFolders.McFolder>? _mcFolderListLast;
    private bool _refreshed;

    public PageSelectLeft()
    {
        InitializeComponent();
        AnimatedControl = PanList;
        GameFolders.McFolderListLoader.PreviewFinish += _ => Dispatcher.UIThread.Post(RefreshList);
        AttachedToVisualTree += (_, _) => RefreshOnNavigate();
        DetachedFromVisualTree += (_, _) => _refreshed = false;
    }

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
        _mcFolderListLast = null;
        if (_refreshed)
            McFolderListUi();
    }

    private void RefreshOnNavigate()
    {
        if (_refreshed)
            return;
        _refreshed = true;
        if (_isFirstLoad)
            McFolderListUi();
        _isFirstLoad = false;
    }

    private void RefreshList()
    {
        if (_refreshed)
            McFolderListUi();
    }

    private void McFolderListUi()
    {
        try
        {
            if (_mcFolderListLast is not null
                && _mcFolderListLast.SequenceEqual(GameFolders.mcFolderList))
                return;

            _mcFolderListLast = [.. GameFolders.mcFolderList];
            _isBuilding = true;
            PanList.Children.Clear();
            _rows.Clear();

            PanList.Children.Add(CreateSectionTitle(Lang.Text("Select.Folder.ListTitle")));
            foreach (var folder in GameFolders.mcFolderList)
                PanList.Children.Add(CreateFolderItem(folder));

            PanList.Children.Add(CreateSectionTitle(Lang.Text("Select.Folder.AddOrImport")));
            if (!Directory.Exists(Path.Combine(LauncherPaths.ExecutableDirectory, ".minecraft")))
                PanList.Children.Add(
                    CreateActionItem(
                        "Select.Folder.CreateNew.Title",
                        "lucide/folder-plus",
                        "Select.Folder.CreateNew.ToolTip",
                        CreateFolder));
            PanList.Children.Add(
                CreateActionItem(
                    "Select.Folder.AddExisting.Title",
                    "lucide/folder-input",
                    "Select.Folder.AddExisting.ToolTip",
                    () => _ = AddExistingFolderAsync()));
            PanList.Children.Add(
                CreateActionItem(
                    "Select.Folder.ImportModpack.Title",
                    "lucide/package-plus",
                    "Select.Folder.ImportModpack.ToolTip",
                    Modpack.ModpackInstall));
            PanList.Children.Add(
                new Border
                {
                    Height = 10
                });

            var selected = GameFolders.mcFolderList.FirstOrDefault(folder =>
                folder.Location == GameFolders.mcFolderSelected);
            if (selected is null && GameFolders.mcFolderList.Count > 0)
            {
                selected = GameFolders.mcFolderList[0];
                States.Game.SelectedFolder = selected.Location
                    .Replace(LauncherPaths.ExecutableDirectory, "$");
            }

            foreach (var (folder, item) in _rows)
                item.Checked = folder.Location == selected?.Location;
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "构建 Minecraft 文件夹列表 UI 出错");
        }
        finally
        {
            _isBuilding = false;
            ReloadInstanceList();
        }
    }

    private static void ReloadInstanceList()
    {
        if (string.IsNullOrEmpty(GameFolders.mcFolderSelected))
            return;
        Loading.LoaderFolderRun(
            InstanceList.mcInstanceListLoader,
            GameFolders.mcFolderSelected,
            Loading.LoaderFolderRunType.RunOnUpdated,
            1,
            $"versions{Path.DirectorySeparatorChar}");
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
            if (ReferenceEquals(sourceFolder, targetFolder))
                return;
            if (!GameFolders.mcFolderList.Contains(sourceFolder)
                || !GameFolders.mcFolderList.Contains(targetFolder))
                return;

            var sourceIndex = GameFolders.mcFolderList.IndexOf(sourceFolder);
            var targetIndex = GameFolders.mcFolderList.IndexOf(targetFolder);
            if (sourceIndex < 0 || targetIndex < 0 || sourceIndex == targetIndex)
                return;

            GameFolders.mcFolderList.RemoveAt(sourceIndex);
            targetIndex = Math.Clamp(targetIndex, 0, GameFolders.mcFolderList.Count);
            GameFolders.mcFolderList.Insert(targetIndex, sourceFolder);
            UpdateFolderOrder();

            LauncherCommon.Log(
                $"[Control] 文件夹拖拽排序：{sourceFolder.DisplayName} -> 位置 {targetIndex}"
                + $"（目标 {targetFolder.DisplayName}）",
                LauncherLogLevel.Debug);
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

    #region 右键菜单

    private ContextMenu CreateContextMenu(GameFolders.McFolder folder)
    {
        var items = new List<MyMenuItem>();

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

        var index = GameFolders.mcFolderList.IndexOf(folder);
        if (folder.Type == GameFolders.McFolder.Types.RenamedOriginal)
            AddItem(
                "Select.Folder.RestoreName",
                "lucide/rotate-ccw",
                () => RestoreFolderName(folder));
        AddItem("Select.Folder.Rename", "lucide/pencil", () => _ = RenameFolderAsync(folder));
        AddItem(
            "Select.Folder.MoveUp",
            "lucide/move-up",
            () => MoveFolder(folder, -1),
            index > 0,
            index > 0);
        AddItem(
            "Select.Folder.MoveDown",
            "lucide/move-down",
            () => MoveFolder(folder, 1),
            index >= 0 && index < GameFolders.mcFolderList.Count - 1,
            index >= 0 && index < GameFolders.mcFolderList.Count - 1);
        AddItem("Common.Action.Open", "lucide/folder-open", () => Basics.OpenPath(folder.Location));
        AddItem(
            "Common.Action.Refresh",
            "lucide/refresh-cw",
            () => RefreshCurrent(folder.Location));
        if (folder.Type == GameFolders.McFolder.Types.Custom)
            AddItem(
                "Select.Folder.RemoveFromList",
                "lucide/list-x",
                () => _ = RemoveFolderAsync(folder));
        AddItem(GetDeleteMenuKey(folder), "lucide/trash-2", () => _ = DeleteFolderAsync(folder));

        var menu = new ContextMenu
        {
            ItemsSource = items
        };
        return menu;
    }

    private static string GetDeleteMenuKey(GameFolders.McFolder folder)
    {
        return GameFolders.mcFolderList.Count == 1
            && folder.Location
            == Path.Combine(LauncherPaths.ExecutableDirectory, ".minecraft")
            + Path.DirectorySeparatorChar
                ? "Select.Folder.Clear"
                : "Common.Action.Delete";
    }

    private static bool IsClearingDefaultFolder(GameFolders.McFolder folder)
    {
        return folder.Type is GameFolders.McFolder.Types.Original
                or GameFolders.McFolder.Types.RenamedOriginal
            && folder.Location
            == Path.Combine(LauncherPaths.ExecutableDirectory, ".minecraft")
            + Path.DirectorySeparatorChar
            && GameFolders.mcFolderList.Count == 1;
    }

    #endregion

    #region 文件夹操作

    private static void MoveFolder(GameFolders.McFolder folder, int delta)
    {
        var index = GameFolders.mcFolderList.IndexOf(folder);
        var newIndex = index + delta;
        if (index < 0 || newIndex < 0 || newIndex >= GameFolders.mcFolderList.Count)
            return;
        GameFolders.mcFolderList.RemoveAt(index);
        GameFolders.mcFolderList.Insert(newIndex, folder);
        UpdateFolderOrder();
    }

    private static void RestoreFolderName(GameFolders.McFolder folder)
    {
        var index = GameFolders.mcFolderList.IndexOf(folder);
        if (index < 0)
            return;

        var separator = Path.DirectorySeparatorChar;
        var location = folder.Location;
        var minecraftFolder =
            Path.Combine(LauncherPaths.ExecutableDirectory, ".minecraft") + separator;
        var mojangFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                ".minecraft")
            + separator;
        string name;
        if (location == minecraftFolder
            || location == LauncherPaths.ExecutableDirectory + separator)
            name = GameFolders.McFolder.BuiltinNamePrefix + "Select.Folder.CurrentFolder";
        else if (location == mojangFolder)
            name = GameFolders.McFolder.BuiltinNamePrefix + "Select.Folder.OfficialLauncherFolder";
        else
            name = Path.GetFileName(location.TrimEnd(separator));

        GameFolders.mcFolderList[index].Type = GameFolders.McFolder.Types.Original;
        GameFolders.mcFolderList[index].Name = name;
        UpdateFolderOrder();
    }

    private static void UpdateFolderOrder()
    {
        States.Game.Folders = string.Join(
            "|",
            GameFolders.mcFolderList.Select(folder => $"{folder.Name}>{folder.Location}"));
        GameFolders.McFolderListLoader.Start(null, true);
    }

    private static string? ValidateFolderName(string? input)
    {
        var name = input?.Trim();
        if (string.IsNullOrWhiteSpace(name)
            || name.Contains('>')
            || name.Contains('|'))
        {
            HintService.Show(Hints.Error(Lang.Text("Select.Folder.Invalid", name ?? "")));
            return null;
        }

        return name;
    }

    private async Task RenameFolderAsync(GameFolders.McFolder folder)
    {
        try
        {
            var dialog = await DialogService.Instance.ShowAsync(
                new InputDialogRequest
                {
                    Title = Lang.Text("Select.Folder.Rename.Title"),
                    DefaultValue = folder.Name,
                    ValidateRules =
                    [
                        new NullOrWhiteSpaceValidator(),
                        new StringLengthValidator(1, 30),
                        new BlacklistValidator([">", "|"])
                    ],
                    Buttons = DialogButtons.OkCancel,
                    HighLight = false
                });
            if (!dialog.IsConfirmed)
                return;
            var newName = ValidateFolderName(dialog.InputText);
            if (newName is null)
                return;

            var folders = (States.Game.Folders ?? "")
                .Split('|', StringSplitOptions.RemoveEmptyEntries)
                .ToList();
            var isAdded = false;
            for (var i = 0; i < folders.Count; i++)
            {
                var parts = folders[i].Split('>');
                if (parts.Length < 2 || parts[1] != folder.Location)
                    continue;
                isAdded = true;
                if (parts[0] == newName)
                    return;
                folders[i] = $"{newName}>{folder.Location}";
                break;
            }

            if (!isAdded)
                folders.Add($"{newName}>{folder.Location}");
            States.Game.Folders = string.Join("|", folders);
            HintService.Show(Hints.Success(Lang.Text("Select.Folder.NameUpdated", newName)));
            GameFolders.McFolderListLoader.Start(null, true);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Select.Folder.Error.Rename"),
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Select.Folder.Error.Rename"));
        }
    }

    private async Task RemoveFolderAsync(GameFolders.McFolder folder)
    {
        try
        {
            var dialog = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Select.Folder.Cleanup.Title"),
                    Content = Lang.Text("Select.Folder.Cleanup.Message"),
                    Buttons = DialogButtons.Create(
                        "Common.Action.Delete",
                        "Select.Folder.Cleanup.Keep",
                        "Common.Action.Cancel")
                });

            switch (dialog.Button)
            {
                case 1:
                    if (File.Exists(Path.Combine(folder.Location, "PCL.ini")))
                        File.Delete(Path.Combine(folder.Location, "PCL.ini"));
                    var versionsFolder = Path.Combine(folder.Location, "versions");
                    if (Directory.Exists(versionsFolder))
                        foreach (var version in new DirectoryInfo(versionsFolder)
                                     .EnumerateDirectories())
                        {
                            var pclFolder = Path.Combine(version.FullName, "PCL");
                            if (Directory.Exists(pclFolder))
                                Directory.Delete(pclFolder, true);
                        }

                    break;

                case 2:
                    break;

                default:
                    return;
            }

            var folders = (States.Game.Folders ?? "")
                .Split('|', StringSplitOptions.RemoveEmptyEntries)
                .ToList();
            var name = "";
            for (var i = folders.Count - 1; i >= 0; i--)
            {
                if (!folders[i].EndsWith(folder.Location))
                    continue;
                name = folders[i].Split('>')[0];
                folders.RemoveAt(i);
                break;
            }

            States.Game.Folders = folders.Count == 0 ? "" : string.Join("|", folders);
            HintService.Show(
                Hints.Success(
                    folder.Type == GameFolders.McFolder.Types.Custom
                        ? Lang.Text("Select.Folder.RemoveSuccess", name)
                        : Lang.Text("Select.Folder.RestoreSuccess")));
            GameFolders.McFolderListLoader.Start(null, true);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Select.Folder.Error.Remove"),
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Select.Folder.Error.Remove"));
        }
    }

    private async Task DeleteFolderAsync(GameFolders.McFolder folder)
    {
        var isClearing = IsClearingDefaultFolder(folder);
        var deleteText = Lang.Text(isClearing ? "Select.Folder.Clear" : "Common.Action.Delete");
        var firstWarning = Lang.Text(
            isClearing ? "Select.Folder.Clear.FirstWarning" : "Select.Folder.Delete.FirstWarning",
            folder.Location);
        var finalWarning = Lang.Text(
            isClearing ? "Select.Folder.Clear.FinalWarning" : "Select.Folder.Delete.FinalWarning",
            folder.Location);
        var confirmTitle = Lang.Text(
            isClearing ? "Select.Folder.Clear.Confirm" : "Select.Folder.Delete.Confirm");
        var inProgress = Lang.Text(
            isClearing ? "Select.Folder.Clear.InProgress" : "Select.Folder.Delete.InProgress",
            folder.DisplayName);
        var success = Lang.Text(
            isClearing ? "Select.Folder.Clear.Success" : "Select.Folder.Delete.Success",
            folder.DisplayName);

        try
        {
            var firstDialog = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Select.Folder.Delete.WarningTitle"),
                    Content = firstWarning,
                    Buttons = DialogButtons.Create(
                        "Common.Action.Cancel",
                        "Common.Action.Confirm",
                        "Common.Action.Cancel")
                });
            if (firstDialog.Button != 2)
                return;

            var finalDialog = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Select.Folder.Delete.WarningTitle"),
                    Content = finalWarning,
                    Buttons = DialogButtons.From(
                        DialogButton.Create(confirmTitle),
                        DialogButton.Create(Lang.Text("Common.Action.Cancel"))),
                    IsWarn = true
                });
            if (finalDialog.Button != 1)
                return;
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Select.Folder.Error.Operate", deleteText, folder.DisplayName));
            return;
        }

        var folders = (States.Game.Folders ?? "")
            .Split('|', StringSplitOptions.RemoveEmptyEntries)
            .ToList();
        var index = folders.FindIndex(entry => entry.EndsWith(folder.Location));
        if (index >= 0)
            folders.RemoveAt(index);
        States.Game.Folders = string.Join("|", folders);

        LauncherCommon.RunInNewThread(
            () =>
            {
                try
                {
                    HintService.Show(Hints.Info(inProgress));
                    LauncherCommon.DeleteDirectory(folder.Location);
                    if (isClearing)
                        Directory.CreateDirectory(folder.Location);
                    HintService.Show(Hints.Success(success));
                }
                catch (Exception ex)
                {
                    LauncherCommon.Log(
                        ex,
                        Lang.Text("Select.Folder.Error.Operate", deleteText, folder.DisplayName),
                        LauncherLogLevel.Hint,
                        userSummary: Lang.Text(
                            "Select.Folder.Error.Operate",
                            deleteText,
                            folder.DisplayName));
                }
                finally
                {
                    GameFolders.McFolderListLoader.Start(null, true);
                }
            },
            "Folder Delete " + LauncherCommon.GetUuid(),
            ThreadPriority.BelowNormal);
    }

    public static void RefreshCurrent()
    {
        RefreshCurrent(GameFolders.mcFolderSelected ?? "");
    }

    public static void RefreshCurrent(string folder)
    {
        if (string.IsNullOrEmpty(folder))
            return;
        LauncherCommon.WriteIni(Path.Combine(folder, "PCL.ini"), "InstanceCache", "");
        if (folder == GameFolders.mcFolderSelected)
            ReloadInstanceList();
    }

    private void FolderChanged(MyListItem sender, RouteEventArgs e)
    {
        if (_isBuilding || !e.raiseByMouse || !sender.Checked)
            return;
        if (sender.tag is not GameFolders.McFolder folder)
            return;
        if (folder.Location == GameFolders.mcFolderSelected)
            return;

        if (NetFacade.HasDownloadingTask(true))
        {
            HintService.Show(Hints.Error(Lang.Text("Select.Folder.SwitchBlockedByDownload")));
            _isBuilding = true;
            foreach (var (rowFolder, item) in _rows)
                item.Checked = rowFolder.Location == GameFolders.mcFolderSelected;
            _isBuilding = false;
            e.handled = true;
            return;
        }

        States.Game.SelectedFolder = folder.Location
            .Replace(LauncherPaths.ExecutableDirectory, "$");
        GameFolders.McFolderListLoader.Start(null, true);
        ReloadInstanceList();
    }

    private void CreateFolder()
    {
        if (NetFacade.HasDownloadingTask())
        {
            HintService.Show(Hints.Error(Lang.Text("Select.Folder.CannotCreateWhileDownloading")));
            return;
        }

        var minecraftFolder = Path.Combine(LauncherPaths.ExecutableDirectory, ".minecraft")
            + Path.DirectorySeparatorChar;
        if (!Directory.Exists(minecraftFolder))
        {
            Directory.CreateDirectory(minecraftFolder);
            Directory.CreateDirectory(Path.Combine(minecraftFolder, "versions"));
            States.Game.SelectedFolder = "$.minecraft" + Path.DirectorySeparatorChar;
            GameFolders.McFolderLauncherProfilesJsonCreate(minecraftFolder);
            HintService.Show(Hints.Success(Lang.Text("Select.Folder.CreateSuccess")));
        }

        GameFolders.McFolderListLoader.Start(null, true);
    }

    private async Task AddExistingFolderAsync()
    {
        var newFolder = "";
        if (NetFacade.HasDownloadingTask())
        {
            HintService.Show(Hints.Error(Lang.Text("Select.Folder.CannotAddWhileDownloading")));
            return;
        }

        try
        {
            newFolder = SystemDialogs.SelectFolder();
            if (string.IsNullOrEmpty(newFolder))
                return;
            if (newFolder.Contains('!') || newFolder.Contains(';'))
            {
                HintService.Show(Hints.Error(Lang.Text("Select.Folder.InvalidPathChars")));
                return;
            }

            var separator = Path.DirectorySeparatorChar;
            var segments = newFolder
                .TrimEnd(separator)
                .Split(separator);
            var defaultName = segments[^1] == ".minecraft"
                ? segments.Length >= 3 ? segments[^2] : ""
                : segments[^1];
            if (defaultName.Length > 40)
                defaultName = defaultName[..39];

            var dialog = await DialogService.Instance.ShowAsync(
                new InputDialogRequest
                {
                    Title = Lang.Text("Select.Folder.InputDisplayName.Title"),
                    Content = Lang.Text("Select.Folder.InputDisplayName.Message"),
                    DefaultValue = defaultName,
                    ValidateRules =
                    [
                        new NullOrWhiteSpaceValidator(),
                        new StringLengthValidator(),
                        new BlacklistValidator([">", "|"])
                    ],
                    HighLight = false
                });
            if (!dialog.IsConfirmed)
                return;
            var newName = ValidateFolderName(dialog.InputText);
            if (newName is null)
                return;

            AddFolder(newFolder, newName, true);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Select.Folder.Error.Add", newFolder),
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Select.Folder.Error.Add", newFolder));
        }
    }

    public static void AddFolder(string folderPath, string displayName, bool showHint)
    {
        LauncherCommon.RunInThread(() =>
        {
            try
            {
                if (!folderPath.EndsWith(Path.DirectorySeparatorChar))
                    folderPath += Path.DirectorySeparatorChar;
                if (!CheckPermission(folderPath))
                {
                    if (!showHint)
                        throw new Exception("PCL 没有访问文件夹的权限：" + folderPath);
                    HintService.Show(Hints.Error(Lang.Text("Select.Folder.AccessDenied")));
                    return;
                }

                if (!CheckPermission(Path.Combine(folderPath, "versions")))
                    foreach (var folder in new DirectoryInfo(folderPath).GetDirectories())
                        if (CheckPermission(Path.Combine(folder.FullName, "versions")))
                        {
                            folderPath = folder.FullName + Path.DirectorySeparatorChar;
                            break;
                        }

                var folders = new List<string>((States.Game.Folders ?? "").Split('|'));
                var isAdded = false;
                var isReplace = false;
                for (var i = 0; i < folders.Count; i++)
                {
                    var folder = folders[i];
                    if (string.IsNullOrEmpty(folder))
                        continue;
                    var parts = folder.Split('>');
                    if (parts.Length < 2 || parts[1] != folderPath)
                        continue;
                    isAdded = true;
                    if (parts[0] == displayName)
                    {
                        if (showHint)
                            HintService.Show(Hints.Info(Lang.Text("Select.Folder.AlreadyInList")));
                        return;
                    }

                    folders[i] = $"{displayName}>{folderPath}";
                    isReplace = true;
                    if (showHint)
                        HintService.Show(
                            Hints.Success(Lang.Text("Select.Folder.NameUpdated", displayName)));
                    break;
                }

                if (!isAdded)
                    folders.Add($"{displayName}>{folderPath}");
                States.Game.Folders = string.Join("|", folders);
                States.Game.SelectedFolder = folderPath
                    .Replace(LauncherPaths.ExecutableDirectory, "$");
                GameFolders.McFolderListLoader.Start(null, true);
                if (isReplace)
                    return;
                if (showHint)
                    HintService.Show(Hints.Success(Lang.Text("Select.Folder.Added", displayName)));

                var modFolder = new DirectoryInfo(Path.Combine(folderPath, "mods"));
                if (!(modFolder.Exists && modFolder.EnumerateFiles().Count() >= 3))
                    return;
                var versionFolder = new DirectoryInfo(Path.Combine(folderPath, "versions"));
                if (!(versionFolder.Exists && versionFolder.EnumerateDirectories().Count() <= 3))
                    return;
                foreach (var versionPath in versionFolder.EnumerateDirectories())
                {
                    var version = new McInstance(versionPath.FullName);
                    version.Load();
                    if (!version.Modable)
                        continue;
                    var modIndieFolder = new DirectoryInfo(
                        Path.Combine(version.PathInstance, "mods"));
                    if (modIndieFolder.Exists && modIndieFolder.EnumerateFiles().Any())
                        return;
                    Config.Instance.IndieV1[version.PathInstance] = 2;
                    Config.Instance.IndieV2[version.PathInstance] = false;
                    LauncherCommon.Log(
                        "[Setup] 已自动关闭单版本隔离：" + version.Name,
                        LauncherLogLevel.Debug);
                }
            }
            catch (Exception ex)
            {
                LauncherCommon.Log(
                    ex,
                    Lang.Text("Select.Folder.Error.AddNew"),
                    LauncherLogLevel.Feedback,
                    userSummary: Lang.Text("Select.Folder.Error.AddNew"));
            }
        });
    }

    private static bool CheckPermission(string path)
    {
        try
        {
            LauncherCommon.CheckPermissionWithException(path);
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion
}