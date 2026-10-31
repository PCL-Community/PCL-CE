using Microsoft.VisualBasic.FileIO;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft;
using PCL.Core.UI;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.Core.Utils.Validate;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FileSystem = Microsoft.VisualBasic.FileIO.FileSystem;

namespace PCL;

public partial class PageInstanceOverall : IPageView
{
    private readonly PageInstanceOverallViewModel _vm = new();

    private Loading.LoaderCombo<int> instanceInfoLoader;

    private bool isLoad;
    private bool _refreshed;

    public MyListItem itemVersion;
    private MyCompItem modpackCompItem;

    public PageInstanceOverall()
    {
        InitializeComponent();
        DataContext = _vm;
        DialogServiceInstaller.Install(_vm);
        Loaded += PageSetupLaunch_Loaded;
        Unloaded += (_, _) => _refreshed = false;
        // Handles
        ComboDisplayType.SelectionChanged += ComboDisplayType_SelectionChanged;
        ComboDisplayLogo.SelectionChanged += ComboDisplayLogo_SelectionChanged;
    }

    public PageInstanceOverallViewModel ViewModel => _vm;

    public void OnNavigatedTo(object? parameter)
    {
        RefreshOnNavigate();
    }

    public void OnNavigatedFrom()
    {
    }

    protected override void RefreshLocalization()
    {
        // 重建信息卡片前记录滚动位置，避免切语言时跳回顶部
        var savedOffset = PanBack.VerticalOffset;
        Reload();
        Dispatcher.BeginInvoke(new Action(() => PanBack.ScrollToVerticalOffset(savedOffset)));
    }

    private void PageSetupLaunch_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshOnNavigate();
    }

    private void RefreshOnNavigate()
    {
        if (_refreshed)
            return;
        _refreshed = true;

        // 重复加载部分
        PanBack.ScrollToHome();

        // 更新设置
        ItemDisplayLogoCustom.Tag = @"PCL\Logo.png";
        Reload();

        // 非重复加载部分
        if (isLoad)
            return;
        isLoad = true;
        PanDisplay.TriggerForceResize();
    }

    /// <summary>
    ///     确保当前页面上的信息已正确显示。
    /// </summary>
    private void Reload()
    {
        Animations.AniBeginControl();

        var instance = PageInstanceLeft.McInstance;
        // 刷新设置项目
        ComboDisplayType.SelectedIndex = States.Instance.CardType[instance.PathInstance];
        _vm.RefreshDisplay(instance);
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
        modpackCompItem = null;
        Common.RunInUi(() =>
        {
            _vm.InfoLoadingVisible = true;
            _vm.ModpackVisible = false;
            PanModpack.Content = null;
            _vm.InfoRows.Clear();
        });
        var loaders = new List<Loading.LoaderBase>();
        loaders.Add(new Loading.LoaderTask<int, int>(Lang.Text("Instance.Overall.Info.LoadModpackInfoTask"), _ =>
        {
            var modpackId = States.Instance.ModpackId[PageInstanceLeft.McInstance.PathInstance];
            if (!string.IsNullOrWhiteSpace(modpackId))
            {
                var compProjects = Comp.CompRequest.GetCompProjectsByIds(new List<string> { modpackId });
                if (compProjects.Count > 0)
                    Common.RunInUi(() =>
                    {
                        modpackCompItem = compProjects.First().ToCompItem(false, false);
                        modpackCompItem.Tag = compProjects.First();
                    });
            }
        })
        {
            block = true
        });
        loaders.Add(new Loading.LoaderTask<int, int>(Lang.Text("Instance.Overall.Info.LoadInstanceInfoTask"), _ => Common.RunInUi(() =>
        {
            _vm.RefreshInfoRows(PageInstanceLeft.McInstance);
            PanModpack.Content = modpackCompItem;
            _vm.ModpackVisible = modpackCompItem is not null;
            _vm.InfoLoadingVisible = false;
        })));
        instanceInfoLoader = new Loading.LoaderCombo<int>("Instance Info Loader", loaders) { show = false };
        instanceInfoLoader.Start();
    }

    #region 卡片：个性化

    // 实例分类
    private async void ComboDisplayType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _vm.CardTypeComboEnabled = false;
        try
        {
            if (!(isLoad && !Animations.AniExitsAnyControl()))
                return;
            if (ComboDisplayType.SelectedIndex != 1)
            {
                // 改为不隐藏
                try
                {
                    // 若设置分类为可安装 Mod，则显示正常的 Mod 管理页面
                    States.Instance.CardType[PageInstanceLeft.McInstance.PathInstance] = ComboDisplayType.SelectedIndex;
                    PageInstanceLeft.McInstance.displayType = (McInstanceCardType)States.Instance.CardType[PageInstanceLeft.McInstance.PathInstance];
                    Launcher.frmInstanceLeft.RefreshModDisabled();

                    Common.WriteIni(GameFolders.mcFolderSelected + "PCL.ini", "InstanceCache", ""); // 要求刷新缓存
                    Loading.LoaderFolderRun(InstanceList.mcInstanceListLoader, GameFolders.mcFolderSelected,
                        Loading.LoaderFolderRunType.ForceRun, 1, @"versions\");
                }
                catch (Exception ex)
                {
                    Common.Log(
                        ex,
                        $"修改实例分类失败（{PageInstanceLeft.McInstance.Name}）",
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
                        if (!(await _vm.Dialog.ShowAsync(new TextDialogRequest
                        {
                            Title = Lang.Text("Instance.Overall.Hide.ConfirmTitle"),
                            Content = Lang.Text("Instance.Overall.Hide.ConfirmMessage"),
                            Buttons = DialogButtons.OkCancel
                        }).ConfigureAwait(true)).IsConfirmed)
                        {
                            ComboDisplayType.SelectedIndex = 0;
                            return;
                        }

                        States.Hint.HideGameInstance = true;
                    }

                    States.Instance.CardType[PageInstanceLeft.McInstance.PathInstance] =
                        (int)McInstanceCardType.Hidden;
                    Common.WriteIni(GameFolders.mcFolderSelected + "PCL.ini", "InstanceCache", ""); // 要求刷新缓存
                    Loading.LoaderFolderRun(InstanceList.mcInstanceListLoader, GameFolders.mcFolderSelected,
                        Loading.LoaderFolderRunType.ForceRun, 1, @"versions\");
                }
                catch (Exception ex)
                {
                    Common.Log(
                        ex,
                        $"隐藏实例 {PageInstanceLeft.McInstance.Name} 失败",
                        Common.LogLevel.Feedback,
                        userSummary: Lang.Text("Instance.Overall.Error.OperationFailed"));
                }
            }
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "修改实例分类失败",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Instance.Overall.Error.OperationFailed"));
        }
        finally
        {
            _vm.CardTypeComboEnabled = true;
        }
    }

    // 更改描述
    private async void BtnDisplayDesc_Click(object sender, MouseButtonEventArgs e)
    {
        _vm.DescriptionButtonEnabled = false;
        try
        {
            var oldInfo = States.Instance.CustomInfo[PageInstanceLeft.McInstance.PathInstance];
            var newInfoDialog = await _vm.Dialog.ShowAsync(new InputDialogRequest
            {
                Title = Lang.Text("Instance.Overall.Description.EditTitle"),
                Content = Lang.Text("Instance.Overall.Description.EditMessage"),
                DefaultValue = oldInfo ?? "",
                HintText = Lang.Text("Instance.Overall.Description.Default"),
                HighLight = false
            }).ConfigureAwait(true);
            var newInfo = newInfoDialog.IsConfirmed ? newInfoDialog.InputText : null;
            if (newInfo is not null && (oldInfo ?? "") != (newInfo ?? ""))
                States.Instance.CustomInfo[PageInstanceLeft.McInstance.PathInstance] = newInfo;
            PageInstanceLeft.McInstance = new McInstance(PageInstanceLeft.McInstance.Name).Load();
            Reload();
            Loading.LoaderFolderRun(InstanceList.mcInstanceListLoader, GameFolders.mcFolderSelected,
                Loading.LoaderFolderRunType.ForceRun, 1, @"versions\");
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                $"实例 {PageInstanceLeft.McInstance.Name} 描述更改失败",
                Common.LogLevel.Msgbox,
                userSummary: Lang.Text("Instance.Overall.Error.OperationFailed"));
        }
        finally
        {
            _vm.DescriptionButtonEnabled = true;
        }
    }

    // 重命名实例
    private async void BtnDisplayRename_Click(object sender, MouseButtonEventArgs e)
    {
        _vm.RenameButtonEnabled = false;
        try
        {
            // 确认输入的新名称
            var oldName = PageInstanceLeft.McInstance.Name;
            var oldPath = PageInstanceLeft.McInstance.PathInstance;
            // 修改此部分的同时修改快速安装的实例名检测*
            var newNameDialog = await _vm.Dialog.ShowAsync(new InputDialogRequest
            {
                Title = Lang.Text("Instance.Overall.Name.EditTitle"),
                Content = "",
                DefaultValue = oldName ?? "",
                ValidateRules = [new FolderNameValidator(GameFolders.mcFolderSelected + "versions", ignoreCase: false)],
                HighLight = false
            }).ConfigureAwait(true);
            var newName = newNameDialog.IsConfirmed ? newNameDialog.InputText : null;
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
                jsonObject = (JsonObject)Common.GetJson(Common.ReadFile(PageInstanceLeft.McInstance.PathInstance +
                                                                       PageInstanceLeft.McInstance.Name + ".json"));
            }
            catch (Exception ex)
            {
                Common.Log(ex, "重命名读取 Json 时失败");
                jsonObject = PageInstanceLeft.McInstance.JsonObject;
            }

            // 重命名主文件夹
            FileSystem.RenameDirectory(oldPath, tempName);
            FileSystem.RenameDirectory(tempPath, newName);
            // 清理 ini 缓存
            Common.IniClearCache(Path.Combine(PageInstanceLeft.McInstance.PathIndie, "options.txt"));
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
                    Common.ReadFile(Path.Combine(newPath, "PCL", "Setup.ini")).Replace(oldPath, newPath));
            // 更改已选中的实例
            if ((Common.ReadIni(GameFolders.mcFolderSelected + "PCL.ini", "Version") ?? "") == (oldName ?? ""))
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
            HintService.Show(Hints.Success(Lang.Text("Instance.Overall.Name.RenameSuccess")));
            PageInstanceLeft.McInstance = new McInstance(newName).Load();
            if (InstanceList.McMcInstanceSelected is not null &&
                InstanceList.McMcInstanceSelected.Equals(PageInstanceLeft.McInstance))
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
        finally
        {
            _vm.RenameButtonEnabled = true;
        }
    }

    // 实例图标
    private void ComboDisplayLogo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!(isLoad && !Animations.AniExitsAnyControl()))
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

                Common.CopyFile(fileName, PageInstanceLeft.McInstance.PathInstance + @"PCL\Logo.png");
            }
            else
            {
                File.Delete(PageInstanceLeft.McInstance.PathInstance + @"PCL\Logo.png");
            }
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                $"更改自定义实例图标失败（{PageInstanceLeft.McInstance.Name}）",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Instance.Overall.Error.OperationFailed"));
        }

        // 进行更改
        try
        {
            string newLogo = ((MyComboBoxItem)ComboDisplayLogo.SelectedItem).Tag?.ToString();
            States.Instance.LogoPath[PageInstanceLeft.McInstance.PathInstance] = newLogo;
            States.Instance.IsLogoCustom[PageInstanceLeft.McInstance.PathInstance] = !string.IsNullOrEmpty(newLogo);
            // 刷新显示
            Common.WriteIni(GameFolders.mcFolderSelected + "PCL.ini", "InstanceCache", ""); // 要求刷新缓存
            PageInstanceLeft.McInstance = new McInstance(PageInstanceLeft.McInstance.Name).Load();
            Reload();
            Loading.LoaderFolderRun(InstanceList.mcInstanceListLoader, GameFolders.mcFolderSelected,
                Loading.LoaderFolderRunType.ForceRun, 1, @"versions\");
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                $"更改实例图标失败（{PageInstanceLeft.McInstance.Name}）",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Instance.Overall.Error.OperationFailed"));
        }
    }

    // 收藏夹
    private void BtnDisplayStar_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            States.Instance.Starred[PageInstanceLeft.McInstance.PathInstance] = !PageInstanceLeft.McInstance.IsStar;
            PageInstanceLeft.McInstance = new McInstance(PageInstanceLeft.McInstance.Name).Load();
            Reload();
            InstanceList.mcInstanceListForceRefresh = true;
            Loading.LoaderFolderRun(InstanceList.mcInstanceListLoader, GameFolders.mcFolderSelected,
                Loading.LoaderFolderRunType.ForceRun, 1, @"versions\");
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                $"实例 {PageInstanceLeft.McInstance.Name} 收藏状态更改失败",
                Common.LogLevel.Msgbox,
                userSummary: Lang.Text("Instance.Overall.Error.OperationFailed"));
        }
    }

    #endregion

    #region 卡片：快捷方式

    public static void OpenVersionFolder(McInstance version)
    {
        PageInstanceOverallViewModel.OpenVersionFolder(version);
    }

    #endregion

    #region 卡片：管理

    // 重置
    private async void BtnManageRestore_Click(object sender, MouseButtonEventArgs e)
    {
        if (!RestrictedFeaturePolicy.Check("minecraft-reset")) return;
        _vm.RestoreButtonEnabled = false;
        try
        {
            var currentVersion = PageInstanceLeft.McInstance.Info;
            if (currentVersion.Drop != 99 &&
                McVersionComparer.CompareVersion(currentVersion.VanillaName, "1.5.2") == -1 && currentVersion.HasForge)
            {
                HintService.Show(Hints.Info(Lang.Text("Instance.Overall.Reset.NotSupported")));
                return;
            }

            if (currentVersion.HasQuilt)
            {
                HintService.Show(Hints.Info(Lang.Text("Instance.Overall.Reset.QuiltUnsupported")));
                return;
            }

            // 确认操作
            if ((await _vm.Dialog.ShowAsync(new TextDialogRequest
            {
                Title = Lang.Text("Instance.Overall.Reset.ConfirmTitle"),
                Content = Lang.Text("Instance.Overall.Reset.ConfirmMessage", PageInstanceLeft.McInstance.Name),
                Buttons = DialogButtons.OkCancel
            }).ConfigureAwait(true)).Button == 2)
                return;

            // 备份实例核心文件
            Common.CopyFile(PageInstanceLeft.McInstance.PathInstance + PageInstanceLeft.McInstance.Name + ".json",
                PageInstanceLeft.McInstance.PathInstance + @"PCLInstallBackups\" + PageInstanceLeft.McInstance.Name +
                ".json");
            Common.CopyFile(PageInstanceLeft.McInstance.PathInstance + PageInstanceLeft.McInstance.Name + ".jar",
                PageInstanceLeft.McInstance.PathInstance + @"PCLInstallBackups\" + PageInstanceLeft.McInstance.Name +
                ".jar");
            // 提交安装申请
            var request = new DownloadUi.McInstallRequest
            {
                targetInstanceName = PageInstanceLeft.McInstance.Name,
                targetInstanceFolder = $@"{GameFolders.mcFolderSelected}versions\{PageInstanceLeft.McInstance.Name}\",
                minecraftName = currentVersion.VanillaName,
                optiFineEntry = currentVersion.HasOptiFine
                    ? new GameDownload.DlOptiFineListEntry
                    {
                        Inherit = currentVersion.VanillaName,
                        DisplayName = currentVersion.VanillaName + " " + currentVersion.OptiFine
                    }
                    : null,
                forgeEntry = currentVersion.HasForge
                    ? new GameDownload.DlForgeVersionEntry(currentVersion.Forge, null, currentVersion.VanillaName)
                    { Category = "installer" }
                    : null,
                forgeVersion = currentVersion.HasForge ? currentVersion.Forge : null,
                neoForgeVersion = currentVersion.HasNeoForge ? currentVersion.NeoForge : null,
                cleanroomVersion = currentVersion.HasCleanroom ? currentVersion.Cleanroom : null,
                fabricVersion = currentVersion.HasFabric ? currentVersion.Fabric : null,
                liteLoaderEntry = currentVersion.HasLiteLoader
                    ? new GameDownload.DlLiteLoaderListEntry { Inherit = currentVersion.VanillaName }
                    : null,
                legacyFabricVersion = currentVersion.HasLegacyFabric ? currentVersion.LegacyFabric : null
            };
            // .MinecraftJson = CurrentVersion.McName,
            if (!DownloadUi.McInstall(request, Lang.Text("Common.Action.Reset")))
                return;
            Launcher.frmMain.PageChange(new FormMain.PageStackData { page = FormMain.PageType.Launch });
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                $"重置实例 {PageInstanceLeft.McInstance.Name} 失败",
                Common.LogLevel.Msgbox,
                userSummary: Lang.Text("Instance.Overall.Error.OperationFailed"));
        }
        finally
        {
            _vm.RestoreButtonEnabled = true;
        }
    }

    // 删除实例
    // 修改此代码时，同时修改 PageSelectRight 中的代码
    private async void BtnManageDelete_Click(object sender, MouseButtonEventArgs e)
    {
        _vm.DeleteButtonEnabled = false;
        try
        {
            var isShiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

            var isIsolatedInstance =
                PageInstanceLeft.McInstance.state != McInstanceState.Error &&
                !string.Equals(
                    PageInstanceLeft.McInstance.PathIndie,
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

            var confirmResult = (await _vm.Dialog.ShowAsync(new TextDialogRequest
            {
                Title = Lang.Text("Instance.Overall.Delete.ConfirmTitle"),
                Content = Lang.Text(confirmMessageKey, PageInstanceLeft.McInstance.Name),
                Buttons = DialogButtons.OkCancel,
                IsWarn = isIsolatedInstance || isShiftPressed
            }).ConfigureAwait(true)).Button;

            switch (confirmResult)
            {
                case 1:
                    {
                        var instancePath = PageInstanceLeft.McInstance.PathInstance;
                        var instanceName = PageInstanceLeft.McInstance.Name;
                        Common.IniClearCache(Path.Combine(PageInstanceLeft.McInstance.PathIndie, "options.txt"));
                        ((DynamicCacheConfigStorage)ConfigService.GetProvider(ConfigSource.GameInstance)).InvalidateCache(
                            instancePath);
                        if (isShiftPressed)
                        {
                            Common.DeleteDirectory(instancePath);
                            HintService.Show(Hints.Success(Lang.Text("Instance.Overall.Delete.PermanentSuccess", instanceName)));
                        }
                        else
                        {
                            FileSystem.DeleteDirectory(instancePath, UIOption.OnlyErrorDialogs,
                                RecycleOption.SendToRecycleBin);
                            HintService.Show(Hints.Success(Lang.Text("Instance.Overall.Delete.RecycleBinSuccess", instanceName)));
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
            Common.Log(ex, "删除实例 " + PageInstanceLeft.McInstance.Name + " 被主动取消");
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                $"删除实例 {PageInstanceLeft.McInstance.Name} 失败",
                Common.LogLevel.Msgbox,
                userSummary: Lang.Text("Instance.Overall.Error.OperationFailed"));
        }
        finally
        {
            _vm.DeleteButtonEnabled = true;
        }
    }

    // 修补核心
    private async void BtnManagePatch_Click(object sender, MouseButtonEventArgs e)
    {
        _vm.PatchButtonEnabled = false;
        try
        {
            switch ((await _vm.Dialog.ShowAsync(new TextDialogRequest
            {
                Title = Lang.Text("Instance.Overall.Patch.ConfirmTitle"),
                Content = Lang.Text("Instance.Overall.Patch.ConfirmMessage", PageInstanceLeft.McInstance.Name),
                Buttons = DialogButtons.OkCancel
            }).ConfigureAwait(true)).Button)
            {
                case 1:
                    {
                        var userInput = SystemDialogs.SelectFile(Lang.Text("Instance.Overall.Patch.SelectFile.Filter"), Lang.Text("Instance.Overall.Patch.SelectFile.Title"));
                        if (userInput is null || string.IsNullOrWhiteSpace(userInput))
                            return;
                        HintService.Show(Hints.Info(Lang.Text("Instance.Overall.Patch.Patching")));
                        Common.RunInNewThread(() =>
                        {
                            var core = new GameCore(PageInstanceLeft.McInstance.PathInstance + PageInstanceLeft.McInstance.Name +
                                                    ".jar");
                            core.AddToCore(userInput);
                            HintService.Show(Hints.Success(Lang.Text("Instance.Overall.Patch.Success")));
                            Config.Instance.DisableAssetVerifyV2[PageInstanceLeft.McInstance.PathInstance] = true;
                        });
                        break;
                    }
                case 2:
                    {
                        return;
                    }
            }
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "修补核心失败",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Instance.Overall.Error.OperationFailed"));
        }
        finally
        {
            _vm.PatchButtonEnabled = true;
        }
    }

    #endregion
}
