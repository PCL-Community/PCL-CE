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
    private List<ModMinecraft.McFolder> mcFolderListLast;

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
        ModMinecraft.mcFolderListLoader.PreviewFinish += _ =>
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
            if (mcFolderListLast is not null && mcFolderListLast.SequenceEqual(ModMinecraft.mcFolderList))
                return;

            mcFolderListLast = new List<ModMinecraft.McFolder>(ModMinecraft.mcFolderList);

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

            for (var i = 0; i < ModMinecraft.mcFolderList.Count; i++)
            {
                var folder = ModMinecraft.mcFolderList[i];

                // 创建 ContextMenu
                var contMenu = new ContextMenu();

                // 添加菜单项
                void AddMenuItem(string name, string header, string icon = null, Thickness? padding = null,
                    RoutedEventHandler clickHandler = null)
                {
                    var item = new MyMenuItem
                    {
                        Name = name,
                        Header = header,
                        Icon = icon,
                        Padding = padding ?? new Thickness(0)
                    };
                    if (clickHandler is not null)
                        item.Click += clickHandler;
                    contMenu.Items.Add(item);
                }

                const string ICON_RENAME =
                    "F1 M 53.2929,21.2929L 54.7071,22.7071C 56.4645,24.4645 56.4645,27.3137 54.7071,29.0711L 52.2323,31.5459L 44.4541,23.7677L 46.9289,21.2929C 48.6863,19.5355 51.5355,19.5355 53.2929,21.2929 Z M 31.7262,52.052L 23.948,44.2738L 43.0399,25.182L 50.818,32.9601L 31.7262,52.052 Z M 23.2409,47.1023L 28.8977,52.7591L 21.0463,54.9537L 23.2409,47.1023 Z";
                const string ICON_MOVEUP =
                    "M104.704 685.248a64 64 0 0 0 90.496 0L512 368.448l316.8 316.8a64 64 0 0 0 90.496-90.496L557.248 232.704a64 64 0 0 0-90.496 0L104.704 594.752a64 64 0 0 0 0 90.496z";
                const string ICON_MOVEDOWN =
                    "M104.704 338.752a64 64 0 0 1 90.496 0L512 655.552l316.8-316.8a64 64 0 0 1 90.496 90.496l-362.048 362.048a64 64 0 0 1-90.496 0L104.704 429.248a64 64 0 0 1 0-90.496z";
                const string ICON_OPEN =
                    "F1 M 19,50L 28,34L 63,34L 54,50L 19,50 Z M 19,28.0001L 35,28C 36,25 37.4999,24.0001 37.4999,24.0001L 48.75,24C 49.3023,24 50,24.6977 50,25.25L 50,28L 54,28.0001L 54,32L 27,32L 19,46.4L 19,28.0001 Z";
                const string ICON_REFRESH =
                    "F1 M 38,20.5833C 42.9908,20.5833 47.4912,22.6825 50.6667,26.046L 50.6667,17.4167L 55.4166,22.1667L 55.4167,34.8333L 42.75,34.8333L 38,30.0833L 46.8512,30.0833C 44.6768,27.6539 41.517,26.125 38,26.125C 31.9785,26.125 27.0037,30.6068 26.2296,36.4167L 20.6543,36.4167C 21.4543,27.5397 28.9148,20.5833 38,20.5833 Z M 38,49.875C 44.0215,49.875 48.9963,45.3932 49.7703,39.5833L 55.3457,39.5833C 54.5457,48.4603 47.0852,55.4167 38,55.4167C 33.0092,55.4167 28.5088,53.3175 25.3333,49.954L 25.3333,58.5833L 20.5833,53.8333L 20.5833,41.1667L 33.25,41.1667L 38,45.9167L 29.1487,45.9167C 31.3231,48.3461 34.483,49.875 38,49.875 Z";
                const string ICON_DELETE =
                    "F1 M 26.9166,22.1667L 37.9999,33.25L 49.0832,22.1668L 53.8332,26.9168L 42.7499,38L 53.8332,49.0834L 49.0833,53.8334L 37.9999,42.75L 26.9166,53.8334L 22.1666,49.0833L 33.25,38L 22.1667,26.9167L 26.9166,22.1667 Z";

                switch (folder.type)
                {
                    case ModMinecraft.McFolder.Types.Original:
                        AddMenuItem("Rename", Lang.Text("Select.Folder.Rename"), ICON_RENAME, new Thickness(0, 2, 0, 0),
                            ModMain.frmSelectLeft.Rename_Click);
                        AddMenuItem("MoveUp", Lang.Text("Select.Folder.MoveUp"), ICON_MOVEUP, null,
                            ModMain.frmSelectLeft.MoveUp_Click);
                        AddMenuItem("MoveDown", Lang.Text("Select.Folder.MoveDown"), ICON_MOVEDOWN, null,
                            ModMain.frmSelectLeft.MoveDown_Click);
                        AddMenuItem("Open", Lang.Text("Common.Action.Open"), ICON_OPEN, null,
                            ModMain.frmSelectLeft.Open_Click);
                        AddMenuItem("Refresh", Lang.Text("Common.Action.Refresh"), ICON_REFRESH, null,
                            ModMain.frmSelectLeft.Refresh_Click);
                        AddMenuItem("Delete",
                            ModMinecraft.mcFolderList.Count == 1 &&
                            folder.Location == Path.Combine(ModBase.exePath, ".minecraft") + @"\"
                                ? Lang.Text("Select.Folder.Clear")
                                : Lang.Text("Common.Action.Delete"), ICON_DELETE, new Thickness(0, 0, 0, 2),
                            ModMain.frmSelectLeft.Delete_Click);
                        break;

                    case ModMinecraft.McFolder.Types.RenamedOriginal:
                        AddMenuItem("Restore", Lang.Text("Select.Folder.RestoreName"), ICON_RENAME,
                            new Thickness(0, 2, 0, 0),
                            ModMain.frmSelectLeft.Restore_Click);
                        AddMenuItem("Rename", Lang.Text("Select.Folder.Rename"), ICON_RENAME, null,
                            ModMain.frmSelectLeft.Rename_Click);
                        AddMenuItem("MoveUp", Lang.Text("Select.Folder.MoveUp"), ICON_MOVEUP, null,
                            ModMain.frmSelectLeft.MoveUp_Click);
                        AddMenuItem("MoveDown", Lang.Text("Select.Folder.MoveDown"), ICON_MOVEDOWN, null,
                            ModMain.frmSelectLeft.MoveDown_Click);
                        AddMenuItem("Open", Lang.Text("Common.Action.Open"), ICON_OPEN, null,
                            ModMain.frmSelectLeft.Open_Click);
                        AddMenuItem("Refresh", Lang.Text("Common.Action.Refresh"), ICON_REFRESH, null,
                            ModMain.frmSelectLeft.Refresh_Click);
                        AddMenuItem("Delete", Lang.Text("Common.Action.Delete"), ICON_DELETE, new Thickness(0, 0, 0, 2),
                            ModMain.frmSelectLeft.Delete_Click);
                        break;

                    case ModMinecraft.McFolder.Types.Custom:
                        AddMenuItem("Rename", Lang.Text("Select.Folder.Rename"), ICON_RENAME, new Thickness(0, 2, 0, 0),
                            ModMain.frmSelectLeft.Rename_Click);
                        AddMenuItem("MoveUp", Lang.Text("Select.Folder.MoveUp"), ICON_MOVEUP, null,
                            ModMain.frmSelectLeft.MoveUp_Click);
                        AddMenuItem("MoveDown", Lang.Text("Select.Folder.MoveDown"), ICON_MOVEDOWN, null,
                            ModMain.frmSelectLeft.MoveDown_Click);
                        AddMenuItem("Open", Lang.Text("Common.Action.Open"), ICON_OPEN, null,
                            ModMain.frmSelectLeft.Open_Click);
                        AddMenuItem("Refresh", Lang.Text("Common.Action.Refresh"), ICON_REFRESH, null,
                            ModMain.frmSelectLeft.Refresh_Click);
                        AddMenuItem("Remove", Lang.Text("Select.Folder.RemoveFromList"),
                            "F1 M 23.3428,25.205L 23.3805,25.4461C 23.9229,27.177 30.261,29.0992 38,29.0992C 45.7386,29.0992 52.0765,27.1771 52.6194,25.4463L 52.6571,25.205C 52.6571,23.3616 46.0949,21.3109 38,21.3109C 29.9051,21.3109 23.3428,23.3616 23.3428,25.205 Z M 23.3428,53.0204L 19.1571,26.2111C 19.0534,25.8817 19,25.5459 19,25.205C 19,20.9036 27.5066,17.4167 38,17.4167C 48.4934,17.4167 57,20.9036 57,25.205C 57,25.5459 56.9466,25.8818 56.8429,26.2112L 52.6571,53.0204L 52.5974,53.0204C 51.9241,56.1393 45.6457,58.5833 38,58.5833C 30.3543,58.5833 24.076,56.1393 23.4026,53.0204L 23.3428,53.0204 Z M 51.8228,30.5485C 48.3585,32.0537 43.4469,32.9933 38,32.9933C 32.5531,32.9933 27.6415,32.0537 24.1771,30.5484L 27.5988,52.464L 27.6857,52.464C 27.6857,53.3857 32.3036,54.6892 38,54.6892C 43.6964,54.6892 48.3143,53.3857 48.3143,52.464L 48.4011,52.464L 51.8228,30.5485 Z ",
                            null, ModMain.frmSelectLeft.Remove_Click);
                        AddMenuItem("Delete", Lang.Text("Common.Action.Delete"), ICON_DELETE, new Thickness(0, 0, 0, 2),
                            ModMain.frmSelectLeft.Delete_Click);
                        break;
                }

                // 控制上移下移显示
                var moveUpItem = contMenu.Items.OfType<MyMenuItem>().FirstOrDefault(x => x.Name == "MoveUp");
                var moveDownItem = contMenu.Items.OfType<MyMenuItem>().FirstOrDefault(x => x.Name == "MoveDown");

                // 如果是第一个项目，隐藏上移按钮
                if (i == 0) moveUpItem.Visibility = Visibility.Collapsed;

                // 如果是最后一个项目，隐藏下移按钮
                if (i == ModMinecraft.mcFolderList.Count - 1) moveDownItem.Visibility = Visibility.Collapsed;

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
                    Logo = Icon.IconButtonSetup,
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
                    Logo = Icon.IconButtonCreate
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
                Logo = Icon.IconButtonAdd
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
                Logo =
                    "F1 m 11.293 11.293 l -3 3 a 1 1 0 0 0 0 1.41406 a 1 1 0 0 0 1.41406 0 L 12 13.4141 l 2.29297 2.29297 a 1 1 0 0 0 1.41406 0 a 1 1 0 0 0 0 -1.41406 l -3 -3 a 1.0001 1.0001 0 0 0 -1.41406 0 z M 12 11 a 1 1 0 0 0 -1 1 v 6 a 1 1 0 0 0 1 1 a 1 1 0 0 0 1 -1 V 12 A 1 1 0 0 0 12 11 Z M 14 1 a 1 1 0 0 0 -1 1 v 5 c 0 1.09272 0.907275 2 2 2 h 5 A 1 1 0 0 0 21 8 A 1 1 0 0 0 20 7 H 15 V 2 A 1 1 0 0 0 14 1 Z M 6 1 C 4.35499 1 3 2.35499 3 4 v 16 c 0 1.64501 1.35499 3 3 3 h 12 c 1.64501 0 3 -1.35499 3 -3 V 8.00195 V 8 C 21.001 7.09394 20.6387 6.22279 19.9961 5.58398 L 16.4121 2 L 16.4101 1.99805 C 15.7718 1.35838 14.9038 0.999054 14 1 Z m 0 2 h 8 a 1.0001 1.0001 0 0 0 0.002 0 c 0.373356 -0.0006051 0.730614 0.147632 0.994141 0.412109 a 1.0001 1.0001 0 0 0 0 0.00195 l 3.58789 3.58789 a 1.0001 1.0001 0 0 0 0.0039 0.00195 C 18.8531 7.26753 19.0006 7.62412 19 7.99805 A 1.0001 1.0001 0 0 0 19 8 v 12 c 0 0.564129 -0.435871 1 -1 1 H 6 C 5.43587 21 5 20.5641 5 20 V 4 C 5 3.43587 5.43587 3 6 3 Z"
            };
            ToolTipService.SetPlacement(itemInstall, PlacementMode.Right);
            ToolTipService.SetHorizontalOffset(itemInstall, -50);
            ToolTipService.SetVerticalOffset(itemInstall, 2.5);
            itemInstall.Click += (_, _) => ModModpack.ModpackInstall();
            ModMain.frmSelectLeft.PanList.Children.Add(itemInstall);

            // 边距
            ModMain.frmSelectLeft.PanList.Children.Add(new FrameworkElement { Height = 10, IsHitTestVisible = false });

            // 确认勾选状态
            for (var i = 0; i < ModMinecraft.mcFolderList.Count; i++)
                if (ModMinecraft.mcFolderList[i].Location == ModMinecraft.mcFolderSelected)
                {
                    ((MyListItem)ModMain.frmSelectLeft.PanList.Children[i + 1]).Checked = true; //去掉第一个标题
                    return;
                }

            if (ModMinecraft.mcFolderList.Count == 0)
                throw new ArgumentNullException("没有可用的 Minecraft 文件夹");
            States.Game.SelectedFolder = ModMinecraft.mcFolderList[0].Location.Replace(ModBase.exePath, "$");
            ((MyListItem)ModMain.frmSelectLeft.PanList.Children[1]).Checked = true;
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "构建 Minecraft 文件夹列表 UI 出错");
        }
        finally
        {
            ModLoader.LoaderFolderRun(ModMinecraft.mcInstanceListLoader,
                ModMinecraft.mcFolderSelected,
                ModLoader.LoaderFolderRunType.RunOnUpdated,
                1,
                "versions\\");
        }
    }

    private void MoveUp_Click(object sender, RoutedEventArgs e)
    {
        var folder =
            (ModMinecraft.McFolder)((MyListItem)((Popup)((ContextMenu)((MyMenuItem)sender).Parent).Parent)
                .PlacementTarget)
            .Tag;
        var index = ModMinecraft.mcFolderList.IndexOf(folder);
        if (index <= 0) return;
        ModMinecraft.mcFolderList.RemoveAt(index);
        ModMinecraft.mcFolderList.Insert(index - 1, folder);
        UpdateFolderOrder();
    }

    private void MoveDown_Click(object sender, RoutedEventArgs e)
    {
        var folder =
            (ModMinecraft.McFolder)((MyListItem)((Popup)((ContextMenu)((MyMenuItem)sender).Parent).Parent)
                .PlacementTarget)
            .Tag;
        var index = ModMinecraft.mcFolderList.IndexOf(folder);
        if (index >= ModMinecraft.mcFolderList.Count - 1) return;
        ModMinecraft.mcFolderList.RemoveAt(index);
        ModMinecraft.mcFolderList.Insert(index + 1, folder);
        UpdateFolderOrder();
    }

    private void UpdateFolderOrder()
    {
        States.Game.Folders = ModMinecraft.mcFolderList
            .Select(folder => $"{folder.Name}>{folder.Location}")
            .ToArray()
            .Join("|");
        McFolderListUI();
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        var folder =
            (ModMinecraft.McFolder)((MyListItem)((Popup)((ContextMenu)((MyMenuItem)sender).Parent).Parent)
                .PlacementTarget)
            .Tag;
        var index = ModMinecraft.mcFolderList.IndexOf(folder);
        ModMinecraft.mcFolderList[index].type = ModMinecraft.McFolder.Types.Original;
        ModMinecraft.mcFolderList[index].Name = Lang.Text("Select.Folder.OfficialLauncherFolder");
        UpdateFolderOrder();
    }

    // 添加文件夹
    private void Add_Click()
    {
        var newFolder = "";
        // 检查是否有下载任务
        if (ModNet.HasDownloadingTask())
        {
            ModMain.Hint(Lang.Text("Select.Folder.CannotAddWhileDownloading"), ModMain.HintType.Critical);
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
                ModMain.Hint(Lang.Text("Select.Folder.InvalidPathChars"), ModMain.HintType.Critical);
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
                    ModMain.Hint(Lang.Text("Select.Folder.AccessDenied"), ModMain.HintType.Critical);
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
                        if (showHint) ModMain.Hint(Lang.Text("Select.Folder.AlreadyInList"));
                        return;
                    }

                    folders[i] = $"{displayName}>{folderPath}";
                    isReplace = true;
                    if (showHint)
                        ModMain.Hint(Lang.Text("Select.Folder.NameUpdated", displayName), ModMain.HintType.Finish);
                    break;
                }

                if (!isAdded) folders.Add($"{displayName}>{folderPath}");
                States.Game.Folders = folders.ToArray().Join("|");
                States.Game.SelectedFolder = folderPath.Replace(ModBase.exePath, "$");
                ModMinecraft.mcFolderListLoader.Start(isForceRestart: true);
                if (isReplace) return;
                if (showHint) ModMain.Hint(Lang.Text("Select.Folder.Added", displayName), ModMain.HintType.Finish);
                var modFolder = new DirectoryInfo(folderPath + @"mods\");
                if (!(modFolder.Exists && modFolder.EnumerateFiles().Count() >= 3)) return;
                var versionFolder = new DirectoryInfo(folderPath + @"versions\");
                if (!(versionFolder.Exists && versionFolder.EnumerateDirectories().Count() <= 3)) return;
                foreach (var VersionPath in versionFolder.EnumerateDirectories())
                {
                    var version = new ModMinecraft.Instance(VersionPath.FullName);
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
            ModMain.Hint(Lang.Text("Select.Folder.CannotCreateWhileDownloading"), ModMain.HintType.Critical);
            return;
        }

        if (!Directory.Exists(ModBase.exePath + @".minecraft\"))
        {
            Directory.CreateDirectory(ModBase.exePath + @".minecraft\");
            Directory.CreateDirectory(ModBase.exePath + @".minecraft\versions\");
            States.Game.SelectedFolder = @"$.minecraft\";
            ModMinecraft.McFolderLauncherProfilesJsonCreate(ModBase.exePath + @".minecraft\");
            ModMain.Hint(Lang.Text("Select.Folder.CreateSuccess"), ModMain.HintType.Finish);
        }

        ModMinecraft.mcFolderListLoader.Start(isForceRestart: true);
    }

    // 右键菜单
    public void Remove_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var folder =
                (ModMinecraft.McFolder)((MyListItem)((Popup)((ContextMenu)((MyMenuItem)sender).Parent).Parent)
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
            ModMain.Hint(
                folder.type == ModMinecraft.McFolder.Types.Custom
                    ? Lang.Text("Select.Folder.RemoveSuccess", name)
                    : Lang.Text("Select.Folder.RestoreSuccess"),
                ModMain.HintType.Finish);
            ModMinecraft.mcFolderListLoader.Start(isForceRestart: true);
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
        var folder = (ModMinecraft.McFolder)listItem.Tag;

        var isClearing =
            folder.type is ModMinecraft.McFolder.Types.Original or ModMinecraft.McFolder.Types.RenamedOriginal
            && folder.Location == ModBase.exePath + @".minecraft\"
            && ModMinecraft.mcFolderList.Count == 1;

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
                ModMain.Hint(inProgress);
                ModBase.DeleteDirectory(folder.Location);
                if (isClearing)
                    Directory.CreateDirectory(folder.Location);
                ModMain.Hint(success, ModMain.HintType.Finish);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, Lang.Text("Select.Folder.Error.Operate", deleteText, folder.Name), ModBase.LogLevel.Hint);
            }
            finally
            {
                ModMinecraft.mcFolderListLoader.Start(isForceRestart: true);
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
        var data = (ModMinecraft.McFolder)((MyListItem)((Popup)((ContextMenu)((MyMenuItem)sender).Parent).Parent)
            .PlacementTarget).Tag;
        RefreshCurrent(data.Location);
    }

    public void RefreshCurrent()
    {
        RefreshCurrent(ModMinecraft.mcFolderSelected);
    }

    public static void RefreshCurrent(string folder)
    {
        ModBase.WriteIni(Path.Combine(folder, "PCL.ini"), "InstanceCache", "");
        if (folder == ModMinecraft.mcFolderSelected)
            ModLoader.LoaderFolderRun(ModMinecraft.mcInstanceListLoader, ModMinecraft.mcFolderSelected,
                ModLoader.LoaderFolderRunType.ForceRun, 1, @"versions\");
    }

    public void Rename_Click(object sender, RoutedEventArgs e)
    {
        var folder =
            (ModMinecraft.McFolder)((MyListItem)((Popup)((ContextMenu)((MyMenuItem)sender).Parent).Parent)
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
            ModMain.Hint(Lang.Text("Select.Folder.NameUpdated", newName), ModMain.HintType.Finish);
            // 保存
            States.Game.Folders = folders.ToArray().Join("|");
            ModMinecraft.mcFolderListLoader.Start(isForceRestart: true);
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
            ModMain.Hint(Lang.Text("Select.Folder.SwitchBlockedByDownload"), ModMain.HintType.Critical);
            e.handled = true;
            return;
        }

        // 更换
        States.Game.SelectedFolder = ((ModMinecraft.McFolder)sender.Tag).Location.Replace(ModBase.exePath, "$");
        ModMinecraft.mcFolderListLoader.Start(isForceRestart: true);
        ModLoader.LoaderFolderRun(ModMinecraft.mcInstanceListLoader, ModMinecraft.mcFolderSelected,
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
            if (e.Data.GetDataPresent(typeof(ModMinecraft.McFolder)))
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
            e.Effects = e.Data.GetDataPresent(typeof(ModMinecraft.McFolder))
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
            var targetFolder = (ModMinecraft.McFolder)targetItem.Tag;

            // 恢复视觉状态
            targetItem.Opacity = 1.0d;

            // 检查数据有效性
            if (!e.Data.GetDataPresent(typeof(ModMinecraft.McFolder)))
            {
                e.Handled = true;
                return;
            }

            var sourceFolder = (ModMinecraft.McFolder)e.Data.GetData(typeof(ModMinecraft.McFolder));

            // 检查是否为有效的拖拽操作
            if (ReferenceEquals(sourceFolder, targetFolder))
            {
                e.Handled = true;
                return;
            }

            // 检查文件夹是否在列表中
            if (!ModMinecraft.mcFolderList.Contains(sourceFolder) || !ModMinecraft.mcFolderList.Contains(targetFolder))
            {
                e.Handled = true;
                return;
            }

            // 获取源文件夹和目标文件夹的索引
            var sourceIndex = ModMinecraft.mcFolderList.IndexOf(sourceFolder);
            var targetIndex = ModMinecraft.mcFolderList.IndexOf(targetFolder);

            // 执行移动操作
            if (sourceIndex == targetIndex) return;
            // 先移除源文件夹
            ModMinecraft.mcFolderList.RemoveAt(sourceIndex);

            // 计算新的插入位置
            int newTargetIndex;

            // 向下拖拽：插入到目标项目的后面
            // 由于移除了源项目，目标索引已经自动减1，所以直接使用TargetIndex就是插入到目标后面
            // 向上拖拽：插入到目标项目的前面
            newTargetIndex = targetIndex;

            // 确保插入位置不超出列表范围
            if (newTargetIndex > ModMinecraft.mcFolderList.Count)
                newTargetIndex = ModMinecraft.mcFolderList.Count;
            else if (newTargetIndex < 0) newTargetIndex = 0;

            // 插入到新位置
            ModMinecraft.mcFolderList.Insert(newTargetIndex, sourceFolder);

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