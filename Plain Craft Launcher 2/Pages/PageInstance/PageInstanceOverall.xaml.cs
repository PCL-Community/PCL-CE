using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FluentValidation;
using Microsoft.VisualBasic.FileIO;
using Newtonsoft.Json.Linq;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.Minecraft;
using PCL.Core.UI;
using PCL.Core.Utils.Validate;
using FileSystem = Microsoft.VisualBasic.FileIO.FileSystem;

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
        BtnDisplayStar.Text = instance.IsStar ? "从收藏夹中移除" : "加入收藏夹";
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
            PanInfo.Children.Add(new MyLoading { Text = "正在获取信息", Margin = new Thickness(0d, 0d, 0d, 10d) });
        });
        var loaders = new List<ModLoader.LoaderBase>();
        loaders.Add(new ModLoader.LoaderTask<int, int>("获取可能的整合包信息", _ =>
        {
            var modpackId = States.Instance.ModpackId[PageInstanceLeft.Instance.PathInstance];
            if (!string.IsNullOrWhiteSpace(modpackId))
            {
                var compProjects = ModComp.CompRequest.GetCompProjectsByIds(new List<string> { modpackId });
                if (!(compProjects.Count == 0))
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
        loaders.Add(new ModLoader.LoaderTask<int, int>("获取实例信息", _ => ModBase.RunInUi(() =>
        {
            var instance = PageInstanceLeft.Instance;
            var instanceInfo = instance.Info;
            List<MyListItem> items = [];
            var launchCount = States.Instance.LaunchCount[instance.PathInstance];
            if (launchCount == 0)
                items.Add(new MyListItem
                {
                    Title = "启动次数", Info = "从未启动", Logo = "pack://application:,,,/images/Blocks/RedstoneLampOff.png"
                });
            else
                items.Add(new MyListItem
                {
                    Title = "启动次数",
                    Info = "已启动 " + States.Instance.LaunchCount[instance.PathInstance] + " 次",
                    Logo = "pack://application:,,,/images/Blocks/RedstoneLampOn.png"
                });
            if (!string.IsNullOrWhiteSpace(States.Instance.ModpackVersion[instance.PathInstance]))
                items.Add(new MyListItem
                {
                    Title = "整合包版本", Info = States.Instance.ModpackVersion[instance.PathInstance],
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
                    { Title = "LiteLoader", Info = "已安装", Logo = "pack://application:,,,/images/Blocks/Egg.png" });
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
                            "确认要从实例列表中隐藏该实例吗？隐藏该实例后，它将不再出现于 PCL 显示的实例列表中。" + "\r\n" +
                            "此后，在实例列表页面按下 F11 才可以查看被隐藏的实例。", "隐藏实例提示", Button2: "取消") != 1)
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
            var NewInfo = ModMain.MyMsgBoxInput("更改描述", "修改实例的描述文本，留空则使用 PCL 的默认描述。", OldInfo,
                [], "默认描述");
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
            var NewName = ModMain.MyMsgBoxInput("重命名实例", "", OldName,
                [new FolderNameValidator(ModMinecraft.McFolderSelected + "versions", ignoreCase: false)]);
            if (string.IsNullOrWhiteSpace(NewName))
                return;
            var NewPath = ModMinecraft.McFolderSelected + @"versions\" + NewName + @"\";
            // 获取临时中间名，以防止仅修改大小写的重命名失败
            var TempName = NewName + "_temp";
            var TempPath = ModMinecraft.McFolderSelected + @"versions\" + TempName + @"\";
            var IsCaseChangedOnly = (NewName.ToLower() ?? "") == (OldName.ToLower() ?? "");
            // 重新加载实例 Json 信息，避免 HMCL 项被合并
            JObject JsonObject;
            try
            {
                JsonObject = (JObject)ModBase.GetJson(ModBase.ReadFile(PageInstanceLeft.Instance.PathInstance +
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
            ModBase.IniClearCache(PageInstanceLeft.Instance.PathIndie + "options.txt");
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
            ModMain.Hint("重命名成功！", ModMain.HintType.Finish);
            PageInstanceLeft.Instance = new ModMinecraft.McInstance(NewName).Load();
            if (!(ModMinecraft.McInstanceSelected == null) &&
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
                var FileName = SystemDialogs.SelectFile("常用图片文件(*.png;*.jpg;*.gif)|*.png;*.jpg;*.gif", "选择图片");
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
            var SavePath = SystemDialogs.SelectSaveFile("选择脚本保存位置", "启动 " + PageInstanceLeft.Instance.Name + ".bat",
                "批处理文件(*.bat)|*.bat");
            if (string.IsNullOrEmpty(SavePath))
                return;
            // 检查中断（等玩家选完弹窗指不定任务就结束了呢……）
            if (ModLaunch.McLaunchLoader.State == ModBase.LoadState.Loading)
            {
                ModMain.Hint("请在当前启动任务结束后再试！", ModMain.HintType.Critical);
                return;
            }

            // 生成脚本
            if (ModLaunch.McLaunchStart(new ModLaunch.McLaunchOptions
                    { SaveBatch = SavePath, Instance = PageInstanceLeft.Instance }))
            {
                if (ModProfile.SelectedProfile.Type == ModLaunch.McLoginType.Legacy)
                    ModMain.Hint("正在导出启动脚本……");
                else
                    ModMain.Hint("正在导出启动脚本……（注意，使用脚本启动可能会导致登录失效！）");
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
                ModMain.Hint("请先关闭 [实例设置 → 设置 → 高级启动选项 → 关闭文件校验]，然后再尝试补全文件！");
                return;
            }

            // 重复任务检查
            foreach (var OngoingLoader in ModLoader.LoaderTaskbar)
            {
                if ((OngoingLoader.Name ?? "") != (PageInstanceLeft.Instance.Name + " 文件补全" ?? ""))
                    continue;
                ModMain.Hint("正在处理中，请稍候！", ModMain.HintType.Critical);
                return;
            }

            // 启动
            var Loader = new ModLoader.LoaderCombo<string>(PageInstanceLeft.Instance.Name + " 文件补全",
                ModDownload.DlClientFix(PageInstanceLeft.Instance, true,
                    ModDownload.AssetsIndexExistsBehaviour.AlwaysDownload));
            Loader.OnStateChanged = _ =>
            {
                switch (Loader.State)
                {
                    case ModBase.LoadState.Finished:
                    {
                        ModMain.Hint(Loader.Name + "成功！", ModMain.HintType.Finish);
                        break;
                    }
                    case ModBase.LoadState.Failed:
                    {
                        ModMain.Hint(Loader.Name + "失败：" + Loader.Error.Message, ModMain.HintType.Critical);
                        break;
                    }
                    case ModBase.LoadState.Aborted:
                    {
                        ModMain.Hint(Loader.Name + "已取消！");
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
                ModMain.Hint("该实例暂不支持重置！");
                return;
            }

            // 确认操作
            if (ModMain.MyMsgBox(
                    "你确定要重置实例 " + PageInstanceLeft.Instance.Name + " 吗？" + "\r\n" +
                    "PCL 将会尝试重新从互联网获取此实例的资源文件信息，并重新执行自动安装。", "实例重置确认", "确认", "取消") == 2)
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
            if (!ModDownloadLib.McInstall(Request, "重置"))
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
            var IsShiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
            var IsHintIndie = PageInstanceLeft.Instance.State != ModMinecraft.McInstanceState.Error &&
                              (PageInstanceLeft.Instance.PathIndie ?? "") != (ModMinecraft.McFolderSelected ?? "");
            switch (ModMain.MyMsgBox(
                        $"你确定要{(IsShiftPressed ? "永久" : "")}删除实例 {PageInstanceLeft.Instance.Name} 吗？" + (IsHintIndie
                            ? "\r\n" + "由于该实例开启了版本隔离，删除时该实例对应的存档、资源包、Mod 等文件也将被一并删除！"
                            : ""), "实例删除确认", Button2: "取消", IsWarn: IsHintIndie || IsShiftPressed))
            {
                case 1:
                {
                    var instancePath = PageInstanceLeft.Instance.PathInstance;
                    var instanceName = PageInstanceLeft.Instance.Name;
                    ModBase.IniClearCache(PageInstanceLeft.Instance.PathIndie + "options.txt");
                    ((DynamicCacheConfigStorage)ConfigService.GetProvider(ConfigSource.GameInstance)).InvalidateCache(
                        instancePath);
                    if (IsShiftPressed)
                    {
                        ModBase.DeleteDirectory(instancePath);
                        ModMain.Hint("实例 " + instanceName + " 已永久删除！", ModMain.HintType.Finish);
                    }
                    else
                    {
                        FileSystem.DeleteDirectory(instancePath, UIOption.OnlyErrorDialogs,
                            RecycleOption.SendToRecycleBin);
                        ModMain.Hint("实例 " + instanceName + " 已删除到回收站！", ModMain.HintType.Finish);
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
                    $"你确定要对 {PageInstanceLeft.Instance.Name} 的核心文件进行修补吗？ {"\r\n"}修补游戏核心可能导致游戏崩溃等问题。{"\r\n"}在修补核心后，文件校验会自动关闭。",
                    "修补提示", Button2: "取消"))
        {
            case 1:
            {
                var UserInput = SystemDialogs.SelectFile("压缩文件(*.jar;*.zip)|*.jar;*.zip", "选择用于修补核心的文件");
                if (UserInput is null | string.IsNullOrWhiteSpace(UserInput))
                    return;
                ModMain.Hint("正在修补游戏核心，这可能需要一段时间");
                ModBase.RunInNewThread(() =>
                {
                    var Core = new GameCore(PageInstanceLeft.Instance.PathInstance + PageInstanceLeft.Instance.Name +
                                            ".jar");
                    Core.AddToCore(UserInput);
                    ModMain.Hint("修补游戏核心成功", ModMain.HintType.Finish);
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
