using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.IO;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Java.UserPreference;
using PCL.Core.UI;
using PCL.Core.Utils.OS;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageInstanceSetup
{
    private new bool IsLoaded;

    public PageInstanceSetup()
    {     
        Loaded += PageSetupSystem_Loaded;
        InitializeComponent();

        ComboArgumentIndieV2.SelectionChanged += ComboArgumentIndieV2_SelectionChanged;
        TextArgumentTitle.TextChanged += TextArgumentTitle_TextChanged;
        TextArgumentInfo.TextChanged += TextBoxChange;
        ComboArgumentJava.SelectionChanged += JavaSelectionUpdate;

        RadioRamType2.Check += RadioBoxChange;
        RadioRamType0.Check += RadioBoxChange;
        RadioRamType1.Check += RadioBoxChange;
        SliderRamCustom.Change += SliderChange;

        ComboServerLoginRequire.SelectionChanged += ComboServerLogin_Changed;
        TextServerAuthServer.TextChanged += TextBoxChange;
        TextServerAuthServer.LostFocus += TextServerAuthServer_MouseLeave;
        TextServerAuthRegister.TextChanged += TextBoxChange;
        TextServerAuthName.TextChanged += TextBoxChange;
        TextServerEnter.TextChanged += TextBoxChange;
        BtnServerAuthLittle.Click += BtnServerAuthLittle_Click;
        BtnServerAuthLock.Click += BtnServerAuthLock_Click;
        BtnServerNewProfile.Click += BtnServerNewProfile_Click;

        ComboAdvanceRenderer.SelectionChanged += ComboAdvanceRenderer_SelectionChanged;
        TextAdvanceJvm.TextChanged += TextBoxChange;
        TextAdvanceGame.TextChanged += TextBoxChange;
        TextAdvanceClasspathHead.TextChanged += TextBoxChange;
        TextAdvanceRun.TextChanged += TextAdvanceRun_TextChanged;
        CheckAdvanceRunWait.Change += CheckBoxChange;
        CheckAdvanceJava.Change += CheckBoxChange;
        CheckAdvanceAssetsV2.Change += CheckBoxChange;
        CheckAdvanceUseProxyV2.Change += CheckBoxChange;
        CheckAdvanceDisableJLW.Change += CheckBoxChange;
        CheckAdvanceDisableRW.Change += CheckBoxChange;
        CheckUseDebugLog4j2Config.Change += CheckUseDebugLog4j2Config_CheckChanged;
        CheckAdvanceDisableLwjglUnsafeAgent.Change += CheckBoxChange;

        BtnSwitch.Click += BtnSwitch_Click;
        
        TextServerEnter.TextChanged += TextServerEnter_Change;
        ComboArgumentJava.DropDownOpened += ComboArgumentJava_DropDownOpened;
        CheckArgumentTitleEmpty.Change += CheckArgumentTitleEmpty_Change;
    }

    private void PageSetupSystem_Loaded(object sender, RoutedEventArgs e)
    {
        // 重复加载部分
        PanBack.ScrollToHome();
        RefreshRam(false);

        // 由于各个实例不同，每次都需要重新加载
        ModAnimation.AniControlEnabled += 1;
        Reload();
        ModAnimation.AniControlEnabled -= 1;

        // 非重复加载部分
        if (IsLoaded)
            return;
        IsLoaded = true;

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
            TextArgumentTitle.Text = Config.Instance.Title[PageInstanceLeft.Instance.PathInstance];
            CheckArgumentTitleEmpty.Checked = Config.Instance.UseGlobalTitle[PageInstanceLeft.Instance.PathInstance];
            TextArgumentInfo.Text = Config.Instance.TypeInfo[PageInstanceLeft.Instance.PathInstance];
            var _unused = PageInstanceLeft.Instance.PathIndie; // 触发自动判定
            ComboArgumentIndieV2.SelectedIndex = Config.Instance.IndieV2[PageInstanceLeft.Instance.PathInstance] ? 0 : 1;
            CheckArgumentTitleEmpty.Visibility = TextArgumentTitle.Text.Length > 0 ? Visibility.Collapsed : Visibility.Visible;
            TextArgumentTitle.HintText = CheckArgumentTitleEmpty.Checked == true ? Lang.Text("Common.Option.Default") : "跟随全局设置";
            RefreshJavaComboBox();

            // 游戏内存
            var ramType = Config.Instance.MemorySolution[PageInstanceLeft.Instance.PathInstance];
            ((MyRadioBox)FindName("RadioRamType" + ramType)).Checked = true;
            SliderRamCustom.Value = Config.Instance.CustomMemorySize[PageInstanceLeft.Instance.PathInstance];

            // 服务器
            TextServerEnter.Text = Config.Instance.ServerToEnter[PageInstanceLeft.Instance.PathInstance];
            ComboServerLoginRequire.SelectedIndex = Config.InstanceAuth.LoginRequirementSolution[PageInstanceLeft.Instance.PathInstance];
            ComboServerLoginLast = ComboServerLoginRequire.SelectedIndex;
            ServerLogin(ComboServerLoginRequire.SelectedIndex);
            TextServerAuthServer.Text = Config.InstanceAuth.AuthServerAddress[PageInstanceLeft.Instance.PathInstance];
            TextServerAuthName.Text = Config.InstanceAuth.AuthServerDisplayName[PageInstanceLeft.Instance.PathInstance];
            TextServerAuthRegister.Text = Config.InstanceAuth.AuthRegisterAddress[PageInstanceLeft.Instance.PathInstance];

            // 高级设置
            ComboAdvanceRenderer.SelectedIndex = Config.Instance.Renderer[PageInstanceLeft.Instance.PathInstance];
            TextAdvanceClasspathHead.Text = Config.Instance.ClasspathHead[PageInstanceLeft.Instance.PathInstance];
            TextAdvanceJvm.Text = Config.Instance.JvmArgs[PageInstanceLeft.Instance.PathInstance];
            TextAdvanceGame.Text = Config.Instance.GameArgs[PageInstanceLeft.Instance.PathInstance];
            TextAdvanceRun.Text = Config.Instance.PreLaunchCommand[PageInstanceLeft.Instance.PathInstance];
            CheckAdvanceRunWait.Checked = Config.Instance.PreLaunchCommandWait[PageInstanceLeft.Instance.PathInstance];
            CheckAdvanceDisableLwjglUnsafeAgent.Checked = Config.Instance.DisableLwjglUnsafeAgent[PageInstanceLeft.Instance.PathInstance];
            if (Config.Instance.AssetVerifySolutionV1[PageInstanceLeft.Instance.PathInstance] == 2)
            {
                ModBase.Log("[Setup] 已迁移老版本的关闭文件校验设置");
                Config.Instance.AssetVerifySolutionV1Config.Reset(PageInstanceLeft.Instance.PathInstance);
                Config.Instance.DisableAssetVerifyV2[PageInstanceLeft.Instance.PathInstance] = true;
            }

            CheckAdvanceAssetsV2.Checked = Config.Instance.DisableAssetVerifyV2[PageInstanceLeft.Instance.PathInstance];
            CheckAdvanceUseProxyV2.Checked = Config.Instance.UseProxy[PageInstanceLeft.Instance.PathInstance];
            CheckAdvanceJava.Checked = Config.Instance.IgnoreJavaCompatibility[PageInstanceLeft.Instance.PathInstance];
            if (ModBase.IsArm64System)
            {
                CheckAdvanceDisableJLW.Checked = true;
                CheckAdvanceDisableJLW.IsEnabled = false;
                CheckAdvanceDisableJLW.ToolTip = "在启动游戏时不使用 Java Wrapper 进行包装。&#xa;由于系统为 ARM64 架构，Java Wrapper 已被强制禁用。";
            }
            else
            {
                CheckAdvanceDisableJLW.Checked = Config.Instance.DisableJlw[PageInstanceLeft.Instance.PathInstance];
            }
            CheckUseDebugLog4j2Config.Checked = Config.Instance.UseDebugLof4j2Config[PageInstanceLeft.Instance.PathInstance];
            CheckAdvanceDisableRW.Checked = Config.Instance.DisableRw[PageInstanceLeft.Instance.PathInstance];
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, "重载实例独立设置时出错", ModBase.LogLevel.Feedback);
        }
    }

    // 初始化
    public void Reset()
    {
        try
        {
            if (!Config.InstanceAuth.AuthLocked[PageInstanceLeft.Instance.PathInstance])
                Config.InstanceAuth.Reset(PageInstanceLeft.Instance.PathInstance);

            Config.Instance.Reset(PageInstanceLeft.Instance.PathInstance);

            ModBase.Log("[Setup] 已初始化实例独立设置");
            ModMain.Hint("已初始化实例独立设置！", ModMain.HintType.Finish, false);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "初始化实例独立设置失败", ModBase.LogLevel.Msgbox);
        }

        Reload();
    }

    // 将控件改变路由到设置改变
    private void RadioBoxChange(object o, ModBase.RouteEventArgs routeEventArgs)
    {
        var sender = (MyRadioBox)o;
        var gotCfg = sender.Tag.ToString().Split("/");
        if (ModAnimation.AniControlEnabled == 0)
            SetInstanceByTag(gotCfg[0], int.Parse(gotCfg[1]));
    }

    private void TextBoxChange(object o, TextChangedEventArgs textChangedEventArgs)
    {
        if (ModAnimation.AniControlEnabled != 0)
            return;
        if (o is not MyTextBox textBox) return;
        
        var tag = textBox.Tag?.ToString();
        var value = textBox.Text;
        ArgConfig<string> setting = tag switch 
        {
            "VersionArgumentTitle" => Config.Instance.Title,
            "VersionArgumentInfo" => Config.Instance.TypeInfo,
            "VersionServerAuthServer" => Config.InstanceAuth.AuthServerAddress,
            "VersionServerAuthRegister" => Config.InstanceAuth.AuthRegisterAddress,
            "VersionServerAuthName" => Config.InstanceAuth.AuthServerDisplayName,
            "VersionServerEnter" => Config.Instance.ServerToEnter,
            "VersionAdvanceJvm" => Config.Instance.JvmArgs,
            "VersionAdvanceGame" => Config.Instance.GameArgs,
            "VersionAdvanceClasspathHead" => Config.Instance.ClasspathHead,
            "VersionAdvanceRun" => Config.Instance.PreLaunchCommand,
            _ => throw new ArgumentOutOfRangeException()
        };
        setting[PageInstanceLeft.Instance.PathInstance] = value;
    }

    private void SliderChange(object o, bool user)
    {
        var sender = (MySlider)o;
        if (ModAnimation.AniControlEnabled == 0)
            SetInstanceByTag(sender.Tag?.ToString(), sender.Value);
    }

    private static void ComboChange(MyComboBox sender, object e)
    {
        if (ModAnimation.AniControlEnabled == 0)
            SetInstanceByTag(sender.Tag?.ToString(), sender.SelectedIndex);
    }

    private static void SetInstanceByTag(string tag, object value)
    {
        var path = PageInstanceLeft.Instance.PathInstance;
        switch (tag)
        {
            case "VersionRamType": Config.Instance.MemorySolution[path] = (int)value; break;
            case "VersionRamCustom": Config.Instance.CustomMemorySize[path] = (int)value; break;
            case "VersionServerLoginRequire": Config.InstanceAuth.LoginRequirementSolution[path] = (int)value; break;
        }
    }

    private void CheckBoxChange(object sender, bool user)
    {
        if (ModAnimation.AniControlEnabled != 0)
            return;
        if (sender is not MyCheckBox checkBox) return;
        
        var tag = checkBox.Tag?.ToString();
        var value = checkBox.Checked.GetValueOrDefault();
        ArgConfig<bool> setting = tag switch
        {
            "VersionArgumentTitleEmpty" => Config.Instance.UseGlobalTitle,
            "VersionAdvanceRunWait" => Config.Instance.PreLaunchCommandWait,
            "VersionAdvanceJava" => Config.Instance.IgnoreJavaCompatibility,
            "VersionAdvanceAssetsV2" => Config.Instance.DisableAssetVerifyV2,
            "VersionAdvanceUseProxyV2" => Config.Instance.UseProxy,
            "VersionAdvanceDisableJLW" => Config.Instance.DisableJlw,
            "VersionAdvanceDisableRW" => Config.Instance.DisableRw,
            "VersionUseDebugLog4j2Config" => Config.Instance.UseDebugLof4j2Config,
            "VersionAdvanceDisableLwjglUnsafeAgent" => Config.Instance.DisableLwjglUnsafeAgent,
            _ => throw new ArgumentOutOfRangeException()
        };
        setting[PageInstanceLeft.Instance.PathInstance] = value;
    }

    // 切换到全局设置
    private void BtnSwitch_Click(object sender, MouseButtonEventArgs e)
    {
        ModMain.FrmMain.PageChange(FormMain.PageType.Setup);
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
    public void RefreshRam(bool ShowAnim)
    {
        if (LabRamGame is null || LabRamUsed is null ||
            ModMain.FrmMain.PageCurrent != FormMain.PageType.InstanceSetup ||
            ModMain.FrmInstanceLeft.PageID != FormMain.PageSubType.VersionSetup)
            return;
        // 获取内存情况
        var RamGame = Math.Round(GetRam(PageInstanceLeft.Instance), 5);
        var phyRam = KernelInterop.GetPhysicalMemoryBytes();
        var RamTotal = Math.Round((double)(phyRam.Total / 1024 / 1024 / 1024), 1);
        var RamAvailable = Math.Round((double)(phyRam.Available / 1024 / 1024 / 1024), 1);
        var RamGameActual = Math.Round(Math.Min(RamGame, RamAvailable), 5);
        var RamUsed = Math.Round(RamTotal - RamAvailable, 5);
        var RamEmpty = Math.Round(ModBase.MathClamp(RamTotal - RamUsed - RamGame, 0d, 1000d), 1);
        // 设置最大可用内存
        if (RamTotal <= 1.5d)
            SliderRamCustom.MaxValue = (int)Math.Round(Math.Max(Math.Floor((RamTotal - 0.3d) / 0.1d), 1d));
        else if (RamTotal <= 8d)
            SliderRamCustom.MaxValue = (int)Math.Round(Math.Floor((RamTotal - 1.5d) / 0.5d) + 12d);
        else if (RamTotal <= 16d)
            SliderRamCustom.MaxValue = (int)Math.Round(Math.Floor((RamTotal - 8d) / 1d) + 25d);
        else
            SliderRamCustom.MaxValue = (int)Math.Round(Math.Floor((RamTotal - 16d) / 2d) + 33d);
        // 设置文本
        LabRamGame.Text = $"{Lang.Number(RamGame, "N1")} GB{(RamGame != RamGameActual ? $" (可用 {Lang.Number(RamGameActual, "N1")} GB)" : "")}";
        LabRamUsed.Text = $"{Lang.Number(RamUsed, "N1")} GB";
        LabRamTotal.Text = $" / {Lang.Number(RamTotal, "N1")} GB";
        LabRamWarn.Visibility =
            RamGame == 1d && !ModJava.IsGameSet64BitJava(PageInstanceLeft.Instance) && !ModBase.Is32BitSystem &&
            ModJava.Javas.ExistAnyJava()
                ? Visibility.Visible
                : Visibility.Collapsed;
        HintRamTooHigh.Visibility = RamGame / RamTotal > 0.75d ? Visibility.Visible : Visibility.Collapsed;
        if (ShowAnim)
        {
            // 宽度动画
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaGridLengthWidth(ColumnRamUsed, RamUsed - ColumnRamUsed.Width.Value, 800,
                        Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong)),
                    ModAnimation.AaGridLengthWidth(ColumnRamGame, RamGameActual - ColumnRamGame.Width.Value, 800,
                        Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong)),
                    ModAnimation.AaGridLengthWidth(ColumnRamEmpty, RamEmpty - ColumnRamEmpty.Width.Value, 800,
                        Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong))
                }, "VersionSetup Ram Grid");
        }
        else
        {
            // 宽度设置
            ColumnRamUsed.Width = new GridLength(RamUsed, GridUnitType.Star);
            ColumnRamGame.Width = new GridLength(RamGameActual, GridUnitType.Star);
            ColumnRamEmpty.Width = new GridLength(RamEmpty, GridUnitType.Star);
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
                        }, "VersionSetup Ram TextLeft");
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
                        }, "VersionSetup Ram TextLeft");
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
                        }, "VersionSetup Ram TextLeft");
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
                (RamTextRight != Right || ModAnimation.AniIsRun("VersionSetup Ram TextRight")))
            {
                // 需要动画
                ModAnimation.AniStart(
                    new[]
                    {
                        ModAnimation.AaX(LabRamGame, TotalWidth - LabGameWidth - LabRamGame.Margin.Left, 100,
                            Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)),
                        ModAnimation.AaX(LabRamGameTitle, TotalWidth - LabGameTitleWidth - LabRamGameTitle.Margin.Left,
                            100, Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak))
                    }, "VersionSetup Ram TextRight");
            }
            else
            {
                // 不需要动画
                LabRamGame.Margin = new Thickness(TotalWidth - LabGameWidth, 3d, 0d, 0d);
                LabRamGameTitle.Margin = new Thickness(TotalWidth - LabGameTitleWidth, 0d, 0d, 5d);
            }
        }
        else if (ModAnimation.AniControlEnabled == 0 &&
                 (RamTextRight != Right || ModAnimation.AniIsRun("VersionSetup Ram TextRight")))
        {
            // 需要动画
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaX(LabRamGame, 2d + RectUsedWidth - LabRamGame.Margin.Left, 100,
                        Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)),
                    ModAnimation.AaX(LabRamGameTitle, 2d + RectUsedWidth - LabRamGameTitle.Margin.Left, 100,
                        Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak))
                }, "VersionSetup Ram TextRight");
        }
        else
        {
            // 不需要动画
            LabRamGame.Margin = new Thickness(2d + RectUsedWidth, 3d, 0d, 0d);
            LabRamGameTitle.Margin = new Thickness(2d + RectUsedWidth, 0d, 0d, 5d);
        }

        RamTextRight = Right;
    }

    /// <summary>
    ///     获取当前设置的 RAM 值。单位为 GB。
    /// </summary>
    public static double GetRam(ModMinecraft.McInstance Version, bool? Is32BitJava = default)
    {
        var instancePath = Version?.PathInstance;
        // 跟随全局设置
        if (Config.Instance.MemorySolution[instancePath] == 2)
            return PageSetupLaunch.GetRam(Version, true, Is32BitJava);

        // ------------------------------------------
        // 修改下方代码时需要一并修改 PageSetupLaunch
        // ------------------------------------------

        // 使用当前实例的设置
        var RamGive = default(double);
        if (Config.Instance.MemorySolution[instancePath] == 0)
        {
            // 自动配置
            var RamAvailable =
                Math.Round((double)(KernelInterop.GetAvailablePhysicalMemoryBytes() / 1024 / 1024 / 1024 * 10)) / 10;
            // 确定需求的内存值
            double RamMininum; // 无论如何也需要保证的最低限度内存
            double RamTarget1; // 估计能勉强带动了的内存
            double RamTarget2; // 估计没啥问题了的内存
            double RamTarget3; // 安装过多附加组件需要的内存
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
            if (RamAvailable >= 0.1d)
            {
                // 预分配内存，阶段二，T1 ~ T2，70%
                RamDelta = RamTarget2 - RamTarget1;
                RamGive += Math.Min(RamAvailable * 0.7d, RamDelta);
                RamAvailable -= RamDelta / 0.7d;
                if (RamAvailable >= 0.1d)
                {
                    // 预分配内存，阶段三，T2 ~ T3，40%
                    RamDelta = RamTarget3 - RamTarget2;
                    RamGive += Math.Min(RamAvailable * 0.4d, RamDelta);
                    RamAvailable -= RamDelta / 0.4d;
                    if (RamAvailable >= 0.1d)
                    {
                        // 预分配内存，阶段四，T3 ~ T3 * 2，15%
                        RamDelta = RamTarget3;
                        RamGive += Math.Min(RamAvailable * 0.15d, RamDelta);
                        RamAvailable -= RamDelta / 0.15d;
                    }
                }
            }

            // 不低于最低值
            RamGive = Math.Round(Math.Max(RamGive, RamMininum), 1);
        }
        else
        {
            // 手动配置
            var Value = Config.Instance.CustomMemorySize[instancePath];
            if (Value <= 12)
                RamGive = Value * 0.1d + 0.3d;
            else if (Value <= 25)
                RamGive = (Value - 12) * 0.5d + 1.5d;
            else if (Value <= 33)
                RamGive = (Value - 25) * 1 + 8;
            else
                RamGive = (Value - 33) * 2 + 16;
        }

        // 若使用 32 位 Java，则限制为 1G
        if (Is32BitJava ?? !ModJava.IsGameSet64BitJava(PageInstanceLeft.Instance))
            RamGive = Math.Min(1d, RamGive);
        return RamGive;
    }

    #endregion

    #region 服务器

    // 全局
    private int ComboServerLoginLast;

    private void ComboServerLogin_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (ModAnimation.AniControlEnabled != 0)
            return;
        ServerLogin(ComboServerLoginRequire.SelectedIndex);
        if (TextServerAuthServer.IsValidated)
            BtnServerAuthLock.IsEnabled = true;
        else
            BtnServerAuthLock.IsEnabled = false;
        if ((ComboServerLoginRequire.SelectedIndex == 2 || ComboServerLoginRequire.SelectedIndex == 3) &&
            !TextServerAuthServer.IsValidated)
            return;
        if (ComboServerLoginLast == ComboServerLoginRequire.SelectedIndex)
            return;
        ComboServerLoginLast = ComboServerLoginRequire.SelectedIndex;
        Config.InstanceAuth.LoginRequirementSolution[PageInstanceLeft.Instance.PathInstance] = ComboServerLoginRequire.SelectedIndex;
    }

    private void TextServerAuthServer_MouseLeave(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TextServerAuthServer.Text))
            return;
        if (!(TextServerAuthServer.Text.EndsWithF("/api/yggdrasil/") ||
              TextServerAuthServer.Text.EndsWithF("/api/yggdrasil")))
        {
            if (TextServerAuthServer.Text.EndsWithF("/"))
            {
                TextServerAuthServer.Text = $"{TextServerAuthServer.Text}api/yggdrasil";
                ModMain.Hint("已自动格式化验证服务器地址！");
            }
            else
            {
                TextServerAuthServer.Text = $"{TextServerAuthServer.Text}/api/yggdrasil";
                ModMain.Hint("已自动格式化验证服务器地址！");
            }
        }

        if (TextServerAuthServer.Text.EndsWithF("/api/yggdrasil/"))
        {
            TextServerAuthServer.Text = TextServerAuthServer.Text.BeforeLast("/");
            ModMain.Hint("已自动格式化验证服务器地址！");
        }

        ComboServerLoginLast = ComboServerLoginRequire.SelectedIndex;
        ComboChange(ComboServerLoginRequire, null);
    }

    public void ServerLogin(int Type)
    {
        LabServerAuthName.Visibility = Type == 2 || Type == 3 ? Visibility.Visible : Visibility.Collapsed;
        TextServerAuthName.Visibility = Type == 2 || Type == 3 ? Visibility.Visible : Visibility.Collapsed;
        LabServerAuthRegister.Visibility = Type == 2 || Type == 3 ? Visibility.Visible : Visibility.Collapsed;
        TextServerAuthRegister.Visibility = Type == 2 || Type == 3 ? Visibility.Visible : Visibility.Collapsed;
        LabServerAuthServer.Visibility = Type == 2 || Type == 3 ? Visibility.Visible : Visibility.Collapsed;
        TextServerAuthServer.Visibility = Type == 2 || Type == 3 ? Visibility.Visible : Visibility.Collapsed;
        BtnServerAuthLittle.Visibility = Type == 2 || Type == 3 ? Visibility.Visible : Visibility.Collapsed;
        BtnServerNewProfile.Visibility = Type == 2 || Type == 3 ? Visibility.Visible : Visibility.Collapsed;
        if (Type == 0 || Type == 1)
            BtnServerAuthLock.Visibility = Visibility.Collapsed;
        else
            BtnServerAuthLock.Visibility = Visibility.Visible;
        if (Config.InstanceAuth.AuthLocked[PageInstanceLeft.Instance.PathInstance])
        {
            HintServerLoginLock.Visibility = Visibility.Visible;
            ComboServerLoginRequire.IsEnabled = false;
            TextServerAuthServer.IsEnabled = false;
            TextServerAuthName.IsEnabled = false;
            TextServerAuthRegister.IsEnabled = false;
            BtnServerAuthLittle.IsEnabled = false;
        }
        else
        {
            HintServerLoginLock.Visibility = Visibility.Collapsed;
            ComboServerLoginRequire.IsEnabled = true;
            TextServerAuthServer.IsEnabled = true;
            TextServerAuthName.IsEnabled = true;
            TextServerAuthRegister.IsEnabled = true;
            BtnServerAuthLittle.IsEnabled = true;
        }

        CardServer.TriggerForceResize();
        // 避免正版验证和离线验证出现此提示
        if (Type != 2 && Type != 3)
        {
            LabServerAuthServerSecurity.Visibility = Visibility.Collapsed;
            LabServerAuthServerSecurityCL.Visibility = Visibility.Collapsed;
            LabServerAuthServerSecurityVerify.Visibility = Visibility.Collapsed;
        }
        // 如果开头为 http:// 给予警告
        else if (TextServerAuthServer.Text.StartsWithF("https://"))
        {
            LabServerAuthServerSecurity.Visibility = Visibility.Collapsed;
            LabServerAuthServerSecurityVerify.Visibility = Visibility.Visible;
            LabServerAuthServerSecurityCL.Visibility = Visibility.Visible;
        }
        else if (TextServerAuthServer.Text.StartsWithF("http://"))
        {
            LabServerAuthServerSecurity.Visibility = Visibility.Visible;
            LabServerAuthServerSecurityCL.Visibility = Visibility.Visible;
            LabServerAuthServerSecurityVerify.Visibility = Visibility.Collapsed;
        }
        else
        {
            LabServerAuthServerSecurity.Visibility = Visibility.Collapsed;
            LabServerAuthServerSecurityVerify.Visibility = Visibility.Collapsed;
            LabServerAuthServerSecurityCL.Visibility = Visibility.Collapsed;
        }
    }

    // LittleSkin
    private void BtnServerAuthLittle_Click(object sender, MouseButtonEventArgs e)
    {
        if (!string.IsNullOrEmpty(TextServerAuthServer.Text) &&
            TextServerAuthServer.Text != "https://littleskin.cn/api/yggdrasil" && ModMain.MyMsgBox(
                """
                即将把第三方登录设置覆盖为 LittleSkin 登录。
                除非你是服主，或者服主要求你这样做，否则请不要继续。

                是否确实需要覆盖当前设置？
                """, "设置覆盖确认", "继续", Lang.Text("Common.Action.Cancel")) == 2)
            return;
        TextServerAuthServer.Text = "https://littleskin.cn/api/yggdrasil";
        TextServerAuthRegister.Text = "https://littleskin.cn/auth/register";
        TextServerAuthName.Text = "LittleSkin 登录";
    }

    // 锁定设置
    private void BtnServerAuthLock_Click(object sender, MouseButtonEventArgs e)
    {
        if (ModMain.MyMsgBox(
                $"你正在选择锁定此实例的验证方式。锁定之后，将无法再更改此实例的验证方式要求，启动此实例将必须使用指定的验证方式。{"\r\n"}此功能可能会帮助一些服主吧。{"\r\n"}是否继续？",
                "锁定验证方式确认", Lang.Text("Common.Action.Confirm"), Lang.Text("Common.Action.Cancel"), IsWarn: true) == 1)
        {
            Config.InstanceAuth.AuthLocked[PageInstanceLeft.Instance.PathInstance] = true;
            Reload();
        }
    }

    // 跳转新建档案
    private void BtnServerNewProfile_Click(object sender, MouseButtonEventArgs e)
    {
        ModMain.FrmMain.PageChange(new FormMain.PageStackData { Page = FormMain.PageType.Launch });
        PageLoginAuth.DraggedAuthServer = TextServerAuthServer.Text;
        ModBase.RunInNewThread(() =>
        {
            Thread.Sleep(150);
            ModBase.RunInUi(() => ModMain.FrmLaunchLeft.RefreshPage(true, ModLaunch.McLoginType.Auth));
        });
    }

    private static void TextServerEnter_Change(object sender, TextChangedEventArgs e)
    {
        if (sender is MyTextBox textBox) textBox.Text = textBox.Text.Replace("：", ":");
    }

    #endregion

    #region Java 选择

    // 刷新 Java 下拉框显示
    public void RefreshJavaComboBox()
    {
        if (ComboArgumentJava is null)
            return;

        // 获取实例的 Java 偏好（已兼容新旧格式）
        var preference = ModJava.GetInstanceJavaPreference(PageInstanceLeft.Instance);

        // === 1. 初始化固定选项（使用类型安全的 Tag） ===
        ComboArgumentJava.Items.Clear();

        // 选项 0: 跟随全局设置
        ComboArgumentJava.Items.Add(new MyComboBoxItem
        {
            Content = "跟随全局设置",
            Tag = new UseGlobalPreference()
        });

        // 选项 1: 自动选择
        ComboArgumentJava.Items.Add(new MyComboBoxItem
        {
            Content = "自动选择合适的 Java",
            Tag = new AutoSelect() // Nothing 表示自动选择
        });

        // 选项 2: 相对路径选项
        MyComboBoxItem relativePathItem;
        if (preference is UseRelativePath)
        {
            var relPref = (UseRelativePath)preference;
            var absPath = Path.GetFullPath(Path.Combine(Basics.ExecutableDirectory, relPref.RelativePath));
            var javaEntry = ModJava.Javas.Get(absPath);

            if (Files.IsPathWithinDirectory(absPath, Basics.ExecutableDirectory) && javaEntry is not null &&
                javaEntry.IsEnabled)
                // 有效路径：显示具体 Java 信息
                relativePathItem = new MyComboBoxItem
                {
                    Content = $"启动器目录下的 Java | {javaEntry}",
                    Tag = new UseRelativePath(relPref.RelativePath),
                    ToolTip = $"相对路径: {relPref.RelativePath}{"\r\n"}解析路径: {absPath}"
                };
            else
                // 无效路径：提示用户重新选择
                relativePathItem = new MyComboBoxItem
                {
                    Content = "选择启动器目录下的 Java（当前路径无效）",
                    Tag = new UseRelativePath(relPref.RelativePath),
                    ToolTip = $"无效路径: {absPath}{"\r\n"}点击此项重新选择有效 Java"
                };
        }
        else
        {
            // 未配置相对路径：使用默认模板
            relativePathItem = new MyComboBoxItem
            {
                Content = "选择启动器目录下的 Java",
                Tag = new UseRelativePath(@"jre\bin\java.exe"),
                ToolTip = "将选择相对于实例目录的 Java 路径"
            };
        }

        ComboArgumentJava.Items.Add(relativePathItem);

        // === 2. 添加所有可用 Java 运行时 ===
        MyComboBoxItem selectedItem = null;
        try
        {
            foreach (var curJava in ModJava.Javas.GetSortedJavaList())
            {
                var item = new MyComboBoxItem
                {
                    Content = curJava.ToString(),
                    ToolTip =
                        $"路径: {curJava.Installation.JavaExePath}\r\n版本: {curJava.Installation.Version}\r\n来源: {curJava.Source}",
                    Tag = curJava
                };
                ToolTipService.SetInitialShowDelay(item, 300);
                ToolTipService.SetBetweenShowDelay(item, 100);
                ComboArgumentJava.Items.Add(item);
            }
        }
        catch (Exception ex)
        {
            Config.Instance.SelectedJava[PageInstanceLeft.Instance.PathInstance] = "使用全局设置";
            ModBase.Log(ex, "更新实例设置 Java 下拉框失败", ModBase.LogLevel.Feedback);
            ComboArgumentJava.Items.Clear();
            ComboArgumentJava.Items.Add(new MyComboBoxItem
            {
                Content = "列表加载失败，请重试",
                IsEnabled = false
            });
            ComboArgumentJava.SelectedIndex = 0;
            RefreshRam(true);
            return;
        }

        // === 3. 根据当前偏好设置选中项（优先使用新格式 preference） ===
        if (preference is null)
        {
            // 自动选择
            selectedItem = ComboArgumentJava.Items[1] as MyComboBoxItem;
        }
        else if (preference is UseGlobalPreference)
        {
            selectedItem = ComboArgumentJava.Items[0] as MyComboBoxItem;
        }
        else if (preference is UseRelativePath)
        {
            selectedItem = ComboArgumentJava.Items[2] as MyComboBoxItem;
        }
        else if (preference is ExistingJava)
        {
            var existPref = (ExistingJava)preference;
            // 在 Java 列表中查找匹配项（从索引 3 开始）
            for (int i = 3, loopTo = ComboArgumentJava.Items.Count - 1; i <= loopTo; i++)
            {
                var item = ComboArgumentJava.Items[i] as MyComboBoxItem;
                if (item is not null && item.Tag is JavaEntry)
                {
                    var javaEntry = (JavaEntry)item.Tag;
                    if (string.Equals(javaEntry.Installation.JavaExePath, existPref.JavaExePath,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        selectedItem = item;
                        break;
                    }
                }
            }
        }

        // 降级处理：无匹配项时回退到自动选择
        if (selectedItem is null && ComboArgumentJava.Items.Count > 1)
            selectedItem = ComboArgumentJava.Items[1] as MyComboBoxItem;

        // 设置选中项
        if (selectedItem is not null) ComboArgumentJava.SelectedItem = selectedItem;

        // === 4. 无可用 Java 时的降级处理 ===
        if (!ModJava.Javas.ExistAnyJava() && ComboArgumentJava.Items.Count <= 3)
        {
            ComboArgumentJava.Items.Clear();
            var noJavaItem = new MyComboBoxItem
            {
                Content = "未检测到可用的 Java 运行时",
                ToolTip = "请在设置中手动指定 Java 路径，或点击'扫描'按钮重新检测",
                IsEnabled = false
            };
            ComboArgumentJava.Items.Add(noJavaItem);
            ComboArgumentJava.SelectedItem = noJavaItem;
        }

        // === 5. 刷新关联控件 ===
        RefreshRam(true);
    }

    // 阻止在无效状态下展开下拉框
    private void ComboArgumentJava_DropDownOpened(object? sender, EventArgs e)
    {
        if (ComboArgumentJava.SelectedItem is null)
        {
            ComboArgumentJava.IsDropDownOpen = false;
            return;
        }

        var firstItem = ComboArgumentJava.Items[0] as MyComboBoxItem;
        if (firstItem is not null &&
            ((string)firstItem.Content == "未检测到可用的 Java 运行时" ||
             (string)firstItem.Content == "列表加载失败，请重试"))
            ComboArgumentJava.IsDropDownOpen = false;
    }

    // 下拉框选择更改处理（保存新格式配置）
    private void JavaSelectionUpdate(object sender, SelectionChangedEventArgs e)
    {
        if (ModAnimation.AniControlEnabled != 0)
            return;
        if (ComboArgumentJava.SelectedItem is null)
            return;

        var selectedItem = ComboArgumentJava.SelectedItem as MyComboBoxItem;
        if (selectedItem is null || (selectedItem.Tag is null &&
                                     (string)selectedItem.Content != "自动选择合适的 Java"))
            return;

        JavaPreference preference = default;
        var logMessage = "";

        // 根据 Tag 类型生成偏好对象
        if (selectedItem.Tag is null)
        {
            // 自动选择：存储空字符串
            preference = new AutoSelect();
            logMessage = "[Java] 修改实例 Java 选择设置：自动选择";
        }
        else if (selectedItem.Tag is UseGlobalPreference)
        {
            preference = new UseGlobalPreference();
            logMessage = "[Java] 修改实例 Java 选择设置：跟随全局设置";
        }
        else if (selectedItem.Tag is UseRelativePath)
        {
            // 相对路径：需要用户选择实际文件
            var ret = SystemDialogs.SelectFile("Java 程序(java.exe)|java.exe", "选择 Java 程序", Basics.ExecutableDirectory);
            if (string.IsNullOrWhiteSpace(ret))
                // 用户取消，不保存配置，保持原选择
                return;

            ret = Path.GetFullPath(ret);
            var relativePath = Path.GetRelativePath(Basics.ExecutableDirectory, ret);

            // 验证路径是否在启动器目录内
            if (!Files.IsPathWithinDirectory(relativePath, Basics.ExecutableDirectory))
            {
                ModMain.Hint("超出路径允许范围，请选择启动器文件夹或其子文件夹下的文件", ModMain.HintType.Critical);
                return;
            }

            preference = new UseRelativePath(relativePath);
            logMessage = $"[Java] 修改实例 Java 选择设置：相对路径 | {relativePath}";
        }
        else if (selectedItem.Tag is JavaEntry)
        {
            var javaEntry = (JavaEntry)selectedItem.Tag;
            preference = new ExistingJava(javaEntry.Installation.JavaExePath);
            logMessage = $"[Java] 修改实例 Java 选择设置：{javaEntry}";
        }

        // 保存配置
        var json = JsonSerializer.Serialize(preference);
        Config.Instance.SelectedJava[PageInstanceLeft.Instance.PathInstance] = json;


        ModBase.Log(logMessage);
        RefreshRam(true);
    }

    #endregion

    #region 其他设置

    // 版本隔离警告
    private bool IsReverting;

    private void ComboArgumentIndieV2_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModAnimation.AniControlEnabled != 0)
            return;
        if (IsReverting)
            return;
        if (ModMain.MyMsgBox(
                """
                调整版本隔离后，你可能得把游戏存档、Mod 等文件手动迁移到新的游戏文件夹中。
                如果修改后发现存档消失，把这项设置改回来就能恢复。
                如果你不会迁移存档，不建议修改这项设置！
                """, "警告", "我知道我在做什么", Lang.Text("Common.Action.Cancel"), IsWarn: true) == 2)
        {
            IsReverting = true;
            ComboArgumentIndieV2.SelectedItem = e.RemovedItems[0];
            IsReverting = false;
        }
        else
        {
            bool newValue = ComboArgumentIndieV2.SelectedIndex == 0;
            Config.Instance.IndieV2[PageInstanceLeft.Instance.PathInstance] = newValue;
        }
    }

    // 游戏窗口
    private void CheckArgumentTitleEmpty_Change(object sender, bool e)
    {
        TextArgumentTitle.HintText = CheckArgumentTitleEmpty.Checked == true ? Lang.Text("Common.Option.Default") : "跟随全局设置";
        CheckBoxChange(sender,e);
    }

    private void TextArgumentTitle_TextChanged(object sender, TextChangedEventArgs e)
    {
        CheckArgumentTitleEmpty.Visibility = TextArgumentTitle.Text.Length > 0 ? Visibility.Collapsed : Visibility.Visible;
        TextBoxChange(sender,e);
    }

    #endregion

    #region 高级设置

    private void TextAdvanceRun_TextChanged(object sender, TextChangedEventArgs e)
    {
        CheckAdvanceRunWait.Visibility = string.IsNullOrEmpty(TextAdvanceRun.Text) ? Visibility.Collapsed : Visibility.Visible;
        TextBoxChange(sender,e);
    }

    private void ComboAdvanceRenderer_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModAnimation.AniControlEnabled != 0)
            return;

        var args = (SelectionChangedEventArgs)e; // 转换事件参数

        if (!States.Hint.Renderer && ComboAdvanceRenderer.SelectedIndex != 0)
        {
            if (ModMain.MyMsgBox("""
                                 修改此项会严重影响游戏的稳定性与性能。如果你不知道你在做什么，不要修改此选项！
                                 你确定要继续修改吗？
                                 """, "警告",
                    "我知道我在做什么", Lang.Text("Common.Action.Cancel"), IsWarn: true) == 2)
            {
                ComboAdvanceRenderer.SelectedItem = args.RemovedItems[0];
            }
            else
            {
                ComboChange(ComboAdvanceRenderer, e);
                States.Hint.Renderer = true;
            }
        }
        else
        {
            ComboChange(ComboAdvanceRenderer, e);
        }
    }

    private void CheckUseDebugLog4j2Config_CheckChanged(object sender, bool e)
    {
        if (ModAnimation.AniControlEnabled != 0)
            return;
        var checkBox = sender as MyCheckBox;
        if (checkBox is null) return;
    
        if (checkBox.Checked.GetValueOrDefault() && !States.Hint.DebugLog4j2Config)
        {
            if (ModMain.MyMsgBox(
                    """
                    本选项会修改游戏日志级别修改为最低，大量日志输出会消耗大量磁盘空间并可能影响游戏性能。这也可能带来一定安全风险。如果你不知道你在做什么，不要修改此选项！
                    你确定要继续修改吗？
                    """, "警告", "我知道我在做什么", Lang.Text("Common.Action.Cancel"), IsWarn: true) == 2)
            {
                checkBox.Checked = false;
            }
            else
            {
                CheckBoxChange(sender, e);
                States.Hint.DebugLog4j2Config = true;
            }
        }
        else
        {
            CheckBoxChange(sender, e);
        }
    }

    #endregion
}
