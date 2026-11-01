using System.Collections;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using PCL.Core.App;
using PCL.Core.UI.Dialogs;
using PCL.Core.Utils.Validate;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;

namespace PCL;

public partial class PageDownloadInstall : IPageView
{
    private bool isLoad;
    private bool _refreshed;

    private readonly PageDownloadInstallViewModel _vm = new();

    public PageDownloadInstallViewModel ViewModel => _vm;

    public PageDownloadInstall()
    {
        Initialized += (_, _) => LoaderInit();
        Loaded += (_, _) => Init();
        Unloaded += (_, _) => _refreshed = false;
        InitializeComponent();
        DataContext = _vm;
        PanScroll = PanBack;
        _vm.LoadingText = Lang.Text("Download.Version.LoadingList");
        WeakLanguageChanged.Add(this, OnLanguageChanged);
        LoadOptiFine.StateChanged += (_, _, _) => OptiFine_Loaded();
        LoadLiteLoader.StateChanged += (_, _, _) => LiteLoader_Loaded();
        LoadForge.StateChanged += (_, _, _) => Forge_Loaded();
        LoadNeoForge.StateChanged += (_, _, _) => NeoForge_Loaded();
        LoadCleanroom.StateChanged += (_, _, _) => Cleanroom_Loaded();
        LoadFabric.StateChanged += (_, _, _) => Fabric_Loaded();
        LoadFabricApi.StateChanged += (_, _, _) => FabricApi_Loaded();
        LoadOptiFabric.StateChanged += (_, _, _) => OptiFabric_Loaded();
        LoadLabyMod.StateChanged += (_, _, _) => LabyMod_Loaded();
    }

    private void Card_Swap(object sender, Common.RouteEventArgs e) => ReloadSelected();

    private void Load_State(object sender, MyLoading.MyLoadingState state, MyLoading.MyLoadingState oldState) =>
        ReloadSelected();

    private void LoadLegacyFabric_StateChanged(object sender, MyLoading.MyLoadingState state,
        MyLoading.MyLoadingState oldState) => LegacyFabric_Loaded();

    private void LoadLegacyFabricApi_StateChanged(object sender, MyLoading.MyLoadingState state,
        MyLoading.MyLoadingState oldState) => LegacyFabricApi_Loaded();

    private void BtnStart_OnClick(object sender, MouseButtonEventArgs e) => BtnStart_Click();

    private static void OnLanguageChanged(PageDownloadInstall page) => page._OnLanguageChanged();

    private void LoaderInit()
    {
        disabledPageAnimControls.Add(BtnStart);
        PageLoaderInit(LoadMinecraft, PanLoad, PanAllBack, null, GameDownload.dlClientListLoader,
            _ => LoadMinecraft_OnFinish());
    }

    public void OnNavigatedTo(object? parameter) => RefreshOnNavigate();

    public void OnNavigatedFrom() { }

    private void RefreshOnNavigate()
    {
        if (_refreshed) return;
        _refreshed = true;

        PanBack.ScrollToHome();
        GameDownload.DlOptiFineListLoader.Start();
        GameDownload.DlLiteLoaderListLoader.Start();
        GameDownload.DlFabricListLoader.Start();
        GameDownload.DlNeoForgeListLoader.Start();
        GameDownload.DlCleanroomListLoader.Start();
        GameDownload.DlLabyModListLoader.Start();
        GameDownload.DlLegacyFabricListLoader.Start();

        // 重载预览
        TextSelectName.ValidateRules = [new FolderNameValidator(GameFolders.mcFolderSelected + "versions")];
        TextSelectName.Validate();
        ReloadSelected();
    }

    private void Init()
    {
        RefreshOnNavigate();

        // 非重复加载部分
        if (isLoad)
            return;
        isLoad = true;

        DownloadUi.McDownloadForgeRecommendedRefresh();

        LoadOptiFine.State = GameDownload.DlOptiFineListLoader;
        LoadLiteLoader.State = GameDownload.DlLiteLoaderListLoader;
        LoadFabric.State = GameDownload.DlFabricListLoader;
        LoadFabricApi.State = GameDownload.DlFabricApiLoader;
        LoadNeoForge.State = GameDownload.DlNeoForgeListLoader;
        LoadCleanroom.State = GameDownload.DlCleanroomListLoader;
        LoadOptiFabric.State = GameDownload.DlOptiFabricLoader;
        LoadLabyMod.State = GameDownload.DlLabyModListLoader;
        LoadLegacyFabric.State = GameDownload.DlLegacyFabricListLoader;
        LoadLegacyFabricApi.State = GameDownload.DlLegacyFabricApiLoader;
    }

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
                var message = ((Loading.LoaderBase)loader.State).Error.Message;
                return message == Lang.Text("Download.Install.State.NoVersion")
                    ? Lang.Text("Download.Install.State.NoVersion")
                    : Lang.Text("Download.Install.State.GetFailed", message);
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

    private void BtnBack_Click(object sender, EventArgs e)
    {
        ExitSelectPage();
    }

    #region 页面切换

    // 页面切换动画
    public bool isInSelectPage;
    private bool isFirstLoaded;

    private void EnterSelectPage()
    {
        if (isInSelectPage)
            return;
        isInSelectPage = true;

        _vm.PanInnerMargin = new Thickness(25d, 10d, 25d, 40d);

        autoSelectedFabricApi = false;
        autoSelectedOptiFabric = false;
        isSelectNameEdited = false;
        PanSelect.Visibility = Visibility.Visible;
        _vm.PanSelectHitTestVisible = true;
        _vm.PanMinecraftHitTestVisible = false;
        _vm.PanBackHitTestVisible = false;
        PanBack.ScrollToHome();

        disabledPageAnimControls.Remove(BtnStart);
        BtnStart.Show = true;
        CardOptiFine.IsSwapped = true;
        CardLiteLoader.IsSwapped = true;
        CardForge.IsSwapped = true;
        CardNeoForge.IsSwapped = true;
        CardCleanroom.IsSwapped = true;
        CardFabric.IsSwapped = true;
        CardFabricApi.IsSwapped = true;
        CardOptiFabric.IsSwapped = true;
        CardLabyMod.IsSwapped = true;

        if (!States.Hint.InstallPageBack)
        {
            States.Hint.InstallPageBack = true;
            HintService.Show(Hints.Info(Lang.Text("Download.Install.Hint.MinecraftBack")));
        }

        // 如果在选择页面按了刷新键，选择页的东西可能会由于动画被隐藏，但不会由于加载结束而再次显示，因此这里需要手动恢复
        foreach (var control in GetAllAnimControls(PanSelect))
        {
            control.Opacity = 1d;
            if (control.RenderTransform is null || control.RenderTransform is TranslateTransform)
                control.RenderTransform = new TranslateTransform();
        }

        // 启动 Forge 加载
        if (McInstanceInfo.IsFormatFit(_vanillaName))
        {
            var forgeLoader =
                new Loading.LoaderTask<string, List<GameDownload.DlForgeVersionEntry>>(
                    "DlForgeVersion " + _vanillaName, GameDownload.DlForgeVersionMain);
            LoadForge.State = forgeLoader;
            forgeLoader.Start(_vanillaName);
        }

        // 启动 Fabric API、Legacy Fabric API、OptiFabric、LabyMod 加载
        GameDownload.DlFabricApiLoader.Start();
        GameDownload.DlLegacyFabricApiLoader.Start();
        GameDownload.DlOptiFabricLoader.Start();
        GameDownload.DlLabyModListLoader.Start();

        Animations.AniStart(new[]
        {
            Animations.AaOpacity(PanMinecraft, -PanMinecraft.Opacity, 70, 10),
            Animations.AaTranslateX(PanMinecraft, -50 - ((TranslateTransform)PanMinecraft.RenderTransform).X, 90, 10),
            Animations.AaCode(() =>
            {
                PanBack.ScrollToHome();
                TextSelectName.Validate();
                OptiFine_Loaded();
                LiteLoader_Loaded();
                Forge_Loaded();
                NeoForge_Loaded();
                Cleanroom_Loaded();
                Fabric_Loaded();
                LegacyFabric_Loaded();
                FabricApi_Loaded();
                LegacyFabricApi_Loaded();
                OptiFabric_Loaded();
                LabyMod_Loaded();
                ReloadSelected();
                PanMinecraft.Visibility = Visibility.Collapsed;
            }, after: true),
            Animations.AaOpacity(PanSelect, 1d - PanSelect.Opacity, 70, 100),
            Animations.AaTranslateX(PanSelect, -((TranslateTransform)PanSelect.RenderTransform).X, 160, 100,
                new Animations.AniEaseOutFluent(Animations.AniEasePower.ExtraStrong)),
            Animations.AaCode(() =>
            {
                _vm.PanBackHitTestVisible = true;
                // 初始化 Binding
                if (isFirstLoaded)
                    return;
                isFirstLoaded = true;
                BtnOptiFineClearInner.SetBinding(Shape.FillProperty,
                    new Binding("Foreground") { Source = CardOptiFine.MainTextBlock, Mode = BindingMode.OneWay });
                BtnLiteLoaderClearInner.SetBinding(Shape.FillProperty,
                    new Binding("Foreground") { Source = CardLiteLoader.MainTextBlock, Mode = BindingMode.OneWay });
                BtnForgeClearInner.SetBinding(Shape.FillProperty,
                    new Binding("Foreground") { Source = CardForge.MainTextBlock, Mode = BindingMode.OneWay });
                BtnNeoForgeClearInner.SetBinding(Shape.FillProperty,
                    new Binding("Foreground") { Source = CardNeoForge.MainTextBlock, Mode = BindingMode.OneWay });
                BtnCleanroomClearInner.SetBinding(Shape.FillProperty,
                    new Binding("Foreground") { Source = CardCleanroom.MainTextBlock, Mode = BindingMode.OneWay });
                BtnFabricClearInner.SetBinding(Shape.FillProperty,
                    new Binding("Foreground") { Source = CardFabric.MainTextBlock, Mode = BindingMode.OneWay });
                BtnLegacyFabricClearInner.SetBinding(Shape.FillProperty,
                    new Binding("Foreground") { Source = CardLegacyFabric.MainTextBlock, Mode = BindingMode.OneWay });
                BtnFabricApiClearInner.SetBinding(Shape.FillProperty,
                    new Binding("Foreground") { Source = CardFabricApi.MainTextBlock, Mode = BindingMode.OneWay });
                BtnLegacyFabricApiClearInner.SetBinding(Shape.FillProperty,
                    new Binding("Foreground")
                        { Source = CardLegacyFabricApi.MainTextBlock, Mode = BindingMode.OneWay });
                BtnLabyModClearInner.SetBinding(Shape.FillProperty,
                    new Binding("Foreground") { Source = CardLabyMod.MainTextBlock, Mode = BindingMode.OneWay });
                BtnOptiFabricClearInner.SetBinding(Shape.FillProperty,
                    new Binding("Foreground") { Source = CardOptiFabric.MainTextBlock, Mode = BindingMode.OneWay });
            }, after: true)
        }, "FrmDownloadInstall SelectPageSwitch", true);
    }

    public void ExitSelectPage()
    {
        if (!isInSelectPage)
            return;
        isInSelectPage = false;

        _vm.PanInnerMargin = new Thickness(25d, 10d, 25d, 25d);

        disabledPageAnimControls.Add(BtnStart);
        BtnStart.Show = false;
        ClearSelected(); // 清除已选择项
        PanMinecraft.Visibility = Visibility.Visible;
        _vm.PanSelectHitTestVisible = false;
        _vm.PanMinecraftHitTestVisible = true;
        _vm.PanBackHitTestVisible = false;
        PanBack.ScrollToHome();

        Animations.AniStart(new[]
        {
            Animations.AaOpacity(PanSelect, -PanSelect.Opacity, 70, 10),
            Animations.AaTranslateX(PanSelect, 50d - ((TranslateTransform)PanSelect.RenderTransform).X, 90, 10),
            Animations.AaCode(() => PanBack.ScrollToHome(), after: true),
            Animations.AaOpacity(PanMinecraft, 1d - PanMinecraft.Opacity, 70, 100),
            Animations.AaTranslateX(PanMinecraft, -((TranslateTransform)PanMinecraft.RenderTransform).X, 160, 100,
                new Animations.AniEaseOutFluent(Animations.AniEasePower.ExtraStrong)),
            Animations.AaCode(() =>
            {
                PanSelect.Visibility = Visibility.Collapsed;
                _vm.PanBackHitTestVisible = true;
            }, after: true)
        }, "FrmDownloadInstall SelectPageSwitch");
    }

    public void MinecraftSelected(MyListItem sender, MouseButtonEventArgs e)
    {
        _vanillaName = sender.Title;
        _vanillaData = (JsonObject)(dynamic)sender.Tag;
        _vanillaIcon = sender.Logo;
        EnterSelectPage();
    }

    #endregion

    #region 选择

    // Minecraft
    private string? _vanillaName;
    private JsonObject? _vanillaData;
    private string? _vanillaIcon;
    private int VanillaDrop => McInstanceInfo.VersionToDrop(_vanillaName, true);

    // OptiFine
    private GameDownload.DlOptiFineListEntry? selectedOptiFine;

    /// <summary>
    ///     选定的主 Mod 加载器。
    /// </summary>
    private ModLoaderType? _selectedLoaderType;

    /// <summary>
    ///     选定的 Mod Loader API 名称，内容应为 Fabric API
    /// </summary>
    private string? selectedAPIName;

    // LiteLoader
    private GameDownload.DlLiteLoaderListEntry? selectedLiteLoader;

    // Forge
    private GameDownload.DlForgeVersionEntry? selectedForge;

    // Cleanroom
    private GameDownload.DlCleanroomListEntry? selectedCleanroom;

    // NeoForge
    private GameDownload.DlNeoForgeListEntry? selectedNeoForge;

    // Fabric
    private string? selectedFabric;

    // FabricApi
    private Comp.CompFile? selectedFabricApi;

    // LegacyFabric
    private string? selectedLegacyFabric;

    // Legacy FabricApi
    private Comp.CompFile? selectedLegacyFabricApi;

    // LabyMod
    private string? selectedLabyModChannel;
    private string? selectedLabyModCommitRef;
    private string? selectedLabyModVersion;
    private string? selectedLabyModBaseVersion;

    // OptiFabric
    private Comp.CompFile? selectedOptiFabric;

    private bool _ReloadSelected_Ongoing; // #3742 中，LoadOptiFineGetError 会初始化 LoadOptiFine，触发事件 LoadOptiFine.StateChanged，导致再次调用 SelectReload

    /// <summary>
    ///     重载已选择的项目的显示。
    /// </summary>
    private void ReloadSelected()
    {
        if (_vanillaName is null || _ReloadSelected_Ongoing)
            return;
        _ReloadSelected_Ongoing = true;
        // 主预览
        try
        {
        SelectNameUpdate();
        ImgLogo.Source = GetSelectLogo();
        // OptiFine
        if (!McVersionComparer.CompareVersionGe(_vanillaName, "1.7.2"))
        {
            _vm.ShowOptiFineCard = false;
        }
        else
        {
            _vm.ShowOptiFineCard = true;
            var optiFineError = LoadOptiFineGetError();
            CardOptiFine.MainSwap.Visibility = optiFineError is null ? Visibility.Visible : Visibility.Collapsed;
            if (optiFineError is not null)
                CardOptiFine.IsSwapped = true;
            SetPanelVisibility(PanOptiFineInfo, CardOptiFine.IsSwapped);
            if (selectedOptiFine is null)
            {
                _vm.ShowOptiFineClear = false;
                _vm.ShowOptiFineImage = false;
                _vm.OptiFineText = optiFineError ?? Lang.Text("Download.Install.State.CanAdd");
                _vm.OptiFineForeground = ThemeManager.colorGray4;
            }
            else
            {
                _vm.ShowOptiFineClear = true;
                _vm.ShowOptiFineImage = true;
                _vm.OptiFineText = selectedOptiFine.DisplayName.Replace(_vanillaName + " ", "");
                _vm.OptiFineForeground = ThemeManager.colorGray1;
            }
        }

        // LiteLoader
        if (!McInstanceInfo.IsFormatFit(_vanillaName)
            || !McVersionComparer.CompareVersionGe(_vanillaName, "1.5.2")
            || !McVersionComparer.CompareVersionGe("1.12.2", _vanillaName))
        {
            _vm.ShowLiteLoaderCard = false;
        }
        else
        {
            _vm.ShowLiteLoaderCard = true;
            var liteLoaderError = LoadLiteLoaderGetError();
            CardLiteLoader.MainSwap.Visibility = liteLoaderError is null ? Visibility.Visible : Visibility.Collapsed;
            if (liteLoaderError is not null)
                CardLiteLoader.IsSwapped = true; // 例如在同时展开卡片时选择了不兼容项则强制折叠
            SetPanelVisibility(PanLiteLoaderInfo, CardLiteLoader.IsSwapped);
            if (selectedLiteLoader is null)
            {
                _vm.ShowLiteLoaderClear = false;
                _vm.ShowLiteLoaderImage = false;
                _vm.LiteLoaderText = liteLoaderError ?? Lang.Text("Download.Install.State.CanAdd");
                _vm.LiteLoaderForeground = ThemeManager.colorGray4;
            }
            else
            {
                _vm.ShowLiteLoaderClear = true;
                _vm.ShowLiteLoaderImage = true;
                _vm.LiteLoaderText = selectedLiteLoader.Inherit;
                _vm.LiteLoaderForeground = ThemeManager.colorGray1;
            }
        }

        // Forge
        if (!McInstanceInfo.IsFormatFit(_vanillaName)
            || !McVersionComparer.CompareVersionGe(_vanillaName, "1.1"))
        {
            _vm.ShowForgeCard = false;
        }
        else
        {
            _vm.ShowForgeCard = true;
            var forgeError = LoadForgeGetError();
            CardForge.MainSwap.Visibility = forgeError is null ? Visibility.Visible : Visibility.Collapsed;
            if (forgeError is not null)
                CardForge.IsSwapped = true;
            SetPanelVisibility(PanForgeInfo, CardForge.IsSwapped);
            if (selectedForge is null)
            {
                _vm.ShowForgeClear = false;
                _vm.ShowForgeImage = false;
                _vm.ForgeText = forgeError ?? Lang.Text("Download.Install.State.CanAdd");
                _vm.ForgeForeground = ThemeManager.colorGray4;
            }
            else
            {
                _vm.ShowForgeClear = true;
                _vm.ShowForgeImage = true;
                _vm.ForgeText = selectedForge.VersionName;
                _vm.ForgeForeground = ThemeManager.colorGray1;
            }
        }

        // Cleanroom
        if (_vanillaName == "1.12.2")
        {
            _vm.ShowCleanroomCard = true;
            var cleanroomError = LoadCleanroomGetError();
            CardCleanroom.MainSwap.Visibility = cleanroomError is null ? Visibility.Visible : Visibility.Collapsed;
            if (cleanroomError is not null)
                CardCleanroom.IsSwapped = true;
            SetPanelVisibility(PanCleanroomInfo, CardCleanroom.IsSwapped);
            if (selectedCleanroom is null)
            {
                _vm.ShowCleanroomClear = false;
                _vm.ShowCleanroomImage = false;
                _vm.CleanroomText = cleanroomError ?? Lang.Text("Download.Install.State.CanAdd");
                _vm.CleanroomForeground = ThemeManager.colorGray4;
            }
            else
            {
                _vm.ShowCleanroomClear = true;
                _vm.ShowCleanroomImage = true;
                _vm.CleanroomText = selectedCleanroom.VersionName;
                _vm.CleanroomForeground = ThemeManager.colorGray1;
            }
        }
        else
        {
            _vm.ShowCleanroomCard = false;
        }

        // NeoForge
        if (!McVersionComparer.CompareVersionGe(_vanillaName, "1.20.1"))
        {
            _vm.ShowNeoForgeCard = false;
        }
        else
        {
            _vm.ShowNeoForgeCard = true;
            var neoForgeError = LoadNeoForgeGetError();
            CardNeoForge.MainSwap.Visibility = neoForgeError is null ? Visibility.Visible : Visibility.Collapsed;
            if (neoForgeError is not null)
                CardNeoForge.IsSwapped = true;
            SetPanelVisibility(PanNeoForgeInfo, CardNeoForge.IsSwapped);
            if (selectedNeoForge is null)
            {
                _vm.ShowNeoForgeClear = false;
                _vm.ShowNeoForgeImage = false;
                _vm.NeoForgeText = neoForgeError ?? Lang.Text("Download.Install.State.CanAdd");
                _vm.NeoForgeForeground = ThemeManager.colorGray4;
            }
            else
            {
                _vm.ShowNeoForgeClear = true;
                _vm.ShowNeoForgeImage = true;
                _vm.NeoForgeText = selectedNeoForge.VersionName;
                _vm.NeoForgeForeground = ThemeManager.colorGray1;
            }
        }

        // Fabric
        if (VanillaDrop < 130
            || (VanillaDrop == 130 && !McVersionComparer.CompareVersionGe(_vanillaName, "18w43b")))
        {
            _vm.ShowFabricCard = false;
        }
        else
        {
            _vm.ShowFabricCard = true;
            var fabricError = LoadFabricGetError();
            CardFabric.MainSwap.Visibility = fabricError is null ? Visibility.Visible : Visibility.Collapsed;
            if (fabricError is not null)
                CardFabric.IsSwapped = true;
            SetPanelVisibility(PanFabricInfo, CardFabric.IsSwapped);
            if (selectedFabric is null)
            {
                _vm.ShowFabricClear = false;
                _vm.ShowFabricImage = false;
                _vm.FabricText = fabricError ?? Lang.Text("Download.Install.State.CanAdd");
                _vm.FabricForeground = ThemeManager.colorGray4;
            }
            else
            {
                _vm.ShowFabricClear = true;
                _vm.ShowFabricImage = true;
                _vm.FabricText = selectedFabric.Replace("+build", "");
                _vm.FabricForeground = ThemeManager.colorGray1;
            }
        }

        // FabricApi
        if (selectedFabric is null)
        {
            _vm.ShowFabricApiCard = false;
        }
        else
        {
            _vm.ShowFabricApiCard = true;
            var fabricApiError = LoadFabricApiGetError();
            CardFabricApi.MainSwap.Visibility = fabricApiError is null ? Visibility.Visible : Visibility.Collapsed;
            if (fabricApiError is not null || selectedFabric is null)
                CardFabricApi.IsSwapped = true;
            SetPanelVisibility(PanFabricApiInfo, CardFabricApi.IsSwapped);
            if (selectedFabricApi is null)
            {
                _vm.ShowFabricApiClear = false;
                _vm.ShowFabricApiImage = false;
                _vm.FabricApiText = fabricApiError ?? Lang.Text("Download.Install.State.CanAdd");
                _vm.FabricApiForeground = ThemeManager.colorGray4;
            }
            else
            {
                _vm.ShowFabricApiClear = true;
                _vm.ShowFabricApiImage = true;
                _vm.FabricApiText = selectedFabricApi.DisplayName.Split("]")[1].Replace("Fabric API ", "")
                    .Replace(" build ", ".").Trim();
                _vm.FabricApiForeground = ThemeManager.colorGray1;
            }
        }

        // LegacyFabric
        if (VanillaDrop < 30 || VanillaDrop > 130)
        {
            _vm.ShowLegacyFabricCard = false;
        }
        else
        {
            _vm.ShowLegacyFabricCard = true;
            var legacyFabricError = LoadLegacyFabricGetError();
            CardLegacyFabric.MainSwap.Visibility =
                legacyFabricError is null ? Visibility.Visible : Visibility.Collapsed;
            if (legacyFabricError is not null)
                CardLegacyFabric.IsSwapped = true;
            SetPanelVisibility(PanLegacyFabricInfo, CardLegacyFabric.IsSwapped);
            if (selectedLegacyFabric is null)
            {
                _vm.ShowLegacyFabricClear = false;
                _vm.ShowLegacyFabricImage = false;
                _vm.LegacyFabricText = legacyFabricError ?? Lang.Text("Download.Install.State.CanAdd");
                _vm.LegacyFabricForeground = ThemeManager.colorGray4;
            }
            else
            {
                _vm.ShowLegacyFabricClear = true;
                _vm.ShowLegacyFabricImage = true;
                _vm.LegacyFabricText = selectedLegacyFabric.Replace("+build", "");
                _vm.LegacyFabricForeground = ThemeManager.colorGray1;
            }
        }

        // LegacyFabricApi
        if (selectedLegacyFabric is null)
        {
            _vm.ShowLegacyFabricApiCard = false;
        }
        else
        {
            _vm.ShowLegacyFabricApiCard = true;
            var legacyFabricApiError = LoadLegacyFabricApiGetError();
            CardLegacyFabricApi.MainSwap.Visibility =
                legacyFabricApiError is null ? Visibility.Visible : Visibility.Collapsed;
            if (legacyFabricApiError is not null || selectedLegacyFabric is null)
                CardLegacyFabricApi.IsSwapped = true;
            SetPanelVisibility(PanLegacyFabricApiInfo, CardLegacyFabricApi.IsSwapped);
            if (selectedLegacyFabricApi is null)
            {
                _vm.ShowLegacyFabricApiClear = false;
                _vm.ShowLegacyFabricApiImage = false;
                _vm.LegacyFabricApiText = legacyFabricApiError ?? Lang.Text("Download.Install.State.CanAdd");
                _vm.LegacyFabricApiForeground = ThemeManager.colorGray4;
            }
            else
            {
                _vm.ShowLegacyFabricApiClear = true;
                _vm.ShowLegacyFabricApiImage = true;
                _vm.LegacyFabricApiText = selectedLegacyFabricApi.DisplayName.Replace("Legacy Fabric API ", "");
                _vm.LegacyFabricApiForeground = ThemeManager.colorGray1;
            }
        }

        // LabyMod
        if (!McInstanceInfo.IsFormatFit(_vanillaName)
            || !McVersionComparer.CompareVersionGe(_vanillaName, "1.8.9"))
        {
            _vm.ShowLabyModCard = false;
        }
        else
        {
            _vm.ShowLabyModCard = true;
            var labyModError = LoadLabyModGetError();
            CardLabyMod.MainSwap.Visibility = labyModError is null ? Visibility.Visible : Visibility.Collapsed;
            if (labyModError is not null)
                CardLabyMod.IsSwapped = true;
            SetPanelVisibility(PanLabyModInfo, CardLabyMod.IsSwapped);
            if (selectedLabyModVersion is null)
            {
                _vm.ShowLabyModClear = false;
                _vm.ShowLabyModImage = false;
                _vm.LabyModText = labyModError ?? Lang.Text("Download.Install.State.CanAdd");
                _vm.LabyModForeground = ThemeManager.colorGray4;
            }
            else
            {
                _vm.ShowLabyModClear = true;
                _vm.ShowLabyModImage = true;
                _vm.LabyModText = selectedLabyModVersion;
                _vm.LabyModForeground = ThemeManager.colorGray1;
            }
        }

        // OptiFabric
        if (selectedFabric is null || selectedOptiFine is null)
        {
            _vm.ShowOptiFabricCard = false;
        }
        else
        {
            _vm.ShowOptiFabricCard = true;
            var optiFabricError = LoadOptiFabricGetError();
            CardOptiFabric.MainSwap.Visibility = optiFabricError is null ? Visibility.Visible : Visibility.Collapsed;
            if (optiFabricError is not null || selectedFabric is null)
                CardOptiFabric.IsSwapped = true;
            SetPanelVisibility(PanOptiFabricInfo, CardOptiFabric.IsSwapped);
            if (selectedOptiFabric is null)
            {
                _vm.ShowOptiFabricClear = false;
                _vm.ShowOptiFabricImage = false;
                _vm.OptiFabricText = optiFabricError ?? Lang.Text("Download.Install.State.CanAdd");
                _vm.OptiFabricForeground = ThemeManager.colorGray4;
            }
            else
            {
                _vm.ShowOptiFabricClear = true;
                _vm.ShowOptiFabricImage = true;
                _vm.OptiFabricText = selectedOptiFabric.DisplayName.ToLower().Replace("optifabric-", "")
                    .Replace(".jar", "").Trim().TrimStart('v');
                _vm.OptiFabricForeground = ThemeManager.colorGray1;
            }
        }

        // 主警告
        if (selectedFabric is not null && selectedFabricApi is null)
            _vm.ShowHintFabricAPI = true;
        else
            _vm.ShowHintFabricAPI = false;
        if (selectedLegacyFabric is not null && selectedLegacyFabricApi is null)
            _vm.ShowHintLegacyFabricAPI = true;
        else
            _vm.ShowHintLegacyFabricAPI = false;

        if ((selectedFabric is not null || selectedLegacyFabric is not null) && selectedOptiFine is not null &&
            selectedOptiFabric is null)
        {
            if (VanillaDrop >= 140 && VanillaDrop <= 150)
            {
                _vm.ShowHintOptiFabric = false;
                _vm.ShowHintLegacyOptiFabric = false;
                _vm.ShowHintOptiFabricOld = true;
            }
            else if (selectedLegacyFabric is not null)
            {
                _vm.ShowHintOptiFabric = false;
                _vm.ShowHintLegacyOptiFabric = true;
                _vm.ShowHintOptiFabricOld = false;
            }
            else
            {
                _vm.ShowHintOptiFabric = true;
                _vm.ShowHintOptiFabricOld = false;
                _vm.ShowHintLegacyOptiFabric = false;
            }
        }
        else
        {
            _vm.ShowHintOptiFabric = false;
            _vm.ShowHintOptiFabricOld = false;
            _vm.ShowHintLegacyOptiFabric = false;
        }

        if (VanillaDrop >= 160 && selectedOptiFine is not null &&
            (selectedForge is not null || selectedFabric is not null))
            _vm.ShowHintModOptiFine = true;
        else
            _vm.ShowHintModOptiFine = false;
        // 结束
        }
        finally
        {
            _ReloadSelected_Ongoing = false;
        }
    }

    /// <summary>
    ///     清空已选择的项目。
    /// </summary>
    private void ClearSelected()
    {
        _vanillaName = null;
        _vanillaData = null;
        _vanillaIcon = null;
        selectedOptiFine = null;
        selectedLiteLoader = null;
        _selectedLoaderType = null;
        selectedAPIName = null;
        selectedForge = null;
        selectedNeoForge = null;
        selectedCleanroom = null;
        selectedFabric = null;
        selectedFabricApi = null;
        selectedOptiFabric = null;
        selectedLabyModCommitRef = null;
        selectedLabyModVersion = null;
        selectedLabyModBaseVersion = null;
        selectedLabyModChannel = null;
        selectedLegacyFabric = null;
        selectedLegacyFabricApi = null;
    }

    /// <summary>
    ///     选择主 Mod 加载器时，清除其它主加载器及其专属组件。
    ///     OptiFine 与 LiteLoader 可与部分加载器共存，故不在此处清除。
    /// </summary>
    private void SelectModLoader(ModLoaderType loaderType)
    {
        if (loaderType != ModLoaderType.Forge)
            selectedForge = null;

        if (loaderType != ModLoaderType.NeoForge)
            selectedNeoForge = null;

        if (loaderType != ModLoaderType.Cleanroom)
            selectedCleanroom = null;

        if (loaderType != ModLoaderType.Fabric)
        {
            selectedFabric = null;
            selectedFabricApi = null;
            selectedOptiFabric = null;
            autoSelectedFabricApi = false;
            autoSelectedOptiFabric = false;
        }

        if (loaderType != ModLoaderType.LegacyFabric)
        {
            selectedLegacyFabric = null;
            selectedLegacyFabricApi = null;
            autoSelectedLegacyFabricApi = false;
        }

        if (loaderType != ModLoaderType.LabyMod)
        {
            selectedLabyModChannel = null;
            selectedLabyModCommitRef = null;
            selectedLabyModBaseVersion = null;
            selectedLabyModVersion = null;
        }

        _selectedLoaderType = loaderType;
        selectedAPIName = null;
    }

    // 信息栏动画
    private void SetPanelVisibility(Grid panel, bool visible)
    {
        if (Equals(panel.Tag, visible.ToString()))
            return;
        panel.Tag = visible.ToString();
        if (visible)
            Animations.AniStart(
                new[]
                {
                    Animations.AaTranslateY(panel, -((TranslateTransform)panel.RenderTransform).Y, 150,
                        ease: new Animations.AniEaseOutFluent()),
                    Animations.AaOpacity(panel, 1d - panel.Opacity, 60)
                }, "PageDownloadInstall Visibility " + panel.Name);
        else
            Animations.AniStart(
                new[]
                {
                    Animations.AaTranslateY(panel, 6d - ((TranslateTransform)panel.RenderTransform).Y, 60),
                    Animations.AaOpacity(panel, -panel.Opacity, 60)
                }, "PageDownloadInstall Visibility " + panel.Name);
    }

    /// <summary>
    ///     获取实例图标。
    /// </summary>
    private string GetSelectLogo()
    {
        if (selectedFabric is not null) return "pack://application:,,,/images/Blocks/Fabric.png";

        if (selectedLegacyFabric is not null) return "pack://application:,,,/images/Blocks/Fabric.png";

        if (selectedForge is not null) return "pack://application:,,,/images/Blocks/Anvil.png";

        if (selectedNeoForge is not null) return "pack://application:,,,/images/Blocks/NeoForge.png";

        if (selectedLiteLoader is not null) return "pack://application:,,,/images/Blocks/Egg.png";

        if (selectedOptiFine is not null) return "pack://application:,,,/images/Blocks/GrassPath.png";

        if (selectedCleanroom is not null) return "pack://application:,,,/images/Blocks/Cleanroom.png";

        if (selectedLabyModVersion is not null) return "pack://application:,,,/images/Blocks/LabyMod.png";

        return _vanillaIcon;
    }

    // 实例名处理
    /// <summary>
    ///     获取默认实例名。
    /// </summary>
    private string GetSelectName()
    {
        var name = _vanillaName;
        if (selectedFabric is not null) name += "-Fabric_" + selectedFabric.Replace("+build", "");
        if (selectedLegacyFabric is not null) name += "-LegacyFabric_" + selectedLegacyFabric;
        if (selectedLabyModVersion is not null)
            name += "-LabyMod_" + selectedLabyModBaseVersion + (selectedLabyModChannel == "snapshot" ? "_Snapshot" : "_Production");
        if (selectedForge is not null) name += "-Forge_" + selectedForge.VersionName;
        if (selectedNeoForge is not null) name += "-NeoForge_" + selectedNeoForge.VersionName;
        if (selectedCleanroom is not null) name += "-Cleanroom_" + selectedCleanroom.VersionName;
        if (selectedLiteLoader is not null) name += "-LiteLoader";
        if (selectedOptiFine is not null)
            name += "-OptiFine_" + selectedOptiFine.DisplayName.Replace(_vanillaName + " ", "").Replace(" ", "_");
        return name;
    }

    private bool isSelectNameEdited;
    private bool isSelectNameChanging;

    private void SelectNameUpdate()
    {
        if (isSelectNameEdited || isSelectNameChanging)
            return;
        isSelectNameChanging = true;
        _vm.InstanceName = GetSelectName();
        isSelectNameChanging = false;
    }

    private void TextSelectName_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (isSelectNameChanging)
            return;
        isSelectNameEdited = true;
        ReloadSelected();
    }

    private void TextSelectName_ValidateChanged(object sender, EventArgs e)
    {
        _vm.CanStartInstall = TextSelectName.IsValidated;
    }

    #endregion

    #region 加载器

    // 结果数据化
    private void LoadMinecraft_OnFinish()
    {
        ExitSelectPage(); // 返回

        try
        {
            if (GameDownload.dlClientListLoader.output.Value["versions"] is not JsonArray versions)
                return;

            _RebuildVersionCards(versions);

            if (mcVersionWaitingForSelect is null) return;

            Common.Log("[Download] 自动选择 MC 版本：" + mcVersionWaitingForSelect);

            foreach (JsonObject version1 in versions)
            {
                if (((string)version1["id"] ?? "") != mcVersionWaitingForSelect)
                    continue;

                var item = DownloadUi.McDownloadListItem(version1, (_, _) => { }, false);
                MinecraftSelected(item, null);
                break;
            }
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "可视化安装版本列表出错",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Download.Install.Error.OperationFailed"));
        }
    }

    private void _RebuildVersionCards(JsonArray versions)
    {
        var categoryOrder = new[]
        {
            McVersionCategory.Release,
            McVersionCategory.Snapshot,
            McVersionCategory.BeforeRelease,
            McVersionCategory.AprilFools
        };

        var dict = categoryOrder.ToDictionary(
            category => category,
            _ => new List<JsonObject>()
        );

        foreach (JsonObject version in versions)
        {
            var category = McVersionClassifier.ClassifyVersion(version);
            dict[category].Add(version);
        }

        foreach (var category in categoryOrder)
            dict[category] = dict[category]
                .OrderByDescending(McVersionClassifier.GetReleaseTime)
                .ToList();

        PanMinecraft.Children.Clear();

        _AddLatestVersionCard(dict);
        _AddCategoryCards(dict, categoryOrder);
    }

    private void _OnLanguageChanged() => Common.RunInUi(() =>
    {
        _vm.LoadingText = Lang.Text("Download.Version.LoadingList");
        if (GameDownload.dlClientListLoader.output.Value?["versions"] is JsonArray versions)
            _RebuildVersionCards(versions);
    });

    private void _AddLatestVersionCard(Dictionary<McVersionCategory, List<JsonObject>> dict)
    {
        var latestRelease = dict[McVersionCategory.Release].FirstOrDefault();
        var latestSnapshot = dict[McVersionCategory.Snapshot].FirstOrDefault();

        var latestVersions = new List<JsonObject>();

        if (latestRelease is not null)
        {
            var release = (JsonObject)latestRelease.DeepClone();
            release["lore"] = Lang.Text(
                "Download.Version.Latest.Release",
                Lang.Date(McVersionClassifier.GetReleaseTime(release), "g")
            );
            latestVersions.Add(release);
        }

        if (latestSnapshot is not null &&
            (latestRelease is null || McVersionClassifier.GetReleaseTime(latestRelease) <
                McVersionClassifier.GetReleaseTime(latestSnapshot)))
        {
            var snapshot = (JsonObject)latestSnapshot.DeepClone();
            snapshot["lore"] = Lang.Text(
                "Download.Version.Latest.Development",
                Lang.Date(McVersionClassifier.GetReleaseTime(snapshot), "g")
            );
            latestVersions.Add(snapshot);
        }

        if (latestVersions.Count == 0)
            return;

        var cardInfo = new MyCard
        {
            Title = Lang.Text("Download.Version.Latest.Title"),
            Margin = new Thickness(0d, 15d, 0d, 15d)
        };

        var panInfo = _CreateVersionStack(latestVersions);
        MyCard.StackInstall(ref panInfo, _StackInstall);

        cardInfo.Children.Add(panInfo);
        PanMinecraft.Children.Insert(0, cardInfo);
    }

    private void _AddCategoryCards(
        Dictionary<McVersionCategory, List<JsonObject>> dict,
        IEnumerable<McVersionCategory> categoryOrder)
    {
        foreach (var category in categoryOrder)
        {
            var versions = dict[category];
            if (versions.Count == 0)
                continue;

            var card = new MyCard
            {
                Title = $"{Lang.Text(McVersionClassifier.GetCategoryKey(category))} ({versions.Count})",
                Margin = new Thickness(0d, 0d, 0d, 15d)
            };

            var stack = _CreateVersionStack(versions);

            card.Children.Add(stack);
            card.SwapControl = stack;

            // 不能使用 AddressOf，这导致了 #535，原因完全不明，疑似是编译器 Bug
            card.InstallMethod = _StackInstall;
            card.IsSwapped = true;

            PanMinecraft.Children.Add(card);
        }
    }

    private static StackPanel _CreateVersionStack(IEnumerable<JsonObject> versions)
    {
        return new StackPanel
        {
            Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d),
            VerticalAlignment = VerticalAlignment.Top,
            RenderTransform = new TranslateTransform(0d, 0d),
            Tag = versions
        };
    }

    private static void _StackInstall(StackPanel stack)
    {
        foreach (var item in (IEnumerable)stack.Tag)
            stack.Children.Add(
                DownloadUi.McDownloadListItem(
                    (JsonObject)item,
                    (sender, e) => Launcher.frmDownloadInstall.MinecraftSelected((MyListItem)sender, e),
                    false
                )
            );
    }

    /// <summary>
    ///     当 MC 版本列表加载完时，立即自动选择的版本。用于外部调用。
    /// </summary>
    public static string? mcVersionWaitingForSelect = null;

    #endregion

    #region OptiFine 列表

    /// <summary>
    ///     获取 OptiFine 的加载异常信息。若正常则返回 Nothing。
    /// </summary>
    private string LoadOptiFineGetError()
    {
        if (_selectedLoaderType is ModLoaderType.NeoForge or ModLoaderType.LabyMod or ModLoaderType.Cleanroom)
            return Lang.Text("Download.Install.Compat.IncompatibleWithLoader", _selectedLoaderType);
        if (LoadOptiFine is null || LoadOptiFine.State.LoadingState == MyLoading.MyLoadingState.Run)
            return Lang.Text("Download.Install.State.Loading");
        if (LoadOptiFine.State.LoadingState == MyLoading.MyLoadingState.Error)
            return $"{Lang.Text("Download.Install.State.GetVersionListFailed")}{((Loading.LoaderBase)LoadOptiFine.State).Error.Message}";
        // 检查 Forge 1.13 - 1.14.3：全部不兼容
        if (_selectedLoaderType == ModLoaderType.Forge && McVersionComparer.CompareVersion(_vanillaName, "1.13") >= 0 &&
            McVersionComparer.CompareVersion("1.14.3", _vanillaName) >= 0) return Lang.Text("Download.Install.Compat.IncompatibleWithLoader", _selectedLoaderType);
        // 检查 Fabric 1.20.5+: 全部不兼容
        if (selectedFabric is not null && McVersionComparer.CompareVersion(_vanillaName, "1.20.4") > 0)
            return Lang.Text("Download.Install.Compat.IncompatibleWithLoader", _selectedLoaderType);
        // 检查 Loader
        if (GetLoaderError(LoadOptiFine) is not null)
            return GetLoaderError(LoadOptiFine);
        // 检查 Forge 版本
        var hasAny = false;
        var hasRequiredVersion = false;
        foreach (var OptiFineVersion in GameDownload.DlOptiFineListLoader.output.Value)
        {
            if (!OptiFineVersion.DisplayName.StartsWith(_vanillaName + " "))
                continue; // 不是同一个大版本
            hasAny = true;
            if (selectedForge is null)
                return null; // 未选择 Forge
            if ((bool)IsOptiFineSuitForForge(OptiFineVersion, selectedForge))
                return null; // 该版本可用
            if (OptiFineVersion.RequiredForgeVersion is not null)
                hasRequiredVersion = true;
        }

        if (!hasAny) return Lang.Text("Download.Install.State.NoVersion");

        if (hasRequiredVersion) return Lang.Text("Download.Install.Compat.CompatForgeSpecificOnly");

        return Lang.Text("Download.Install.Compat.IncompatibleWithLoader", _selectedLoaderType);
    }

    // 检查某个 OptiFine 是否与某个 Forge 兼容
    private object IsOptiFineSuitForForge(GameDownload.DlOptiFineListEntry optiFine,
        GameDownload.DlForgeVersionEntry forge)
    {
        if ((forge.Inherit ?? "") != (optiFine.Inherit ?? ""))
            return false; // 不是同一个大版本
        if (optiFine.RequiredForgeVersion is null)
            return false; // 不兼容 Forge
        if (string.IsNullOrWhiteSpace(optiFine.RequiredForgeVersion))
            return true; // #4183
        if (optiFine.RequiredForgeVersion.Contains(".")) // XX.X.XXX
            return McVersionComparer.CompareVersion(forge.version.ToString(), optiFine.RequiredForgeVersion) == 0;

        // XXXX
        return forge.version.Revision == Convert.ToDouble(optiFine.RequiredForgeVersion);
    }

    // 限制展开
    private void CardOptiFine_PreviewSwap(object sender, Common.RouteEventArgs e)
    {
        if (LoadOptiFineGetError() is not null)
            e.handled = true;
    }

    /// <summary>
    ///     尝试重新可视化 OptiFine 版本列表。
    /// </summary>
    private void OptiFine_Loaded()
    {
        try
        {
            if (GameDownload.DlOptiFineListLoader.State != Common.LoadState.Finished)
                return;

            // 获取版本列表
            var versions = new List<GameDownload.DlOptiFineListEntry>();
            foreach (var Version in GameDownload.DlOptiFineListLoader.output.Value)
            {
                if (selectedForge is not null &&
                                          !(bool)IsOptiFineSuitForForge(Version, selectedForge))
                    continue;
                if (Version.DisplayName.StartsWith(_vanillaName + " "))
                    versions.Add(Version);
            }

            if (!versions.Any())
                return;
            // 排序
            versions.Sort((left, right) =>
            {
                if (!left.IsPreview && right.IsPreview)
                    return true;
                if (left.IsPreview && !right.IsPreview)
                    return false;
                return McVersionComparer.CompareVersionGe(left.DisplayName, right.DisplayName);
            });
            // 可视化
            PanOptiFine.Children.Clear();
            foreach (var Version in versions)
                PanOptiFine.Children.Add(
                    DownloadUi.OptiFineDownloadListItem(Version, (a, b) => this.OptiFine_Selected((dynamic)a, b),
                        false));
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "可视化 OptiFine 安装版本列表出错",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Download.Install.Error.OperationFailed"));
        }
    }

    // 选择与清除
    private void OptiFine_Selected(MyListItem sender, EventArgs e)
    {
        selectedOptiFine = (GameDownload.DlOptiFineListEntry)(dynamic)sender.Tag;
        if (selectedForge is not null &&
                                  !(bool)IsOptiFineSuitForForge(selectedOptiFine, selectedForge))
            selectedForge = null;
        OptiFabric_Loaded();
        Forge_Loaded();
        NeoForge_Loaded();
        CardOptiFine.IsSwapped = true;
        ReloadSelected();
    }

    private void OptiFine_Clear(object sender, MouseButtonEventArgs e)
    {
        selectedOptiFine = null;
        selectedOptiFabric = null;
        autoSelectedOptiFabric = false;
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
        if (_selectedLoaderType is ModLoaderType.NeoForge or ModLoaderType.LegacyFabric or ModLoaderType.LabyMod or ModLoaderType.Cleanroom)
            return Lang.Text("Download.Install.Compat.IncompatibleWithLoader", _selectedLoaderType);
        // 检查版本
        return GameDownload.DlLiteLoaderListLoader.output.Value.Any(v => (v.Inherit ?? "") == (_vanillaName ?? ""))
            ? null
            : Lang.Text("Download.Install.State.NoVersion");
    }

    // 限制展开
    private void CardLiteLoader_PreviewSwap(object sender, Common.RouteEventArgs e)
    {
        if (LoadLiteLoaderGetError() is not null)
            e.handled = true;
    }

    /// <summary>
    ///     尝试重新可视化 LiteLoader 版本列表。
    /// </summary>
    private void LiteLoader_Loaded()
    {
        try
        {
            if (GameDownload.DlLiteLoaderListLoader.State != Common.LoadState.Finished)
                return;
            // 获取版本列表
            var versions = new List<GameDownload.DlLiteLoaderListEntry>();
            foreach (var Version in GameDownload.DlLiteLoaderListLoader.output.Value)
                if ((Version.Inherit ?? "") == (_vanillaName ?? ""))
                    versions.Add(Version);
            if (!versions.Any())
                return;
            // 可视化
            PanLiteLoader.Children.Clear();
            foreach (var Version in versions)
                PanLiteLoader.Children.Add(DownloadUi.LiteLoaderDownloadListItem(Version,
                    (a, b) => this.LiteLoader_Selected((dynamic)a, b), false));
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "可视化 LiteLoader 安装版本列表出错",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Download.Install.Error.OperationFailed"));
        }
    }

    // 选择与清除
    private void LiteLoader_Selected(MyListItem sender, EventArgs e)
    {
        selectedLiteLoader = (GameDownload.DlLiteLoaderListEntry)(dynamic)sender.Tag;
        CardLiteLoader.IsSwapped = true;
        ReloadSelected();
    }

    private void LiteLoader_Clear(object sender, MouseButtonEventArgs e)
    {
        selectedLiteLoader = null;
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
        if (McVersionComparer.CompareVersionGe("1.5.1", _vanillaName) && McVersionComparer.CompareVersionGe(_vanillaName, "1.1"))
            return Lang.Text("Download.Install.State.NoVersion");

        if (_selectedLoaderType is not null && _selectedLoaderType != ModLoaderType.Forge)
            return Lang.Text("Download.Install.Compat.IncompatibleWithLoader", _selectedLoaderType);

        // 检查 Loader
        if (GetLoaderError(LoadForge) is not null)
            return GetLoaderError(LoadForge);
        var loader = (Loading.LoaderTask<string, List<GameDownload.DlForgeVersionEntry>>)LoadForge.State;
        if ((_vanillaName ?? "") != (loader.input ?? ""))
            return Lang.Text("Download.Install.State.Getting");
        // 检查版本
        foreach (var Version in loader.output)
        {
            if (Version.Category == "universal" || Version.Category == "client")
                continue; // 跳过无法自动安装的版本
            if (selectedNeoForge is not null || selectedFabric is not null)
                return Lang.Text("Download.Install.Compat.IncompatibleWithLoader", _selectedLoaderType);
            if (selectedOptiFine is not null && McVersionComparer.CompareVersionGe(_vanillaName, "1.13") &&
                McVersionComparer.CompareVersionGe("1.14.3", _vanillaName))
                return Lang.Text("Download.Install.Compat.IncompatibleWithOptiFine"); // 1.13 ~ 1.14.3 OptiFine 检查
            if (selectedOptiFine is not null && !(bool)IsOptiFineSuitForForge(selectedOptiFine, Version))
                continue;
            return null;
        }

        return Lang.Text("Download.Install.Compat.IncompatibleWithOptiFine");
    }

    // 限制展开
    private void CardForge_PreviewSwap(object sender, Common.RouteEventArgs e)
    {
        if (LoadForgeGetError() is not null)
            e.handled = true;
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
            var loader = (Loading.LoaderTask<string, List<GameDownload.DlForgeVersionEntry>>)LoadForge.State;
            if ((_vanillaName ?? "") != (loader.input ?? ""))
                return;
            if (loader.State != Common.LoadState.Finished)
                return;
            // 获取要显示的版本
            var versions = loader.output.ToList(); // 复制数组，以免 Output 在实例化后变空
            if (!loader.output.Any())
                return;
            PanForge.Children.Clear();
            versions = versions.Where(v =>
            {
                if (v.Category == "universal" || v.Category == "client")
                    return false; // 跳过无法自动安装的版本
                if (selectedOptiFine is not null &&
                                          !(bool)IsOptiFineSuitForForge(selectedOptiFine, v))
                    return false;
                return true;
            }).OrderByDescending(v => v).ToList();
            DownloadUi.ForgeDownloadListItemPreload(PanForge, versions,
                (a, b) => this.Forge_Selected((dynamic)a, b), false);
            foreach (var Version in versions)
                PanForge.Children.Add(
                    DownloadUi.ForgeDownloadListItem(Version, (a, b) => this.Forge_Selected((dynamic)a, b), false));
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "可视化 Forge 安装版本列表出错",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Download.Install.Error.OperationFailed"));
        }
    }

    // 选择与清除
    private void Forge_Selected(MyListItem sender, EventArgs e)
    {
        SelectModLoader(ModLoaderType.Forge);
        selectedForge = (GameDownload.DlForgeVersionEntry)(dynamic)sender.Tag;
        CardForge.IsSwapped = true;
        if (selectedOptiFine is not null &&
                                  !(bool)IsOptiFineSuitForForge(selectedOptiFine, selectedForge))
            selectedOptiFine = null;
        OptiFine_Loaded();
        ReloadSelected();
    }

    private void Forge_Clear(object sender, MouseButtonEventArgs e)
    {
        selectedForge = null;
        _selectedLoaderType = null;
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
        if (selectedOptiFine is not null)
            return Lang.Text("Download.Install.Compat.IncompatibleWithOptiFine");
        if (_selectedLoaderType is not null && _selectedLoaderType != ModLoaderType.NeoForge)
            return Lang.Text("Download.Install.Compat.IncompatibleWithLoader", _selectedLoaderType);
        // 检查 Loader
        if (GetLoaderError(LoadNeoForge) is not null)
            return GetLoaderError(LoadNeoForge);
        // 检查版本
        return GameDownload.DlNeoForgeListLoader.output.Value.Any(v => (v.Inherit ?? "") == (_vanillaName ?? ""))
            ? null
            : Lang.Text("Download.Install.State.NoVersion");
    }

    // 限制展开
    private void CardNeoForge_PreviewSwap(object sender, Common.RouteEventArgs e)
    {
        if (LoadNeoForgeGetError() is not null)
            e.handled = true;
    }

    /// <summary>
    ///     尝试重新可视化 NeoForge 版本列表。
    /// </summary>
    private void NeoForge_Loaded()
    {
        try
        {
            // 获取版本列表
            if (GameDownload.DlNeoForgeListLoader.State != Common.LoadState.Finished)
                return;
            var versions = GameDownload.DlNeoForgeListLoader.output.Value
                .Where(v => (v.Inherit ?? "") == (_vanillaName ?? "")).ToList();
            if (!versions.Any())
                return;
            // 可视化
            PanNeoForge.Children.Clear();
            DownloadUi.NeoForgeDownloadListItemPreload(PanNeoForge, versions,
                (a, b) => this.NeoForge_Selected((dynamic)a, b),
                false);
            foreach (var Version in versions)
                PanNeoForge.Children.Add(
                    DownloadUi.NeoForgeDownloadListItem(Version, (a, b) => this.NeoForge_Selected((dynamic)a, b),
                        false));
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "可视化 NeoForge 安装版本列表出错",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Download.Install.Error.OperationFailed"));
        }
    }

    // 选择与清除
    private void NeoForge_Selected(MyListItem sender, EventArgs e)
    {
        SelectModLoader(ModLoaderType.NeoForge);
        selectedNeoForge = (GameDownload.DlNeoForgeListEntry)(dynamic)sender.Tag;
        CardNeoForge.IsSwapped = true;
        OptiFine_Loaded();
        ReloadSelected();
    }

    private void NeoForge_Clear(object sender, MouseButtonEventArgs e)
    {
        selectedNeoForge = null;
        _selectedLoaderType = null;
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
    private string? LoadCleanroomGetError()
    {
        if (!_vanillaName.StartsWith("1."))
            return Lang.Text("Download.Install.State.NoAvailableVersion");
        if (selectedOptiFine is not null)
            return Lang.Text("Download.Install.Compat.IncompatibleWithOptiFine");
        if (_selectedLoaderType is not null && _selectedLoaderType != ModLoaderType.Cleanroom)
            return Lang.Text("Download.Install.Compat.IncompatibleWithLoader", _selectedLoaderType);
        if (selectedLiteLoader is not null)
            return Lang.Text("Download.Install.Compat.IncompatibleWithLiteLoader");
        // 检查 Loader
        if (GetLoaderError(LoadCleanroom) is not null)
            return GetLoaderError(LoadCleanroom);
        // 检查版本
        return GameDownload.DlCleanroomListLoader.output.Value.Any(v => (v.Inherit ?? "") == (_vanillaName ?? ""))
            ? null
            : Lang.Text("Download.Install.State.NoVersion");
    }

    // 限制展开
    private void CardCleanroom_PreviewSwap(object sender, Common.RouteEventArgs e)
    {
        if (LoadCleanroomGetError() is not null)
            e.handled = true;
    }

    /// <summary>
    ///     尝试重新可视化 Cleanroom 版本列表。
    /// </summary>
    private void Cleanroom_Loaded()
    {
        try
        {
            // 获取版本列表
            if (GameDownload.DlCleanroomListLoader.State != Common.LoadState.Finished)
                return;
            var versions = GameDownload.DlCleanroomListLoader.output.Value
                .Where(v => (v.Inherit ?? "") == (_vanillaName ?? "")).ToList();
            if (!versions.Any())
                return;
            // 可视化
            PanCleanroom.Children.Clear();
            DownloadUi.CleanroomDownloadListItemPreload(PanCleanroom, versions,
                (a, b) => this.Cleanroom_Selected((dynamic)a, b), false);
            foreach (var Version in versions)
                PanCleanroom.Children.Add(
                    DownloadUi.CleanroomDownloadListItem(Version, (a, b) => this.Cleanroom_Selected((dynamic)a, b),
                        false));
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "可视化 Cleanroom 安装版本列表出错",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Download.Install.Error.OperationFailed"));
        }
    }

    // 选择与清除
    private void Cleanroom_Selected(MyListItem sender, EventArgs e)
    {
        SelectModLoader(ModLoaderType.Cleanroom);
        selectedCleanroom = (GameDownload.DlCleanroomListEntry)(dynamic)sender.Tag;
        CardCleanroom.IsSwapped = true;
        OptiFine_Loaded();
        ReloadSelected();
    }

    private void Cleanroom_Clear(object sender, MouseButtonEventArgs e)
    {
        selectedCleanroom = null;
        _selectedLoaderType = null;
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
        if (selectedOptiFine is not null && McVersionComparer.CompareVersionGe(_vanillaName, "1.20.5"))
            return Lang.Text("Download.Install.Compat.IncompatibleWithOptiFine");
        // 检查 Loader
        if (GetLoaderError(LoadFabric) is not null)
            return GetLoaderError(LoadFabric);
        // 检查版本
        foreach (JsonObject version in GameDownload.DlFabricListLoader.output.Value["game"].AsArray())
            if ((version["version"].ToString() ?? "") ==
                (_vanillaName.Replace("∞", "infinite").Replace("Combat Test 7c", "1.16_combat-3") ?? ""))
            {
                if (_selectedLoaderType is not null && _selectedLoaderType != ModLoaderType.Fabric)
                    return Lang.Text("Download.Install.Compat.IncompatibleWithLoader", _selectedLoaderType);
                return null;
            }

        return Lang.Text("Download.Install.State.NoVersion");
    }

    // 限制展开
    private void CardFabric_PreviewSwap(object sender, Common.RouteEventArgs e)
    {
        if (LoadFabricGetError() is not null)
            e.handled = true;
    }

    /// <summary>
    ///     尝试重新可视化 Fabric 版本列表。
    /// </summary>
    private void Fabric_Loaded()
    {
        try
        {
            if (GameDownload.DlFabricListLoader.State != Common.LoadState.Finished)
                return;
            // 获取版本列表
            var versions = (JsonArray)GameDownload.DlFabricListLoader.output.Value["loader"];
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
                        DownloadUi.FabricDownloadListItem((JsonObject)item,
                            (a, b) => this.Fabric_Selected((dynamic)a, b)));
            };
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "可视化 Fabric 安装版本列表出错",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Download.Install.Error.OperationFailed"));
        }
    }

    // 选择与清除
    public void Fabric_Selected(MyListItem sender, EventArgs e)
    {
        SelectModLoader(ModLoaderType.Fabric);
        Common.Log(((dynamic)sender.Tag).ToString());
        selectedFabric = ((dynamic)sender.Tag)["version"].ToString();
        FabricApi_Loaded();
        OptiFabric_Loaded();
        CardFabric.IsSwapped = true;
        ReloadSelected();
    }

    private void Fabric_Clear(object sender, MouseButtonEventArgs e)
    {
        selectedFabric = null;
        selectedFabricApi = null;
        autoSelectedFabricApi = false;
        selectedOptiFabric = null;
        autoSelectedOptiFabric = false;
        _selectedLoaderType = null;
        selectedAPIName = null;
        CardFabric.IsSwapped = true;
        e.Handled = true;
        ReloadSelected();
    }

    #endregion

    #region Fabric API 列表

    /// <summary>
    ///     判断某 Fabric API 是否适配当前选择的原版版本。
    /// </summary>
    public bool IsFabricApiCompatible(Comp.CompFile fabricApi)
    {
        var fabricApiName = fabricApi.DisplayName;
        try
        {
            if (fabricApiName is null || _vanillaName is null)
                return false;
            var targetName = _vanillaName.Replace("∞", "infinite").Replace("Combat Test 7c", "1.16_combat-3").ToLower();
            return fabricApi.RawGameVersions.Any(f => f == targetName);
        }
        catch (Exception ex)
        {
            Common.Log(ex, "判断 Fabric API 版本适配性出错（" + fabricApiName + ", " + _vanillaName + "）");
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
        if (GameDownload.DlFabricApiLoader.output is null)
            return selectedFabric is null ? Lang.Text("Download.Install.Compat.RequiresFabric") : Lang.Text("Download.Install.State.Getting");
        // 检查版本
        if (GameDownload.DlFabricApiLoader.output.Any(f => IsFabricApiCompatible(f)))
            return selectedFabric is null ? Lang.Text("Download.Install.Compat.RequiresFabric") : null;

        return Lang.Text("Download.Install.State.NoVersion");
    }

    // 限制展开
    private void CardFabricApi_PreviewSwap(object sender, Common.RouteEventArgs e)
    {
        if (LoadFabricApiGetError() is not null)
            e.handled = true;
    }

    private bool autoSelectedFabricApi;

    /// <summary>
    ///     尝试重新可视化 FabricApi 版本列表。
    /// </summary>
    private void FabricApi_Loaded()
    {
        try
        {
            if (GameDownload.DlFabricApiLoader.State != Common.LoadState.Finished)
                return;
            if (_vanillaName is null || selectedFabric is null)
                return;
            // 获取版本列表
            var versions = new List<Comp.CompFile>();
            foreach (var version in GameDownload.DlFabricApiLoader.output)
                if (IsFabricApiCompatible(version))
                {
                    if (!version.DisplayName.StartsWith("["))
                    {
                        Common.Log("[Download] 已特判修改 Fabric API 显示名：" + version.DisplayName, Common.LogLevel.Debug);
                        version.DisplayName = "[" + _vanillaName + "] " + version.DisplayName;
                    }

                    versions.Add(version);
                }

            if (!versions.Any())
                return;
            versions = versions.OrderByDescending(v => v.ReleaseDate).ToList();
            // 可视化
            var rows = new List<FabricApiVersionRow>();
            foreach (var version in versions)
            {
                if (!IsFabricApiCompatible(version))
                    continue;
                rows.Add(DownloadUi.FabricApiVersionRowBuild(version));
            }

            _vm.SetFabricApiVersions(rows);

            // 自动选择 Fabric API
            if (!autoSelectedFabricApi && rows.Count > 0)
            {
                autoSelectedFabricApi = true;
                Common.Log($"[Download] 已自动选择 Fabric API：{rows[0].Title}");
                SelectFabricApi(rows[0].Entry);
            }
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "可视化 Fabric API 安装版本列表出错",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Download.Install.Error.OperationFailed"));
        }
    }

    // 选择与清除
    private void FabricApi_Selected(object sender, MouseButtonEventArgs e)
    {
        SelectFabricApi((Comp.CompFile)((MyListItem)sender).Tag);
    }

    private void SelectFabricApi(Comp.CompFile entry)
    {
        selectedFabricApi = entry;
        selectedAPIName = "Fabric API";
        CardFabricApi.IsSwapped = true;
        ReloadSelected();
    }

    private void FabricApi_Clear(object sender, MouseButtonEventArgs e)
    {
        selectedFabricApi = null;
        selectedAPIName = null;
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
            return $"{Lang.Text("Download.Install.State.GetVersionListFailed")}{((Loading.LoaderBase)LoadLegacyFabric.State).Error.Message}";
        foreach (JsonObject Version in GameDownload.DlLegacyFabricListLoader.output.Value["game"].AsArray())
            if ((Version["version"].ToString() ?? "") == (_vanillaName ?? ""))
            {
                if (selectedLiteLoader is not null)
                    return Lang.Text("Download.Install.Compat.IncompatibleWithLiteLoader");
                if (_selectedLoaderType is not null && _selectedLoaderType != ModLoaderType.LegacyFabric)
                    return Lang.Text("Download.Install.Compat.IncompatibleWithLoader", _selectedLoaderType);
                return null;
            }

        return Lang.Text("Download.Install.State.NoVersion");
    }

    // 限制展开
    private void CardLegacyFabric_PreviewSwap(object sender, Common.RouteEventArgs e)
    {
        if (LoadLegacyFabricGetError() is not null)
            e.handled = true;
    }

    /// <summary>
    ///     尝试重新可视化 LegacyFabric 版本列表。
    /// </summary>
    private void LegacyFabric_Loaded()
    {
        try
        {
            if (GameDownload.DlLegacyFabricListLoader.State != Common.LoadState.Finished)
                return;
            // 获取版本列表
            var versions = (JsonArray)GameDownload.DlLegacyFabricListLoader.output.Value["loader"];
            if (!versions.Any())
                return;
            // 可视化
            PanLegacyFabric.Children.Clear();
            PanLegacyFabric.Tag = versions;
            CardLegacyFabric.SwapControl = PanLegacyFabric;
            CardLegacyFabric.InstallMethod = stack =>
            {
                foreach (var item in (IEnumerable)stack.Tag)
                    stack.Children.Add(DownloadUi.LegacyFabricDownloadListItem((JsonObject)item,
                        (a, b) => this.LegacyFabric_Selected((dynamic)a, b)));
            };
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "可视化 LegacyFabric 安装版本列表出错",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Download.Install.Error.OperationFailed"));
        }
    }

    // 选择与清除
    public void LegacyFabric_Selected(MyListItem sender, EventArgs e)
    {
        SelectModLoader(ModLoaderType.LegacyFabric);
        selectedLegacyFabric = ((dynamic)sender.Tag)["version"].ToString();
        LegacyFabricApi_Loaded();
        CardLegacyFabric.IsSwapped = true;
        ReloadSelected();
    }

    private void LegacyFabric_Clear(object sender, MouseButtonEventArgs e)
    {
        selectedLegacyFabric = null;
        selectedLegacyFabricApi = null;
        autoSelectedLegacyFabricApi = false;
        _selectedLoaderType = null;
        selectedAPIName = null;
        CardLegacyFabric.IsSwapped = true;
        e.Handled = true;
        ReloadSelected();
    }

    #endregion

    #region Legacy Fabric API 列表

    /// <summary>
    ///     从显示名判断该 API 是否与某版本适配。
    /// </summary>
    public static bool IsSuitableLegacyFabricApi(List<string> supportVersions, string minecraftVersion)
    {
        try
        {
            if (supportVersions.Contains(minecraftVersion)) return true;

            return false;
        }
        catch (Exception ex)
        {
            Common.Log(ex, "判断 Legacy Fabric API 版本适配性出错（" + supportVersions + ", " + minecraftVersion + "）");
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
            return $"{Lang.Text("Download.Install.State.GetVersionListFailed")}{((Loading.LoaderBase)LoadLegacyFabricApi.State).Error.Message}";
        if (selectedAPIName is not null && !ReferenceEquals(selectedAPIName, "Legacy Fabric API"))
            return Lang.Text("Download.Install.Compat.IncompatibleWithLoader", selectedAPIName);
        if (GameDownload.DlLegacyFabricApiLoader.output is null)
        {
            if (selectedLegacyFabric is null)
                return Lang.Text("Download.Install.Compat.RequiresLegacyFabric");
            return Lang.Text("Download.Install.State.Loading");
        }

        foreach (var Version in GameDownload.DlLegacyFabricApiLoader.output)
        {
            if (!IsSuitableLegacyFabricApi(Version.GameVersions, _vanillaName))
                continue;
            if (selectedLegacyFabric is null)
                return Lang.Text("Download.Install.Compat.RequiresLegacyFabric");
            return null;
        }

        return Lang.Text("Download.Install.State.NoVersion");
    }

    // 限制展开
    private void CardLegacyFabricApi_PreviewSwap(object sender, Common.RouteEventArgs e)
    {
        if (LoadLegacyFabricApiGetError() is not null)
            e.handled = true;
    }

    private bool autoSelectedLegacyFabricApi;

    /// <summary>
    ///     尝试重新可视化 LegacyFabricApi 版本列表。
    /// </summary>
    private void LegacyFabricApi_Loaded()
    {
        try
        {
            if (GameDownload.DlLegacyFabricApiLoader.State != Common.LoadState.Finished)
                return;
            if (_vanillaName is null || selectedLegacyFabric is null)
                return;
            // 获取版本列表
            var versions = new List<Comp.CompFile>();
            foreach (var Version in GameDownload.DlLegacyFabricApiLoader.output)
                if (IsSuitableLegacyFabricApi(Version.GameVersions, _vanillaName))
                    versions.Add(Version);

            if (!versions.Any())
                return;
            versions = versions.OrderByDescending(v => v.ReleaseDate).ToList();
            // 可视化
            PanLegacyFabricApi.Children.Clear();
            foreach (var Version in versions)
            {
                if (!IsSuitableLegacyFabricApi(Version.GameVersions, _vanillaName))
                    continue;
                PanLegacyFabricApi.Children.Add(
                    DownloadUi.LegacyFabricApiDownloadListItem(Version,
                        (a, b) => this.LegacyFabricApi_Selected((dynamic)a, b)));
            }

            // 自动选择 Legacy Fabric API
            if (!autoSelectedLegacyFabricApi)
            {
                autoSelectedLegacyFabricApi = true;
                Common.Log($"[Download] 已自动选择 Legacy Fabric API：{((MyListItem)PanLegacyFabricApi.Children[0]).Title}");
                LegacyFabricApi_Selected((MyListItem)PanLegacyFabricApi.Children[0], null);
            }
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "可视化 Legacy Fabric API 安装版本列表出错",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Download.Install.Error.OperationFailed"));
        }
    }

    // 选择与清除
    private void LegacyFabricApi_Selected(MyListItem sender, EventArgs e)
    {
        selectedLegacyFabricApi = (Comp.CompFile)(dynamic)sender.Tag;
        selectedAPIName = "Legacy Fabric API";
        CardLegacyFabricApi.IsSwapped = true;
        ReloadSelected();
    }

    private void LegacyFabricApi_Clear(object sender, MouseButtonEventArgs e)
    {
        selectedLegacyFabricApi = null;
        selectedAPIName = null;
        CardLegacyFabricApi.IsSwapped = true;
        e.Handled = true;
        ReloadSelected();
    }

    #endregion

    #region OptiFabric 列表

    /// <summary>
    ///     判断某 OptiFabric 是否适配当前选择的原版版本。
    /// </summary>
    private bool IsOptiFabricCompatible(Comp.CompFile modFile)
    {
        try
        {
            if (_vanillaName is null)
                return false;
            return modFile.GameVersions.Contains(_vanillaName);
        }
        catch (Exception ex)
        {
            Common.Log(ex, "判断 OptiFabric 版本适配性出错（" + _vanillaName + "）");
            return false;
        }
    }

    private bool autoSelectedOptiFabric;

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
        if (GameDownload.DlOptiFabricLoader.output is null)
        {
            if (selectedFabric is null && selectedOptiFine is null)
                return Lang.Text("Download.Install.Compat.RequiresOptiFineAndFabric");
            if (selectedFabric is null)
                return Lang.Text("Download.Install.Compat.RequiresFabric");
            if (selectedOptiFine is null)
                return Lang.Text("Download.Install.Compat.RequiresOptiFine");
            return Lang.Text("Download.Install.State.Getting");
        }

        foreach (var version in GameDownload.DlOptiFabricLoader.output)
        {
            if (!IsOptiFabricCompatible(version))
                continue; // 2135#
            if (selectedFabric is null && selectedOptiFine is null)
                return Lang.Text("Download.Install.Compat.RequiresOptiFineAndFabric");
            if (selectedFabric is null)
                return Lang.Text("Download.Install.Compat.RequiresFabric");
            if (selectedOptiFine is null)
                return Lang.Text("Download.Install.Compat.RequiresOptiFine");
            return null; // 通过检查
        }

        return Lang.Text("Download.Install.State.NoVersion");
    }

    // 限制展开
    private void CardOptiFabric_PreviewSwap(object sender, Common.RouteEventArgs e)
    {
        if (LoadOptiFabricGetError() is not null)
            e.handled = true;
    }

    /// <summary>
    ///     尝试重新可视化 OptiFabric 版本列表。
    /// </summary>
    private void OptiFabric_Loaded()
    {
        try
        {
            if (GameDownload.DlOptiFabricLoader.State != Common.LoadState.Finished)
                return;
            if (_vanillaName is null || selectedFabric is null || selectedOptiFine is null)
                return;
            // 获取版本列表
            var versions = new List<Comp.CompFile>();
            foreach (var Version in GameDownload.DlOptiFabricLoader.output)
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
                    DownloadUi.OptiFabricDownloadListItem(Version,
                        (a, b) => this.OptiFabric_Selected((dynamic)a, b)));
            }

            // 自动选择 OptiFabric
            if (autoSelectedOptiFabric || (VanillaDrop >= 140 && VanillaDrop <= 150))
                return; // 1.14~15 不自动选择
            autoSelectedOptiFabric = true;
            Common.Log($"[Download] 已自动选择 OptiFabric：{((MyListItem)PanOptiFabric.Children[0]).Title}");
            OptiFabric_Selected((MyListItem)PanOptiFabric.Children[0], null);
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "可视化 OptiFabric 安装版本列表出错",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Download.Install.Error.OperationFailed"));
        }
    }

    // 选择与清除
    private void OptiFabric_Selected(MyListItem sender, EventArgs e)
    {
        selectedOptiFabric = (Comp.CompFile)(dynamic)sender.Tag;
        CardOptiFabric.IsSwapped = true;
        ReloadSelected();
    }

    private void OptiFabric_Clear(object sender, MouseButtonEventArgs e)
    {
        selectedOptiFabric = null;
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
            return $"{Lang.Text("Download.Install.State.GetVersionListFailed")}{((Loading.LoaderBase)LoadLabyMod.State).Error.Message}";
        // 检查 Loader
        if (GetLoaderError(LoadLabyMod) is not null)
            return GetLoaderError(LoadLabyMod);
        if (selectedOptiFine is not null)
            return Lang.Text("Download.Install.Compat.IncompatibleWithOptiFine");
        if (_selectedLoaderType is not null && _selectedLoaderType != ModLoaderType.LabyMod)
            return Lang.Text("Download.Install.Compat.IncompatibleWithLoader", _selectedLoaderType);
        if (selectedLiteLoader is not null)
            return Lang.Text("Download.Install.Compat.IncompatibleWithLiteLoader");
        foreach (JsonObject Version in GameDownload.DlLabyModListLoader.output.Value["production"]["minecraftVersions"].AsArray())
            if ((Version["version"].ToString() ?? "") == (_vanillaName ?? ""))
                return null;
        foreach (JsonObject Version in GameDownload.DlLabyModListLoader.output.Value["snapshot"]["minecraftVersions"].AsArray())
            if ((Version["version"].ToString() ?? "") == (_vanillaName ?? ""))
                return null;
        return Lang.Text("Download.Install.State.NoVersion");
    }

    // 限制展开
    private void CardLabyMod_PreviewSwap(object sender, Common.RouteEventArgs e)
    {
        if (LoadLabyModGetError() is not null)
            e.handled = true;
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
            var versions = GameDownload.DlLabyModListLoader.output.Value;
            if (versions is null || versions["production"] is null || versions["snapshot"] is null)
                return;
            // 可视化
            var processedVersions = new JsonArray();
            foreach (JsonObject Production in versions["production"]["minecraftVersions"].AsArray())
                if ((Production["version"].ToString() ?? "") == (_vanillaName ?? ""))
                {
                    var productionVersion = new JsonObject();
                    productionVersion.Add("version", versions["production"]["labyModVersion"].ToString());
                    productionVersion.Add("channel", "production");
                    productionVersion.Add("commitReference", versions["production"]["commitReference"].ToString());
                    processedVersions.Add(productionVersion);
                }

            foreach (JsonObject Snapshot in versions["snapshot"]["minecraftVersions"].AsArray())
                if ((Snapshot["version"].ToString() ?? "") == (_vanillaName ?? ""))
                {
                    var snapshotVersion = new JsonObject();
                    snapshotVersion.Add("version", versions["snapshot"]["labyModVersion"].ToString());
                    snapshotVersion.Add("channel", "snapshot");
                    snapshotVersion.Add("commitReference", versions["snapshot"]["commitReference"].ToString());
                    processedVersions.Add(snapshotVersion);
                }

            // MyMsgBox(If(ProcessedVersions.ToString, "Nothing"))
            PanLabyMod.Children.Clear();
            PanLabyMod.Tag = processedVersions;
            CardLabyMod.SwapControl = PanLabyMod;
            CardLabyMod.InstallMethod = stack =>
            {
                foreach (JsonObject item in (IEnumerable)stack.Tag)
                    stack.Children.Add(
                        DownloadUi.LabyModDownloadListItem(item, (a, b) => this.LabyMod_Selected((dynamic)a, b)));
            };
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "可视化 LabyMod 安装版本列表出错",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Download.Install.Error.OperationFailed"));
        }
    }

    // 选择与清除
    public void LabyMod_Selected(MyListItem sender, EventArgs e)
    {
        SelectModLoader(ModLoaderType.LabyMod);
        selectedLabyModChannel = ((dynamic)sender.Tag)["channel"].ToString();
        selectedLabyModCommitRef = ((dynamic)sender.Tag)["commitReference"].ToString();
        selectedLabyModBaseVersion = ((dynamic)sender.Tag)["version"].ToString();
        selectedLabyModVersion =
            selectedLabyModBaseVersion + " " + (selectedLabyModChannel == "snapshot" ? Lang.Text("Download.Version.Type.Snapshot") : Lang.Text("Download.Version.Type.Stable"));
        CardLabyMod.IsSwapped = true;
        ReloadSelected();
    }

    private void LabyMod_Clear(object sender, MouseButtonEventArgs e)
    {
        selectedLabyModCommitRef = null;
        selectedLabyModVersion = null;
        selectedLabyModBaseVersion = null;
        selectedLabyModChannel = null;

        if (_selectedLoaderType == ModLoaderType.LabyMod)
        {
            _selectedLoaderType = null;
        }

        selectedAPIName = null;
        CardLabyMod.IsSwapped = true;
        e.Handled = true;
        ReloadSelected();
    }

    #endregion

    #region 安装

    private void TextSelectName_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && BtnStart.IsEnabled)
            BtnStart_Click();
    }

    private void BtnStart_Click()
    {
        if (!RestrictedFeaturePolicy.Check("minecraft-install")) return;
        // 确认版本隔离
        if (_selectedLoaderType is not null &&
            (Config.Launch.IndieSolutionV2 == 0 ||
             Config.Launch.IndieSolutionV2 == 2))
            if ((DialogService.Instance.Show(new TextDialogRequest
            {
                Title = Lang.Text("Download.Install.InstanceIsolation.Warning.Title"),
                Content = Lang.Text("Download.Install.InstanceIsolation.Warning.Message"),
                Buttons = DialogButtons.Create("Download.Install.InstanceIsolation.Warning.Cancel", "Download.Install.InstanceIsolation.Warning.Continue"),
                ForceWait = true
            }).Result?.Button ?? 0) == 1)
                return;

        // 提交安装申请
        var instanceName = _vm.InstanceName;
        var request = new DownloadUi.McInstallRequest
        {
            targetInstanceName = instanceName,
            targetInstanceFolder = $@"{GameFolders.mcFolderSelected}versions\{instanceName}\",
            minecraftJson = _vanillaData?["url"].ToString(),
            minecraftName = _vanillaName,
            optiFineEntry = selectedOptiFine,
            forgeEntry = selectedForge,
            neoForgeEntry = selectedNeoForge,
            cleanroomEntry = selectedCleanroom,
            fabricVersion = selectedFabric,
            fabricApi = selectedFabricApi,
            optiFabric = selectedOptiFabric,
            liteLoaderEntry = selectedLiteLoader,
            labyModChannel = selectedLabyModChannel,
            labyModCommitRef = selectedLabyModCommitRef,
            legacyFabricVersion = selectedLegacyFabric,
            legacyFabricApi = selectedLegacyFabricApi
        };
        if (!DownloadUi.McInstall(request))
            return;
        // 返回，这样在再次进入安装页面时这个实例就会显示文件夹已重复
        ExitSelectPage();
    }

    #endregion
}
