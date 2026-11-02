using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.Network;

namespace PCL.Views.Pages;

/// <summary>
///     Minecraft 文件夹选择页左侧栏：文件夹的排序、重命名、增删与切换。
///     与 WPF 版 PageSelectLeft 对齐；拖拽排序依赖 Avalonia 的 DragDrop 接线，留待后续迁移。
// TODO: 拖拽排序、右键菜单图标（SvgIcon 体系）尚未实现，当前用 "⋯" 按钮替代。
/// </summary>
public partial class PageSelectLeft : PageBase
{
    private readonly StackPanel? _panList;
    private readonly List<(GameFolders.McFolder Folder, RadioButton Radio)> _rows = [];

    private bool _isBuilding;
    private bool _isFirstLoad = true;
    private bool _refreshed;
    private List<GameFolders.McFolder>? _mcFolderListLast;

    public PageSelectLeft()
    {
        InitializeComponent();
        _panList = this.FindControl<StackPanel>("PanList");
        GameFolders.McFolderListLoader.PreviewFinish += _ => Dispatcher.UIThread.Post(RefreshList);
        AttachedToVisualTree += (_, _) => RefreshOnNavigate();
        DetachedFromVisualTree += (_, _) => _refreshed = false;
    }

    public override void OnNavigatedTo(object? parameter)
    {
        RefreshOnNavigate();
    }

    private void RefreshOnNavigate()
    {
        if (_refreshed)
            return;
        _refreshed = true;
        if (_isFirstLoad)
            McFolderListUI();
        _isFirstLoad = false;
    }

    private void RefreshList()
    {
        if (_refreshed)
            McFolderListUI();
    }

    private void McFolderListUI()
    {
        try
        {
            if (_panList is null)
                return;
            if (_mcFolderListLast is not null
                && _mcFolderListLast.SequenceEqual(GameFolders.mcFolderList))
                return;

            _mcFolderListLast = [.. GameFolders.mcFolderList];
            _isBuilding = true;
            _panList.Children.Clear();
            _rows.Clear();

            _panList.Children.Add(_CreateSectionTitle(Lang.Text("Select.Folder.ListTitle")));
            for (var i = 0; i < GameFolders.mcFolderList.Count; i++)
                _panList.Children.Add(_CreateFolderItem(GameFolders.mcFolderList[i]));

            _panList.Children.Add(_CreateSectionTitle(Lang.Text("Select.Folder.AddOrImport")));
            if (!Directory.Exists(
                    Path.Combine(LauncherPaths.ExecutableDirectory, ".minecraft")))
                _panList.Children.Add(
                    _CreateActionItem("Select.Folder.CreateNew.Title", CreateFolder));
            _panList.Children.Add(
                _CreateActionItem(
                    "Select.Folder.AddExisting.Title",
                    () => _ = AddExistingFolderAsync()));
            _panList.Children.Add(
                _CreateActionItem("Select.Folder.ImportModpack.Title", Modpack.ModpackInstall));
            _panList.Children.Add(
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

            foreach (var (folder, radio) in _rows)
                radio.IsChecked = folder.Location == selected?.Location;
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

    private Control _CreateFolderItem(GameFolders.McFolder folder)
    {
        var radio = new RadioButton
        {
            GroupName = "SelectLeftFolders",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        radio.IsCheckedChanged += (_, _) =>
        {
            if (radio.IsChecked == true && !_isBuilding)
                FolderChanged(folder);
        };
        _rows.Add((folder, radio));

        var title = new TextBlock
        {
            Text = folder.DisplayName
        };
        title.Classes.Add("RowTitle");
        var location = new TextBlock
        {
            Text = folder.Location
        };
        location.Classes.Add("RowInfo");
        var textPanel = new StackPanel
        {
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center
        };
        textPanel.Children.Add(title);
        textPanel.Children.Add(location);

        var menuButton = new Button
        {
            Content = "\u22EF",
            VerticalAlignment = VerticalAlignment.Center
        };
        menuButton.Classes.Add("PageIconButton");
        menuButton.Click += (_, _) => OpenContextMenu(folder, menuButton);

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto")
        };
        Grid.SetColumn(radio, 0);
        Grid.SetColumn(textPanel, 1);
        Grid.SetColumn(menuButton, 2);
        grid.Children.Add(radio);
        grid.Children.Add(textPanel);
        grid.Children.Add(menuButton);

        var row = new Border
        {
            Child = grid,
            Padding = new Thickness(8, 6)
        };
        row.Classes.Add("PageListItem");
        return row;
    }

    private static TextBlock _CreateSectionTitle(string text)
    {
        var title = new TextBlock
        {
            Text = text
        };
        title.Classes.Add("SectionTitle");
        return title;
    }

    private static Control _CreateActionItem(string titleKey, Action action)
    {
        var title = new TextBlock
        {
            Text = Lang.Text(titleKey)
        };
        title.Classes.Add("RowTitle");
        var row = new Border
        {
            Child = title,
            Padding = new Thickness(13, 8),
            Margin = new Thickness(4, 1)
        };
        row.Classes.Add("PageListItem");
        row.PointerPressed += (_, _) => action();
        return row;
    }

    private void OpenContextMenu(GameFolders.McFolder folder, Control anchor)
    {
        var items = new List<MenuItem>();

        void AddItem(string headerKey, Action action, bool enabled = true)
        {
            var item = new MenuItem
            {
                Header = Lang.Text(headerKey),
                IsEnabled = enabled
            };
            item.Click += (_, _) => action();
            items.Add(item);
        }

        if (folder.Type == GameFolders.McFolder.Types.RenamedOriginal)
            AddItem("Select.Folder.RestoreName", () => RestoreFolderName(folder));

        AddItem("Select.Folder.Rename", () => _ = RenameFolderAsync(folder));
        AddItem(
            "Select.Folder.MoveUp",
            () => MoveFolder(folder, -1),
            GameFolders.mcFolderList.IndexOf(folder) > 0);
        AddItem(
            "Select.Folder.MoveDown",
            () => MoveFolder(folder, 1),
            GameFolders.mcFolderList.IndexOf(folder) is var index
            && index >= 0
            && index < GameFolders.mcFolderList.Count - 1);
        AddItem("Common.Action.Open", () => Basics.OpenPath(folder.Location));
        AddItem("Common.Action.Refresh", () => RefreshCurrent(folder.Location));

        if (folder.Type == GameFolders.McFolder.Types.Custom)
            AddItem("Select.Folder.RemoveFromList", () => _ = RemoveFolderAsync(folder));
        AddItem(GetDeleteMenuText(folder), () => _ = DeleteFolderAsync(folder));

        var menu = new ContextMenu
        {
            ItemsSource = items
        };
        menu.Open(anchor);
    }

    private static string GetDeleteMenuText(GameFolders.McFolder folder)
    {
        return GameFolders.mcFolderList.Count == 1
            && folder.Location
            == Path.Combine(LauncherPaths.ExecutableDirectory, ".minecraft")
            + Path.DirectorySeparatorChar
                ? Lang.Text("Select.Folder.Clear")
                : Lang.Text("Common.Action.Delete");
    }

    private static bool IsClearingDefaultFolder(GameFolders.McFolder folder)
    {
        return folder.Type
                is GameFolders.McFolder.Types.Original
                or GameFolders.McFolder.Types.RenamedOriginal
            && folder.Location
            == Path.Combine(LauncherPaths.ExecutableDirectory, ".minecraft")
            + Path.DirectorySeparatorChar
            && GameFolders.mcFolderList.Count == 1;
    }

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
        GameFolders.mcFolderList[index].Type = GameFolders.McFolder.Types.Original;
        GameFolders.mcFolderList[index].Name = Lang.Text("Select.Folder.OfficialLauncherFolder");
        UpdateFolderOrder();
    }

    private static void UpdateFolderOrder()
    {
        States.Game.Folders = string.Join(
            "|",
            GameFolders.mcFolderList.Select(folder => $"{folder.Name}>{folder.Location}"));
        GameFolders.McFolderListLoader.Start(null, true);
    }

    private async Task RenameFolderAsync(GameFolders.McFolder folder)
    {
        try
        {
            var dialog = await DialogService.Instance.ShowAsync(
                new InputDialogRequest
                {
                    Title = Lang.Text("Select.Folder.Rename.Title"),
                    DefaultValue = folder.Name
                });
            if (!dialog.IsConfirmed || string.IsNullOrWhiteSpace(dialog.InputText))
                return;

            var newName = dialog.InputText.Trim();
            if (newName.Contains('>') || newName.Contains('|'))
            {
                HintService.Show(Hints.Error(Lang.Text("Select.Folder.Invalid", newName)));
                return;
            }

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
            folder.Name);
        var success = Lang.Text(
            isClearing ? "Select.Folder.Clear.Success" : "Select.Folder.Delete.Success",
            folder.Name);

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
                Lang.Text("Select.Folder.Error.Operate", deleteText, folder.Name));
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
                        Lang.Text("Select.Folder.Error.Operate", deleteText, folder.Name),
                        LauncherLogLevel.Hint,
                        userSummary: Lang.Text(
                            "Select.Folder.Error.Operate",
                            deleteText,
                            folder.Name));
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

    private void FolderChanged(GameFolders.McFolder folder)
    {
        if (_isBuilding || folder.Location == GameFolders.mcFolderSelected)
            return;

        if (NetFacade.HasDownloadingTask(true))
        {
            HintService.Show(Hints.Error(Lang.Text("Select.Folder.SwitchBlockedByDownload")));
            _isBuilding = true;
            foreach (var (rowFolder, radio) in _rows)
                radio.IsChecked = rowFolder.Location == GameFolders.mcFolderSelected;
            _isBuilding = false;
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

            var segments = newFolder
                .TrimEnd(Path.DirectorySeparatorChar)
                .Split(Path.DirectorySeparatorChar);
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
                    DefaultValue = defaultName
                });
            if (!dialog.IsConfirmed || string.IsNullOrWhiteSpace(dialog.InputText))
                return;

            AddFolder(newFolder, dialog.InputText.Trim(), true);
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

    /// <summary>把文件夹加入列表并选中，逻辑与 WPF 端 PageSelectLeft.AddFolder 保持一致。</summary>
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
}