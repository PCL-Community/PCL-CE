using System.Collections;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI;

namespace PCL;

public partial class PageInstanceInstall
{
    private enum InstallAction
    {
        Modify,
        Reset
    }

    private bool IsLoad;
    private string LastVersionName;
    private InstallAction _installAction;

    public PageInstanceInstall()
    {
        Initialized += (a, b) => LoaderInit();
        Loaded += (a, b) => Init();
        InitializeComponent();
        LoadMinecraft.Text = Lang.Text("Download.Version.LoadingList");
    }

    private void LoaderInit()
    {
        DisabledPageAnimControls.Add(BtnSelectStart);
        // PageLoaderInit(LoadMinecraft, PanLoad, PanBack, Nothing, DlClientListLoader, AddressOf LoadMinecraft_OnFinish)
        PageLoaderInit(LoadMinecraft, PanLoad, PanAllBack, null, ModDownload.DlClientListLoader, _ => GetCurrentInfo());
    }

    private void Init()
    {
        PanBack.ScrollToHome();

        GetCurrentInfo();

        var NeedRefresh = LastVersionName is null || (LastVersionName ?? "") != (_vanillaName ?? "");
        LastVersionName = _vanillaName;

        ModDownload.DlOptiFineListLoader.Start(IsForceRestart: NeedRefresh);
        ModDownload.DlLiteLoaderListLoader.Start(IsForceRestart: NeedRefresh);
        ModDownload.DlFabricListLoader.Start(IsForceRestart: NeedRefresh);
        ModDownload.DlQuiltListLoader.Start(IsForceRestart: NeedRefresh);
        ModDownload.DlNeoForgeListLoader.Start(IsForceRestart: NeedRefresh);
        ModDownload.DlCleanroomListLoader.Start(IsForceRestart: NeedRefresh);
        ModDownload.DlLabyModListLoader.Start(IsForceRestart: NeedRefresh);
        ModDownload.DlLegacyFabricListLoader.Start(IsForceRestart: NeedRefresh);
        ModDownload.DlFabricApiLoader.Start(IsForceRestart: NeedRefresh);
        ModDownload.DlQSLLoader.Start(IsForceRestart: NeedRefresh);
        ModDownload.DlLegacyFabricApiLoader.Start(IsForceRestart: NeedRefresh);
        ModDownload.DlOptiFabricLoader.Start(IsForceRestart: NeedRefresh);

        // 重载预览
        ReloadSelected();

        // 非重复加载部分
        if (IsLoad)
            return;
        IsLoad = true;

        ModDownloadLib.McDownloadForgeRecommendedRefresh();

        LoadOptiFine.State = ModDownload.DlOptiFineListLoader;
        LoadLiteLoader.State = ModDownload.DlLiteLoaderListLoader;
        LoadFabric.State = ModDownload.DlFabricListLoader;
        LoadFabricApi.State = ModDownload.DlFabricApiLoader;
        LoadQuilt.State = ModDownload.DlQuiltListLoader;
        LoadQSL.State = ModDownload.DlQSLLoader;
        LoadNeoForge.State = ModDownload.DlNeoForgeListLoader;
        LoadCleanroom.State = ModDownload.DlCleanroomListLoader;
        LoadOptiFabric.State = ModDownload.DlOptiFabricLoader;
        LoadLabyMod.State = ModDownload.DlLabyModListLoader;
        LoadLegacyFabric.State = ModDownload.DlLegacyFabricListLoader;
        LoadLegacyFabricApi.State = ModDownload.DlLegacyFabricApiLoader;
    }

    #region 安装

    private void BtnSelectStart_Click(object sender, MouseButtonEventArgs mouseButtonEventArgs)
    {
        // 确认版本隔离
        if (SelectedLoaderName is not null &&
            (Config.Launch.IndieSolutionV2 == 0 ||
             Config.Launch.IndieSolutionV2 == 2))
            if (ModMain.MyMsgBox(
                    Lang.Text("Download.Install.InstanceIsolation.Warning.Message"), Lang.Text("Download.Install.InstanceIsolation.Warning.Title"), Lang.Text("Download.Install.InstanceIsolation.Warning.Cancel"), Lang.Text("Download.Install.InstanceIsolation.Warning.Continue")) == 1)
                return;

        if (_installAction == InstallAction.Reset)
            if (ModMain.MyMsgBox(
                    Lang.Text("Instance.Install.Reset.Message"),
                    Lang.Text("Instance.Install.Reset.Title"),
                    Lang.Text("Common.Action.Continue"),
                    Lang.Text("Common.Action.Cancel")
                ) == 2)
                return;

        // 删除 LabyMod Neo 文件
        if ((PageInstanceLeft.Instance.PathIndie ?? "") != (PageInstanceLeft.Instance.PathInstance ?? "") &&
            PageInstanceLeft.Instance.Info.HasLabyMod)
            Directory.Delete(System.IO.Path.Combine(PageInstanceLeft.Instance.PathIndie, "labymod-neo"), true);
        // 备份实例核心文件
        ModBase.CopyFile(PageInstanceLeft.Instance.PathInstance + PageInstanceLeft.Instance.Name + ".json",
            PageInstanceLeft.Instance.PathInstance + @"PCLInstallBackups\" + PageInstanceLeft.Instance.Name + ".json");
        if (File.Exists(PageInstanceLeft.Instance.PathInstance + PageInstanceLeft.Instance.Name + ".jar"))
            ModBase.CopyFile(PageInstanceLeft.Instance.PathInstance + PageInstanceLeft.Instance.Name + ".jar",
                PageInstanceLeft.Instance.PathInstance + @"PCLInstallBackups\" + PageInstanceLeft.Instance.Name +
                ".jar");
        // 确认独立 API (如 Fabric API 等) 是否需要被修改
        if (SelectedFabricApi?.Equals(_currentFabricApi) == true)
            SelectedFabricApi = null;
        if (SelectedLegacyFabricApi?.Equals(_currentLegacyFabricApi) == true)
            SelectedLegacyFabricApi = null;
        if (SelectedQSL?.Equals(_currentQsl) == true)
            SelectedQSL = null;
        if (SelectedOptiFabric?.Equals(_currentOptiFabric) == true)
            SelectedOptiFabric = null;
        // 提交安装申请
        var Request = new ModDownloadLib.McInstallRequest
        {
            TargetInstanceName = PageInstanceLeft.Instance.Name,
            TargetInstanceFolder = $@"{ModMinecraft.McFolderSelected}versions\{PageInstanceLeft.Instance.Name}\",
            MinecraftJson = _vanillaData?["url"].ToString(),
            MinecraftName = _vanillaName,
            OptiFineEntry = SelectedOptiFine,
            ForgeEntry = SelectedForge,
            NeoForgeEntry = SelectedNeoForge,
            NeoForgeVersion = SelectedNeoForgeVersion,
            CleanroomEntry = SelectedCleanroom,
            CleanroomVersion = SelectedCleanroomVersion,
            FabricVersion = SelectedFabric,
            FabricApi = SelectedFabricApi,
            QuiltVersion = SelectedQuilt,
            QSL = SelectedQSL,
            OptiFabric = SelectedOptiFabric,
            LiteLoaderEntry = SelectedLiteLoader,
            LabyModChannel = SelectedLabyModChannel,
            LabyModCommitRef = SelectedLabyModCommitRef,
            LegacyFabricVersion = SelectedLegacyFabric,
            LegacyFabricApi = SelectedLegacyFabricApi
        };
        BtnSelectStart.IsEnabled = false;
        if (!ModDownloadLib.McInstall(Request, _installAction == InstallAction.Modify ? Lang.Text("Instance.Install.Action.ModifyLabel") : Lang.Text("Common.Action.Reset")))
            return;
        // 删除旧的独立 API 文件
        if (SelectedFabricApi is not null && _currentFabricApiPath is not null)
            File.Delete(_currentFabricApiPath);
        if (SelectedLegacyFabricApi is not null && _currentLegacyFabricApiPath is not null)
            File.Delete(_currentLegacyFabricApiPath);
        if (SelectedQSL is not null && _currentQslPath is not null)
            File.Delete(_currentQslPath);
        if (SelectedOptiFabric is not null && _currentOptiFabricPath is not null)
            File.Delete(_currentOptiFabricPath);
        // 返回主页
        ModMain.FrmMain.PageChange(new FormMain.PageStackData { Page = FormMain.PageType.Launch });
    }

    #endregion

    private string GetLoaderError(MyLoading loader)
    {
        if (loader is null || !loader.State.IsLoader)
            return Lang.Text("Download.Install.State.Getting");
        switch (loader.State.LoadingState)
        {
            case MyLoading.MyLoadingState.Run:
            {
                return Lang.Text("Download.Install.State.Getting");
            }
            case MyLoading.MyLoadingState.Error:
            {
                var message = ((ModLoader.LoaderBase)loader.State).Error.Message;
                return message == Lang.Text("Download.Install.State.NoVersion") ? Lang.Text("Download.Install.State.NoVersion") : Lang.Text("Download.Install.State.GetFailed", message);
            }
            case MyLoading.MyLoadingState.Unloaded:
            {
                return Lang.Text("Download.Install.State.UnknownUnloaded");
            }

            default:
            {
                return null;
            }
        }
    }

    #region 页面切换

    // 页面切换动画
    public bool IsInSelectPage;
    private bool IsFirstLoaded;

    private void EnterSelectPage()
    {
        if (IsInSelectPage)
            return;
        IsInSelectPage = true;

        DisabledPageAnimControls.Remove(BtnSelectStart);
        BtnSelectStart.Show = true;
        AutoSelectedFabricApi = false;
        AutoSelectedQSL = false;
        AutoSelectedOptiFabric = false;
        PanSelect.Visibility = Visibility.Visible;
        PanSelect.IsHitTestVisible = true;
        PanMinecraft.IsHitTestVisible = false;
        PanBack.IsHitTestVisible = false;
        PanBack.ScrollToHome();

        CardMinecraft.IsSwapped = true;
        CardOptiFine.IsSwapped = true;
        CardLiteLoader.IsSwapped = true;
        CardForge.IsSwapped = true;
        CardNeoForge.IsSwapped = true;
        CardCleanroom.IsSwapped = true;
        CardFabric.IsSwapped = true;
        CardFabricApi.IsSwapped = true;
        CardQuilt.IsSwapped = true;
        CardQSL.IsSwapped = true;
        CardOptiFabric.IsSwapped = true;
        CardLabyMod.IsSwapped = true;
        CardLegacyFabric.IsSwapped = true;
        CardLegacyFabricApi.IsSwapped = true;

        if (!(bool)States.Hint.InstallPageBack)
        {
            States.Hint.InstallPageBack = true;
            ModMain.Hint(Lang.Text("Download.Install.Hint.MinecraftBack"));
        }

        // 如果在选择页面按了刷新键，选择页的东西可能会由于动画被隐藏，但不会由于加载结束而再次显示，因此这里需要手动恢复
        foreach (var Card in GetAllAnimControls(PanSelect))
        {
            Card.Opacity = 1d;
            Card.RenderTransform = new TranslateTransform();
        }

        // 启动 Forge 加载
        if (ModMinecraft.McInstanceInfo.IsFormatFit(_vanillaName))
        {
            var ForgeLoader =
                new ModLoader.LoaderTask<string, List<ModDownload.DlForgeVersionEntry>>(
                    "DlForgeVersion " + _vanillaName, ModDownload.DlForgeVersionMain);
            LoadForge.State = ForgeLoader;
            ForgeLoader.Start(_vanillaName);
        }

        // 启动 Fabric API、QSL、Legacy Fabric API、OptiFabric、LabyMod 加载
        ModDownload.DlFabricApiLoader.Start();
        ModDownload.DlQSLLoader.Start();
        ModDownload.DlLegacyFabricApiLoader.Start();
        ModDownload.DlOptiFabricLoader.Start();

        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaOpacity(PanMinecraft, -PanMinecraft.Opacity, 100, 10),
            ModAnimation.AaCode(() =>
            {
                PanBack.ScrollToHome();
                OptiFine_Loaded();
                LiteLoader_Loaded();
                Forge_Loaded();
                NeoForge_Loaded();
                Cleanroom_Loaded();
                Fabric_Loaded();
                LegacyFabric_Loaded();
                FabricApi_Loaded();
                LegacyFabricApi_Loaded();
                Quilt_Loaded();
                QSL_Loaded();
                LabyMod_Loaded();
                OptiFabric_Loaded();
                ReloadSelected();
            }, After: true),
            ModAnimation.AaOpacity(PanSelect, 1d - PanSelect.Opacity, 250, 150),
            ModAnimation.AaCode(() =>
            {
                PanMinecraft.Visibility = Visibility.Collapsed;
                PanBack.IsHitTestVisible = true;
                // 初始化 Binding
                if (IsFirstLoaded)
                    return;
                IsFirstLoaded = true;
                BtnOptiFineClearInner.SetBinding(Shape.FillProperty,
                    new Binding("Foreground") { Source = CardOptiFine.MainTextBlock, Mode = BindingMode.OneWay });
                BtnLiteLoaderClearInner.SetBinding(Shape.FillProperty,
                    new Binding("Foreground") { Source = CardLiteLoader.MainTextBlock, Mode = BindingMode.OneWay });
                BtnForgeClearInner.SetBinding(Shape.FillProperty,
                    new Binding("Foreground") { Source = CardForge.MainTextBlock, Mode = BindingMode.OneWay });
                BtnLegacyFabricClearInner.SetBinding(Shape.FillProperty,
                    new Binding("Foreground") { Source = CardLegacyFabric.MainTextBlock, Mode = BindingMode.OneWay });
                BtnNeoForgeClearInner.SetBinding(Shape.FillProperty,
                    new Binding("Foreground") { Source = CardNeoForge.MainTextBlock, Mode = BindingMode.OneWay });
                BtnCleanroomClearInner.SetBinding(Shape.FillProperty,
                    new Binding("Foreground") { Source = CardCleanroom.MainTextBlock, Mode = BindingMode.OneWay });
                BtnFabricClearInner.SetBinding(Shape.FillProperty,
                    new Binding("Foreground") { Source = CardFabric.MainTextBlock, Mode = BindingMode.OneWay });
                BtnLegacyFabricApiClearInner.SetBinding(Shape.FillProperty,
                    new Binding("Foreground")
                        { Source = CardLegacyFabricApi.MainTextBlock, Mode = BindingMode.OneWay });
                BtnFabricApiClearInner.SetBinding(Shape.FillProperty,
                    new Binding("Foreground") { Source = CardFabricApi.MainTextBlock, Mode = BindingMode.OneWay });
                BtnQuiltClearInner.SetBinding(Shape.FillProperty,
                    new Binding("Foreground") { Source = CardQuilt.MainTextBlock, Mode = BindingMode.OneWay });
                BtnQSLClearInner.SetBinding(Shape.FillProperty,
                    new Binding("Foreground") { Source = CardQSL.MainTextBlock, Mode = BindingMode.OneWay });
                BtnLabyModClearInner.SetBinding(Shape.FillProperty,
                    new Binding("Foreground") { Source = CardLabyMod.MainTextBlock, Mode = BindingMode.OneWay });
                BtnOptiFabricClearInner.SetBinding(Shape.FillProperty,
                    new Binding("Foreground") { Source = CardOptiFabric.MainTextBlock, Mode = BindingMode.OneWay });
            }, After: true)
        }, "FrmInstanceInstall SelectPageSwitch", true);
    }

    public void ExitSelectPage()
    {
        if (!IsInSelectPage)
            return;
        IsInSelectPage = false;

        LoadMinecraft_OnFinish();

        DisabledPageAnimControls.Add(BtnSelectStart);
        BtnSelectStart.Show = false;

        ClearSelected(); // 清除已选择项
        PanMinecraft.Visibility = Visibility.Visible;
        PanSelect.IsHitTestVisible = false;
        PanMinecraft.IsHitTestVisible = true;
        PanBack.IsHitTestVisible = false;
        PanBack.ScrollToHome();

        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaOpacity(PanSelect, -PanSelect.Opacity, 90, 10),
            ModAnimation.AaCode(() => PanBack.ScrollToHome(), After: true),
            ModAnimation.AaOpacity(PanMinecraft, 1d - PanMinecraft.Opacity, 150, 100),
            ModAnimation.AaCode(() =>
            {
                PanSelect.Visibility = Visibility.Collapsed;
                PanBack.IsHitTestVisible = true;
            }, After: true)
        }, "FrmInstanceInstall SelectPageSwitch");
    }

    // 页面切换触发
    public void MinecraftSelected(MyListItem sender, MouseButtonEventArgs e)
    {
        _vanillaName = sender.Title;
        _vanillaData = (JsonObject)sender.Tag;
        _vanillaIcon = sender.Logo;
        EnterSelectPage();
    }

    private void CardMinecraft_PreviewSwap(object sender, ModBase.RouteEventArgs e)
    {
        ExitSelectPage();
        e.Handled = true;
    }

    #endregion

    #region 选择

    // Minecraft
    private string? _vanillaName;
    private JsonObject? _vanillaData;
    private string? _vanillaIcon;
    private int VanillaDrop => ModMinecraft.McInstanceInfo.VersionToDrop(_vanillaName, true);

    // OptiFine
    private ModDownload.DlOptiFineListEntry? SelectedOptiFine;

    /// <summary>
    ///     选定的 Mod Loader 名称，内容应为 Forge / NeoForge / Fabric / Quilt / Cleanroom / LabyMod
    /// </summary>
    private string? SelectedLoaderName;

    /// <summary>
    ///     选定的 Mod Loader API 名称，内容应为 Fabric API 或 QFAPI / QSL
    /// </summary>
    private string? SelectedAPIName;

    // LiteLoader
    private ModDownload.DlLiteLoaderListEntry? SelectedLiteLoader;

    // Forge
    private ModDownload.DlForgeVersionEntry? SelectedForge;

    // Cleanroom
    private ModDownload.DlCleanroomListEntry? SelectedCleanroom;
    private string? SelectedCleanroomVersion;

    // NeoForge
    private ModDownload.DlNeoForgeListEntry? SelectedNeoForge;
    private string? SelectedNeoForgeVersion;

    // Fabric
    private string? SelectedFabric;

    // FabricApi
    private ModComp.CompFile? SelectedFabricApi;

    // LegacyFabric
    private string? SelectedLegacyFabric;

    // Legacy FabricApi
    private ModComp.CompFile? SelectedLegacyFabricApi;

    // Quilt
    private string? SelectedQuilt;

    // QSL
    private ModComp.CompFile? SelectedQSL;

    // LabyMod
    private string? SelectedLabyModChannel;
    private string? SelectedLabyModCommitRef;
    private string? SelectedLabyModVersion;

    // OptiFabric
    private ModComp.CompFile? SelectedOptiFabric;

    private bool _ReloadSelected_Ongoing; // #3742 中，LoadOptiFineGetError 会初始化 LoadOptiFine，触发事件 LoadOptiFine.StateChanged，导致再次调用 SelectReload

    /// <summary>
    ///     重载已选择的项目的显示。
    /// </summary>
    private void ReloadSelected()
    {
        if (_vanillaName is null || _ReloadSelected_Ongoing)
            return;
        _ReloadSelected_Ongoing = true;
        var selectedInfo = GetSelectInfo();
        // 主预览
        ItemSelect.Title = PageInstanceLeft.Instance.Name;
        ItemSelect.Logo = GetSelectLogo();
        BtnSelectStart.IsEnabled = true;
        if ((selectedInfo ?? "") == (CurrentInfo ?? ""))
        {
            ItemSelect.Info = selectedInfo;
            BtnSelectStart.Text = Lang.Text("Instance.Install.Action.StartReset");
            _installAction = InstallAction.Reset;
            BtnSelectStart.Logo = Icon.IconButtonReset;
        }
        else
        {
            ItemSelect.Info = CurrentInfo + " → " + selectedInfo;
            BtnSelectStart.Text = Lang.Text("Instance.Install.Action.StartModify");
            _installAction = InstallAction.Modify;
            BtnSelectStart.Logo = Icon.IconButtonEdit;
        }

        // Minecraft
        ImgMinecraft.Source = new MyBitmap(_vanillaIcon);
        LabMinecraft.Text = _vanillaName;
        LabMinecraft.Foreground = ThemeManager.ColorGray1;
        // OptiFine
        var OptiFineError = LoadOptiFineGetError();
        CardOptiFine.MainSwap.Visibility = OptiFineError is null ? Visibility.Visible : Visibility.Collapsed;
        if (OptiFineError is not null)
            CardOptiFine.IsSwapped = true; // 例如在同时展开卡片时选择了不兼容项则强制折叠
        SetPanelVisibility(PanOptiFineInfo, CardOptiFine.IsSwapped);
        if (SelectedOptiFine is null)
        {
            BtnOptiFineClear.Visibility = Visibility.Collapsed;
            ImgOptiFine.Visibility = Visibility.Collapsed;
            LabOptiFine.Text = OptiFineError ?? Lang.Text("Download.Install.State.CanAdd");
            LabOptiFine.Foreground = ThemeManager.ColorGray4;
        }
        else
        {
            BtnOptiFineClear.Visibility = Visibility.Visible;
            ImgOptiFine.Visibility = Visibility.Visible;
            LabOptiFine.Text = SelectedOptiFine.DisplayName.Replace(_vanillaName + " ", "");
            LabOptiFine.Foreground = ThemeManager.ColorGray1;
        }

        // LiteLoader
        if (VanillaDrop >= 130)
        {
            CardLiteLoader.Visibility = Visibility.Collapsed;
        }
        else
        {
            CardLiteLoader.Visibility = Visibility.Visible;
            var LiteLoaderError = LoadLiteLoaderGetError();
            CardLiteLoader.MainSwap.Visibility = LiteLoaderError is null ? Visibility.Visible : Visibility.Collapsed;
            if (LiteLoaderError is not null)
                CardLiteLoader.IsSwapped = true; // 例如在同时展开卡片时选择了不兼容项则强制折叠
            SetPanelVisibility(PanLiteLoaderInfo, CardLiteLoader.IsSwapped);
            if (SelectedLiteLoader is null)
            {
                BtnLiteLoaderClear.Visibility = Visibility.Collapsed;
                ImgLiteLoader.Visibility = Visibility.Collapsed;
                LabLiteLoader.Text = LiteLoaderError ?? Lang.Text("Download.Install.State.CanAdd");
                LabLiteLoader.Foreground = ThemeManager.ColorGray4;
            }
            else
            {
                BtnLiteLoaderClear.Visibility = Visibility.Visible;
                ImgLiteLoader.Visibility = Visibility.Visible;
                LabLiteLoader.Text = SelectedLiteLoader.Inherit;
                LabLiteLoader.Foreground = ThemeManager.ColorGray1;
            }
        }

        // Forge
        if (!ModMinecraft.McInstanceInfo.IsFormatFit(_vanillaName))
        {
            CardForge.Visibility = Visibility.Collapsed;
        }
        else
        {
            CardForge.Visibility = Visibility.Visible;
            var forgeError = LoadForgeGetError();
            CardForge.MainSwap.Visibility = forgeError is null ? Visibility.Visible : Visibility.Collapsed;
            if (forgeError is not null)
                CardForge.IsSwapped = true;
            SetPanelVisibility(PanForgeInfo, CardForge.IsSwapped);
            if (SelectedForge is null)
            {
                BtnForgeClear.Visibility = Visibility.Collapsed;
                ImgForge.Visibility = Visibility.Collapsed;
                LabForge.Text = forgeError ?? Lang.Text("Download.Install.State.CanAdd");
                LabForge.Foreground = ThemeManager.ColorGray4;
            }
            else
            {
                BtnForgeClear.Visibility = Visibility.Visible;
                ImgForge.Visibility = Visibility.Visible;
                LabForge.Text = SelectedForge.VersionName;
                LabForge.Foreground = ThemeManager.ColorGray1;
            }
        }

        // Cleanroom
        if (_vanillaName == "1.12.2")
        {
            CardCleanroom.Visibility = Visibility.Visible;
            var cleanroomError = LoadCleanroomGetError();
            CardCleanroom.MainSwap.Visibility = cleanroomError is null ? Visibility.Visible : Visibility.Collapsed;
            if (cleanroomError is not null)
                CardCleanroom.IsSwapped = true;
            SetPanelVisibility(PanCleanroomInfo, CardCleanroom.IsSwapped);
            if (SelectedCleanroom is null)
            {
                BtnCleanroomClear.Visibility = Visibility.Collapsed;
                ImgCleanroom.Visibility = Visibility.Collapsed;
                LabCleanroom.Text = cleanroomError ?? Lang.Text("Download.Install.State.CanAdd");
                LabCleanroom.Foreground = ThemeManager.ColorGray4;
            }
            else
            {
                BtnCleanroomClear.Visibility = Visibility.Visible;
                ImgCleanroom.Visibility = Visibility.Visible;
                LabCleanroom.Text = SelectedCleanroom.VersionName;
                LabCleanroom.Foreground = ThemeManager.ColorGray1;
            }
        }
        else
        {
            CardCleanroom.Visibility = Visibility.Collapsed;
        }

        // NeoForge
        if (VanillaDrop is > 0 and < 200) // 匹配 1.20.1+ 与一些愚人节版本
        {
            CardNeoForge.Visibility = Visibility.Collapsed;
        }
        else
        {
            CardNeoForge.Visibility = Visibility.Visible;
            var neoForgeError = LoadNeoForgeGetError();
            CardNeoForge.MainSwap.Visibility = neoForgeError is null ? Visibility.Visible : Visibility.Collapsed;
            if (neoForgeError is not null)
                CardNeoForge.IsSwapped = true;
            SetPanelVisibility(PanNeoForgeInfo, CardNeoForge.IsSwapped);
            if (SelectedNeoForge is null)
            {
                BtnNeoForgeClear.Visibility = Visibility.Collapsed;
                ImgNeoForge.Visibility = Visibility.Collapsed;
                LabNeoForge.Text = neoForgeError ?? Lang.Text("Download.Install.State.CanAdd");
                LabNeoForge.Foreground = ThemeManager.ColorGray4;
            }
            else
            {
                BtnNeoForgeClear.Visibility = Visibility.Visible;
                ImgNeoForge.Visibility = Visibility.Visible;
                LabNeoForge.Text = SelectedNeoForge.VersionName;
                LabNeoForge.Foreground = ThemeManager.ColorGray1;
            }
        }

        // Fabric
        if (VanillaDrop < 0 || VanillaDrop <= 130)
        {
            CardFabric.Visibility = Visibility.Collapsed;
        }
        else
        {
            CardFabric.Visibility = Visibility.Visible;
            var fabricError = LoadFabricGetError();
            CardFabric.MainSwap.Visibility = fabricError is null ? Visibility.Visible : Visibility.Collapsed;
            if (fabricError is not null)
                CardFabric.IsSwapped = true;
            SetPanelVisibility(PanFabricInfo, CardFabric.IsSwapped);
            if (SelectedFabric is null)
            {
                BtnFabricClear.Visibility = Visibility.Collapsed;
                ImgFabric.Visibility = Visibility.Collapsed;
                LabFabric.Text = fabricError ?? Lang.Text("Download.Install.State.CanAdd");
                LabFabric.Foreground = ThemeManager.ColorGray4;
            }
            else
            {
                BtnFabricClear.Visibility = Visibility.Visible;
                ImgFabric.Visibility = Visibility.Visible;
                LabFabric.Text = SelectedFabric.Replace("+build", "");
                LabFabric.Foreground = ThemeManager.ColorGray1;
            }
        }

        // FabricApi
        if (SelectedFabric is null && SelectedQuilt is null)
        {
            CardFabricApi.Visibility = Visibility.Collapsed;
        }
        else
        {
            CardFabricApi.Visibility = Visibility.Visible;
            var fabricApiError = LoadFabricApiGetError();
            CardFabricApi.MainSwap.Visibility = fabricApiError is null ? Visibility.Visible : Visibility.Collapsed;
            if (fabricApiError is not null || (SelectedFabric is null && SelectedQuilt is null))
                CardFabricApi.IsSwapped = true;
            SetPanelVisibility(PanFabricApiInfo, CardFabricApi.IsSwapped);
            if (SelectedFabricApi is null)
            {
                BtnFabricApiClear.Visibility = Visibility.Collapsed;
                ImgFabricApi.Visibility = Visibility.Collapsed;
                LabFabricApi.Text = fabricApiError ?? Lang.Text("Download.Install.State.CanAdd");
                LabFabricApi.Foreground = ThemeManager.ColorGray4;
            }
            else
            {
                BtnFabricApiClear.Visibility = Visibility.Visible;
                ImgFabricApi.Visibility = Visibility.Visible;
                LabFabricApi.Text = SelectedFabricApi.DisplayName.Split("]")[1].Replace("Fabric API ", "")
                    .Replace(" build ", ".").Split("+").First().Trim();
                LabFabricApi.Foreground = ThemeManager.ColorGray1;
            }
        }

        // LegacyFabric
        if (VanillaDrop > 130)
        {
            CardLegacyFabric.Visibility = Visibility.Collapsed;
        }
        else
        {
            CardLegacyFabric.Visibility = Visibility.Visible;
            var legacyFabricError = LoadLegacyFabricGetError();
            CardLegacyFabric.MainSwap.Visibility =
                legacyFabricError is null ? Visibility.Visible : Visibility.Collapsed;
            if (legacyFabricError is not null)
                CardLegacyFabric.IsSwapped = true;
            SetPanelVisibility(PanLegacyFabricInfo, CardLegacyFabric.IsSwapped);
            if (SelectedLegacyFabric is null)
            {
                BtnLegacyFabricClear.Visibility = Visibility.Collapsed;
                ImgLegacyFabric.Visibility = Visibility.Collapsed;
                LabLegacyFabric.Text = legacyFabricError ?? Lang.Text("Download.Install.State.CanAdd");
                LabLegacyFabric.Foreground = ThemeManager.ColorGray4;
            }
            else
            {
                BtnLegacyFabricClear.Visibility = Visibility.Visible;
                ImgLegacyFabric.Visibility = Visibility.Visible;
                LabLegacyFabric.Text = SelectedLegacyFabric.Replace("+build", "");
                LabLegacyFabric.Foreground = ThemeManager.ColorGray1;
            }
        }

        // LegacyFabricApi
        if (SelectedLegacyFabric is null)
        {
            CardLegacyFabricApi.Visibility = Visibility.Collapsed;
        }
        else
        {
            CardLegacyFabricApi.Visibility = Visibility.Visible;
            var legacyFabricApiError = LoadLegacyFabricApiGetError();
            CardLegacyFabricApi.MainSwap.Visibility =
                legacyFabricApiError is null ? Visibility.Visible : Visibility.Collapsed;
            if (legacyFabricApiError is not null || (SelectedLegacyFabric is null && SelectedQuilt is null))
                CardLegacyFabricApi.IsSwapped = true;
            SetPanelVisibility(PanLegacyFabricApiInfo, CardLegacyFabricApi.IsSwapped);
            if (SelectedLegacyFabricApi is null)
            {
                BtnLegacyFabricApiClear.Visibility = Visibility.Collapsed;
                ImgLegacyFabricApi.Visibility = Visibility.Collapsed;
                LabLegacyFabricApi.Text = legacyFabricApiError ?? Lang.Text("Download.Install.State.CanAdd");
                LabLegacyFabricApi.Foreground = ThemeManager.ColorGray4;
            }
            else
            {
                BtnLegacyFabricApiClear.Visibility = Visibility.Visible;
                ImgLegacyFabricApi.Visibility = Visibility.Visible;
                LabLegacyFabricApi.Text = SelectedLegacyFabricApi.DisplayName.Replace("Legacy Fabric API ", "");
                LabLegacyFabricApi.Foreground = ThemeManager.ColorGray1;
            }
        }

        // Quilt
        if (VanillaDrop < 144)
        {
            CardQuilt.Visibility = Visibility.Collapsed;
        }
        else
        {
            CardQuilt.Visibility = Visibility.Visible;
            var quiltError = LoadQuiltGetError();
            CardQuilt.MainSwap.Visibility = quiltError is null ? Visibility.Visible : Visibility.Collapsed;
            if (quiltError is not null)
                CardQuilt.IsSwapped = true;
            SetPanelVisibility(PanQuiltInfo, CardQuilt.IsSwapped);
            if (SelectedQuilt is null)
            {
                BtnQuiltClear.Visibility = Visibility.Collapsed;
                ImgQuilt.Visibility = Visibility.Collapsed;
                LabQuilt.Text = quiltError ?? Lang.Text("Download.Install.State.CanAdd");
                LabQuilt.Foreground = ThemeManager.ColorGray4;
            }
            else
            {
                BtnQuiltClear.Visibility = Visibility.Visible;
                ImgQuilt.Visibility = Visibility.Visible;
                LabQuilt.Text = SelectedQuilt.Replace("+build", "");
                LabQuilt.Foreground = ThemeManager.ColorGray1;
            }
        }

        // QSL
        if (SelectedQuilt is null)
        {
            CardQSL.Visibility = Visibility.Collapsed;
        }
        else
        {
            CardQSL.Visibility = Visibility.Visible;
            var qslError = LoadQSLGetError();
            CardQSL.MainSwap.Visibility = qslError is null ? Visibility.Visible : Visibility.Collapsed;
            if (qslError is not null || SelectedQuilt is null)
                CardQSL.IsSwapped = true;
            SetPanelVisibility(PanQSLInfo, CardQSL.IsSwapped);
            if (SelectedQSL is null)
            {
                BtnQSLClear.Visibility = Visibility.Collapsed;
                ImgQSL.Visibility = Visibility.Collapsed;
                LabQSL.Text = qslError ?? Lang.Text("Download.Install.State.CanAdd");
                LabQSL.Foreground = ThemeManager.ColorGray4;
            }
            else
            {
                BtnQSLClear.Visibility = Visibility.Visible;
                ImgQSL.Visibility = Visibility.Visible;
                LabQSL.Text = SelectedQSL.DisplayName.Split("]")[1].Trim();
                LabQSL.Foreground = ThemeManager.ColorGray1;
            }
        }

        // LabyMod
        if (VanillaDrop < 80)
        {
            CardLabyMod.Visibility = Visibility.Collapsed;
        }
        else
        {
            CardLabyMod.Visibility = Visibility.Visible;
            var labyModError = LoadLabyModGetError();
            CardLabyMod.MainSwap.Visibility = labyModError is null ? Visibility.Visible : Visibility.Collapsed;
            if (labyModError is not null)
                CardLabyMod.IsSwapped = true;
            SetPanelVisibility(PanLabyModInfo, CardLabyMod.IsSwapped);
            if (SelectedLabyModVersion is null)
            {
                BtnLabyModClear.Visibility = Visibility.Collapsed;
                ImgLabyMod.Visibility = Visibility.Collapsed;
                LabLabyMod.Text = labyModError ?? Lang.Text("Download.Install.State.CanAdd");
                LabLabyMod.Foreground = ThemeManager.ColorGray4;
            }
            else
            {
                BtnLabyModClear.Visibility = Visibility.Visible;
                ImgLabyMod.Visibility = Visibility.Visible;
                LabLabyMod.Text = SelectedLabyModVersion;
                LabLabyMod.Foreground = ThemeManager.ColorGray1;
            }
        }

        // OptiFabric
        if (SelectedFabric is null || SelectedOptiFine is null)
        {
            CardOptiFabric.Visibility = Visibility.Collapsed;
        }
        else
        {
            CardOptiFabric.Visibility = Visibility.Visible;
            var optiFabricError = LoadOptiFabricGetError();
            CardOptiFabric.MainSwap.Visibility = optiFabricError is null ? Visibility.Visible : Visibility.Collapsed;
            if (optiFabricError is not null || SelectedFabric is null)
                CardOptiFabric.IsSwapped = true;
            SetPanelVisibility(PanOptiFabricInfo, CardOptiFabric.IsSwapped);
            if (SelectedOptiFabric is null)
            {
                BtnOptiFabricClear.Visibility = Visibility.Collapsed;
                ImgOptiFabric.Visibility = Visibility.Collapsed;
                LabOptiFabric.Text = optiFabricError ?? Lang.Text("Download.Install.State.CanAdd");
                LabOptiFabric.Foreground = ThemeManager.ColorGray4;
            }
            else
            {
                BtnOptiFabricClear.Visibility = Visibility.Visible;
                ImgOptiFabric.Visibility = Visibility.Visible;
                LabOptiFabric.Text = SelectedOptiFabric.DisplayName.ToLower().Replace("optifabric-", "")
                    .Replace(".jar", "").Trim().TrimStart('v');
                LabOptiFabric.Foreground = ThemeManager.ColorGray1;
            }
        }

        // 主警告
        if (SelectedFabric is not null && SelectedFabricApi is null)
            HintFabricAPI.Visibility = Visibility.Visible;
        else
            HintFabricAPI.Visibility = Visibility.Collapsed;
        if (SelectedLegacyFabric is not null && SelectedLegacyFabricApi is null)
            HintLegacyFabricAPI.Visibility = Visibility.Visible;
        else
            HintLegacyFabricAPI.Visibility = Visibility.Collapsed;
        if (SelectedQuilt is not null && SelectedQSL is null && SelectedFabricApi is null)
            HintQSL.Visibility = Visibility.Visible;
        else
            HintQSL.Visibility = Visibility.Collapsed;
        if (SelectedQuilt is not null && SelectedFabricApi is not null && ModDownload.DlQSLLoader.Output is not null)
            foreach (var Version in ModDownload.DlQSLLoader.Output)
            {
                if (IsSuitableQSL(Version.GameVersions, _vanillaName))
                {
                    HintQuiltFabricAPI.Visibility = Visibility.Visible;
                    break;
                }

                HintQuiltFabricAPI.Visibility = Visibility.Collapsed;
            }
        else
            HintQuiltFabricAPI.Visibility = Visibility.Collapsed;

        if ((SelectedFabric is not null || SelectedLegacyFabric is not null) && SelectedOptiFine is not null &&
            SelectedOptiFabric is null)
        {
            if (VanillaDrop >= 140 && VanillaDrop <= 150)
            {
                HintOptiFabric.Visibility = Visibility.Collapsed;
                HintLegacyOptiFabric.Visibility = Visibility.Collapsed;
                HintOptiFabricOld.Visibility = Visibility.Visible;
            }
            else if (SelectedLegacyFabric is not null)
            {
                HintOptiFabric.Visibility = Visibility.Collapsed;
                HintLegacyOptiFabric.Visibility = Visibility.Visible;
                HintOptiFabricOld.Visibility = Visibility.Collapsed;
            }
            else
            {
                HintOptiFabric.Visibility = Visibility.Visible;
                HintOptiFabricOld.Visibility = Visibility.Collapsed;
                HintLegacyOptiFabric.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            HintOptiFabric.Visibility = Visibility.Collapsed;
            HintOptiFabricOld.Visibility = Visibility.Collapsed;
            HintLegacyOptiFabric.Visibility = Visibility.Collapsed;
        }

        if (VanillaDrop >= 160 && SelectedOptiFine is not null &&
            (SelectedForge is not null || SelectedFabric is not null))
            HintModOptiFine.Visibility = Visibility.Visible;
        else
            HintModOptiFine.Visibility = Visibility.Collapsed;
        // 结束
        _ReloadSelected_Ongoing = false;
    }

    /// <summary>
    ///     清空已选择的项目。
    /// </summary>
    private void ClearSelected()
    {
        _vanillaName = null;
        _vanillaData = null;
        _vanillaIcon = null;
        SelectedOptiFine = null;
        SelectedLiteLoader = null;
        SelectedLoaderName = null;
        SelectedAPIName = null;
        SelectedForge = null;
        SelectedNeoForge = null;
        SelectedNeoForgeVersion = null;
        SelectedCleanroom = null;
        SelectedCleanroomVersion = null;
        SelectedFabric = null;
        SelectedFabricApi = null;
        SelectedQuilt = null;
        SelectedQSL = null;
        SelectedOptiFabric = null;
        SelectedLabyModCommitRef = null;
        SelectedLabyModVersion = null;
        SelectedLabyModChannel = null;
        SelectedLegacyFabric = null;
        SelectedLegacyFabricApi = null;
    }

    // 信息栏动画
    private void SetPanelVisibility(Grid panel, bool visible)
    {
        if (Equals(panel.Tag, visible.ToString()))
            return;
        panel.Tag = visible.ToString();
        if (visible)
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaTranslateY(panel, -((TranslateTransform)panel.RenderTransform).Y, 150,
                        Ease: new ModAnimation.AniEaseOutFluent()),
                    ModAnimation.AaOpacity(panel, 1d - panel.Opacity, 60)
                }, "PageDownloadInstall Visibility " + panel.Name);
        else
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaTranslateY(panel, 6d - ((TranslateTransform)panel.RenderTransform).Y, 60),
                    ModAnimation.AaOpacity(panel, -panel.Opacity, 60)
                }, "PageDownloadInstall Visibility " + panel.Name);
    }

    /// <summary>
    ///     获取实例图标。
    /// </summary>
    private string GetSelectLogo()
    {
        if (SelectedFabric is not null) return "pack://application:,,,/images/Blocks/Fabric.png";

        if (SelectedLegacyFabric is not null) return "pack://application:,,,/images/Blocks/Fabric.png";

        if (SelectedForge is not null) return "pack://application:,,,/images/Blocks/Anvil.png";

        if (SelectedNeoForge is not null) return "pack://application:,,,/images/Blocks/NeoForge.png";

        if (SelectedLiteLoader is not null) return "pack://application:,,,/images/Blocks/Egg.png";

        if (SelectedOptiFine is not null) return "pack://application:,,,/images/Blocks/GrassPath.png";

        if (SelectedQuilt is not null) return "pack://application:,,,/images/Blocks/Quilt.png";

        if (SelectedCleanroom is not null) return "pack://application:,,,/images/Blocks/Cleanroom.png";

        if (SelectedLabyModVersion is not null) return "pack://application:,,,/images/Blocks/LabyMod.png";

        return _vanillaIcon;
    }

    /// <summary>
    ///     获取实例描述信息。
    /// </summary>
    private string GetSelectInfo()
    {
        var parts = new List<string>
        {
            _vanillaName
        };

        var loaderInfos = new (string NameKey, string? Version)[]
        {
            ("Common.Installation.Fabric", SelectedFabric?.Replace("+build", "")),
            ("Common.Installation.LegacyFabric", SelectedLegacyFabric),
            ("Common.Installation.Quilt", SelectedQuilt),
            ("Common.Installation.Forge", SelectedForge?.VersionName),
            ("Common.Installation.NeoForge",
                SelectedNeoForge?.VersionName ?? VersionOrNull(SelectedNeoForgeVersion)),
            ("Common.Installation.Cleanroom",
                SelectedCleanroom?.VersionName ?? VersionOrNull(SelectedCleanroomVersion)),
            ("Common.Installation.LabyMod", SelectedLabyModVersion),
            ("Common.Installation.OptiFine",
                SelectedOptiFine?.DisplayName.Replace(_vanillaName + " ", ""))
        };

        parts.AddRange(
            loaderInfos
                .Where(info => !string.IsNullOrWhiteSpace(info.Version))
                .Select(info => $"{Lang.Text(info.NameKey)} {info.Version}")
        );

        if (SelectedLiteLoader is not null) parts.Add(Lang.Text("Common.Installation.LiteLoader"));

        if (parts.Count == 1) parts.Add(Lang.Text("Instance.Install.NoExtraInstall"));

        return string.Join("  |  ", parts);
    }

    private static string? VersionOrNull<T>(T version)
    {
        return EqualityComparer<T>.Default.Equals(version, default!)
            ? null
            : version?.ToString();
    }

    #endregion

    #region 当前信息获取

    private ModComp.CompFile _currentFabricApi; // 加载完成后直接调用以提高性能
    private string _currentFabricApiPath;

    private object GetCurrentFabricApi() // 进入页面和联网加载时调用
    {
        var loaderOutput = ModDownload.DlFabricApiLoader.Output;
        if (loaderOutput is null)
            return null; // 确保联网信息已加载
        var localComp = ModLocalComp.GetModLocalCompByKeywords(PageInstanceLeft.Instance,
            new[] { "fabric-api", "fabric" }, "fabric", "api");
        if (localComp is null)
            return null;
        var result = loaderOutput.FirstOrDefault(comp => (comp.Hash ?? "") == (localComp.ModrinthHash ?? ""));
        if (result is not null)
        {
            _currentFabricApi = result;
            _currentFabricApiPath = localComp.Path;
        }

        return result;
    }

    private ModComp.CompFile _currentLegacyFabricApi; // 加载完成后直接调用以提高性能
    private string _currentLegacyFabricApiPath;

    private object GetCurrentLegacyFabricApi() // 进入页面和联网加载时调用
    {
        var loaderOutput = ModDownload.DlLegacyFabricApiLoader.Output;
        if (loaderOutput is null)
            return null; // 确保联网信息已加载
        var localComp = ModLocalComp.GetModLocalCompByKeywords(PageInstanceLeft.Instance,
            new[] { "legacy-fabric-api", "legacy-fabric" }, "legacy-fabric", "api");
        if (localComp is null)
            return null;
        var result = loaderOutput.FirstOrDefault(comp => (comp.Hash ?? "") == (localComp.ModrinthHash ?? ""));
        if (result is not null)
        {
            _currentLegacyFabricApi = result;
            _currentLegacyFabricApiPath = localComp.Path;
        }

        return result;
    }

    private ModComp.CompFile _currentQsl;
    private string _currentQslPath;

    private object GetCurrentQsl()
    {
        var loaderOutput = ModDownload.DlQSLLoader.Output;
        if (loaderOutput is null)
            return null;
        var localComp = ModLocalComp.GetModLocalCompByKeywords(PageInstanceLeft.Instance, "quilted_fabric_api", "qsl",
            "qf", "fabric", "api");
        // 兼容测试版的文件名 没错这玩意测试版命名方式甚至与正式版不一样
        if (localComp is null)
            localComp = ModLocalComp.GetModLocalCompByKeywords(PageInstanceLeft.Instance, "quilted_fabric_api",
                "quilted-fabric-api");
        if (localComp is null)
            return null;
        var result = loaderOutput.FirstOrDefault(comp => (comp.Hash ?? "") == (localComp.ModrinthHash ?? ""));
        if (result is not null)
        {
            _currentQsl = result;
            _currentQslPath = localComp.Path;
        }

        return result;
    }

    private ModComp.CompFile _currentOptiFabric;
    private string _currentOptiFabricPath;

    private object GetCurrentOptiFabric()
    {
        var loaderOutput = ModDownload.DlOptiFabricLoader.Output;
        if (loaderOutput is null)
            return null;
        var localComp =
            ModLocalComp.GetModLocalCompByKeywords(PageInstanceLeft.Instance, "optifabric", "optifabric", "opti");
        if (localComp is null)
            return null;
        var result = loaderOutput.FirstOrDefault(comp => (comp.Hash ?? "") == (localComp.ModrinthHash ?? ""));
        if (result is not null)
        {
            _currentOptiFabric = result;
            _currentOptiFabricPath = localComp.Path;
        }

        return result;
    }

    // 当前信息获取
    public void GetCurrentInfo()
    {
        ClearSelected();
        BtnSelectStart.IsEnabled = true;
        var CurrentInstance = PageInstanceLeft.Instance.Info;
        _vanillaName = CurrentInstance.VanillaName;
        if (CurrentInstance.HasLiteLoader)
            SelectedLiteLoader = new ModDownload.DlLiteLoaderListEntry { Inherit = CurrentInstance.VanillaName };
        if (CurrentInstance.HasOptiFine)
            SelectedOptiFine = new ModDownload.DlOptiFineListEntry
            {
                DisplayName = CurrentInstance.VanillaName + " " + CurrentInstance.OptiFine.Replace("_", " "),
                IsPreview = CurrentInstance.OptiFine.ContainsF("pre"), Inherit = CurrentInstance.VanillaName,
                NameVersion = CurrentInstance.VanillaName + "-OptiFine_HD_U_" + CurrentInstance.OptiFine
            };
        if (CurrentInstance.HasCleanroom)
        {
            SelectedAPIName = "Cleanroom";
            SelectedCleanroomVersion = CurrentInstance.Cleanroom;
        }
        else if (CurrentInstance.HasForge)
        {
            SelectedLoaderName = "Forge";
            SelectedForge =
                new ModDownload.DlForgeVersionEntry(CurrentInstance.Forge, null, CurrentInstance.VanillaName)
                {
                    Category = "installer", ForgeType = ModDownload.DlForgelikeEntry.ForgelikeType.Forge,
                    Inherit = CurrentInstance.VanillaName
                };
        }
        else if (CurrentInstance.HasLegacyFabric)
        {
            SelectedLoaderName = "LegacyFabric";
            SelectedLegacyFabric = CurrentInstance.LegacyFabric;
            SelectedLegacyFabricApi = (ModComp.CompFile)GetCurrentLegacyFabricApi();
        }
        else if (CurrentInstance.HasFabric)
        {
            SelectedLoaderName = "Fabric";
            SelectedFabric = CurrentInstance.Fabric;
            SelectedFabricApi = (ModComp.CompFile)GetCurrentFabricApi();
        }
        else if (CurrentInstance.HasLabyMod)
        {
            SelectedLoaderName = "LabyMod";
            SelectedLabyModVersion = CurrentInstance.LabyMod;
        }
        else if (CurrentInstance.HasNeoForge)
        {
            SelectedLoaderName = "NeoForge";
            SelectedNeoForgeVersion = CurrentInstance.NeoForge;
            SelectedNeoForge = new ModDownload.DlNeoForgeListEntry(CurrentInstance.NeoForge)
            {
                VersionName = CurrentInstance.NeoForge, Inherit = CurrentInstance.VanillaName,
                ForgeType = ModDownload.DlForgelikeEntry.ForgelikeType.NeoForge
            };
        }
        else if (CurrentInstance.HasQuilt)
        {
            SelectedLoaderName = "Quilt";
            SelectedQuilt = CurrentInstance.Quilt;
            SelectedQSL = (ModComp.CompFile)GetCurrentQsl();
            SelectedFabricApi = (ModComp.CompFile)GetCurrentFabricApi();
        }

        if ((CurrentInstance.HasFabric || CurrentInstance.HasQuilt) && CurrentInstance.HasOptiFine)
            SelectedOptiFabric = (ModComp.CompFile)GetCurrentOptiFabric();
        _vanillaIcon = "pack://application:,,,/images/Blocks/Grass.png"; // TODO: 需要判断 Icon
        CurrentInfo = GetSelectInfo();
        EnterSelectPage();
    }

    private string CurrentInfo;

    #endregion

    #region 加载器

    // 结果数据化
    private static string GetVersionTypeTitle(string key) => key switch
    {
        "正式版" => Lang.Text("Download.Version.Type.Release"),
        "预览版" => Lang.Text("Download.Version.Type.Development"),
        "远古版" => Lang.Text("Download.Version.Type.BeforeRelease"),
        "愚人节版" => Lang.Text("Download.Version.Type.AprilFools"),
        _ => key
    };

    private void LoadMinecraft_OnFinish()
    {
        ExitSelectPage(); // 返回
        do
        {
            try
            {
                var Dict = new Dictionary<string, List<JsonObject>>
                {
                    { "正式版", new List<JsonObject>() }, { "预览版", new List<JsonObject>() }, { "远古版", new List<JsonObject>() },
                    { "愚人节版", new List<JsonObject>() }
                };
                var Versions = (JsonArray)ModDownload.DlClientListLoader.Output.Value["versions"];
                foreach (JsonObject Version in Versions)
                {
                    // 确定分类
                    var Type = Version["type"].ToString();
                    var versionId = Version["id"].ToString().ToLower();
                    switch (Type ?? "")
                    {
                        case "release":
                        {
                            Type = "正式版";
                            break;
                        }
                        case "snapshot":
                        case "pending":
                        {
                            Type = "预览版";
                            // Mojang 误分类
                            if (versionId.StartsWith("1.") && !versionId.Contains("combat") &&
                                !versionId.Contains("rc") && !versionId.Contains("experimental") &&
                                !versionId.Equals("1.2") && !versionId.Contains("pre"))
                            {
                                Type = "正式版";
                                Version["type"] = "release";
                            }

                            // 愚人节版本
                            switch (Version["id"].ToString().ToLower() ?? "")
                            {
                                case "2point0_blue":
                                case "2point0_red":
                                case "2point0_purple":
                                case "2.0_blue":
                                case "2.0_red":
                                case "2.0_purple":
                                case "2.0":
                                {
                                    Type = "愚人节版";
                                    Version["id"] = Version["id"].ToString().Replace("point", ".");
                                    Version["type"] = "special";
                                    Version.Add("lore", McVersionClassifier.GetMcFoolName((string)Version["id"]));
                                    break;
                                }
                                case "20w14infinite":
                                case "20w14∞":
                                {
                                    Type = "愚人节版";
                                    Version["id"] = "20w14∞";
                                    Version["type"] = "special";
                                    Version.Add("lore", McVersionClassifier.GetMcFoolName((string)Version["id"]));
                                    break;
                                }
                                case "3d shareware v1.34":
                                case "1.rv-pre1":
                                case "15w14a":
                                case var @case when @case == "2.0":
                                case "22w13oneblockatatime":
                                case "23w13a_or_b":
                                case "24w14potato":
                                case "25w14craftmine":
                                case "26w14a":
                                {
                                    Type = "愚人节版";
                                    Version["type"] = "special";
                                    Version.Add("lore",
                                        McVersionClassifier.GetMcFoolName((string)Version["id"])); // 4/1 自动视作愚人节版
                                    break;
                                }

                                default:
                                {
                                    var ReleaseDate = Version["releaseTime"].GetValue<DateTime>().ToUniversalTime()
                                        .AddHours(2d);
                                    if (ReleaseDate.Month == 4 && ReleaseDate.Day == 1)
                                    {
                                        Type = "愚人节版";
                                        Version["type"] = "special";
                                    }

                                    break;
                                }
                            }

                            break;
                        }
                        case "special":
                        {
                            // 已被处理的愚人节版
                            Type = "愚人节版";
                            break;
                        }

                        default:
                        {
                            Type = "远古版";
                            break;
                        }
                    }

                    // 加入辞典
                    Dict[Type].Add(Version);
                }

                // 排序
                foreach (var Pair in Dict.ToList())
                    Dict[Pair.Key] = Pair.Value.OrderByDescending(j => j["releaseTime"].GetValue<DateTime>()).ToList();
                // 清空当前
                PanMinecraft.Children.Clear();
                // 添加最新版本
                var CardInfo = new MyCard { Title = Lang.Text("Download.Version.Latest.Title"), Margin = new Thickness(0d, 15d, 0d, 15d) };
                var TopestVersions = new List<JsonObject>();
                var Release = (JsonObject)Dict["正式版"][0].DeepClone();
                Release["lore"] = Lang.Text("Download.Version.Latest.Release", Lang.Date(Release["releaseTime"].GetValue<DateTime>(), "g"));
                TopestVersions.Add(Release);
                if (Dict["正式版"][0]["releaseTime"].GetValue<DateTime>() < Dict["预览版"][0]["releaseTime"].GetValue<DateTime>())
                {
                    var Snapshot = (JsonObject)Dict["预览版"][0].DeepClone();
                    Snapshot["lore"] = Lang.Text("Download.Version.Latest.Development", Lang.Date(Snapshot["releaseTime"].GetValue<DateTime>(), "g"));
                    TopestVersions.Add(Snapshot);
                }

                var PanInfo = new StackPanel
                {
                    Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d),
                    VerticalAlignment = VerticalAlignment.Top, RenderTransform = new TranslateTransform(0d, 0d),
                    Tag = TopestVersions
                };

                void StackInstall(StackPanel Stack)
                {
                    foreach (var item in (IEnumerable)Stack.Tag)
                        Stack.Children.Add(ModDownloadLib.McDownloadListItem((JsonObject)item,
                            (sender, e) => MinecraftSelected((MyListItem)sender, e), false));
                }

                ;
                MyCard.StackInstall(ref PanInfo, StackInstall);
                CardInfo.Children.Add(PanInfo);
                PanMinecraft.Children.Insert(0, CardInfo);
                // 添加其他版本
                foreach (var Pair in Dict)
                {
                    if (!Pair.Value.Any())
                        continue;
                    // 增加卡片
                    var NewCard = new MyCard
                        { Title = GetVersionTypeTitle(Pair.Key) + " (" + Pair.Value.Count + ")", Margin = new Thickness(0d, 0d, 0d, 15d) };
                    var NewStack = new StackPanel
                    {
                        Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d),
                        VerticalAlignment = VerticalAlignment.Top, RenderTransform = new TranslateTransform(0d, 0d),
                        Tag = Pair.Value
                    };
                    NewCard.Children.Add(NewStack);
                    NewCard.SwapControl = NewStack;
                    // 不能使用 AddressOf，这导致了 #535，原因完全不明，疑似是编译器 Bug
                    NewCard.InstallMethod = StackInstall;
                    NewCard.IsSwapped = true;
                    PanMinecraft.Children.Add(NewCard);
                }

                // 自动选择版本
                if (McVersionWaitingForSelect is null)
                    break;
                ModBase.Log("[Download] 自动选择 MC 版本：" + McVersionWaitingForSelect);
                foreach (JsonObject Version in Versions)
                {
                    if ((Version["id"].ToString() ?? "") != (McVersionWaitingForSelect ?? ""))
                        continue;
                    var Item = ModDownloadLib.McDownloadListItem(Version, (_, _) => { }, false);
                    MinecraftSelected(Item, null);
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "可视化安装版本列表出错", ModBase.LogLevel.Feedback);
            }
        } while (false);
    }

    /// <summary>
    ///     当 MC 版本列表加载完时，立即自动选择的版本。用于外部调用。
    /// </summary>
    public static string McVersionWaitingForSelect = null;

    #endregion

    #region OptiFine 列表

    /// <summary>
    ///     获取 OptiFine 的加载异常信息。若正常则返回 Nothing。
    /// </summary>
    private string LoadOptiFineGetError()
    {
        if (SelectedLoaderName == "NeoForge" || SelectedLoaderName == "Quilt" || SelectedLoaderName == "LabyMod")
            return Lang.Text("Download.Install.Compat.IncompatibleWithLoader", SelectedLoaderName);
        if (LoadOptiFine is null || LoadOptiFine.State.LoadingState == MyLoading.MyLoadingState.Run)
            return Lang.Text("Download.Install.State.Loading");
        if (LoadOptiFine.State.LoadingState == MyLoading.MyLoadingState.Error)
            return Lang.Text("Download.Install.State.GetVersionListFailed", ((ModLoader.LoaderBase)LoadOptiFine.State).Error.Message);
        // 是否有 Cleanroom
        if (SelectedCleanroom is not null)
            return Lang.Text("Download.Install.Compat.IncompatibleWithCleanroom");
        // 检查 Forge 1.13 - 1.14.3：全部不兼容
        if (SelectedLoaderName == "Forge" && ModMinecraft.CompareVersion(_vanillaName, "1.13") >= 0 &&
            ModMinecraft.CompareVersion("1.14.3", _vanillaName) >= 0) return Lang.Text("Download.Install.Compat.IncompatibleWithForge");
        // 检查 Fabric 1.20.5+: 全部不兼容
        if (SelectedFabric is not null && ModMinecraft.CompareVersion(_vanillaName, "1.20.4") > 0)
            return Lang.Text("Download.Install.Compat.IncompatibleWithFabric");
        // 检查 Loader
        if (GetLoaderError(LoadOptiFine) is not null)
            return GetLoaderError(LoadOptiFine);
        // 检查 Forge 版本
        var HasAny = false;
        var HasRequiredVersion = false;
        foreach (var OptiFineVersion in ModDownload.DlOptiFineListLoader.Output.Value)
        {
            if (!OptiFineVersion.DisplayName.StartsWith(_vanillaName + " "))
                continue; // 不是同一个大版本
            HasAny = true;
            if (SelectedForge is null)
                return null; // 未选择 Forge
            if ((bool)IsOptiFineSuitForForge(OptiFineVersion, SelectedForge))
                return null; // 该版本可用
            if (OptiFineVersion.RequiredForgeVersion is not null)
                HasRequiredVersion = true;
        }

        if (!HasAny) return Lang.Text("Download.Install.State.NoVersion");

        if (HasRequiredVersion) return Lang.Text("Download.Install.Compat.CompatForgeSpecificOnly");

        return Lang.Text("Download.Install.Compat.IncompatibleWithForge");
    }

    // 检查某个 OptiFine 是否与某个 Forge 兼容
    private object IsOptiFineSuitForForge(ModDownload.DlOptiFineListEntry OptiFine,
        ModDownload.DlForgeVersionEntry Forge)
    {
        if ((Forge.Inherit ?? "") != (OptiFine.Inherit ?? ""))
            return false; // 不是同一个大版本
        if (OptiFine.RequiredForgeVersion is null)
            return false; // 不兼容 Forge
        if (string.IsNullOrWhiteSpace(OptiFine.RequiredForgeVersion))
            return true; // #4183
        if (OptiFine.RequiredForgeVersion.Contains(".")) // XX.X.XXX
            return ModMinecraft.CompareVersion(Forge.Version.ToString(), OptiFine.RequiredForgeVersion) == 0;

        // XXXX
        return Forge.Version.Revision == Convert.ToDouble(OptiFine.RequiredForgeVersion);
    }

    // 限制展开
    private void CardOptiFine_PreviewSwap(object sender, ModBase.RouteEventArgs e)
    {
        if (LoadOptiFineGetError() is not null)
            e.Handled = true;
    }

    /// <summary>
    ///     尝试重新可视化 OptiFine 版本列表。
    /// </summary>
    private void OptiFine_Loaded()
    {
        try
        {
            if (ModDownload.DlOptiFineListLoader.State != ModBase.LoadState.Finished)
                return;

            // 获取版本列表
            var Versions = new List<ModDownload.DlOptiFineListEntry>();
            foreach (var Version in ModDownload.DlOptiFineListLoader.Output.Value)
            {
                if (SelectedForge is not null &&
                                          !(bool)IsOptiFineSuitForForge(Version, SelectedForge))
                    continue;
                if (Version.DisplayName.StartsWith(_vanillaName + " "))
                    Versions.Add(Version);
            }

            if (!Versions.Any())
                return;
            // 排序
            Versions.Sort((Left, Right) =>
            {
                if (!Left.IsPreview && Right.IsPreview)
                    return true;
                if (Left.IsPreview && !Right.IsPreview)
                    return false;
                return ModMinecraft.CompareVersion(Left.DisplayName, Right.DisplayName) != 0;
            });
            // 可视化
            PanOptiFine.Children.Clear();
            foreach (var Version in Versions)
                PanOptiFine.Children.Add(
                    ModDownloadLib.OptiFineDownloadListItem(Version, (a, b) =>
                        this.OptiFine_Selected((dynamic)a, b), false));
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "可视化 OptiFine 安装版本列表出错", ModBase.LogLevel.Feedback);
        }
    }

    // 选择与清除
    private void OptiFine_Selected(MyListItem sender, EventArgs e)
    {
        SelectedOptiFine = (ModDownload.DlOptiFineListEntry)sender.Tag;
        if (SelectedForge is not null &&
                                  !(bool)IsOptiFineSuitForForge(SelectedOptiFine, SelectedForge))
            SelectedForge = null;
        OptiFabric_Loaded();
        Forge_Loaded();
        NeoForge_Loaded();
        CardOptiFine.IsSwapped = true;
        ReloadSelected();
    }

    private void OptiFine_Clear(object sender, MouseButtonEventArgs e)
    {
        SelectedOptiFine = null;
        SelectedOptiFabric = null;
        AutoSelectedOptiFabric = false;
        CardOptiFine.IsSwapped = true;
        e.Handled = true;
        Forge_Loaded();
        NeoForge_Loaded();
        ReloadSelected();
    }

    #endregion

    #region LiteLoader 列表

    /// <summary>
    ///     获取 LiteLoader 的加载异常信息。若正常则返回 Nothing。
    /// </summary>
    private string LoadLiteLoaderGetError()
    {
        // 检查 Loader
        if (GetLoaderError(LoadLiteLoader) is not null)
            return GetLoaderError(LoadLiteLoader);
        // 检查版本
        return ModDownload.DlLiteLoaderListLoader.Output.Value.Any(v => (v.Inherit ?? "") == (_vanillaName ?? ""))
            ? null
            : Lang.Text("Download.Install.State.NoVersion");
    }

    // 限制展开
    private void CardLiteLoader_PreviewSwap(object sender, ModBase.RouteEventArgs e)
    {
        if (LoadLiteLoaderGetError() is not null)
            e.Handled = true;
    }

    /// <summary>
    ///     尝试重新可视化 LiteLoader 版本列表。
    /// </summary>
    private void LiteLoader_Loaded()
    {
        try
        {
            if (ModDownload.DlLiteLoaderListLoader.State != ModBase.LoadState.Finished)
                return;
            // 获取版本列表
            var Versions = new List<ModDownload.DlLiteLoaderListEntry>();
            foreach (var Version in ModDownload.DlLiteLoaderListLoader.Output.Value)
                if ((Version.Inherit ?? "") == (_vanillaName ?? ""))
                    Versions.Add(Version);
            if (!Versions.Any())
                return;
            // 可视化
            PanLiteLoader.Children.Clear();
            foreach (var Version in Versions)
                PanLiteLoader.Children.Add(ModDownloadLib.LiteLoaderDownloadListItem(Version,
                    (a, b) => this.LiteLoader_Selected((dynamic)a, b), false));
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "可视化 LiteLoader 安装版本列表出错", ModBase.LogLevel.Feedback);
        }
    }

    // 选择与清除
    private void LiteLoader_Selected(MyListItem sender, EventArgs e)
    {
        SelectedLiteLoader = (ModDownload.DlLiteLoaderListEntry)sender.Tag;
        CardLiteLoader.IsSwapped = true;
        ReloadSelected();
    }

    private void LiteLoader_Clear(object sender, MouseButtonEventArgs e)
    {
        SelectedLiteLoader = null;
        CardLiteLoader.IsSwapped = true;
        e.Handled = true;
        ReloadSelected();
    }

    #endregion

    #region Forge 列表

    /// <summary>
    ///     获取 Forge 的加载异常信息。若正常则返回 Nothing。
    /// </summary>
    private string LoadForgeGetError()
    {
        if (ModMinecraft.CompareVersionGe("1.5.1", _vanillaName) && ModMinecraft.CompareVersionGe(_vanillaName, "1.1"))
            return Lang.Text("Download.Install.State.NoVersion");
                
        if (SelectedLoaderName is not null && !ReferenceEquals(SelectedLoaderName, "Forge"))
            return Lang.Text("Download.Install.Compat.IncompatibleWithLoader", SelectedLoaderName);

        // 检查 Loader
        if (GetLoaderError(LoadForge) is not null)
            return GetLoaderError(LoadForge);
        var loader = (ModLoader.LoaderTask<string, List<ModDownload.DlForgeVersionEntry>>)LoadForge.State;
        if ((_vanillaName ?? "") != (loader.Input ?? ""))
            return Lang.Text("Download.Install.State.Getting");
        // 检查版本
        foreach (var Version in loader.Output)
        {
            if (Version.Category == "universal" || Version.Category == "client")
                continue; // 跳过无法自动安装的版本
            if (SelectedNeoForge is not null)
                return Lang.Text("Download.Install.Compat.IncompatibleWithLoader", "NeoForge");
            if (SelectedFabric is not null)
                return Lang.Text("Download.Install.Compat.IncompatibleWithFabric");
            if (SelectedOptiFine is not null && ModMinecraft.CompareVersionGe(_vanillaName, "1.13") &&
                ModMinecraft.CompareVersionGe("1.14.3", _vanillaName))
                return Lang.Text("Download.Install.Compat.IncompatibleWithOptiFine"); // 1.13 ~ 1.14.3 OptiFine 检查
            if (SelectedOptiFine is not null && !(bool)IsOptiFineSuitForForge(SelectedOptiFine, Version))
                continue;
            return null;
        }

        return Lang.Text("Download.Install.Compat.IncompatibleWithOptiFine");
    }

    // 限制展开
    private void CardForge_PreviewSwap(object sender, ModBase.RouteEventArgs e)
    {
        if (LoadForgeGetError() is not null)
            e.Handled = true;
    }

    /// <summary>
    ///     尝试重新可视化 Forge 版本列表。
    /// </summary>
    private void Forge_Loaded()
    {
        try
        {
            if (!LoadForge.State.IsLoader)
                return;
            var loader = (ModLoader.LoaderTask<string, List<ModDownload.DlForgeVersionEntry>>)LoadForge.State;
            if ((_vanillaName ?? "") != (loader.Input ?? ""))
                return;
            if (loader.State != ModBase.LoadState.Finished)
                return;
            // 获取要显示的版本
            var versions = loader.Output.ToList(); // 复制数组，以免 Output 在实例化后变空
            if (!loader.Output.Any())
                return;
            PanForge.Children.Clear();
            versions = versions.Where(v =>
            {
                if (v.Category == "universal" || v.Category == "client")
                    return false; // 跳过无法自动安装的版本
                if (SelectedOptiFine is not null &&
                                          !(bool)IsOptiFineSuitForForge(SelectedOptiFine, v))
                    return false;
                return true;
            }).OrderByDescending(v => v).ToList();
            ModDownloadLib.ForgeDownloadListItemPreload(PanForge, versions,
                (a, b) => this.Forge_Selected((dynamic)a, b), false);
            foreach (var Version in versions)
                PanForge.Children.Add(
                    ModDownloadLib.ForgeDownloadListItem(Version, (a, b) => this.Forge_Selected((dynamic)a, b), false));
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "可视化 Forge 安装版本列表出错", ModBase.LogLevel.Feedback);
        }
    }

    // 选择与清除
    private void Forge_Selected(MyListItem sender, EventArgs e)
    {
        SelectedForge = (ModDownload.DlForgeVersionEntry)sender.Tag;
        SelectedLoaderName = "Forge";
        CardForge.IsSwapped = true;
        if (SelectedOptiFine is not null &&
                                  !(bool)IsOptiFineSuitForForge(SelectedOptiFine, SelectedForge))
            SelectedOptiFine = null;
        OptiFine_Loaded();
        ReloadSelected();
    }

    private void Forge_Clear(object sender, MouseButtonEventArgs e)
    {
        SelectedForge = null;
        SelectedLoaderName = null;
        CardForge.IsSwapped = true;
        e.Handled = true;
        OptiFine_Loaded();
        ReloadSelected();
    }

    #endregion

    #region NeoForge 列表

    /// <summary>
    ///     获取 NeoForge 的加载异常信息。若正常则返回 Nothing。
    /// </summary>
    private string LoadNeoForgeGetError()
    {
        if (SelectedOptiFine is not null)
            return Lang.Text("Download.Install.Compat.IncompatibleWithOptiFine");
        if (SelectedLoaderName is not null && !ReferenceEquals(SelectedLoaderName, "NeoForge"))
            return Lang.Text("Download.Install.Compat.IncompatibleWithLoader", SelectedLoaderName);
        // 检查 Loader
        if (GetLoaderError(LoadNeoForge) is not null)
            return GetLoaderError(LoadNeoForge);
        // 检查版本
        return ModDownload.DlNeoForgeListLoader.Output.Value.Any(v => (v.Inherit ?? "") == (_vanillaName ?? ""))
            ? null
            : Lang.Text("Download.Install.State.NoVersion");
    }

    // 限制展开
    private void CardNeoForge_PreviewSwap(object sender, ModBase.RouteEventArgs e)
    {
        if (LoadNeoForgeGetError() is not null)
            e.Handled = true;
    }

    /// <summary>
    ///     尝试重新可视化 NeoForge 版本列表。
    /// </summary>
    private void NeoForge_Loaded()
    {
        try
        {
            // 获取版本列表
            if (ModDownload.DlNeoForgeListLoader.State != ModBase.LoadState.Finished)
                return;
            var Versions = ModDownload.DlNeoForgeListLoader.Output.Value
                .Where(v => (v.Inherit ?? "") == (_vanillaName ?? "")).ToList();
            if (!Versions.Any())
                return;
            // 可视化
            PanNeoForge.Children.Clear();
            ModDownloadLib.NeoForgeDownloadListItemPreload(PanNeoForge, Versions,
                (a, b) => this.NeoForge_Selected((dynamic)a, b),
                false);
            foreach (var Version in Versions)
                PanNeoForge.Children.Add(
                    ModDownloadLib.NeoForgeDownloadListItem(Version, (a, b) => this.NeoForge_Selected((dynamic)a, b),
                        false));
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "可视化 NeoForge 安装版本列表出错", ModBase.LogLevel.Feedback);
        }
    }

    // 选择与清除
    private void NeoForge_Selected(MyListItem sender, EventArgs e)
    {
        SelectedNeoForge = (ModDownload.DlNeoForgeListEntry)sender.Tag;
        SelectedLoaderName = "NeoForge";
        CardNeoForge.IsSwapped = true;
        OptiFine_Loaded();
        ReloadSelected();
    }

    private void NeoForge_Clear(object sender, MouseButtonEventArgs e)
    {
        SelectedNeoForge = null;
        SelectedLoaderName = null;
        CardNeoForge.IsSwapped = true;
        e.Handled = true;
        OptiFine_Loaded();
        ReloadSelected();
    }

    #endregion

    #region Cleanroom 列表

    /// <summary>
    ///     获取 Cleanroom 的加载异常信息。若正常则返回 Nothing。
    /// </summary>
    private string LoadCleanroomGetError()
    {
        if (!_vanillaName.StartsWith("1."))
            return Lang.Text("Download.Install.State.NoAvailableVersion");
        if (SelectedOptiFine is not null)
            return Lang.Text("Download.Install.Compat.IncompatibleWithOptiFine");
        if (SelectedLoaderName is not null && !ReferenceEquals(SelectedLoaderName, "Cleanroom"))
            return Lang.Text("Download.Install.Compat.IncompatibleWithLoader", SelectedLoaderName);
        // 检查 Loader
        if (GetLoaderError(LoadCleanroom) is not null)
            return GetLoaderError(LoadCleanroom);
        // 检查版本
        return ModDownload.DlCleanroomListLoader.Output.Value.Any(v => (v.Inherit ?? "") == (_vanillaName ?? ""))
            ? null
            : Lang.Text("Download.Install.State.NoVersion");
    }

    // 限制展开
    private void CardCleanroom_PreviewSwap(object sender, ModBase.RouteEventArgs e)
    {
        if (LoadCleanroomGetError() is not null)
            e.Handled = true;
    }

    /// <summary>
    ///     尝试重新可视化 Cleanroom 版本列表。
    /// </summary>
    private void Cleanroom_Loaded()
    {
        try
        {
            // 获取版本列表
            if (ModDownload.DlCleanroomListLoader.State != ModBase.LoadState.Finished)
                return;
            var Versions = ModDownload.DlCleanroomListLoader.Output.Value
                .Where(v => (v.Inherit ?? "") == (_vanillaName ?? "")).ToList();
            if (!Versions.Any())
                return;
            // 可视化
            PanCleanroom.Children.Clear();
            ModDownloadLib.CleanroomDownloadListItemPreload(PanCleanroom, Versions,
                (a, b) => this.Cleanroom_Selected((dynamic)a, b), false);
            foreach (var Version in Versions)
                PanCleanroom.Children.Add(
                    ModDownloadLib.CleanroomDownloadListItem(Version, (a, b) => this.Cleanroom_Selected((dynamic)a, b),
                        false));
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "可视化 Cleanroom 安装版本列表出错", ModBase.LogLevel.Feedback);
        }
    }

    // 选择与清除
    private void Cleanroom_Selected(MyListItem sender, EventArgs e)
    {
        SelectedCleanroom = (ModDownload.DlCleanroomListEntry)sender.Tag;
        SelectedLoaderName = "Cleanroom";
        CardCleanroom.IsSwapped = true;
        OptiFine_Loaded();
        ReloadSelected();
    }

    private void Cleanroom_Clear(object sender, MouseButtonEventArgs e)
    {
        SelectedCleanroom = null;
        SelectedLoaderName = null;
        CardCleanroom.IsSwapped = true;
        e.Handled = true;
        OptiFine_Loaded();
        ReloadSelected();
    }

    #endregion

    #region Fabric 列表

    /// <summary>
    ///     获取 Fabric 的加载异常信息。若正常则返回 Nothing。
    /// </summary>
    private string LoadFabricGetError()
    {
        // 检查 OptiFine 1.20.5+：没有 OptiFabric 故全部不兼容
        if (SelectedOptiFine is not null && ModMinecraft.CompareVersionGe(_vanillaName, "1.20.5"))
            return Lang.Text("Download.Install.Compat.IncompatibleWithOptiFine");
        // 检查 Loader
        if (GetLoaderError(LoadFabric) is not null)
            return GetLoaderError(LoadFabric);
        // 检查版本
        foreach (JsonObject version in ModDownload.DlFabricListLoader.Output.Value["game"].AsArray())
            if ((version["version"].ToString() ?? "") ==
                (_vanillaName.Replace("∞", "infinite").Replace("Combat Test 7c", "1.16_combat-3") ?? ""))
            {
                if (SelectedLoaderName is not null && !ReferenceEquals(SelectedLoaderName, "Fabric"))
                    return Lang.Text("Download.Install.Compat.IncompatibleWithLoader", SelectedLoaderName);
                return null;
            }

        return Lang.Text("Download.Install.State.NoVersion");
    }

    // 限制展开
    private void CardFabric_PreviewSwap(object sender, ModBase.RouteEventArgs e)
    {
        if (LoadFabricGetError() is not null)
            e.Handled = true;
    }

    /// <summary>
    ///     尝试重新可视化 Fabric 版本列表。
    /// </summary>
    private void Fabric_Loaded()
    {
        try
        {
            if (ModDownload.DlFabricListLoader.State != ModBase.LoadState.Finished)
                return;
            // 获取版本列表
            var versions = (JsonArray)ModDownload.DlFabricListLoader.Output.Value["loader"];
            if (!versions.Any())
                return;
            // 可视化
            PanFabric.Children.Clear();
            PanFabric.Tag = versions;
            CardFabric.SwapControl = PanFabric;
            CardFabric.InstallMethod = stack =>
            {
                foreach (var item in (IEnumerable)stack.Tag)
                    stack.Children.Add(
                        ModDownloadLib.FabricDownloadListItem((JsonObject)item,
                            (a, b) => this.Fabric_Selected((dynamic)a, b)));
            };
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "可视化 Fabric 安装版本列表出错", ModBase.LogLevel.Feedback);
        }
    }

    // 选择与清除
    public void Fabric_Selected(MyListItem sender, EventArgs e)
    {
        SelectedFabric = ((dynamic)sender.Tag)["version"].ToString();
        SelectedLoaderName = "Fabric";
        FabricApi_Loaded();
        OptiFabric_Loaded();
        CardFabric.IsSwapped = true;
        ReloadSelected();
    }

    private void Fabric_Clear(object sender, MouseButtonEventArgs e)
    {
        SelectedFabric = null;
        SelectedFabricApi = null;
        AutoSelectedFabricApi = false;
        SelectedOptiFabric = null;
        AutoSelectedOptiFabric = false;
        SelectedLoaderName = null;
        SelectedAPIName = null;
        CardFabric.IsSwapped = true;
        e.Handled = true;
        ReloadSelected();
    }

    #endregion

    #region Fabric API 列表

    /// <summary>
    ///     判断某 Fabric API 是否适配当前选择的原版版本。
    /// </summary>
    public bool IsFabricApiCompatible(ModComp.CompFile fabricApi)
    {
        var fabricApiName = fabricApi.DisplayName;
        try
        {
            if (fabricApiName is null || _vanillaName is null)
                return false;
            fabricApiName = fabricApiName.ToLower();
            _vanillaName = _vanillaName.Replace("∞", "infinite").Replace("Combat Test 7c", "1.16_combat-3").ToLower();
            if (fabricApiName.StartsWith("[" + _vanillaName + "]"))
                return true;
            if (!fabricApiName.Contains("/") || !fabricApiName.Contains("]"))
                return false;
            // 直接的判断（例如 1.18.1/22w03a）
            foreach (var part in fabricApiName.BeforeFirst("]").TrimStart('[').Split("/"))
                if ((part ?? "") == (_vanillaName ?? ""))
                    return true;
            // 将版本名分割语素（例如 1.16.4/5）
            var lefts = fabricApiName.BeforeFirst("]").RegexSearch("[a-z/]+|[0-9/]+");
            var rights = _vanillaName.BeforeFirst("]").RegexSearch("[a-z/]+|[0-9/]+");
            // 对每段进行判断
            var i = 0;
            while (true)
            {
                // 两边均缺失，感觉是一个东西
                if (lefts.Count - 1 < i && rights.Count - 1 < i)
                    return true;
                // 确定两边是否一致
                var leftValue = lefts.Count - 1 < i ? "-1" : lefts[i];
                var rightValue = rights.Count - 1 < i ? "-1" : rights[i];
                if (!leftValue.Contains("/"))
                {
                    if ((leftValue ?? "") != (rightValue ?? ""))
                        return false;
                }
                // 左边存在斜杠
                else if (!leftValue.Contains(rightValue))
                {
                    return false;
                }

                i += 1;
            }

            return true;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "判断 Fabric API 版本适配性出错（" + fabricApiName + ", " + _vanillaName + "）");
            return false;
        }
    }

    /// <summary>
    ///     获取 FabricApi 的加载异常信息。若正常则返回 Nothing。
    /// </summary>
    private string LoadFabricApiGetError()
    {
        // 检查 Loader
        if (GetLoaderError(LoadFabricApi) is not null)
            return GetLoaderError(LoadFabricApi);
        if (ModDownload.DlFabricApiLoader.Output is null)
            return SelectedFabric is null ? Lang.Text("Download.Install.Compat.RequiresFabric") : Lang.Text("Download.Install.State.Getting");
        // 检查版本
        if (ModDownload.DlFabricApiLoader.Output.Any(f => IsFabricApiCompatible(f)))
            return SelectedFabric is null ? Lang.Text("Download.Install.Compat.RequiresFabric") : null;

        return Lang.Text("Download.Install.State.NoVersion");
    }

    // 限制展开
    private void CardFabricApi_PreviewSwap(object sender, ModBase.RouteEventArgs e)
    {
        if (LoadFabricApiGetError() is not null)
            e.Handled = true;
    }

    private bool AutoSelectedFabricApi;

    /// <summary>
    ///     尝试重新可视化 FabricApi 版本列表。
    /// </summary>
    private void FabricApi_Loaded()
    {
        try
        {
            if (ModDownload.DlFabricApiLoader.State != ModBase.LoadState.Finished)
                return;
            if (_vanillaName is null || (SelectedFabric is null && SelectedQuilt is null))
                return;
            // 获取版本列表
            var versions = new List<ModComp.CompFile>();
            foreach (var version in ModDownload.DlFabricApiLoader.Output)
                if (IsFabricApiCompatible(version))
                {
                    if (!version.DisplayName.StartsWith("["))
                    {
                        ModBase.Log("[Download] 已特判修改 Fabric API 显示名：" + version.DisplayName, ModBase.LogLevel.Debug);
                        version.DisplayName = "[" + _vanillaName + "] " + version.DisplayName;
                    }

                    versions.Add(version);
                }

            if (!versions.Any())
                return;
            versions = versions.OrderByDescending(v => v.ReleaseDate).ToList();
            // 可视化
            PanFabricApi.Children.Clear();
            foreach (var version in versions)
            {
                if (!IsFabricApiCompatible(version))
                    continue;
                PanFabricApi.Children.Add(
                    ModDownloadLib.FabricApiDownloadListItem(version,
                        (a, b) => this.FabricApi_Selected((dynamic)a, b)));
            }

            // 自动选择 Fabric API
            if ((!AutoSelectedFabricApi && SelectedQuilt is null) ||
                (SelectedQuilt is not null && ReferenceEquals(LoadQSLGetError(), Lang.Text("Download.Install.State.NoAvailableVersion"))))
            {
                AutoSelectedFabricApi = true;
                ModBase.Log($"[Download] 已自动选择 Fabric API：{((MyListItem)PanFabricApi.Children[0]).Title}");
                FabricApi_Selected((MyListItem)PanFabricApi.Children[0], null);
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "可视化 Fabric API 安装版本列表出错", ModBase.LogLevel.Feedback);
        }
    }

    // 选择与清除
    private void FabricApi_Selected(MyListItem sender, EventArgs e)
    {
        SelectedFabricApi = (ModComp.CompFile)sender.Tag;
        SelectedAPIName = "Fabric API";
        CardFabricApi.IsSwapped = true;
        ReloadSelected();
    }

    private void FabricApi_Clear(object sender, MouseButtonEventArgs e)
    {
        SelectedFabricApi = null;
        SelectedAPIName = null;
        CardFabricApi.IsSwapped = true;
        e.Handled = true;
        ReloadSelected();
    }

    #endregion

    #region LegacyFabric 列表

    /// <summary>
    ///     获取 LegacyFabric 的加载异常信息。若正常则返回 Nothing。
    /// </summary>
    private string LoadLegacyFabricGetError()
    {
        if (LoadLegacyFabric is null || LoadLegacyFabric.State.LoadingState == MyLoading.MyLoadingState.Run)
            return Lang.Text("Download.Install.State.Loading");
        if (LoadLegacyFabric.State.LoadingState == MyLoading.MyLoadingState.Error)
            return Lang.Text("Download.Install.State.GetVersionListFailed", ((ModLoader.LoaderBase)LoadLegacyFabric.State).Error.Message);
        foreach (JsonObject Version in ModDownload.DlLegacyFabricListLoader.Output.Value["game"].AsArray())
            if ((Version["version"].ToString() ?? "") == (_vanillaName ?? ""))
            {
                if (SelectedLiteLoader is not null)
                    return Lang.Text("Download.Install.Compat.IncompatibleWithLiteLoader");
                if (SelectedLoaderName is not null && !ReferenceEquals(SelectedLoaderName, "LegacyFabric"))
                    return Lang.Text("Download.Install.Compat.IncompatibleWithLoader", SelectedLoaderName);
                return null;
            }

        return Lang.Text("Download.Install.State.NoVersion");
    }

    // 限制展开
    private void CardLegacyFabric_PreviewSwap(object sender, ModBase.RouteEventArgs e)
    {
        if (LoadLegacyFabricGetError() is not null)
            e.Handled = true;
    }

    /// <summary>
    ///     尝试重新可视化 LegacyFabric 版本列表。
    /// </summary>
    private void LegacyFabric_Loaded()
    {
        try
        {
            if (ModDownload.DlLegacyFabricListLoader.State != ModBase.LoadState.Finished)
                return;
            // 获取版本列表
            var Versions = (JsonArray)ModDownload.DlLegacyFabricListLoader.Output.Value["loader"];
            if (!Versions.Any())
                return;
            // 可视化
            PanLegacyFabric.Children.Clear();
            PanLegacyFabric.Tag = Versions;
            CardLegacyFabric.SwapControl = PanLegacyFabric;
            CardLegacyFabric.InstallMethod = Stack =>
            {
                foreach (var item in (IEnumerable)Stack.Tag)
                    Stack.Children.Add(ModDownloadLib.LegacyFabricDownloadListItem((JsonObject)item,
                        (a, b) => this.LegacyFabric_Selected((dynamic)a, b)));
            };
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "可视化 LegacyFabric 安装版本列表出错", ModBase.LogLevel.Feedback);
        }
    }

    // 选择与清除
    public void LegacyFabric_Selected(MyListItem sender, EventArgs e)
    {
        SelectedLegacyFabric = ((dynamic)sender.Tag)("version").ToString();
        SelectedLoaderName = "LegacyFabric";
        LegacyFabricApi_Loaded();
        CardLegacyFabric.IsSwapped = true;
        ReloadSelected();
    }

    private void LegacyFabric_Clear(object sender, MouseButtonEventArgs e)
    {
        SelectedLegacyFabric = null;
        SelectedLegacyFabricApi = null;
        AutoSelectedLegacyFabricApi = false;
        SelectedLoaderName = null;
        SelectedAPIName = null;
        CardLegacyFabric.IsSwapped = true;
        e.Handled = true;
        ReloadSelected();
    }

    #endregion

    #region Legacy Fabric API 列表

    /// <summary>
    ///     从显示名判断该 API 是否与某版本适配。
    /// </summary>
    public static bool IsSuitableLegacyFabricApi(List<string> SupportVersions, string MinecraftVersion)
    {
        try
        {
            if (SupportVersions.Contains(MinecraftVersion)) return true;

            return false;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "判断 Legacy Fabric API 版本适配性出错（" + SupportVersions + ", " + MinecraftVersion + "）");
            return false;
        }
    }

    /// <summary>
    ///     获取 LegacyFabricApi 的加载异常信息。若正常则返回 Nothing。
    /// </summary>
    private string LoadLegacyFabricApiGetError()
    {
        if (LoadLegacyFabricApi is null || LoadLegacyFabricApi.State.LoadingState == MyLoading.MyLoadingState.Run)
            return Lang.Text("Download.Install.State.Loading");
        if (LoadLegacyFabricApi.State.LoadingState == MyLoading.MyLoadingState.Error)
            return Lang.Text("Download.Install.State.GetVersionListFailed", ((ModLoader.LoaderBase)LoadLegacyFabricApi.State).Error.Message);
        if (SelectedAPIName is not null && !ReferenceEquals(SelectedAPIName, "Legacy Fabric API"))
            return Lang.Text("Download.Install.Compat.IncompatibleWithLoader", SelectedAPIName);
        if (ModDownload.DlLegacyFabricApiLoader.Output is null)
        {
            if (SelectedLegacyFabric is null)
                return Lang.Text("Download.Install.Compat.RequiresLegacyFabric");
            return Lang.Text("Download.Install.State.Loading");
        }

        foreach (var Version in ModDownload.DlLegacyFabricApiLoader.Output)
        {
            if (!IsSuitableLegacyFabricApi(Version.GameVersions, _vanillaName))
                continue;
            if (SelectedLegacyFabric is null)
                return Lang.Text("Download.Install.Compat.RequiresLegacyFabric");
            return null;
        }

        return Lang.Text("Download.Install.State.NoVersion");
    }

    // 限制展开
    private void CardLegacyFabricApi_PreviewSwap(object sender, ModBase.RouteEventArgs e)
    {
        if (LoadLegacyFabricApiGetError() is not null)
            e.Handled = true;
    }

    private bool AutoSelectedLegacyFabricApi;

    /// <summary>
    ///     尝试重新可视化 LegacyFabricApi 版本列表。
    /// </summary>
    private void LegacyFabricApi_Loaded()
    {
        try
        {
            if (ModDownload.DlLegacyFabricApiLoader.State != ModBase.LoadState.Finished)
                return;
            if (_vanillaName is null || (SelectedLegacyFabric is null && SelectedQuilt is null))
                return;
            // 获取版本列表
            var Versions = new List<ModComp.CompFile>();
            foreach (var Version in ModDownload.DlLegacyFabricApiLoader.Output)
                if (IsSuitableLegacyFabricApi(Version.GameVersions, _vanillaName))
                    Versions.Add(Version);

            if (!Versions.Any())
                return;
            Versions = Versions.OrderByDescending(v => v.ReleaseDate).ToList();
            // 可视化
            PanLegacyFabricApi.Children.Clear();
            foreach (var Version in Versions)
            {
                if (!IsSuitableLegacyFabricApi(Version.GameVersions, _vanillaName))
                    continue;
                PanLegacyFabricApi.Children.Add(
                    ModDownloadLib.LegacyFabricApiDownloadListItem(Version,
                        (a, b) => this.LegacyFabricApi_Selected((dynamic)a, b)));
            }

            // 自动选择 Legacy Fabric API
            if ((!AutoSelectedLegacyFabricApi && SelectedQuilt is null) ||
                (SelectedQuilt is not null && ReferenceEquals(LoadQSLGetError(), Lang.Text("Download.Install.State.NoAvailableVersion"))))
            {
                AutoSelectedLegacyFabricApi = true;
                ModBase.Log($"[Download] 已自动选择 Legacy Fabric API：{((MyListItem)PanLegacyFabricApi.Children[0]).Title}");
                LegacyFabricApi_Selected((MyListItem)PanLegacyFabricApi.Children[0], null);
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "可视化 Legacy Fabric API 安装版本列表出错", ModBase.LogLevel.Feedback);
        }
    }

    // 选择与清除
    private void LegacyFabricApi_Selected(MyListItem sender, EventArgs e)
    {
        SelectedLegacyFabricApi = (ModComp.CompFile)sender.Tag;
        SelectedAPIName = "Legacy Fabric API";
        CardLegacyFabricApi.IsSwapped = true;
        ReloadSelected();
    }

    private void LegacyFabricApi_Clear(object sender, MouseButtonEventArgs e)
    {
        SelectedLegacyFabricApi = null;
        SelectedAPIName = null;
        CardLegacyFabricApi.IsSwapped = true;
        e.Handled = true;
        ReloadSelected();
    }

    #endregion

    #region Quilt 列表

    /// <summary>
    ///     获取 Quilt 的加载异常信息。若正常则返回 Nothing。
    /// </summary>
    private string LoadQuiltGetError()
    {
        if (SelectedOptiFine is not null)
            return Lang.Text("Download.Install.Compat.IncompatibleWithOptiFine");
        if (SelectedLoaderName is not null && !ReferenceEquals(SelectedLoaderName, "Quilt"))
            return Lang.Text("Download.Install.Compat.IncompatibleWithLoader", SelectedLoaderName);
        // 检查 Loader
        if (GetLoaderError(LoadQuilt) is not null)
            return GetLoaderError(LoadQuilt);
        // 检查版本
        foreach (JsonObject version in ModDownload.DlFabricListLoader.Output.Value["game"].AsArray())
            if ((version["version"].ToString() ?? "") ==
                (_vanillaName.Replace("∞", "infinite").Replace("Combat Test 7c", "1.16_combat-3") ?? ""))
            {
                if (SelectedLoaderName is not null && !ReferenceEquals(SelectedLoaderName, "Fabric"))
                    return Lang.Text("Download.Install.Compat.IncompatibleWithLoader", SelectedLoaderName);
                return null;
            }

        return Lang.Text("Download.Install.State.NoVersion");
    }

    // 限制展开
    private void CardQuilt_PreviewSwap(object sender, ModBase.RouteEventArgs e)
    {
        if (LoadQuiltGetError() is not null)
            e.Handled = true;
    }

    /// <summary>
    ///     尝试重新可视化 Quilt 版本列表。
    /// </summary>
    private void Quilt_Loaded()
    {
        try
        {
            if (ModDownload.DlQuiltListLoader.State != ModBase.LoadState.Finished)
                return;
            // 获取版本列表
            var Versions = (JsonArray)ModDownload.DlQuiltListLoader.Output.Value["loader"];
            if (!Versions.Any())
                return;
            // 可视化
            PanQuilt.Children.Clear();
            PanQuilt.Tag = Versions;
            CardQuilt.SwapControl = PanQuilt;
            CardQuilt.InstallMethod = Stack =>
            {
                foreach (var item in (IEnumerable)Stack.Tag)
                    Stack.Children.Add(
                        ModDownloadLib.QuiltDownloadListItem((JsonObject)item,
                            (a, b) => this.Quilt_Selected((dynamic)a, b)));
            };
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "可视化 Quilt 安装版本列表出错", ModBase.LogLevel.Feedback);
        }
    }

    // 选择与清除
    public void Quilt_Selected(MyListItem sender, EventArgs e)
    {
        SelectedQuilt = ((dynamic)sender.Tag)["version"].ToString();
        SelectedLoaderName = "Quilt";
        FabricApi_Loaded();
        QSL_Loaded();
        CardQuilt.IsSwapped = true;
        ReloadSelected();
    }

    private void Quilt_Clear(object sender, MouseButtonEventArgs e)
    {
        SelectedQuilt = null;
        SelectedQSL = null;
        SelectedFabricApi = null;
        SelectedLoaderName = null;
        SelectedAPIName = null;
        CardQuilt.IsSwapped = true;
        e.Handled = true;
        ReloadSelected();
    }

    #endregion

    #region QSL 列表

    /// <summary>
    ///     从显示名判断该 API 是否与某版本适配。
    /// </summary>
    public static bool IsSuitableQSL(List<string> SupportVersions, string MinecraftVersion)
    {
        try
        {
            if (SupportVersions.Contains(MinecraftVersion)) return true;

            return false;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "判断 QSL 版本适配性出错（" + SupportVersions + ", " + MinecraftVersion + "）");
            return false;
        }
    }

    /// <summary>
    ///     获取 QSL 的加载异常信息。若正常则返回 Nothing。
    /// </summary>
    private string LoadQSLGetError()
    {
        if (LoadQSL is null || LoadQSL.State.LoadingState == MyLoading.MyLoadingState.Run)
            return Lang.Text("Download.Version.LoadingList");
        if (LoadQSL.State.LoadingState == MyLoading.MyLoadingState.Error)
            return Lang.Text("Download.Install.State.GetVersionListFailed", ((ModLoader.LoaderBase)LoadQSL.State).Error.Message);
        if (SelectedAPIName is not null && !ReferenceEquals(SelectedAPIName, "QFAPI / QSL"))
            return Lang.Text("Download.Install.Compat.IncompatibleWithLoader", SelectedAPIName);
        if (ModDownload.DlQSLLoader.Output is null)
        {
            if (SelectedQuilt is null)
                return Lang.Text("Download.Install.Compat.RequiresQuilt");
            return Lang.Text("Download.Version.LoadingList");
        }

        foreach (var Version in ModDownload.DlQSLLoader.Output)
        {
            if (!IsSuitableQSL(Version.GameVersions, _vanillaName))
                continue;
            if (SelectedQuilt is null)
                return Lang.Text("Download.Install.Compat.RequiresQuilt");
            return null;
        }

        return Lang.Text("Download.Install.State.NoAvailableVersion");
    }

    // 限制展开
    private void CardQSL_PreviewSwap(object sender, ModBase.RouteEventArgs e)
    {
        if (LoadQSLGetError() is not null)
            e.Handled = true;
    }

    private bool AutoSelectedQSL;

    /// <summary>
    ///     尝试重新可视化 QSL 版本列表。
    /// </summary>
    private void QSL_Loaded()
    {
        try
        {
            if (ModDownload.DlQSLLoader.State != ModBase.LoadState.Finished)
                return;
            if (_vanillaName is null || SelectedQuilt is null)
                return;
            // 获取版本列表
            var Versions = new List<ModComp.CompFile>();
            foreach (var Version in ModDownload.DlQSLLoader.Output)
                if (IsSuitableQSL(Version.GameVersions, _vanillaName))
                {
                    if (!Version.DisplayName.StartsWith("["))
                    {
                        ModBase.Log("[Download] 已特判修改 QSL 显示名：" + Version.DisplayName, ModBase.LogLevel.Debug);
                        Version.DisplayName = "[" + _vanillaName + "] " + Version.DisplayName;
                    }

                    Versions.Add(Version);
                }

            if (!Versions.Any())
                return;
            Versions = Versions.Sort((a, b) => a.ReleaseDate > b.ReleaseDate);
            // 可视化
            PanQSL.Children.Clear();
            foreach (var Version in Versions)
            {
                if (!IsSuitableQSL(Version.GameVersions, _vanillaName))
                    continue;
                PanQSL.Children.Add(
                    ModDownloadLib.QSLDownloadListItem(Version, (a, b) => this.QSL_Selected((dynamic)a, b)));
            }

            // 自动选择 QSL
            if (!AutoSelectedQSL)
            {
                AutoSelectedQSL = true;
                ModBase.Log($"[Download] 已自动选择 QSL：{((MyListItem)PanQSL.Children[0]).Title}");
                QSL_Selected((MyListItem)PanQSL.Children[0], null);
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "可视化 QSL 安装版本列表出错", ModBase.LogLevel.Feedback);
        }
    }

    // 选择与清除
    private void QSL_Selected(MyListItem sender, EventArgs e)
    {
        SelectedQSL = (ModComp.CompFile)sender.Tag;
        SelectedAPIName = "QFAPI / QSL";
        CardQSL.IsSwapped = true;
        ReloadSelected();
    }

    private void QSL_Clear(object sender, MouseButtonEventArgs e)
    {
        SelectedQSL = null;
        SelectedAPIName = null;
        CardQSL.IsSwapped = true;
        e.Handled = true;
        ReloadSelected();
    }

    #endregion

    #region OptiFabric 列表

    /// <summary>
    ///     判断某 OptiFabric 是否适配当前选择的原版版本。
    /// </summary>
    private bool IsOptiFabricCompatible(ModComp.CompFile modFile)
    {
        try
        {
            if (_vanillaName is null)
                return false;
            return modFile.GameVersions.Contains(_vanillaName);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "判断 OptiFabric 版本适配性出错（" + _vanillaName + "）");
            return false;
        }
    }

    private bool AutoSelectedOptiFabric;

    /// <summary>
    ///     获取 OptiFabric 的加载异常信息。若正常则返回 Nothing。
    /// </summary>
    private string LoadOptiFabricGetError()
    {
        if (VanillaDrop >= 140 && VanillaDrop <= 150)
            return Lang.Text("Download.Install.Compat.OptiFabricOriginsRequired");
        // 检查 Loader
        if (GetLoaderError(LoadOptiFabric) is not null)
            return GetLoaderError(LoadOptiFabric);
        // 检查版本
        if (ModDownload.DlOptiFabricLoader.Output is null)
        {
            if (SelectedFabric is null && SelectedOptiFine is null)
                return Lang.Text("Download.Install.Compat.RequiresOptiFineAndFabric");
            if (SelectedFabric is null)
                return Lang.Text("Download.Install.Compat.RequiresFabric");
            if (SelectedOptiFine is null)
                return Lang.Text("Download.Install.Compat.RequiresOptiFine");
            return Lang.Text("Download.Install.State.Getting");
        }

        foreach (var version in ModDownload.DlOptiFabricLoader.Output)
        {
            if (!IsOptiFabricCompatible(version))
                continue; // 2135#
            if (SelectedFabric is null && SelectedOptiFine is null)
                return Lang.Text("Download.Install.Compat.RequiresOptiFineAndFabric");
            if (SelectedFabric is null)
                return Lang.Text("Download.Install.Compat.RequiresFabric");
            if (SelectedOptiFine is null)
                return Lang.Text("Download.Install.Compat.RequiresOptiFine");
            return null; // 通过检查
        }

        return Lang.Text("Download.Install.State.NoVersion");
    }

    // 限制展开
    private void CardOptiFabric_PreviewSwap(object sender, ModBase.RouteEventArgs e)
    {
        if (LoadOptiFabricGetError() is not null)
            e.Handled = true;
    }

    /// <summary>
    ///     尝试重新可视化 OptiFabric 版本列表。
    /// </summary>
    private void OptiFabric_Loaded()
    {
        try
        {
            if (ModDownload.DlOptiFabricLoader.State != ModBase.LoadState.Finished)
                return;
            if (_vanillaName is null || SelectedFabric is null || SelectedOptiFine is null)
                return;
            // 获取版本列表
            var versions = new List<ModComp.CompFile>();
            foreach (var Version in ModDownload.DlOptiFabricLoader.Output)
                if (IsOptiFabricCompatible(Version))
                    versions.Add(Version);
            if (!versions.Any())
                return;
            // 排序
            versions = versions.OrderByDescending(v => v.ReleaseDate).ToList();
            // 可视化
            PanOptiFabric.Children.Clear();
            foreach (var Version in versions)
            {
                if (!IsOptiFabricCompatible(Version))
                    continue;
                PanOptiFabric.Children.Add(
                    ModDownloadLib.OptiFabricDownloadListItem(Version,
                        (a, b) => this.OptiFabric_Selected((dynamic)a, b)));
            }

            // 自动选择 OptiFabric
            if (AutoSelectedOptiFabric || (VanillaDrop >= 140 && VanillaDrop <= 150))
                return; // 1.14~15 不自动选择
            AutoSelectedOptiFabric = true;
            ModBase.Log($"[Download] 已自动选择 OptiFabric：{((MyListItem)PanOptiFabric.Children[0]).Title}");
            OptiFabric_Selected((MyListItem)PanOptiFabric.Children[0], null);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "可视化 OptiFabric 安装版本列表出错", ModBase.LogLevel.Feedback);
        }
    }

    // 选择与清除
    private void OptiFabric_Selected(MyListItem sender, EventArgs e)
    {
        SelectedOptiFabric = (ModComp.CompFile)sender.Tag;
        CardOptiFabric.IsSwapped = true;
        ReloadSelected();
    }

    private void OptiFabric_Clear(object sender, MouseButtonEventArgs e)
    {
        SelectedOptiFabric = null;
        CardOptiFabric.IsSwapped = true;
        e.Handled = true;
        ReloadSelected();
    }

    #endregion

    #region LabyMod 列表

    /// <summary>
    ///     获取 LabyMod 的加载异常信息。若正常则返回 Nothing。
    /// </summary>
    private string LoadLabyModGetError()
    {
        if (LoadLabyMod is null || LoadLabyMod.State.LoadingState == MyLoading.MyLoadingState.Run)
            return Lang.Text("Download.Install.State.Loading");
        if (LoadLabyMod.State.LoadingState == MyLoading.MyLoadingState.Error)
            return Lang.Text("Download.Install.State.GetVersionListFailed", ((ModLoader.LoaderBase)LoadLabyMod.State).Error.Message);
        // 检查 Loader
        if (GetLoaderError(LoadLabyMod) is not null)
            return GetLoaderError(LoadLabyMod);
        if (SelectedOptiFine is not null)
            return Lang.Text("Download.Install.Compat.IncompatibleWithOptiFine");
        if (SelectedLoaderName is not null && !ReferenceEquals(SelectedLoaderName, "LabyMod"))
            return Lang.Text("Download.Install.Compat.IncompatibleWithLoader", SelectedLoaderName);
        foreach (JsonObject Version in ModDownload.DlLabyModListLoader.Output.Value["production"]["minecraftVersions"].AsArray())
            if ((Version["version"].ToString() ?? "") == (_vanillaName ?? ""))
                return null;
        foreach (JsonObject Version in ModDownload.DlLabyModListLoader.Output.Value["snapshot"]["minecraftVersions"].AsArray())
            if ((Version["version"].ToString() ?? "") == (_vanillaName ?? ""))
                return null;
        return Lang.Text("Download.Install.State.NoVersion");
    }

    // 限制展开
    private void CardLabyMod_PreviewSwap(object sender, ModBase.RouteEventArgs e)
    {
        if (LoadLabyModGetError() is not null)
            e.Handled = true;
    }

    /// <summary>
    ///     尝试重新可视化 LabyMod 版本列表。
    /// </summary>
    private void LabyMod_Loaded()
    {
        try
        {
            if (LoadLabyMod.State.LoadingState == MyLoading.MyLoadingState.Run)
                return;
            // 获取版本列表
            var Versions = ModDownload.DlLabyModListLoader.Output.Value;
            if (Versions is null || Versions["production"] is null || Versions["snapshot"] is null)
                return;
            // 可视化
            var ProcessedVersions = new JsonArray();
            foreach (JsonObject Production in Versions["production"]["minecraftVersions"].AsArray())
                if ((Production["version"].ToString() ?? "") == (_vanillaName ?? ""))
                {
                    var ProductionVersion = new JsonObject();
                    ProductionVersion.Add("version", Versions["production"]["labyModVersion"].ToString());
                    ProductionVersion.Add("channel", "production");
                    ProductionVersion.Add("commitReference", Versions["production"]["commitReference"].ToString());
                    ProcessedVersions.Add(ProductionVersion);
                }

            foreach (JsonObject Snapshot in Versions["snapshot"]["minecraftVersions"].AsArray())
                if ((Snapshot["version"].ToString() ?? "") == (_vanillaName ?? ""))
                {
                    var SnapshotVersion = new JsonObject();
                    SnapshotVersion.Add("version", Versions["snapshot"]["labyModVersion"].ToString());
                    SnapshotVersion.Add("channel", "snapshot");
                    SnapshotVersion.Add("commitReference", Versions["snapshot"]["commitReference"].ToString());
                    ProcessedVersions.Add(SnapshotVersion);
                }

            // MyMsgBox(If(ProcessedVersions.ToString, "Nothing"))
            PanLabyMod.Children.Clear();
            PanLabyMod.Tag = ProcessedVersions;
            CardLabyMod.SwapControl = PanLabyMod;
            CardLabyMod.InstallMethod = Stack =>
            {
                foreach (JsonObject item in (IEnumerable)Stack.Tag)
                    Stack.Children.Add(
                        ModDownloadLib.LabyModDownloadListItem(item, (a, b) => this.LabyMod_Selected((dynamic)a, b)));
            };
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "可视化 LabyMod 安装版本列表出错", ModBase.LogLevel.Feedback);
        }
    }

    // 选择与清除
    public void LabyMod_Selected(MyListItem sender, EventArgs e)
    {
        SelectedLabyModChannel = ((dynamic)sender.Tag)("channel").ToString();
        SelectedLabyModCommitRef = ((dynamic)sender.Tag)("commitReference").ToString();
        SelectedLabyModVersion =
            ((dynamic)sender.Tag)("version").ToString() + (SelectedLabyModChannel == "snapshot" ? " " + Lang.Text("Download.Version.Type.Snapshot") : " " + Lang.Text("Download.Version.Type.Stable"));
        SelectedLoaderName = "LabyMod";
        CardLabyMod.IsSwapped = true;
        ReloadSelected();
    }

    private void LabyMod_Clear(object sender, MouseButtonEventArgs e)
    {
        SelectedLabyModCommitRef = null;
        SelectedLabyModVersion = null;
        SelectedLabyModChannel = null;
        
        if (SelectedLoaderName == "LabyMod")
        {
            SelectedLoaderName = null;
        }    

        SelectedAPIName = null;
        CardLabyMod.IsSwapped = true;
        e.Handled = true;
        ReloadSelected();
    }

    #endregion
}
