using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FluentValidation;
using Microsoft.VisualBasic.FileIO;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.Minecraft;
using PCL.Core.UI;
using PCL.Core.Utils.Validate;
using FileSystem = Microsoft.VisualBasic.FileIO.FileSystem;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageInstanceOverall
{
    private ModLoader.LoaderCombo<int> InstanceInfoLoader;

    private bool IsLoad;

    public MyListItem ItemVersion;
    private MyCompItem ModpackCompItem;

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
        if (IsLoad)
            return;
        IsLoad = true;
        PanDisplay.TriggerForceResize();
    }

    /// <summary>
    ///     确保当前页面上的信息已正确显示。
    /// </summary>
    private void Reload()
    {
        ModAnimation.AniControlEnabled += 1;

        var instance = PageInstanceLeft.Instance;
        // 刷新设置项目
        ComboDisplayType.SelectedIndex = States.Instance.CardType[instance.PathInstance];
        BtnDisplayStar.Text = instance.IsStar ? Lang.Text("Instance.Overall.Unfavorite") : Lang.Text("Instance.Overall.Favorite");
        BtnFolderMods.Visibility = instance.Modable ? Visibility.Visible : Visibility.Collapsed;
        // 刷新实例显示
        PanDisplayItem.Children.Clear();
        ItemVersion = PageSelectRight.McVersionListItem(instance);
        ItemVersion.IsHitTestVisible = false;
        PanDisplayItem.Children.Add(ItemVersion);
        ModMain.FrmMain.PageNameRefresh();
        // 刷新实例信息
        GetInstanceInfo();
        // 刷新实例图标
        ComboDisplayLogo.SelectedIndex = 0;
        var Logo = States.Instance.LogoPath[instance.PathInstance];
        var LogoCustom = States.Instance.IsLogoCustom[instance.PathInstance];
        if (LogoCustom)
            foreach (MyComboBoxItem Selection in ComboDisplayLogo.Items)
                if (Equals(Selection.Tag, Logo) ||
                    (Equals(Selection.Tag, @"PCL\Logo.png") &&
                     Logo.EndsWith(@"PCL\Logo.png")))
                {
                    ComboDisplayLogo.SelectedItem = Selection;
                    break;
                }

        ModAnimation.AniControlEnabled -= 1;
    }

    private void GetInstanceInfo()
    {
        ModpackCompItem = null;
        ModBase.RunInUi(() =>
        {
            PanInfo.Children.Clear();
            PanInfo.Children.Add(new MyLoading { Text = Lang.Text("Instance.Overall.Info.Loading"), Margin = new Thickness(0d, 0d, 0d, 10d) });
        });
        var loaders = new List<ModLoader.LoaderBase>();
        loaders.Add(new ModLoader.LoaderTask<int, int>(Lang.Text("Instance.Overall.Info.LoadModpackInfoTask"), _ =>
        {
            var modpackId = States.Instance.ModpackId[PageInstanceLeft.Instance.PathInstance];
            if (!string.IsNullOrWhiteSpace(modpackId))
            {
                var compProjects = ModComp.CompRequest.GetCompProjectsByIds(new List<string> { modpackId });
                if (compProjects.Count > 0)
                    ModBase.RunInUi(() =>
                    {
                        ModpackCompItem = compProjects.First().ToCompItem(false, false);
                        ModpackCompItem.Tag = compProjects.First();
                    });
            }
        })
        {
            Block = true
        });
        loaders.Add(new ModLoader.LoaderTask<int, int>(Lang.Text("Instance.Overall.Info.LoadInstanceInfoTask"), _ => ModBase.RunInUi(() =>
        {
            var instance = PageInstanceLeft.Instance;
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
            if (ModpackCompItem is not null)
            {
                PanInfo.Children.Add(ModpackCompItem);
                PanInfo.Children.Add(new TextBlock());
            }

            PanInfo.Children.Add(wrapPanel);
        })));
        InstanceInfoLoader = new ModLoader.LoaderCombo<int>("Instance Info Loader", loaders) { Show = false };
        InstanceInfoLoader.Start();
    }

    #region 卡片：个性化

    // 实例分类
    private void ComboDisplayType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!(IsLoad && ModAnimation.AniControlEnabled == 0))
            return;
        if (ComboDisplayType.SelectedIndex != 1)
        {
            // 改为不隐藏
            try
            {
                // 若设置分类为可安装 Mod，则显示正常的 Mod 管理页面
                States.Instance.CardType[PageInstanceLeft.Instance.PathInstance] = ComboDisplayType.SelectedIndex;
                PageInstanceLeft.Instance.DisplayType = (ModMinecraft.McInstanceCardType)States.Instance.CardType[PageInstanceLeft.Instance.PathInstance];
                ModMain.FrmInstanceLeft.RefreshModDisabled();

                ModBase.WriteIni(ModMinecraft.McFolderSelected + "PCL.ini", "InstanceCache", ""); // 要求刷新缓存
                ModLoader.LoaderFolderRun(ModMinecraft.McInstanceListLoader, ModMinecraft.McFolderSelected,
                    ModLoader.LoaderFolderRunType.ForceRun, 1, @"versions\");
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "修改实例分类失败（" + PageInstanceLeft.Instance.Name + "）", ModBase.LogLevel.Feedback);
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
                if (ModMain.MyMsgBox(
                        Lang.Text("Instance.Overall.Hide.ConfirmMessage"), Lang.Text("Instance.Overall.Hide.ConfirmTitle"), Button2: Lang.Text("Common.Action.Cancel")) != 1)
                    {
                        ComboDisplayType.SelectedIndex = 0;
                        return;
                    }

                    States.Hint.HideGameInstance = true;
                }

                States.Instance.CardType[PageInstanceLeft.Instance.PathInstance] =
                    (int)ModMinecraft.McInstanceCardType.Hidden;
                ModBase.WriteIni(ModMinecraft.McFolderSelected + "PCL.ini", "InstanceCache", ""); // 要求刷新缓存
                ModLoader.LoaderFolderRun(ModMinecraft.McInstanceListLoader, ModMinecraft.McFolderSelected,
                    ModLoader.LoaderFolderRunType.ForceRun, 1, @"versions\");
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "隐藏实例 " + PageInstanceLeft.Instance.Name + " 失败", ModBase.LogLevel.Feedback);
            }
        }
    }

    // 更改描述
    private void BtnDisplayDesc_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            var OldInfo = States.Instance.CustomInfo[PageInstanceLeft.Instance.PathInstance];
            var NewInfo = ModMain.MyMsgBoxInput(Lang.Text("Instance.Overall.Description.EditTitle"), Lang.Text("Instance.Overall.Description.EditMessage"), OldInfo,
                [], Lang.Text("Instance.Overall.Description.Default"));
            if (NewInfo is not null && (OldInfo ?? "") != (NewInfo ?? ""))
                States.Instance.CustomInfo[PageInstanceLeft.Instance.PathInstance] = NewInfo;
            PageInstanceLeft.Instance = new ModMinecraft.McInstance(PageInstanceLeft.Instance.Name).Load();
            Reload();
            ModLoader.LoaderFolderRun(ModMinecraft.McInstanceListLoader, ModMinecraft.McFolderSelected,
                ModLoader.LoaderFolderRunType.ForceRun, 1, @"versions\");
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "实例 " + PageInstanceLeft.Instance.Name + " 描述更改失败", ModBase.LogLevel.Msgbox);
        }
    }

    // 重命名实例
    private void BtnDisplayRename_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            // 确认输入的新名称
            var OldName = PageInstanceLeft.Instance.Name;
            var OldPath = PageInstanceLeft.Instance.PathInstance;
            // 修改此部分的同时修改快速安装的实例名检测*
            var NewName = ModMain.MyMsgBoxInput(Lang.Text("Instance.Overall.Name.EditTitle"), "", OldName,
                [new FolderNameValidator(ModMinecraft.McFolderSelected + "versions", ignoreCase: false)]);
            if (string.IsNullOrWhiteSpace(NewName))
                return;
            var NewPath = Path.Combine(ModMinecraft.McFolderSelected, "versions", NewName);
            // 获取临时中间名，以防止仅修改大小写的重命名失败
            var TempName = NewName + "_temp";
            var TempPath = Path.Combine(ModMinecraft.McFolderSelected, "versions", TempName);
            var IsCaseChangedOnly = (NewName.ToLower() ?? "") == (OldName.ToLower() ?? "");
            // 重新加载实例 Json 信息，避免 HMCL 项被合并
            JsonObject JsonObject;
            try
            {
                JsonObject = (JsonObject)ModBase.GetJson(ModBase.ReadFile(PageInstanceLeft.Instance.PathInstance +
                                                                       PageInstanceLeft.Instance.Name + ".json"));
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "重命名读取 Json 时失败");
                JsonObject = PageInstanceLeft.Instance.JsonObject;
            }

            // 重命名主文件夹
            FileSystem.RenameDirectory(OldPath, TempName);
            FileSystem.RenameDirectory(TempPath, NewName);
            // 清理 ini 缓存
            ModBase.IniClearCache(Path.Combine(PageInstanceLeft.Instance.PathIndie, "options.txt"));
            // 重命名 Jar 文件与 natives 文件夹
            // 不能进行遍历重命名，否则在实例名很短的时候容易误伤其他文件（Meloong-Git/#6443）
            if (Directory.Exists($"{NewPath}{OldName}-natives"))
            {
                if (IsCaseChangedOnly)
                {
                    FileSystem.RenameDirectory($"{NewPath}{OldName}-natives", $"{OldName}natives_temp");
                    FileSystem.RenameDirectory($"{NewPath}{OldName}-natives_temp", $"{NewName}-natives");
                }
                else
                {
                    ModBase.DeleteDirectory($"{NewPath}{NewName}-natives");
                    FileSystem.RenameDirectory($"{NewPath}{OldName}-natives", $"{NewName}-natives");
                }
            }

            if (File.Exists($"{NewPath}{OldName}.jar"))
            {
                if (IsCaseChangedOnly)
                {
                    FileSystem.RenameFile($"{NewPath}{OldName}.jar", $"{OldName}_temp.jar");
                    FileSystem.RenameFile($"{NewPath}{OldName}_temp.jar", $"{NewName}.jar");
                }
                else
                {
                    File.Delete($"{NewPath}{NewName}.jar");
                    FileSystem.RenameFile($"{NewPath}{OldName}.jar", $"{NewName}.jar");
                }
            }

            // 替换实例设置文件中的路径
            if (File.Exists(NewPath + @"PCL\Setup.ini"))
                ModBase.WriteFile(NewPath + @"PCL\Setup.ini",
                    ModBase.ReadFile(NewPath + @"PCL\Setup.ini").Replace(OldPath, NewPath));
            // 更改已选中的实例
            if ((ModBase.ReadIni(ModMinecraft.McFolderSelected + "PCL.ini", "Version") ?? "") == (OldName ?? ""))
                ModBase.WriteIni(ModMinecraft.McFolderSelected + "PCL.ini", "Version", NewName);
            // 写入实例 Json
            try
            {
                JsonObject["id"] = NewName;
                ModBase.WriteFile(NewPath + NewName + ".json", JsonObject.ToString());
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "重命名实例 Json 失败");
            }

            // 刷新与提示
            ModMain.Hint(Lang.Text("Instance.Overall.Name.RenameSuccess"), ModMain.HintType.Finish);
            PageInstanceLeft.Instance = new ModMinecraft.McInstance(NewName).Load();
            if (ModMinecraft.McInstanceSelected is not null &&
                ModMinecraft.McInstanceSelected.Equals(PageInstanceLeft.Instance))
                ModBase.WriteIni(ModMinecraft.McFolderSelected + "PCL.ini", "Version", NewName);
            Reload();
            ModLoader.LoaderFolderRun(ModMinecraft.McInstanceListLoader, ModMinecraft.McFolderSelected,
                ModLoader.LoaderFolderRunType.ForceRun, 1, @"versions\");
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "重命名实例失败", ModBase.LogLevel.Msgbox);
        }
    }

    // 实例图标
    private void ComboDisplayLogo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!(IsLoad && ModAnimation.AniControlEnabled == 0))
            return;
        // 选择 自定义 时修改图片
        try
        {
            if (ReferenceEquals(ComboDisplayLogo.SelectedItem, ItemDisplayLogoCustom))
            {
                var FileName = SystemDialogs.SelectFile(Lang.Text("Instance.Overall.Icon.SelectFile.Filter"), Lang.Text("Instance.Overall.Icon.SelectFile.Title"));
                if (string.IsNullOrEmpty(FileName))
                {
                    Reload(); // 还原选项
                    return;
                }

                ModBase.CopyFile(FileName, PageInstanceLeft.Instance.PathInstance + @"PCL\Logo.png");
            }
            else
            {
                File.Delete(PageInstanceLeft.Instance.PathInstance + @"PCL\Logo.png");
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "更改自定义实例图标失败（" + PageInstanceLeft.Instance.Name + "）", ModBase.LogLevel.Feedback);
        }

        // 进行更改
        try
        {
            string NewLogo = ((MyComboBoxItem)ComboDisplayLogo.SelectedItem).Tag?.ToString();
            States.Instance.LogoPath[PageInstanceLeft.Instance.PathInstance] = NewLogo;
            States.Instance.IsLogoCustom[PageInstanceLeft.Instance.PathInstance] = !string.IsNullOrEmpty(NewLogo);
            // 刷新显示
            ModBase.WriteIni(ModMinecraft.McFolderSelected + "PCL.ini", "InstanceCache", ""); // 要求刷新缓存
            PageInstanceLeft.Instance = new ModMinecraft.McInstance(PageInstanceLeft.Instance.Name).Load();
            Reload();
            ModLoader.LoaderFolderRun(ModMinecraft.McInstanceListLoader, ModMinecraft.McFolderSelected,
                ModLoader.LoaderFolderRunType.ForceRun, 1, @"versions\");
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "更改实例图标失败（" + PageInstanceLeft.Instance.Name + "）", ModBase.LogLevel.Feedback);
        }
    }

    // 收藏夹
    private void BtnDisplayStar_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            States.Instance.Starred[PageInstanceLeft.Instance.PathInstance] = !PageInstanceLeft.Instance.IsStar;
            PageInstanceLeft.Instance = new ModMinecraft.McInstance(PageInstanceLeft.Instance.Name).Load();
            Reload();
            ModMinecraft.McInstanceListForceRefresh = true;
            ModLoader.LoaderFolderRun(ModMinecraft.McInstanceListLoader, ModMinecraft.McFolderSelected,
                ModLoader.LoaderFolderRunType.ForceRun, 1, @"versions\");
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "实例 " + PageInstanceLeft.Instance.Name + " 收藏状态更改失败", ModBase.LogLevel.Msgbox);
        }
    }

    #endregion

    #region 卡片：快捷方式

    // 实例文件夹
    private void BtnFolderVersion_Click(object sender, MouseButtonEventArgs mouseButtonEventArgs)
    {
        OpenVersionFolder(PageInstanceLeft.Instance);
    }

    public static void OpenVersionFolder(ModMinecraft.McInstance Version)
    {
        ModBase.OpenExplorer(Version.PathInstance);
    }

    // 存档文件夹
    private void BtnFolderSaves_Click(object sender, MouseButtonEventArgs mouseButtonEventArgs)
    {
        var FolderPath = PageInstanceLeft.Instance.PathIndie + @"saves\";
        Directory.CreateDirectory(FolderPath);
        ModBase.OpenExplorer(FolderPath);
    }

    // Mod 文件夹
    private void BtnFolderMods_Click(object sender, MouseButtonEventArgs mouseButtonEventArgs)
    {
        var FolderPath = PageInstanceLeft.Instance.PathIndie + @"mods\";
        Directory.CreateDirectory(FolderPath);
        ModBase.OpenExplorer(FolderPath);
    }

    #endregion

    #region 卡片：管理

    // 导出启动脚本
    private void BtnManageScript_Click(object sender, MouseButtonEventArgs mouseButtonEventArgs)
    {
        try
        {
            // 弹窗要求指定脚本的保存位置
            var SavePath = SystemDialogs.SelectSaveFile(Lang.Text("Instance.Overall.Script.SelectSaveTitle"), "启动 " + PageInstanceLeft.Instance.Name + ".bat",
                Lang.Text("Instance.Overall.Script.FileFilter"));
            if (string.IsNullOrEmpty(SavePath))
                return;
            // 检查中断（等玩家选完弹窗指不定任务就结束了呢……）
            if (ModLaunch.McLaunchLoader.State == ModBase.LoadState.Loading)
            {
                ModMain.Hint(Lang.Text("Instance.Overall.Script.WaitForLaunchTask"), ModMain.HintType.Critical);
                return;
            }

            // 生成脚本
            if (ModLaunch.McLaunchStart(new ModLaunch.McLaunchOptions
                    { SaveBatch = SavePath, Instance = PageInstanceLeft.Instance }))
            {
                if (ModProfile.SelectedProfile.Type == ModLaunch.McLoginType.Legacy)
                    ModMain.Hint(Lang.Text("Instance.Overall.Script.Exporting"));
                else
                    ModMain.Hint(Lang.Text("Instance.Overall.Script.ExportingWarning"));
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "导出启动脚本失败（" + PageInstanceLeft.Instance.Name + "）", ModBase.LogLevel.Msgbox);
        }
    }

    // 补全文件
    private void BtnManageCheck_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            // 忽略文件检查提示
            if ((bool)ModMinecraft.ShouldIgnoreFileCheck(PageInstanceLeft.Instance))
            {
                ModMain.Hint(Lang.Text("Instance.Overall.Repair.DisableVerificationHint"));
                return;
            }

            // 重复任务检查
            var taskName = PageInstanceLeft.Instance.Name + " " + Lang.Text("Instance.Overall.Repair.TaskName");
            foreach (var OngoingLoader in ModLoader.LoaderTaskbar)
            {
                if ((OngoingLoader.Name ?? "") != (taskName ?? ""))
                    continue;
                ModMain.Hint(Lang.Text("Instance.Overall.Repair.Processing"), ModMain.HintType.Critical);
                return;
            }

            // 启动
            var Loader = new ModLoader.LoaderCombo<string>(taskName,
                ModDownload.DlClientFix(PageInstanceLeft.Instance, true,
                    ModDownload.AssetsIndexExistsBehaviour.AlwaysDownload));
            Loader.OnStateChanged = _ =>
            {
                switch (Loader.State)
                {
                    case ModBase.LoadState.Finished:
                    {
                        ModMain.Hint(taskName + Lang.Text("Instance.Overall.Repair.Success"), ModMain.HintType.Finish);
                        break;
                    }
                    case ModBase.LoadState.Failed:
                    {
                        ModMain.Hint(taskName + Lang.Text("Instance.Overall.Repair.Failed") + Loader.Error.Message, ModMain.HintType.Critical);
                        break;
                    }
                    case ModBase.LoadState.Aborted:
                    {
                        ModMain.Hint(taskName + Lang.Text("Common.Action.Cancel") + "！");
                        break;
                    }
                }
            };
            Loader.Start(PageInstanceLeft.Instance.Name);
            ModLoader.LoaderTaskbarAdd(Loader);
            ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
            ModMain.FrmMain.BtnExtraDownload.Ribble();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "尝试补全文件失败（" + PageInstanceLeft.Instance.Name + "）", ModBase.LogLevel.Msgbox);
        }
    }

    // 重置
    private void BtnManageRestore_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            var CurrentVersion = PageInstanceLeft.Instance.Info;
            if (!(CurrentVersion.Drop == 99) &&
                ModMinecraft.CompareVersion(CurrentVersion.VanillaName, "1.5.2") == -1 && CurrentVersion.HasForge)
            {
                ModMain.Hint(Lang.Text("Instance.Overall.Reset.NotSupported"));
                return;
            }

            // 确认操作
            if (ModMain.MyMsgBox(
                    Lang.Text("Instance.Overall.Reset.ConfirmMessage", PageInstanceLeft.Instance.Name), Lang.Text("Instance.Overall.Reset.ConfirmTitle"), Lang.Text("Common.Action.Confirm"), Lang.Text("Common.Action.Cancel")) == 2)
                return;

            // 备份实例核心文件
            ModBase.CopyFile(PageInstanceLeft.Instance.PathInstance + PageInstanceLeft.Instance.Name + ".json",
                PageInstanceLeft.Instance.PathInstance + @"PCLInstallBackups\" + PageInstanceLeft.Instance.Name +
                ".json");
            ModBase.CopyFile(PageInstanceLeft.Instance.PathInstance + PageInstanceLeft.Instance.Name + ".jar",
                PageInstanceLeft.Instance.PathInstance + @"PCLInstallBackups\" + PageInstanceLeft.Instance.Name +
                ".jar");
            // 提交安装申请
            var Request = new ModDownloadLib.McInstallRequest
            {
                TargetInstanceName = PageInstanceLeft.Instance.Name,
                TargetInstanceFolder = $@"{ModMinecraft.McFolderSelected}versions\{PageInstanceLeft.Instance.Name}\",
                MinecraftName = CurrentVersion.VanillaName,
                OptiFineEntry = CurrentVersion.HasOptiFine
                    ? new ModDownload.DlOptiFineListEntry
                    {
                        Inherit = CurrentVersion.VanillaName,
                        DisplayName = CurrentVersion.VanillaName + " " + CurrentVersion.OptiFine
                    }
                    : null,
                ForgeEntry = CurrentVersion.HasForge
                    ? new ModDownload.DlForgeVersionEntry(CurrentVersion.Forge, null, CurrentVersion.VanillaName)
                        { Category = "installer" }
                    : null,
                ForgeVersion = CurrentVersion.HasForge ? CurrentVersion.Forge : null,
                NeoForgeVersion = CurrentVersion.HasNeoForge ? CurrentVersion.NeoForge : null,
                CleanroomVersion = CurrentVersion.HasCleanroom ? CurrentVersion.Cleanroom : null,
                FabricVersion = CurrentVersion.HasFabric ? CurrentVersion.Fabric : null,
                QuiltVersion = CurrentVersion.HasQuilt ? CurrentVersion.Quilt : null,
                LiteLoaderEntry = CurrentVersion.HasLiteLoader
                    ? new ModDownload.DlLiteLoaderListEntry { Inherit = CurrentVersion.VanillaName }
                    : null,
                LegacyFabricVersion = CurrentVersion.HasLegacyFabric ? CurrentVersion.LegacyFabric : null
            };
            // .MinecraftJson = CurrentVersion.McName,
            if (!ModDownloadLib.McInstall(Request, Lang.Text("Common.Action.Reset")))
                return;
            ModMain.FrmMain.PageChange(new FormMain.PageStackData { Page = FormMain.PageType.Launch });
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "重置实例 " + PageInstanceLeft.Instance.Name + " 失败", ModBase.LogLevel.Msgbox);
        }
    }

    // 测试游戏
    private void BtnManageTest_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            ModLaunch.McLaunchStart(new ModLaunch.McLaunchOptions
                { Instance = PageInstanceLeft.Instance, IsTest = true });
            ModMain.FrmMain.PageChange(FormMain.PageType.Launch);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "测试游戏失败", ModBase.LogLevel.Feedback);
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
                PageInstanceLeft.Instance.State != ModMinecraft.McInstanceState.Error &&
                !string.Equals(
                    PageInstanceLeft.Instance.PathIndie,
                    ModMinecraft.McFolderSelected,
                    StringComparison.OrdinalIgnoreCase
                );

            var confirmMessageKey = (isIsolatedInstance, isShiftPressed) switch
            {
                (true, true) => "Instance.Overall.Delete.ConfirmMessageIsolatedPermanent",
                (true, false) => "Instance.Overall.Delete.ConfirmMessageIsolated",
                (false, true) => "Instance.Overall.Delete.ConfirmMessagePermanent",
                (false, false) => "Instance.Overall.Delete.ConfirmMessage"
            };

            var confirmResult = ModMain.MyMsgBox(
                Lang.Text(confirmMessageKey, PageInstanceLeft.Instance.Name),
                Lang.Text("Instance.Overall.Delete.ConfirmTitle"),
                Button2: Lang.Text("Common.Action.Cancel"),
                IsWarn: isIsolatedInstance || isShiftPressed
            );

            switch (confirmResult)
            {
                case 1:
                {
                    var instancePath = PageInstanceLeft.Instance.PathInstance;
                    var instanceName = PageInstanceLeft.Instance.Name;
                    ModBase.IniClearCache(Path.Combine(PageInstanceLeft.Instance.PathIndie, "options.txt"));
                    ((DynamicCacheConfigStorage)ConfigService.GetProvider(ConfigSource.GameInstance)).InvalidateCache(
                        instancePath);
                    if (isShiftPressed)
                    {
                        ModBase.DeleteDirectory(instancePath);
                        ModMain.Hint(Lang.Text("Instance.Overall.Delete.PermanentSuccess", instanceName),
                            ModMain.HintType.Finish);
                    }
                    else
                    {
                        FileSystem.DeleteDirectory(instancePath, UIOption.OnlyErrorDialogs,
                            RecycleOption.SendToRecycleBin);
                        ModMain.Hint(Lang.Text("Instance.Overall.Delete.RecycleBinSuccess", instanceName),
                            ModMain.HintType.Finish);
                    }

                    break;
                }
                case 2:
                {
                    return;
                }
            }

            ModLoader.LoaderFolderRun(ModMinecraft.McInstanceListLoader, ModMinecraft.McFolderSelected,
                ModLoader.LoaderFolderRunType.ForceRun, 1, @"versions\");
            ModMain.FrmMain.PageBack();
        }
        catch (OperationCanceledException ex)
        {
            ModBase.Log(ex, "删除实例 " + PageInstanceLeft.Instance.Name + " 被主动取消");
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "删除实例 " + PageInstanceLeft.Instance.Name + " 失败", ModBase.LogLevel.Msgbox);
        }
    }

    // 修补核心
    private void BtnManagePatch_Click(object sender, MouseButtonEventArgs e)
    {
        switch (ModMain.MyMsgBox(
                    Lang.Text("Instance.Overall.Patch.ConfirmMessage", PageInstanceLeft.Instance.Name),
                    Lang.Text("Instance.Overall.Patch.ConfirmTitle"), Button2: Lang.Text("Common.Action.Cancel")))
        {
            case 1:
            {
                var UserInput = SystemDialogs.SelectFile(Lang.Text("Instance.Overall.Patch.SelectFile.Filter"), Lang.Text("Instance.Overall.Patch.SelectFile.Title"));
                if (UserInput is null || string.IsNullOrWhiteSpace(UserInput))
                    return;
                ModMain.Hint(Lang.Text("Instance.Overall.Patch.Patching"));
                ModBase.RunInNewThread(() =>
                {
                    var Core = new GameCore(PageInstanceLeft.Instance.PathInstance + PageInstanceLeft.Instance.Name +
                                            ".jar");
                    Core.AddToCore(UserInput);
                    ModMain.Hint(Lang.Text("Instance.Overall.Patch.Success"), ModMain.HintType.Finish);
                    Config.Instance.DisableAssetVerifyV2[PageInstanceLeft.Instance] = true;
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
