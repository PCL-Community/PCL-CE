using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using PCL.Core.App;
using PCL.Core.Utils.OS;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageSetupLaunch
{
    private bool IsLoad;

    public PageSetupLaunch()
    {
        Loaded += PageSetupLaunch_Loaded;
        InitializeComponent();
    }

    private void PageSetupLaunch_Loaded(object sender, RoutedEventArgs e)
    {
        // 重复加载部分
        PanBack.ScrollToHome();
        RefreshRam(false);
        if (ModMinecraft.McInstanceSelected is null)
            BtnSwitch.Visibility = Visibility.Collapsed;
        else
            BtnSwitch.Visibility = Visibility.Visible;

        // 非重复加载部分
        if (IsLoad)
            return;
        IsLoad = true;

        ModAnimation.AniControlEnabled += 1;
        Reload();
        ModAnimation.AniControlEnabled -= 1;

        // 内存自动刷新
        var timer = new DispatcherTimer { Interval = new TimeSpan(0, 0, 0, 1) };
        timer.Tick += (_, _) => RefreshRam();
        timer.Start();
        RectRamGame.SizeChanged += (s, e) => RefreshRamText();
    }

    public void Reload()
    {
        try
        {
            // 启动参数
            TextArgumentTitle.Text = Config.Launch.Title;
            TextArgumentInfo.Text = Config.Launch.TypeInfo;
            ComboArgumentIndieV2.SelectedIndex = Config.Launch.IndieSolutionV2;
            ComboArgumentVisibie.SelectedIndex = (int)Config.Launch.LauncherVisibility;
            ComboArgumentPriority.SelectedIndex = (int)Config.Launch.ProcessPriority;
            ComboArgumentWindowType.SelectedIndex = (int)Config.Launch.GameWindowMode;
            TextArgumentWindowWidth.Text = Config.Launch.GameWindowWidth.ToString();
            TextArgumentWindowHeight.Text = Config.Launch.GameWindowHeight.ToString();
            ComboMsAuthType.SelectedIndex = Config.Launch.LoginMsAuthType;
            ComboPreferredIpStack.SelectedIndex = (int)Config.Launch.PreferredIpStack;
            // CheckArgumentJavaTraversal.Checked = Setup.Get("LaunchArgumentJavaTraversal")

            // 游戏内存
            ((MyRadioBox)FindName("RadioRamType" + ModBase.Setup.Load("LaunchRamType"))).Checked = true;
            SliderRamCustom.Value = Config.Launch.CustomMemorySize;

            // 高级设置
            ComboAdvanceRenderer.SelectedIndex = Config.Launch.Renderer;
            TextAdvanceJvm.Text = Config.Launch.JvmArgs;
            TextAdvanceGame.Text = Config.Launch.GameArgs;
            TextAdvanceRun.Text = Config.Launch.PreLaunchCommand;
            CheckAdvanceRunWait.Checked = Config.Launch.PreLaunchCommandWait;
            CheckAdvanceDisableRW.Checked = Config.Launch.DisableRw;
            CheckAdvanceGraphicCard.Checked = Config.Launch.SetGpuPreference;
            CheckAdvanceNoJavaw.Checked = Config.Launch.NoJavaw;
            CheckAdvanceDisableLwjglUnsafeAgent.Checked = Config.Launch.DisableLwjglUnsafeAgent;
            if (ModBase.IsArm64System)
            {
                CheckAdvanceDisableJLW.Checked = true;
                CheckAdvanceDisableJLW.IsEnabled = false;
                CheckAdvanceDisableJLW.ToolTip = "在启动游戏时不使用 Java Wrapper 进行包装。&#xa;由于系统为 ARM64 架构，Java Wrapper 已被强制禁用。";
            }
            else
            {
                CheckAdvanceDisableJLW.Checked = Config.Launch.DisableJlw;
            }
        }

        catch (NullReferenceException ex)
        {
            ModBase.Log(ex, "启动设置项存在异常，已被自动重置", ModBase.LogLevel.Msgbox);
            Reset();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "重载启动设置时出错", ModBase.LogLevel.Feedback);
        }
    }

    // 初始化
    public void Reset()
    {
        try
        {
            Config.Launch.Reset();
            ModBase.Log("[Setup] 已初始化启动设置");
            ModMain.Hint("已初始化启动设置！", ModMain.HintType.Finish, false);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "初始化启动设置失败", ModBase.LogLevel.Msgbox);
        }

        Reload();
    }

    // 将控件改变路由到设置改变
    private void RadioBoxChange(object senderRaw, ModBase.RouteEventArgs e)
    {
        var sender = (MyRadioBox)senderRaw;
        var gotCfg = sender.Tag?.ToString()?.Split("/") ?? Array.Empty<string>();
        if (ModAnimation.AniControlEnabled == 0 && gotCfg.Length >= 2)
            ModBase.Setup.Set(gotCfg[0], int.Parse(gotCfg[1]));
    }

    private void TextBoxChange(object senderRaw, RoutedEventArgs e)
    {
        var sender = (MyTextBox)senderRaw;
        if (ModAnimation.AniControlEnabled == 0)
            ModBase.Setup.Set(sender.Tag?.ToString(), sender.Text);
    }

    private void TextArgumentTitle_OnTextChanged(object senderRaw, TextChangedEventArgs e)
    {
        var sender = (MyTextBox)senderRaw;
        if (ModAnimation.AniControlEnabled == 0)
            ModBase.Setup.Set(sender.Tag?.ToString(), sender.Text);
    }

    private void SliderChange(object senderRaw, bool user)
    {
        var sender = (MySlider)senderRaw;
        if (ModAnimation.AniControlEnabled == 0)
            ModBase.Setup.Set(sender.Tag?.ToString(), sender.Value);
    }

    private void ComboChange(object senderRaw, SelectionChangedEventArgs e)
    {
        var sender = (MyComboBox)senderRaw;
        if (ModAnimation.AniControlEnabled == 0)
            ModBase.Setup.Set(sender.Tag?.ToString(), sender.SelectedIndex);
    }

    private void CheckBoxChange(object senderRaw, bool user)
    {
        var sender = (MyCheckBox)senderRaw;
        if (ModAnimation.AniControlEnabled == 0)
            ModBase.Setup.Set(sender.Tag?.ToString(), sender.Checked);
    }

    // 切换到实例独立设置
    private void BtnSwitch_Click(object sender, MouseButtonEventArgs e)
    {
        ModMinecraft.McInstanceSelected.Load();
        PageInstanceLeft.Instance = ModMinecraft.McInstanceSelected;
        ModMain.FrmMain.PageChange(FormMain.PageType.InstanceSetup, FormMain.PageSubType.VersionSetup);
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

    public void RamType(int Type)
    {
        if (SliderRamCustom is null)
            return;
        SliderRamCustom.IsEnabled = Type == 1;
    }

    /// <summary>
    ///     刷新 UI 上的 RAM 显示。
    /// </summary>
    public void RefreshRam(bool showAnim)
    {
        if (LabRamGame is null || LabRamUsed is null || ModMain.FrmMain.PageCurrent != FormMain.PageType.Setup ||
            ModMain.FrmSetupLeft.PageID != FormMain.PageSubType.SetupLaunch)
            return;
        // 获取内存情况
        var ramGame = Math.Round(GetRam(ModMinecraft.McInstanceSelected, false), 5);
        var phyRam = KernelInterop.GetPhysicalMemoryBytes();
        var ramTotal = Math.Round((double)phyRam.Total / 1024 / 1024 / 1024, 1);
        var ramAvailable = Math.Round((double)phyRam.Available / 1024 / 1024 / 1024, 1);
        var ramGameActual = Math.Round(Math.Min(ramGame, ramAvailable), 5);
        var ramUsed = Math.Round(ramTotal - ramAvailable, 5);
        var ramEmpty = Math.Round(ModBase.MathClamp(ramTotal - ramUsed - ramGame, 0d, 1000d), 1);
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
        LabRamGame.Text = $"{Lang.Number(ramGame, "N1")} GB{(ramGame != ramGameActual ? $" (可用 {Lang.Number(ramGameActual, "N1")} GB)" : "")}";
        LabRamUsed.Text = $"{Lang.Number(ramUsed, "N1")} GB";
        LabRamTotal.Text = $" / {Lang.Number(ramTotal, "N1")} GB";
        LabRamWarn.Visibility =
            ramGame == 1d && !ModJava.IsGameSet64BitJava() && !ModBase.Is32BitSystem && ModJava.Javas.ExistAnyJava()
                ? Visibility.Visible
                : Visibility.Collapsed;
        HintRamTooHigh.Visibility = ramGame / ramTotal > 0.75d ? Visibility.Visible : Visibility.Collapsed;
        if (showAnim)
        {
            // 宽度动画
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaGridLengthWidth(ColumnRamUsed, ramUsed - ColumnRamUsed.Width.Value, 800,
                        Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong)),
                    ModAnimation.AaGridLengthWidth(ColumnRamGame, ramGameActual - ColumnRamGame.Width.Value, 800,
                        Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong)),
                    ModAnimation.AaGridLengthWidth(ColumnRamEmpty, ramEmpty - ColumnRamEmpty.Width.Value, 800,
                        Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong))
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

    private int RamTextLeft = 2;
    private int RamTextRight = 1;

    /// <summary>
    ///     刷新 UI 上的文本位置。
    /// </summary>
    private void RefreshRamText()
    {
        // 获取宽度信息
        var RectUsedWidth = RectRamUsed.ActualWidth;
        var TotalWidth = PanRamDisplay.ActualWidth;
        var LabGameWidth = LabRamGame.ActualWidth;
        var LabUsedWidth = LabRamUsed.ActualWidth;
        var LabTotalWidth = LabRamTotal.ActualWidth;
        var LabGameTitleWidth = LabRamGameTitle.ActualWidth;
        var LabUsedTitleWidth = LabRamUsedTitle.ActualWidth;
        // 左侧
        int Left;
        if (RectUsedWidth - 30d < LabUsedWidth || RectUsedWidth - 30d < LabUsedTitleWidth)
            // 全写不下了
            Left = 0;
        else if (RectUsedWidth - 25d < LabUsedWidth + LabTotalWidth)
            // 显示不下完整数据
            Left = 1;
        else
            // 正常
            Left = 2;
        if (RamTextLeft != Left)
        {
            RamTextLeft = Left;
            switch (Left)
            {
                case 0:
                {
                    ModAnimation.AniStart(
                        new[]
                        {
                            ModAnimation.AaOpacity(LabRamUsed, -LabRamUsed.Opacity, 100),
                            ModAnimation.AaOpacity(LabRamTotal, -LabRamTotal.Opacity, 100),
                            ModAnimation.AaOpacity(LabRamUsedTitle, -LabRamUsedTitle.Opacity, 100)
                        }, "SetupLaunch Ram TextLeft");
                    break;
                }
                case 1:
                {
                    ModAnimation.AniStart(
                        new[]
                        {
                            ModAnimation.AaOpacity(LabRamUsed, 1d - LabRamUsed.Opacity, 100),
                            ModAnimation.AaOpacity(LabRamTotal, -LabRamTotal.Opacity, 100),
                            ModAnimation.AaOpacity(LabRamUsedTitle, 0.7d - LabRamUsedTitle.Opacity, 100)
                        }, "SetupLaunch Ram TextLeft");
                    break;
                }
                case 2:
                {
                    ModAnimation.AniStart(
                        new[]
                        {
                            ModAnimation.AaOpacity(LabRamUsed, 1d - LabRamUsed.Opacity, 100),
                            ModAnimation.AaOpacity(LabRamTotal, 1d - LabRamTotal.Opacity, 100),
                            ModAnimation.AaOpacity(LabRamUsedTitle, 0.7d - LabRamUsedTitle.Opacity, 100)
                        }, "SetupLaunch Ram TextLeft");
                    break;
                }
            }
        }

        // 右侧
        int Right;
        if (TotalWidth < LabGameWidth + 2d + RectUsedWidth || TotalWidth < LabGameTitleWidth + 2d + RectUsedWidth)
            // 挤到最右边
            Right = 0;
        else
            // 正常情况
            Right = 1;
        if (Right == 0)
        {
            if (ModAnimation.AniControlEnabled == 0 &&
                (RamTextRight != Right || ModAnimation.AniIsRun("SetupLaunch Ram TextRight")))
            {
                // 需要动画
                ModAnimation.AniStart(
                    new[]
                    {
                        ModAnimation.AaX(LabRamGame, TotalWidth - LabGameWidth - LabRamGame.Margin.Left, 100,
                            Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)),
                        ModAnimation.AaX(LabRamGameTitle, TotalWidth - LabGameTitleWidth - LabRamGameTitle.Margin.Left,
                            100, Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak))
                    }, "SetupLaunch Ram TextRight");
            }
            else
            {
                // 不需要动画
                ModAnimation.AniStop("SetupLaunch Ram TextRight");
                LabRamGame.Margin = new Thickness(TotalWidth - LabGameWidth, 3d, 0d, 0d);
                LabRamGameTitle.Margin = new Thickness(TotalWidth - LabGameTitleWidth, 0d, 0d, 5d);
            }
        }
        else if (ModAnimation.AniControlEnabled == 0 &&
                 (RamTextRight != Right || ModAnimation.AniIsRun("SetupLaunch Ram TextRight")))
        {
            // 需要动画
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaX(LabRamGame, 2d + RectUsedWidth - LabRamGame.Margin.Left, 100,
                        Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)),
                    ModAnimation.AaX(LabRamGameTitle, 2d + RectUsedWidth - LabRamGameTitle.Margin.Left, 100,
                        Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak))
                }, "SetupLaunch Ram TextRight");
        }
        else
        {
            // 不需要动画
            ModAnimation.AniStop("SetupLaunch Ram TextRight");
            LabRamGame.Margin = new Thickness(2d + RectUsedWidth, 3d, 0d, 0d);
            LabRamGameTitle.Margin = new Thickness(2d + RectUsedWidth, 0d, 0d, 5d);
        }

        RamTextRight = Right;
    }

    /// <summary>
    ///     获取当前设置的 RAM 值。单位为 GB。
    /// </summary>
    public static double GetRam(ModMinecraft.McInstance Version, bool UseVersionJavaSetup, bool? Is32BitJava = default)
    {
        // ------------------------------------------
        // 修改下方代码时需要一并修改 PageInstanceSetup
        // ------------------------------------------

        var RamGive = default(double);
        if (Config.Launch.MemoryAllocationMode == 0)
        {
            // 自动配置
            var RamAvailable =
                Math.Round((double)KernelInterop.GetAvailablePhysicalMemoryBytes() / 1024 / 1024 / 1024 * 10) / 10;
            // 确定需求的内存值
            double RamMininum; // 无论如何也需要保证的最低限度内存
            double RamTarget1; // 估计能勉强带动了的内存
            double RamTarget2; // 估计没啥问题了的内存
            double RamTarget3; // 放一百万个材质和 Mod 和光影需要的内存
            if (Version is not null && !Version.IsLoaded)
                Version.Load();
            if (Version is not null && Version.Modable)
            {
                // 可安装 Mod 的实例
                var ModDir = new DirectoryInfo(Version.PathIndie + @"mods\");
                var ModCount = ModDir.Exists ? ModDir.GetFiles().Length : 0;
                RamMininum = 0.5d + ModCount / 150d;
                RamTarget1 = 1.5d + ModCount / 90d;
                RamTarget2 = 2.7d + ModCount / 50d;
                RamTarget3 = 4.5d + ModCount / 25d;
            }
            else if (Version is not null && Version.Info.HasOptiFine)
            {
                // OptiFine 实例
                RamMininum = 0.5d;
                RamTarget1 = 1.5d;
                RamTarget2 = 3d;
                RamTarget3 = 5d;
            }
            else
            {
                // 普通实例
                RamMininum = 0.5d;
                RamTarget1 = 1.5d;
                RamTarget2 = 2.5d;
                RamTarget3 = 4d;
            }

            double RamDelta;
            // 预分配内存，阶段一，0 ~ T1，100%
            RamDelta = RamTarget1;
            RamGive += Math.Min(RamAvailable, RamDelta);
            RamAvailable -= RamDelta;
            if (RamAvailable < 0.1d)
                goto PreFin;
            // 预分配内存，阶段二，T1 ~ T2，70%
            RamDelta = RamTarget2 - RamTarget1;
            RamGive += Math.Min(RamAvailable * 0.7d, RamDelta);
            RamAvailable -= RamDelta / 0.7d;
            if (RamAvailable < 0.1d)
                goto PreFin;
            // 预分配内存，阶段三，T2 ~ T3，40%
            RamDelta = RamTarget3 - RamTarget2;
            RamGive += Math.Min(RamAvailable * 0.4d, RamDelta);
            RamAvailable -= RamDelta / 0.4d;
            if (RamAvailable < 0.1d)
                goto PreFin;
            // 预分配内存，阶段四，T3 ~ T3 * 2，15%
            RamDelta = RamTarget3;
            RamGive += Math.Min(RamAvailable * 0.15d, RamDelta);
            RamAvailable -= RamDelta / 0.15d;
            if (RamAvailable < 0.1d)
                goto PreFin;
            PreFin: ;

            // 不低于最低值
            RamGive = Math.Round(Math.Max(RamGive, RamMininum), 1);
        }
        else
        {
            // 手动配置
            var Value = Config.Launch.CustomMemorySize;
            RamGive = Value switch
            {
                <= 12 => Value * 0.1d + 0.3d,
                <= 25 => (Value - 12) * 0.5d + 1.5d,
                <= 33 => (Value - 25) * 1 + 8,
                _ => (Value - 33) * 2 + 16
            };
        }

        // 若使用 32 位 Java，则限制为 1G
        if (Is32BitJava ?? !ModJava.IsGameSet64BitJava(UseVersionJavaSetup ? Version : null))
            RamGive = Math.Min(1d, RamGive);
        return RamGive;
    }

    #endregion

    #region 其他选项

    private void WindowTypeUIRefresh()
    {
        if (ComboArgumentWindowType is null)
            return;
        if (ComboArgumentWindowType.SelectedIndex == 3 && LabArgumentWindowMiddle is not null &&
            LabArgumentWindowMiddle.Visibility == Visibility.Collapsed)
        {
            LabArgumentWindowMiddle.Visibility = Visibility.Visible;
            TextArgumentWindowHeight.Visibility = Visibility.Visible;
            TextArgumentWindowWidth.Visibility = Visibility.Visible;
        }
        else if (ComboArgumentWindowType.SelectedIndex != 3 && LabArgumentWindowMiddle is not null &&
                 LabArgumentWindowMiddle.Visibility == Visibility.Visible)
        {
            LabArgumentWindowMiddle.Visibility = Visibility.Collapsed;
            TextArgumentWindowHeight.Visibility = Visibility.Collapsed;
            TextArgumentWindowWidth.Visibility = Visibility.Collapsed;
        }
    }

    // 可见性选择直接关闭的警告
    private void ComboArgumentVisibie_SelectionChanged(object sender, SelectionChangedEventArgs sizeChangedEventArgs)
    {
        ComboChange(sender, sizeChangedEventArgs);
        if (ModAnimation.AniControlEnabled != 0)
            return;
        if (ComboArgumentVisibie.SelectedIndex == 0)
            if (ModMain.MyMsgBox(
                    """
                    若在游戏启动后立即关闭启动器，崩溃检测、更改游戏标题等功能将失效。
                    如果想保留这些功能，可以选择让启动器在游戏启动后隐藏，游戏退出后自动关闭。
                    """,
                    "提醒", "继续", Lang.Text("Common.Action.Cancel")) == 2)
                ComboArgumentVisibie.SelectedItem = sizeChangedEventArgs.RemovedItems[0];
    }

    // 实例隔离提示
    private void ComboArgumentIndie_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModAnimation.AniControlEnabled != 0)
            return;
        ModMain.MyMsgBox("""
                         默认策略只会对今后新安装的实例生效。
                         已有实例的隔离策略需要在它的设置中调整。
                         """);
    }

    #endregion

    #region 高级设置

    private void TextAdvanceRun_TextChanged(object sender, TextChangedEventArgs e)
    {
        CheckAdvanceRunWait.Visibility =
            string.IsNullOrEmpty(TextAdvanceRun.Text) ? Visibility.Collapsed : Visibility.Visible;
    }

    // JVM 参数重设
    private void TextAdvanceJvm_TextChanged(object sender, TextChangedEventArgs e)
    {
        BtnAdvanceJvmReset.Visibility =
            TextAdvanceJvm.Text == (string)ModBase.Setup.GetDefault("LaunchAdvanceJvm")
                ? Visibility.Hidden
                : Visibility.Visible;
    }

    private void BtnAdvanceJvmReset_Click(object sender, EventArgs e)
    {
        ModBase.Setup.Reset("LaunchAdvanceJvm");
        Reload();
    }

    private void ComboAdvanceRenderer_SelectionChanged(MyComboBox sender, object e)
    {
        if (ModAnimation.AniControlEnabled != 0)
            return;
        if (!States.Hint.Renderer && ComboAdvanceRenderer.SelectedIndex != 0)
        {
            if (ModMain.MyMsgBox("""
                                 修改此项会严重影响游戏的稳定性与性能。如果你不知道你在做什么，不要修改此选项！
                                 你确定要继续修改吗？
                                 """, "警告",
                    "我知道我在做什么", Lang.Text("Common.Action.Cancel"), IsWarn: true) == 2)
            {
                ComboAdvanceRenderer.SelectedItem = ((SelectionChangedEventArgs)e).RemovedItems[0];
            }
            else
            {
                ModBase.Setup.Set((string)sender.Tag, sender.SelectedIndex);
                States.Hint.Renderer = true;
            }
        }
        else
        {
            ModBase.Setup.Set((string)sender.Tag, sender.SelectedIndex);
        }
    }

    #endregion
}
