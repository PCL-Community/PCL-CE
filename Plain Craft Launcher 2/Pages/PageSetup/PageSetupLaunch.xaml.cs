using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.Utils.OS;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.Core.UI.Dialogs;

namespace PCL;

public partial class PageSetupLaunch : IPageView
{
    private readonly PageSetupLaunchViewModel _vm = new();
    private bool _refreshed;
    private bool isLoad;

    public PageSetupLaunch()
    {
        Loaded += PageSetupLaunch_Loaded;
        Unloaded += PageSetupLaunch_Unloaded;
        InitializeComponent();
        DataContext = _vm;
        DialogServiceInstaller.Install(_vm);
    }

    public PageSetupLaunchViewModel ViewModel => _vm;

    public void OnNavigatedTo(object? parameter) => Refresh();

    public void OnNavigatedFrom()
    {
    }

    private void PageSetupLaunch_Loaded(object sender, RoutedEventArgs e)
    {
        // 重复加载部分
        PanBack.ScrollToHome();
        RefreshRam(false);
        _vm.ShowSwitchButton = InstanceList.McMcInstanceSelected is not null;

        Refresh();

        // 非重复加载部分
        if (isLoad)
            return;
        isLoad = true;

        // 内存自动刷新
        var timer = new DispatcherTimer { Interval = new TimeSpan(0, 0, 0, 1) };
        timer.Tick += (_, _) => RefreshRam();
        timer.Start();
        RectRamGame.SizeChanged += (s, e) => RefreshRamText();
    }

    private void PageSetupLaunch_Unloaded(object sender, RoutedEventArgs e) => _refreshed = false;

    private void Refresh()
    {
        if (_refreshed)
            return;
        _refreshed = true;
        Animations.AniBeginControl();
        Reload();
        Animations.AniEndControl();
    }

    public void Reload()
    {
        Animations.AniBeginControl();
        try
        {
            // 启动参数
            TextArgumentTitle.Text = Config.Launch.Title;
            _vm.Reload();
            WindowTypeUIRefresh();

            // 游戏内存
            ((MyRadioBox)FindName("RadioRamType" + Config.Launch.MemoryAllocationMode)).Checked = true;
            SliderRamCustom.Value = Config.Launch.CustomMemorySize;
            SliderRamInitialCustom.Value = Config.Launch.CustomInitialMemorySize;
            RamType(Config.Launch.MemoryAllocationMode);

            // 高级设置
            CheckAdvanceRunWait.Checked = Config.Launch.PreLaunchCommandWait;
            CheckAdvanceDisableLF.Checked = Config.Launch.DisableLF;
            CheckAdvanceGraphicCard.Checked = Config.Launch.SetGpuPreference;
            CheckAdvanceNoJavaw.Checked = Config.Launch.NoJavaw;
            CheckAdvanceDisableLwjglUnsafeAgent.Checked = Config.Launch.DisableLwjglUnsafeAgent;
            CheckAdvanceDisableCrashAnalysis.Checked = Config.Launch.DisableCrashAnalysis;
            CheckAdvanceLockMemory.Checked = Config.Launch.LockMemory;
            if (SystemInfo.IsArm64System)
            {
                CheckAdvanceDisableJLW.Checked = true;
                CheckAdvanceDisableJLW.ToolTip = Lang.Text("Setup.Launch.Advanced.DisableJlw.Arm64Notice");
            }
            else
            {
                CheckAdvanceDisableJLW.Checked = Config.Launch.DisableJlw;
            }
        }

        catch (NullReferenceException ex)
        {
            Common.Log(
                ex,
                Lang.Text("Setup.Launch.Error.ConfigReset"),
                Common.LogLevel.Msgbox,
                userSummary: Lang.Text("Setup.Launch.Error.ConfigReset"));
            Reset();
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                Lang.Text("Setup.Launch.Error.LoadFailed"),
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Setup.Launch.Error.LoadFailed"));
        }
        finally
        {
            Animations.AniEndControl();
        }
    }

    // 初始化
    public void Reset()
    {
        try
        {
            Config.Launch.Reset();
            Common.Log("[Setup] 已初始化启动设置");
            HintService.Show(Hints.Success(Lang.Text("Setup.Launch.Initialized"), log: false));
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                Lang.Text("Setup.Launch.Error.InitFailed"),
                Common.LogLevel.Msgbox,
                userSummary: Lang.Text("Setup.Launch.Error.InitFailed"));
        }

        Reload();
    }

    // 将控件改变路由到设置改变
    private void RadioBoxChange(object senderRaw, Common.RouteEventArgs e)
    {
        var sender = (MyRadioBox)senderRaw;
        var gotCfg = sender.Tag?.ToString()?.Split("/") ?? Array.Empty<string>();
        if (!Animations.AniExitsAnyControl() && gotCfg.Length >= 2)
            SetByTag(gotCfg[0], int.Parse(gotCfg[1]));
    }

    private void TextBoxChange(object senderRaw, RoutedEventArgs e)
    {
        var sender = (MyTextBox)senderRaw;
        if (!Animations.AniExitsAnyControl())
            SetByTag(sender.Tag?.ToString(), sender.Text);
    }

    private void TextArgumentTitle_OnTextChanged(object senderRaw, TextChangedEventArgs e)
    {
        var sender = (MyTextBox)senderRaw;
        if (!Animations.AniExitsAnyControl())
            SetByTag(sender.Tag?.ToString(), sender.Text);
    }

    private void SliderChange(object senderRaw, bool user)
    {
        var sender = (MySlider)senderRaw;
        if (!Animations.AniExitsAnyControl())
            SetByTag(sender.Tag?.ToString(), sender.Value);
    }

    private void ComboChange(object senderRaw, SelectionChangedEventArgs e)
    {
        var sender = (MyComboBox)senderRaw;
        if (Animations.AniExitsAnyControl())
            return;
        var senderTag = sender.Tag?.ToString();
        if (string.IsNullOrEmpty(senderTag))
            return;
        // SelectedIndex < 0 表示下拉尚未完成初始化，此时不得写回配置
        switch (senderTag)
        {
            case "LoginMsAuthType":
                if (sender.SelectedIndex < 0)
                    return;
                SetByTag(senderTag, Math.Clamp(sender.SelectedIndex, 0, 1));
                return;
            case "LaunchArgumentPriority":
                if (sender.SelectedValue is null)
                    return;
                SetByTag(senderTag, Convert.ToInt32(sender.SelectedValue));
                return;
            default:
                if (sender.SelectedIndex < 0)
                    return;
                SetByTag(senderTag, sender.SelectedIndex);
                if (senderTag == "LaunchArgumentWindowType")
                    WindowTypeUIRefresh();
                return;
        }
    }

    private void CheckBoxChange(object senderRaw, bool user)
    {
        var sender = (MyCheckBox)senderRaw;
        if (!Animations.AniExitsAnyControl())
            SetByTag(sender.Tag?.ToString(), sender.Checked);
        // 锁定内存状态变化时同步刷新初始内存滑块的可用状态
        if (ReferenceEquals(sender, CheckAdvanceLockMemory))
            RamType(Config.Launch.MemoryAllocationMode);
    }

    private static void SetByTag(string tag, object value)
        => ConfigService.TrySetValue(tag, value);

    // 切换到实例独立设置
    private void BtnSwitch_Click(object sender, MouseButtonEventArgs e)
    {
        InstanceList.McMcInstanceSelected.Load();
        PageInstanceLeft.McInstance = InstanceList.McMcInstanceSelected;
        Launcher.frmMain.PageChange(FormMain.PageType.InstanceSetup, FormMain.PageSubType.VersionSetup);
    }

    private void ComboAdvanceRenderer_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ComboChange(sender, e);
        ComboAdvanceRenderer_SelectionChanged((MyComboBox)sender, e);
    }

    private void ComboArgumentIndie_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ComboChange(sender, e);
        ComboArgumentIndie_SelectionChanged(sender, e);
    }

    #region 游戏内存

    public void RamType(int type)
    {
        if (SliderRamCustom is null)
            return;
        // 锁定内存时 -Xms 恒等于 -Xmx，初始内存设置不生效
        _vm.RamType(type);
    }

    /// <summary>
    /// 刷新 UI 上的 RAM 显示。
    /// </summary>
    public void RefreshRam(bool showAnim)
    {
        if (LabRamGame is null || LabRamUsed is null || Launcher.frmMain.pageCurrent != FormMain.PageType.Setup ||
            Launcher.frmSetupLeft.pageID != FormMain.PageSubType.SetupLaunch)
            return;
        // 获取内存情况
        var ramGame = Math.Round(GetRam(InstanceList.McMcInstanceSelected, false), 5);
        var phyRam = KernelInterop.GetPhysicalMemoryBytes();
        var ramTotal = Math.Round((double)phyRam.Total / 1024 / 1024 / 1024, 1);
        var ramAvailable = Math.Round((double)phyRam.Available / 1024 / 1024 / 1024, 1);
        var ramGameActual = Math.Round(Math.Min(ramGame, ramAvailable), 5);
        var ramUsed = Math.Round(ramTotal - ramAvailable, 5);
        var ramEmpty = Math.Round(Common.MathClamp(ramTotal - ramUsed - ramGame, 0d, 1000d), 1);
        // 设置最大可用内存
        if (ramTotal <= 1.5d)
            SliderRamCustom.MaxValue = (int)Math.Round(Math.Max(Math.Floor((ramTotal - 0.3d) / 0.1d), 1d));
        else if (ramTotal <= 8d)
            SliderRamCustom.MaxValue = (int)Math.Round(Math.Floor((ramTotal - 1.5d) / 0.5d) + 12d);
        else if (ramTotal <= 16d)
            SliderRamCustom.MaxValue = (int)Math.Round(Math.Floor((ramTotal - 8d) / 1d) + 25d);
        else
            SliderRamCustom.MaxValue = (int)Math.Round(Math.Floor((ramTotal - 16d) / 2d) + 33d);
        // 设置文本
        var ramInitial = GetInitialRam(InstanceList.McMcInstanceSelected, false);
        string suffixText;
        if (ramInitial.HasValue && ramGame != ramGameActual)
            suffixText = Lang.Text("Setup.Launch.Memory.SuffixBoth", Lang.Number(ramInitial.Value, "N1"), Lang.Number(ramGameActual, "N1"));
        else if (ramInitial.HasValue)
            suffixText = Lang.Text("Setup.Launch.Memory.SuffixInitial", Lang.Number(ramInitial.Value, "N1"));
        else if (ramGame != ramGameActual)
            suffixText = Lang.Text("Setup.Launch.Memory.SuffixAvailable", Lang.Number(ramGameActual, "N1"));
        else
            suffixText = "";
        _vm.RamGameText = $"{Lang.Number(ramGame, "N1")} GiB{(suffixText.Length > 0 ? $" ({suffixText})" : "")}";
        _vm.RamUsedText = $"{Lang.Number(ramUsed, "N1")} GiB";
        _vm.RamTotalText = $" / {Lang.Number(ramTotal, "N1")} GiB";
        _vm.RamWarnVisible =
            ramGame == 1d && !JavaRuntime.IsGameSet64BitJava() && !SystemInfo.Is32BitSystem && JavaRuntime.Javas.ExistAnyJava();
        _vm.RamTooHighVisible = ramGame / ramTotal > 0.75d;
        if (showAnim)
        {
            // 宽度动画
            Animations.AniStart(
                new[]
                {
                    Animations.AaGridLengthWidth(ColumnRamUsed, ramUsed - ColumnRamUsed.Width.Value, 800,
                        ease: new Animations.AniEaseOutFluent(Animations.AniEasePower.Strong)),
                    Animations.AaGridLengthWidth(ColumnRamGame, ramGameActual - ColumnRamGame.Width.Value, 800,
                        ease: new Animations.AniEaseOutFluent(Animations.AniEasePower.Strong)),
                    Animations.AaGridLengthWidth(ColumnRamEmpty, ramEmpty - ColumnRamEmpty.Width.Value, 800,
                        ease: new Animations.AniEaseOutFluent(Animations.AniEasePower.Strong))
                }, "SetupLaunch Ram Grid");
        }
        else
        {
            // 宽度设置
            ColumnRamUsed.Width = new GridLength(ramUsed, GridUnitType.Star);
            ColumnRamGame.Width = new GridLength(ramGameActual, GridUnitType.Star);
            ColumnRamEmpty.Width = new GridLength(ramEmpty, GridUnitType.Star);
        }
    }

    private void RefreshRam()
    {
        RefreshRam(true);
    }

    private int ramTextLeft = 2;
    private int ramTextRight = 1;

    /// <summary>
    /// 刷新 UI 上的文本位置。
    /// </summary>
    private void RefreshRamText()
    {
        // 获取宽度信息
        var rectUsedWidth = RectRamUsed.ActualWidth;
        var totalWidth = PanRamDisplay.ActualWidth;
        var labGameWidth = LabRamGame.ActualWidth;
        var labUsedWidth = LabRamUsed.ActualWidth;
        var labTotalWidth = LabRamTotal.ActualWidth;
        var labGameTitleWidth = LabRamGameTitle.ActualWidth;
        var labUsedTitleWidth = LabRamUsedTitle.ActualWidth;
        // 左侧
        int left;
        if (rectUsedWidth - 30d < labUsedWidth || rectUsedWidth - 30d < labUsedTitleWidth)
            // 全写不下了
            left = 0;
        else if (rectUsedWidth - 25d < labUsedWidth + labTotalWidth)
            // 显示不下完整数据
            left = 1;
        else
            // 正常
            left = 2;
        if (ramTextLeft != left)
        {
            ramTextLeft = left;
            switch (left)
            {
                case 0:
                {
                    Animations.AniStart(
                        new[]
                        {
                            Animations.AaOpacity(LabRamUsed, -LabRamUsed.Opacity, 100),
                            Animations.AaOpacity(LabRamTotal, -LabRamTotal.Opacity, 100),
                            Animations.AaOpacity(LabRamUsedTitle, -LabRamUsedTitle.Opacity, 100)
                        }, "SetupLaunch Ram TextLeft");
                    break;
                }
                case 1:
                {
                    Animations.AniStart(
                        new[]
                        {
                            Animations.AaOpacity(LabRamUsed, 1d - LabRamUsed.Opacity, 100),
                            Animations.AaOpacity(LabRamTotal, -LabRamTotal.Opacity, 100),
                            Animations.AaOpacity(LabRamUsedTitle, 0.7d - LabRamUsedTitle.Opacity, 100)
                        }, "SetupLaunch Ram TextLeft");
                    break;
                }
                case 2:
                {
                    Animations.AniStart(
                        new[]
                        {
                            Animations.AaOpacity(LabRamUsed, 1d - LabRamUsed.Opacity, 100),
                            Animations.AaOpacity(LabRamTotal, 1d - LabRamTotal.Opacity, 100),
                            Animations.AaOpacity(LabRamUsedTitle, 0.7d - LabRamUsedTitle.Opacity, 100)
                        }, "SetupLaunch Ram TextLeft");
                    break;
                }
            }
        }

        // 右侧
        int right;
        if (totalWidth < labGameWidth + 2d + rectUsedWidth || totalWidth < labGameTitleWidth + 2d + rectUsedWidth)
            // 挤到最右边
            right = 0;
        else
            // 正常情况
            right = 1;
        if (right == 0)
        {
            if (!Animations.AniExitsAnyControl() &&
                (ramTextRight != right || Animations.AniIsRun("SetupLaunch Ram TextRight")))
            {
                // 需要动画
                Animations.AniStart(
                    new[]
                    {
                        Animations.AaX(LabRamGame, totalWidth - labGameWidth - LabRamGame.Margin.Left, 100,
                            ease: new Animations.AniEaseOutFluent(Animations.AniEasePower.Weak)),
                        Animations.AaX(LabRamGameTitle, totalWidth - labGameTitleWidth - LabRamGameTitle.Margin.Left,
                            100, ease: new Animations.AniEaseOutFluent(Animations.AniEasePower.Weak))
                    }, "SetupLaunch Ram TextRight");
            }
            else
            {
                // 不需要动画
                Animations.AniStop("SetupLaunch Ram TextRight");
                LabRamGame.Margin = new Thickness(totalWidth - labGameWidth, 3d, 0d, 0d);
                LabRamGameTitle.Margin = new Thickness(totalWidth - labGameTitleWidth, 0d, 0d, 5d);
            }
        }
        else if (!Animations.AniExitsAnyControl() &&
                 (ramTextRight != right || Animations.AniIsRun("SetupLaunch Ram TextRight")))
        {
            // 需要动画
            Animations.AniStart(
                new[]
                {
                    Animations.AaX(LabRamGame, 2d + rectUsedWidth - LabRamGame.Margin.Left, 100,
                        ease: new Animations.AniEaseOutFluent(Animations.AniEasePower.Weak)),
                    Animations.AaX(LabRamGameTitle, 2d + rectUsedWidth - LabRamGameTitle.Margin.Left, 100,
                        ease: new Animations.AniEaseOutFluent(Animations.AniEasePower.Weak))
                }, "SetupLaunch Ram TextRight");
        }
        else
        {
            // 不需要动画
            Animations.AniStop("SetupLaunch Ram TextRight");
            LabRamGame.Margin = new Thickness(2d + rectUsedWidth, 3d, 0d, 0d);
            LabRamGameTitle.Margin = new Thickness(2d + rectUsedWidth, 0d, 0d, 5d);
        }

        ramTextRight = right;
    }

    /// <summary>
    /// 将内存滑块刻度值转换为 GB。
    /// </summary>
    public static double GetRamFromTick(int value)
    {
        return value switch
        {
            <= 12 => value * 0.1d + 0.3d,
            <= 25 => (value - 12) * 0.5d + 1.5d,
            <= 33 => (value - 25) * 1 + 8,
            _ => (value - 33) * 2 + 16
        };
    }

    /// <summary>
    /// 判断生效的自定义 JVM 参数中是否已包含 -Xms。
    /// 此时启动路径会跳过自动追加的初始堆，实际值以自定义参数为准。
    /// </summary>
    public static bool HasCustomXms(McInstance version)
    {
        // 与启动路径一致：实例级参数非空时完全忽略全局参数，仅在为空时跟随全局
        var dataJvmCustom = version is null ? "" : Config.Instance.JvmArgs[version.PathInstance];
        var effectiveArgs = string.IsNullOrEmpty(dataJvmCustom) ? Config.Launch.JvmArgs : dataJvmCustom;
        return !string.IsNullOrEmpty(effectiveArgs) &&
            effectiveArgs.Contains("-Xms", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 获取全局设置的初始堆大小（-Xms）。单位为 GB；未启用自定义初始大小时返回 null。
    /// </summary>
    public static double? GetInitialRam(McInstance version, bool useVersionJavaSetup, bool? is32BitJava = default)
    {
        // 锁定内存时 -Xms 恒等于 -Xmx，由锁定内存逻辑处理
        if (Config.Launch.LockMemory)
            return null;
        // 自定义 JVM 参数含 -Xms 时由其决定实际初始堆，不再显示滑块值
        if (HasCustomXms(version))
            return null;
        // 仅手动分配模式下生效
        if (Config.Launch.MemoryAllocationMode != 1)
            return null;
        var initialTick = Config.Launch.CustomInitialMemorySize;
        if (initialTick <= 0)
            return null;
        return Math.Min(GetRamFromTick(initialTick), GetRam(version, useVersionJavaSetup, is32BitJava));
    }

    /// <summary>
    /// 获取当前设置的 RAM 值。单位为 GB。
    /// </summary>
    public static double GetRam(McInstance version, bool useVersionJavaSetup, bool? is32BitJava = default)
    {
        // ------------------------------------------
        // 修改下方代码时需要一并修改 PageInstanceSetup
        // ------------------------------------------

        var ramGive = default(double);
        if (Config.Launch.MemoryAllocationMode == 0)
        {
            // 自动配置
            var ramAvailable =
                Math.Round((double)KernelInterop.GetAvailablePhysicalMemoryBytes() / 1024 / 1024 / 1024 * 10) / 10;
            // 确定需求的内存值
            double ramMininum; // 无论如何也需要保证的最低限度内存
            double ramTarget1; // 估计能勉强带动了的内存
            double ramTarget2; // 估计没啥问题了的内存
            double ramTarget3; // 放一百万个材质和 Mod 和光影需要的内存
            if (version is not null && !version.IsLoaded)
                version.Load();
            if (version is not null && version.Modable)
            {
                // 可安装 Mod 的实例
                var modDir = new DirectoryInfo(version.PathIndie + @"mods\");
                var modCount = modDir.Exists ? modDir.GetFiles().Length : 0;
                ramMininum = 0.5d + modCount / 150d;
                ramTarget1 = 1.5d + modCount / 90d;
                ramTarget2 = 2.7d + modCount / 50d;
                ramTarget3 = 4.5d + modCount / 25d;
            }
            else if (version is not null && version.Info.HasOptiFine)
            {
                // OptiFine 实例
                ramMininum = 0.5d;
                ramTarget1 = 1.5d;
                ramTarget2 = 3d;
                ramTarget3 = 5d;
            }
            else
            {
                // 普通实例
                ramMininum = 0.5d;
                ramTarget1 = 1.5d;
                ramTarget2 = 2.5d;
                ramTarget3 = 4d;
            }

            var ramStages = new[]
            {
                (Delta: ramTarget1, Ratio: 1d),
                (Delta: ramTarget2 - ramTarget1, Ratio: 0.7d),
                (Delta: ramTarget3 - ramTarget2, Ratio: 0.4d),
                (Delta: ramTarget3, Ratio: 0.15d)
            };
            foreach (var (RamDelta, RamRatio) in ramStages)
            {
                ramGive += Math.Min(ramAvailable * RamRatio, RamDelta);
                ramAvailable -= RamDelta / RamRatio;
                if (ramAvailable < 0.1d)
                    break;
            }

            // 不低于最低值
            ramGive = Math.Round(Math.Max(ramGive, ramMininum), 1);
        }
        else
        {
            // 手动配置
            ramGive = GetRamFromTick(Config.Launch.CustomMemorySize);
        }

        // 若使用 32 位 Java，则限制为 1G
        if (is32BitJava ?? !JavaRuntime.IsGameSet64BitJava(useVersionJavaSetup ? version : null))
            ramGive = Math.Min(1d, ramGive);
        return ramGive;
    }

    #endregion

    #region 其他选项

    private void WindowTypeUIRefresh()
    {
        if (ComboArgumentWindowType is null)
            return;
        _vm.ShowWindowSize = ComboArgumentWindowType.SelectedIndex == 3;
    }

    // 可见性选择直接关闭的警告
    private async void ComboArgumentVisibie_SelectionChanged(object sender, SelectionChangedEventArgs sizeChangedEventArgs)
    {
        ComboChange(sender, sizeChangedEventArgs);
        if (Animations.AniExitsAnyControl())
            return;
        if (ComboArgumentVisibie.SelectedIndex != 0)
            return;
        var removedItem = sizeChangedEventArgs.RemovedItems.Count > 0 ? sizeChangedEventArgs.RemovedItems[0] : null;
        try
        {
            if ((await _vm.Dialog.ShowAsync(new TextDialogRequest
            {
                Title = Lang.Text("Setup.Launch.Options.Visibility.CloseImmediately.Warning.Title"),
                Content = Lang.Text("Setup.Launch.Options.Visibility.CloseImmediately.Warning.Message"),
                Buttons = DialogButtons.From(
                    DialogButton.Create(Lang.Text("Setup.Launch.Options.Visibility.CloseImmediately.Warning.Continue")),
                    DialogButton.Create(Lang.Text("Common.Action.Cancel")))
            })).Button == 2 &&
                removedItem is not null)
                ComboArgumentVisibie.SelectedItem = removedItem;
        }
        catch (Exception ex)
        {
            Common.Log(ex, "关闭窗口可见性确认失败", Common.LogLevel.Msgbox,
                userSummary: Lang.Text("Setup.Error.OperationFailed"));
        }
    }

    // 实例隔离提示
    private async void ComboArgumentIndie_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Animations.AniExitsAnyControl())
            return;
        try
        {
            await _vm.Dialog.ShowAsync(new TextDialogRequest
            {
                Title = Lang.Text("Common.Dialog.Title"),
                Content = Lang.Text("Setup.Launch.Options.InstanceIsolation.DefaultPolicyHint"),
                Buttons = DialogButtons.Ok
            });
        }
        catch (Exception ex)
        {
            Common.Log(ex, "实例隔离提示显示失败", Common.LogLevel.Feedback,
                userSummary: Lang.Text("Setup.Error.OperationFailed"));
        }
    }

    #endregion

    #region 高级设置

    private void TextAdvanceRun_TextChanged(object sender, TextChangedEventArgs e)
    {
        _vm.CheckAdvanceRunWaitVisible = !string.IsNullOrEmpty(TextAdvanceRun.Text);
    }

    // JVM 参数重设
    private void TextAdvanceJvm_TextChanged(object sender, TextChangedEventArgs e)
    {
        _vm.JvmResetVisibility =
            TextAdvanceJvm.Text == Config.Launch.JvmArgsConfig.DefaultValue
                ? Visibility.Hidden
                : Visibility.Visible;
    }

    private void BtnAdvanceJvmReset_Click(object sender, EventArgs e)
    {
        Config.Launch.JvmArgsConfig.Reset();
        Reload();
    }

    private async void ComboAdvanceRenderer_SelectionChanged(MyComboBox sender, object e)
    {
        if (Animations.AniExitsAnyControl())
            return;
        if (!States.Hint.Renderer && ComboAdvanceRenderer.SelectedIndex != 0)
        {
            var removedItem = ((SelectionChangedEventArgs)e).RemovedItems.Count > 0
                ? ((SelectionChangedEventArgs)e).RemovedItems[0]
                : null;
            try
            {
                if ((await _vm.Dialog.ShowAsync(new TextDialogRequest
                {
                    Title = Lang.Text("Common.Dialog.Warning"),
                    Content = Lang.Text("Setup.Launch.Advanced.Renderer.Warning.Message"),
                    Buttons = DialogButtons.From(
                        DialogButton.Create(Lang.Text("Setup.Launch.Advanced.Renderer.Warning.Confirm")),
                        DialogButton.Create(Lang.Text("Common.Action.Cancel"))),
                    IsWarn = true
                })).Button == 2)
                {
                    if (removedItem is not null)
                        ComboAdvanceRenderer.SelectedItem = removedItem;
                }
                else
                {
                    Config.Launch.Renderer = sender.SelectedIndex;
                    States.Hint.Renderer = true;
                }
            }
            catch (Exception ex)
            {
                Common.Log(ex, "渲染器可用性提示显示失败", Common.LogLevel.Feedback,
                    userSummary: Lang.Text("Setup.Error.OperationFailed"));
            }
        }
        else
        {
            Config.Launch.Renderer = sender.SelectedIndex;
        }
    }

    #endregion
}
