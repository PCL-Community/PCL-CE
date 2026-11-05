using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.Core.Utils.Validate;
using PCL.Core.ViewModel;
using PCL.Network;

namespace PCL;

/// <summary>Minecraft 文件夹选择页左栏的视图模型：文件夹集合、选择、排序与全部菜单操作。</summary>
public partial class PageSelectLeftViewModel : ViewModelBase
{
    private List<GameFolders.McFolder>? _lastFolders;

    [ObservableProperty] public partial bool CanCreateFolder { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<GameFolders.McFolder> Folders { get; set; } = [];

    [ObservableProperty] public partial bool IsFirstLoad { get; set; } = true;

    [ObservableProperty] public partial GameFolders.McFolder? SelectedFolder { get; set; }

    /// <summary>从全局文件夹列表同步快照；列表未变化时返回 <c>false</c>。</summary>
    public bool RefreshFolders(bool force = false)
    {
        if (!force
            && _lastFolders is not null
            && _lastFolders.SequenceEqual(GameFolders.mcFolderList))
            return false;

        _lastFolders = [.. GameFolders.mcFolderList];
        Folders = _lastFolders;
        CanCreateFolder = !Directory.Exists(
            Path.Combine(LauncherPaths.ExecutableDirectory, ".minecraft"));

        var selected = GameFolders.mcFolderList.FirstOrDefault(folder =>
            folder.Location == GameFolders.mcFolderSelected);
        if (selected is null && GameFolders.mcFolderList.Count > 0)
        {
            selected = GameFolders.mcFolderList[0];
            States.Game.SelectedFolder = selected.Location
                .Replace(LauncherPaths.ExecutableDirectory, "$");
        }

        SelectedFolder = selected;
        return true;
    }

    public void InvalidateFolders()
    {
        _lastFolders = null;
    }

    public static bool CanMoveUp(GameFolders.McFolder folder)
    {
        return GameFolders.mcFolderList.IndexOf(folder) > 0;
    }

    public static bool CanMoveDown(GameFolders.McFolder folder)
    {
        var index = GameFolders.mcFolderList.IndexOf(folder);
        return index >= 0 && index < GameFolders.mcFolderList.Count - 1;
    }

    public static string GetDeleteMenuKey(GameFolders.McFolder folder)
    {
        return GameFolders.mcFolderList.Count == 1
            && folder.Location
            == Path.Combine(LauncherPaths.ExecutableDirectory, ".minecraft")
            + Path.DirectorySeparatorChar
                ? "Select.Folder.Clear"
                : "Common.Action.Delete";
    }

    public static bool IsClearingDefaultFolder(GameFolders.McFolder folder)
    {
        return folder.Type is GameFolders.McFolder.Types.Original
                or GameFolders.McFolder.Types.RenamedOriginal
            && folder.Location
            == Path.Combine(LauncherPaths.ExecutableDirectory, ".minecraft")
            + Path.DirectorySeparatorChar
            && GameFolders.mcFolderList.Count == 1;
    }

    /// <summary>切换当前文件夹；被下载任务阻止时返回 <c>false</c>，调用方需要恢复选中状态。</summary>
    public bool SelectFolder(GameFolders.McFolder folder)
    {
        if (folder.Location == GameFolders.mcFolderSelected)
            return true;

        if (NetFacade.HasDownloadingTask(true))
        {
            HintService.Show(Hints.Error(Lang.Text("Select.Folder.SwitchBlockedByDownload")));
            return false;
        }

        States.Game.SelectedFolder = folder.Location
            .Replace(LauncherPaths.ExecutableDirectory, "$");
        GameFolders.McFolderListLoader.Start(null, true);
        SelectedFolder = folder;
        ReloadInstanceList();
        return true;
    }

    public static bool ReorderFolders(GameFolders.McFolder sourceFolder, GameFolders.McFolder targetFolder)
    {
        if (ReferenceEquals(sourceFolder, targetFolder))
            return false;
        if (!GameFolders.mcFolderList.Contains(sourceFolder)
            || !GameFolders.mcFolderList.Contains(targetFolder))
            return false;

        var sourceIndex = GameFolders.mcFolderList.IndexOf(sourceFolder);
        var targetIndex = GameFolders.mcFolderList.IndexOf(targetFolder);
        if (sourceIndex < 0 || targetIndex < 0 || sourceIndex == targetIndex)
            return false;

        GameFolders.mcFolderList.RemoveAt(sourceIndex);
        targetIndex = Math.Clamp(targetIndex, 0, GameFolders.mcFolderList.Count);
        GameFolders.mcFolderList.Insert(targetIndex, sourceFolder);
        UpdateFolderOrder();
        LauncherCommon.Log(
            $"[Control] 文件夹拖拽排序：{sourceFolder.DisplayName} -> 位置 {targetIndex}"
            + $"（目标 {targetFolder.DisplayName}）",
            LauncherLogLevel.Debug);
        return true;
    }

    [RelayCommand]
    public static void MoveUp(GameFolders.McFolder folder)
    {
        MoveFolder(folder, -1);
    }

    [RelayCommand]
    public static void MoveDown(GameFolders.McFolder folder)
    {
        MoveFolder(folder, 1);
    }

    [RelayCommand]
    public static void RestoreName(GameFolders.McFolder folder)
    {
        RestoreFolderName(folder);
    }

    [RelayCommand]
    public static void OpenFolder(GameFolders.McFolder folder)
    {
        Basics.OpenPath(folder.Location);
    }

    [RelayCommand]
    public static void RefreshFolder(GameFolders.McFolder folder)
    {
        RefreshCurrent(folder.Location);
    }

    [RelayCommand]
    public static void CreateFolder()
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

    [RelayCommand]
    public async Task AddExistingFolderAsync()
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

            var dialog = await Dialog.ShowAsync(
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

    [RelayCommand]
    public static void InstallModpack()
    {
        Modpack.ModpackInstall();
    }

    [RelayCommand]
    public async Task RenameFolderAsync(GameFolders.McFolder folder)
    {
        try
        {
            var dialog = await Dialog.ShowAsync(
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

    [RelayCommand]
    public async Task RemoveFolderAsync(GameFolders.McFolder folder)
    {
        try
        {
            var dialog = await Dialog.ShowAsync(
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

    [RelayCommand]
    public async Task DeleteFolderAsync(GameFolders.McFolder folder)
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
            var firstDialog = await Dialog.ShowAsync(
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

            var finalDialog = await Dialog.ShowAsync(
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

    public static void ReloadInstanceList()
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