using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
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

public class ModSetup : IConfigScope
{
    #region 基础

    public IEnumerable<string> CheckScope(IReadOnlySet<string> keys)
    {
        var methods = typeof(ModSetup).GetMethods();
        foreach (var method in methods)
            _methodCache.TryAdd(method.Name, method);
        return methods.Where(method => keys.Contains(method.Name)).Select(method => method.Name);
    }

    public bool Reset(object? argument = null)
    {
        throw new NotSupportedException();
    }

    public bool IsDefault(object? argument = null)
    {
        throw new NotSupportedException();
    }

    public ModSetup()
    {
        ConfigService.RegisterObserver(this, new ConfigObserver(ConfigEvent.Changed, OnConfigChanged));
    }

    private readonly ConcurrentDictionary<string, MethodInfo?> _methodCache = new();

    private void InvokeEventMethod(string key, Func<object> valueGetter)
    {
        var method = _methodCache.GetOrAdd(key, typeof(ModSetup).GetMethod);
        if (method == null) return;
        var para = method.GetParameters();
        if (para.Length < 1) return;
        var paraType = para[0].ParameterType;
        var value = valueGetter();
        var valueType = value.GetType();
        if (valueType != paraType)
        {
            if (valueType.IsEnum) value = (int)value;
            else if (value is string s) value = StringConvertExtension.Convert(s, paraType);
            else if (paraType == typeof(string)) value = value.ConvertToString();
            else
                throw new InvalidCastException(
                    $"{key}: {valueType.FullName} cannot be converted to {paraType.FullName}");
        }

        method.Invoke(this, [value]);
    }

    public void OnConfigChanged(ConfigEventArgs e)
    {
        var key = e.Item.Key;
        InvokeEventMethod(key, () => e.Value ?? GetConfigItem(key).DefaultValueNoType);
    }

    private static ConfigItem GetConfigItem(string key)
    {
        var result = ConfigService.TryGetConfigItemNoType(key, out var item);
        return result ? item! : throw new KeyNotFoundException($"配置项 '{key}' 不存在");
    }

    /// <summary>
    ///     改变某个设置项的值。
    /// </summary>
    public void Set(string key, object value, bool forceReload = false, ModMinecraft.McInstance? instance = null)
    {
        GetConfigItem(key).SetValueNoType(value, instance?.PathInstance);
    }

    /// <summary>
    ///     应用某个设置项的值。
    /// </summary>
    public object Load(string key, bool forceReload = false, ModMinecraft.McInstance? instance = null)
    {
        var value = Get(key, instance);
        InvokeEventMethod(key, () => value);
        return value;
    }
    
    /// <summary>
    /// 写入某个未经加密的设置项。
    /// 若该设置项经过了加密，则会抛出异常。
    /// </summary>
    public void SetSafe(string key, object value, bool forceReload = false, ModMinecraft.McInstance instance = null)
    {
        if (!ConfigService.TryGetConfigItemNoType(key, out ConfigItem item)) return;
        if (item.Source == ConfigSource.SharedEncrypt) throw new InvalidOperationException("禁止写入加密设置项：" + key);
        Set(key, value, forceReload, instance);
    }

    /// <summary>
    /// 获取某个未经加密的设置项的值。
    /// 若该设置项经过了加密，则会抛出异常。
    /// </summary>
    public object GetSafe(string key, ModMinecraft.McInstance instance = null)
    {
        if (!ConfigService.TryGetConfigItemNoType(key, out ConfigItem item)) return null;
        if (item.Source == ConfigSource.SharedEncrypt) throw new InvalidOperationException("禁止读取加密设置项：" + key);
        return Get(key, instance);
    }
    
    /// <summary>
    ///     获取某个设置项的值。
    /// </summary>
    public object Get(string key, ModMinecraft.McInstance? instance = null)
    {
        return GetConfigItem(key).GetValueNoType(instance?.PathInstance);
    }

    /// <summary>
    ///     初始化某个设置项的值。
    /// </summary>
    public void Reset(string key, bool forceReload = false, ModMinecraft.McInstance? instance = null)
    {
        GetConfigItem(key).Reset(instance?.PathInstance);
    }

    /// <summary>
    ///     获取某个设置项的默认值。
    /// </summary>
    public object GetDefault(string key)
    {
        return GetConfigItem(key).DefaultValueNoType;
    }

    /// <summary>
    ///     某个设置项是否从未被设置过。
    /// </summary>
    public bool IsUnset(string key, ModMinecraft.McInstance? instance = null)
    {
        return GetConfigItem(key).IsDefault(instance?.PathInstance);
    }

    #endregion

    #region Launch

    // 切换选择
    public void LaunchInstanceSelect(string Value)
    {
        ModBase.Log("[Setup] 当前选择的 Minecraft 版本：" + Value);
        ModBase.WriteIni(ModMinecraft.McFolderSelected + "PCL.ini", "Version",
            ModMinecraft.McInstanceSelected == null ? "" : ModMinecraft.McInstanceSelected.Name);
    }

    public void LaunchFolderSelect(string Value)
    {
        ModBase.Log("[Setup] 当前选择的 Minecraft 文件夹：" + Value.Replace("$", ModBase.ExePath));
        ModMinecraft.McFolderSelected = Value.Replace("$", ModBase.ExePath);
    }

    // 游戏内存
    public void LaunchRamType(int Type)
    {
        if (ModMain.FrmSetupLaunch is null)
            return;
        ModMain.FrmSetupLaunch.RamType(Type);
    }

    #endregion

    #region Tool

    public void ToolDownloadThread(int Value)
    {
        ModNet.NetTaskThreadLimit = Value + 1;
    }

    public void ToolDownloadSpeed(int Value)
    {
        if (Value <= 14)
            ModNet.NetTaskSpeedLimitHigh = (long)Math.Round((Value + 1) * 0.1d * 1024d * 1024d);
        else if (Value <= 31)
            ModNet.NetTaskSpeedLimitHigh = (long)Math.Round((Value - 11) * 0.5d * 1024d * 1024d);
        else if (Value <= 41)
            ModNet.NetTaskSpeedLimitHigh = (Value - 21) * 1024 * 1024L;
        else
            ModNet.NetTaskSpeedLimitHigh = -1;
    }

    #endregion

    #region UI

    // 启动器
    public void UiLauncherTransparent(int Value)
    {
        ModMain.FrmMain.Opacity = Value / 1000d + 0.4d;
    }

    public void UiLauncherTheme(int Value)
    {
        ThemeManager.ThemeRefresh(Value);
    }

    public void UiBackgroundColorful(bool Value)
    {
        ThemeManager.ThemeRefresh();
    }

    public void UiLockWindowSize(bool Value)
    {
        if (Value)
            ModMain.FrmMain.RemoveResizer();
        else
            ModMain.FrmMain.AddResizer();
    }

    // 视频背景
    public void UiAutoPauseVideo(bool Value)
    {
        if (!Value)
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
    public void UiBackgroundOpacity(int Value)
    {
        ModMain.FrmMain.ImgBack.Opacity = Value / 1000d;
    }

    public void UiBackgroundBlur(int Value)
    {
        if (Value == 0)
            ModMain.FrmMain.ImgBack.Effect = null;
        else
            ModMain.FrmMain.ImgBack.Effect = new BlurEffect { Radius = Value + 1 };
        ModMain.FrmMain.ImgBack.Margin = new Thickness(-(Value + 1) / 1.8d);
    }

    public void UiBackgroundSuit(int Value)
    {
        if (ModMain.FrmMain.ImgBack.Background == null)
            return;
        var Width = ((ImageBrush)ModMain.FrmMain.ImgBack.Background).ImageSource.Width;
        var Height = ((ImageBrush)ModMain.FrmMain.ImgBack.Background).ImageSource.Height;
        if (Value == 0)
        {
            // 智能：当图片较小时平铺，较大时适应
            if (Width < ModMain.FrmMain.PanMain.ActualWidth / 2d && Height < ModMain.FrmMain.PanMain.ActualHeight / 2d)
                Value = 4; // 平铺
            else
                Value = 2; // 适应
        }

        ((ImageBrush)ModMain.FrmMain.ImgBack.Background).TileMode = TileMode.None;
        ((ImageBrush)ModMain.FrmMain.ImgBack.Background).Viewport = new Rect(0d, 0d, 1d, 1d);
        ((ImageBrush)ModMain.FrmMain.ImgBack.Background).ViewportUnits = BrushMappingMode.RelativeToBoundingBox;
        switch (Value)
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
    public void UiFont(string value)
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
    public void UiCustomType(int Value)
    {
        if (ModMain.FrmSetupUI is null)
            return;
        switch (Value)
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
                ModMain.FrmSetupUI.HintCustomWarn.Visibility = States.Hint.UntrustedHomepage ? Visibility.Collapsed : Visibility.Visible;
                ModMain.FrmSetupUI.HintCustom.Text =
                    $"从 PCL 文件夹下的 Custom.xaml 读取主页内容。{"\r\n"}你可以手动编辑该文件，向主页添加文本、图片、常用网站、快捷启动等功能。";
                CustomEventService.SetEventType(ModMain.FrmSetupUI.HintCustom, CustomEvent.EventType.None);
                break;
            }
            case 2: // 联网
            {
                ModMain.FrmSetupUI.PanCustomPreset.Visibility = Visibility.Collapsed;
                ModMain.FrmSetupUI.PanCustomLocal.Visibility = Visibility.Collapsed;
                ModMain.FrmSetupUI.PanCustomNet.Visibility = Visibility.Visible;
                ModMain.FrmSetupUI.HintCustom.Visibility = Visibility.Visible;
                ModMain.FrmSetupUI.HintCustomWarn.Visibility = States.Hint.UntrustedHomepage ? Visibility.Collapsed : Visibility.Visible;
                ModMain.FrmSetupUI.HintCustom.Text =
                    $"从指定网址联网获取主页内容。服主也可以用于动态更新服务器公告。{"\r\n"}如果你制作了稳定运行的联网主页，可以点击这条提示投稿，若合格即可加入预设！";
                CustomEventService.SetEventType(ModMain.FrmSetupUI.HintCustom, CustomEvent.EventType.打开网页);
                CustomEventService.SetEventData(ModMain.FrmSetupUI.HintCustom, "https://github.com/Meloong-Git/PCL/discussions/2528");
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
    public void UiBlur(bool Value)
    {
        ModMain.FrmSetupUI.PanBlurValue.Visibility = Value ? Visibility.Visible : Visibility.Collapsed;
        if (Value)
            UiBlurValue(Config.Preference.Blur.Radius);
        else
            UiBlurValue(0);
    }

    public void UiBlurValue(int Value)
    {
        System.Windows.Application.Current.Resources["BlurRadius"] = Value * 1.0d;
    }

    public void UiBlurSamplingRate(int Value)
    {
        System.Windows.Application.Current.Resources["BlurSamplingRate"] = Value * 0.01d;
    }

    public void UiBlurType(int Value)
    {
        System.Windows.Application.Current.Resources["BlurType"] = (KernelType)Value;
    }

    // 顶部栏
    public void UiLogoType(int Value)
    {
        if (ThemeService.CurrentTheme == ColorTheme.HmclBlue) Value = 4;
        switch (Value)
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
                if (!(ModMain.FrmSetupUI == null))
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
                if (!(ModMain.FrmSetupUI == null))
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
                if (ModMain.FrmSetupUI != null)
                {
                    ModMain.FrmSetupUI.CheckLogoLeft.Visibility = Visibility.Collapsed;
                    ModMain.FrmSetupUI.PanLogoText.Visibility = Visibility.Visible;
                    ModMain.FrmSetupUI.PanLogoChange.Visibility = Visibility.Collapsed;
                }

                ModBase.Setup.Load("UiLogoText", true);
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
                if (ModMain.FrmSetupUI != null)
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
                if (ModMain.FrmSetupUI != null) 
                    ModMain.FrmSetupUI.CheckLogoLeft.Visibility = Visibility.Collapsed;
                ModMain.FrmSetupUI.PanLogoText.Visibility = Visibility.Collapsed;
                ModMain.FrmSetupUI.PanLogoChange.Visibility = Visibility.Collapsed;
                
                break;
        }

        ModBase.Setup.Load("UiLogoLeft", true);
        if (ModMain.FrmSetupUI != null)
            ModMain.FrmSetupUI.CardLogo.TriggerForceResize();
    }

    public void UiLogoText(string Value)
    {
        ModMain.FrmMain.LabTitleLogo.Text = Value;
    }

    public void UiLogoLeft(bool Value)
    {
        ModMain.FrmMain.PanTitleMain.ColumnDefinitions[0].Width = new GridLength(
            Value && Config.Preference.WindowTitleType == LauncherTitleType.None ? 0 : 1,
            GridUnitType.Star);
    }

    public void UiHiddenPageDownload(bool Value)
    {
        PageSetupUI.HiddenRefresh();
    }

    public void UiHiddenPageSetup(bool Value)
    {
        PageSetupUI.HiddenRefresh();
    }

    public void UiHiddenPageTools(bool Value)
    {
        PageSetupUI.HiddenRefresh();
    }

    public void UiHiddenSetupLaunch(bool Value)
    {
        PageSetupUI.HiddenRefresh();
    }

    public void UiHiddenSetupUi(bool Value)
    {
        PageSetupUI.HiddenRefresh();
    }

    public void UiHiddenSetupLauncherLanguage(bool Value)
    {
        PageSetupUI.HiddenRefresh();
    }

    public void UiHiddenSetupLauncherMisc(bool Value)
    {
        PageSetupUI.HiddenRefresh();
    }

    public void UiHiddenSetupGameManage(bool Value)
    {
        PageSetupUI.HiddenRefresh();
    }

    public void UiHiddenSetupJava(bool Value)
    {
        PageSetupUI.HiddenRefresh();
    }

    public void UiHiddenSetupUpdate(bool Value)
    {
        PageSetupUI.HiddenRefresh();
    }

    public void UiHiddenSetupGameLink(bool Value)
    {
        PageSetupUI.HiddenRefresh();
    }

    public void UiHiddenSetupAbout(bool Value)
    {
        PageSetupUI.HiddenRefresh();
    }

    public void UiHiddenSetupFeedback(bool Value)
    {
        PageSetupUI.HiddenRefresh();
    }

    public void UiHiddenSetupLog(bool Value)
    {
        PageSetupUI.HiddenRefresh();
    }

    public void UiHiddenToolsGameLink(bool Value)
    {
        PageSetupUI.HiddenRefresh();
    }

    public void UiHiddenToolsHelp(bool Value)
    {
        PageSetupUI.HiddenRefresh();
    }

    public void UiHiddenToolsTest(bool Value)
    {
        PageSetupUI.HiddenRefresh();
    }

    public void UiHiddenVersionEdit(bool Value)
    {
        PageSetupUI.HiddenRefresh();
    }

    public void UiHiddenVersionExport(bool Value)
    {
        PageSetupUI.HiddenRefresh();
    }

    public void UiHiddenVersionSave(bool Value)
    {
        PageSetupUI.HiddenRefresh();
    }

    public void UiHiddenVersionScreenshot(bool Value)
    {
        PageSetupUI.HiddenRefresh();
    }

    public void UiHiddenVersionMod(bool Value)
    {
        PageSetupUI.HiddenRefresh();
    }

    public void UiHiddenVersionResourcePack(bool Value)
    {
        PageSetupUI.HiddenRefresh();
    }

    public void UiHiddenVersionShader(bool Value)
    {
        PageSetupUI.HiddenRefresh();
    }

    public void UiHiddenVersionSchematic(bool Value)
    {
        PageSetupUI.HiddenRefresh();
    }

    public void UiHiddenVersionServer(bool Value)
    {
        PageSetupUI.HiddenRefresh();
    }

    public void UiHiddenFunctionSelect(bool Value)
    {
        PageSetupUI.HiddenRefresh();
    }

    public void UiHiddenFunctionModUpdate(bool Value)
    {
        PageSetupUI.HiddenRefresh();
    }

    public void UiHiddenFunctionHidden(bool Value)
    {
        PageSetupUI.HiddenRefresh();
    }

    #endregion

    #region System

    // 调试选项
    public void SystemDebugMode(bool Value)
    {
        ModBase.ModeDebug = Value;
    }

    public void SystemDebugAnim(int Value)
    {
        ModAnimation.AniSpeed = Value >= 30 ? 200d : ModBase.MathClamp(Value * 0.1d + 0.1d, 0.1d, 3d);
    }

    public void SystemHttpProxy(string value)
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

    public void SystemHttpProxyType(int value)
    {
        var mode = (HttpProxyManager.ProxyMode)value;
        HttpProxyManager.Instance.Mode = Enum.IsDefined(mode)
            ? mode
            : HttpProxyManager.Instance.Mode;
    }

    public void SystemHttpProxyCustomUsername(string value)
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

    public void SystemHttpProxyCustomPassword(string value)
    {
        var username = Config.Network.HttpProxy.CustomUsername;
        if (!string.IsNullOrEmpty(username))
            HttpProxyManager.Instance.Credentials = new NetworkCredential(username, value);
        else
            HttpProxyManager.Instance.Credentials = null;
    }

    #endregion

    #region Version

    // 游戏内存
    public void VersionRamType(int Type)
    {
        if (ModMain.FrmInstanceSetup is null)
            return;
        ModMain.FrmInstanceSetup.RamType(Type);
    }

    // 服务器
    public void VersionServerLogin(int Type)
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
