using Avalonia.Controls;
using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.Utils.OS;
using PCL.Core.Utils.Validate;
using PCL.Navigation;
using PCL.UI.Controls;
using static PCL.Animations;

namespace PCL.Views.Pages.Setup;

/// <summary>
///     启动设置页：实例隔离、窗口标题/大小/优先级、内存分配与 JVM/游戏参数。
///     内存分配写 <c>Config.Launch</c>；内存显示按当前配置实时计算（与实例设置页共享计算规则）。
/// </summary>
public partial class PageSetupLaunch : MyPageRight, ISetupResetPage
{
    private readonly DispatcherTimer _ramTimer = new()
    {
        Interval = TimeSpan.FromSeconds(1)
    };

    private bool _isActive;
    private bool _loaded;
    private bool _reloading;

    public PageSetupLaunch()
    {
        InitializeComponent();
        PanScroll = PanBack;

        SliderRamCustom.getHintText = new Func<object, object>(value =>
            Lang.Number(GetRamFromTick(Convert.ToInt32(value)), "N1") + " GiB");
        SliderRamInitialCustom.getHintText = new Func<object, object>(value =>
            Lang.Number(GetRamFromTick(Convert.ToInt32(value)), "N1") + " GiB");

        TextArgumentInfo.ValidateRules.Add(new BlacklistValidator(["\"", "\u201c", "\u201d"]));
        TextArgumentWindowWidth.ValidateRules.Add(new IntValidator(9999, 1));
        TextArgumentWindowHeight.ValidateRules.Add(new IntValidator(9999, 1));

        RadioRamType0.Check += (_, _) => WriteRamType(0);
        RadioRamType1.Check += (_, _) => WriteRamType(1);
        SliderRamCustom.Change += (_, _) => WriteIfReady(() =>
        {
            Config.Launch.CustomMemorySize = SliderRamCustom.Value;
            RefreshRamVisual(false);
        });
        SliderRamInitialCustom.Change += (_, _) => WriteIfReady(() =>
        {
            Config.Launch.CustomInitialMemorySize = SliderRamInitialCustom.Value;
            RefreshRamVisual(false);
        });
        CheckAdvanceLockMemory.Change += (_, _) => WriteIfReady(() =>
        {
            Config.Launch.LockMemory = CheckAdvanceLockMemory.Checked == true;
            RefreshRamControls();
            RefreshRamVisual(false);
        });

        ComboArgumentIndieV2.SelectionChanged += (_, _) =>
            WriteIfReady(() => Config.Launch.IndieSolutionV2 = ComboArgumentIndieV2.SelectedIndex);
        ComboArgumentTitle.TextChanged += (_, _) =>
            WriteIfReady(() => Config.Launch.Title = ComboArgumentTitle.Text ?? string.Empty);
        TextArgumentInfo.TextChanged += (_, _) =>
            WriteIfReady(() => Config.Launch.TypeInfo = TextArgumentInfo.Text ?? string.Empty);
        ComboArgumentVisibie.SelectionChanged += (_, _) => WriteIfReady(() =>
        {
            if (ComboArgumentVisibie.SelectedIndex >= 0)
                Config.Launch.LauncherVisibility =
                    (LauncherVisibility)ComboArgumentVisibie.SelectedIndex;
        });
        ComboArgumentPriority.SelectionChanged += (_, _) => WriteIfReady(() =>
        {
            if (ComboArgumentPriority.SelectedItem is MyComboBoxItem { Tag: string tag }
                && int.TryParse(tag, out var value))
                Config.Launch.ProcessPriority = (GameProcessPriority)value;
        });
        ComboArgumentWindowType.SelectionChanged += (_, _) => WriteIfReady(() =>
        {
            if (ComboArgumentWindowType.SelectedIndex < 0) return;
            Config.Launch.GameWindowMode =
                (GameWindowSizeMode)ComboArgumentWindowType.SelectedIndex;
            RefreshWindowSizeVisibility();
        });
        TextArgumentWindowWidth.TextChanged += (_, _) => WriteWindowSize();
        TextArgumentWindowHeight.TextChanged += (_, _) => WriteWindowSize();
        ComboMsAuthType.SelectionChanged += (_, _) => WriteIfReady(() =>
        {
            if (ComboMsAuthType.SelectedIndex >= 0)
                Config.Launch.LoginMsAuthType = ComboMsAuthType.SelectedIndex;
        });
        ComboPreferredIpStack.SelectionChanged += (_, _) => WriteIfReady(() =>
        {
            if (ComboPreferredIpStack.SelectedIndex >= 0)
                Config.Launch.PreferredIpStack =
                    (JvmPreferredIpStack)ComboPreferredIpStack.SelectedIndex;
        });

        // TODO(FIX-P3-15): WPF
        // 在用户切到非默认渲染器时弹一次提示（PageSetupLaunch.xaml.cs:672-700，States.Hint.Renderer），
        // 依赖 PCL.Core 的提示状态服务，本次未迁移。
        ComboAdvanceRenderer.SelectionChanged += (_, _) => WriteIfReady(() =>
        {
            if (ComboAdvanceRenderer.SelectedIndex >= 0)
                Config.Launch.Renderer = ComboAdvanceRenderer.SelectedIndex;
        });
        TextAdvanceJvm.TextChanged += (_, _) => WriteIfReady(() =>
        {
            Config.Launch.JvmArgs = TextAdvanceJvm.Text ?? string.Empty;
            RefreshJvmResetVisibility();
        });
        TextAdvanceGame.TextChanged += (_, _) =>
            WriteIfReady(() => Config.Launch.GameArgs = TextAdvanceGame.Text ?? string.Empty);
        TextAdvanceRun.TextChanged += (_, _) => WriteIfReady(() =>
            Config.Launch.PreLaunchCommand = TextAdvanceRun.Text ?? string.Empty);
        CheckAdvanceRunWait.Change += (_, _) => WriteIfReady(() =>
            Config.Launch.PreLaunchCommandWait = CheckAdvanceRunWait.Checked == true);
        CheckAdvanceDisableJLW.Change += (_, _) =>
            WriteIfReady(() => Config.Launch.DisableJlw = CheckAdvanceDisableJLW.Checked == true);
        CheckAdvanceDisableLF.Change += (_, _) =>
            WriteIfReady(() => Config.Launch.DisableLF = CheckAdvanceDisableLF.Checked == true);
        CheckAdvanceGraphicCard.Change += (_, _) => WriteIfReady(() =>
            Config.Launch.SetGpuPreference = CheckAdvanceGraphicCard.Checked == true);
        CheckAdvanceNoJavaw.Change += (_, _) =>
            WriteIfReady(() => Config.Launch.NoJavaw = CheckAdvanceNoJavaw.Checked == true);
        CheckAdvanceDisableLwjglUnsafeAgent.Change += (_, _) => WriteIfReady(() =>
            Config.Launch.DisableLwjglUnsafeAgent =
                CheckAdvanceDisableLwjglUnsafeAgent.Checked == true);
        CheckAdvanceDisableCrashAnalysis.Change += (_, _) => WriteIfReady(() =>
            Config.Launch.DisableCrashAnalysis = CheckAdvanceDisableCrashAnalysis.Checked == true);

        BtnAdvanceJvmReset.Click += (_, _) => ResetJvmArgs();
        BtnSwitch.Click += (_, _) => SwitchToInstanceSetup();

        _ramTimer.Tick += (_, _) => RefreshRamVisual(true);

        Loaded += (_, _) =>
        {
            if (_loaded) return;
            _loaded = true;
            Reload();
            _ramTimer.Start();
        };
        Unloaded += (_, _) => _ramTimer.Stop();
    }

    public void Reset()
    {
        Config.Launch.Reset();
        Reload();
    }

    public override void OnNavigatedTo(object? parameter)
    {
        PanBack.ScrollToHome();
        _isActive = true;
        if (_loaded) Reload();
        RefreshRamVisual(false);
    }

    public override void OnNavigatedFrom()
    {
        _isActive = false;
    }

    public void Reload()
    {
        _reloading = true;
        try
        {
            ComboArgumentIndieV2.SelectedIndex = Math.Clamp(Config.Launch.IndieSolutionV2, 0, 4);
            ComboArgumentTitle.Text = Config.Launch.Title;
            TextArgumentInfo.Text = Config.Launch.TypeInfo;
            ComboArgumentVisibie.SelectedIndex = Math.Clamp(
                (int)Config.Launch.LauncherVisibility,
                0,
                5);
            SelectPriorityItem((int)Config.Launch.ProcessPriority);
            ComboArgumentWindowType.SelectedIndex = Math.Clamp(
                (int)Config.Launch.GameWindowMode,
                0,
                4);
            TextArgumentWindowWidth.Text = Config.Launch.GameWindowWidth.ToString();
            TextArgumentWindowHeight.Text = Config.Launch.GameWindowHeight.ToString();
            RefreshWindowSizeVisibility();
            ComboMsAuthType.SelectedIndex = Math.Clamp(Config.Launch.LoginMsAuthType, 0, 1);
            ComboPreferredIpStack.SelectedIndex = Math.Clamp(
                (int)Config.Launch.PreferredIpStack,
                0,
                2);

            var ramType = Math.Clamp(Config.Launch.MemoryAllocationMode, 0, 1);
            var ramRadios = new[]
            {
                RadioRamType0,
                RadioRamType1
            };
            ramRadios[ramType].SetChecked(true, false);
            SliderRamCustom.Value = Config.Launch.CustomMemorySize;
            SliderRamInitialCustom.Value = Config.Launch.CustomInitialMemorySize;
            CheckAdvanceLockMemory.Checked = Config.Launch.LockMemory;
            RefreshRamControls();
            RefreshRamVisual(false);

            ComboAdvanceRenderer.SelectedIndex = Math.Clamp(Config.Launch.Renderer, 0, 3);
            TextAdvanceJvm.Text = Config.Launch.JvmArgs;
            TextAdvanceGame.Text = Config.Launch.GameArgs;
            TextAdvanceRun.Text = Config.Launch.PreLaunchCommand;
            CheckAdvanceRunWait.Checked = Config.Launch.PreLaunchCommandWait;
            CheckAdvanceDisableJLW.Checked = Config.Launch.DisableJlw;
            CheckAdvanceDisableLF.Checked = Config.Launch.DisableLF;
            CheckAdvanceGraphicCard.Checked = Config.Launch.SetGpuPreference;
            CheckAdvanceNoJavaw.Checked = Config.Launch.NoJavaw;
            CheckAdvanceDisableLwjglUnsafeAgent.Checked = Config.Launch.DisableLwjglUnsafeAgent;
            CheckAdvanceDisableCrashAnalysis.Checked = Config.Launch.DisableCrashAnalysis;
            CheckAdvanceDisableJLW.IsEnabled = !SystemInfo.IsArm64System;
            RefreshJvmResetVisibility();
        }
        finally
        {
            _reloading = false;
        }
    }

    private void WriteIfReady(Action action)
    {
        if (_reloading) return;
        try
        {
            action();
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "写入启动设置失败");
        }
    }

    private void WriteRamType(int type)
    {
        if (_reloading) return;
        Config.Launch.MemoryAllocationMode = type;
        RefreshRamControls();
        RefreshRamVisual(false);
    }

    private void RefreshRamControls()
    {
        var isCustom = Config.Launch.MemoryAllocationMode == 1;
        SliderRamCustom.IsEnabled = isCustom;
        SliderRamInitialCustom.IsEnabled = isCustom && !Config.Launch.LockMemory;
    }

    private void RefreshWindowSizeVisibility()
    {
        var isCustom = ComboArgumentWindowType.SelectedIndex == 3;
        TextArgumentWindowWidth.IsVisible = isCustom;
        TextArgumentWindowHeight.IsVisible = isCustom;
        LabArgumentWindowMiddle.IsVisible = isCustom;
    }

    private void WriteWindowSize()
    {
        if (_reloading) return;
        if (int.TryParse(TextArgumentWindowWidth.Text, out var width) && width is > 0 and <= 9999)
            Config.Launch.GameWindowWidth = width;
        if (int.TryParse(TextArgumentWindowHeight.Text, out var height)
            && height is > 0 and <= 9999)
            Config.Launch.GameWindowHeight = height;
    }

    private void SelectPriorityItem(int priority)
    {
        for (var i = 0; i < ComboArgumentPriority.ItemCount; i++)
        {
            if (ComboArgumentPriority.Items[i] is not MyComboBoxItem item) continue;
            if (item.Tag?.ToString() == priority.ToString())
            {
                ComboArgumentPriority.SelectedIndex = i;
                return;
            }
        }
    }

    private static double GetRamFromTick(int value)
    {
        return value switch
        {
            <= 12 => value * 0.1d + 0.3d,
            <= 25 => (value - 12) * 0.5d + 1.5d,
            <= 33 => value - 25 + 8d,
            _ => (value - 33) * 2d + 16d
        };
    }

    #region 内存可视化

    private void RefreshRamVisual(bool animate)
    {
        if (!_isActive) return;
        try
        {
            var instance = InstanceList.McMcInstanceSelected;
            var ramGame = Math.Round(MemorySettings.Current?.GetRam(instance!) ?? 0d, 5);
            var phyRam = KernelInterop.GetPhysicalMemoryBytes();
            var ramTotal = Math.Round(phyRam.Total / 1024d / 1024d / 1024d, 1);
            var ramAvailable = Math.Round(phyRam.Available / 1024d / 1024d / 1024d, 1);
            var ramGameActual = Math.Round(Math.Min(ramGame, ramAvailable), 5);
            var ramUsed = Math.Round(ramTotal - ramAvailable, 5);
            var ramEmpty = Math.Round(Math.Clamp(ramTotal - ramUsed - ramGame, 0d, 1000d), 1);

            SliderRamCustom.MaxValue = ramTotal switch
            {
                <= 1.5d => (int)Math.Round(Math.Max(Math.Floor((ramTotal - 0.3d) / 0.1d), 1d)),
                <= 8d => (int)Math.Round(Math.Floor((ramTotal - 1.5d) / 0.5d) + 12d),
                <= 16d => (int)Math.Round(Math.Floor((ramTotal - 8d) / 1d) + 25d),
                _ => (int)Math.Round(Math.Floor((ramTotal - 16d) / 2d) + 33d)
            };

            var ramInitial = MemorySettings.Current?.GetInitialRam(instance!);
            string suffixText;
            if (ramInitial.HasValue && ramGame != ramGameActual)
                suffixText = Lang.Text(
                    "Setup.Launch.Memory.SuffixBoth",
                    Lang.Number(ramInitial.Value, "N1"),
                    Lang.Number(ramGameActual, "N1"));
            else if (ramInitial.HasValue)
                suffixText = Lang.Text(
                    "Setup.Launch.Memory.SuffixInitial",
                    Lang.Number(ramInitial.Value, "N1"));
            else if (ramGame != ramGameActual)
                suffixText = Lang.Text(
                    "Setup.Launch.Memory.SuffixAvailable",
                    Lang.Number(ramGameActual, "N1"));
            else
                suffixText = string.Empty;

            LabRamGame.Text = Lang.Number(ramGame, "N1")
                + " GiB"
                + (suffixText.Length > 0 ? " (" + suffixText + ")" : string.Empty);
            LabRamUsed.Text = Lang.Number(ramUsed, "N1") + " GiB";
            LabRamTotal.Text = " / " + Lang.Number(ramTotal, "N1") + " GiB";

            LabRamWarn.IsVisible = ramGame == 1d
                && !JavaRuntime.IsGameSet64BitJava(instance)
                && !SystemInfo.Is32BitSystem
                && JavaRuntime.Javas.ExistAnyJava();
            HintRamTooHigh.IsVisible = ramGame / ramTotal > 0.75d;

            if (animate)
                AnimateRamColumns(ramUsed, ramGameActual, ramEmpty);
            else
                SetRamColumnWidths(ramUsed, ramGameActual, ramEmpty);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Setup", "[Setup] 计算内存分配失败，已跳过内存可视化刷新");
        }
    }

    private void AnimateRamColumns(double ramUsed, double ramGameActual, double ramEmpty)
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
                        ramUsed - columns[0].Width.Value,
                        800,
                        ease: new AniEaseOutFluent(AniEasePower.Strong)),
                    AaGridLengthWidth(
                        columns[1],
                        ramGameActual - columns[1].Width.Value,
                        800,
                        ease: new AniEaseOutFluent(AniEasePower.Strong)),
                    AaGridLengthWidth(
                        columns[2],
                        Math.Max(ramEmpty, 0.001d) - columns[2].Width.Value,
                        800,
                        ease: new AniEaseOutFluent(AniEasePower.Strong))
                },
                animationKey);
            return;
        }

        AniStop(animationKey);
        SetRamColumnWidths(ramUsed, ramGameActual, ramEmpty);
    }

    private void SetRamColumnWidths(double ramUsed, double ramGameActual, double ramEmpty)
    {
        var columns = PanRamDisplay.ColumnDefinitions;
        columns[0].Width = new GridLength(ramUsed, GridUnitType.Star);
        columns[1].Width = new GridLength(ramGameActual, GridUnitType.Star);
        columns[2].Width = new GridLength(Math.Max(ramEmpty, 0.001d), GridUnitType.Star);
    }

    #endregion

    #region 高级设置

    private void RefreshJvmResetVisibility()
    {
        try
        {
            var defaultArgs = ConfigService.GetConfigItem<string>("LaunchAdvanceJvm").DefaultValue;
            BtnAdvanceJvmReset.IsVisible = TextAdvanceJvm.Text != defaultArgs;
        }
        catch (Exception)
        {
            BtnAdvanceJvmReset.IsVisible = false;
        }
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

    private void SwitchToInstanceSetup()
    {
        var instance = InstanceList.McMcInstanceSelected;
        if (instance is null) return;
        PageRouter.Navigate(PageType.InstanceSetup, PageSubType.VersionSetup, instance);
    }

    #endregion
}