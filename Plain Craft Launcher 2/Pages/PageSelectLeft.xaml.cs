using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.UI;
using PCL.Core.Utils.Validate;
using PCL.Network;

namespace PCL;

public partial class PageSelectLeft : IRefreshable
{
    private bool isFirstLoad = true;
    private List<ModFolder.McFolder> mcFolderListLast;

    public PageSelectLeft()
    {
        Initialized += PageSelectLeft_Initialized;
        Loaded += PageSelectLeft_Loaded;
        InitializeComponent();
    }

    void IRefreshable.Refresh()
    {
        RefreshCurrent();
    }

    private void PageSelectLeft_Initialized(object sender, EventArgs e)
    {
        ModFolder.mcFolderListLoader.PreviewFinish += _ =>
        {
            if (ModMain.frmSelectLeft is not null) ModBase.RunInUiWait(McFolderListUI);
        };
    }

    private void PageSelectLeft_Loaded(object sender, RoutedEventArgs e)
    {
        if (isFirstLoad)
            McFolderListUI(); // 若已经执行完成，触发首次加载
        isFirstLoad = false;
    }

    private void McFolderListUI()
    {
        try
        {
            // 确认数据有变化
            if (mcFolderListLast is not null && mcFolderListLast.SequenceEqual(ModFolder.mcFolderList))
                return;

            mcFolderListLast = new List<ModFolder.McFolder>(ModFolder.mcFolderList);

            // 创建 UI
            ModMain.frmSelectLeft.PanList.Children.Clear();

            // 文件夹列表标题
            ModMain.frmSelectLeft.PanList.Children.Add(new TextBlock
            {
                Text = Lang.Text("Select.Folder.ListTitle"),
                Margin = new Thickness(13, 18, 5, 4),
                Opacity = 0.6,
                FontSize = 12
            });

            for (var i = 0; i < ModFolder.mcFolderList.Count; i++)
            {
                var folder = ModFolder.mcFolderList[i];

                // 创建 ContextMenu
                var contMenu = new ContextMenu();

                // 添加菜单项
                void AddMenuItem(string name, string header, string svgIcon = null, Thickness? padding = null,
                    RoutedEventHandler clickHandler = null)
                {
                    var item = new MyMenuItem
                    {
                        Name = name,
                        Header = header,
                        SvgIcon = svgIcon,
                        Padding = padding ?? new Thickness(0)
                    };
                    if (clickHandler is not null)
                        item.Click += clickHandler;
                    contMenu.Items.Add(item);
                }

                const string iconRestore = "lucide/rotate-ccw";
                const string iconRename = "lucide/pencil";
                const string iconMoveup = "lucide/move-up";
                const string iconMovedown = "lucide/move-down";
                const string iconOpen = "lucide/folder-open";
                const string iconRefresh = "lucide/refresh-cw";
                const string iconDelete = "lucide/trash-2";
                const string iconRemove = "lucide/list-x";

                switch (folder.type)
                {
                    case ModFolder.McFolder.Types.Original:
                        AddMenuItem("Rename", Lang.Text("Select.Folder.Rename"), iconRename, new Thickness(0, 2, 0, 0),
                            ModMain.frmSelectLeft.Rename_Click);
                        AddMenuItem("MoveUp", Lang.Text("Select.Folder.MoveUp"), iconMoveup, null,
                            ModMain.frmSelectLeft.MoveUp_Click);
                        AddMenuItem("MoveDown", Lang.Text("Select.Folder.MoveDown"), iconMovedown, null,
                            ModMain.frmSelectLeft.MoveDown_Click);
                        AddMenuItem("Open", Lang.Text("Common.Action.Open"), iconOpen, null,
                            ModMain.frmSelectLeft.Open_Click);
                        AddMenuItem("Refresh", Lang.Text("Common.Action.Refresh"), iconRefresh, null,
                            ModMain.frmSelectLeft.Refresh_Click);
                        AddMenuItem("Delete",
                            ModFolder.mcFolderList.Count == 1 &&
                            folder.Location == Path.Combine(ModBase.exePath, ".minecraft") + @"\"
                                ? Lang.Text("Select.Folder.Clear")
                                : Lang.Text("Common.Action.Delete"), iconDelete, new Thickness(0, 0, 0, 2),
                            ModMain.frmSelectLeft.Delete_Click);
                        break;

                    case ModFolder.McFolder.Types.RenamedOriginal:
                        AddMenuItem("Restore", Lang.Text("Select.Folder.RestoreName"), iconRestore,
                            new Thickness(0, 2, 0, 0),
                            ModMain.frmSelectLeft.Restore_Click);
                        AddMenuItem("Rename", Lang.Text("Select.Folder.Rename"), iconRename, null,
                            ModMain.frmSelectLeft.Rename_Click);
                        AddMenuItem("MoveUp", Lang.Text("Select.Folder.MoveUp"), iconMoveup, null,
                            ModMain.frmSelectLeft.MoveUp_Click);
                        AddMenuItem("MoveDown", Lang.Text("Select.Folder.MoveDown"), iconMovedown, null,
                            ModMain.frmSelectLeft.MoveDown_Click);
                        AddMenuItem("Open", Lang.Text("Common.Action.Open"), iconOpen, null,
                            ModMain.frmSelectLeft.Open_Click);
                        AddMenuItem("Refresh", Lang.Text("Common.Action.Refresh"), iconRefresh, null,
                            ModMain.frmSelectLeft.Refresh_Click);
                        AddMenuItem("Delete", Lang.Text("Common.Action.Delete"), iconDelete, new Thickness(0, 0, 0, 2),
                            ModMain.frmSelectLeft.Delete_Click);
                        break;

                    case ModFolder.McFolder.Types.Custom:
                        AddMenuItem("Rename", Lang.Text("Select.Folder.Rename"), iconRename, new Thickness(0, 2, 0, 0),
                            ModMain.frmSelectLeft.Rename_Click);
                        AddMenuItem("MoveUp", Lang.Text("Select.Folder.MoveUp"), iconMoveup, null,
                            ModMain.frmSelectLeft.MoveUp_Click);
                        AddMenuItem("MoveDown", Lang.Text("Select.Folder.MoveDown"), iconMovedown, null,
                            ModMain.frmSelectLeft.MoveDown_Click);
                        AddMenuItem("Open", Lang.Text("Common.Action.Open"), iconOpen, null,
                            ModMain.frmSelectLeft.Open_Click);
                        AddMenuItem("Refresh", Lang.Text("Common.Action.Refresh"), iconRefresh, null,
                            ModMain.frmSelectLeft.Refresh_Click);
                        AddMenuItem("Remove", Lang.Text("Select.Folder.RemoveFromList"), iconRemove,
                            null, ModMain.frmSelectLeft.Remove_Click);
                        AddMenuItem("Delete", Lang.Text("Common.Action.Delete"), iconDelete, new Thickness(0, 0, 0, 2),
                            ModMain.frmSelectLeft.Delete_Click);
                        break;
                }

                // 控制上移下移显示
                var moveUpItem = contMenu.Items.OfType<MyMenuItem>().FirstOrDefault(x => x.Name == "MoveUp");
                var moveDownItem = contMenu.Items.OfType<MyMenuItem>().FirstOrDefault(x => x.Name == "MoveDown");

                // 如果是第一个项目，隐藏上移按钮
                if (i == 0) moveUpItem.Visibility = Visibility.Collapsed;

                // 如果是最后一个项目，隐藏下移按钮
                if (i == ModFolder.mcFolderList.Count - 1) moveDownItem.Visibility = Visibility.Collapsed;

                // 构建列表项
                var newItem = new MyListItem
                {
                    IsScaleAnimationEnabled = false,
                    Type = MyListItem.CheckType.RadioBox,
                    MinPaddingRight = 30,
                    Title = folder.Name,
                    Info = folder.Location,
                    Height = 40,
                    ContextMenu = contMenu,
                    Tag = folder
                };

                newItem.Changed += (a, b) => ModMain.frmSelectLeft.Folder_Change((MyListItem)a, b);

                // 拖拽
                newItem.AllowDrop = true;
                newItem.MouseMove += ModMain.frmSelectLeft.Item_MouseMove;
                newItem.DragEnter += ModMain.frmSelectLeft.Item_DragEnter;
                newItem.DragOver += ModMain.frmSelectLeft.Item_DragOver;
                newItem.DragLeave += ModMain.frmSelectLeft.Item_DragLeave;
                newItem.Drop += ModMain.frmSelectLeft.Item_Drop;

                // 图标按钮
                var newIconButton = new MyIconButton
                {
                    SvgIcon = "lucide/settings",
                    LogoScale = 1.1
                };
                newIconButton.Click += (_, _) =>
                {
                    contMenu.PlacementTarget = newItem;
                    contMenu.IsOpen = true;
                };
                newItem.Buttons = [newIconButton];

                ModMain.frmSelectLeft.PanList.Children.Add(newItem);

                LogWrapper.Info($"[Minecraft] 有效的 Minecraft 文件夹：{folder.Name} > {folder.Location}");
            }

            // 标题文本
            ModMain.frmSelectLeft.PanList.Children.Add(new TextBlock
            {
                Text = Lang.Text("Select.Folder.AddOrImport"),
                Margin = new Thickness(13, 18, 5, 4),
                Opacity = 0.6,
                FontSize = 12
            });

            // 创建新文件夹按钮
            if (!Directory.Exists(Path.Combine(ModBase.exePath, ".minecraft")))
            {
                var itemCreate = new MyListItem
                {
                    IsScaleAnimationEnabled = false,
                    Type = MyListItem.CheckType.Clickable,
                    Title = Lang.Text("Select.Folder.CreateNew.Title"),
                    Height = 34,
                    ToolTip = Lang.Text("Select.Folder.CreateNew.ToolTip"),
                    LogoScale = 0.9,
                    SvgIcon = "lucide/folder-plus"
                };
                ToolTipService.SetPlacement(itemCreate, PlacementMode.Right);
                ToolTipService.SetHorizontalOffset(itemCreate, -50);
                ToolTipService.SetVerticalOffset(itemCreate, 2.5);
                itemCreate.Click += (_, _) => ModMain.frmSelectLeft.Create_Click();
                ModMain.frmSelectLeft.PanList.Children.Add(itemCreate);
            }

            // 添加按钮
            var itemAdd = new MyListItem
            {
                IsScaleAnimationEnabled = false,
                Type = MyListItem.CheckType.Clickable,
                Title = Lang.Text("Select.Folder.AddExisting.Title"),
                Height = 34,
                ToolTip = Lang.Text("Select.Folder.AddExisting.ToolTip"),
                SvgIcon = "lucide/folder-input"
            };
            ToolTipService.SetPlacement(itemAdd, PlacementMode.Right);
            ToolTipService.SetHorizontalOffset(itemAdd, -50);
            ToolTipService.SetVerticalOffset(itemAdd, 2.5);
            itemAdd.Click += (_, _) => ModMain.frmSelectLeft.Add_Click();
            ModMain.frmSelectLeft.PanList.Children.Add(itemAdd);

            // 导入整合包
            var itemInstall = new MyListItem
            {
                IsScaleAnimationEnabled = false,
                Type = MyListItem.CheckType.Clickable,
                Title = Lang.Text("Select.Folder.ImportModpack.Title"),
                Height = 34,
                ToolTip = Lang.Text("Select.Folder.ImportModpack.ToolTip"),
                SvgIcon = "lucide/package-plus"
            };
            ToolTipService.SetPlacement(itemInstall, PlacementMode.Right);
            ToolTipService.SetHorizontalOffset(itemInstall, -50);
            ToolTipService.SetVerticalOffset(itemInstall, 2.5);
            itemInstall.Click += (_, _) => ModModpack.ModpackInstall();
            ModMain.frmSelectLeft.PanList.Children.Add(itemInstall);

            // 边距
            ModMain.frmSelectLeft.PanList.Children.Add(new FrameworkElement { Height = 10, IsHitTestVisible = false });

            // 确认勾选状态
            for (var i = 0; i < ModFolder.mcFolderList.Count; i++)
                if (ModFolder.mcFolderList[i].Location == ModFolder.mcFolderSelected)
                {
                    ((MyListItem)ModMain.frmSelectLeft.PanList.Children[i + 1]).Checked = true; //去掉第一个标题
                    return;
                }

            if (ModFolder.mcFolderList.Count == 0)
                throw new ArgumentNullException("没有可用的 Minecraft 文件夹");
            States.Game.SelectedFolder = ModFolder.mcFolderList[0].Location.Replace(ModBase.exePath, "$");
            ((MyListItem)ModMain.frmSelectLeft.PanList.Children[1]).Checked = true;
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "构建 Minecraft 文件夹列表 UI 出错");
        }
        finally
        {
            ModLoader.LoaderFolderRun(ModInstanceList.mcInstanceListLoader,
                ModFolder.mcFolderSelected,
                ModLoader.LoaderFolderRunType.RunOnUpdated,
                1,
                "versions\\");
        }
    }

    private void MoveUp_Click(object sender, RoutedEventArgs e)
    {
        var folder =
            (ModFolder.McFolder)((MyListItem)((Popup)((ContextMenu)((MyMenuItem)sender).Parent).Parent)
                .PlacementTarget)
            .Tag;
        var index = ModFolder.mcFolderList.IndexOf(folder);
        if (index <= 0) return;
        ModFolder.mcFolderList.RemoveAt(index);
        ModFolder.mcFolderList.Insert(index - 1, folder);
        UpdateFolderOrder();
    }

    private void MoveDown_Click(object sender, RoutedEventArgs e)
    {
        var folder =
            (ModFolder.McFolder)((MyListItem)((Popup)((ContextMenu)((MyMenuItem)sender).Parent).Parent)
                .PlacementTarget)
            .Tag;
        var index = ModFolder.mcFolderList.IndexOf(folder);
        if (index >= ModFolder.mcFolderList.Count - 1) return;
        ModFolder.mcFolderList.RemoveAt(index);
        ModFolder.mcFolderList.Insert(index + 1, folder);
        UpdateFolderOrder();
    }

    private void UpdateFolderOrder()
    {
        States.Game.Folders = ModFolder.mcFolderList
            .Select(folder => $"{folder.Name}>{folder.Location}")
            .ToArray()
            .Join("|");
        McFolderListUI();
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        var folder =
            (ModFolder.McFolder)((MyListItem)((Popup)((ContextMenu)((MyMenuItem)sender).Parent).Parent)
                .PlacementTarget)
            .Tag;
        var index = ModFolder.mcFolderList.IndexOf(folder);
        ModFolder.mcFolderList[index].type = ModFolder.McFolder.Types.Original;
        ModFolder.mcFolderList[index].Name = Lang.Text("Select.Folder.OfficialLauncherFolder");
        UpdateFolderOrder();
    }

    // 添加文件夹
    private void Add_Click()
    {
        var newFolder = "";
        // 检查是否有下载任务
        if (ModNet.HasDownloadingTask())
        {
            HintService.Hint(Lang.Text("Select.Folder.CannotAddWhileDownloading"), HintType.Error);
            return;
        }

        try
        {
            // 获取输入
            newFolder = SystemDialogs.SelectFolder();
            if (string.IsNullOrEmpty(newFolder))
                return;
            if (newFolder.Contains('!') || newFolder.Contains(';'))
            {
                HintService.Hint(Lang.Text("Select.Folder.InvalidPathChars"), HintType.Error);
                return;
            }

            // 要求输入显示名称
            var splitedNames = newFolder.TrimEnd('\\').Split(@"\");
            var defaultName = splitedNames.Last() == ".minecraft"
                ? splitedNames.Length >= 3 ? splitedNames[^2] : ""
                : splitedNames.Last();
            if (defaultName.Length > 40)
                defaultName = defaultName[..39];
            var newName = ModMain.MyMsgBoxInput(Lang.Text("Select.Folder.InputDisplayName.Title"),
                Lang.Text("Select.Folder.InputDisplayName.Message"), defaultName,
                [new NullOrWhiteSpaceValidator(), new StringLengthValidator(), new BlacklistValidator([">", "|"])]);
            if (string.IsNullOrWhiteSpace(newName))
                return;
            // 添加文件夹
            AddFolder(newFolder, newName, true);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, Lang.Text("Select.Folder.Error.Add", newFolder), ModBase.LogLevel.Feedback);
        }
    }

    /// <summary>
    ///     将指定文件夹添加到 Minecraft 文件夹列表，并选中它。
    /// </summary>
    public static void AddFolder(string folderPath, string displayName, bool showHint)
    {
        // 检查文件夹权限
        // 检查实际的 Minecraft 文件夹位置（没有问题，或是在子文件夹中）
        // 判断是否已经添加过，若添加过则直接修改自定义名
        // 如果没有添加过，则添加进去
        // 保存
        // 切换选择并更新列表
        // 提示
        // 检查是否为根目录整合包，自动关闭版本隔离
        // 1. 根目录中存在数个 Mod
        // 2. 实例数较少，可能为整合包
        // 3. 能够找到可安装 Mod 的实例
        // 4. 该实例的隔离文件夹下不存在 mods
        // 满足以上全部条件则视为根目录整合包
        ModBase.RunInThread(() =>
        {
            try
            {
                if (!folderPath.EndsWith(@"\")) folderPath += @"\";
                if (!ModBase.CheckPermission(folderPath))
                {
                    if (!showHint) throw new Exception("PCL 没有访问文件夹的权限：" + folderPath);
                    HintService.Hint(Lang.Text("Select.Folder.AccessDenied"), HintType.Error);
                    return;
                }

                if (!ModBase.CheckPermission(folderPath + @"versions\"))
                    foreach (var Folder in new DirectoryInfo(folderPath).GetDirectories())
                        if (ModBase.CheckPermission(Path.Combine(Folder.FullName, "versions")))
                        {
                            folderPath = Folder.FullName + @"\";
                            break;
                        }

                var folders = new List<string>(States.Game.Folders.Split("|"));
                var isAdded = false;
                var isReplace = false;
                for (int i = 0, loopTo = folders.Count - 1; i <= loopTo; i++)
                {
                    var folder = folders[i];
                    if (string.IsNullOrEmpty(folder)) continue;
                    if (folder.Split(">")[1] != (folderPath ?? "")) continue;
                    isAdded = true;
                    if (folder.Split(">")[0] == displayName)
                    {
                        if (showHint) HintService.Hint(Lang.Text("Select.Folder.AlreadyInList"));
                        return;
                    }

                    folders[i] = $"{displayName}>{folderPath}";
                    isReplace = true;
                    if (showHint)
                        HintService.Hint(Lang.Text("Select.Folder.NameUpdated", displayName), HintType.Success);
                    break;
                }

                if (!isAdded) folders.Add($"{displayName}>{folderPath}");
                States.Game.Folders = folders.ToArray().Join("|");
                States.Game.SelectedFolder = folderPath.Replace(ModBase.exePath, "$");
                ModFolder.mcFolderListLoader.Start(isForceRestart: true);
                if (isReplace) return;
                if (showHint) HintService.Hint(Lang.Text("Select.Folder.Added", displayName), HintType.Success);
                var modFolder = new DirectoryInfo(folderPath + @"mods\");
                if (!(modFolder.Exists && modFolder.EnumerateFiles().Count() >= 3)) return;
                var versionFolder = new DirectoryInfo(folderPath + @"versions\");
                if (!(versionFolder.Exists && versionFolder.EnumerateDirectories().Count() <= 3)) return;
                foreach (var VersionPath in versionFolder.EnumerateDirectories())
                {
                    var version = new McInstance(VersionPath.FullName);
                    version.Load();
                    if (!version.Modable) continue;
                    var modIndieFolder = new DirectoryInfo(version.PathInstance + @"mods\");
                    if (modIndieFolder.Exists && modIndieFolder.EnumerateFiles().Any()) return;
                    Config.Instance.IndieV1[version.PathInstance] = 2;
                    Config.Instance.IndieV2[version.PathInstance] = false;
                    ModBase.Log("[Setup] 已自动关闭单版本隔离：" + version.Name, ModBase.LogLevel.Debug);
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, Lang.Text("Select.Folder.Error.AddNew"), ModBase.LogLevel.Feedback);
            }
        }); // 加上斜杠……
    }

    // 创建文件夹
    public void Create_Click()
    {
        // 检查是否有下载任务
        if (ModNet.HasDownloadingTask())
        {
            HintService.Hint(Lang.Text("Select.Folder.CannotCreateWhileDownloading"), HintType.Error);
            return;
        }

        if (!Directory.Exists(ModBase.exePath + @".minecraft\"))
        {
            Directory.CreateDirectory(ModBase.exePath + @".minecraft\");
            Directory.CreateDirectory(ModBase.exePath + @".minecraft\versions\");
            States.Game.SelectedFolder = @"$.minecraft\";
            ModFolder.McFolderLauncherProfilesJsonCreate(ModBase.exePath + @".minecraft\");
            HintService.Hint(Lang.Text("Select.Folder.CreateSuccess"), HintType.Success);
        }

        ModFolder.mcFolderListLoader.Start(isForceRestart: true);
    }

    // 右键菜单
    public void Remove_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var folder =
                (ModFolder.McFolder)((MyListItem)((Popup)((ContextMenu)((MyMenuItem)sender).Parent).Parent)
                    .PlacementTarget).Tag;
            switch (ModMain.MyMsgBox(
                        Lang.Text("Select.Folder.Cleanup.Message"),
                        Lang.Text("Select.Folder.Cleanup.Title"), Lang.Text("Common.Action.Delete"),
                        Lang.Text("Select.Folder.Cleanup.Keep"), Lang.Text("Common.Action.Cancel")))
            {
                case 1:
                {
                    // 删除配置文件
                    if (File.Exists(folder.Location + "PCL.ini"))
                        File.Delete(folder.Location + "PCL.ini");
                    if (Directory.Exists(folder.Location + @"versions\"))
                        foreach (var Version in new DirectoryInfo(folder.Location + @"versions\")
                                     .EnumerateDirectories())
                            if (Directory.Exists(Path.Combine(Version.FullName, "PCL")))
                                Directory.Delete(Path.Combine(Version.FullName, "PCL"), true);

                    break;
                }
                case 2:
                {
                    break;
                }
                // 不删除
                case 3:
                {
                    // 取消
                    return;
                }
            }

            // 若修改了本部分代码，应对应修改 Delete_Click 中的代码
            // 获取并删除列表项
            var folders = new List<string>(States.Game.Folders.Split("|"));
            var name = "";
            for (int i = 0, loopTo = folders.Count - 1; i <= loopTo; i++)
            {
                if (string.IsNullOrEmpty(folders[i]))
                    break;
                if (!folders[i].EndsWith(folder.Location)) continue;
                name = folders[i].BeforeFirst(">");
                folders.RemoveAt(i);
                break;
            }

            // 保存
            States.Game.Folders = folders.Count == 0 ? "" : folders.ToArray().Join("|");
            HintService.Hint(
                folder.type == ModFolder.McFolder.Types.Custom
                    ? Lang.Text("Select.Folder.RemoveSuccess", name)
                    : Lang.Text("Select.Folder.RestoreSuccess"),
                HintType.Success);
            ModFolder.mcFolderListLoader.Start(isForceRestart: true);
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, Lang.Text("Select.Folder.Error.Remove"), ModBase.LogLevel.Feedback);
        }
    }

    public void Delete_Click(object sender, RoutedEventArgs e)
    {
        var menuItem = (MyMenuItem)sender;
        var contextMenu = (ContextMenu)menuItem.Parent;
        var popup = (Popup)contextMenu.Parent;
        var listItem = (MyListItem)popup.PlacementTarget;
        var folder = (ModFolder.McFolder)listItem.Tag;

        var isClearing =
            folder.type is ModFolder.McFolder.Types.Original or ModFolder.McFolder.Types.RenamedOriginal
            && folder.Location == ModBase.exePath + @".minecraft\"
            && ModFolder.mcFolderList.Count == 1;

        var deleteText = Lang.Text(isClearing ? "Select.Folder.Clear" : "Common.Action.Delete");
        var firstWarning =
            Lang.Text(isClearing ? "Select.Folder.Clear.FirstWarning" : "Select.Folder.Delete.FirstWarning",
                folder.Location);
        var finalWarning =
            Lang.Text(isClearing ? "Select.Folder.Clear.FinalWarning" : "Select.Folder.Delete.FinalWarning",
                folder.Location);
        var confirmTitle = Lang.Text(isClearing ? "Select.Folder.Clear.Confirm" : "Select.Folder.Delete.Confirm");
        var inProgress = Lang.Text(isClearing ? "Select.Folder.Clear.InProgress" : "Select.Folder.Delete.InProgress",
            folder.Name);
        var success = Lang.Text(isClearing ? "Select.Folder.Clear.Success" : "Select.Folder.Delete.Success",
            folder.Name);

        if (ModMain.MyMsgBox(firstWarning, Lang.Text("Select.Folder.Delete.WarningTitle"),
                Lang.Text("Common.Action.Cancel"), Lang.Text("Common.Action.Confirm"),
                Lang.Text("Common.Action.Cancel")) != 2)
            return;

        if (ModMain.MyMsgBox(finalWarning, Lang.Text("Select.Folder.Delete.WarningTitle"),
                confirmTitle, Lang.Text("Common.Action.Cancel"),
                isWarn: true) != 1)
            return;

        var folders = States.Game.Folders.Split('|', StringSplitOptions.RemoveEmptyEntries).ToList();
        var index = folders.FindIndex(f => f.EndsWith(folder.Location));
        if (index >= 0)
            folders.RemoveAt(index);
        States.Game.Folders = string.Join("|", folders);

        ModBase.RunInNewThread(() =>
        {
            try
            {
                HintService.Hint(inProgress);
                ModBase.DeleteDirectory(folder.Location);
                if (isClearing)
                    Directory.CreateDirectory(folder.Location);
                HintService.Hint(success, HintType.Success);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, Lang.Text("Select.Folder.Error.Operate", deleteText, folder.Name), ModBase.LogLevel.Hint);
            }
            finally
            {
                ModFolder.mcFolderListLoader.Start(isForceRestart: true);
            }
        }, "Folder Delete " + ModBase.GetUuid(), ThreadPriority.BelowNormal);
    }

    public void Open_Click(object sender, RoutedEventArgs e)
    {
        ModBase.OpenExplorer(((MyListItem)((Popup)((ContextMenu)((MyMenuItem)sender).Parent).Parent).PlacementTarget)
            .Info);
    }

    public void Refresh_Click(object sender, RoutedEventArgs e)
    {
        var data = (ModFolder.McFolder)((MyListItem)((Popup)((ContextMenu)((MyMenuItem)sender).Parent).Parent)
            .PlacementTarget).Tag;
        RefreshCurrent(data.Location);
    }

    public void RefreshCurrent()
    {
        RefreshCurrent(ModFolder.mcFolderSelected);
    }

    public static void RefreshCurrent(string folder)
    {
        ModBase.WriteIni(Path.Combine(folder, "PCL.ini"), "InstanceCache", "");
        if (folder == ModFolder.mcFolderSelected)
            ModLoader.LoaderFolderRun(ModInstanceList.mcInstanceListLoader, ModFolder.mcFolderSelected,
                ModLoader.LoaderFolderRunType.ForceRun, 1, @"versions\");
    }

    public void Rename_Click(object sender, RoutedEventArgs e)
    {
        var folder =
            (ModFolder.McFolder)((MyListItem)((Popup)((ContextMenu)((MyMenuItem)sender).Parent).Parent)
                .PlacementTarget)
            .Tag;
        try
        {
            // 获取输入
            var newName = ModMain.MyMsgBoxInput(Lang.Text("Select.Folder.Rename.Title"), "", folder.Name,
            [
                new NullOrWhiteSpaceValidator(), new StringLengthValidator(1, 30),
                new BlacklistValidator([">", "|"])
            ]);
            if (string.IsNullOrWhiteSpace(newName))
                return;
            // 修改自定义名
            var folders = new List<string>(States.Game.Folders.Split("|"));
            var isAdded = false;
            for (int i = 0, loopTo = folders.Count - 1; i <= loopTo; i++)
            {
                var folderCurrent = folders[i];
                if (string.IsNullOrEmpty(folderCurrent))
                    continue;
                if (folderCurrent.Split(">")[1] != (folder.Location ?? "")) continue;
                isAdded = true;
                if (folderCurrent.Split(">")[0] == newName)
                    // 名称未修改
                    return;

                folders[i] = $"{newName}>{folder.Location}";
                break;
            }

            // 如果没有添加过，则添加进去（因为修改了默认项的名称）
            if (!isAdded)
                folders.Add($"{newName}>{folder.Location}");
            HintService.Hint(Lang.Text("Select.Folder.NameUpdated", newName), HintType.Success);
            // 保存
            States.Game.Folders = folders.ToArray().Join("|");
            ModFolder.mcFolderListLoader.Start(isForceRestart: true);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, Lang.Text("Select.Folder.Error.Rename"), ModBase.LogLevel.Feedback);
        }
    }

    // 点击选项
    public void Folder_Change(MyListItem sender, ModBase.RouteEventArgs e)
    {
        if (!e.raiseByMouse || !sender.Checked)
            return;
        // 检查是否有下载任务
        if (ModNet.HasDownloadingTask(true))
        {
            HintService.Hint(Lang.Text("Select.Folder.SwitchBlockedByDownload"), HintType.Error);
            e.handled = true;
            return;
        }

        // 更换
        States.Game.SelectedFolder = ((ModFolder.McFolder)sender.Tag).Location.Replace(ModBase.exePath, "$");
        ModFolder.mcFolderListLoader.Start(isForceRestart: true);
        ModLoader.LoaderFolderRun(ModInstanceList.mcInstanceListLoader, ModFolder.mcFolderSelected,
            ModLoader.LoaderFolderRunType.RunOnUpdated, 1, @"versions\"); // 刷新实例列表
    }

    #region 拖拽排序功能

    // 拖拽开始时的鼠标移动处理
    private void Item_MouseMove(object sender, MouseEventArgs e)
    {
        var item = (MyListItem)sender;
        // 当按住鼠标左键时开始拖拽操作
        if (e.LeftButton != MouseButtonState.Pressed) return;
        try
        {
            DragDrop.DoDragDrop(item, item.Tag, DragDropEffects.Move);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "开始拖拽操作失败");
        }
    }

    // 拖拽进入时的处理
    private void Item_DragEnter(object sender, DragEventArgs e)
    {
        try
        {
            if (e.Data.GetDataPresent(typeof(ModFolder.McFolder)))
            {
                e.Effects = DragDropEffects.Move;
                // 添加视觉反馈
                var item = (MyListItem)sender;
                item.Opacity = 0.7d;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }
        catch (Exception)
        {
            e.Effects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    // 拖拽悬停时的处理
    private void Item_DragOver(object sender, DragEventArgs e)
    {
        try
        {
            e.Effects = e.Data.GetDataPresent(typeof(ModFolder.McFolder))
                ? DragDropEffects.Move
                : DragDropEffects.None;
        }
        catch (Exception)
        {
            e.Effects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    // 拖拽离开时的处理
    private void Item_DragLeave(object sender, DragEventArgs e)
    {
        try
        {
            // 恢复视觉状态
            var item = (MyListItem)sender;
            item.Opacity = 1.0d;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "拖拽离开处理失败");
        }

        e.Handled = true;
    }

    // 拖拽放下时的处理
    private void Item_Drop(object sender, DragEventArgs e)
    {
        try
        {
            var targetItem = (MyListItem)sender;
            var targetFolder = (ModFolder.McFolder)targetItem.Tag;

            // 恢复视觉状态
            targetItem.Opacity = 1.0d;

            // 检查数据有效性
            if (!e.Data.GetDataPresent(typeof(ModFolder.McFolder)))
            {
                e.Handled = true;
                return;
            }

            var sourceFolder = (ModFolder.McFolder)e.Data.GetData(typeof(ModFolder.McFolder));

            // 检查是否为有效的拖拽操作
            if (ReferenceEquals(sourceFolder, targetFolder))
            {
                e.Handled = true;
                return;
            }

            // 检查文件夹是否在列表中
            if (!ModFolder.mcFolderList.Contains(sourceFolder) || !ModFolder.mcFolderList.Contains(targetFolder))
            {
                e.Handled = true;
                return;
            }

            // 获取源文件夹和目标文件夹的索引
            var sourceIndex = ModFolder.mcFolderList.IndexOf(sourceFolder);
            var targetIndex = ModFolder.mcFolderList.IndexOf(targetFolder);

            // 执行移动操作
            if (sourceIndex == targetIndex) return;
            // 先移除源文件夹
            ModFolder.mcFolderList.RemoveAt(sourceIndex);

            // 计算新的插入位置
            int newTargetIndex;

            // 向下拖拽：插入到目标项目的后面
            // 由于移除了源项目，目标索引已经自动减1，所以直接使用TargetIndex就是插入到目标后面
            // 向上拖拽：插入到目标项目的前面
            newTargetIndex = targetIndex;

            // 确保插入位置不超出列表范围
            if (newTargetIndex > ModFolder.mcFolderList.Count)
                newTargetIndex = ModFolder.mcFolderList.Count;
            else if (newTargetIndex < 0) newTargetIndex = 0;

            // 插入到新位置
            ModFolder.mcFolderList.Insert(newTargetIndex, sourceFolder);

            // 更新文件夹顺序并刷新UI
            UpdateFolderOrder();

            var direction = sourceIndex < targetIndex ? "后面" : "前面";
            ModBase.Log(
                $"[Control] 文件夹拖拽排序：{sourceFolder.Name} -> 位置 {newTargetIndex} (在 {targetFolder.Name} {direction})",
                ModBase.LogLevel.Debug);
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, Lang.Text("Select.Folder.Error.DragDrop"), ModBase.LogLevel.Feedback);
        }
        finally
        {
            e.Handled = true;
        }
    }

    #endregion
}