using System.Net;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.IO.Net.Http;
using PCL.Core.UI.Theme;
using PCL.Core.Utils.Exts;
using PCL.Network;

namespace PCL;

public class ModSetup
{
    public ModSetup()
    {
        // === Hide Group ===
        ConfigService.RegisterObserver(Config.Preference.Hide,
            new ConfigObserver(ConfigEvent.Changed, _ => PageSetupUI.HiddenRefresh()));

        // === Launch ===
        Config.Launch.MemoryAllocationModeConfig.Observe(new ConfigObserver(ConfigEvent.Changed,
            e => LaunchRamType((int)e.Value!)));
        States.Game.SelectedFolderConfig.Observe(new ConfigObserver(ConfigEvent.Changed,
            e => LaunchFolderSelect((string)(e.Value ?? ""))));
        States.Game.SelectedInstanceConfig.Observe(new ConfigObserver(ConfigEvent.Changed,
            e => LaunchInstanceSelect((string)(e.Value ?? ""))));

        // === Tool ===
        Config.Download.ThreadLimitConfig.Observe(new ConfigObserver(ConfigEvent.Changed,
            e => ToolDownloadThread((int)e.Value!)));
        Config.Download.SpeedLimitConfig.Observe(new ConfigObserver(ConfigEvent.Changed,
            e => ToolDownloadSpeed((int)e.Value!)));

        // === UI - Launcher ===
        Config.Preference.Theme.WindowOpacityConfig.Observe(new ConfigObserver(ConfigEvent.Changed,
            e => UiLauncherTransparent((int)e.Value!)));
        Config.Preference.Theme.ThemeSelectedConfig.Observe(new ConfigObserver(ConfigEvent.Changed,
            e => UiLauncherTheme((int)e.Value!)));
        Config.Preference.Background.BackgroundColorfulConfig.Observe(new ConfigObserver(ConfigEvent.Changed,
            e => UiBackgroundColorful((bool)e.Value!)));
        Config.Preference.LockWindowSizeConfig.Observe(new ConfigObserver(ConfigEvent.Changed,
            e => UiLockWindowSize((bool)e.Value!)));

        // UI - Video Background
        Config.Preference.Background.AutoPauseVideoConfig.Observe(new ConfigObserver(ConfigEvent.Changed,
            e => UiAutoPauseVideo((bool)e.Value!)));

        // UI - Background Image
        Config.Preference.Background.WallpaperOpacityConfig.Observe(new ConfigObserver(ConfigEvent.Changed,
            e => UiBackgroundOpacity((int)e.Value!)));
        Config.Preference.Background.WallpaperBlurRadiusConfig.Observe(new ConfigObserver(ConfigEvent.Changed,
            e => UiBackgroundBlur((int)e.Value!)));
        Config.Preference.Background.WallpaperSuitModeConfig.Observe(new ConfigObserver(ConfigEvent.Changed,
            e => UiBackgroundSuit((int)e.Value!)));

        // UI - Font
        Config.Preference.FontConfig.Observe(new ConfigObserver(ConfigEvent.Changed,
            e => UiFont((string)(e.Value ?? ""))));

        // UI - Homepage
        Config.Preference.Homepage.TypeConfig.Observe(new ConfigObserver(ConfigEvent.Changed,
            e => UiCustomType((int)e.Value!)));

        // UI - Blur
        Config.Preference.Blur.IsEnabledConfig.Observe(new ConfigObserver(ConfigEvent.Changed,
            e => UiBlur((bool)e.Value!)));
        Config.Preference.Blur.RadiusConfig.Observe(new ConfigObserver(ConfigEvent.Changed,
            e => UiBlurValue((int)e.Value!)));
        Config.Preference.Blur.SamplingRateConfig.Observe(new ConfigObserver(ConfigEvent.Changed,
            e => UiBlurSamplingRate((int)e.Value!)));
        Config.Preference.Blur.KernelTypeConfig.Observe(new ConfigObserver(ConfigEvent.Changed,
            e => UiBlurType((int)e.Value!)));

        // UI - Title Bar
        Config.Preference.WindowTitleTypeConfig.Observe(new ConfigObserver(ConfigEvent.Changed,
            e => UiLogoType((int)e.Value!)));
        Config.Preference.WindowTitleCustomTextConfig.Observe(new ConfigObserver(ConfigEvent.Changed,
            e => UiLogoText((string)(e.Value ?? ""))));
        Config.Preference.TopBarLeftAlignConfig.Observe(new ConfigObserver(ConfigEvent.Changed,
            e => UiLogoLeft((bool)e.Value!)));

        // === System ===
        Config.Debug.EnabledConfig.Observe(new ConfigObserver(ConfigEvent.Changed,
            e => SystemDebugMode((bool)e.Value!)));
        Config.Debug.AnimationSpeedConfig.Observe(new ConfigObserver(ConfigEvent.Changed,
            e => SystemDebugAnim((int)e.Value!)));
        Config.Network.HttpProxy.CustomAddressConfig.Observe(new ConfigObserver(ConfigEvent.Changed,
            e => SystemHttpProxy((string)(e.Value ?? ""))));
        Config.Network.HttpProxy.TypeConfig.Observe(new ConfigObserver(ConfigEvent.Changed,
            e => SystemHttpProxyType((int)e.Value!)));
        Config.Network.HttpProxy.CustomUsernameConfig.Observe(new ConfigObserver(ConfigEvent.Changed,
            e => SystemHttpProxyCustomUsername((string)(e.Value ?? ""))));
        Config.Network.HttpProxy.CustomPasswordConfig.Observe(new ConfigObserver(ConfigEvent.Changed,
            e => SystemHttpProxyCustomPassword((string)(e.Value ?? ""))));

        // === Version ===
        Config.Instance.MemorySolutionConfig.Observe(new ConfigObserver(ConfigEvent.Changed,
            e => VersionRamType((int)e.Value!)));
        Config.InstanceAuth.LoginRequirementSolutionConfig.Observe(new ConfigObserver(ConfigEvent.Changed,
            e => VersionServerLogin((int)e.Value!)));
    }

    /// <summary>
    ///     主动应用所有当前配置值。
    /// </summary>
    public static void ApplyAll()
    {
        // Launch
        LaunchRamType(Config.Launch.MemoryAllocationMode);

        // Tool
        ToolDownloadThread(Config.Download.ThreadLimit);
        ToolDownloadSpeed(Config.Download.SpeedLimit);

        // UI - Launcher
        UiLauncherTransparent(Config.Preference.Theme.WindowOpacity);
        UiLauncherTheme(Config.Preference.Theme.ThemeSelected);
        UiBackgroundColorful(Config.Preference.Background.BackgroundColorful);
        UiLockWindowSize(Config.Preference.LockWindowSize);

        // UI - Video Background
        UiAutoPauseVideo(Config.Preference.Background.AutoPauseVideo);

        // UI - Background Image
        UiBackgroundOpacity(Config.Preference.Background.WallpaperOpacity);
        UiBackgroundBlur(Config.Preference.Background.WallpaperBlurRadius);
        UiBackgroundSuit(Config.Preference.Background.WallpaperSuitMode);

        // UI - Font
        UiFont(Config.Preference.Font);

        // UI - Homepage
        UiCustomType(Config.Preference.Homepage.Type);

        // UI - Blur
        if (Config.Preference.Blur.IsEnabled)
        {
            UiBlurValue(Config.Preference.Blur.Radius);
            UiBlurSamplingRate(Config.Preference.Blur.SamplingRate);
            UiBlurType(Config.Preference.Blur.KernelType);
        }
        else
        {
            UiBlurValue(0);
        }

        UiBlur(Config.Preference.Blur.IsEnabled);

        // UI - Title Bar
        UiLogoType((int)Config.Preference.WindowTitleType);
        UiLogoText(Config.Preference.WindowTitleCustomText);
        UiLogoLeft(Config.Preference.TopBarLeftAlign);

        // UI - Hide
        PageSetupUI.HiddenRefresh();

        // System
        SystemDebugMode(Config.Debug.Enabled);
        SystemDebugAnim(Config.Debug.AnimationSpeed);
        SystemHttpProxy(Config.Network.HttpProxy.CustomAddress);
        SystemHttpProxyType(Config.Network.HttpProxy.Type);
        SystemHttpProxyCustomUsername(Config.Network.HttpProxy.CustomUsername);
        SystemHttpProxyCustomPassword(Config.Network.HttpProxy.CustomPassword);
    }

    #region Launch

    // 切换选择
    public static void LaunchInstanceSelect(string value)
    {
        ModBase.Log("[Setup] 当前选择的 Minecraft 版本：" + value);
        ModBase.WriteIni(ModMinecraft.McFolderSelected + "PCL.ini", "Version", value);
    }

    public static void LaunchFolderSelect(string value)
    {
        ModBase.Log("[Setup] 当前选择的 Minecraft 文件夹：" + value.Replace("$", ModBase.ExePath));
        ModMinecraft.McFolderSelected = value.Replace("$", ModBase.ExePath);
    }

    // 游戏内存
    public static void LaunchRamType(int type)
    {
        if (ModMain.FrmSetupLaunch is null)
            return;
        ModMain.FrmSetupLaunch.RamType(type);
    }

    #endregion

    #region Tool

    public static void ToolDownloadThread(int value)
    {
        ModNet.NetTaskThreadLimit = value + 1;
    }

    public static void ToolDownloadSpeed(int value)
    {
        ModNet.NetTaskSpeedLimitHigh = value switch
        {
            <= 14 => (long)Math.Round((value + 1) * 0.1d * 1024d * 1024d),
            <= 31 => (long)Math.Round((value - 11) * 0.5d * 1024d * 1024d),
            <= 41 => (value - 21) * 1024 * 1024L,
            _ => -1
        };
    }

    #endregion

    #region UI

    // 启动器
    public static void UiLauncherTransparent(int value)
    {
        ModMain.FrmMain.Opacity = value / 1000d + 0.4d;
    }

    public static void UiLauncherTheme(int value)
    {
        ThemeManager.ThemeRefresh(value);
    }

    public static void UiBackgroundColorful(bool value)
    {
        ThemeManager.ThemeRefresh();
    }

    public static void UiLockWindowSize(bool value)
    {
        if (value)
            ModMain.FrmMain.RemoveResizer();
        else
            ModMain.FrmMain.AddResizer();
    }

    // 视频背景
    public static void UiAutoPauseVideo(bool value)
    {
        if (!value)
        {
            ModVideoBack.ForcePlay = true;
            ModVideoBack.VideoPlay();
        }
        else
        {
            ModVideoBack.ForcePlay = false;
            if (ModVideoBack.IsGaming)
                ModVideoBack.VideoPause();
        }
    }

    // 背景图片
    public static void UiBackgroundOpacity(int value)
    {
        ModMain.FrmMain.ImgBack.Opacity = value / 1000d;
    }

    public static void UiBackgroundBlur(int value)
    {
        ModMain.FrmMain.ImgBack.Effect = value == 0 ? null : new BlurEffect { Radius = value + 1 };
        ModMain.FrmMain.ImgBack.Margin = new Thickness(-(value + 1) / 1.8d);
    }

    public static void UiBackgroundSuit(int value)
    {
        if (ModMain.FrmMain.ImgBack.Background is null)
            return;
        var width = ((ImageBrush)ModMain.FrmMain.ImgBack.Background).ImageSource.Width;
        var height = ((ImageBrush)ModMain.FrmMain.ImgBack.Background).ImageSource.Height;
        if (value == 0)
        {
            // 智能：当图片较小时平铺，较大时适应
            if (width < ModMain.FrmMain.PanMain.ActualWidth / 2d && height < ModMain.FrmMain.PanMain.ActualHeight / 2d)
                value = 4; // 平铺
            else
                value = 2; // 适应
        }

        ((ImageBrush)ModMain.FrmMain.ImgBack.Background).TileMode = TileMode.None;
        ((ImageBrush)ModMain.FrmMain.ImgBack.Background).Viewport = new Rect(0d, 0d, 1d, 1d);
        ((ImageBrush)ModMain.FrmMain.ImgBack.Background).ViewportUnits = BrushMappingMode.RelativeToBoundingBox;
        switch (value)
        {
            case 1: // 居中
            {
                ModMain.FrmMain.ImgBack.HorizontalAlignment = HorizontalAlignment.Center;
                ModMain.FrmMain.ImgBack.VerticalAlignment = VerticalAlignment.Center;
                ((ImageBrush)ModMain.FrmMain.ImgBack.Background).Stretch = Stretch.None;
                ModMain.FrmMain.ImgBack.Width = ((ImageBrush)ModMain.FrmMain.ImgBack.Background).ImageSource.Width;
                ModMain.FrmMain.ImgBack.Height = ((ImageBrush)ModMain.FrmMain.ImgBack.Background).ImageSource.Height;
                break;
            }
            case 2: // 适应
            {
                ModMain.FrmMain.ImgBack.HorizontalAlignment = HorizontalAlignment.Stretch;
                ModMain.FrmMain.ImgBack.VerticalAlignment = VerticalAlignment.Stretch;
                ((ImageBrush)ModMain.FrmMain.ImgBack.Background).Stretch = Stretch.UniformToFill;
                ModMain.FrmMain.ImgBack.Width = double.NaN;
                ModMain.FrmMain.ImgBack.Height = double.NaN;
                break;
            }
            case 3: // 拉伸
            {
                ModMain.FrmMain.ImgBack.HorizontalAlignment = HorizontalAlignment.Stretch;
                ModMain.FrmMain.ImgBack.VerticalAlignment = VerticalAlignment.Stretch;
                ((ImageBrush)ModMain.FrmMain.ImgBack.Background).Stretch = Stretch.Fill;
                ModMain.FrmMain.ImgBack.Width = double.NaN;
                ModMain.FrmMain.ImgBack.Height = double.NaN;
                break;
            }
            case 4: // 平铺
            {
                ModMain.FrmMain.ImgBack.HorizontalAlignment = HorizontalAlignment.Stretch;
                ModMain.FrmMain.ImgBack.VerticalAlignment = VerticalAlignment.Stretch;
                ((ImageBrush)ModMain.FrmMain.ImgBack.Background).Stretch = Stretch.None;
                ((ImageBrush)ModMain.FrmMain.ImgBack.Background).TileMode = TileMode.Tile;
                ((ImageBrush)ModMain.FrmMain.ImgBack.Background).Viewport = new Rect(0d, 0d,
                    ((ImageBrush)ModMain.FrmMain.ImgBack.Background).ImageSource.Width,
                    ((ImageBrush)ModMain.FrmMain.ImgBack.Background).ImageSource.Height);
                ((ImageBrush)ModMain.FrmMain.ImgBack.Background).ViewportUnits = BrushMappingMode.Absolute;
                ModMain.FrmMain.ImgBack.Width = double.NaN;
                ModMain.FrmMain.ImgBack.Height = double.NaN;
                break;
            }
            case 5: // 左上
            {
                ModMain.FrmMain.ImgBack.HorizontalAlignment = HorizontalAlignment.Left;
                ModMain.FrmMain.ImgBack.VerticalAlignment = VerticalAlignment.Top;
                ((ImageBrush)ModMain.FrmMain.ImgBack.Background).Stretch = Stretch.None;
                ModMain.FrmMain.ImgBack.Width = ((ImageBrush)ModMain.FrmMain.ImgBack.Background).ImageSource.Width;
                ModMain.FrmMain.ImgBack.Height = ((ImageBrush)ModMain.FrmMain.ImgBack.Background).ImageSource.Height;
                break;
            }
            case 6: // 右上
            {
                ModMain.FrmMain.ImgBack.HorizontalAlignment = HorizontalAlignment.Right;
                ModMain.FrmMain.ImgBack.VerticalAlignment = VerticalAlignment.Top;
                ((ImageBrush)ModMain.FrmMain.ImgBack.Background).Stretch = Stretch.None;
                ModMain.FrmMain.ImgBack.Width = ((ImageBrush)ModMain.FrmMain.ImgBack.Background).ImageSource.Width;
                ModMain.FrmMain.ImgBack.Height = ((ImageBrush)ModMain.FrmMain.ImgBack.Background).ImageSource.Height;
                break;
            }
            case 7: // 左下
            {
                ModMain.FrmMain.ImgBack.HorizontalAlignment = HorizontalAlignment.Left;
                ModMain.FrmMain.ImgBack.VerticalAlignment = VerticalAlignment.Bottom;
                ((ImageBrush)ModMain.FrmMain.ImgBack.Background).Stretch = Stretch.None;
                ModMain.FrmMain.ImgBack.Width = ((ImageBrush)ModMain.FrmMain.ImgBack.Background).ImageSource.Width;
                ModMain.FrmMain.ImgBack.Height = ((ImageBrush)ModMain.FrmMain.ImgBack.Background).ImageSource.Height;
                break;
            }
            case 8: // 右下
            {
                ModMain.FrmMain.ImgBack.HorizontalAlignment = HorizontalAlignment.Right;
                ModMain.FrmMain.ImgBack.VerticalAlignment = VerticalAlignment.Bottom;
                ((ImageBrush)ModMain.FrmMain.ImgBack.Background).Stretch = Stretch.None;
                ModMain.FrmMain.ImgBack.Width = ((ImageBrush)ModMain.FrmMain.ImgBack.Background).ImageSource.Width;
                ModMain.FrmMain.ImgBack.Height = ((ImageBrush)ModMain.FrmMain.ImgBack.Background).ImageSource.Height;
                break;
            }
        }
    }

    // 字体
    public static void UiFont(string value)
    {
        try
        {
            ModBase.SetLaunchFont(value);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "字体加载失败", ModBase.LogLevel.Hint);
        }
    }

    // 主页
    public static void UiCustomType(int value)
    {
        if (ModMain.FrmSetupUI is null)
            return;
        switch (value)
        {
            case 0: // 无
            {
                ModMain.FrmSetupUI.PanCustomPreset.Visibility = Visibility.Collapsed;
                ModMain.FrmSetupUI.PanCustomLocal.Visibility = Visibility.Collapsed;
                ModMain.FrmSetupUI.PanCustomNet.Visibility = Visibility.Collapsed;
                ModMain.FrmSetupUI.HintCustom.Visibility = Visibility.Collapsed;
                ModMain.FrmSetupUI.HintCustomWarn.Visibility = Visibility.Collapsed;
                break;
            }
            case 1: // 本地
            {
                ModMain.FrmSetupUI.PanCustomPreset.Visibility = Visibility.Collapsed;
                ModMain.FrmSetupUI.PanCustomLocal.Visibility = Visibility.Visible;
                ModMain.FrmSetupUI.PanCustomNet.Visibility = Visibility.Collapsed;
                ModMain.FrmSetupUI.HintCustom.Visibility = Visibility.Visible;
                ModMain.FrmSetupUI.HintCustomWarn.Visibility =
                    States.Hint.UntrustedHomepage ? Visibility.Collapsed : Visibility.Visible;
                ModMain.FrmSetupUI.HintCustom.Text =
                    "从 PCL 文件夹下的 Custom.xaml 读取主页内容。\r\n你可以手动编辑该文件，向主页添加文本、图片、常用网站、快捷启动等功能。";
                CustomEventService.SetEventType(ModMain.FrmSetupUI.HintCustom, CustomEvent.EventType.None);
                break;
            }
            case 2: // 联网
            {
                ModMain.FrmSetupUI.PanCustomPreset.Visibility = Visibility.Collapsed;
                ModMain.FrmSetupUI.PanCustomLocal.Visibility = Visibility.Collapsed;
                ModMain.FrmSetupUI.PanCustomNet.Visibility = Visibility.Visible;
                ModMain.FrmSetupUI.HintCustom.Visibility = Visibility.Visible;
                ModMain.FrmSetupUI.HintCustomWarn.Visibility =
                    States.Hint.UntrustedHomepage ? Visibility.Collapsed : Visibility.Visible;
                ModMain.FrmSetupUI.HintCustom.Text =
                    "从指定网址联网获取主页内容。服主也可以用于动态更新服务器公告。\r\n如果你制作了稳定运行的联网主页，可以点击这条提示投稿，若合格即可加入预设！";
                CustomEventService.SetEventType(ModMain.FrmSetupUI.HintCustom, CustomEvent.EventType.打开网页);
                CustomEventService.SetEventData(ModMain.FrmSetupUI.HintCustom,
                    "https://github.com/Meloong-Git/PCL/discussions/2528");
                break;
            }
            case 3: // 预设
            {
                ModMain.FrmSetupUI.PanCustomPreset.Visibility = Visibility.Visible;
                ModMain.FrmSetupUI.PanCustomLocal.Visibility = Visibility.Collapsed;
                ModMain.FrmSetupUI.PanCustomNet.Visibility = Visibility.Collapsed;
                ModMain.FrmSetupUI.HintCustom.Visibility = Visibility.Collapsed;
                ModMain.FrmSetupUI.HintCustomWarn.Visibility = Visibility.Collapsed;
                break;
            }
        }

        ModMain.FrmSetupUI.CardCustom.TriggerForceResize();
    }

    // 高级材质
    public static void UiBlur(bool value)
    {
        if (ModMain.FrmSetupUI is null)
            return;

        ModMain.FrmSetupUI.PanBlurValue.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        UiBlurValue(value ? Config.Preference.Blur.Radius : 0);
    }

    public static void UiBlurValue(int value)
    {
        System.Windows.Application.Current.Resources["BlurRadius"] = value * 1.0d;
    }

    public static void UiBlurSamplingRate(int value)
    {
        System.Windows.Application.Current.Resources["BlurSamplingRate"] = value * 0.01d;
    }

    public static void UiBlurType(int value)
    {
        System.Windows.Application.Current.Resources["BlurType"] = (KernelType)value;
    }

    // 顶部栏
    public static void UiLogoType(int value)
    {
        if (ThemeService.CurrentTheme == ColorTheme.HmclBlue) value = 4;
        switch (value)
        {
            case 0: // 无
            {
                ModMain.FrmMain.ShapeTitleLogo.Visibility = Visibility.Collapsed;
                ModMain.FrmMain.BtnTitleHelp.Visibility = Visibility.Collapsed;
                ModMain.FrmMain.ShapeHMCLTitleLogo.Visibility = Visibility.Collapsed;
                ModMain.FrmMain.LabTitleLogo.Visibility = Visibility.Collapsed;
                ModMain.FrmMain.ImageTitleLogo.Visibility = Visibility.Collapsed;
                ModMain.FrmMain.ImageHMCLTitleLogo.Visibility = Visibility.Collapsed;
                ModMain.FrmMain.CELogo.Visibility = Visibility.Collapsed;
                if (ModMain.FrmSetupUI is not null)
                {
                    ModMain.FrmSetupUI.CheckLogoLeft.Visibility = Visibility.Visible;
                    ModMain.FrmSetupUI.PanLogoText.Visibility = Visibility.Collapsed;
                    ModMain.FrmSetupUI.PanLogoChange.Visibility = Visibility.Collapsed;
                }

                break;
            }
            case 1: // 默认
            {
                ModMain.FrmMain.ShapeTitleLogo.Visibility = Visibility.Visible;
                ModMain.FrmMain.BtnTitleHelp.Visibility = Visibility.Collapsed;
                ModMain.FrmMain.ShapeHMCLTitleLogo.Visibility = Visibility.Collapsed;
                ModMain.FrmMain.LabTitleLogo.Visibility = Visibility.Collapsed;
                ModMain.FrmMain.ImageTitleLogo.Visibility = Visibility.Collapsed;
                ModMain.FrmMain.ImageHMCLTitleLogo.Visibility = Visibility.Collapsed;
                ModMain.FrmMain.CELogo.Visibility = Visibility.Visible;
                if (ModMain.FrmSetupUI is not null)
                {
                    ModMain.FrmSetupUI.CheckLogoLeft.Visibility = Visibility.Collapsed;
                    ModMain.FrmSetupUI.PanLogoText.Visibility = Visibility.Collapsed;
                    ModMain.FrmSetupUI.PanLogoChange.Visibility = Visibility.Collapsed;
                }

                break;
            }
            case 2: // 文本
            {
                ModMain.FrmMain.ShapeTitleLogo.Visibility = Visibility.Collapsed;
                ModMain.FrmMain.BtnTitleHelp.Visibility = Visibility.Collapsed;
                ModMain.FrmMain.ShapeHMCLTitleLogo.Visibility = Visibility.Collapsed;
                ModMain.FrmMain.LabTitleLogo.Visibility = Visibility.Visible;
                ModMain.FrmMain.ImageTitleLogo.Visibility = Visibility.Collapsed;
                ModMain.FrmMain.ImageHMCLTitleLogo.Visibility = Visibility.Collapsed;
                ModMain.FrmMain.CELogo.Visibility = Visibility.Visible;
                if (ModMain.FrmSetupUI is not null)
                {
                    ModMain.FrmSetupUI.CheckLogoLeft.Visibility = Visibility.Collapsed;
                    ModMain.FrmSetupUI.PanLogoText.Visibility = Visibility.Visible;
                    ModMain.FrmSetupUI.PanLogoChange.Visibility = Visibility.Collapsed;
                }

                _ = Config.Preference.WindowTitleCustomText;
                break;
            }
            case 3: // 图片
            {
                ModMain.FrmMain.ShapeTitleLogo.Visibility = Visibility.Collapsed;
                ModMain.FrmMain.BtnTitleHelp.Visibility = Visibility.Collapsed;
                ModMain.FrmMain.ShapeHMCLTitleLogo.Visibility = Visibility.Collapsed;
                ModMain.FrmMain.LabTitleLogo.Visibility = Visibility.Collapsed;
                ModMain.FrmMain.ImageTitleLogo.Visibility = Visibility.Visible;
                ModMain.FrmMain.ImageHMCLTitleLogo.Visibility = Visibility.Collapsed;
                ModMain.FrmMain.CELogo.Visibility = Visibility.Visible;
                if (ModMain.FrmSetupUI is not null)
                {
                    ModMain.FrmSetupUI.CheckLogoLeft.Visibility = Visibility.Collapsed;
                    ModMain.FrmSetupUI.PanLogoText.Visibility = Visibility.Collapsed;
                    ModMain.FrmSetupUI.PanLogoChange.Visibility = Visibility.Visible;
                }

                try
                {
                    ModMain.FrmMain.ImageTitleLogo.Source = ModBase.ExePath + @"PCL\Logo.png";
                }
                catch (Exception ex)
                {
                    ModMain.FrmMain.ImageTitleLogo.Source = null;
                    ModBase.Log(ex, "显示标题栏图片失败", ModBase.LogLevel.Msgbox);
                }

                break;
            }
            case 4: //HMCL (愚人节)
                ModMain.FrmMain.ShapeTitleLogo.Visibility = Visibility.Collapsed;
                ModMain.FrmMain.ShapeHMCLTitleLogo.Visibility = Visibility.Visible;
                ModMain.FrmMain.LabTitleLogo.Visibility = Visibility.Collapsed;
                ModMain.FrmMain.ImageTitleLogo.Visibility = Visibility.Collapsed;
                ModMain.FrmMain.BtnTitleHelp.Visibility = Visibility.Visible;
                ModMain.FrmMain.ImageHMCLTitleLogo.Visibility = Visibility.Visible;
                if (ModMain.FrmSetupUI is not null) 
                {
                    ModMain.FrmSetupUI.CheckLogoLeft.Visibility = Visibility.Collapsed;
                    ModMain.FrmSetupUI.PanLogoText.Visibility = Visibility.Collapsed;
                    ModMain.FrmSetupUI.PanLogoChange.Visibility = Visibility.Collapsed;
                }

                break;
        }

        _ = Config.Preference.TopBarLeftAlign;
        if (ModMain.FrmSetupUI is not null)
            ModMain.FrmSetupUI.CardLogo.TriggerForceResize();
    }

    public static void UiLogoText(string value)
    {
        ModMain.FrmMain.LabTitleLogo.Text = value;
    }

    public static void UiLogoLeft(bool value)
    {
        ModMain.FrmMain.PanTitleMain.ColumnDefinitions[0].Width = new GridLength(
            value && Config.Preference.WindowTitleType == LauncherTitleType.None ? 0 : 1,
            GridUnitType.Star);
    }

    #endregion

    #region System

    // 调试选项
    public static void SystemDebugMode(bool value)
    {
        ModBase.ModeDebug = value;
    }

    public static void SystemDebugAnim(int value)
    {
        ModAnimation.AniSpeed = value >= 30
            ? 200d
            : ModBase.MathClamp(value * 0.1d + 0.1d, 0.1d, 3d);
    }

    public static void SystemHttpProxy(string value)
    {
        if (value.IsNullOrWhiteSpace()) return;
        try
        {
            HttpProxyManager.Instance.CustomProxyAddress = new Uri(value);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "HTTP 代理应用出错");
        }
    }

    public static void SystemHttpProxyType(int value)
    {
        var mode = (HttpProxyManager.ProxyMode)value;
        HttpProxyManager.Instance.Mode = Enum.IsDefined(mode)
            ? mode
            : HttpProxyManager.Instance.Mode;
    }

    public static void SystemHttpProxyCustomUsername(string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            var password = Config.Network.HttpProxy.CustomPassword;
            HttpProxyManager.Instance.Credentials = new NetworkCredential(value, password);
        }
        else
        {
            HttpProxyManager.Instance.Credentials = null;
        }
    }

    public static void SystemHttpProxyCustomPassword(string value)
    {
        var username = Config.Network.HttpProxy.CustomUsername;
        HttpProxyManager.Instance.Credentials = !string.IsNullOrEmpty(username)
            ? new NetworkCredential(username, value)
            : null;
    }

    #endregion

    #region Version

    // 游戏内存
    public static void VersionRamType(int type)
    {
        if (ModMain.FrmInstanceSetup is null)
            return;
        ModMain.FrmInstanceSetup.RamType(type);
    }

    // 服务器
    public static void VersionServerLogin(int type)
    {
        if (ModMain.FrmInstanceSetup is null)
            return;
        // 为第三方登录清空缓存以更新描述
        ModBase.WriteIni(ModMinecraft.McFolderSelected + "PCL.ini", "InstanceCache", "");
        if (PageInstanceLeft.Instance is null)
            return;
        PageInstanceLeft.Instance = new ModMinecraft.McInstance(PageInstanceLeft.Instance.Name).Load();
        ModLoader.LoaderFolderRun(ModMinecraft.McInstanceListLoader, ModMinecraft.McFolderSelected,
            ModLoader.LoaderFolderRunType.ForceRun, 1, @"versions\");
    }

    #endregion
}