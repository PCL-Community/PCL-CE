using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.Core.Utils.OS;
using PCL.Core.Utils.Validate;
using PCL.Navigation;
using PCL.UI.Controls;
using static PCL.Animations;

namespace PCL.Views.Pages.Setup;

/// <summary>
///     启动设置页：实例隔离、窗口标题/大小/优先级、内存分配与 JVM/游戏参数。
///     状态与配置写入由 <see cref="PageSetupLaunchViewModel" /> 负责，页面仅处理视图生命周期、
///     用户选择确认、内存占用条动画与动态文本排布。
/// </summary>
public partial class PageSetupLaunch : MyPageRight, ISetupResetPage
{
    private readonly DispatcherTimer _ramTimer = new()
    {
        Interval = TimeSpan.FromSeconds(1)
    };

    private bool _isActive;
    private bool _loaded;
    private int _ramTextLeft = 2;
    private int _ramTextRight = 1;
    private bool _syncing;

    public PageSetupLaunch()
    {
        InitializeComponent();
        PanScroll = PanBack;
        DataContext = ViewModel;

        CheckAdvanceNoJavaw.IsEnabled = OperatingSystem.IsWindows();
        if (!OperatingSystem.IsWindows())
            ToolTip.SetTip(
                CheckAdvanceNoJavaw,
                Lang.Text("Setup.Launch.Advanced.UseJavaExe.Unsupported"));

        SliderRamCustom.getHintText = new Func<object, object>(value =>
            Lang.Number(PageSetupLaunchViewModel.GetRamFromTick(Convert.ToInt32(value)), "N1")
            + " GiB");
        SliderRamInitialCustom.getHintText = new Func<object, object>(value =>
            Lang.Number(PageSetupLaunchViewModel.GetRamFromTick(Convert.ToInt32(value)), "N1")
            + " GiB");

        TextArgumentInfo.ValidateRules.Add(new BlacklistValidator(["\"", "\u201c", "\u201d"]));
        TextArgumentInfo.ValidatedTextChanged += (_, _) =>
        {
            if (!_syncing)
                ViewModel.TypeInfoText = TextArgumentInfo.Text ?? string.Empty;
        };
        TextArgumentWindowWidth.ValidateRules.Add(new IntValidator(9999, 1));
        TextArgumentWindowHeight.ValidateRules.Add(new IntValidator(9999, 1));

        RadioRamType0.Check += (_, _) => WriteRamType(0);
        RadioRamType1.Check += (_, _) => WriteRamType(1);
        SliderRamCustom.Change += (_, _) => RefreshRamVisual(false);
        SliderRamInitialCustom.Change += (_, _) => RefreshRamVisual(false);

        BtnAdvanceJvmReset.Click += (_, _) => ResetJvmArgs();
        BtnSwitch.Click += (_, _) => SwitchToInstanceSetup();

        _ramTimer.Tick += (_, _) => RefreshRamVisual(true);
        PanRamDisplay.SizeChanged += (_, _) => RefreshRamText();
        LabRamGame.SizeChanged += (_, _) => RefreshRamText();
        LabRamGameTitle.SizeChanged += (_, _) => RefreshRamText();
        LabRamUsed.SizeChanged += (_, _) => RefreshRamText();
        LabRamTotal.SizeChanged += (_, _) => RefreshRamText();
        LabRamUsedTitle.SizeChanged += (_, _) => RefreshRamText();

        Loaded += (_, _) =>
        {
            _loaded = true;
            _isActive = true;
            Reload();
            _ramTimer.Start();
        };
        Unloaded += (_, _) =>
        {
            _ramTimer.Stop();
            _isActive = false;
        };
    }

    public PageSetupLaunchViewModel ViewModel { get; } = new();

    public void Reset()
    {
        try
        {
            Config.Launch.Reset();
            HintService.Show(Hints.Success(Lang.Text("Setup.Launch.Initialized"), false));
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Setup.Launch.Error.InitFailed"),
                LauncherLogLevel.Msgbox,
                userSummary: Lang.Text("Setup.Launch.Error.InitFailed"));
        }

        Reload();
    }

    public override void OnNavigatedTo(object? parameter)
    {
        PanBack.ScrollToHome();
        _isActive = true;
        ViewModel.RefreshInstanceState();
        if (_loaded) Reload();
        RefreshRamVisual(false);
    }

    public override void OnNavigatedFrom()
    {
        _isActive = false;
    }

    protected override void RefreshLocalization()
    {
        if (!_loaded) return;
        RefreshDisableJlwToolTip();
        Reload();
        RefreshRamVisual(false);
    }

    public void Reload()
    {
        _syncing = true;
        try
        {
            ViewModel.Reload();
            var ramType = Math.Clamp(ViewModel.MemoryAllocationMode, 0, 1);
            (ramType == 0 ? RadioRamType0 : RadioRamType1).SetChecked(true, false);
            RefreshDisableJlwToolTip();
            RefreshRamVisual(false);
        }
        catch (NullReferenceException ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Setup.Launch.Error.ConfigReset"),
                LauncherLogLevel.Msgbox,
                userSummary: Lang.Text("Setup.Launch.Error.ConfigReset"));
            Config.Launch.Reset();
            ViewModel.Reload();
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Setup.Launch.Error.LoadFailed"),
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Setup.Launch.Error.LoadFailed"));
        }
        finally
        {
            _syncing = false;
        }
    }

    private void WriteRamType(int type)
    {
        if (_syncing) return;
        ViewModel.MemoryAllocationMode = type;
        RefreshRamVisual(false);
    }

    #region 确认框

    private async void ComboArgumentIndie_OnSelectionChanged(
        object? sender,
        SelectionChangedEventArgs e)
    {
        if (_syncing || ComboArgumentIndieV2.SelectedIndex < 0) return;
        var previousValue = Config.Launch.IndieSolutionV2;
        var selected = ComboArgumentIndieV2.SelectedIndex;
        if (selected == previousValue || !IsUserSelecting(ComboArgumentIndieV2)) return;
        ViewModel.IndieSolutionIndex = selected;
        try
        {
            await ViewModel.ConfirmInstanceIsolationAsync();
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "实例隔离提示显示失败",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Setup.Error.OperationFailed"));
        }
    }

    private async void ComboArgumentVisibie_SelectionChanged(
        object? sender,
        SelectionChangedEventArgs e)
    {
        if (_syncing || ComboArgumentVisibie.SelectedIndex < 0) return;
        var previousValue = (int)Config.Launch.LauncherVisibility;
        var selected = ComboArgumentVisibie.SelectedIndex;
        if (selected == previousValue || !IsUserSelecting(ComboArgumentVisibie)) return;
        if (selected != 0)
        {
            ViewModel.VisibilityIndex = selected;
            return;
        }

        try
        {
            if (await ViewModel.ConfirmCloseImmediatelyAsync())
            {
                ViewModel.VisibilityIndex = selected;
                return;
            }
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "关闭窗口可见性确认失败",
                LauncherLogLevel.Msgbox,
                userSummary: Lang.Text("Setup.Error.OperationFailed"));
        }

        _syncing = true;
        try
        {
            ComboArgumentVisibie.SelectedIndex = previousValue;
        }
        finally
        {
            _syncing = false;
        }
    }

    private async void ComboAdvanceRenderer_OnSelectionChanged(
        object? sender,
        SelectionChangedEventArgs e)
    {
        if (_syncing || ComboAdvanceRenderer.SelectedIndex < 0) return;
        var previousValue = Config.Launch.Renderer;
        var selected = ComboAdvanceRenderer.SelectedIndex;
        if (selected == previousValue || !IsUserSelecting(ComboAdvanceRenderer)) return;

        if (!States.Hint.Renderer && selected != 0)
        {
            try
            {
                if (await ViewModel.ConfirmRendererAsync())
                {
                    States.Hint.Renderer = true;
                    ViewModel.RendererIndex = selected;
                    return;
                }
            }
            catch (Exception ex)
            {
                LauncherCommon.Log(
                    ex,
                    "渲染器可用性提示显示失败",
                    LauncherLogLevel.Feedback,
                    userSummary: Lang.Text("Setup.Error.OperationFailed"));
            }

            _syncing = true;
            try
            {
                ComboAdvanceRenderer.SelectedIndex = previousValue;
            }
            finally
            {
                _syncing = false;
            }

            return;
        }

        ViewModel.RendererIndex = selected;
    }

    /// <summary>判断下拉框的选中变更是否来自用户操作，而非页面刷新时的绑定回写。</summary>
    private static bool IsUserSelecting(ComboBox combo)
    {
        return combo.IsDropDownOpen || combo.IsKeyboardFocusWithin;
    }

    #endregion

    #region 内存可视化

    private void RefreshRamVisual(bool animate)
    {
        if (!_isActive) return;
        var state = ViewModel.BuildRamVisual();
        if (state is null) return;
        SliderRamCustom.MaxValue = state.MaxTick;
        if (animate)
            AnimateRamColumns(state);
        else
            SetRamColumnWidths(state);
        Dispatcher.UIThread.Post(RefreshRamText, DispatcherPriority.Loaded);
    }

    private void AnimateRamColumns(RamVisualState state)
    {
        var columns = PanRamDisplay.ColumnDefinitions;
        const string animationKey = "SetupLaunch Ram Grid";
        if (ContainerAnimation.CanAnimate(this))
        {
            AniStart(
                new[]
                {
                    AaGridLengthWidth(
                        columns[0],
                        state.Used - columns[0].Width.Value,
                        800,
                        ease: new AniEaseOutFluent(AniEasePower.Strong)),
                    AaGridLengthWidth(
                        columns[1],
                        state.GameActual - columns[1].Width.Value,
                        800,
                        ease: new AniEaseOutFluent(AniEasePower.Strong)),
                    AaGridLengthWidth(
                        columns[2],
                        Math.Max(state.Empty, 0.001d) - columns[2].Width.Value,
                        800,
                        ease: new AniEaseOutFluent(AniEasePower.Strong))
                },
                animationKey);
            return;
        }

        AniStop(animationKey);
        SetRamColumnWidths(state);
    }

    private void SetRamColumnWidths(RamVisualState state)
    {
        var columns = PanRamDisplay.ColumnDefinitions;
        columns[0].Width = new GridLength(state.Used, GridUnitType.Star);
        columns[1].Width = new GridLength(state.GameActual, GridUnitType.Star);
        columns[2].Width = new GridLength(Math.Max(state.Empty, 0.001d), GridUnitType.Star);
    }

    /// <summary>按已用内存条宽度动态调整右侧文本位置，避免与已用内存文本重叠。</summary>
    private void RefreshRamText()
    {
        if (!_isActive || PanRamDisplay.Bounds.Width <= 0d) return;

        var rectUsedWidth = RectRamUsed.Bounds.Width;
        var totalWidth = PanRamDisplay.Bounds.Width;
        var labGameWidth = LabRamGame.Bounds.Width;
        var labUsedWidth = LabRamUsed.Bounds.Width;
        var labTotalWidth = LabRamTotal.Bounds.Width;
        var labGameTitleWidth = LabRamGameTitle.Bounds.Width;
        var labUsedTitleWidth = LabRamUsedTitle.Bounds.Width;

        int left;
        if (rectUsedWidth - 30d < labUsedWidth || rectUsedWidth - 30d < labUsedTitleWidth)
            left = 0;
        else if (rectUsedWidth - 25d < labUsedWidth + labTotalWidth)
            left = 1;
        else
            left = 2;
        if (_ramTextLeft != left)
        {
            _ramTextLeft = left;
            var usedOpacity = left switch
            {
                0 => 0d,
                1 => 1d,
                _ => 1d
            };
            var totalOpacity = left == 0 ? 0d : 1d;
            var titleOpacity = left == 0 ? 0d : 0.7d;
            AniStart(
                new[]
                {
                    AaOpacity(LabRamUsed, usedOpacity - LabRamUsed.Opacity, 100),
                    AaOpacity(LabRamTotal, totalOpacity - LabRamTotal.Opacity, 100),
                    AaOpacity(LabRamUsedTitle, titleOpacity - LabRamUsedTitle.Opacity, 100)
                },
                "SetupLaunch Ram TextLeft");
        }

        int right;
        if (totalWidth < labGameWidth + 2d + rectUsedWidth
            || totalWidth < labGameTitleWidth + 2d + rectUsedWidth)
            right = 0;
        else
            right = 1;

        var targetGameLeft = right == 0
            ? totalWidth - labGameWidth
            : 2d + rectUsedWidth;
        var targetTitleLeft = right == 0
            ? totalWidth - labGameTitleWidth
            : 2d + rectUsedWidth;

        if (ContainerAnimation.CanAnimate(this)
            && (right != _ramTextRight || AniIsRun("SetupLaunch Ram TextRight")))
        {
            AniStart(
                new[]
                {
                    AaX(
                        LabRamGame,
                        targetGameLeft - LabRamGame.Margin.Left,
                        100,
                        ease: new AniEaseOutFluent(AniEasePower.Weak)),
                    AaX(
                        LabRamGameTitle,
                        targetTitleLeft - LabRamGameTitle.Margin.Left,
                        100,
                        ease: new AniEaseOutFluent(AniEasePower.Weak))
                },
                "SetupLaunch Ram TextRight");
        }
        else
        {
            AniStop("SetupLaunch Ram TextRight");
            LabRamGame.Margin = new Thickness(targetGameLeft, 3d, 0d, 0d);
            LabRamGameTitle.Margin = new Thickness(targetTitleLeft, 0d, 0d, 5d);
        }

        _ramTextRight = right;
    }

    #endregion

    #region 高级设置

    private void RefreshDisableJlwToolTip()
    {
        if (!_loaded) return;
        ToolTip.SetTip(
            CheckAdvanceDisableJLW,
            Lang.Text(
                SystemInfo.IsArm64System
                    ? "Setup.Launch.Advanced.DisableJlw.Arm64Notice"
                    : "Setup.Launch.Advanced.DisableJlw.ToolTip"));
        ToolTip.SetShowOnDisabled(CheckAdvanceDisableJLW, true);
    }

    private void ResetJvmArgs()
    {
        try
        {
            ConfigService.GetConfigItem<string>("LaunchAdvanceJvm").Reset();
            Reload();
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "恢复默认 JVM 参数失败");
        }
    }

    private static void SwitchToInstanceSetup()
    {
        var instance = InstanceList.McMcInstanceSelected;
        if (instance is null) return;
        PageRouter.Navigate(PageType.InstanceSetup, PageSubType.VersionSetup, instance);
    }

    #endregion
}