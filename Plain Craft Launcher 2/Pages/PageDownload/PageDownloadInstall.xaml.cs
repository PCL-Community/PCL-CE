using System.Collections;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using FluentValidation;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json.Linq;
using PCL.Core.App;
using PCL.Core.Utils.Validate;

namespace PCL;

public partial class PageDownloadInstall
{
    private bool IsLoad;

    public PageDownloadInstall()
    {
        PanScroll = PanBack;
        Initialized += (_, _) => LoaderInit();
        Loaded += (_, _) => Init();
        InitializeComponent();
        BtnBack.Click += (_, _) => ExitSelectPage();
        CardOptiFine.Swap += (_, _) => ReloadSelected();
        LoadOptiFine.StateChanged += (_, _, _) => ReloadSelected();
        CardForge.Swap += (_, _) => ReloadSelected();
        LoadForge.StateChanged += (_, _, _) => ReloadSelected();
        CardNeoForge.Swap += (_, _) => ReloadSelected();
        LoadNeoForge.StateChanged += (_, _, _) => ReloadSelected();
        CardFabric.Swap += (_, _) => ReloadSelected();
        LoadFabric.StateChanged += (_, _, _) => ReloadSelected();
        CardFabricApi.Swap += (_, _) => ReloadSelected();
        LoadFabricApi.StateChanged += (_, _, _) => ReloadSelected();
        CardOptiFabric.Swap += (_, _) => ReloadSelected();
        LoadOptiFabric.StateChanged += (_, _, _) => ReloadSelected();
        CardLiteLoader.Swap += (_, _) => ReloadSelected();
        LoadLiteLoader.StateChanged += (_, _, _) => ReloadSelected();
        LoadQuilt.StateChanged += (_, _, _) => ReloadSelected();
        CardQuilt.Swap += (_, _) => ReloadSelected();
        LoadQSL.StateChanged += (_, _, _) => ReloadSelected();
        CardQSL.Swap += (_, _) => ReloadSelected();
        LoadCleanroom.StateChanged += (_, _, _) => ReloadSelected();
        CardCleanroom.Swap += (_, _) => ReloadSelected();
        LoadLabyMod.StateChanged += (_, _, _) => ReloadSelected();
        CardLabyMod.Swap += (_, _) => ReloadSelected();
        TextSelectName.TextChanged += TextSelectName_TextChanged;
        TextSelectName.ValidateChanged += TextSelectName_ValidateChanged;
        CardOptiFine.PreviewSwap += CardOptiFine_PreviewSwap;
        LoadOptiFine.StateChanged += (_, _, _) => OptiFine_Loaded();
        BtnOptiFineClear.MouseLeftButtonUp += OptiFine_Clear;
        CardLiteLoader.PreviewSwap += CardLiteLoader_PreviewSwap;
        LoadLiteLoader.StateChanged += (_, _, _) => LiteLoader_Loaded();
        BtnLiteLoaderClear.MouseLeftButtonUp += LiteLoader_Clear;
        CardForge.PreviewSwap += CardForge_PreviewSwap;
        LoadForge.StateChanged += (_, _, _) => Forge_Loaded();
        BtnForgeClear.MouseLeftButtonUp += Forge_Clear;
        CardNeoForge.PreviewSwap += CardNeoForge_PreviewSwap;
        LoadNeoForge.StateChanged += (_, _, _) => NeoForge_Loaded();
        BtnNeoForgeClear.MouseLeftButtonUp += NeoForge_Clear;
        CardCleanroom.PreviewSwap += CardCleanroom_PreviewSwap;
        LoadCleanroom.StateChanged += (_, _, _) => Cleanroom_Loaded();
        BtnCleanroomClear.MouseLeftButtonUp += Cleanroom_Clear;
        CardFabric.PreviewSwap += CardFabric_PreviewSwap;
        LoadFabric.StateChanged += (_, _, _) => Fabric_Loaded();
        BtnFabricClear.MouseLeftButtonUp += Fabric_Clear;
        CardFabricApi.PreviewSwap += CardFabricApi_PreviewSwap;
        LoadFabricApi.StateChanged += (_, _, _) => FabricApi_Loaded();
        BtnFabricApiClear.MouseLeftButtonUp += FabricApi_Clear;
        CardLegacyFabric.PreviewSwap += CardLegacyFabric_PreviewSwap;
        LoadLegacyFabric.StateChanged += (_, _, _) => LegacyFabric_Loaded();
        BtnLegacyFabricClear.MouseLeftButtonUp += LegacyFabric_Clear;
        CardLegacyFabricApi.PreviewSwap += CardLegacyFabricApi_PreviewSwap;
        LoadLegacyFabricApi.StateChanged += (_, _, _) => LegacyFabricApi_Loaded();
        BtnLegacyFabricApiClear.MouseLeftButtonUp += LegacyFabricApi_Clear;
        CardQuilt.PreviewSwap += CardQuilt_PreviewSwap;
        LoadQuilt.StateChanged += (_, _, _) => Quilt_Loaded();
        BtnQuiltClear.MouseLeftButtonUp += Quilt_Clear;
        CardQSL.PreviewSwap += CardQSL_PreviewSwap;
        LoadQSL.StateChanged += (_, _, _) => QSL_Loaded();
        BtnQSLClear.MouseLeftButtonUp += QSL_Clear;
        CardOptiFabric.PreviewSwap += CardOptiFabric_PreviewSwap;
        LoadOptiFabric.StateChanged += (_, _, _) => OptiFabric_Loaded();
        BtnOptiFabricClear.MouseLeftButtonUp += OptiFabric_Clear;
        CardLabyMod.PreviewSwap += CardLabyMod_PreviewSwap;
        LoadLabyMod.StateChanged += (_, _, _) => LabyMod_Loaded();
        BtnLabyModClear.MouseLeftButtonUp += LabyMod_Clear;
        TextSelectName.KeyDown += TextSelectName_KeyDown;
        BtnStart.Click += (_, _) => BtnStart_Click();
    }

    private void LoaderInit()
    {
        DisabledPageAnimControls.Add(BtnStart);
        PageLoaderInit(LoadMinecraft, PanLoad, PanAllBack, null, ModDownload.DlClientListLoader,
            _ => LoadMinecraft_OnFinish());
    }

    private void Init()
    {
        PanBack.ScrollToHome();
        ModDownload.DlOptiFineListLoader.Start();
        ModDownload.DlLiteLoaderListLoader.Start();
        ModDownload.DlFabricListLoader.Start();
        ModDownload.DlQuiltListLoader.Start();
        ModDownload.DlNeoForgeListLoader.Start();
        ModDownload.DlCleanroomListLoader.Start();
        ModDownload.DlLabyModListLoader.Start();
        ModDownload.DlLegacyFabricListLoader.Start();

        // 重载预览
        TextSelectName.ValidateRules = [new FolderNameValidator(ModMinecraft.McFolderSelected + "versions")];
        TextSelectName.Validate();
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

    private string GetLoaderError(MyLoading loader)
    {
        if (loader is null)
            return "获取中……";
        if (!loader.State.IsLoader)
            return "获取中……";
        switch (loader.State.LoadingState)
        {
            case MyLoading.MyLoadingState.Run:
            {
                return "获取中……";
            }
            case MyLoading.MyLoadingState.Error:
            {
                var message = ((ModLoader.LoaderBase)loader.State).Error.Message;
                return message == "无可用版本" ? "无可用版本" : "获取失败：" + message;
            }
            case MyLoading.MyLoadingState.Unloaded:
            {
                return "未知错误，状态为 Unloaded";
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
    public bool IsInSelectPage;
    private bool IsFirstLoaded;

    private void EnterSelectPage()
    {
        if (IsInSelectPage)
            return;
        IsInSelectPage = true;

        PanInner.Margin = new Thickness(25d, 10d, 25d, 40d);

        AutoSelectedFabricApi = false;
        AutoSelectedQSL = false;
        AutoSelectedOptiFabric = false;
        IsSelectNameEdited = false;
        PanSelect.Visibility = Visibility.Visible;
        PanSelect.IsHitTestVisible = true;
        PanMinecraft.IsHitTestVisible = false;
        PanBack.IsHitTestVisible = false;
        PanBack.ScrollToHome();

        DisabledPageAnimControls.Remove(BtnStart);
        BtnStart.Show = true;
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

        if (!States.Hint.InstallPageBack)
        {
            States.Hint.InstallPageBack = true;
            ModMain.Hint("点击 Minecraft 项即可返回游戏主版本选择页面！");
        }

        // 如果在选择页面按了刷新键，选择页的东西可能会由于动画被隐藏，但不会由于加载结束而再次显示，因此这里需要手动恢复
        foreach (var control in GetAllAnimControls(PanSelect))
        {
            control.Opacity = 1d;
            if (control.RenderTransform is null || control.RenderTransform is TranslateTransform)
                control.RenderTransform = new TranslateTransform();
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
        ModDownload.DlLabyModListLoader.Start();

        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaOpacity(PanMinecraft, -PanMinecraft.Opacity, 70, 10),
            ModAnimation.AaTranslateX(PanMinecraft, -50 - ((TranslateTransform)PanMinecraft.RenderTransform).X, 90, 10),
            ModAnimation.AaCode(() =>
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
                Quilt_Loaded();
                QSL_Loaded();
                OptiFabric_Loaded();
                LabyMod_Loaded();
                ReloadSelected();
                PanMinecraft.Visibility = Visibility.Collapsed;
            }, After: true),
            ModAnimation.AaOpacity(PanSelect, 1d - PanSelect.Opacity, 70, 100),
            ModAnimation.AaTranslateX(PanSelect, -((TranslateTransform)PanSelect.RenderTransform).X, 160, 100,
                new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.ExtraStrong)),
            ModAnimation.AaCode(() =>
            {
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
                BtnQuiltClearInner.SetBinding(Shape.FillProperty,
                    new Binding("Foreground") { Source = CardQuilt.MainTextBlock, Mode = BindingMode.OneWay });
                BtnQSLClearInner.SetBinding(Shape.FillProperty,
                    new Binding("Foreground") { Source = CardQSL.MainTextBlock, Mode = BindingMode.OneWay });
                BtnLabyModClearInner.SetBinding(Shape.FillProperty,
                    new Binding("Foreground") { Source = CardLabyMod.MainTextBlock, Mode = BindingMode.OneWay });
                BtnOptiFabricClearInner.SetBinding(Shape.FillProperty,
                    new Binding("Foreground") { Source = CardOptiFabric.MainTextBlock, Mode = BindingMode.OneWay });
            }, After: true)
        }, "FrmDownloadInstall SelectPageSwitch", true);
    }

    public void ExitSelectPage()
    {
        if (!IsInSelectPage)
            return;
        IsInSelectPage = false;

        PanInner.Margin = new Thickness(25d, 10d, 25d, 25d);

        DisabledPageAnimControls.Add(BtnStart);
        BtnStart.Show = false;
        ClearSelected(); // 清除已选择项
        PanMinecraft.Visibility = Visibility.Visible;
        PanSelect.IsHitTestVisible = false;
        PanMinecraft.IsHitTestVisible = true;
        PanBack.IsHitTestVisible = false;
        PanBack.ScrollToHome();

        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaOpacity(PanSelect, -PanSelect.Opacity, 70, 10),
            ModAnimation.AaTranslateX(PanSelect, 50d - ((TranslateTransform)PanSelect.RenderTransform).X, 90, 10),
            ModAnimation.AaCode(() => PanBack.ScrollToHome(), After: true),
            ModAnimation.AaOpacity(PanMinecraft, 1d - PanMinecraft.Opacity, 70, 100),
            ModAnimation.AaTranslateX(PanMinecraft, -((TranslateTransform)PanMinecraft.RenderTransform).X, 160, 100,
                new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.ExtraStrong)),
            ModAnimation.AaCode(() =>
            {
                PanSelect.Visibility = Visibility.Collapsed;
                PanBack.IsHitTestVisible = true;
            }, After: true)
        }, "FrmDownloadInstall SelectPageSwitch");
    }

    public void MinecraftSelected(MyListItem sender, MouseButtonEventArgs e)
    {
        _vanillaName = sender.Title;
        _vanillaData = (JObject)(dynamic)sender.Tag;
        _vanillaIcon = sender.Logo;
        EnterSelectPage();
    }

    #endregion

    #region 选择

    // Minecraft
    private string? _vanillaName;
    private JObject? _vanillaData;
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

    // NeoForge
    private ModDownload.DlNeoForgeListEntry? SelectedNeoForge;

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
        // 主预览
        SelectNameUpdate();
        ImgLogo.Source = GetSelectLogo();
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
            LabOptiFine.Text = OptiFineError ?? "可以添加";
            LabOptiFine.Foreground = ModSecret.ColorGray4;
        }
        else
        {
            BtnOptiFineClear.Visibility = Visibility.Visible;
            ImgOptiFine.Visibility = Visibility.Visible;
            LabOptiFine.Text = SelectedOptiFine.DisplayName.Replace(_vanillaName + " ", "");
            LabOptiFine.Foreground = ModSecret.ColorGray1;
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
                LabLiteLoader.Text = LiteLoaderError ?? "可以添加";
                LabLiteLoader.Foreground = ModSecret.ColorGray4;
            }
            else
            {
                BtnLiteLoaderClear.Visibility = Visibility.Visible;
                ImgLiteLoader.Visibility = Visibility.Visible;
                LabLiteLoader.Text = SelectedLiteLoader.Inherit;
                LabLiteLoader.Foreground = ModSecret.ColorGray1;
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
                LabForge.Text = forgeError ?? "可以添加";
                LabForge.Foreground = ModSecret.ColorGray4;
            }
            else
            {
                BtnForgeClear.Visibility = Visibility.Visible;
                ImgForge.Visibility = Visibility.Visible;
                LabForge.Text = SelectedForge.VersionName;
                LabForge.Foreground = ModSecret.ColorGray1;
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
                LabCleanroom.Text = cleanroomError ?? "可以添加";
                LabCleanroom.Foreground = ModSecret.ColorGray4;
            }
            else
            {
                BtnCleanroomClear.Visibility = Visibility.Visible;
                ImgCleanroom.Visibility = Visibility.Visible;
                LabCleanroom.Text = SelectedCleanroom.VersionName;
                LabCleanroom.Foreground = ModSecret.ColorGray1;
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
                LabNeoForge.Text = neoForgeError ?? "可以添加";
                LabNeoForge.Foreground = ModSecret.ColorGray4;
            }
            else
            {
                BtnNeoForgeClear.Visibility = Visibility.Visible;
                ImgNeoForge.Visibility = Visibility.Visible;
                LabNeoForge.Text = SelectedNeoForge.VersionName;
                LabNeoForge.Foreground = ModSecret.ColorGray1;
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
                LabFabric.Text = fabricError ?? "可以添加";
                LabFabric.Foreground = ModSecret.ColorGray4;
            }
            else
            {
                BtnFabricClear.Visibility = Visibility.Visible;
                ImgFabric.Visibility = Visibility.Visible;
                LabFabric.Text = SelectedFabric.Replace("+build", "");
                LabFabric.Foreground = ModSecret.ColorGray1;
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
                LabFabricApi.Text = fabricApiError ?? "可以添加";
                LabFabricApi.Foreground = ModSecret.ColorGray4;
            }
            else
            {
                BtnFabricApiClear.Visibility = Visibility.Visible;
                ImgFabricApi.Visibility = Visibility.Visible;
                LabFabricApi.Text = SelectedFabricApi.DisplayName.Split("]")[1].Replace("Fabric API ", "")
                    .Replace(" build ", ".").Trim();
                LabFabricApi.Foreground = ModSecret.ColorGray1;
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
                LabLegacyFabric.Text = legacyFabricError ?? "可以添加";
                LabLegacyFabric.Foreground = ModSecret.ColorGray4;
            }
            else
            {
                BtnLegacyFabricClear.Visibility = Visibility.Visible;
                ImgLegacyFabric.Visibility = Visibility.Visible;
                LabLegacyFabric.Text = SelectedLegacyFabric.Replace("+build", "");
                LabLegacyFabric.Foreground = ModSecret.ColorGray1;
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
                LabLegacyFabricApi.Text = legacyFabricApiError ?? "可以添加";
                LabLegacyFabricApi.Foreground = ModSecret.ColorGray4;
            }
            else
            {
                BtnLegacyFabricApiClear.Visibility = Visibility.Visible;
                ImgLegacyFabricApi.Visibility = Visibility.Visible;
                LabLegacyFabricApi.Text = SelectedLegacyFabricApi.DisplayName.Replace("Legacy Fabric API ", "");
                LabLegacyFabricApi.Foreground = ModSecret.ColorGray1;
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
                LabQuilt.Text = quiltError ?? "可以添加";
                LabQuilt.Foreground = ModSecret.ColorGray4;
            }
            else
            {
                BtnQuiltClear.Visibility = Visibility.Visible;
                ImgQuilt.Visibility = Visibility.Visible;
                LabQuilt.Text = SelectedQuilt.Replace("+build", "");
                LabQuilt.Foreground = ModSecret.ColorGray1;
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
                LabQSL.Text = qslError ?? "可以添加";
                LabQSL.Foreground = ModSecret.ColorGray4;
            }
            else
            {
                BtnQSLClear.Visibility = Visibility.Visible;
                ImgQSL.Visibility = Visibility.Visible;
                LabQSL.Text = SelectedQSL.DisplayName.Split("]")[1].Trim();
                LabQSL.Foreground = ModSecret.ColorGray1;
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
                LabLabyMod.Text = labyModError ?? "可以添加";
                LabLabyMod.Foreground = ModSecret.ColorGray4;
            }
            else
            {
                BtnLabyModClear.Visibility = Visibility.Visible;
                ImgLabyMod.Visibility = Visibility.Visible;
                LabLabyMod.Text = SelectedLabyModVersion;
                LabLabyMod.Foreground = ModSecret.ColorGray1;
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
                LabOptiFabric.Text = optiFabricError ?? "可以添加";
                LabOptiFabric.Foreground = ModSecret.ColorGray4;
            }
            else
            {
                BtnOptiFabricClear.Visibility = Visibility.Visible;
                ImgOptiFabric.Visibility = Visibility.Visible;
                LabOptiFabric.Text = SelectedOptiFabric.DisplayName.ToLower().Replace("optifabric-", "")
                    .Replace(".jar", "").Trim().TrimStart('v');
                LabOptiFabric.Foreground = ModSecret.ColorGray1;
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

        if (SelectedFabric is not null | SelectedLegacyFabric is not null && SelectedOptiFine is not null &&
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
        SelectedCleanroom = null;
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
        if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(panel.Tag, visible.ToString(), false)))
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

    // 实例名处理
    /// <summary>
    ///     获取默认实例名。
    /// </summary>
    private string GetSelectName()
    {
        var name = _vanillaName;
        if (SelectedFabric is not null) name += "-Fabric_" + SelectedFabric.Replace("+build", "");
        if (SelectedLegacyFabric is not null) name += "-LegacyFabric_" + SelectedLegacyFabric;
        if (SelectedQuilt is not null) name += "-Quilt_" + SelectedQuilt;
        if (SelectedLabyModVersion is not null)
            name += "-LabyMod_" + SelectedLabyModVersion.Replace(" 稳定版", "_Production").Replace(" 快照版", "_Snapshot");
        if (SelectedForge is not null) name += "-Forge_" + SelectedForge.VersionName;
        if (SelectedNeoForge is not null) name += "-NeoForge_" + SelectedNeoForge.VersionName;
        if (SelectedCleanroom is not null) name += "-Cleanroom_" + SelectedCleanroom.VersionName;
        if (SelectedLiteLoader is not null) name += "-LiteLoader";
        if (SelectedOptiFine is not null)
            name += "-OptiFine_" + SelectedOptiFine.DisplayName.Replace(_vanillaName + " ", "").Replace(" ", "_");
        return name;
    }

    private bool IsSelectNameEdited;
    private bool IsSelectNameChanging;

    private void SelectNameUpdate()
    {
        if (IsSelectNameEdited || IsSelectNameChanging)
            return;
        IsSelectNameChanging = true;
        TextSelectName.Text = GetSelectName();
        IsSelectNameChanging = false;
    }

    private void TextSelectName_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (IsSelectNameChanging)
            return;
        IsSelectNameEdited = true;
        ReloadSelected();
    }

    private void TextSelectName_ValidateChanged(object sender, EventArgs e)
    {
        BtnStart.IsEnabled = TextSelectName.IsValidated;
    }

    #endregion

    #region 加载器

    // 结果数据化
    private void LoadMinecraft_OnFinish()
    {
        ExitSelectPage(); // 返回
        do
        {
            try
            {
                var Dict = new Dictionary<string, List<JObject>>
                {
                    { "正式版", new List<JObject>() }, { "预览版", new List<JObject>() }, { "远古版", new List<JObject>() },
                    { "愚人节版", new List<JObject>() }
                };
                var Versions = (JArray)ModDownload.DlClientListLoader.Output.Value["versions"];
                foreach (JObject Version in Versions)
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
                                    Version.Add("lore", ModMinecraft.GetMcFoolName((string)Version["id"]));
                                    break;
                                }
                                case "20w14infinite":
                                case "20w14∞":
                                {
                                    Type = "愚人节版";
                                    Version["id"] = "20w14∞";
                                    Version["type"] = "special";
                                    Version.Add("lore", ModMinecraft.GetMcFoolName((string)Version["id"]));
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
                                        ModMinecraft.GetMcFoolName((string)Version["id"])); // 4/1 自动视作愚人节版
                                    break;
                                }

                                default:
                                {
                                    var ReleaseDate = Version["releaseTime"].Value<DateTime>().ToUniversalTime()
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
                    Dict[Pair.Key] = Pair.Value.OrderByDescending(j => j["releaseTime"].Value<DateTime>()).ToList();
                // 清空当前
                PanMinecraft.Children.Clear();
                // 添加最新版本
                var CardInfo = new MyCard { Title = "最新版本", Margin = new Thickness(0d, 15d, 0d, 15d) };
                var TopestVersions = new List<JObject>();
                var Release = (JObject)Dict["正式版"][0].DeepClone();
                Release["lore"] = "最新正式版，发布于 " +
                                  Release["releaseTime"].Value<DateTime>().ToString("yyyy'/'MM'/'dd HH':'mm");
                TopestVersions.Add(Release);
                if (Dict["正式版"][0]["releaseTime"].Value<DateTime>() < Dict["预览版"][0]["releaseTime"].Value<DateTime>())
                {
                    var Snapshot = (JObject)Dict["预览版"][0].DeepClone();
                    Snapshot["lore"] = "最新预览版，发布于 " +
                                       Snapshot["releaseTime"].Value<DateTime>().ToString("yyyy'/'MM'/'dd HH':'mm");
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
                        Stack.Children.Add(ModDownloadLib.McDownloadListItem((JObject)item,
                            (sender, e) => ModMain.FrmDownloadInstall.MinecraftSelected((MyListItem)sender, e), false));
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
                        { Title = Pair.Key + " (" + Pair.Value.Count + ")", Margin = new Thickness(0d, 0d, 0d, 15d) };
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
                foreach (JObject Version in Versions)
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
            return $"与 {SelectedLoaderName} 不兼容";
        if (LoadOptiFine is null || LoadOptiFine.State.LoadingState == MyLoading.MyLoadingState.Run)
            return "加载中……";
        if (LoadOptiFine.State.LoadingState == MyLoading.MyLoadingState.Error)
            return Conversions.ToString(Operators.ConcatenateObject("获取版本列表失败：",
                ((dynamic)LoadOptiFine.State).Error.Message));
        // 是否有 Cleanroom
        if (SelectedCleanroom is not null)
            return "与 Cleanroom 不兼容";
        // 检查 Forge 1.13 - 1.14.3：全部不兼容
        if (SelectedLoaderName == "Forge" && ModMinecraft.CompareVersion(_vanillaName, "1.13") >= 0 &&
            ModMinecraft.CompareVersion("1.14.3", _vanillaName) >= 0) return "与 Forge 不兼容";
        // 检查 Fabric 1.20.5+: 全部不兼容
        if (SelectedFabric is not null && ModMinecraft.CompareVersion(_vanillaName, "1.20.4") > 0)
            return "与 Fabric 不兼容";
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
            if (Conversions.ToBoolean(IsOptiFineSuitForForge(OptiFineVersion, SelectedForge)))
                return null; // 该版本可用
            if (OptiFineVersion.RequiredForgeVersion is not null)
                HasRequiredVersion = true;
        }

        if (!HasAny) return "无可用版本";

        if (HasRequiredVersion) return "仅兼容特定版本的 Forge";

        return "与 Forge 不兼容";
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
        return Forge.Version.Revision == Conversions.ToDouble(OptiFine.RequiredForgeVersion);
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
                if (Conversions.ToBoolean(SelectedForge is not null &&
                                          !(bool)IsOptiFineSuitForForge(Version, SelectedForge)))
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
                return ModMinecraft.CompareVersionGe(Left.DisplayName, Right.DisplayName);
            });
            // 可视化
            PanOptiFine.Children.Clear();
            foreach (var Version in Versions)
                PanOptiFine.Children.Add(
                    ModDownloadLib.OptiFineDownloadListItem(Version, (a, b) => this.OptiFine_Selected((dynamic)a, b),
                        false));
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "可视化 OptiFine 安装版本列表出错", ModBase.LogLevel.Feedback);
        }
    }

    // 选择与清除
    private void OptiFine_Selected(MyListItem sender, EventArgs e)
    {
        SelectedOptiFine = (ModDownload.DlOptiFineListEntry)(dynamic)sender.Tag;
        if (Conversions.ToBoolean(SelectedForge is not null &&
                                  !(bool)IsOptiFineSuitForForge(SelectedOptiFine, SelectedForge)))
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
            : "无可用版本";
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
        SelectedLiteLoader = (ModDownload.DlLiteLoaderListEntry)(dynamic)sender.Tag;
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
            return "无可用版本";
        
        if (SelectedLoaderName is not null && !ReferenceEquals(SelectedLoaderName, "Forge"))
            return $"与 {SelectedLoaderName} 不兼容";
        
        // 检查 Loader
        if (GetLoaderError(LoadForge) is not null)
            return GetLoaderError(LoadForge);
        var loader = (ModLoader.LoaderTask<string, List<ModDownload.DlForgeVersionEntry>>)LoadForge.State;
        if ((_vanillaName ?? "") != (loader.Input ?? ""))
            return "获取中……";
        // 检查版本
        foreach (var Version in loader.Output)
        {
            if (Version.Category == "universal" || Version.Category == "client")
                continue; // 跳过无法自动安装的版本
            if (SelectedNeoForge is not null || SelectedFabric is not null || SelectedQuilt is not null)
                return $"与 {SelectedLoaderName} 不兼容";
            if (SelectedOptiFine is not null && ModMinecraft.CompareVersionGe(_vanillaName, "1.13") &&
                ModMinecraft.CompareVersionGe("1.14.3", _vanillaName))
                return "与 OptiFine 不兼容"; // 1.13 ~ 1.14.3 OptiFine 检查
            if (Conversions.ToBoolean(
                    SelectedOptiFine is not null && !(bool)IsOptiFineSuitForForge(SelectedOptiFine, Version)))
                continue;
            return null;
        }

        return "与 OptiFine 不兼容";
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
                if (Conversions.ToBoolean(SelectedOptiFine is not null &&
                                          !(bool)IsOptiFineSuitForForge(SelectedOptiFine, v)))
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
        SelectedForge = (ModDownload.DlForgeVersionEntry)(dynamic)sender.Tag;
        SelectedLoaderName = "Forge";
        CardForge.IsSwapped = true;
        if (Conversions.ToBoolean(SelectedOptiFine is not null &&
                                  !(bool)IsOptiFineSuitForForge(SelectedOptiFine, SelectedForge)))
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
            return "与 OptiFine 不兼容";
        if (SelectedLoaderName is not null && !ReferenceEquals(SelectedLoaderName, "NeoForge"))
            return $"与 {SelectedLoaderName} 不兼容";
        // 检查 Loader
        if (GetLoaderError(LoadNeoForge) is not null)
            return GetLoaderError(LoadNeoForge);
        // 检查版本
        return ModDownload.DlNeoForgeListLoader.Output.Value.Any(v => (v.Inherit ?? "") == (_vanillaName ?? ""))
            ? null
            : "无可用版本";
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
        SelectedNeoForge = (ModDownload.DlNeoForgeListEntry)(dynamic)sender.Tag;
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
    private string? LoadCleanroomGetError()
    {
        if (!_vanillaName.StartsWith("1."))
            return "没有可用版本";
        if (SelectedOptiFine is not null)
            return "与 OptiFine 不兼容";
        if (SelectedLoaderName is not null && !ReferenceEquals(SelectedLoaderName, "Cleanroom"))
            return $"与 {SelectedLoaderName} 不兼容";
        // 检查 Loader
        if (GetLoaderError(LoadCleanroom) is not null)
            return GetLoaderError(LoadNeoForge);
        // 检查版本
        return ModDownload.DlCleanroomListLoader.Output.Value.Any(v => (v.Inherit ?? "") == (_vanillaName ?? ""))
            ? null
            : "无可用版本";
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
        SelectedCleanroom = (ModDownload.DlCleanroomListEntry)(dynamic)sender.Tag;
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
            return "与 OptiFine 不兼容";
        // 检查 Loader
        if (GetLoaderError(LoadFabric) is not null)
            return GetLoaderError(LoadFabric);
        // 检查版本
        foreach (JObject version in ModDownload.DlFabricListLoader.Output.Value["game"])
            if ((version["version"].ToString() ?? "") ==
                (_vanillaName.Replace("∞", "infinite").Replace("Combat Test 7c", "1.16_combat-3") ?? ""))
            {
                if (SelectedLoaderName is not null && !ReferenceEquals(SelectedLoaderName, "Fabric"))
                    return $"与 {SelectedLoaderName} 不兼容";
                return null;
            }

        return "无可用版本";
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
            var versions = (JArray)ModDownload.DlFabricListLoader.Output.Value["loader"];
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
                        ModDownloadLib.FabricDownloadListItem((JObject)item,
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
        ModBase.Log(((dynamic)sender.Tag).ToString());
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
            var targetName = _vanillaName.Replace("∞", "infinite").Replace("Combat Test 7c", "1.16_combat-3").ToLower();
            return fabricApi.RawGameVersions.Any(f => f == targetName);
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
            return SelectedFabric is null && SelectedQuilt is null ? "需要安装 Fabric" : "获取中……";
        // 检查版本
        if (ModDownload.DlFabricApiLoader.Output.Any(f => IsFabricApiCompatible(f)))
            return SelectedFabric is null && SelectedQuilt is null ? "需要安装 Fabric" : null;

        return "无可用版本";
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
                    ModDownloadLib.FabricApiDownloadListItem(version, (a, b) => this.Fabric_Selected((dynamic)a, b)));
            }

            // 自动选择 Fabric API
            if ((!AutoSelectedFabricApi && SelectedQuilt is null) ||
                (SelectedQuilt is not null && ReferenceEquals(LoadQSLGetError(), "没有可用版本")))
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
        SelectedFabricApi = (ModComp.CompFile)(dynamic)sender.Tag;
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
            return "加载中……";
        if (LoadLegacyFabric.State.LoadingState == MyLoading.MyLoadingState.Error)
            return Conversions.ToString(Operators.ConcatenateObject("获取版本列表失败：",
                ((dynamic)LoadLegacyFabric.State).Error.Message));
        foreach (JObject Version in ModDownload.DlLegacyFabricListLoader.Output.Value["game"])
            if ((Version["version"].ToString() ?? "") == (_vanillaName ?? ""))
            {
                if (SelectedLiteLoader is not null)
                    return "与 LiteLoader 不兼容";
                if (SelectedLoaderName is not null && !ReferenceEquals(SelectedLoaderName, "LegacyFabric"))
                    return $"与 {SelectedLoaderName} 不兼容";
                return null;
            }

        return "无可用版本";
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
            var Versions = (JArray)ModDownload.DlLegacyFabricListLoader.Output.Value["loader"];
            if (!Versions.Any())
                return;
            // 可视化
            PanLegacyFabric.Children.Clear();
            PanLegacyFabric.Tag = Versions;
            CardLegacyFabric.SwapControl = PanLegacyFabric;
            CardLegacyFabric.InstallMethod = Stack =>
            {
                foreach (var item in (IEnumerable)Stack.Tag)
                    Stack.Children.Add(ModDownloadLib.LegacyFabricDownloadListItem((JObject)item,
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
        SelectedLegacyFabric = ((dynamic)sender.Tag)["version"].ToString();
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
            return "加载中……";
        if (LoadLegacyFabricApi.State.LoadingState == MyLoading.MyLoadingState.Error)
            return Conversions.ToString(Operators.ConcatenateObject("获取版本列表失败：",
                ((dynamic)LoadLegacyFabricApi.State).Error.Message));
        if (SelectedAPIName is not null && !ReferenceEquals(SelectedAPIName, "Legacy Fabric API"))
            return $"与 {SelectedAPIName} 不兼容";
        if (ModDownload.DlLegacyFabricApiLoader.Output is null)
        {
            if (SelectedLegacyFabric is null)
                return "需要安装 LegacyFabric";
            return "加载中……";
        }

        foreach (var Version in ModDownload.DlLegacyFabricApiLoader.Output)
        {
            if (!IsSuitableLegacyFabricApi(Version.GameVersions, _vanillaName))
                continue;
            if (SelectedLegacyFabric is null)
                return "需要安装 LegacyFabric";
            return null;
        }

        return "无可用版本";
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
                (SelectedQuilt is not null && ReferenceEquals(LoadQSLGetError(), "没有可用版本")))
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
        SelectedLegacyFabricApi = (ModComp.CompFile)(dynamic)sender.Tag;
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
            return "与 OptiFine 不兼容";
        if (SelectedLoaderName is not null && !ReferenceEquals(SelectedLoaderName, "Quilt"))
            return $"与 {SelectedLoaderName} 不兼容";
        // 检查 Loader
        if (GetLoaderError(LoadQuilt) is not null)
            return GetLoaderError(LoadQuilt);
        // 检查版本
        foreach (JObject version in ModDownload.DlQuiltListLoader.Output.Value["game"])
            if ((version["version"].ToString() ?? "") ==
                (_vanillaName.Replace("∞", "infinite").Replace("Combat Test 7c", "1.16_combat-3") ?? ""))
            {
                if (SelectedLoaderName is not null && !ReferenceEquals(SelectedLoaderName, "Fabric"))
                    return $"与 {SelectedLoaderName} 不兼容";
                return null;
            }

        return "无可用版本";
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
            var Versions = (JArray)ModDownload.DlQuiltListLoader.Output.Value["loader"];
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
                        ModDownloadLib.QuiltDownloadListItem((JObject)item,
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
            return "正在获取版本列表……";
        if (LoadQSL.State.LoadingState == MyLoading.MyLoadingState.Error)
            return Conversions.ToString(Operators.ConcatenateObject("获取版本列表失败：",
                ((dynamic)LoadQSL.State).Error.Message));
        if (SelectedAPIName is not null && !ReferenceEquals(SelectedAPIName, "QFAPI / QSL"))
            return $"与 {SelectedAPIName} 不兼容";
        if (ModDownload.DlQSLLoader.Output is null)
        {
            if (SelectedQuilt is null)
                return "需要安装 Quilt";
            return "正在获取版本列表……";
        }

        foreach (var Version in ModDownload.DlQSLLoader.Output)
        {
            if (!IsSuitableQSL(Version.GameVersions, _vanillaName))
                continue;
            if (SelectedQuilt is null)
                return "需要安装 Quilt";
            return null;
        }

        return "没有可用版本";
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
        SelectedQSL = (ModComp.CompFile)(dynamic)sender.Tag;
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
            return "不兼容老版本 Fabric，请手动下载 OptiFabric Origins";
        // 检查 Loader
        if (GetLoaderError(LoadOptiFabric) is not null)
            return GetLoaderError(LoadOptiFabric);
        // 检查版本
        if (ModDownload.DlOptiFabricLoader.Output is null)
        {
            if (SelectedFabric is null && SelectedOptiFine is null)
                return "需要安装 OptiFine 与 Fabric";
            if (SelectedFabric is null)
                return "需要安装 Fabric";
            if (SelectedOptiFine is null)
                return "需要安装 OptiFine";
            return "获取中……";
        }

        foreach (var version in ModDownload.DlOptiFabricLoader.Output)
        {
            if (!IsOptiFabricCompatible(version))
                continue; // 2135#
            if (SelectedFabric is null && SelectedOptiFine is null)
                return "需要安装 OptiFine 与 Fabric";
            if (SelectedFabric is null)
                return "需要安装 Fabric";
            if (SelectedOptiFine is null)
                return "需要安装 OptiFine";
            return null; // 通过检查
        }

        return "无可用版本";
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
        SelectedOptiFabric = (ModComp.CompFile)(dynamic)sender.Tag;
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
            return "加载中……";
        if (LoadLabyMod.State.LoadingState == MyLoading.MyLoadingState.Error)
            return Conversions.ToString(Operators.ConcatenateObject("获取版本列表失败：",
                ((dynamic)LoadLabyMod.State).Error.Message));
        // 检查 Loader
        if (GetLoaderError(LoadLabyMod) is not null)
            return GetLoaderError(LoadLabyMod);
        if (SelectedOptiFine is not null)
            return "与 OptiFine 不兼容";
        if (SelectedLoaderName is not null && !ReferenceEquals(SelectedLoaderName, "LabyMod"))
            return $"与 {SelectedLoaderName} 不兼容";
        foreach (JObject Version in ModDownload.DlLabyModListLoader.Output.Value["production"]["minecraftVersions"])
            if ((Version["version"].ToString() ?? "") == (_vanillaName ?? ""))
                return null;
        foreach (JObject Version in ModDownload.DlLabyModListLoader.Output.Value["snapshot"]["minecraftVersions"])
            if ((Version["version"].ToString() ?? "") == (_vanillaName ?? ""))
                return null;
        return "无可用版本";
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
            var ProcessedVersions = new JArray();
            foreach (JObject Production in Versions["production"]["minecraftVersions"])
                if ((Production["version"].ToString() ?? "") == (_vanillaName ?? ""))
                {
                    var ProductionVersion = new JObject();
                    ProductionVersion.Add("version", Versions["production"]["labyModVersion"]);
                    ProductionVersion.Add("channel", "production");
                    ProductionVersion.Add("commitReference", Versions["production"]["commitReference"]);
                    ProcessedVersions.Add(ProductionVersion);
                }

            foreach (JObject Snapshot in Versions["snapshot"]["minecraftVersions"])
                if ((Snapshot["version"].ToString() ?? "") == (_vanillaName ?? ""))
                {
                    var SnapshotVersion = new JObject();
                    SnapshotVersion.Add("version", Versions["production"]["labyModVersion"]);
                    SnapshotVersion.Add("channel", "snapshot");
                    SnapshotVersion.Add("commitReference", Versions["snapshot"]["commitReference"]);
                    ProcessedVersions.Add(SnapshotVersion);
                }

            // MyMsgBox(If(ProcessedVersions.ToString, "Nothing"))
            PanLabyMod.Children.Clear();
            PanLabyMod.Tag = ProcessedVersions;
            CardLabyMod.SwapControl = PanLabyMod;
            CardLabyMod.InstallMethod = Stack =>
            {
                foreach (JObject item in (IEnumerable)Stack.Tag)
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
        SelectedLabyModChannel = ((dynamic)sender.Tag)["channel"].ToString();
        SelectedLabyModCommitRef = ((dynamic)sender.Tag)["commitReference"].ToString();
        SelectedLabyModVersion =
            ((dynamic)sender.Tag)["version"].ToString() + (SelectedLabyModChannel == "snapshot" ? " 快照版" : " 稳定版");
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

    #region 安装

    private void TextSelectName_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && BtnStart.IsEnabled)
            BtnStart_Click();
    }

    private void BtnStart_Click()
    {
        // 确认版本隔离
        if (SelectedLoaderName is not null &&
            (Conversions.ToBoolean(
                 Operators.ConditionalCompareObjectEqual(Config.Launch.IndieSolutionV2, 0, false)) ||
             Conversions.ToBoolean(
                 Operators.ConditionalCompareObjectEqual(Config.Launch.IndieSolutionV2, 2, false))))
            if (ModMain.MyMsgBox(
                    "你尚未开启版本隔离，多个 MC 实例会共用同一个 Mod 文件夹。" + "\r\n" + "因此，游戏可能会因为读取到与当前实例不符的 Mod 而崩溃。" +
                    "\r\n" + "推荐先在 设置 → 启动选项 → 默认版本隔离 中开启版本隔离！", "版本隔离提示", "取消下载", "继续") == 1)
                return;

        // 提交安装申请
        var instanceName = TextSelectName.Text;
        var request = new ModDownloadLib.McInstallRequest
        {
            TargetInstanceName = instanceName,
            TargetInstanceFolder = $@"{ModMinecraft.McFolderSelected}versions\{instanceName}\",
            MinecraftJson = _vanillaData?["url"].ToString(),
            MinecraftName = _vanillaName,
            OptiFineEntry = SelectedOptiFine,
            ForgeEntry = SelectedForge,
            NeoForgeEntry = SelectedNeoForge,
            CleanroomEntry = SelectedCleanroom,
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
        if (!ModDownloadLib.McInstall(request))
            return;
        // 返回，这样在再次进入安装页面时这个实例就会显示文件夹已重复
        ExitSelectPage();
    }

    #endregion
}
