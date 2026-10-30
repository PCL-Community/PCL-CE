using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.VisualBasic.FileIO;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Profile;
using PCL.Core.Minecraft.Profile.Models;
using PCL.Core.UI;
using PCL.Core.Utils.Validate;
using FileSystem = Microsoft.VisualBasic.FileIO.FileSystem;

namespace PCL;

public partial class PageInstanceOverall
{
    private Loading.LoaderCombo<int> _instanceInfoLoader;

    private bool _isLoad;

    public MyListItem itemVersion;
    private MyCompItem _modpackCompItem;

    public PageInstanceOverall()
    {
        InitializeComponent();
        Loaded += PageSetupLaunch_Loaded;
        LabInfoLoading.Text = Lang.Text("Instance.Overall.Info.Loading");
        // Handles
        ComboDisplayType.SelectionChanged += ComboDisplayType_SelectionChanged;
        BtnDisplayDesc.Click += BtnDisplayDesc_Click;
        BtnDisplayRename.Click += BtnDisplayRename_Click;
        ComboDisplayLogo.SelectionChanged += ComboDisplayLogo_SelectionChanged;
        BtnDisplayStar.Click += BtnDisplayStar_Click;
        BtnFolderVersion.Click += BtnFolderVersion_Click;
        BtnFolderSaves.Click += BtnFolderSaves_Click;
        BtnFolderMods.Click += BtnFolderMods_Click;
        BtnManageScript.Click += BtnManageScript_Click;
        BtnManageCheck.Click += BtnManageCheck_Click;
        BtnManageRestore.Click += BtnManageRestore_Click;
        BtnManageTest.Click += BtnManageTest_Click;
        BtnManageDelete.Click += BtnManageDelete_Click;
        BtnManagePatch.Click += BtnManagePatch_Click;
    }

    private void PageSetupLaunch_Loaded(object sender, RoutedEventArgs e)
    {
        // 重复加载部分
        PanBack.ScrollToHome();

        // 更新设置
        ItemDisplayLogoCustom.Tag = @"PCL\Logo.png";
        Reload();

        // 非重复加载部分
        if (_isLoad)
            return;
        _isLoad = true;
        PanDisplay.TriggerForceResize();
    }

    /// <summary>
    ///     确保当前页面上的信息已正确显示。
    /// </summary>
    private void Reload()
    {
        Animations.AniBeginControl();

        var instance = PageInstanceLeft.mcInstance;
        // 刷新设置项目
        ComboDisplayType.SelectedIndex = States.Instance.CardType[instance.PathInstance];
        BtnDisplayStar.Text = instance.IsStar ? Lang.Text("Instance.Overall.Unfavorite") : Lang.Text("Instance.Overall.Favorite");
        BtnFolderMods.Visibility = instance.Modable ? Visibility.Visible : Visibility.Collapsed;
        // 刷新实例显示
        PanDisplayItem.Children.Clear();
        itemVersion = PageSelectRight.McVersionListItem(instance);
        itemVersion.IsHitTestVisible = false;
        PanDisplayItem.Children.Add(itemVersion);
        Launcher.frmMain.PageNameRefresh();
        // 刷新实例信息
        GetInstanceInfo();
        // 刷新实例图标
        ComboDisplayLogo.SelectedIndex = 0;
        var logo = States.Instance.LogoPath[instance.PathInstance];
        var logoCustom = States.Instance.IsLogoCustom[instance.PathInstance];
        if (logoCustom)
            foreach (MyComboBoxItem Selection in ComboDisplayLogo.Items)
                if (Equals(Selection.Tag, logo) ||
                    (Equals(Selection.Tag, @"PCL\Logo.png") &&
                     logo.EndsWith(@"PCL\Logo.png")))
                {
                    ComboDisplayLogo.SelectedItem = Selection;
                    break;
                }

        Animations.AniEndControl();
    }

    private void GetInstanceInfo()
    {
        _modpackCompItem = null;
        Common.RunInUi(() =>
        {
            PanInfo.Children.Clear();
            PanInfo.Children.Add(new MyLoading { Text = Lang.Text("Instance.Overall.Info.Loading"), Margin = new Thickness(0d, 0d, 0d, 10d) });
        });
        var loaders = new List<Loading.LoaderBase>();
        loaders.Add(new Loading.LoaderTask<int, int>(
            Lang.Text("Instance.Overall.Info.LoadModpackInfoTask"), _ =>
        {
            var modpackId = States.Instance.ModpackId[PageInstanceLeft.mcInstance.PathInstance];
            if (!string.IsNullOrWhiteSpace(modpackId))
            {
                var compProjects =
                    Comp.CompRequest.GetCompProjectsByIds(new List<string> { modpackId });
                if (compProjects.Count > 0)
                    Common.RunInUi(() =>
                    {
                        _modpackCompItem = compProjects.First().ToCompItem(false, false);
                        _modpackCompItem.Tag = compProjects.First();
                    });
            }
        })
        {
            block = true
        });
        loaders.Add(new Loading.LoaderTask<int, int>(
            Lang.Text("Instance.Overall.Info.LoadInstanceInfoTask"), _ => Common.RunInUi(() =>
        {
            var instance = PageInstanceLeft.mcInstance;
            var instanceInfo = instance.Info;
            List<MyListItem> items = [];
            var launchCount = States.Instance.LaunchCount[instance.PathInstance];
            if (launchCount == 0)
                items.Add(new MyListItem
                {
                    Title = Lang.Text("Instance.Overall.Info.LaunchCount.Title"), Info = Lang.Text("Instance.Overall.Info.LaunchCount.Never"), Logo = "pack://application:,,,/images/Blocks/RedstoneLampOff.png"
                });
            else
                items.Add(new MyListItem
                {
                    Title = Lang.Text("Instance.Overall.Info.LaunchCount.Title"),
                    Info = Lang.Text("Instance.Overall.Info.LaunchCount.Count", States.Instance.LaunchCount[instance.PathInstance]),
                    Logo = "pack://application:,,,/images/Blocks/RedstoneLampOn.png"
                });
            if (!string.IsNullOrWhiteSpace(States.Instance.ModpackVersion[instance.PathInstance]))
                items.Add(new MyListItem
                {
                    Title = Lang.Text("Instance.Overall.Info.ModpackVersion"), Info = States.Instance.ModpackVersion[instance.PathInstance],
                    Logo = "pack://application:,,,/images/Blocks/CommandBlock.png"
                });
            items.Add(new MyListItem
            {
                Title = "Minecraft", Info = instanceInfo.VanillaName,
                Logo = "pack://application:,,,/images/Blocks/Grass.png"
            });
            if (instanceInfo.HasForge)
                items.Add(new MyListItem
                {
                    Title = "Forge", Info = instanceInfo.Forge, Logo = "pack://application:,,,/images/Blocks/Anvil.png"
                });
            if (instanceInfo.HasNeoForge)
                items.Add(new MyListItem
                {
                    Title = "NeoForge", Info = instanceInfo.NeoForge,
                    Logo = "pack://application:,,,/images/Blocks/NeoForge.png"
                });
            if (instanceInfo.HasCleanroom)
                items.Add(new MyListItem
                {
                    Title = "Cleanroom", Info = instanceInfo.Cleanroom,
                    Logo = "pack://application:,,,/images/Blocks/Cleanroom.png"
                });
            if (instanceInfo.HasFabric)
                items.Add(new MyListItem
                {
                    Title = "Fabric", Info = instanceInfo.Fabric,
                    Logo = "pack://application:,,,/images/Blocks/Fabric.png"
                });
            if (instanceInfo.HasQuilt)
                items.Add(new MyListItem
                {
                    Title = "Quilt", Info = instanceInfo.Quilt, Logo = "pack://application:,,,/images/Blocks/Quilt.png"
                });
            if (instanceInfo.HasOptiFine)
                items.Add(new MyListItem
                {
                    Title = "OptiFine", Info = instanceInfo.OptiFine,
                    Logo = "pack://application:,,,/images/Blocks/GrassPath.png"
                });
            if (instanceInfo.HasLiteLoader)
                items.Add(new MyListItem
                    { Title = "LiteLoader", Info = Lang.Text("Instance.Overall.Info.Installed"), Logo = "pack://application:,,,/images/Blocks/Egg.png" });
            if (instanceInfo.HasLegacyFabric)
                items.Add(new MyListItem
                {
                    Title = "Legacy Fabric", Info = instanceInfo.LegacyFabric,
                    Logo = "pack://application:,,,/images/Blocks/Fabric.png"
                });
            if (instanceInfo.HasLabyMod)
                items.Add(new MyListItem
                {
                    Title = "LabyMod", Info = instanceInfo.LabyMod,
                    Logo = "pack://application:,,,/images/Blocks/LabyMod.png"
                });
            var wrapPanel = new WrapPanel { Margin = new Thickness(0, -5, -20, 7) };
            foreach (var item in items)
            {
                wrapPanel.Children.Add(item);
                wrapPanel.Children.Add(new TextBlock { Width = 2d });
            }

            PanInfo.Children.Clear();
            if (_modpackCompItem is not null)
            {
                PanInfo.Children.Add(_modpackCompItem);
                PanInfo.Children.Add(new TextBlock());
            }

            PanInfo.Children.Add(wrapPanel);
        })));
        _instanceInfoLoader = new Loading.LoaderCombo<int>("Instance Info Loader", loaders)
            { show = false };
        _instanceInfoLoader.Start();
    }

    #region 卡片：个性化

    // 实例分类
    private void ComboDisplayType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!(_isLoad && !Animations.AniExitsAnyControl()))
            return;
        if (ComboDisplayType.SelectedIndex != 1)
        {
            // 改为不隐藏
            try
            {
                // 若设置分类为可安装 Mod，则显示正常的 Mod 管理页面
                States.Instance.CardType[PageInstanceLeft.mcInstance.PathInstance] =
                    ComboDisplayType.SelectedIndex;
                PageInstanceLeft.mcInstance.displayType =
                    (McInstanceCardType)States.Instance.CardType[
                        PageInstanceLeft.mcInstance.PathInstance];
                Launcher.frmInstanceLeft.RefreshModDisabled();

                Common.WriteIni(GameFolders.mcFolderSelected + "PCL.ini", "InstanceCache",
                    ""); // 要求刷新缓存
                Loading.LoaderFolderRun(InstanceList.mcInstanceListLoader,
                    GameFolders.mcFolderSelected,
                    Loading.LoaderFolderRunType.ForceRun, 1, @"versions\");
            }
            catch (Exception ex)
            {
                Common.Log(
                    ex,
                    $"修改实例分类失败（{PageInstanceLeft.mcInstance.Name}）",
                    Common.LogLevel.Feedback,
                    userSummary: Lang.Text("Instance.Overall.Error.OperationFailed"));
            }

            Reload(); // 更新 “打开 Mod 文件夹” 按钮
        }
        else
        {
            // 改为隐藏
            try
            {
                if (!States.Hint.HideGameInstance)
                {
                if (Launcher.MyMsgBox(
                        Lang.Text("Instance.Overall.Hide.ConfirmMessage"), Lang.Text("Instance.Overall.Hide.ConfirmTitle"), button2: Lang.Text("Common.Action.Cancel")) != 1)
                    {
                        ComboDisplayType.SelectedIndex = 0;
                        return;
                    }

                    States.Hint.HideGameInstance = true;
                }

                States.Instance.CardType[PageInstanceLeft.mcInstance.PathInstance] =
                    (int)McInstanceCardType.Hidden;
                Common.WriteIni(GameFolders.mcFolderSelected + "PCL.ini", "InstanceCache",
                    ""); // 要求刷新缓存
                Loading.LoaderFolderRun(InstanceList.mcInstanceListLoader,
                    GameFolders.mcFolderSelected,
                    Loading.LoaderFolderRunType.ForceRun, 1, @"versions\");
            }
            catch (Exception ex)
            {
                Common.Log(
                    ex,
                    $"隐藏实例 {PageInstanceLeft.mcInstance.Name} 失败",
                    Common.LogLevel.Feedback,
                    userSummary: Lang.Text("Instance.Overall.Error.OperationFailed"));
            }
        }
    }

    // 更改描述
    private void BtnDisplayDesc_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            var oldInfo = States.Instance.CustomInfo[PageInstanceLeft.mcInstance.PathInstance];
            var newInfo = Launcher.MyMsgBoxInput(
                Lang.Text("Instance.Overall.Description.EditTitle"),
                Lang.Text("Instance.Overall.Description.EditMessage"), oldInfo,
                [], Lang.Text("Instance.Overall.Description.Default"));
            if (newInfo is not null && (oldInfo ?? "") != (newInfo ?? ""))
                States.Instance.CustomInfo[PageInstanceLeft.mcInstance.PathInstance] = newInfo;
            PageInstanceLeft.mcInstance = new McInstance(PageInstanceLeft.mcInstance.Name).Load();
            Reload();
            Loading.LoaderFolderRun(InstanceList.mcInstanceListLoader, GameFolders.mcFolderSelected,
                Loading.LoaderFolderRunType.ForceRun, 1, @"versions\");
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                $"实例 {PageInstanceLeft.mcInstance.Name} 描述更改失败",
                Common.LogLevel.Msgbox,
                userSummary: Lang.Text("Instance.Overall.Error.OperationFailed"));
        }
    }

    // 重命名实例
    private void BtnDisplayRename_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            // 确认输入的新名称
            var oldName = PageInstanceLeft.mcInstance.Name;
            var oldPath = PageInstanceLeft.mcInstance.PathInstance;
            // 修改此部分的同时修改快速安装的实例名检测*
            var newName = Launcher.MyMsgBoxInput(Lang.Text("Instance.Overall.Name.EditTitle"), "",
                oldName,
                [
                    new FolderNameValidator(GameFolders.mcFolderSelected + "versions",
                        ignoreCase: false)
                ]);
            if (string.IsNullOrWhiteSpace(newName))
                return;
            var newPath = Path.Combine(GameFolders.mcFolderSelected, "versions", newName);
            // 获取临时中间名，以防止仅修改大小写的重命名失败
            var tempName = newName + "_temp";
            var tempPath = Path.Combine(GameFolders.mcFolderSelected, "versions", tempName);
            var isCaseChangedOnly = (newName.ToLower() ?? "") == (oldName.ToLower() ?? "");
            // 重新加载实例 Json 信息，避免 HMCL 项被合并
            JsonObject jsonObject;
            try
            {
                jsonObject = (JsonObject)Common.GetJson(Common.ReadFile(
                    PageInstanceLeft.mcInstance.PathInstance +
                    PageInstanceLeft.mcInstance.Name + ".json"));
            }
            catch (Exception ex)
            {
                Common.Log(ex, "重命名读取 Json 时失败");
                jsonObject = PageInstanceLeft.mcInstance.JsonObject;
            }

            // 重命名主文件夹
            FileSystem.RenameDirectory(oldPath, tempName);
            FileSystem.RenameDirectory(tempPath, newName);
            // 清理 ini 缓存
            Common.IniClearCache(Path.Combine(PageInstanceLeft.mcInstance.PathIndie,
                "options.txt"));
            // 重命名 Jar 文件与 natives 文件夹
            // 不能进行遍历重命名，否则在实例名很短的时候容易误伤其他文件（Meloong-Git/#6443）
            if (Directory.Exists(Path.Combine(newPath, $"{oldName}-natives")))
            {
                if (isCaseChangedOnly)
                {
                    FileSystem.RenameDirectory(Path.Combine(newPath, $"{oldName}-natives"), $"{oldName}natives_temp");
                    FileSystem.RenameDirectory(Path.Combine(newPath, $"{oldName}-natives_temp"), $"{newName}-natives");
                }
                else
                {
                    Common.DeleteDirectory(Path.Combine(newPath, $"{newName}-natives"));
                    FileSystem.RenameDirectory(Path.Combine(newPath, $"{oldName}-natives"), $"{newName}-natives");
                }
            }

            if (File.Exists(Path.Combine(newPath, $"{oldName}.jar")))
            {
                if (isCaseChangedOnly)
                {
                    FileSystem.RenameFile(Path.Combine(newPath, $"{oldName}.jar"), $"{oldName}_temp.jar");
                    FileSystem.RenameFile(Path.Combine(newPath, $"{oldName}_temp.jar"), $"{newName}.jar");
                }
                else
                {
                    File.Delete(Path.Combine(newPath, $"{newName}.jar"));
                    FileSystem.RenameFile(Path.Combine(newPath, $"{oldName}.jar"), $"{newName}.jar");
                }
            }

            // 替换实例设置文件中的路径
            if (File.Exists(Path.Combine(newPath, "PCL", "Setup.ini")))
                Common.WriteFile(Path.Combine(newPath, "PCL", "Setup.ini"),
                    Common.ReadFile(Path.Combine(newPath, "PCL", "Setup.ini"))
                        .Replace(oldPath, newPath));
            // 更改已选中的实例
            if ((Common.ReadIni(GameFolders.mcFolderSelected + "PCL.ini", "Version") ?? "") ==
                (oldName ?? ""))
                Common.WriteIni(GameFolders.mcFolderSelected + "PCL.ini", "Version", newName);
            // 写入实例 Json，并删除旧的 Json
            try
            {
                jsonObject["id"] = newName;
                Common.WriteFile(Path.Combine(newPath, $"{newName}.json"), jsonObject.ToString());
                if (!isCaseChangedOnly)
                    File.Delete(Path.Combine(newPath, $"{oldName}.json"));
            }
            catch (Exception ex)
            {
                Common.Log(ex, "重命名实例 Json 失败");
            }

            // 刷新与提示
            HintService.Hint(Lang.Text("Instance.Overall.Name.RenameSuccess"), HintType.Success);
            PageInstanceLeft.mcInstance = new McInstance(newName).Load();
            if (InstanceList.McMcInstanceSelected is not null &&
                InstanceList.McMcInstanceSelected.Equals(PageInstanceLeft.mcInstance))
                Common.WriteIni(GameFolders.mcFolderSelected + "PCL.ini", "Version", newName);
            Reload();
            Loading.LoaderFolderRun(InstanceList.mcInstanceListLoader, GameFolders.mcFolderSelected,
                Loading.LoaderFolderRunType.ForceRun, 1, @"versions\");
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "重命名实例失败",
                Common.LogLevel.Msgbox,
                userSummary: Lang.Text("Instance.Overall.Error.OperationFailed"));
        }
    }

    // 实例图标
    private void ComboDisplayLogo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!(_isLoad && !Animations.AniExitsAnyControl()))
            return;
        // 选择 自定义 时修改图片
        try
        {
            if (ReferenceEquals(ComboDisplayLogo.SelectedItem, ItemDisplayLogoCustom))
            {
                var fileName = SystemDialogs.SelectFile(Lang.Text("Instance.Overall.Icon.SelectFile.Filter"), Lang.Text("Instance.Overall.Icon.SelectFile.Title"));
                if (string.IsNullOrEmpty(fileName))
                {
                    Reload(); // 还原选项
                    return;
                }

                Common.CopyFile(fileName,
                    PageInstanceLeft.mcInstance.PathInstance + @"PCL\Logo.png");
            }
            else
            {
                File.Delete(PageInstanceLeft.mcInstance.PathInstance + @"PCL\Logo.png");
            }
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                $"更改自定义实例图标失败（{PageInstanceLeft.mcInstance.Name}）",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Instance.Overall.Error.OperationFailed"));
        }

        // 进行更改
        try
        {
            string newLogo = ((MyComboBoxItem)ComboDisplayLogo.SelectedItem).Tag?.ToString();
            States.Instance.LogoPath[PageInstanceLeft.mcInstance.PathInstance] = newLogo;
            States.Instance.IsLogoCustom[PageInstanceLeft.mcInstance.PathInstance] =
                !string.IsNullOrEmpty(newLogo);
            // 刷新显示
            Common.WriteIni(GameFolders.mcFolderSelected + "PCL.ini", "InstanceCache",
                ""); // 要求刷新缓存
            PageInstanceLeft.mcInstance = new McInstance(PageInstanceLeft.mcInstance.Name).Load();
            Reload();
            Loading.LoaderFolderRun(InstanceList.mcInstanceListLoader, GameFolders.mcFolderSelected,
                Loading.LoaderFolderRunType.ForceRun, 1, @"versions\");
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                $"更改实例图标失败（{PageInstanceLeft.mcInstance.Name}）",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Instance.Overall.Error.OperationFailed"));
        }
    }

    // 收藏夹
    private void BtnDisplayStar_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            States.Instance.Starred[PageInstanceLeft.mcInstance.PathInstance] =
                !PageInstanceLeft.mcInstance.IsStar;
            PageInstanceLeft.mcInstance = new McInstance(PageInstanceLeft.mcInstance.Name).Load();
            Reload();
            InstanceList.mcInstanceListForceRefresh = true;
            Loading.LoaderFolderRun(InstanceList.mcInstanceListLoader, GameFolders.mcFolderSelected,
                Loading.LoaderFolderRunType.ForceRun, 1, @"versions\");
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                $"实例 {PageInstanceLeft.mcInstance.Name} 收藏状态更改失败",
                Common.LogLevel.Msgbox,
                userSummary: Lang.Text("Instance.Overall.Error.OperationFailed"));
        }
    }

    #endregion

    #region 卡片：快捷方式

    // 实例文件夹
    private void BtnFolderVersion_Click(object sender, MouseButtonEventArgs mouseButtonEventArgs)
    {
        OpenVersionFolder(PageInstanceLeft.mcInstance);
    }

    public static void OpenVersionFolder(McInstance version)
    {
        Common.OpenExplorer(version.PathInstance);
    }

    // 存档文件夹
    private void BtnFolderSaves_Click(object sender, MouseButtonEventArgs mouseButtonEventArgs)
    {
        var folderPath = PageInstanceLeft.mcInstance.PathIndie + @"saves\";
        Directory.CreateDirectory(folderPath);
        Common.OpenExplorer(folderPath);
    }

    // Mod 文件夹
    private void BtnFolderMods_Click(object sender, MouseButtonEventArgs mouseButtonEventArgs)
    {
        var folderPath = PageInstanceLeft.mcInstance.PathIndie + @"mods\";
        Directory.CreateDirectory(folderPath);
        Common.OpenExplorer(folderPath);
    }

    #endregion

    #region 卡片：管理

    // 导出启动脚本
    private void BtnManageScript_Click(object sender, MouseButtonEventArgs mouseButtonEventArgs)
    {
        try
        {
            // 弹窗要求指定脚本的保存位置
            var savePath = SystemDialogs.SelectSaveFile(
                Lang.Text("Instance.Overall.Script.SelectSaveTitle"),
                "启动 " + PageInstanceLeft.mcInstance.Name + ".bat",
                Lang.Text("Instance.Overall.Script.FileFilter"));
            if (string.IsNullOrEmpty(savePath))
                return;
            // 检查中断（等玩家选完弹窗指不定任务就结束了呢……）
            if (GameLaunch.mcLaunchLoader.State == Common.LoadState.Loading)
            {
                HintService.Hint(Lang.Text("Instance.Overall.Script.WaitForLaunchTask"), HintType.Error);
                return;
            }

            // 生成脚本
            if (GameLaunch.McLaunchStart(new GameLaunch.McLaunchOptions
                    { SaveBatch = savePath, instance = PageInstanceLeft.mcInstance }))
            {
                if (ProfileService.Current?.ProfileType == ProfileType.Offline)
                    HintService.Hint(Lang.Text("Instance.Overall.Script.Exporting"));
                else
                    HintService.Hint(Lang.Text("Instance.Overall.Script.ExportingWarning"));
            }
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                $"导出启动脚本失败（{PageInstanceLeft.mcInstance.Name}）",
                Common.LogLevel.Msgbox,
                userSummary: Lang.Text("Instance.Overall.Error.OperationFailed"));
        }
    }

    // 补全文件
    private void BtnManageCheck_Click(object sender, MouseButtonEventArgs e)
    {
        if (!RestrictedFeaturePolicy.Check("minecraft-repair")) return;
        try
        {
            // 忽略文件检查提示
            if (Libraries.ShouldIgnoreFileCheck(PageInstanceLeft.mcInstance))
            {
                HintService.Hint(Lang.Text("Instance.Overall.Repair.DisableVerificationHint"));
                return;
            }

            // 重复任务检查
            var taskName = PageInstanceLeft.mcInstance.Name + " " +
                           Lang.Text("Instance.Overall.Repair.TaskName");
            foreach (var OngoingLoader in Loading.loaderTaskbar)
            {
                if ((OngoingLoader.name ?? "") != (taskName ?? ""))
                    continue;
                HintService.Hint(Lang.Text("Instance.Overall.Repair.Processing"), HintType.Error);
                return;
            }

            // 启动
            var loader = new Loading.LoaderCombo<string>(taskName,
                GameDownload.DlClientFix(PageInstanceLeft.mcInstance, true,
                    GameDownload.AssetsIndexExistsBehaviour.AlwaysDownload));
            loader.OnStateChanged = _ =>
            {
                switch (loader.State)
                {
                    case Common.LoadState.Finished:
                    {
                        HintService.Hint(
                            Lang.Text("Instance.Overall.Repair.Success.WithTaskName", taskName), HintType.Success);
                        break;
                    }
                    case Common.LoadState.Failed:
                    {
                        HintService.Hint(
                            Lang.Text("Instance.Overall.Repair.Failed.WithDetail", taskName, loader.Error.ToString()),
                            HintType.Error);
                        break;
                    }
                    case Common.LoadState.Aborted:
                    {
                        HintService.Hint(
                            Lang.Text("Instance.Overall.Repair.Cancelled.WithTaskName", taskName));
                        break;
                    }
                }
            };
            loader.Start(PageInstanceLeft.mcInstance.Name);
            Loading.LoaderTaskbarAdd(loader);
            Launcher.frmMain.BtnExtraDownload.ShowRefresh();
            Launcher.frmMain.BtnExtraDownload.Ribble();
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                $"尝试补全文件失败（{PageInstanceLeft.mcInstance.Name}）",
                Common.LogLevel.Msgbox,
                userSummary: Lang.Text("Instance.Overall.Error.OperationFailed"));
        }
    }

    // 重置
    private void BtnManageRestore_Click(object sender, MouseButtonEventArgs e)
    {
        if (!RestrictedFeaturePolicy.Check("minecraft-reset")) return;
        try
        {
            var currentVersion = PageInstanceLeft.mcInstance.Info;
            if (!(currentVersion.Drop == 99) &&
                McVersionComparer.CompareVersion(currentVersion.VanillaName, "1.5.2") == -1 && currentVersion.HasForge)
            {
                HintService.Hint(Lang.Text("Instance.Overall.Reset.NotSupported"));
                return;
            }

            if (currentVersion.HasQuilt)
            {
                HintService.Hint(Lang.Text("Instance.Overall.Reset.QuiltUnsupported"));
                return;
            }

            // 确认操作
            if (Launcher.MyMsgBox(
                    Lang.Text("Instance.Overall.Reset.ConfirmMessage",
                        PageInstanceLeft.mcInstance.Name),
                    Lang.Text("Instance.Overall.Reset.ConfirmTitle"),
                    Lang.Text("Common.Action.Confirm"), Lang.Text("Common.Action.Cancel")) == 2)
                return;

            // 备份实例核心文件
            Common.CopyFile(
                PageInstanceLeft.mcInstance.PathInstance + PageInstanceLeft.mcInstance.Name +
                ".json",
                PageInstanceLeft.mcInstance.PathInstance + @"PCLInstallBackups\" +
                PageInstanceLeft.mcInstance.Name +
                ".json");
            Common.CopyFile(
                PageInstanceLeft.mcInstance.PathInstance + PageInstanceLeft.mcInstance.Name +
                ".jar",
                PageInstanceLeft.mcInstance.PathInstance + @"PCLInstallBackups\" +
                PageInstanceLeft.mcInstance.Name +
                ".jar");
            // 提交安装申请
            var request = new DownloadUi.McInstallRequest
            {
                targetInstanceName = PageInstanceLeft.mcInstance.Name,
                targetInstanceFolder =
                    $@"{GameFolders.mcFolderSelected}versions\{PageInstanceLeft.mcInstance.Name}\",
                minecraftName = currentVersion.VanillaName,
                optiFineEntry = currentVersion.HasOptiFine
                    ? new GameDownload.DlOptiFineListEntry
                    {
                        Inherit = currentVersion.VanillaName,
                        DisplayName = currentVersion.VanillaName + " " + currentVersion.OptiFine
                    }
                    : null,
                forgeEntry = currentVersion.HasForge
                    ? new GameDownload.DlForgeVersionEntry(currentVersion.Forge, null,
                            currentVersion.VanillaName)
                        { Category = "installer" }
                    : null,
                forgeVersion = currentVersion.HasForge ? currentVersion.Forge : null,
                neoForgeVersion = currentVersion.HasNeoForge ? currentVersion.NeoForge : null,
                cleanroomVersion = currentVersion.HasCleanroom ? currentVersion.Cleanroom : null,
                fabricVersion = currentVersion.HasFabric ? currentVersion.Fabric : null,
                liteLoaderEntry = currentVersion.HasLiteLoader
                    ? new GameDownload.DlLiteLoaderListEntry
                        { Inherit = currentVersion.VanillaName }
                    : null,
                legacyFabricVersion = currentVersion.HasLegacyFabric ? currentVersion.LegacyFabric : null
            };
            // .MinecraftJson = CurrentVersion.McName,
            if (!DownloadUi.McInstall(request, Lang.Text("Common.Action.Reset")))
                return;
            Launcher.frmMain.PageChange(new FormMain.PageStackData
                { page = FormMain.PageType.Launch });
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                $"重置实例 {PageInstanceLeft.mcInstance.Name} 失败",
                Common.LogLevel.Msgbox,
                userSummary: Lang.Text("Instance.Overall.Error.OperationFailed"));
        }
    }

    // 测试游戏
    private void BtnManageTest_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            GameLaunch.McLaunchStart(new GameLaunch.McLaunchOptions
                { instance = PageInstanceLeft.mcInstance, IsTest = true });
            Launcher.frmMain.PageChange(FormMain.PageType.Launch);
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "测试游戏失败",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Instance.Overall.Error.OperationFailed"));
        }
    }

    // 删除实例
    // 修改此代码时，同时修改 PageSelectRight 中的代码
    private void BtnManageDelete_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            var isShiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

            var isIsolatedInstance =
                PageInstanceLeft.mcInstance.state != McInstanceState.Error &&
                !string.Equals(
                    PageInstanceLeft.mcInstance.PathIndie,
                    GameFolders.mcFolderSelected,
                    StringComparison.OrdinalIgnoreCase
                );

            var confirmMessageKey = (isIsolatedInstance, isShiftPressed) switch
            {
                (true, true) => "Instance.Overall.Delete.ConfirmMessageIsolatedPermanent",
                (true, false) => "Instance.Overall.Delete.ConfirmMessageIsolated",
                (false, true) => "Instance.Overall.Delete.ConfirmMessagePermanent",
                (false, false) => "Instance.Overall.Delete.ConfirmMessage"
            };

            var confirmResult = Launcher.MyMsgBox(
                Lang.Text(confirmMessageKey, PageInstanceLeft.mcInstance.Name),
                Lang.Text("Instance.Overall.Delete.ConfirmTitle"),
                button2: Lang.Text("Common.Action.Cancel"),
                isWarn: isIsolatedInstance || isShiftPressed
            );

            switch (confirmResult)
            {
                case 1:
                {
                    var instancePath = PageInstanceLeft.mcInstance.PathInstance;
                    var instanceName = PageInstanceLeft.mcInstance.Name;
                    Common.IniClearCache(Path.Combine(PageInstanceLeft.mcInstance.PathIndie,
                        "options.txt"));
                    ((DynamicCacheConfigStorage)ConfigService.GetProvider(ConfigSource.GameInstance)).InvalidateCache(
                        instancePath);
                    if (isShiftPressed)
                    {
                        Common.DeleteDirectory(instancePath);
                        HintService.Hint(Lang.Text("Instance.Overall.Delete.PermanentSuccess", instanceName),
                            HintType.Success);
                    }
                    else
                    {
                        FileSystem.DeleteDirectory(instancePath, UIOption.OnlyErrorDialogs,
                            RecycleOption.SendToRecycleBin);
                        HintService.Hint(Lang.Text("Instance.Overall.Delete.RecycleBinSuccess", instanceName),
                            HintType.Success);
                    }

                    break;
                }
                case 2:
                {
                    return;
                }
            }

            Loading.LoaderFolderRun(InstanceList.mcInstanceListLoader, GameFolders.mcFolderSelected,
                Loading.LoaderFolderRunType.ForceRun, 1, @"versions\");
            Launcher.frmMain.PageBack();
        }
        catch (OperationCanceledException ex)
        {
            Common.Log(ex, "删除实例 " + PageInstanceLeft.mcInstance.Name + " 被主动取消");
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                $"删除实例 {PageInstanceLeft.mcInstance.Name} 失败",
                Common.LogLevel.Msgbox,
                userSummary: Lang.Text("Instance.Overall.Error.OperationFailed"));
        }
    }

    // 修补核心
    private void BtnManagePatch_Click(object sender, MouseButtonEventArgs e)
    {
        switch (Launcher.MyMsgBox(
                    Lang.Text("Instance.Overall.Patch.ConfirmMessage",
                        PageInstanceLeft.mcInstance.Name),
                    Lang.Text("Instance.Overall.Patch.ConfirmTitle"), button2: Lang.Text("Common.Action.Cancel")))
        {
            case 1:
            {
                var userInput = SystemDialogs.SelectFile(Lang.Text("Instance.Overall.Patch.SelectFile.Filter"), Lang.Text("Instance.Overall.Patch.SelectFile.Title"));
                if (userInput is null || string.IsNullOrWhiteSpace(userInput))
                    return;
                HintService.Hint(Lang.Text("Instance.Overall.Patch.Patching"));
                Common.RunInNewThread(() =>
                {
                    var core = new GameCore(PageInstanceLeft.mcInstance.PathInstance +
                                            PageInstanceLeft.mcInstance.Name +
                                            ".jar");
                    core.AddToCore(userInput);
                    HintService.Hint(Lang.Text("Instance.Overall.Patch.Success"), HintType.Success);
                    Config.Instance.DisableAssetVerifyV2[PageInstanceLeft.mcInstance.PathInstance] =
                        true;
                });
                break;
            }
            case 2:
            {
                return;
            }
        }
    }

    #endregion
}
